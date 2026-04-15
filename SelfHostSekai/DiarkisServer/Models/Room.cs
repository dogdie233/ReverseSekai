using SekaiApiModel.CP.Realtime;

namespace DiarkisServer.Models;

public class Room
{
    public string RoomID { get; set; } = string.Empty;
    public string OwnerID { get; set; } = string.Empty;
    public uint RoomCreateTime { get; set; }
    public Dictionary<string, RoomPlayer> Players { get; set; } = new();
    public DynamicPropertyPayload? RoomProperty { get; set; }
    public List<NetworkObject> NetworkObjects { get; set; } = new();
    public uint TTL { get; set; }
    public int MaxMembers { get; set; } = 5;
    public bool IsPrivate { get; set; }
    public int? PrivateRoomNumber { get; set; }
    public int? TotalPowerUpperLimit { get; set; }
    public int? TotalPowerLowerLimit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool AllowEmpty { get; set; }
    public string? LiveType { get; set; }
    public string? MatchingName { get; set; }
    public bool IsMatchmakingOpen { get; set; } = true;
}

public class RoomPlayer
{
    public string UserID { get; set; } = string.Empty;
    public int Index { get; set; }
    public bool IsOwner { get; set; }
    public DynamicPropertyPayload? PlayerProperty { get; set; }
    public List<NetworkObject> PlayerNetworkObjects { get; set; } = new();
    public DateTime JoinedAt { get; set; }
    public int TotalPower { get; set; }
}
