using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SekaiApiModel.Sekai;

using SelfHostSekai.Constants;
using SelfHostSekai.Data;
using SelfHostSekai.Services;
using SelfHostSekai.Utils;

namespace SelfHostSekai.Controllers;

[ApiController]
[Route("api/user/{urlUserId:long}")]
public class UserController : ControllerBase
{
    private readonly ILogger<UserController> _logger;
    private readonly AppDbContext _dbContext;
    private readonly JwtService _jwtService;
    private readonly SuiteUserService _suiteUserService;

    public UserController(AppDbContext dbContext, JwtService jwtService, SuiteUserService suiteUserService, ILogger<UserController> logger)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
        _suiteUserService = suiteUserService;
        _logger = logger;
    }

    [HttpPut("auth")]
    public async Task<IActionResult> Auth(long urlUserId, [FromBody] UserAuthRequest request, [FromQuery] bool refreshUpdatedResources = false)
    {
        var validatedUserId = _jwtService.ValidateAndGetUserIdFromCredential(request.credential);
        if (validatedUserId == null) return Unauthorized();
        var userId = validatedUserId.Value;

        var userExist = await _suiteUserService.IsUserExistAsync(userId);
        if (!userExist)
        {
            await _suiteUserService.RegisterUser(userId, null, null, null);
        }

        var sessionTokenGuid = Guid.NewGuid().ToString();
        var sessionTokenString = _jwtService.GenerateSessionToken(userId, sessionTokenGuid);

        var response = new UserAuthResponse
        {
            sessionToken = sessionTokenString,
            appVersion = GameConstants.AppVersion,
            multiPlayVersion = GameConstants.MultiPlayVersion,
            dataVersion = GameConstants.DataVersion,
            assetVersion = GameConstants.AssetVersion,
            removeAssetVersion = GameConstants.RemoveAssetVersion,
            assetHash = GameConstants.AssetHash,
            appVersionStatus = GameConstants.AppVersionStatus,
            isStreamingVirtualLiveForceOpenUser = GameConstants.IsStreamingVirtualLiveForceOpenUser,
            suiteMasterSplitPath = GameConstants.SuiteMasterSplitPath,
            obtainedBondsRewardIds = GameConstants.ObtainedBondsRewardIds
        };

        if (refreshUpdatedResources)
        {
            // TODO: Implement actual logic to determine which resources need to be updated for the user
            response.updatedResources = new SuiteUser();
        }

        return Ok(response);
    }

    [HttpPost("{reportId:guid}")]
    public IActionResult ReportEnvironment(long urlUserId, Guid reportId, [FromBody] UserParamRequest request)
    {
        return Ok();
    }
    
    [HttpPatch]
    public async Task<IActionResult> UpdateUserInfo(long urlUserId, [FromBody] SuiteUser request)
    {
        var userId = User.GetUserIdRequired();

        var suite = new SuiteUser();
        
        if (request.userGamedata is { name: not null })
            suite.userGamedata = await _suiteUserService.UpdateUserNameAsync(userId, request.userGamedata.name);

        return Ok(new SuiteUserCommonResponse
        {
            updatedResources = suite
        });
    }
}