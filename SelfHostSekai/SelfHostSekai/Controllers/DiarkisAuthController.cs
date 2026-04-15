using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SekaiApiModel.Sekai;
using SelfHostSekai.Utils;
using DiarkisServer;

namespace SelfHostSekai.Controllers;

/// <summary>
/// GET /api/user/{id}/user_diarkis_auth
/// Returns UDP connection credentials for the Diarkis realtime server.
/// The client connects to udpHost:udpPort using Diarkis UDP protocol.
/// </summary>
[Authorize]
[ApiController]
[Route("api/user/{userId:long}")]
public class DiarkisAuthController : ControllerBase
{
    private readonly ILogger<DiarkisAuthController> _logger;
    private readonly DiarkisServerOptions _serverOptions;

    public DiarkisAuthController(
        DiarkisServerOptions serverOptions,
        ILogger<DiarkisAuthController> logger)
    {
        _serverOptions = serverOptions;
        _logger = logger;
    }

    [HttpGet("user_diarkis_auth")]
    public IActionResult GetUserDiarkisAuth(long userId)
    {
        var actualUserId = User.GetUserIdRequired();
        if (actualUserId != userId)
            return Forbid();

        var sessionId = Guid.NewGuid().ToString("N");
        var clientKey = Guid.NewGuid().ToString("N");

        // Per-session encryption keys
        var encryptionKey = Guid.NewGuid().ToString("N")[..32];
        var encryptionIv = Guid.NewGuid().ToString("N")[..16];
        var encryptionMacKey = Guid.NewGuid().ToString("N");

        var response = new UserDiarkisAuthResponse
        {
            userId = actualUserId,
            clientKey = clientKey,
            tcpHost = _serverOptions.Host,
            tcpPort = _serverOptions.UdpPort, // TCP fallback uses same port for simplicity
            udpHost = _serverOptions.Host,
            udpPort = _serverOptions.UdpPort,
            sid = sessionId,
            encryptionKey = encryptionKey,
            encryptionIv = encryptionIv,
            encryptionMacKey = encryptionMacKey
        };

        _logger.LogInformation("Diarkis auth: user={UserId} udp={Host}:{Port}",
            userId, _serverOptions.Host, _serverOptions.UdpPort);

        return Ok(response);
    }
}
