using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SekaiApiModel.CP.Realtime;
using SelfHostSekai.Services;
using SelfHostSekai.Utils;
using SelfHostSekai.Configuration;
using Microsoft.Extensions.Options;

namespace SelfHostSekai.Controllers;

[Authorize]
[ApiController]
[Route("api/user/{userId:long}")]
public class DiarkisAuthController : ControllerBase
{
    private readonly ILogger<DiarkisAuthController> _logger;
    private readonly DiarkisOptions _diarkisOptions;
    private readonly JwtService _jwtService;

    public DiarkisAuthController(
        IOptions<DiarkisOptions> diarkisOptions, 
        JwtService jwtService,
        ILogger<DiarkisAuthController> logger)
    {
        _diarkisOptions = diarkisOptions.Value;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpGet("user_diarkis_auth")]
    public async Task<IActionResult> GetUserDiarkisAuth(long userId)
    {
        try
        {
            var actualUserId = User.GetUserIdRequired();
            if (actualUserId != userId)
                return Forbid();

            // Generate session ID
            var sessionId = Guid.NewGuid().ToString();
            
            // Generate client key for Diarkis authentication
            var clientKey = Guid.NewGuid().ToString();

            // Generate encryption keys
            var encryptionKey = Guid.NewGuid().ToString("N").Substring(0, 32); // AES-256 requires 32 bytes
            var encryptionIv = Guid.NewGuid().ToString("N").Substring(0, 16);  // IV requires 16 bytes
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

            _logger.LogInformation("Generated Diarkis auth for user {UserId}: session {SessionId}", userId, sessionId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating Diarkis auth for user {UserId}", userId);
            return StatusCode(500, "Internal server error");
        }
    }
}
