using SekaiApiModel.CP.Realtime;
using SelfHostSekai.Models.Multiplayer;

namespace SelfHostSekai.Services.Multiplayer;

public class MatchmakingService : IMatchmakingService
{
    private readonly IRoomService _roomService;
    private readonly ILogger<MatchmakingService> _logger;

    public MatchmakingService(IRoomService roomService, ILogger<MatchmakingService> logger)
    {
        _roomService = roomService;
        _logger = logger;
    }

    public async Task<List<Room>> SearchRoomsAsync(Dictionary<string, int> searchProps, string matchingName)
    {
        try
        {
            var matchingRooms = new List<Room>();

            await foreach (var room in _roomService.GetAvailableRoomsAsync())
            {
                if (ValidateSearchProps(room.RoomProperty?.values != null ? ConvertProperties(room.RoomProperty.values) : new(), searchProps))
                {
                    matchingRooms.Add(room);
                }
            }

            _logger.LogInformation("Found {Count} matching rooms for {MatchingName}", matchingRooms.Count, matchingName);
            return matchingRooms;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching rooms for {MatchingName}", matchingName);
            return new List<Room>();
        }
    }

    public async Task<Room?> SearchJoinOrCreateAsync(
        RoomInitialData initialData,
        string userId,
        Dictionary<string, int> searchProps,
        string matchingName)
    {
        try
        {
            var availableRooms = await SearchRoomsAsync(searchProps, matchingName);

            if (availableRooms.Count > 0)
            {
                var selectedRoom = availableRooms[0];
                var joinSuccess = await _roomService.JoinRoomAsync(selectedRoom.RoomID, userId, initialData.playerProperty);
                
                if (joinSuccess)
                {
                    _logger.LogInformation("User {UserId} joined existing room {RoomId}", userId, selectedRoom.RoomID);
                    return selectedRoom;
                }
            }

            var newRoom = await _roomService.CreateRoomAsync(initialData, userId);
            if (newRoom != null)
            {
                _logger.LogInformation("Created new room {RoomId} for user {UserId}", newRoom.RoomID, userId);
            }

            return newRoom;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SearchJoinOrCreate for user {UserId}", userId);
            return null;
        }
    }

    public Dictionary<string, int> ApplyScaleUp(Dictionary<string, int> currentProps, int iteration)
    {
        var scaledProps = new Dictionary<string, int>(currentProps);

        foreach (var key in scaledProps.Keys.ToList())
        {
            var originalValue = scaledProps[key];
            var scaleFactor = (int)Math.Pow(1.5, iteration);
            scaledProps[key] = Math.Max(0, originalValue - (originalValue / 10) * iteration);
        }

        _logger.LogInformation("Applied scale-up iteration {Iteration}", iteration);
        return scaledProps;
    }

    public bool ValidateSearchProps(Dictionary<string, int> playerProps, Dictionary<string, int> searchProps)
    {
        if (searchProps == null || searchProps.Count == 0)
            return true;

        foreach (var (key, searchValue) in searchProps)
        {
            if (!playerProps.TryGetValue(key, out var playerValue))
                return false;

            if (Math.Abs(playerValue - searchValue) > searchValue / 10)
                return false;
        }

        return true;
    }

    private Dictionary<string, int> ConvertProperties(Dictionary<int, byte[]> properties)
    {
        var result = new Dictionary<string, int>();

        foreach (var (key, value) in properties)
        {
            if (value.Length == 4)
            {
                var intValue = BitConverter.ToInt32(value, 0);
                result[key.ToString()] = intValue;
            }
        }

        return result;
    }
}
