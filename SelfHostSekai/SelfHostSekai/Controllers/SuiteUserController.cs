using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SelfHostSekai.Services;
using SelfHostSekai.Utils;
using SekaiApiModel.Sekai;

namespace SelfHostSekai.Controllers;

[ApiController]
[Route("api/suite/user")]
[Authorize]
public class SuiteUserController : ControllerBase
{
    private readonly SuiteUserService _suiteUserService;

    public SuiteUserController(SuiteUserService suiteUserService)
    {
        _suiteUserService = suiteUserService;
    }

    [HttpGet("{requestUserId:long}")]
    public async Task<IActionResult> GetSuiteUser(long requestUserId)
    {
        // 关键要求：不用管 url 栏的 userId，直接从 JWT 获取
        var actualUserId = User.GetUserIdRequired();

        // 通过抽象出的服务获取并组装
        var dbUser = await _suiteUserService.GetUserWithoutTrackingAsync(actualUserId);
        if (dbUser == null)
            return NotFound(new { message = "User not found." });

        var suiteUser = _suiteUserService.BuildSuiteUserDto(dbUser);

        // 返回完整的数据
        return Ok(suiteUser);
    }
}
