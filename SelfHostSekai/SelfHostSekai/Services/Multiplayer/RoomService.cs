using Microsoft.Extensions.Caching.Memory;
using SekaiApiModel.CP.Realtime;
using SelfHostSekai.Models.Multiplayer;

namespace SelfHostSekai.Services.Multiplayer;

public class RoomService : IRoomService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<RoomService> _logger;
    private const string RoomCacheKeyPrefix = "room:";

    public RoomService(IMemoryCache memoryCache, ILogger<RoomService> logger)
    {
        _memoryCache = memoryCache;
        _logger = logger;
    }


    public async Task<Room?> CreateRoomAsync(RoomInitialData initialData, string userId)
    {
        try
        {
            var roomId = Guid.NewGuid().ToString("N");
            var roomCreateTime = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var room = new Room
            {
                RoomID = roomId,
                OwnerID = userId,
                RoomCreateTime = roomCreateTime,
                Players = new Dictionary<string, RoomPlayer>(),
                RoomProperty = initialData.roomProperty,
                NetworkObjects = new List<NetworkObject>(),
                TTL = (uint)(initialData.createOption?.roomTtl ?? 3600),
                MaxMembers = initialData.createOption?.maxMembers ?? 4,
                IsPrivate = initialData.createOption?.isPrivate ?? false,
                AllowEmpty = initialData.createOption?.allowEmpty ?? false,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(initialData.createOption?.roomTtl ?? 3600)
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
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(room.TTL));

            _memoryCache.Set(RoomCacheKeyPrefix + roomId, room, cacheOptions);

            _logger.LogInformation("Room created: {RoomId} by user {UserId}", roomId, userId);
            return room;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating room");
            return null;
        }
    }

    public async Task<Room?> GetRoomAsync(string roomId)
    {
        try
        {
            if (_memoryCache.TryGetValue(RoomCacheKeyPrefix + roomId, out Room? room))
            {
                if (room?.ExpiresAt != null && room.ExpiresAt < DateTime.UtcNow)
                {
                    _memoryCache.Remove(RoomCacheKeyPrefix + roomId);
                    return null;
                }
                return room;
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room {RoomId}", roomId);
            return null;
        }
    }

    public async Task<bool> JoinRoomAsync(string roomId, string userId, DynamicPropertyPayload? playerProperty)
    {
        try
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                return false;

            if (room.Players.Count >= room.MaxMembers)
                return false;

            if (room.Players.ContainsKey(userId))
                return false;

            var nextIndex = room.Players.Count;
            var player = new RoomPlayer
            {
                UserID = userId,
                Index = nextIndex,
                IsOwner = false,
                PlayerProperty = playerProperty,
                PlayerNetworkObjects = new List<NetworkObject>(),
                JoinedAt = DateTime.UtcNow,
                TotalPower = 0
            };

            room.Players[userId] = player;

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(room.ExpiresAt!.Value - DateTime.UtcNow);

            _memoryCache.Set(RoomCacheKeyPrefix + roomId, room, cacheOptions);

            _logger.LogInformation("User {UserId} joined room {RoomId}", userId, roomId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining room {RoomId}", roomId);
            return false;
        }
    }

    public async Task<bool> LeaveRoomAsync(string roomId, string userId)
    {
        try
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                return false;

            if (!room.Players.Remove(userId))
                return false;

            if (room.Players.Count == 0 && !room.AllowEmpty)
            {
                _memoryCache.Remove(RoomCacheKeyPrefix + roomId);
                return true;
            }

            if (room.OwnerID == userId && room.Players.Count > 0)
            {
                var newOwner = room.Players.Values.First();
                room.OwnerID = newOwner.UserID;
                newOwner.IsOwner = true;
                _logger.LogInformation("Owner changed in room {RoomId} to {NewOwner}", roomId, newOwner.UserID);
            }

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(room.ExpiresAt!.Value - DateTime.UtcNow);

            _memoryCache.Set(RoomCacheKeyPrefix + roomId, room, cacheOptions);

            _logger.LogInformation("User {UserId} left room {RoomId}", userId, roomId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving room {RoomId}", roomId);
            return false;
        }
    }

    public async Task<RoomSyncData?> GetRoomStateAsync(string roomId)
    {
        try
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                return null;

            var syncPlayers = room.Players.Values.Select(p => new RoomSyncPlayer
            {
                userId = p.UserID,
                index = p.Index,
                playerProperty = p.PlayerProperty
            }).ToArray();

            return new RoomSyncData
            {
                isJoin = true,
                roomCreateTime = room.RoomCreateTime,
                roomId = room.RoomID,
                ownerId = room.OwnerID,
                roomProperty = room.RoomProperty,
                players = syncPlayers,
                networkObjects = room.NetworkObjects.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting room state {RoomId}", roomId);
            return null;
        }
    }

    public async Task<RoomSyncDataMinimal?> GetRoomStateMinimalAsync(string roomId, string userId)
    {
        try
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                return null;

            if (!room.Players.TryGetValue(userId, out var player))
                return null;

            var mySyncPlayer = new RoomSyncPlayer
            {
                userId = player.UserID,
                index = player.Index,
                playerProperty = player.PlayerProperty
            };

            var userIds = room.Players.Keys.ToArray();

            return new RoomSyncDataMinimal
            {
                isJoin = true,
                roomCreateTime = room.RoomCreateTime,
                roomId = room.RoomID,
                ownerId = room.OwnerID,
                roomProperty = room.RoomProperty,
                mySyncPlayer = mySyncPlayer,
                userIds = userIds
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting minimal room state {RoomId}", roomId);
            return null;
        }
    }

    public async Task UpdateRoomPropertyAsync(string roomId, DynamicPropertyPayload property)
    {
        try
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                return;

            room.RoomProperty = property;

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(room.ExpiresAt!.Value - DateTime.UtcNow);

            _memoryCache.Set(RoomCacheKeyPrefix + roomId, room, cacheOptions);

            _logger.LogInformation("Room property updated for {RoomId}", roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating room property {RoomId}", roomId);
        }
    }

    public async Task UpdatePlayerPropertyAsync(string roomId, string userId, DynamicPropertyPayload property)
    {
        try
        {
            var room = await GetRoomAsync(roomId);
            if (room == null)
                return;

            if (!room.Players.TryGetValue(userId, out var player))
                return;

            player.PlayerProperty = property;

            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(room.ExpiresAt!.Value - DateTime.UtcNow);

            _memoryCache.Set(RoomCacheKeyPrefix + roomId, room, cacheOptions);

            _logger.LogInformation("Player property updated for {UserId} in room {RoomId}", userId, roomId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player property {RoomId}", roomId);
        }
    }

    public async Task<bool> ValidatePrivateRoomAccessAsync(Room room, int? privateRoomNumber)
    {
        try
        {
            if (!room.IsPrivate)
                return true;

            if (room.PrivateRoomNumber == null)
                return false;

            return room.PrivateRoomNumber == privateRoomNumber;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating private room access");
            return false;
        }
    }

    public async Task<bool> ValidatePowerLimitsAsync(Room room, int playerTotalPower)
    {
        try
        {
            if (room.TotalPowerUpperLimit.HasValue && playerTotalPower > room.TotalPowerUpperLimit)
                return false;

            if (room.TotalPowerLowerLimit.HasValue && playerTotalPower < room.TotalPowerLowerLimit)
                return false;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating power limits");
            return false;
        }
    }

    public async IAsyncEnumerable<Room> GetAvailableRoomsAsync()
    {
        yield break;
    }
}
