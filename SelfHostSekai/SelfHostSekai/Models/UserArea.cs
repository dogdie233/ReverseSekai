using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

using SekaiApiModel.Sekai;

namespace SelfHostSekai.Models;

public enum AreaStatusType
{
    Unreleased,
    Released,
}

[PrimaryKey(nameof(UserId), nameof(AreaId))]
public class UserArea
{
    public required long UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
    public required int AreaId { get; set; }
    
    public List<UserActionSet> ActionSets { get; set; } = [];
    public List<UserAreaItem> AreaItems { get; set; } = [];
    public AreaStatusType Status { get; set; }
    public int? PlaylistId { get; set; }
    public AreaStatusType PlaylistStatus { get; set; }
}