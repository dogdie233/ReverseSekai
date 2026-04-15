using SekaiApiModel.CP.Realtime;
using DiarkisServer.Models;

namespace DiarkisServer.Services;

public interface IMatchmakingService
{
    Task<List<Room>> SearchRoomsAsync(Dictionary<string, int> searchProps, string matchingName);
    Task<Room?> SearchJoinOrCreateAsync(RoomInitialData initialData, string userId,
        Dictionary<string, int> searchProps, string matchingName);
    Dictionary<string, int> ApplyScaleUp(Dictionary<string, int> currentProps, int iteration);
    bool ValidateSearchProps(Dictionary<string, int> playerProps, Dictionary<string, int> searchProps);
}
