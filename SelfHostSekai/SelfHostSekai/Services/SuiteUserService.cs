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
public class SuiteUserService
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
            .FirstOrDefaultAsync(u => u.Id == userId);
    }

    public async Task<UserGamedata?> UpdateUserNameAsync(long userId, string name)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null)
            return null;
        user.Name = name;
        await _dbContext.SaveChangesAsync();
        return BuildUserGameData(user);
    }

    /// <summary>
    /// 将 Models.User 实体转换为游戏通讯专用的 SuiteUser DTO 
    /// </summary>
    public SuiteUser BuildSuiteUserDto(User dbUser)
    {
        var unlocks = dbUser.Unlocks.ToLookup(u => u.Category);
        return new SuiteUser
        {
            userRegistration = dbUser.RegistrationInfo,
            userGamedata = BuildUserGameData(dbUser),
            userChargedCurrency = dbUser.Currency,
            userBoost = dbUser.BoostInfo,
            refreshableTypes = [],
            userTutorial = dbUser.TutorialInfo,
            userConfig = dbUser.Config,
            userAreas = dbUser.Areas.Select(a => new SekaiApiModel.Sekai.UserArea
                {
                    areaId = a.AreaId,
                    actionSets = a.ActionSets.ToArray(),
                    areaItems = a.AreaItems.ToArray(),
                    userAreaStatus = new UserAreaStatus
                    {
                        areaId = a.AreaId,
                        status = ToSnakeCase(a.Status.ToString()),
                        userAreaPlaylistStatus = a.PlaylistId == null
                            ? null
                            : new UserAreaPlaylistStatus
                            {
                                areaPlaylistId = a.PlaylistId.Value,
                                status = ToSnakeCase(a.PlaylistStatus.ToString())
                            }
                    }
                })
                .ToArray(),
            userCards = dbUser.Cards.Select(c => new SekaiApiModel.Sekai.UserCard
                {
                    userId = c.UserId,
                    cardId = c.CardId,
                    level = c.Level,
                    exp = c.Exp,
                    totalExp = c.TotalExp,
                    skillLevel = c.SkillLevel,
                    skillExp = c.SkillExp,
                    totalSkillExp = c.TotalSkillExp,
                    masterRank = c.MasterRank,
                    specialTrainingStatus = c.SpecialTrainingStatus == 1 ? "done" : "not_done",
                    defaultImage = c.DefaultImage == 1 ? "special_training" : "original",
                    duplicateCount = c.DuplicateCount,
                    createdAt = c.CreatedAt,
                    episodes = []
                })
                .ToArray(),
            userBonds = [],
            userDecks = dbUser.Decks.Select(d => new SekaiApiModel.Sekai.UserDeck
                {
                    userId = d.UserId,
                    deckId = d.DeckId,
                    name = d.Name,
                    leader = d.Member1,
                    subLeader = d.Member2,
                    member1 = d.Member1,
                    member2 = d.Member2,
                    member3 = d.Member3,
                    member4 = d.Member4,
                    member5 = d.Member5
                })
                .ToArray(),
            userMusics = dbUser.Musics?.Select(m => new SekaiApiModel.Sekai.UserMusic
                {
                    musicId = m.MusicId
                })
                .ToArray() ?? [],
            userMusicVocals = dbUser.Musics?.Select(m => new UserMusicVocal
                {
                    musicId = m.MusicId,
                    musicVocalId = m.VocalId
                })
                .ToList() ?? [],
            userMusicResults = dbUser.MusicResults.Select(m => new SekaiApiModel.Sekai.UserMusicResult
                {
                    musicId = m.MusicId,
                    musicDifficultyType = ToSnakeCase(m.MusicDifficulty.ToString()),
                    playType = ToSnakeCase(m.PlayType.ToString()),
                    playResult = m.IsAllPerfect ? "full_perfect" : (m.IsFullCombo ? "full_combo" : (m.IsClear ? "clear" : "none")),
                    highScore = m.HighScore,
                    fullComboFlg = m.IsFullCombo || m.IsAllPerfect,
                    fullPerfectFlg = m.IsAllPerfect,
                    mvpCount = 0,
                    superStarCount = 0
                })
                .ToArray(),
            userShops = dbUser.Shops?.ToArray() ?? [],
            userPracticeTickets = dbUser.Items.Where(i => i.ItemType == ItemType.PracticeTicket).Select(i => new UserPracticeTicket
                {
                    practiceTicketId = i.ItemId,
                    quantity = i.Quantity
                })
                .ToArray(),
            userSkillPracticeTickets = dbUser.Items.Where(i => i.ItemType == ItemType.SkillPracticeTicket).Select(i => new UserSkillPracticeTicket
                {
                    skillPracticeTicketId = i.ItemId,
                    quantity = i.Quantity
                })
                .ToArray(),
            userMaterials = dbUser.Items.Where(i => i.ItemType == ItemType.Material).Select(i => new UserMaterial
                {
                    materialId = i.ItemId,
                    quantity = i.Quantity
                })
                .ToArray(),
            userGachas = [],
            userGachaBonusPoints = [],
            userUnitEpisodeStatuses = dbUser.UnitEpisodeStatuses?.ToArray() ?? [],
            userSpecialEpisodeStatuses = dbUser.SpecialEpisodeStatuses?.ToArray() ?? [],
            userCharacterProfileEpisodeStatuses = dbUser.CharacterProfileEpisodeStatuses?.ToArray() ?? [],
            userUnits = [],
            userPresents = [],
            userCostume3dStatuses = unlocks[UnlockCategoryType.Costume3d].Select(u => new UserCostume3DStatus
                {
                    costume3dId = u.ItemId,
                    obtainedAt = (long)u.UnlockAt,
                    status = "available"
                })
                .ToArray(),
            userCostume3dShopItems = [],
            userCharacterCostume3ds = dbUser.Characters
                .SelectMany(c => c.Costumes3Ds, (character, costume) => new UserCharacterCostume3D
                {
                    characterId = character.CharacterId,
                    unit = ToSnakeCase(costume.Unit.ToString()),
                    headCostume3dId = costume.HeadId,
                    hairCostume3dId = costume.HairId,
                    bodyCostume3dId = costume.BodyId,
                })
                .ToArray(),
            unreadUserTopics = dbUser.UnreadTopics?.ToArray() ?? [],
            userHomeBanners = [],
            userMaterialExchanges = [],
            userGachaCeilExchanges = [],
            userGachaCeilItems = [],
            userGachaCeilExchangeSubstituteCosts = [],
            userBoostItems = dbUser.Items.Where(i => i.ItemType == ItemType.BoostItem).Select(i => new UserBoostItem
                {
                    boostItemId = i.ItemId,
                    quantity = i.Quantity
                })
                .ToArray(),
            userStamps = unlocks[UnlockCategoryType.Stamp].Select(u => new UserStamp
                {
                    stampId = u.ItemId,
                    obtainedAt = u.UnlockAt
                })
                .ToArray(),
            UserStampFavoriteTabs = [],
            userStampFavorites = [],
            userCharacters = dbUser.Characters.Select(c => new SekaiApiModel.Sekai.UserCharacter
                {
                    userId = dbUser.Id,
                    characterId = c.CharacterId,
                    characterRank = c.Rank,
                    exp = c.Exp,
                    totalExp = c.TotalExp,
                })
                .ToArray(),
            userCharacterMissions = dbUser.CharacterMissions?.ToArray() ?? [],
            userCharacterMissionStatuses = dbUser.CharacterMissionStatuses?.ToArray() ?? [],
            userMissionStatuses = [],
            userNormalMissions = [],
            userBeginnerMissions = [],
            userBeginnerMissionV2s = [],
            userLiveMissions = [],
            userEventMissions = [],
            userFixCostumes = [],
            userHonors = [],
            userHonorMissions = [],
            userProfileHonors = [],
            userProfile = dbUser.Profile,
            userChallengeLivePlayStatuses = [],
            userChallengeLivePlayDay = dbUser.ChallengeLivePlayDay,
            userChallengeLiveSoloDecks = [],
            userChallengeLiveSoloResults = [],
            userChallengeLiveSoloStages = default,
            userChallengeLiveSoloHighScoreRewards = default,
            userCharacterLiveUsageCounts = default,
            userOneTimeBehaviors = default,
            userNews = default,
            userVirtualShops = default,
            userVirtualLiveScheduleStatuses = default,
            userVirtualLiveBeginnerScheduleStatuses = default,
            userArchiveVirtualLiveStatuses = default,
            userVirtualLiveRewards = default,
            userPanelMissionCampaigns = default,
            userPanelMissions = default,
            userPanelMissionSheets = default,
            userPanelMissionAchievedElements = default,
            userAvatar = dbUser.Avatar,
            userAvatarAccessories = default,
            userAvatarCostumes = default,
            userAvatarSkinColors = default,
            userAvatarCoordinates = default,
            userAvatarMotions = default,
            userAvatarMotionFavorites = default,
            userPenlights = default,
            userLoginBonuses = default,
            userGachaTickets = dbUser.Items.Where(i => i.ItemType == ItemType.GachaTicket).Select(i => new UserGachaTicket
            {
                gachaTicketId = i.ItemId,
                quantity = i.Quantity
            }).ToArray(),
            userReleaseConditions = unlocks[UnlockCategoryType.ReleaseCondition].Select(u => new UserReleaseCondition
            {
                userId = dbUser.Id,
                releaseConditionId = u.ItemId,
                createdAt = (long)u.UnlockAt
            }).ToArray(),
            newReleaseConditions = default,
            userPlatformInheritIos = default,
            userPlatformInheritAndroid = default,
            userInherit = default,
            userEvents = dbUser.Events?.ToArray() ?? [],
            userEventItems = default,
            userEventEpisodeStatuses = default,
            userEventExchanges = default,
            userEventBreakTime = dbUser.EventBreakTime,
            userMultiLivePenalty = default,
            userBillingRefundPenalty = default,
            userBillingRefunds = default,
            userAutoLive = dbUser.AutoLive,
            now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            userArchiveEventEpisodeStatuses = default,
            userFriends = default,
            userCheerfulCarnivals = default,
            userCheerfulCarnivalBehaviours = default,
            userCheerfulCarnivalResultRewards = default,
            userGachaWishes = default,
            userBoostGranteds = default,
            userBoostReceivables = default,
            userBoostReceived = default,
            userMusicAchievements = default,
            viewableAppeal = dbUser.ViewableAppeal,
            userCustomProfiles = default,
            userCustomProfileCards = default,
            userCustomProfileResources = default,
            userCustomProfileResourceUsages = default,
            userStreamingLiveTickets = default,
            userUsedStreamingLiveTickets = default,
            userVirtualLivePamphlets = default,
            userUnprocessedOrders = default,
            userCustomProfileGachas = default,
            userFriendInvitationCampaigns = default,
            userFriendInvitationCampaignMissionRewardCounts = default,
            userOmikujis = default,
            userBondsHonors = default,
            userBondsHonorWords = default,
            userPreliminaryTournamentLiveResults = default,
            userRankMatchSeasons = default,
            userRankMatchResult = default,
            userGiftGachaWishes = default,
            userActionSets = null,
            userCategorizedGachaWishes = default,
            userBlocks = default,
            userAdRewards = default,
            userMusicMyList = default,
            userGachaFreeResources = default,
            userOfflineEvents = default,
            userColorfulPassV2 = default,
            userPaidVirtualLives = default,
            userPaidVirtualLiveShopItems = default,
            userPaidVirtualLiveStatuses = default,
            userStoryFavorites = default,
            userBookmarkedStories = default,
            userEventArchiveCompleteReadRewards = default,
            userSerialCodeItems = default,
            userMysekaiTreasureBoxes = default,
            userMysekaiMaterialPossession = default,
            userMysekaiMaterials = default,
            userMysekaiBlueprints = default,
            userMysekaiItems = default,
            userMysekaiTools = default,
            userMysekaiFixtures = default,
            userMysekaiColorfulPass = default,
            userMysekaiCanvases = default,
            userMysekaiHarvestMaps = default,
            userMysekaiGamedata = default,
            userMysekaiStamina = default,
            userMysekaiSiteHousingLayouts = default,
            userMysekaiGates = default,
            userMysekaiGateSkin = default,
            userMysekaiGateCharacters = default,
            userMysekaiGateCommonInfo = default,
            userMysekaiMusicRecords = default,
            userMysekaiMusicPlayFixtureSettings = default,
            userMysekaiConvertSlots = default,
            userMysekaiConvertItemHistories = default,
            userMysekaiPhenomenas = default,
            userMysekaiPhotoDecorations = default,
            userMysekaiPhotos = default,
            userMysekaiSiteHousingPresetSlots = default,
            userMysekaiNormalMissionSheet = default,
            userMysekaiNormalMissions = default,
            userMysekaiVisitSetting = default,
            userMysekaiReleaseElements = default,
            userMysekaiBlueprintShopItems = default,
            userBeginnerMissionBehavior = new UserBeginnerMissionBehavior
            {
                userBeginnerMissionBehaviorType = "beginner_mission_v2"
            },
            userWorldBloomSupportDecks = default,
            userWorldBlooms = default,
            userLiveCharacterArchiveVoice = default,
            userStoryMission = default,
            userPlatforms = default,
            userMysekaiFixtureGameCharacterPerformanceBonuses = default,
            userMysekaiCharacterTalks = default,
            userPlayerFrames = default,
            userMysekaiHousingCompetitions = default,
            userBirthdayParties = default,
            userMysekaiSystemFixtureActions = default,
            userVirtualLiveTransitionItems = default,
        };
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