using System.Net;
using DiarkisServer.Protocol;
using DiarkisServer.Transport;

namespace DiarkisServer.Session;

/// <summary>
/// Represents one authenticated Diarkis client connected via UDP.
/// Identified by remote IPEndPoint until clientKey authentication completes.
/// </summary>
public class DiarkisSession
{
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
    public IPEndPoint RemoteEndPoint { get; }
    public string? UserId { get; set; }
    public string? ClientKey { get; set; }
    public string? CurrentRoomId { get; set; }
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;
    public bool IsAuthenticated => UserId != null;

    // Per-session encryption keys (from diarkis-auth API response)
    public byte[]? EncryptionKey { get; set; }
    public byte[]? EncryptionIv { get; set; }
    public byte[]? EncryptionMacKey { get; set; }

    private readonly UdpTransport _transport;

    public DiarkisSession(IPEndPoint remote, UdpTransport transport)
    {
        RemoteEndPoint = remote;
        _transport = transport;
    }

    public void Touch() => LastActivity = DateTime.UtcNow;

    /// <summary>
    /// Send a raw packet to this client. Encrypts if keys are set.
    /// </summary>
    public async Task SendRawAsync(byte[] data)
    {
        byte[] toSend;
        if (EncryptionKey != null && EncryptionIv != null && EncryptionMacKey != null)
            toSend = DiarkisProtocol.EncryptAndSign(EncryptionKey, EncryptionIv, EncryptionMacKey, data);
        else
            toSend = data;

        await _transport.SendToAsync(toSend, RemoteEndPoint);
    }

    public Task SendOkAsync(uint cmd, object? body = null)
        => SendRawAsync(DiarkisProtocol.EncodeOk(cmd, body));

    public Task SendErrorAsync(uint cmd)
        => SendRawAsync(DiarkisProtocol.EncodeError(cmd));
}
