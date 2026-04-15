using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SekaiApiModel.CP.Realtime;
using DiarkisServer.Models;

namespace DiarkisServer.Services;

public class RoomService : IRoomService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<RoomService> _logger;
    private const string Prefix = "room:";

    private static readonly ConcurrentDictionary<string, Room> _index = new();

    public RoomService(IMemoryCache cache, ILogger<RoomService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<Room?> CreateRoomAsync(RoomInitialData initialData, string userId)
    {
        var roomId = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var ttl = (uint)(initialData.createOption?.roomTtl ?? 3600);

        var room = new Room
        {
            RoomID = roomId,
            OwnerID = userId,
            RoomCreateTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            TTL = ttl,
            MaxMembers = initialData.createOption?.maxMembers ?? 5,
            IsPrivate = initialData.createOption?.isPrivate ?? false,
            AllowEmpty = initialData.createOption?.allowEmpty ?? false,
            RoomProperty = initialData.roomProperty,
            CreatedAt = now,
            ExpiresAt = now.AddSeconds(ttl)
        };

        room.Players[userId] = new RoomPlayer
        {
            UserID = userId, Index = 0, IsOwner = true,
            PlayerProperty = initialData.playerProperty,
            JoinedAt = now
        };

        CacheRoom(room);
        _logger.LogInformation("Room {RoomId} created by {UserId}", roomId, userId);
        return Task.FromResult<Room?>(room);
    }

    public Task<Room?> GetRoomAsync(string roomId)
    {
        if (_cache.TryGetValue(Prefix + roomId, out Room? room))
        {
            if (room?.ExpiresAt < DateTime.UtcNow) { RemoveRoom(roomId); return Task.FromResult<Room?>(null); }
            return Task.FromResult(room);
        }
        _index.TryRemove(roomId, out _);
        return Task.FromResult<Room?>(null);
    }

    public async Task<bool> JoinRoomAsync(string roomId, string userId, DynamicPropertyPayload? pp)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null || room.Players.Count >= room.MaxMembers) return false;
        if (room.Players.ContainsKey(userId)) return true;

        room.Players[userId] = new RoomPlayer
        {
            UserID = userId, Index = room.Players.Count, IsOwner = false,
            PlayerProperty = pp, JoinedAt = DateTime.UtcNow
        };
        CacheRoom(room);
        _logger.LogInformation("{UserId} joined {RoomId}", userId, roomId);
        return true;
    }

    public async Task<bool> LeaveRoomAsync(string roomId, string userId)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null || !room.Players.Remove(userId)) return false;

        if (room.Players.Count == 0 && !room.AllowEmpty) { RemoveRoom(roomId); return true; }

        if (room.OwnerID == userId && room.Players.Count > 0)
        {
            var next = room.Players.Values.First();
            room.OwnerID = next.UserID;
            next.IsOwner = true;
        }
        CacheRoom(room);
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
                { userId = p.UserID, index = p.Index, playerProperty = p.PlayerProperty }).ToArray(),
            networkObjects = room.NetworkObjects.ToArray()
        };
    }

    public async Task<RoomSyncDataMinimal?> GetRoomStateMinimalAsync(string roomId, string userId)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null || !room.Players.TryGetValue(userId, out var me)) return null;
        return new RoomSyncDataMinimal
        {
            isJoin = true,
            roomCreateTime = room.RoomCreateTime,
            roomId = room.RoomID,
            ownerId = room.OwnerID,
            roomProperty = room.RoomProperty,
            mySyncPlayer = new RoomSyncPlayer { userId = me.UserID, index = me.Index, playerProperty = me.PlayerProperty },
            userIds = room.Players.Keys.ToArray()
        };
    }

    public async Task UpdateRoomPropertyAsync(string roomId, DynamicPropertyPayload prop)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null) return;
        room.RoomProperty = prop;
        CacheRoom(room);
    }

    public async Task UpdatePlayerPropertyAsync(string roomId, string userId, DynamicPropertyPayload prop)
    {
        var room = await GetRoomAsync(roomId);
        if (room == null || !room.Players.TryGetValue(userId, out var p)) return;
        p.PlayerProperty = prop;
        CacheRoom(room);
    }

    public Task<bool> ValidatePrivateRoomAccessAsync(Room room, int? num)
        => Task.FromResult(!room.IsPrivate || room.PrivateRoomNumber == num);

    public Task<bool> ValidatePowerLimitsAsync(Room room, int power)
    {
        if (room.TotalPowerUpperLimit.HasValue && power > room.TotalPowerUpperLimit) return Task.FromResult(false);
        if (room.TotalPowerLowerLimit.HasValue && power < room.TotalPowerLowerLimit) return Task.FromResult(false);
        return Task.FromResult(true);
    }

    public async IAsyncEnumerable<Room> GetAvailableRoomsAsync()
    {
        foreach (var kv in _index)
        {
            var room = await GetRoomAsync(kv.Key);
            if (room is { IsMatchmakingOpen: true } && room.Players.Count < room.MaxMembers)
                yield return room;
        }
    }

    private void CacheRoom(Room room)
    {
        var remaining = room.ExpiresAt!.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero) { RemoveRoom(room.RoomID); return; }
        var opts = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(remaining)
            .RegisterPostEvictionCallback((_, val, _, _) => { if (val is Room r) _index.TryRemove(r.RoomID, out _); });
        _cache.Set(Prefix + room.RoomID, room, opts);
        _index[room.RoomID] = room;
    }

    private void RemoveRoom(string id) { _cache.Remove(Prefix + id); _index.TryRemove(id, out _); }
}
