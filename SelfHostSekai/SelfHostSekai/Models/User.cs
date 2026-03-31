using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using SekaiApiModel.Sekai;

namespace SelfHostSekai.Models;

public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // 通常Project Sekai的ID是雪花算法生成的长整型
    public required long Id { get; set; }

    // Core Identity
    [MaxLength(255)]
    public string Name { get; set; } = "Player";
    public int Rank { get; set; } = 1;
    public int Exp { get; set; } = 0;
    public int TotalExp { get; set; } = 0;
    public int Coin { get; set; } = 0;
    public int VirtualCoin { get; set; } = 0;
    public int CurrentDeckNumber { get; set; } = 0;

    // 1:1 JSON State (这些可以配置为对应数据库的 JSON 字段，例如 PostgreSQL 的 jsonb)
    public UserRegistration? RegistrationInfo { get; set; }
    public UserConfig? Config { get; set; }
    public ChargedCurrency? Currency { get; set; }
    public Boost? BoostInfo { get; set; }
    public UserTutorial? TutorialInfo { get; set; }
    public UserChallengeLivePlayDay? ChallengeLivePlayDay { get; set; }
    public UserEventBreakTime? EventBreakTime { get; set; }
    public UserProfile? Profile { get; set; }
    public ViewableAppeal? ViewableAppeal { get; set; }
    public UserAvatar? Avatar { get; set; }
    public UserAutoLive? AutoLive { get; set; }

    // 杂项 / 状态集合 (JSON)
    public List<UserEpisodeStatus> UnitEpisodeStatuses { get; set; } = new();
    public List<UserEpisodeStatus> SpecialEpisodeStatuses { get; set; } = new();
    public List<UserEpisodeStatus> CharacterProfileEpisodeStatuses { get; set; } = new();
    public List<UserTopic> UnreadTopics { get; set; } = new();
    public List<UserShop> Shops { get; set; } = new();
    public List<UserCharacterMissionV2> CharacterMissions { get; set; } = new();
    public List<UserCharacterMissionV2Status> CharacterMissionStatuses { get; set; } = new();
    public List<UserEvent> Events { get; set; } = new();

    // 1:N 关系导航属性
    public ICollection<UserCard> Cards { get; set; } = new List<UserCard>();
    public ICollection<UserItem> Items { get; set; } = new List<UserItem>();
    public ICollection<UserDeck> Decks { get; set; } = new List<UserDeck>();
    public ICollection<UserMusicResult> MusicResults { get; set; } = new List<UserMusicResult>();
    public ICollection<UserMusic> Musics { get; set; } = new List<UserMusic>();
    public ICollection<UserArea> Areas { get; set; } = new List<UserArea>();
    public ICollection<UserUnlock> Unlocks { get; set; } = new List<UserUnlock>();
    public ICollection<UserCharacter> Characters { get; set; } = new List<UserCharacter>();
}