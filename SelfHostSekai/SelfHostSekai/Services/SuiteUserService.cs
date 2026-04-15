using Microsoft.EntityFrameworkCore;

using SelfHostSekai.Data;
using SelfHostSekai.Models;

using SekaiApiModel.Sekai;

using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

using SekaiMasterDb;

using SelfHostSekai.Configuration;
using SelfHostSekai.Constants;
using SelfHostSekai.Services.ReleaseConditions;

namespace SelfHostSekai.Services;

/// <summary>
/// 将与用户数据抓取、转换和构建 SuiteUser 的逻辑全部提取到 Service 中
/// 以供所有的 Controllers (如 AuthController, SuiteUserController 等) 复用
/// </summary>
public partial class SuiteUserService
{
    private readonly ILogger<SuiteUserService> _logger;
    private readonly AppDbContext _dbContext;
    private readonly JwtService _jwtService;
    private readonly IOptions<UserInitOptions> _userInitOptions;
    private readonly MasterDb _masterDb;
    private readonly ReleaseConditionManager _releaseConditionManager;

    public SuiteUserService(AppDbContext dbContext, ILogger<SuiteUserService> logger, JwtService jwtService, IOptions<UserInitOptions> userInitOptions, MasterDb masterDb, ReleaseConditionManager releaseConditionManager)
    {
        _dbContext = dbContext;
        _logger = logger;
        _jwtService = jwtService;
        _userInitOptions = userInitOptions;
        _masterDb = masterDb;
        _releaseConditionManager = releaseConditionManager;
    }

    public async Task<bool> IsUserExistAsync(long userId)
    {
        return await _dbContext.Users.AnyAsync(u => u.Id == userId);
    }

    /// <summary>
    /// 获取完整的经过 EF Core 预加载组合的数据库用户对象
    /// </summary>
    public async Task<User?> GetUserWithoutTrackingAsync(long userId)
    {
        return await _dbContext.Users
            .TagWith("Query User with all related data")
            .AsNoTracking()
            .AsSplitQuery()
            .Include(u => u.Cards)
            .Include(u => u.Decks)
            .Include(u => u.Items)
            .Include(u => u.MusicResults)
            .Include(u => u.Musics)
            .Include(u => u.Areas)
            .Include(u => u.Unlocks)
            .Include(u => u.Characters)
            .Include(u => u.Presents)
            .Include(u => u.LoginBonuses)
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<UserGamedata?> UpdateUserNameAsync(long userId, string name)    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
            return null;
        user.Name = name;
        await _dbContext.SaveChangesAsync();
        return BuildUserGameData(user);
    }

