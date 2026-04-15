using MessagePack;
using SekaiApiModel.CP.Realtime;
using DiarkisServer.Protocol;
using DiarkisServer.Services;
using DiarkisServer.Session;
using DiarkisServer.Server;
using Microsoft.Extensions.Logging;

namespace DiarkisServer.Handlers;

/// <summary>
/// Handles all room/matchmaking commands. Translates Diarkis packets into
/// IRoomService calls and broadcasts state changes to room members.
/// Completely game-agnostic — doesn't know about music, scores, etc.
/// </summary>
public class RoomCommandHandler
{
    private readonly IRoomService _roomService;
    private readonly IMatchmakingService _matchmakingService;
    private readonly DiarkisRealtimeServer _server;
    private readonly ILogger<RoomCommandHandler> _logger;

    public RoomCommandHandler(IRoomService roomService, IMatchmakingService matchmakingService,
        DiarkisRealtimeServer server, ILogger<RoomCommandHandler> logger)
    {
        _roomService = roomService;
        _matchmakingService = matchmakingService;
        _server = server;
        _logger = logger;
    }

    public async Task HandleAsync(DiarkisSession session, DiarkisProtocol.Packet pkt, CancellationToken ct)
    {
        switch (pkt.Cmd)
        {
            // ── Create ──
            case DiarkisProtocol.RoomCreate:
            case DiarkisProtocol.CustomCreate:
            case DiarkisProtocol.MultiLiveCreate:
            case DiarkisProtocol.MultiLiveCustomCreate:
                await HandleCreateAsync(session, pkt); break;

            // ── Join ──
            case DiarkisProtocol.RoomJoin:
            case DiarkisProtocol.CustomJoin:
            case DiarkisProtocol.MultiLiveCustomJoin:
                await HandleJoinAsync(session, pkt); break;

            // ── Search-Join-Or-Create ──
            case DiarkisProtocol.CustomRandJoin:
            case DiarkisProtocol.CustomRandRoomJoin:
            case DiarkisProtocol.MultiLiveRandJoin:
            case DiarkisProtocol.MultiLiveRandRoomJoin:
                await HandleSearchJoinOrCreateAsync(session, pkt); break;

            // ── Unlock Join (private) ──
            case DiarkisProtocol.UnlockJoin:
            case DiarkisProtocol.MultiLiveUnlockJoin:
                await HandleUnlockJoinAsync(session, pkt); break;

            // ── Leave ──
            case DiarkisProtocol.RoomLeave:
                await HandleLeaveAsync(session, pkt); break;

            // ── Property updates ──
            case DiarkisProtocol.UpdateRoomProperty:
                await HandleUpdateRoomPropAsync(session, pkt); break;
            case DiarkisProtocol.UpdatePlayerProperty:
            case DiarkisProtocol.UpdatePlayerPropAndIdx:
                await HandleUpdatePlayerPropAsync(session, pkt); break;

            // ── Sync ──
            case DiarkisProtocol.RoomSync:
                await HandleSyncAsync(session, pkt); break;
            case DiarkisProtocol.RoomSyncMinimal:
                await HandleSyncMinimalAsync(session, pkt); break;

            // ── Broadcast / relay — just forward bytes to room ──
            case DiarkisProtocol.RoomBroadcast:
            case DiarkisProtocol.RoomMessage:
                await HandleBroadcastAsync(session, pkt); break;

            // ── Matchmaking conditions ──
            case DiarkisProtocol.AddMatchmakeCondition:
            case DiarkisProtocol.MultiLiveAddMatchmakeCondition:
                await HandleAddConditionAsync(session, pkt); break;
            case DiarkisProtocol.ScaleUpMatchmake:
            case DiarkisProtocol.MultiLiveScaleUpMatchmake:
                await HandleScaleUpAsync(session, pkt); break;
            case DiarkisProtocol.CloseMatchmake:
            case DiarkisProtocol.MultiLiveCloseMatchmake:
            case DiarkisProtocol.MultiLivePrivateCloseMatchmake:
                await HandleCloseMatchmakeAsync(session, pkt); break;

            // ── Private room ──
            case DiarkisProtocol.ReleasePrivateRoom:
            case DiarkisProtocol.ReleaseMultiLivePrivateRoom:
                await HandleReleasePrivateAsync(session, pkt); break;
            case DiarkisProtocol.MultiLiveReStartPrivate:
                await HandleRestartPrivateAsync(session, pkt); break;

            // ── Timestamp ──
            case DiarkisProtocol.TimestampCmd:
                await session.SendOkAsync(pkt.Cmd,
                    new TimestampPayload { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
                break;

            // ── Post-process ──
            case DiarkisProtocol.JoinPostProcess:
            case DiarkisProtocol.JoinPostProcessMinimal:
                await HandlePostProcessAsync(session, pkt); break;

            // ── Anything else: ack OK (dumb pipe) ──
            default:
                // Unknown game command → relay to room as broadcast
                if (session.CurrentRoomId != null)
                    await BroadcastToRoomRawAsync(session.CurrentRoomId, pkt, session.SessionId);
                await session.SendOkAsync(pkt.Cmd);
                break;
        }
    }

    // ─── Handlers ───

    private async Task HandleCreateAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        var init = Deserialize<RoomInitialData>(pkt.Payload)
            ?? new RoomInitialData { createOption = new RoomCreateOption { maxMembers = 5, roomTtl = 3600 } };

        var room = await _roomService.CreateRoomAsync(init, session.UserId ?? session.SessionId);
        if (room == null) { await session.SendErrorAsync(pkt.Cmd); return; }

        session.CurrentRoomId = room.RoomID;
        await session.SendOkAsync(pkt.Cmd, BuildJoinPayload(room, session.UserId ?? session.SessionId));
    }

    private async Task HandleJoinAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        var data = Deserialize<DirectJoinData>(pkt.Payload);
        if (data?.roomId == null) { await session.SendErrorAsync(pkt.Cmd); return; }

        if (!await _roomService.JoinRoomAsync(data.roomId, session.UserId ?? session.SessionId, data.playerProperty))
        { await session.SendErrorAsync(pkt.Cmd); return; }

        session.CurrentRoomId = data.roomId;
        var room = await _roomService.GetRoomAsync(data.roomId);
        if (room == null) return;
        await session.SendOkAsync(pkt.Cmd, BuildJoinPayload(room, session.UserId ?? session.SessionId));
        await BroadcastMemberEventAsync(room, DiarkisProtocol.RoomJoin, session.UserId ?? session.SessionId, session.SessionId);
    }

    private async Task HandleSearchJoinOrCreateAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        var data = Deserialize<SearchJoinOrCreateData>(pkt.Payload);
        var init = new RoomInitialData
        {
            createOption = new RoomCreateOption { maxMembers = 5, roomTtl = 3600, joinRoom = true },
            roomProperty = data?.roomProperty,
            playerProperty = data?.playerProperty
        };

        var uid = session.UserId ?? session.SessionId;
        var room = await _matchmakingService.SearchJoinOrCreateAsync(
            init, uid, data?.searchProps ?? new(), data?.matchingName ?? "");

        if (room == null) { await session.SendErrorAsync(pkt.Cmd); return; }
        session.CurrentRoomId = room.RoomID;
        await session.SendOkAsync(pkt.Cmd, BuildJoinPayload(room, uid));
        if (room.Players.Count > 1)
            await BroadcastMemberEventAsync(room, DiarkisProtocol.RoomJoin, uid, session.SessionId);
    }

    private async Task HandleUnlockJoinAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        var data = Deserialize<UnlockJoinData>(pkt.Payload);
        if (data?.roomId == null) { await session.SendErrorAsync(pkt.Cmd); return; }

        var uid = session.UserId ?? session.SessionId;
        if (!await _roomService.JoinRoomAsync(data.roomId, uid, data.playerProperty))
        { await session.SendErrorAsync(pkt.Cmd); return; }

        session.CurrentRoomId = data.roomId;
        var room = await _roomService.GetRoomAsync(data.roomId);
        if (room == null) return;
        await session.SendOkAsync(pkt.Cmd, BuildJoinPayload(room, uid));
        await BroadcastMemberEventAsync(room, DiarkisProtocol.RoomJoin, uid, session.SessionId);
    }

    private async Task HandleLeaveAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId != null)
        {
            var uid = session.UserId ?? session.SessionId;
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            await _roomService.LeaveRoomAsync(session.CurrentRoomId, uid);
            if (room != null)
                await BroadcastMemberEventAsync(room, DiarkisProtocol.RoomLeave, uid, session.SessionId);
        }
        session.CurrentRoomId = null;
        await session.SendOkAsync(pkt.Cmd);
    }

    private async Task HandleUpdateRoomPropAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId == null) return;
        var prop = Deserialize<DynamicPropertyPayload>(pkt.Payload);
        if (prop == null) return;
        await _roomService.UpdateRoomPropertyAsync(session.CurrentRoomId, prop);
        await BroadcastToRoomRawAsync(session.CurrentRoomId, pkt, session.SessionId);
        await session.SendOkAsync(pkt.Cmd);
    }

    private async Task HandleUpdatePlayerPropAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId == null) return;
        var uid = session.UserId ?? session.SessionId;
        var prop = Deserialize<DynamicPropertyPayload>(pkt.Payload);
        if (prop == null) return;
        await _roomService.UpdatePlayerPropertyAsync(session.CurrentRoomId, uid, prop);
        await BroadcastToRoomRawAsync(session.CurrentRoomId, pkt, session.SessionId);
        await session.SendOkAsync(pkt.Cmd);
    }

    private async Task HandleSyncAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId == null) { await session.SendErrorAsync(pkt.Cmd); return; }
        var state = await _roomService.GetRoomStateAsync(session.CurrentRoomId);
        await session.SendOkAsync(pkt.Cmd, state);
    }

    private async Task HandleSyncMinimalAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId == null) { await session.SendErrorAsync(pkt.Cmd); return; }
        var uid = session.UserId ?? session.SessionId;
        var state = await _roomService.GetRoomStateMinimalAsync(session.CurrentRoomId, uid);
        await session.SendOkAsync(pkt.Cmd, state);
    }

    private async Task HandleBroadcastAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId == null) return;
        await BroadcastToRoomRawAsync(session.CurrentRoomId, pkt, session.SessionId);
    }

    private async Task HandleAddConditionAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            if (room != null) room.IsMatchmakingOpen = true;
        }
        await session.SendOkAsync(pkt.Cmd);
    }

    private async Task HandleScaleUpAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            if (room != null)
            {
                if (room.TotalPowerUpperLimit.HasValue) room.TotalPowerUpperLimit += 5000;
                if (room.TotalPowerLowerLimit.HasValue) room.TotalPowerLowerLimit = Math.Max(0, room.TotalPowerLowerLimit.Value - 5000);
                room.IsMatchmakingOpen = true;
            }
        }
        await session.SendOkAsync(pkt.Cmd);
    }

    private async Task HandleCloseMatchmakeAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            if (room != null) room.IsMatchmakingOpen = false;
        }
        await session.SendOkAsync(pkt.Cmd);
    }

    private async Task HandleReleasePrivateAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            if (room != null) { room.IsPrivate = false; room.IsMatchmakingOpen = true; }
        }
        await session.SendOkAsync(pkt.Cmd);
    }

    private async Task HandleRestartPrivateAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            if (room != null) room.ExpiresAt = DateTime.UtcNow.AddSeconds(room.TTL);
        }
        await session.SendOkAsync(pkt.Cmd);
    }

    private async Task HandlePostProcessAsync(DiarkisSession session, DiarkisProtocol.Packet pkt)
    {
        var data = Deserialize<PostProcessPayload>(pkt.Payload);
        if (data?.playerProperty != null && session.CurrentRoomId != null)
            await _roomService.UpdatePlayerPropertyAsync(session.CurrentRoomId, session.UserId ?? session.SessionId, data.playerProperty);
        await session.SendOkAsync(pkt.Cmd);
    }

    // ─── Helpers ───

    private JoinRoomPayload BuildJoinPayload(Models.Room room, string userId)
    {
        room.Players.TryGetValue(userId, out var me);
        return new JoinRoomPayload
        {
            roomCreateTime = room.RoomCreateTime, roomId = room.RoomID, isJoined = true,
            ownerId = room.OwnerID, roomProperty = room.RoomProperty,
            playerProperty = me?.PlayerProperty,
            userIds = room.Players.Keys.ToArray(), userId = userId,
            privateRoomNumber = room.PrivateRoomNumber ?? 0
        };
    }

    private async Task BroadcastMemberEventAsync(Models.Room room, uint cmd, string userId, string excludeSid)
    {
        var payload = DiarkisProtocol.EncodeOk(cmd, new UserIdPayload { userId = userId });
        foreach (var peer in _server.GetRoomSessions(room.RoomID))
            if (peer.SessionId != excludeSid)
                await peer.SendRawAsync(payload);
    }

    private async Task BroadcastToRoomRawAsync(string roomId, DiarkisProtocol.Packet pkt, string excludeSid)
    {
        var raw = DiarkisProtocol.Encode(pkt.Ver, pkt.Cmd, DiarkisProtocol.StatusOk, pkt.Payload);
        foreach (var peer in _server.GetRoomSessions(roomId))
            if (peer.SessionId != excludeSid)
                await peer.SendRawAsync(raw);
    }

    private static T? Deserialize<T>(byte[]? payload) where T : class
    {
        if (payload == null) return null;
        try { return MessagePackSerializer.Deserialize<T>(payload); }
        catch { return null; }
    }
}
