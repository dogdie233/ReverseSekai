using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SelfHostSekai.Models;

namespace SelfHostSekai.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<UserCard> UserCards { get; set; }
    public DbSet<UserItem> UserItems { get; set; }
    public DbSet<UserDeck> UserDecks { get; set; }
    public DbSet<UserMusicResult> UserMusicResults { get; set; }
    public DbSet<UserMusic> UserMusics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 配置 1:N 关系与级联删除
        modelBuilder.Entity<User>()
            .HasMany(u => u.Cards)
            .WithOne(c => c.User)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Items)
            .WithOne(i => i.User)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.Decks)
            .WithOne(d => d.User)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasMany(u => u.MusicResults)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        modelBuilder.Entity<User>()
            .HasMany(u => u.Musics)
            .WithOne(m => m.User)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        // ==========================================
        // PostgreSQL JSONB 配置 (使用 ValueConverter)
        // 使用 HasConversion 可以避免 EF Core 对深层嵌套对象的解析报错
        // ==========================================
        
        var jsonOptions = new JsonSerializerOptions 
        { 
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            IncludeFields = true
        };

        // 1:1 单体对象 JSON
        modelBuilder.Entity<User>().Property(u => u.RegistrationInfo).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.UserRegistration>(v, jsonOptions));
        modelBuilder.Entity<User>().Property(u => u.Config).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.UserConfig>(v, jsonOptions));
        modelBuilder.Entity<User>().Property(u => u.Currency).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.ChargedCurrency>(v, jsonOptions));
        modelBuilder.Entity<User>().Property(u => u.BoostInfo).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.Boost>(v, jsonOptions));
        modelBuilder.Entity<User>().Property(u => u.TutorialInfo).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.UserTutorial>(v, jsonOptions));
        modelBuilder.Entity<User>().Property(u => u.ChallengeLivePlayDay).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.UserChallengeLivePlayDay>(v, jsonOptions));
        modelBuilder.Entity<User>().Property(u => u.EventBreakTime).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.UserEventBreakTime>(v, jsonOptions));
        modelBuilder.Entity<User>().Property(u => u.Profile).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.UserProfile>(v, jsonOptions));
        modelBuilder.Entity<User>().Property(u => u.ViewableAppeal).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.ViewableAppeal>(v, jsonOptions));
        modelBuilder.Entity<User>().Property(u => u.Avatar).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.UserAvatar>(v, jsonOptions));
        modelBuilder.Entity<User>().Property(u => u.AutoLive).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<SekaiApiModel.Sekai.UserAutoLive>(v, jsonOptions));

        // 1:N 集合数组 JSON
        modelBuilder.Entity<User>().Property(u => u.Areas).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<SekaiApiModel.Sekai.UserArea>>(v, jsonOptions)!);
        modelBuilder.Entity<User>().Property(u => u.ActionSets).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<SekaiApiModel.Sekai.UserActionSet>>(v, jsonOptions)!);
        modelBuilder.Entity<User>().Property(u => u.UnitEpisodeStatuses).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<SekaiApiModel.Sekai.UserEpisodeStatus>>(v, jsonOptions)!);
        modelBuilder.Entity<User>().Property(u => u.SpecialEpisodeStatuses).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<SekaiApiModel.Sekai.UserEpisodeStatus>>(v, jsonOptions)!);
        modelBuilder.Entity<User>().Property(u => u.CharacterProfileEpisodeStatuses).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<SekaiApiModel.Sekai.UserEpisodeStatus>>(v, jsonOptions)!);
        modelBuilder.Entity<User>().Property(u => u.UnreadTopics).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<SekaiApiModel.Sekai.UserTopic>>(v, jsonOptions)!);
        modelBuilder.Entity<User>().Property(u => u.Shops).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<SekaiApiModel.Sekai.UserShop>>(v, jsonOptions)!);
        modelBuilder.Entity<User>().Property(u => u.CharacterMissions).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<SekaiApiModel.Sekai.UserCharacterMissionV2>>(v, jsonOptions)!);
        modelBuilder.Entity<User>().Property(u => u.CharacterMissionStatuses).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<SekaiApiModel.Sekai.UserCharacterMissionV2Status>>(v, jsonOptions)!);
        modelBuilder.Entity<User>().Property(u => u.Events).HasColumnType("jsonb")
            .HasConversion(v => JsonSerializer.Serialize(v, jsonOptions), v => JsonSerializer.Deserialize<List<SekaiApiModel.Sekai.UserEvent>>(v, jsonOptions)!);
    }
}