    public async Task<(User user, string credToken)> RegisterUser(long userId, string? platform, string? deviceModel, string? operatingSystem)
    {
        var registerTimestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var userInitConfig = _userInitOptions.Value;
        var defaultMembers = userInitConfig.CardIds.Take(5).ToArray();

        var characterIds = _masterDb.CharacterProfiles.Value.All.Select(c => c.characterId).ToHashSet();

        var unlockCostume3Ds = _masterDb.Costume3ds.Value.All
            .Where(c => c.howToObtain == userInitConfig.Costume3dUnlockDesc)
            .ToArray();
        var unlockCostume3dIds = unlockCostume3Ds
            .Select(c => new UserUnlock
            {
                UserId = userId,
                Category = UnlockCategoryType.Costume3d,
                ItemId = c.id,
                UnlockAt = registerTimestamp
            })
            .ToArray();
        var unlockStamps = userInitConfig.StampIds
            .Select(i => new UserUnlock
            {
                UserId = userId,
                Category = UnlockCategoryType.Stamp,
                ItemId = i,
                UnlockAt = registerTimestamp
            });

        var unlocks = new List<UserUnlock>();
        unlocks.AddRange(unlockCostume3dIds);
        unlocks.AddRange(unlockStamps);

        var user = new User
        {
            Id = userId,
            Name = userInitConfig.UserName,
            Rank = 1,
            Exp = 0,
            TotalExp = 0,
            Coin = 0,
            VirtualCoin = 0,
            CurrentDeckNumber = 1,
            RegistrationInfo = new UserRegistration
            {
                userId = userId,
                platform = platform,
                deviceModel = deviceModel,
                operatingSystem = operatingSystem,
                registeredAt = registerTimestamp,
                signature = _jwtService.GenerateUserIdToken(userId),
            },
            Config = GameConstants.UserInitUserConfig,
            Currency = new ChargedCurrency
            {
                free = 0,
                paid = 0,
                paidUnitPrices = []
            },
            BoostInfo = new Boost
            {
                current = 114,
                recoveryAt = registerTimestamp,
            },
            TutorialInfo = new UserTutorial
            {
                tutorialStatus = "start",
                tutorialEndAt = 0,
            },
            Musics = userInitConfig.MusicVocalIds.Select(id => new Models.UserMusic
                {
                    UserId = userId,
                    VocalId = id,
                    MusicId = _masterDb.MusicVocals.Value.GetById(id)?.musicId ?? -1
                })
                .Where(m => m.MusicId != -1)
                .ToArray(),
            Cards = userInitConfig.CardIds.Select(id => new Models.UserCard
                {
                    UserId = userId,
                    CardId = id
                })
                .ToArray(),
            Decks =
            [
                new Models.UserDeck
                {
                    UserId = userId,
                    DeckId = 1,
                    Name = "Default Deck",
                    Member1 = defaultMembers[0],
                    Member2 = defaultMembers[1],
                    Member3 = defaultMembers[2],
                    Member4 = defaultMembers[3],
                    Member5 = defaultMembers[4]
                }
            ],
            Areas = GameConstants.BuildUserInitUserAreas(userId),
            Unlocks = unlocks,
            Shops = _masterDb.Shops.Value.All
                .Select(shop => new SekaiApiModel.Sekai.UserShop
                {
                    shopId = shop.id,
                    userShopItems = _masterDb.ShopItems.Value.All
                        .Where(item => item.shopId == shop.id)
                        .Select(item => new SekaiApiModel.Sekai.UserShopItem
                        {
                            shopItemId = item.id,
                            level = 0,
                            status = "purchasable",
                        })
                        .ToArray()
                })
                .ToList(),
            Characters = characterIds.Select(id => new Models.UserCharacter
            {
                UserId = userId,
                CharacterId = id,
                Costumes3Ds = GetCharacterCostume3Ds(id)
            }).ToArray()
        };
        _dbContext.Users.Add(user);

        foreach (var conditionId in userInitConfig.ReleaseConditions)
        {
            await _releaseConditionManager.UnlockAsync(user, conditionId);
        }

        await _dbContext.SaveChangesAsync();
        var credToken = _jwtService.GenerateCredToken(userId);

        _logger.LogInformation("Registered new user with ID {UserId}", userId);

        return (user, credToken);

        List<CharacterCostume3D> GetCharacterCostume3Ds(int characterId)
        {
            if (characterId == 21) // miku
                return GameConstants.UserInitMikuCostume3Ds;
            
            return
            [
                new CharacterCostume3D
                {
                    Unit = characterId switch
                    {
                        >= 1 and <= 4 => CharacterCostume3D.UnitType.LightSound,
                        >= 5 and <= 8 => CharacterCostume3D.UnitType.Idol,
                        >= 9 and <= 12 => CharacterCostume3D.UnitType.Street,
                        >= 13 and <= 16 => CharacterCostume3D.UnitType.ThemePark,
                        >= 17 and <= 20 => CharacterCostume3D.UnitType.SchoolRefusal,
                        _ => CharacterCostume3D.UnitType.Piapro
                    },
                    HeadId = unlockCostume3Ds.First(c => c.characterId == characterId && c.partType == "head").id,
                    HairId = unlockCostume3Ds.First(c => c.characterId == characterId && c.partType == "hair").id,
                    BodyId = unlockCostume3Ds.First(c => c.characterId == characterId && c.partType == "body").id,
                }
            ];
        }
    }

    private static string ToSnakeCase(string text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (text.Length < 2)
            return text.ToLowerInvariant();

        var sb = new StringBuilder();
        sb.Append(char.ToLowerInvariant(text[0]));
        for (var i = 1; i < text.Length; ++i)
        {
            var c = text[i];
            if (char.IsUpper(c))
            {
                sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static UserGamedata BuildUserGameData(User dbUser)
    {
        return new UserGamedata
        {
            userId = dbUser.Id,
            name = dbUser.Name,
            deck = dbUser.CurrentDeckNumber,
            rank = dbUser.Rank,
            exp = dbUser.Exp,
            totalExp = dbUser.TotalExp,
            coin = dbUser.Coin,
            virtualCoin = dbUser.VirtualCoin
        };
    }
}