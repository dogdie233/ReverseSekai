using SekaiApiModel.CP.Realtime;
using DiarkisServer.Models;

namespace DiarkisServer.Services;

public interface IRoomService
{
    Task<Room?> CreateRoomAsync(RoomInitialData initialData, string userId);
    Task<Room?> GetRoomAsync(string roomId);
    Task<bool> JoinRoomAsync(string roomId, string userId, DynamicPropertyPayload? playerProperty);
    Task<bool> LeaveRoomAsync(string roomId, string userId);
    Task<RoomSyncData?> GetRoomStateAsync(string roomId);
    Task<RoomSyncDataMinimal?> GetRoomStateMinimalAsync(string roomId, string userId);
    Task UpdateRoomPropertyAsync(string roomId, DynamicPropertyPayload property);
    Task UpdatePlayerPropertyAsync(string roomId, string userId, DynamicPropertyPayload property);
    Task<bool> ValidatePrivateRoomAccessAsync(Room room, int? privateRoomNumber);
    Task<bool> ValidatePowerLimitsAsync(Room room, int playerTotalPower);
    IAsyncEnumerable<Room> GetAvailableRoomsAsync();
}
