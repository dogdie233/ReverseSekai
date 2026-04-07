using SelfHostSekai.Data;
using SelfHostSekai.Models;
using SekaiApiModel.Sekai;
using SekaiMasterDb;

namespace SelfHostSekai.Services.ReleaseConditions.Handlers;

public class TopicReleaseConditionHandler : IReleaseConditionHandler
{
    private readonly MasterData<MasterTopic> _masterTopics;
    private readonly AppDbContext _dbContext;

    public TopicReleaseConditionHandler(MasterData<MasterTopic> masterTopics, AppDbContext dbContext)
    {
        _masterTopics = masterTopics;
        _dbContext = dbContext;
    }

    public Task OnConditionsUnlockedAsync(User user, IReadOnlyList<int> newConditionIds)
    {
        var unlockedTopics = _masterTopics.All
            .Where(t => newConditionIds.Contains(t.releaseConditionId))
            .ToList();

        if (unlockedTopics.Count == 0)
            return Task.CompletedTask;

        user.UnreadTopics ??= new List<UserTopic>();
        bool isChanged = false;

        foreach (var masterTopic in unlockedTopics)
        {
            if (!user.UnreadTopics.Any(t => t.topicId == masterTopic.id))
            {
                user.UnreadTopics.Add(new UserTopic { topicId = masterTopic.id });
                isChanged = true;
            }
        }

        if (isChanged)
        {
            _dbContext.Entry(user).Property(u => u.UnreadTopics).IsModified = true;
        }

        return Task.CompletedTask;
    }
}