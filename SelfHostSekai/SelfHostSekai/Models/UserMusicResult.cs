using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SelfHostSekai.Models;

public enum MusicDifficulty
{
    Easy = 1,
    Normal = 2,
    Hard = 3,
    Expert = 4,
    Master = 5,
    Append = 6 // Project Sekai 引入的新难度
}

public enum PlayType
{
    Solo = 1,
    Multi = 2,
    ChallengeLive = 3,
    RankMatch = 4,
    CheerfulCarnival = 5
}

public class UserMusicResult
{
    [Key]
    public Guid Id { get; set; }

    public required long UserId { get; set; }
    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    public int MusicId { get; set; }
    
    public MusicDifficulty MusicDifficulty { get; set; } = MusicDifficulty.Easy;
    public PlayType PlayType { get; set; } = PlayType.Solo;
    public int HighScore { get; set; } = 0;
    public bool IsClear { get; set; } = false;
    public bool IsFullCombo { get; set; } = false;
    public bool IsAllPerfect { get; set; } = false;
    public int MaxCombo { get; set; } = 0;
}
