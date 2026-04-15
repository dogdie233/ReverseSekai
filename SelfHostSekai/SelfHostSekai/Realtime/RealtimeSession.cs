using System.Net.WebSockets;

namespace SelfHostSekai.Realtime;

/// <summary>
/// Represents a single authenticated WebSocket session (one game client).
/// Tracks the userId and the roomId the user is currently in.
/// </summary>
public class RealtimeSession
{
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    public string UserId { get; }
    public WebSocket Socket { get; }
    public string? CurrentRoomId { get; set; }

    private readonly ILogger _logger;

    public RealtimeSession(string userId, WebSocket socket, ILogger logger)
    {
        UserId = userId;
        Socket = socket;
        _logger = logger;
    }

    public bool IsOpen => Socket.State == WebSocketState.Open;

    /// <summary>
    /// Send a raw binary frame to this client.
    /// </summary>
    public async Task SendAsync(byte[] data, CancellationToken ct = default)
    {
        if (!IsOpen) return;
        try
        {
            await Socket.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Binary,
                endOfMessage: true,
                ct);
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "Send failed for session {Sid}/{UserId}", SessionId, UserId);
        }
    }

    /// <summary>
    /// Send a protocol packet (header + payload).
    /// </summary>
    public Task SendPacketAsync(uint cmd, uint status, object? body = null, CancellationToken ct = default)
        => SendAsync(RealtimeProtocol.EncodeResponse(cmd, status, body), ct);

    public Task SendOkAsync(uint cmd, object? body = null, CancellationToken ct = default)
        => SendAsync(RealtimeProtocol.EncodeOk(cmd, body), ct);

    public Task SendErrorAsync(uint cmd, CancellationToken ct = default)
        => SendAsync(RealtimeProtocol.EncodeError(cmd), ct);
}
