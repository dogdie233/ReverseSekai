using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SekaiApiModel.Sekai;
using SelfHostSekai.Configuration;
using SelfHostSekai.Services;
using SelfHostSekai.Utils;
using Microsoft.Extensions.Options;

namespace SelfHostSekai.Controllers;

/// <summary>
/// GET /api/user/{id}/user_diarkis_auth
/// Returns connection credentials for the realtime server.
/// The client originally connects to a Diarkis UDP server;
/// we redirect it to our WebSocket endpoint instead.
///
/// The udpHost/udpPort fields are repurposed:
///   udpHost → WebSocket host (same as HTTP server)
///   udpPort → WebSocket port (same as HTTP server)
///   tcpHost/tcpPort → same (fallback)
///
/// Encryption keys are generated per-session but not enforced
/// (WebSocket + TLS provides transport security).
/// </summary>
[Authorize]
[ApiController]
[Route("api/user/{userId:long}")]
public class DiarkisAuthController : ControllerBase
{
    private readonly ILogger<DiarkisAuthController> _logger;
    private readonly DiarkisOptions _diarkisOptions;

    public DiarkisAuthController(
        IOptions<DiarkisOptions> diarkisOptions,
        ILogger<DiarkisAuthController> logger)
    {
        _diarkisOptions = diarkisOptions.Value;
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

        // Generate per-session encryption keys (for protocol compatibility)
        var encryptionKey = Guid.NewGuid().ToString("N")[..32];
        var encryptionIv = Guid.NewGuid().ToString("N")[..16];
        var encryptionMacKey = Guid.NewGuid().ToString("N");

        var response = new UserDiarkisAuthResponse
        {
            userId = actualUserId,
            clientKey = clientKey,
            tcpHost = _diarkisOptions.Host,
            tcpPort = _diarkisOptions.Port,
            udpHost = _diarkisOptions.Host,
            udpPort = _diarkisOptions.UdpPort,
            sid = sessionId,
            encryptionKey = encryptionKey,
            encryptionIv = encryptionIv,
            encryptionMacKey = encryptionMacKey
        };

        _logger.LogInformation(
            "Diarkis auth: user={UserId} session={Sid} clientKey={Key}",
            userId, sessionId, clientKey);

        return Ok(response);
    }
}
