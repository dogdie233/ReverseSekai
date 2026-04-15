using SekaiApiModel.CP.Realtime;
using SelfHostSekai.Models.Multiplayer;

namespace SelfHostSekai.Services.Multiplayer;

public class MatchmakingService : IMatchmakingService
{
    private readonly IRoomService _roomService;
    private readonly ILogger<MatchmakingService> _logger;

    /// <summary>
    /// Power-range matching config from逆向:
    ///   multi_live_power_range_upper_limit_init = 10000
    ///   multi_live_power_range_spread_second     = 3000 (ms per iteration)
    ///   multi_live_power_range_upper_limit_spread = 5000 (per iteration)
    /// </summary>
    private const int PowerRangeInit = 10000;
    private const int PowerRangeSpread = 5000;

    public MatchmakingService(IRoomService roomService, ILogger<MatchmakingService> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    public async Task<List<Room>> SearchRoomsAsync(Dictionary<string, int> searchProps, string matchingName)
    {
        var result = new List<Room>();

        await foreach (var room in _roomService.GetAvailableRoomsAsync())
        {
            if (!string.IsNullOrEmpty(matchingName) && room.MatchingName != matchingName)
                continue;

            if (room.IsPrivate)
                continue;

            if (searchProps.Count > 0)
            {
                var roomProps = ConvertProperties(room.RoomProperty?.values);
                if (!ValidateSearchProps(roomProps, searchProps))
                    continue;
            }

            result.Add(room);
        }

        _logger.LogInformation("SearchRooms: found {Count} for {Name}", result.Count, matchingName);
        return result;
    }

    public async Task<Room?> SearchJoinOrCreateAsync(
        RoomInitialData initialData,
        string userId,
        Dictionary<string, int> searchProps,
        string matchingName)
    {
        var rooms = await SearchRoomsAsync(searchProps, matchingName);

        foreach (var room in rooms)
        {
            if (await _roomService.JoinRoomAsync(room.RoomID, userId, initialData.playerProperty))
            {
                _logger.LogInformation("User {UserId} joined existing room {RoomId}", userId, room.RoomID);
                return room;
            }
        }

        // No matching room → create
        var newRoom = await _roomService.CreateRoomAsync(initialData, userId);
        if (newRoom != null)
        {
            newRoom.MatchingName = matchingName;
            _logger.LogInformation("Created new room {RoomId} for {UserId}, matchingName={Name}",
                newRoom.RoomID, userId, matchingName);
        }
        return newRoom;
    }

    public Dictionary<string, int> ApplyScaleUp(Dictionary<string, int> currentProps, int iteration)
    {
        var scaled = new Dictionary<string, int>(currentProps);
        foreach (var key in scaled.Keys.ToList())
        {
            var original = scaled[key];
            // Each iteration widens the range by PowerRangeSpread
            scaled[key] = Math.Max(0, original - PowerRangeSpread * iteration);
        }
        _logger.LogDebug("ScaleUp iteration {Iter}: {Props}", iteration, scaled);
        return scaled;
    }

    public bool ValidateSearchProps(Dictionary<string, int> playerProps, Dictionary<string, int> searchProps)
    {
        if (searchProps == null || searchProps.Count == 0)
            return true;

        foreach (var (key, searchValue) in searchProps)
        {
            if (!playerProps.TryGetValue(key, out var playerValue))
                return false;

            // Power-based matching: ±PowerRangeInit tolerance
            if (Math.Abs(playerValue - searchValue) > PowerRangeInit)
                return false;
        }
        return true;
    }

    private static Dictionary<string, int> ConvertProperties(Dictionary<int, byte[]>? properties)
    {
        var result = new Dictionary<string, int>();
        if (properties == null) return result;

        foreach (var (key, value) in properties)
        {
            if (value.Length >= 4)
                result[key.ToString()] = BitConverter.ToInt32(value, 0);
        }
        return result;
    }
}
