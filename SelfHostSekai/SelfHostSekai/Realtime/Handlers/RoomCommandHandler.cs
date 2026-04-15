using MessagePack;
using SekaiApiModel.CP.Realtime;
using SelfHostSekai.Services.Multiplayer;

namespace SelfHostSekai.Realtime.Handlers;

/// <summary>
/// Handles Diarkis Room-level and CP.Realtime custom commands.
/// Translates WebSocket packets into IRoomService calls and broadcasts state changes.
/// </summary>
public class RoomCommandHandler
{
    private readonly IRoomService _roomService;
    private readonly IMatchmakingService _matchmakingService;
    private readonly RealtimeServer _server;
    private readonly ILogger<RoomCommandHandler> _logger;

    public RoomCommandHandler(
        IRoomService roomService,
        IMatchmakingService matchmakingService,
        RealtimeServer server,
        ILogger<RoomCommandHandler> logger)
    {
        _roomService = roomService;
        _matchmakingService = matchmakingService;
        _server = server;
        _logger = logger;
    }

    public async Task HandleAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        switch (pkt.Cmd)
        {
            // ── Room Create ──
            case RealtimeProtocol.RoomCreate:
            case RealtimeProtocol.CustomCreate:
            case RealtimeProtocol.MultiLiveCreate:
            case RealtimeProtocol.MultiLiveCustomCreate:
                await HandleCreateAsync(session, pkt, ct);
                break;

            // ── Room Join ──
            case RealtimeProtocol.RoomJoin:
            case RealtimeProtocol.CustomJoin:
            case RealtimeProtocol.MultiLiveCustomJoin:
                await HandleJoinAsync(session, pkt, ct);
                break;

            // ── Random Join / Search-Join-Or-Create ──
            case RealtimeProtocol.CustomRandJoin:
            case RealtimeProtocol.CustomRandRoomJoin:
            case RealtimeProtocol.MultiLiveRandJoin:
            case RealtimeProtocol.MultiLiveRandRoomJoin:
                await HandleSearchJoinOrCreateAsync(session, pkt, ct);
                break;

            // ── Unlock Join (private room) ──
            case RealtimeProtocol.UnlockJoin:
            case RealtimeProtocol.MultiLiveUnlockJoin:
                await HandleUnlockJoinAsync(session, pkt, ct);
                break;

            // ── Room Leave ──
            case RealtimeProtocol.RoomLeave:
                await HandleLeaveAsync(session, pkt, ct);
                break;

            // ── Property Update ──
            case RealtimeProtocol.UpdateRoomProperty:
                await HandleUpdateRoomPropertyAsync(session, pkt, ct);
                break;
            case RealtimeProtocol.UpdatePlayerProperty:
            case RealtimeProtocol.UpdatePlayerPropAndIdx:
                await HandleUpdatePlayerPropertyAsync(session, pkt, ct);
                break;

            // ── Room Sync Request ──
            case RealtimeProtocol.RoomSync:
                await HandleRoomSyncAsync(session, pkt, ct);
                break;
            case RealtimeProtocol.RoomSyncMinimal:
                await HandleRoomSyncMinimalAsync(session, pkt, ct);
                break;

            // ── Broadcast / Message (relay to room members) ──
            case RealtimeProtocol.RoomBroadcast:
            case RealtimeProtocol.RoomMessage:
            case RealtimeProtocol.MsgCountDown:
            case RealtimeProtocol.MsgStamp:
            case RealtimeProtocol.MsgLoadProgress:
            case RealtimeProtocol.MsgPlayerPraiseInfo:
            case RealtimeProtocol.MsgPlayerSkillInfo:
                await HandleBroadcastAsync(session, pkt, ct);
                break;

            // ── Add / Scale-Up Matchmaking Condition ──
            case RealtimeProtocol.AddMatchmakeCondition:
            case RealtimeProtocol.MultiLiveAddMatchmakeCondition:
                await HandleAddMatchmakeConditionAsync(session, pkt, ct);
                break;
            case RealtimeProtocol.ScaleUpMatchmake:
            case RealtimeProtocol.MultiLiveScaleUpMatchmake:
                await HandleScaleUpMatchmakeAsync(session, pkt, ct);
                break;

            // ── Close Matchmaking ──
            case RealtimeProtocol.CloseMatchmake:
            case RealtimeProtocol.MultiLiveCloseMatchmake:
            case RealtimeProtocol.MultiLivePrivateCloseMatchmake:
                await HandleCloseMatchmakeAsync(session, pkt, ct);
                break;

            // ── Restart Private Room ──
            case RealtimeProtocol.MultiLiveReStartPrivate:
                await HandleReStartPrivateAsync(session, pkt, ct);
                break;

            // ── Release Private Room ──
            case RealtimeProtocol.ReleasePrivateRoom:
            case RealtimeProtocol.ReleaseMultiLivePrivateRoom:
                await HandleReleasePrivateRoomAsync(session, pkt, ct);
                break;

            // ── Timestamp ──
            case RealtimeProtocol.TimestampCmd:
                await HandleTimestampAsync(session, pkt, ct);
                break;

            // ── Post-process / fallback ──
            case RealtimeProtocol.JoinPostProcess:
            case RealtimeProtocol.JoinPostProcessMinimal:
                await HandleJoinPostProcessAsync(session, pkt, ct);
                break;

            default:
                _logger.LogDebug("Unhandled room cmd {Cmd} from {UserId}", pkt.Cmd, session.UserId);
                await session.SendOkAsync(pkt.Cmd, ct: ct);
                break;
        }
    }

    // ── Handlers ──

    private async Task HandleCreateAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        RoomInitialData? init = null;
        if (pkt.Payload != null)
            init = MessagePackSerializer.Deserialize<RoomInitialData>(pkt.Payload);

        init ??= new RoomInitialData
        {
            createOption = new RoomCreateOption { maxMembers = 5, roomTtl = 3600 }
        };

        var room = await _roomService.CreateRoomAsync(init, session.UserId);
        if (room == null)
        {
            await session.SendErrorAsync(pkt.Cmd, ct);
            return;
        }

        session.CurrentRoomId = room.RoomID;

        var joinPayload = BuildJoinPayload(room, session.UserId);
        await session.SendOkAsync(pkt.Cmd, joinPayload, ct);
        _logger.LogInformation("Room {RoomId} created by {UserId}", room.RoomID, session.UserId);
    }

    private async Task HandleJoinAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        DirectJoinData? data = null;
        if (pkt.Payload != null)
            data = MessagePackSerializer.Deserialize<DirectJoinData>(pkt.Payload);

        if (data?.roomId == null)
        {
            await session.SendErrorAsync(pkt.Cmd, ct);
            return;
        }

        var ok = await _roomService.JoinRoomAsync(data.roomId, session.UserId, data.playerProperty);
        if (!ok)
        {
            await session.SendErrorAsync(pkt.Cmd, ct);
            return;
        }

        session.CurrentRoomId = data.roomId;

        var room = await _roomService.GetRoomAsync(data.roomId);
        if (room != null)
        {
            var joinPayload = BuildJoinPayload(room, session.UserId);
            await session.SendOkAsync(pkt.Cmd, joinPayload, ct);
            await BroadcastMemberJoinAsync(room, session.UserId, ct);
        }
    }

    private async Task HandleSearchJoinOrCreateAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        SearchJoinOrCreateData? data = null;
        if (pkt.Payload != null)
            data = MessagePackSerializer.Deserialize<SearchJoinOrCreateData>(pkt.Payload);

        var init = new RoomInitialData
        {
            createOption = new RoomCreateOption { maxMembers = 5, roomTtl = 3600, joinRoom = true },
            roomProperty = data?.roomProperty,
            playerProperty = data?.playerProperty
        };

        var searchProps = data?.searchProps ?? new Dictionary<string, int>();
        var matchingName = data?.matchingName ?? "";

        var room = await _matchmakingService.SearchJoinOrCreateAsync(init, session.UserId, searchProps, matchingName);
        if (room == null)
        {
            await session.SendErrorAsync(pkt.Cmd, ct);
            return;
        }

        session.CurrentRoomId = room.RoomID;

        var joinPayload = BuildJoinPayload(room, session.UserId);
        await session.SendOkAsync(pkt.Cmd, joinPayload, ct);

        // If joined an existing room, notify other members
        if (room.Players.Count > 1)
            await BroadcastMemberJoinAsync(room, session.UserId, ct);
    }

    private async Task HandleUnlockJoinAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        UnlockJoinData? data = null;
        if (pkt.Payload != null)
            data = MessagePackSerializer.Deserialize<UnlockJoinData>(pkt.Payload);

        if (data?.roomId == null)
        {
            await session.SendErrorAsync(pkt.Cmd, ct);
            return;
        }

        var room = await _roomService.GetRoomAsync(data.roomId);
        if (room == null)
        {
            await session.SendErrorAsync(pkt.Cmd, ct);
            return;
        }

        var ok = await _roomService.JoinRoomAsync(data.roomId, session.UserId, data.playerProperty);
        if (!ok)
        {
            await session.SendErrorAsync(pkt.Cmd, ct);
            return;
        }

        session.CurrentRoomId = data.roomId;

        var joinPayload = BuildJoinPayload(room, session.UserId);
        await session.SendOkAsync(pkt.Cmd, joinPayload, ct);
        await BroadcastMemberJoinAsync(room, session.UserId, ct);
    }

    private async Task HandleLeaveAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            await _roomService.LeaveRoomAsync(session.CurrentRoomId, session.UserId);

            if (room != null)
                await BroadcastMemberLeaveAsync(room, session.UserId, ct);
        }
        session.CurrentRoomId = null;
        await session.SendOkAsync(pkt.Cmd, ct: ct);
    }

    private async Task HandleUpdateRoomPropertyAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        if (session.CurrentRoomId == null) return;

        DynamicPropertyPayload? prop = null;
        if (pkt.Payload != null)
            prop = MessagePackSerializer.Deserialize<DynamicPropertyPayload>(pkt.Payload);

        if (prop != null)
        {
            await _roomService.UpdateRoomPropertyAsync(session.CurrentRoomId, prop);
            // Broadcast property change to all room members
            await BroadcastToRoomAsync(session.CurrentRoomId, pkt.Cmd, prop, session.UserId, ct);
        }
        await session.SendOkAsync(pkt.Cmd, ct: ct);
    }

    private async Task HandleUpdatePlayerPropertyAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        if (session.CurrentRoomId == null) return;

        DynamicPropertyPayload? prop = null;
        if (pkt.Payload != null)
            prop = MessagePackSerializer.Deserialize<DynamicPropertyPayload>(pkt.Payload);

        if (prop != null)
        {
            await _roomService.UpdatePlayerPropertyAsync(session.CurrentRoomId, session.UserId, prop);
            // Broadcast player property change to all room members
            await BroadcastToRoomAsync(session.CurrentRoomId, pkt.Cmd, prop, session.UserId, ct);
        }
        await session.SendOkAsync(pkt.Cmd, ct: ct);
    }

    private async Task HandleRoomSyncAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        if (session.CurrentRoomId == null)
        {
            await session.SendErrorAsync(pkt.Cmd, ct);
            return;
        }

        var state = await _roomService.GetRoomStateAsync(session.CurrentRoomId);
        await session.SendOkAsync(pkt.Cmd, state, ct);
    }

    private async Task HandleRoomSyncMinimalAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        if (session.CurrentRoomId == null)
        {
            await session.SendErrorAsync(pkt.Cmd, ct);
            return;
        }

        var state = await _roomService.GetRoomStateMinimalAsync(session.CurrentRoomId, session.UserId);
        await session.SendOkAsync(pkt.Cmd, state, ct);
    }

    private async Task HandleBroadcastAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        if (session.CurrentRoomId == null) return;

        // Relay the raw packet to all other room members
        var encoded = RealtimeProtocol.Encode(pkt.Ver, pkt.Cmd, RealtimeProtocol.StatusOk, pkt.Payload);

        var sessions = _server.GetRoomSessions(session.CurrentRoomId);
        foreach (var peer in sessions)
        {
            if (peer.UserId != session.UserId)
                await peer.SendAsync(encoded, ct);
        }
    }

    private async Task HandleCloseMatchmakeAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            if (room != null)
                room.IsMatchmakingOpen = false;
        }
        await session.SendOkAsync(pkt.Cmd, ct: ct);
    }

    private async Task HandleReleasePrivateRoomAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            if (room != null)
            {
                room.IsPrivate = false;
                room.IsMatchmakingOpen = true;
            }
        }
        await session.SendOkAsync(pkt.Cmd, ct: ct);
    }

    private async Task HandleTimestampAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        var ts = new TimestampPayload { timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        await session.SendOkAsync(pkt.Cmd, ts, ct);
    }

    private async Task HandleJoinPostProcessAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        // Post-join: client sends its final player property. Update and ack.
        PostProcessPayload? data = null;
        if (pkt.Payload != null)
            data = MessagePackSerializer.Deserialize<PostProcessPayload>(pkt.Payload);

        if (data?.playerProperty != null && session.CurrentRoomId != null)
        {
            await _roomService.UpdatePlayerPropertyAsync(session.CurrentRoomId, session.UserId, data.playerProperty);
        }
        await session.SendOkAsync(pkt.Cmd, ct: ct);
    }

    private async Task HandleAddMatchmakeConditionAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        // Client adds extra matching criteria (e.g. power range).
        // For our minimal implementation, we just re-open the room to matchmaking
        // with whatever conditions are already on it.
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            if (room != null)
                room.IsMatchmakingOpen = true;
        }
        await session.SendOkAsync(pkt.Cmd, ct: ct);
    }

    private async Task HandleScaleUpMatchmakeAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        // Client requests widening the matchmaking search range.
        // In our implementation, rooms are already matched with a fixed tolerance
        // (±10000 power in MatchmakingService). We ack success so the client
        // proceeds. A real implementation would widen the search props.
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            if (room != null)
            {
                // Widen power limits if set
                if (room.TotalPowerUpperLimit.HasValue)
                    room.TotalPowerUpperLimit += 5000;
                if (room.TotalPowerLowerLimit.HasValue)
                    room.TotalPowerLowerLimit = Math.Max(0, room.TotalPowerLowerLimit.Value - 5000);

                room.IsMatchmakingOpen = true;
            }
        }
        await session.SendOkAsync(pkt.Cmd, ct: ct);
    }

    private async Task HandleReStartPrivateAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        // Client wants to restart a private room after a live finishes
        // (continue playing without recreating the room).
        // We reset the room to Entrance state and clear per-round properties.
        if (session.CurrentRoomId != null)
        {
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            if (room != null)
            {
                // Keep players, reset matchmaking status
                room.IsMatchmakingOpen = false; // private stays closed
                // Extend TTL
                room.ExpiresAt = DateTime.UtcNow.AddSeconds(room.TTL);
                _logger.LogInformation("Private room {RoomId} restarted", room.RoomID);
            }
        }
        await session.SendOkAsync(pkt.Cmd, ct: ct);
    }

    // ── Helpers ──

    private JoinRoomPayload BuildJoinPayload(Models.Multiplayer.Room room, string userId)
    {
        room.Players.TryGetValue(userId, out var me);
        return new JoinRoomPayload
        {
            roomCreateTime = room.RoomCreateTime,
            roomId = room.RoomID,
            isJoined = true,
            ownerId = room.OwnerID,
            roomProperty = room.RoomProperty,
            playerProperty = me?.PlayerProperty,
            userIds = room.Players.Keys.ToArray(),
            userId = userId,
            privateRoomNumber = room.PrivateRoomNumber ?? 0
        };
    }

    private async Task BroadcastMemberJoinAsync(Models.Multiplayer.Room room, string joinedUserId, CancellationToken ct)
    {
        var payload = new UserIdPayload { userId = joinedUserId };
        var encoded = RealtimeProtocol.EncodeOk(RealtimeProtocol.RoomJoin, payload);

        foreach (var peer in _server.GetRoomSessions(room.RoomID))
        {
            if (peer.UserId != joinedUserId)
                await peer.SendAsync(encoded, ct);
        }
    }

    private async Task BroadcastMemberLeaveAsync(Models.Multiplayer.Room room, string leftUserId, CancellationToken ct)
    {
        var payload = new UserIdPayload { userId = leftUserId };
        var encoded = RealtimeProtocol.EncodeOk(RealtimeProtocol.RoomLeave, payload);

        foreach (var peer in _server.GetRoomSessions(room.RoomID))
        {
            await peer.SendAsync(encoded, ct);
        }
    }

    private async Task BroadcastToRoomAsync(string roomId, uint cmd, object body, string? excludeUserId, CancellationToken ct)
    {
        var encoded = RealtimeProtocol.EncodeOk(cmd, body);
        foreach (var peer in _server.GetRoomSessions(roomId))
        {
            if (peer.UserId != excludeUserId)
                await peer.SendAsync(encoded, ct);
        }
    }
}
