using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SekaiApiModel.Sekai;
using SelfHostSekai.Utils;

namespace SelfHostSekai.Controllers;

/// <summary>
/// GET /api/user/{id}/ingame-cutin?userId2=&userId3=&userId4=&userId5=
/// Returns cutin animation data for the 5 players in a multi-live.
/// We return empty lists — cutins are cosmetic and require master data
/// cross-referencing that isn't critical for gameplay.
/// </summary>
[Authorize]
[ApiController]
[Route("api/user/{userId:long}")]
public class IngameCutinController : ControllerBase
{
    [HttpGet("ingame-cutin")]
    public IActionResult GetIngameCutin(
        long userId,
        [FromQuery] long? userId2,
        [FromQuery] long? userId3,
        [FromQuery] long? userId4,
        [FromQuery] long? userId5)
    {
        return Ok(new UserMultiIngameCutins
        {
            user1Cutins = new(),
            user2Cutins = new(),
            user3Cutins = new(),
            user4Cutins = new(),
            user5Cutins = new()
        });
    }
}

/// <summary>
/// POST /api/user/{id}/stamp-use-history
/// Client reports which stamps (emotes) were used during the multi-live lobby.
/// We accept and discard — stamp analytics are non-essential.
/// </summary>
[Authorize]
[ApiController]
[Route("api/user/{userId:long}/stamp-use-history")]
public class StampUseHistoryController : ControllerBase
{
    [HttpPost]
    public IActionResult PostStampUseHistory(long userId, [FromBody] UserStampUseHistoryRequest request)
    {
        // Accept the data, no processing needed for private server
        return Ok(new SuiteUserCommonResponse());
    }
}
