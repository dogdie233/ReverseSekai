using System.Collections.Concurrent;
using System.Net.WebSockets;
using SelfHostSekai.Realtime.Handlers;

namespace SelfHostSekai.Realtime;

/// <summary>
/// WebSocket middleware that replaces the Diarkis UDP/TCP server.
/// Endpoint: /realtime?clientKey={key}&sid={sid}&userId={userId}
/// Manages all active sessions and provides room-level session lookup.
/// </summary>
public class RealtimeServer
{
    private readonly ConcurrentDictionary<string, RealtimeSession> _sessions = new(); // sessionId → session

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RealtimeServer> _logger;

    public RealtimeServer(IServiceProvider serviceProvider, ILogger<RealtimeServer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Get all active sessions in a given room.
    /// </summary>
    public IEnumerable<RealtimeSession> GetRoomSessions(string roomId)
    {
        return _sessions.Values.Where(s => s.IsOpen && s.CurrentRoomId == roomId);
    }

    /// <summary>
    /// ASP.NET Core middleware entry point.
    /// </summary>
    public async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        // Extract auth from query (mirrors Diarkis auth flow)
        var userId = context.Request.Query["userId"].FirstOrDefault();
        var clientKey = context.Request.Query["clientKey"].FirstOrDefault();

        if (string.IsNullOrEmpty(userId))
        {
            context.Response.StatusCode = 401;
            return;
        }

        var ws = await context.WebSockets.AcceptWebSocketAsync();
        var session = new RealtimeSession(userId, ws, _logger);

        _sessions[session.SessionId] = session;
        _logger.LogInformation("WS connected: session={Sid} user={UserId}", session.SessionId, userId);

        try
        {
            await RunSessionLoopAsync(session, context.RequestAborted);
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WS error: session={Sid}", session.SessionId);
        }
        catch (OperationCanceledException)
        {
            // Normal disconnect
        }
        finally
        {
            await CleanupSessionAsync(session);
        }
    }

    private async Task RunSessionLoopAsync(RealtimeSession session, CancellationToken ct)
    {
        var buffer = new byte[8192];

        while (session.IsOpen && !ct.IsCancellationRequested)
        {
            var result = await session.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            if (result.MessageType != WebSocketMessageType.Binary)
                continue;

            // Accumulate if not end of message (large packets)
            byte[] data;
            if (result.EndOfMessage)
            {
                data = buffer[..result.Count];
            }
            else
            {
                using var ms = new MemoryStream();
                ms.Write(buffer, 0, result.Count);
                while (!result.EndOfMessage)
                {
                    result = await session.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    ms.Write(buffer, 0, result.Count);
                }
                data = ms.ToArray();
            }

            try
            {
                var pkt = RealtimeProtocol.Decode(data);
                await DispatchPacketAsync(session, pkt, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Packet decode/dispatch error: session={Sid}", session.SessionId);
            }
        }
    }

    private async Task DispatchPacketAsync(RealtimeSession session, RealtimeProtocol.Packet pkt, CancellationToken ct)
    {
        // Create a scope for DI resolution
        using var scope = _serviceProvider.CreateScope();
        var roomHandler = scope.ServiceProvider.GetRequiredService<RoomCommandHandler>();

        await roomHandler.HandleAsync(session, pkt, ct);
    }

    private async Task CleanupSessionAsync(RealtimeSession session)
    {
        _sessions.TryRemove(session.SessionId, out _);

        // Auto-leave room
        if (session.CurrentRoomId != null)
        {
            using var scope = _serviceProvider.CreateScope();
            var roomService = scope.ServiceProvider.GetRequiredService<Services.Multiplayer.IRoomService>();
            await roomService.LeaveRoomAsync(session.CurrentRoomId, session.UserId);

            // Notify remaining members
            var leavePayload = RealtimeProtocol.EncodeOk(
                RealtimeProtocol.RoomLeave,
                new SekaiApiModel.CP.Realtime.UserIdPayload { userId = session.UserId });

            foreach (var peer in GetRoomSessions(session.CurrentRoomId))
            {
                await peer.SendAsync(leavePayload);
            }
        }

        if (session.Socket.State == WebSocketState.Open)
        {
            try
            {
                await session.Socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { /* best effort */ }
        }

        _logger.LogInformation("WS disconnected: session={Sid} user={UserId}", session.SessionId, session.UserId);
    }
}
