using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SekaiApiModel.Sekai;
using SelfHostSekai.Services.Multiplayer;
using SelfHostSekai.Utils;

namespace SelfHostSekai.Controllers;

/// <summary>
/// Handles the MultiLive HTTP API endpoints.
/// These are the transactional (non-realtime) calls that bookend a live session:
///   POST /api/user/{id}/multi-live/{liveId}  → start (consume boost, assign liveId)
///   PUT  /api/user/{id}/multi-live/{liveId}  → submit 5-player results
/// </summary>
[Authorize]
[ApiController]
[Route("api/user/{userId:long}/multi-live")]
public class MultiLiveController : ControllerBase
{
    private readonly MultiLiveService _multiLiveService;
    private readonly ILogger<MultiLiveController> _logger;

    public MultiLiveController(MultiLiveService multiLiveService, ILogger<MultiLiveController> logger)
    {
        _multiLiveService = multiLiveService;
        _logger = logger;
    }

    /// <summary>
    /// Start a MultiLive session. Consumes boost, returns updatedResources.
    /// </summary>
    [HttpPost("{liveId}")]
    public async Task<IActionResult> StartMultiLive(long userId, string liveId, [FromBody] UserMultiLiveRequest request)
    {
        var actualUserId = User.GetUserIdRequired();

        var response = await _multiLiveService.StartMultiLiveAsync(actualUserId, liveId, request);
        if (response == null)
            return NotFound();

        return Ok(response);
    }

    /// <summary>
    /// Submit MultiLive results (5-player scores).
    /// </summary>
    [HttpPut("{liveId}")]
    public async Task<IActionResult> SubmitMultiLiveResult(long userId, string liveId, [FromBody] UserMultiLiveClearRequest request)
    {
        var actualUserId = User.GetUserIdRequired();

        var response = await _multiLiveService.SubmitResultAsync(actualUserId, liveId, request);
        if (response == null)
            return NotFound();

        return Ok(response);
    }
}

/// <summary>
/// Penalty reporting endpoint.
/// POST /api/user/{id}/multi-live-penalty
/// </summary>
[Authorize]
[ApiController]
[Route("api/user/{userId:long}/multi-live-penalty")]
public class MultiLivePenaltyController : ControllerBase
{
    private readonly MultiLiveService _multiLiveService;

    public MultiLivePenaltyController(MultiLiveService multiLiveService)
    {
        _multiLiveService = multiLiveService;
    }

    [HttpPost]
    public async Task<IActionResult> ReportPenalty(long userId, [FromBody] UserMultiLivePenaltyRequest request)
    {
        var actualUserId = User.GetUserIdRequired();
        var penalty = await _multiLiveService.ReportPenaltyAsync(actualUserId, request.liveId, request.penaltyJudgeStartAt);

        return Ok(new SuiteUserCommonResponse
        {
            updatedResources = new SuiteUser
            {
                userMultiLivePenalty = penalty
            }
        });
    }
}
