using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using DiarkisServer.Protocol;
using DiarkisServer.Session;

namespace DiarkisServer.Transport;

/// <summary>
/// Raw UDP listener that receives Diarkis-formatted packets.
/// Manages the mapping from remote IPEndPoint → DiarkisSession.
/// Handles Diarkis built-in commands (echo, clientKey, heartbeat) at this layer.
/// All other commands are forwarded to the dispatch callback.
/// </summary>
public class UdpTransport
{
    private UdpClient? _udp;
    private readonly int _port;
    private readonly ILogger<UdpTransport> _logger;
    private CancellationTokenSource? _cts;

    /// <summary>All connected sessions, keyed by remote endpoint string.</summary>
    public ConcurrentDictionary<string, DiarkisSession> Sessions { get; } = new();

    /// <summary>Called for every non-builtin command.</summary>
    public Func<DiarkisSession, DiarkisProtocol.Packet, CancellationToken, Task>? OnPacketReceived { get; set; }

    /// <summary>Called when a session is cleaned up (disconnect/timeout).</summary>
    public Func<DiarkisSession, Task>? OnSessionDisconnected { get; set; }

    public UdpTransport(int port, ILogger<UdpTransport> logger)
    {
        _port = port;
        _logger = logger;
    }

    public void Start()
    {
        _udp = new UdpClient(_port);
        _cts = new CancellationTokenSource();
        _logger.LogInformation("Diarkis UDP listening on port {Port}", _port);

        // Receive loop
        _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));
        // Timeout sweeper
        _ = Task.Run(() => TimeoutSweepAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _udp?.Close();
        _logger.LogInformation("Diarkis UDP stopped");
    }

    public async Task SendToAsync(byte[] data, IPEndPoint remote)
    {
        if (_udp == null) return;
        try
        {
            await _udp.SendAsync(data, data.Length, remote);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "UDP send failed to {Remote}", remote);
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _udp != null)
        {
            try
            {
                var result = await _udp.ReceiveAsync(ct);
                var remote = result.RemoteEndPoint;
                var key = remote.ToString();

                var session = Sessions.GetOrAdd(key, _ => new DiarkisSession(remote, this));
                session.Touch();

                // Try decrypt if session has keys
                byte[] rawData = result.Buffer;
                if (session.EncryptionKey != null && session.EncryptionIv != null && session.EncryptionMacKey != null)
                {
                    var decrypted = DiarkisProtocol.AuthAndDecrypt(
                        session.EncryptionKey, session.EncryptionIv, session.EncryptionMacKey, rawData);
                    if (decrypted != null)
                        rawData = decrypted;
                    // If decrypt fails, try as plaintext (initial handshake may be unencrypted)
                }

                if (rawData.Length < DiarkisProtocol.HeaderSize) continue;

                var pkt = DiarkisProtocol.Decode(rawData);
                await HandlePacketAsync(session, pkt, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "UDP receive error");
            }
        }
    }

    private async Task HandlePacketAsync(DiarkisSession session, DiarkisProtocol.Packet pkt, CancellationToken ct)
    {
        // Handle Diarkis built-in commands at transport layer
        if (pkt.Ver == DiarkisProtocol.UtilVer)
        {
            switch (pkt.Cmd)
            {
                case DiarkisProtocol.EchoCmd:
                    // Echo: reply with same payload
                    await session.SendRawAsync(
                        DiarkisProtocol.Encode(0, DiarkisProtocol.EchoCmd, DiarkisProtocol.StatusOk, pkt.Payload));
                    return;

                case DiarkisProtocol.PingCmd:
                    // Ping: reply with server timestamp
                    var ts = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    await session.SendRawAsync(
                        DiarkisProtocol.Encode(0, DiarkisProtocol.PingCmd, DiarkisProtocol.StatusOk, ts));
                    return;

                case DiarkisProtocol.ClientKeyCmd:
                    // Client sends its clientKey for identification
                    if (pkt.Payload != null)
                    {
                        var clientKey = System.Text.Encoding.UTF8.GetString(pkt.Payload);
                        session.ClientKey = clientKey;
                        _logger.LogInformation("Session {Sid} authenticated with clientKey {Key}",
                            session.SessionId, clientKey[..Math.Min(8, clientKey.Length)] + "...");
                    }
                    await session.SendRawAsync(
                        DiarkisProtocol.Encode(0, DiarkisProtocol.ClientKeyCmd, DiarkisProtocol.StatusOk));
                    return;
            }
        }

        // Forward all other commands to the game handler
        if (OnPacketReceived != null)
            await OnPacketReceived(session, pkt, ct);
    }

    private async Task TimeoutSweepAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);

                var timeout = DateTime.UtcNow.AddSeconds(-30); // 30s timeout
                var expired = Sessions
                    .Where(kv => kv.Value.LastActivity < timeout)
                    .Select(kv => kv.Key)
                    .ToList();

                foreach (var key in expired)
                {
                    if (Sessions.TryRemove(key, out var session))
                    {
                        _logger.LogInformation("Session {Sid} timed out ({UserId})", session.SessionId, session.UserId);
                        if (OnSessionDisconnected != null)
                            await OnSessionDisconnected(session);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Timeout sweep error");
            }
        }
    }
}
