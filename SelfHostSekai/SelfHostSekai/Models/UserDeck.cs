using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SelfHostSekai.Models;

public class UserDeck
{
    [Key]
    public long Id { get; set; }

    public required long UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public int DeckId { get; set; } // 用户内的编队序号，例如1~10
    
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    public int Member1 { get; set; }
    public int Member2 { get; set; }
    public int Member3 { get; set; }
    public int Member4 { get; set; }
    public int Member5 { get; set; }
}
