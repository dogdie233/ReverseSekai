using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SelfHostSekai.Models;

public class UserPresent
{
    [Key]
    [MaxLength(36)]
    public required string PresentId { get; set; } // UUID string

    public required long UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>
    /// 用于排序的序列号，越大越靠前
    /// </summary>
    public long Seq { get; set; }

    [MaxLength(64)]
    public required string ResourceType { get; set; }

    public int ResourceId { get; set; }

    public int ResourceLevel { get; set; }

    public int ResourceQuantity { get; set; }

    public long? ExpiredAt { get; set; }

    public long GrantedAt { get; set; }

    [MaxLength(512)]
    public string? Reason { get; set; }
}
