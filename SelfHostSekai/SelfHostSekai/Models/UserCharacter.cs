using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace SelfHostSekai.Models;

[PrimaryKey(nameof(UserId), nameof(CharacterId))]
[Index(nameof(CharacterId), IsUnique = false)]
public class UserCharacter
{
    public required long UserId { get; set; }
    public required int CharacterId { get; set; }

    public int Rank { get; set; } = 1;
    public int Exp { get; set; }
    public int TotalExp { get; set; }
    
    public List<CharacterCostume3D> Costumes3Ds { get; set; } = [];
    
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}