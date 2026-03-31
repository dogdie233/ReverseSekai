using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SekaiApiModel.Sekai;

using SelfHostSekai.Data;

namespace SelfHostSekai.Services;

public class UserTutorialService
{
    private readonly AppDbContext _dbContext;

    public UserTutorialService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserTutorial?> UpdateTutorialProgress(long userId, string tutorialStatus)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) return null;

        user.TutorialInfo ??= new UserTutorial();
        user.TutorialInfo.tutorialStatus = tutorialStatus;
        if (tutorialStatus == "end")
            user.TutorialInfo.tutorialEndAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        _dbContext.Entry(user).Property(u => u.TutorialInfo).IsModified = true;
        await _dbContext.SaveChangesAsync();

        return user.TutorialInfo;
    }
}