using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace SelfHostSekai.Models;

public enum UnlockCategoryType
{
    Costume3d = 0,
    ReleaseCondition = 1,
    Stamp = 2,
}

[PrimaryKey(nameof(UserId), nameof(Category), nameof(ItemId))]
public class UserUnlock
{
    public required long UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
    
    public required UnlockCategoryType Category { get; set; }
    public required int ItemId { get; set; }
    
    public ulong UnlockAt { get; set; }
}