using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace SelfHostSekai.Models;

[PrimaryKey(nameof(UserId), nameof(VocalId))]
public class UserMusic
{
    public required long UserId { get; set; }
    public int VocalId { get; set; }
    public int MusicId { get; set; }
    
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}