using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace SelfHostSekai.Models;

public enum ItemType
{
    Material = 1,
    PracticeTicket = 2,
    SkillPracticeTicket = 3,
    BoostItem = 4,
    GachaTicket = 5,
    VirtualLiveTicket = 6,
    EventItem = 7,
    // 如有更多不同种类可自行扩展
}

/// <summary>
/// 统一管理各种零碎的物品（材料，技能书，练习券，加成体力药等）
/// 使用 ItemType 区分枚举
/// </summary>
[PrimaryKey(nameof(UserId), nameof(ItemType), nameof(ItemId))]
public class UserItem
{
    public required long UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    // 物品的大类
    public ItemType ItemType { get; set; }
    
    // 具体物品ID
    public int ItemId { get; set; }
    
    // 数量
    public int Quantity { get; set; }
}
