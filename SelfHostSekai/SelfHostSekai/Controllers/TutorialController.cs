using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SekaiApiModel.Sekai;

using SelfHostSekai.Services;
using SelfHostSekai.Utils;

namespace SelfHostSekai.Controllers;

[ApiController]
[Route("api/user/{urlUserId:long}/tutorial")]
public class TutorialController : ControllerBase
{
    private readonly ILogger<TutorialController> _logger;
    private readonly UserTutorialService _userTutorialService;
    private readonly SuiteUserService _suiteUserService;
    
    public TutorialController(ILogger<TutorialController> logger, UserTutorialService userTutorialService, SuiteUserService suiteUserService)
    {
        _logger = logger;
        _userTutorialService = userTutorialService;
        _suiteUserService = suiteUserService;
    }
    
    [Authorize]
    [HttpPatch]
    public async Task<IActionResult> UpdateTutorialProgress(long urlUserId, [FromBody] UserTutorialRequest request)
    {
        _logger.LogDebug("Received tutorial progress update for user {UserId}: {TutorialStatus}", urlUserId, request.tutorialStatus);
        
        var userId = User.GetUserIdRequired();
        
        var tutorialInfo = await _userTutorialService.UpdateTutorialProgress(userId, request.tutorialStatus);
        var dbUser = await _suiteUserService.GetUserWithoutTrackingAsync(userId);
        var suiteUser = _suiteUserService.BuildSuiteUserDto(dbUser!);

        return Ok(new SuiteUserCommonResponse
        {
            updatedResources = suiteUser
        });
    }
}