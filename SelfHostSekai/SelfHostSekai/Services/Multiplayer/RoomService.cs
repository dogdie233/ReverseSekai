using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using SekaiApiModel.CP.Realtime;
using SelfHostSekai.Models.Multiplayer;

namespace SelfHostSekai.Services.Multiplayer;

public class RoomService : IRoomService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RoomService> _logger;
    private const string RoomCacheKeyPrefix = "room:";

    /// <summary>
    /// Global index of all active rooms. Key = RoomID.
    /// Thread-safe: multiple WebSocket sessions may mutate rooms concurrently.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Room> _roomIndex = new();

    public RoomService(IMemoryCache memoryCache, ILogger<RoomService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public Task<Room?> CreateRoomAsync(RoomInitialData initialData, string userId)
    {
        try
        {
            var roomId = Guid.NewGuid().ToString("N");
            var roomCreateTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var ttl = (uint)(initialData.createOption?.roomTtl ?? 3600);

            var room = new Room
            {
                RoomID = roomId,
                OwnerID = userId,
                RoomCreateTime = roomCreateTime,
                Players = new Dictionary<string, RoomPlayer>(),
                RoomProperty = initialData.roomProperty,
                NetworkObjects = new List<NetworkObject>(),
                TTL = ttl,
                MaxMembers = initialData.createOption?.maxMembers ?? 5,
                IsPrivate = initialData.createOption?.isPrivate ?? false,
                AllowEmpty = initialData.createOption?.allowEmpty ?? false,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(ttl)
            };

            var ownerPlayer = new RoomPlayer
            {
                UserID = userId,
                Index = 0,
                IsOwner = true,
                PlayerProperty = initialData.playerProperty,
                PlayerNetworkObjects = new List<NetworkObject>(),
                JoinedAt = DateTime.UtcNow,
                TotalPower = 0
            };

            room.Players[userId] = ownerPlayer;

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(ttl))
                .RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    if (value is Room r)
                        _roomIndex.TryRemove(r.RoomID, out _);
                });

            _memoryCache.Set(RoomCacheKeyPrefix + roomId, room, cacheOptions);
            _roomIndex[roomId] = room;

            _logger.LogInformation("Room created: {RoomId} by {UserId}, max={Max}, ttl={Ttl}s",
                roomId, userId, room.MaxMembers, ttl);
            return Task.FromResult<Room?>(room);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return Task.FromResult<Room?>(null);
        }
    }

    public Task<Room?> GetRoomAsync(string roomId)
    {
        if (_memoryCache.TryGetValue(RoomCacheKeyPrefix + roomId, out Room? room))
        {
            if (room?.ExpiresAt != null && room.ExpiresAt < DateTime.UtcNow)
            {
                RemoveRoom(roomId);
                return Task.FromResult<Room?>(null);
            }
            return Task.FromResult(room);
        }
        // Cache miss but index hit → stale, clean up
        _roomIndex.TryRemove(roomId, out _);
        return Task.FromResult<Room?>(null);
    }

    public async Task<bool> JoinRoomAsync(string roomId, string userId, DynamicPropertyPayload? playerProperty)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null) return false;
        if (room.Players.Count >= room.MaxMembers) return false;
        if (room.Players.ContainsKey(userId)) return true; // idempotent

        var nextIndex = room.Players.Count;
        room.Players[userId] = new RoomPlayer
        {
            UserID = userId,
            Index = nextIndex,
            IsOwner = false,
            PlayerProperty = playerProperty,
            PlayerNetworkObjects = new List<NetworkObject>(),
            JoinedAt = DateTime.UtcNow,
            TotalPower = 0
        };

        ReCache(room);
        _logger.LogInformation("User {UserId} joined room {RoomId} (index={Idx})", userId, roomId, nextIndex);
        return true;
    }

    public async Task<bool> LeaveRoomAsync(string roomId, string userId)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null) return false;
        if (!room.Players.Remove(userId)) return false;

        if (room.Players.Count == 0 && !room.AllowEmpty)
        {
            RemoveRoom(roomId);
            return true;
        }

        // Transfer ownership
        if (room.OwnerID == userId && room.Players.Count > 0)
        {
            var newOwner = room.Players.Values.First();
            room.OwnerID = newOwner.UserID;
            newOwner.IsOwner = true;
            _logger.LogInformation("Owner changed in {RoomId} → {NewOwner}", roomId, newOwner.UserID);
        }

        ReCache(room);
        _logger.LogInformation("User {UserId} left room {RoomId}", userId, roomId);
        return true;
    }

    public async Task<RoomSyncData?> GetRoomStateAsync(string roomId)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null) return null;

        return new RoomSyncData
        {
            isJoin = true,
            roomCreateTime = room.RoomCreateTime,
            roomId = room.RoomID,
            ownerId = room.OwnerID,
            roomProperty = room.RoomProperty,
            players = room.Players.Values.Select(p => new RoomSyncPlayer
            {
                userId = p.UserID,
                index = p.Index,
                playerProperty = p.PlayerProperty
            }).ToArray(),
            networkObjects = room.NetworkObjects.ToArray()
        };
    }

    public async Task<RoomSyncDataMinimal?> GetRoomStateMinimalAsync(string roomId, string userId)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null) return null;
        if (!room.Players.TryGetValue(userId, out var player)) return null;

        return new RoomSyncDataMinimal
        {
            isJoin = true,
            roomCreateTime = room.RoomCreateTime,
            roomId = room.RoomID,
            ownerId = room.OwnerID,
            roomProperty = room.RoomProperty,
            mySyncPlayer = new RoomSyncPlayer
            {
                userId = player.UserID,
                index = player.Index,
                playerProperty = player.PlayerProperty
            },
            userIds = room.Players.Keys.ToArray()
        };
    }

    public async Task UpdateRoomPropertyAsync(string roomId, DynamicPropertyPayload property)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null) return;
        room.RoomProperty = property;
        ReCache(room);
    }

    public async Task UpdatePlayerPropertyAsync(string roomId, string userId, DynamicPropertyPayload property)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null) return;
        if (!room.Players.TryGetValue(userId, out var player)) return;
        player.PlayerProperty = property;
        ReCache(room);
    }

    public Task<bool> ValidatePrivateRoomAccessAsync(Room room, int? privateRoomNumber)
    {
        if (!room.IsPrivate) return Task.FromResult(true);
        if (room.PrivateRoomNumber == null) return Task.FromResult(false);
        return Task.FromResult(room.PrivateRoomNumber == privateRoomNumber);
    }

    public Task<bool> ValidatePowerLimitsAsync(Room room, int playerTotalPower)
    {
        if (room.TotalPowerUpperLimit.HasValue && playerTotalPower > room.TotalPowerUpperLimit)
            return Task.FromResult(false);
        if (room.TotalPowerLowerLimit.HasValue && playerTotalPower < room.TotalPowerLowerLimit)
            return Task.FromResult(false);
        return Task.FromResult(true);
    }

    public async IAsyncEnumerable<Room> GetAvailableRoomsAsync()
    {
        foreach (var kvp in _roomIndex)
        {
            var room = await GetRoomAsync(kvp.Key);
            if (room != null
                && room.IsMatchmakingOpen
                && room.Players.Count < room.MaxMembers)
            {
                yield return room;
            }
        }
    }

    // ── helpers ──

    private void ReCache(Room room)
    {
        var remaining = room.ExpiresAt!.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            RemoveRoom(room.RoomID);
            return;
        }
        var opts = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(remaining)
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                if (value is Room r)
                    _roomIndex.TryRemove(r.RoomID, out _);
            });
        _memoryCache.Set(RoomCacheKeyPrefix + room.RoomID, room, opts);
        _roomIndex[room.RoomID] = room;
    }

    private void RemoveRoom(string roomId)
    {
        _memoryCache.Remove(RoomCacheKeyPrefix + roomId);
        _roomIndex.TryRemove(roomId, out _);
    }
}
