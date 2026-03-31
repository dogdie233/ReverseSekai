using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SekaiApiModel.Sekai;

using SelfHostSekai.Services;

using Yitter.IdGenerator;

namespace SelfHostSekai.Controllers;

[ApiController]
[Route("api/user")]
public class UserRegisterController : ControllerBase
{
    private readonly ILogger<UserRegisterController> _logger;
    private readonly SuiteUserService _suiteUserService;

    public UserRegisterController(SuiteUserService suiteUserService, ILogger<UserRegisterController> logger)
    {
        _suiteUserService = suiteUserService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromBody] UserAPIRequest request)
    {
        var userId = YitIdHelper.NextId();
        var (user, cred) = await _suiteUserService.RegisterUser(userId, request.platform, request.deviceModel, request.operatingSystem);
        var suiteUser = _suiteUserService.BuildSuiteUserDto(user);
        var response = new UserAPIResponse
        {
            userRegistration = user.RegistrationInfo,
            credential = cred,
            updatedResources = suiteUser
        };
        return Ok(response);
    }
}