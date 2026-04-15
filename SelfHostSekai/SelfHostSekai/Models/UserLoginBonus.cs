using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SelfHostSekai.Models;

[PrimaryKey(nameof(UserId), nameof(LoginBonusType), nameof(LoginBonusId))]
public class UserLoginBonus
{
    public required long UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>
    /// 登录奖励类型: "normal" / "beginner" / "limited"
    /// </summary>
    [MaxLength(32)]
    public required string LoginBonusType { get; set; }

    public required int LoginBonusId { get; set; }

    /// <summary>
    /// 当前已领取天数（progress）
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// 最近一次领取时间戳（ms）
    /// </summary>
    public long ReceivedAt { get; set; }

    /// <summary>
    /// 用于显示的文本提示（通常为空数组）
    /// </summary>
    public List<string> DisplayTexts { get; set; } = [];
}
