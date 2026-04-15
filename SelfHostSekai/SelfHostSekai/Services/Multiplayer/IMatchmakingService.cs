using SekaiApiModel.CP.Realtime;
using SelfHostSekai.Models.Multiplayer;

namespace SelfHostSekai.Services.Multiplayer;

public interface IMatchmakingService
{
    /// <summary>
    /// Searches for available rooms matching the search criteria.
    /// </summary>
    Task<List<Room>> SearchRoomsAsync(Dictionary<string, int> searchProps, string matchingName);

    /// <summary>
    /// Searches for a room or creates one if not available.
    /// </summary>
    Task<Room?> SearchJoinOrCreateAsync(
        RoomInitialData initialData, 
        string userId, 
        Dictionary<string, int> searchProps,
        string matchingName);

    /// <summary>
    /// Applies scale-up logic to expand search criteria if needed.
    /// </summary>
    Dictionary<string, int> ApplyScaleUp(Dictionary<string, int> currentProps, int iteration);

    /// <summary>
    /// Validates if a player's properties match search criteria.
    /// </summary>
    bool ValidateSearchProps(Dictionary<string, int> playerProps, Dictionary<string, int> searchProps);
}
