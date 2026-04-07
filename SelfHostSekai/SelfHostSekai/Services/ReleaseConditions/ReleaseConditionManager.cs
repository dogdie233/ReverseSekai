using SelfHostSekai.Data;
using SelfHostSekai.Models;

namespace SelfHostSekai.Services.ReleaseConditions;

public class ReleaseConditionManager
{
    private readonly IEnumerable<IReleaseConditionHandler> _handlers;
    private readonly AppDbContext _dbContext;

    public ReleaseConditionManager(IEnumerable<IReleaseConditionHandler> handlers, AppDbContext dbContext)
    {
        _handlers = handlers;
        _dbContext = dbContext;
    }

    public async Task UnlockAsync(User user, int releaseConditionId)
    {
        user.Unlocks ??= new List<UserUnlock>();
        
        bool alreadyUnlocked = user.Unlocks.Any(u => 
            u.Category == UnlockCategoryType.ReleaseCondition && 
            u.ItemId == releaseConditionId);

        if (alreadyUnlocked) return;

        var newUnlock = new UserUnlock
        {
            UserId = user.Id,
            Category = UnlockCategoryType.ReleaseCondition,
            ItemId = releaseConditionId,
            UnlockAt = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            User = user
        };
        user.Unlocks.Add(newUnlock);

        var newIds = new List<int> { releaseConditionId };
        foreach (var handler in _handlers)
        {
            await handler.OnConditionsUnlockedAsync(user, newIds);
        }
    }
}