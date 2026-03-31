using Microsoft.AspNetCore.Mvc;

using SekaiApiModel.Sekai;

using SelfHostSekai.Constants;

namespace SelfHostSekai.Controllers;

[ApiController]
[Route("api/system")]
public class SystemController : ControllerBase
{
    [HttpGet("")]
    public ActionResult<SystemFullResponse> Get()
    {
        if (!TimeZoneInfo.TryConvertWindowsIdToIanaId(TimeZoneInfo.Local.Id, out var timezone))
            timezone = TimeZoneInfo.Local.Id;
        return Ok(new SystemFullResponse
        {
            appVersions = [GameConstants.LatestSystemAppVersion],
            serverDate = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            timezone = timezone,
            fixedFlg = false,
            profile = "production",
        });
    }
}