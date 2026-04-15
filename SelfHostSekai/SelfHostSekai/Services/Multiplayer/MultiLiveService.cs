using SekaiApiModel.Sekai;
using SelfHostSekai.Data;
using SelfHostSekai.Models;
using Microsoft.EntityFrameworkCore;

namespace SelfHostSekai.Services.Multiplayer;

/// <summary>
/// Handles MultiLive HTTP API business logic:
///   POST /api/user/{id}/multi-live/{liveId}   → start (consume boost)
///   PUT  /api/user/{id}/multi-live/{liveId}   → submit results
///   POST /api/user/{id}/multi-live-penalty     → penalty for disconnect
/// </summary>
public class MultiLiveService
{
    private readonly AppDbContext _dbContext;
    private readonly SuiteUserService _suiteUserService;
    private readonly ILogger<MultiLiveService> _logger;

    public MultiLiveService(
        AppDbContext dbContext,
        SuiteUserService suiteUserService,
        ILogger<MultiLiveService> logger)
    {
        _dbContext = dbContext;
        _suiteUserService = suiteUserService;
        _logger = logger;
    }

    /// <summary>
    /// Start a MultiLive: consume boost items and return a liveId.
    /// Called by POST /api/user/{id}/multi-live/{liveId}
    /// </summary>
    public async Task<UserMultiLiveResponse?> StartMultiLiveAsync(long userId, string liveId, UserMultiLiveRequest request)
    {
        _logger.LogInformation(
            "MultiLive start: user={UserId} live={LiveId} music={MusicId} lobby={LobbyId} boost={Boost}",
            userId, liveId, request.musicId, request.multiLiveLobbyId, request.boostCount);

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        // Consume boost if requested
        if (request.boostCount > 0)
        {
            // Decrement live bonus or boost item
            // For now, just log it — actual resource consumption depends on boost system
            _logger.LogInformation("Consuming {BoostCount} boosts for user {UserId}", request.boostCount, userId);
        }

        await _dbContext.SaveChangesAsync();

        var suiteUser = _suiteUserService.BuildSuiteUserDto(
            await _suiteUserService.GetUserWithoutTrackingAsync(userId) ?? user);

        return new UserMultiLiveResponse
        {
            updatedResources = suiteUser,
            isInBreakTime = false
        };
    }

    /// <summary>
    /// Submit MultiLive results. Called by PUT /api/user/{id}/multi-live/{liveId}
    /// </summary>
    public async Task<UserMultiLiveClearResponse?> SubmitResultAsync(long userId, string liveId, UserMultiLiveClearRequest request)
    {
        _logger.LogInformation(
            "MultiLive result: user={UserId} live={LiveId} totalScore={Score} superFever={SF}",
            userId, liveId, request.totalScore, request.superFeverFlg);

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        // Record the result for the calling user
        var myScore = FindMyScore(userId, request);
        if (myScore != null)
        {
            await RecordMusicResultAsync(userId, myScore);
        }

        await _dbContext.SaveChangesAsync();

        var suiteUser = _suiteUserService.BuildSuiteUserDto(
            await _suiteUserService.GetUserWithoutTrackingAsync(userId) ?? user);

        return new UserMultiLiveClearResponse
        {
            updatedResources = suiteUser,
            scoreRank = ComputeScoreRank(request.totalScore),
            multiScoreRank = ComputeScoreRank(request.totalScore),
            totalScore = request.totalScore,
            user1 = BuildClearScoreResponse(request.score1),
            user2 = BuildClearScoreResponse(request.score2),
            user3 = BuildClearScoreResponse(request.score3),
            user4 = BuildClearScoreResponse(request.score4),
            user5 = BuildClearScoreResponse(request.score5),
            isInBreakTime = false,
            isNuisance = false,
            isEventMaintenance = false
        };
    }

    /// <summary>
    /// Report a penalty for disconnecting mid-live.
    /// </summary>
    public async Task<UserMultiLivePenalty> ReportPenaltyAsync(long userId, string liveId, long penaltyJudgeStartAt)
    {
        _logger.LogWarning("MultiLive penalty: user={UserId} live={LiveId}", userId, liveId);

        // For private server, we don't apply real penalties
        return new UserMultiLivePenalty
        {
            penaltyEndAt = 0 // no penalty
        };
    }

    // ── Private helpers ──

    private UserMultiLiveClearScoreRequest? FindMyScore(long userId, UserMultiLiveClearRequest request)
    {
        UserMultiLiveClearScoreRequest?[] scores = [request.score1, request.score2, request.score3, request.score4, request.score5];
        return scores.FirstOrDefault(s => s?.userId == userId);
    }

    private async Task RecordMusicResultAsync(long userId, UserMultiLiveClearScoreRequest score)
    {
        // Find existing result for this user+playType=Multi
        // MusicDifficultyId from the API maps to a MusicId+MusicDifficulty pair;
        // for simplicity we store using the difficultyId as the MusicId field.
        var existing = await _dbContext.UserMusicResults
            .FirstOrDefaultAsync(r =>
                r.UserId == userId
                && r.MusicId == score.musicDifficultyId
                && r.PlayType == PlayType.Multi);

        bool isFullCombo = score.missCount == 0 && score.badCount == 0 && score.goodCount == 0;
        bool isAllPerfect = isFullCombo && score.greatCount == 0;

        if (existing != null)
        {
            if (score.score > existing.HighScore)
                existing.HighScore = score.score;
            if (score.maxCombo > existing.MaxCombo)
                existing.MaxCombo = score.maxCombo;
            existing.IsClear = true;
            if (isFullCombo) existing.IsFullCombo = true;
            if (isAllPerfect) existing.IsAllPerfect = true;
        }
        else
        {
            _dbContext.UserMusicResults.Add(new Models.UserMusicResult
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                MusicId = score.musicDifficultyId,
                PlayType = PlayType.Multi,
                HighScore = score.score,
                MaxCombo = score.maxCombo,
                IsClear = true,
                IsFullCombo = isFullCombo,
                IsAllPerfect = isAllPerfect
            });
        }
    }

    private static string ComputeScoreRank(int totalScore)
    {
        return totalScore switch
        {
            >= 9_500_000 => "S+",
            >= 9_000_000 => "S",
            >= 8_000_000 => "A+",
            >= 7_000_000 => "A",
            >= 6_000_000 => "B",
            >= 5_000_000 => "C",
            _ => "D"
        };
    }

    private static UserMultiLiveClearScoreResponse? BuildClearScoreResponse(UserMultiLiveClearScoreRequest? score)
    {
        if (score == null) return null;

        bool fc = score.missCount == 0 && score.badCount == 0 && score.goodCount == 0;
        bool ap = fc && score.greatCount == 0;

        return new UserMultiLiveClearScoreResponse
        {
            userId = score.userId,
            musicDifficultyId = score.musicDifficultyId,
            score = score.score,
            perfectCount = score.perfectCount,
            greatCount = score.greatCount,
            goodCount = score.goodCount,
            badCount = score.badCount,
            missCount = score.missCount,
            maxCombo = score.maxCombo,
            highScoreFlg = false,
            fullComboFlg = fc,
            fullPerfectFlg = ap,
            mvpFlg = false,
            superStarFlg = false,
            clearType = "clear"
        };
    }
}
