using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SekaiApiModel.Sekai;

using SelfHostSekai.Services;
using SelfHostSekai.Utils;

namespace SelfHostSekai.Controllers;

[Authorize]
[ApiController]
[Route("api/user/{urlUserId:long}/home")]
public class HomeController : ControllerBase
{
    private readonly ILogger<HomeController> _logger;
    private readonly SuiteUserService _userService;
    
    public HomeController(SuiteUserService userService, ILogger<HomeController> logger)
    {
        _userService = userService;
        _logger = logger;
    }
    
    [HttpPut("refresh")]
    public async Task<IActionResult> RefreshHomeData(long urlUserId, [FromBody] UserHomeRefreshRequest request)
    {
        var actualUserId = User.GetUserIdRequired();

        var dbUser = await _userService.GetUserWithoutTrackingAsync(actualUserId);
        if (dbUser == null)
            return Forbid();
        
        var suiteUser = _userService.BuildSuiteUserDto(dbUser);
        var response = new UserHomeRefreshResponse
        {
            updatedResources = suiteUser,
            userLoginBonuses = suiteUser.userLoginBonuses
        };
        if (request.refreshableTypes.Contains("new_pending_friend_request"))
            response.newPendingUserFriends = [];
        if (request.refreshableTypes.Contains("streaming_virtual_live_reward_status"))
            response.receivableRewardStreamingVirtualLiveSchedules = [];
        if (request.refreshableTypes.Contains("web_payments"))
            response.shouldReflectWebPayment = false;
        if (request.refreshableTypes.Contains("receivable_unprocessed_serial_code_campaign"))
            response.receivableUnprocessedSerialCodeCampaignIds = [];
        if (request.refreshableTypes.Contains("get_displayable_offline_event_info"))
            response.displayableOfflineEventIds = [];

        return Ok(response);
    }
}