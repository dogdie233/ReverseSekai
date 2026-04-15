using Microsoft.Extensions.Logging;
using SekaiApiModel.CP.Realtime;
using DiarkisServer.Models;

namespace DiarkisServer.Services;

public class MatchmakingService : IMatchmakingService
{
    private readonly IRoomService _roomService;
    private readonly ILogger<MatchmakingService> _logger;
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
            if (!string.IsNullOrEmpty(matchingName) && room.MatchingName != matchingName) continue;
            if (room.IsPrivate) continue;
            if (searchProps.Count > 0)
            {
                var rp = ConvertProperties(room.RoomProperty?.values);
                if (!ValidateSearchProps(rp, searchProps)) continue;
            }
            result.Add(room);
        }
        return result;
    }

    public async Task<Room?> SearchJoinOrCreateAsync(RoomInitialData initialData, string userId,
        Dictionary<string, int> searchProps, string matchingName)
    {
        var rooms = await SearchRoomsAsync(searchProps, matchingName);
        foreach (var r in rooms)
            if (await _roomService.JoinRoomAsync(r.RoomID, userId, initialData.playerProperty))
                return r;

        var newRoom = await _roomService.CreateRoomAsync(initialData, userId);
        if (newRoom != null) newRoom.MatchingName = matchingName;
        return newRoom;
    }

    public Dictionary<string, int> ApplyScaleUp(Dictionary<string, int> props, int iter)
    {
        var scaled = new Dictionary<string, int>(props);
        foreach (var k in scaled.Keys.ToList())
            scaled[k] = Math.Max(0, scaled[k] - PowerRangeSpread * iter);
        return scaled;
    }

    public bool ValidateSearchProps(Dictionary<string, int> playerProps, Dictionary<string, int> searchProps)
    {
        if (searchProps.Count == 0) return true;
        foreach (var (k, v) in searchProps)
        {
            if (!playerProps.TryGetValue(k, out var pv)) return false;
            if (Math.Abs(pv - v) > PowerRangeInit) return false;
        }
        return true;
    }

    private static Dictionary<string, int> ConvertProperties(Dictionary<int, byte[]>? props)
    {
        var r = new Dictionary<string, int>();
        if (props == null) return r;
        foreach (var (k, v) in props)
            if (v.Length >= 4) r[k.ToString()] = BitConverter.ToInt32(v, 0);
        return r;
    }
}
