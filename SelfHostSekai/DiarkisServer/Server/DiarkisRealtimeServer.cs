using Microsoft.Extensions.Logging;
using DiarkisServer.Handlers;
using DiarkisServer.Protocol;
using DiarkisServer.Session;
using DiarkisServer.Services;
using DiarkisServer.Transport;

namespace DiarkisServer.Server;

/// <summary>
/// Top-level Diarkis-compatible realtime server.
/// Owns the UdpTransport, dispatches packets to RoomCommandHandler,
/// and provides room-level session lookup for broadcasts.
/// </summary>
public class DiarkisRealtimeServer
{
    private readonly UdpTransport _transport;
    private readonly IRoomService _roomService;
    private readonly IMatchmakingService _matchmakingService;
    private readonly ILogger<DiarkisRealtimeServer> _serverLogger;
    private readonly ILoggerFactory _loggerFactory;

    public DiarkisRealtimeServer(
        DiarkisServerOptions options,
        IRoomService roomService,
        IMatchmakingService matchmakingService,
        ILoggerFactory loggerFactory)
    {
        _roomService = roomService;
        _matchmakingService = matchmakingService;
        _loggerFactory = loggerFactory;
        _serverLogger = loggerFactory.CreateLogger<DiarkisRealtimeServer>();
        _transport = new UdpTransport(options.UdpPort, loggerFactory.CreateLogger<UdpTransport>());
    }

    /// <summary>Get all sessions currently in a room (for broadcasting).</summary>
    public IEnumerable<DiarkisSession> GetRoomSessions(string roomId)
        => _transport.Sessions.Values.Where(s => s.CurrentRoomId == roomId);

    public void Start()
    {
        _transport.OnPacketReceived = OnPacketAsync;
        _transport.OnSessionDisconnected = OnSessionDisconnectedAsync;
        _transport.Start();
        _serverLogger.LogInformation("DiarkisRealtimeServer started");
    }

    public void Stop()
    {
        _transport.Stop();
        _serverLogger.LogInformation("DiarkisRealtimeServer stopped");
    }

    private async Task OnPacketAsync(DiarkisSession session, DiarkisProtocol.Packet pkt, CancellationToken ct)
    {
        // Create handler per dispatch (stateless)
        var handler = new RoomCommandHandler(
            _roomService, _matchmakingService, this,
            _loggerFactory.CreateLogger<RoomCommandHandler>());

        await handler.HandleAsync(session, pkt, ct);
    }

    private async Task OnSessionDisconnectedAsync(DiarkisSession session)
    {
        if (session.CurrentRoomId != null)
        {
            var uid = session.UserId ?? session.SessionId;
            var room = await _roomService.GetRoomAsync(session.CurrentRoomId);
            await _roomService.LeaveRoomAsync(session.CurrentRoomId, uid);

            if (room != null)
            {
                var leavePayload = DiarkisProtocol.EncodeOk(DiarkisProtocol.RoomLeave,
                    new SekaiApiModel.CP.Realtime.UserIdPayload { userId = uid });
                foreach (var peer in GetRoomSessions(session.CurrentRoomId))
                    await peer.SendRawAsync(leavePayload);
            }
        }
    }
}
