using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace SelfHostSekai.Models;

[PrimaryKey(nameof(UserId), nameof(CardId))]
public class UserCard
{
    public required long UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public required int CardId { get; set; }
    public int Level { get; set; } = 1;
    public int MasterRank { get; set; } = 0;
    public int SpecialTrainingStatus { get; set; } = 0;
    public int DefaultImage { get; set; } = 0;
    public int SkillLevel { get; set; } = 1;
    public int Exp { get; set; } = 0;
    
    // 增加SekaiApiModel缺少的属性，以方便完整映射
    public int TotalExp { get; set; } = 0;
    public int SkillExp { get; set; } = 0;
    public int TotalSkillExp { get; set; } = 0;
    public int DuplicateCount { get; set; } = 0;
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    // 是否是初始拥有的状态等等
    public bool IsNew { get; set; } = false;
}
