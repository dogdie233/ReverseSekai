using SelfHostSekai.Constants;
using SelfHostSekai.Models;

using SekaiApiModel.Sekai;
using SekaiApiModel.Sekai.ApiData;
using SekaiApiModel.Sekai.RankLive;

using ApiData = SekaiApiModel.Sekai.ApiData;
using SekaiDto = SekaiApiModel.Sekai;

namespace SelfHostSekai.Services;

public partial class SuiteUserService
{
    /// <summary>
    /// 将 Models.User 实体转换为游戏通讯专用的 SuiteUser DTO
    /// </summary>
    public SuiteUser BuildSuiteUserDto(User dbUser)
    {
        var unlocks = dbUser.Unlocks.ToLookup(u => u.Category);

        return new SuiteUser
        {
            // ── 基础信息 ──────────────────────────────────────────────────────────
            userRegistration = dbUser.RegistrationInfo,
            userGamedata = BuildUserGameData(dbUser),
            userChargedCurrency = dbUser.Currency,
            userBoost = dbUser.BoostInfo,
            refreshableTypes = [],
            userTutorial = dbUser.TutorialInfo,
            userConfig = dbUser.Config,
            now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),

            // ── 区域 ─────────────────────────────────────────────────────────────
            userAreas = dbUser.Areas.Select(a => new SekaiDto.UserArea
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

            // ── 卡牌、牌组 ───────────────────────────────────────────────────────
            userCards = dbUser.Cards.Select(c => new SekaiDto.UserCard
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
            userDecks = dbUser.Decks.Select(d => new SekaiDto.UserDeck
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

            // ── 音乐 ─────────────────────────────────────────────────────────────
            userMusics = dbUser.Musics?.Select(m => new SekaiDto.UserMusic { musicId = m.MusicId })
                .ToArray() ?? [],
            userMusicVocals = dbUser.Musics?.Select(m => new UserMusicVocal
                {
                    musicId = m.MusicId,
                    musicVocalId = m.VocalId
                })
                .ToList() ?? [],
            userMusicResults = dbUser.MusicResults.Select(m => new SekaiDto.UserMusicResult
                {
                    musicId = m.MusicId,
                    musicDifficultyType = ToSnakeCase(m.MusicDifficulty.ToString()),
                    playType = ToSnakeCase(m.PlayType.ToString()),
                    playResult = m.IsAllPerfect ? "full_perfect"
                        : (m.IsFullCombo ? "full_combo"
                        : (m.IsClear ? "clear" : "none")),
                    highScore = m.HighScore,
                    fullComboFlg = m.IsFullCombo || m.IsAllPerfect,
                    fullPerfectFlg = m.IsAllPerfect,
                    mvpCount = 0,
                    superStarCount = 0
                })
                .ToArray(),
            userMusicAchievements = default,

            // ── 商店 ─────────────────────────────────────────────────────────────
            userShops = dbUser.Shops?.ToArray() ?? [],

            // 虚拟商店：每个商店所有道具初始均为"purchasable"状态，购买次数 0
            userVirtualShops = _masterDb.VirtualShops.Value.All
                .Select(vs => new UserVirtualShop
                {
                    virtualShopId = vs.id,
                    userVirtualShopItems = vs.virtualShopItems
                        .Select(item => new UserVirtualShopItem
                        {
                            virtualShopId = vs.id,
                            virtualShopItemId = item.id,
                            status = "purchasable",
                            buyCount = 0
                        })
                        .ToArray()
                })
                .ToArray(),

            // ── 道具 ─────────────────────────────────────────────────────────────
            userPracticeTickets = dbUser.Items.Where(i => i.ItemType == ItemType.PracticeTicket)
                .Select(i => new UserPracticeTicket { practiceTicketId = i.ItemId, quantity = i.Quantity })
                .ToArray(),
            userSkillPracticeTickets = dbUser.Items.Where(i => i.ItemType == ItemType.SkillPracticeTicket)
                .Select(i => new UserSkillPracticeTicket { skillPracticeTicketId = i.ItemId, quantity = i.Quantity })
                .ToArray(),
            userMaterials = dbUser.Items.Where(i => i.ItemType == ItemType.Material)
                .Select(i => new UserMaterial { materialId = i.ItemId, quantity = i.Quantity })
                .ToArray(),
            userBoostItems = dbUser.Items.Where(i => i.ItemType == ItemType.BoostItem)
                .Select(i => new UserBoostItem { boostItemId = i.ItemId, quantity = i.Quantity })
                .ToArray(),
            userGachaTickets = dbUser.Items.Where(i => i.ItemType == ItemType.GachaTicket)
                .Select(i => new UserGachaTicket { gachaTicketId = i.ItemId, quantity = i.Quantity })
                .ToArray(),

            // ── 扭蛋 ─────────────────────────────────────────────────────────────
            userGachas = [],
            userGachaBonusPoints = [],
            userGachaCeilExchanges = _masterDb.GachaCeilExchangeSummaries.Value.All
                .SelectMany(s => s.gachaCeilExchanges)
                .Select(ex => new UserGachaCeilExchange
                {
                    userId = dbUser.Id,
                    gachaCeilExchangeId = ex.id,
                    exchangeStatus = "exchangeable",
                })
                .ToArray(),
            userGachaCeilItems = [],
            userGachaCeilExchangeSubstituteCosts = [],
            userGachaWishes = [],
            userGiftGachaWishes = [],
            userCategorizedGachaWishes = [],
            userGachaFreeResources = [],
            userCustomProfileGachas = [],

            // ── 素材交换 ─────────────────────────────────────────────────────────
            userMaterialExchanges = _masterDb.MaterialExchanges.Value.All
                .Select(m => new UserMaterialExchange
                {
                    userId = dbUser.Id,
                    materialExchangeId = m.id,
                    exchangeCount = 0,
                    totalExchangeCount = 0,
                    exchangeStatus = "exchangeable",
                })
                .ToArray(),

            // ── 解锁/印章/服装 ───────────────────────────────────────────────────
            userStamps = unlocks[UnlockCategoryType.Stamp]
                .Select(u => new UserStamp { stampId = u.ItemId, obtainedAt = u.UnlockAt })
                .ToArray(),
            UserStampFavoriteTabs = [],
            userStampFavorites = [],
            userReleaseConditions = unlocks[UnlockCategoryType.ReleaseCondition]
                .Select(u => new UserReleaseCondition
                {
                    userId = dbUser.Id,
                    releaseConditionId = u.ItemId,
                    createdAt = (long)u.UnlockAt
                })
                .ToArray(),
            newReleaseConditions = [],
            userCostume3dStatuses = unlocks[UnlockCategoryType.Costume3d]
                .Select(u => new UserCostume3DStatus
                {
                    costume3dId = u.ItemId,
                    obtainedAt = (long)u.UnlockAt,
                    status = "available"
                })
                .ToArray(),
            // 服装商店道具：全部标为"purchasable"
            userCostume3dShopItems = _masterDb.Costume3dShopItems.Value.All
                .Select(item => new UserCostume3DShopItem
                {
                    costume3dShopItemId = item.id,
                    status = "purchasable"
                })
                .ToArray(),
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
            userFixCostumes = [],
            userHonors = [],
            userProfileHonors = [],
            userBondsHonors = [],
            userBondsHonorWords = [],

            // ── 单位等级 ─────────────────────────────────────────────────────────
            userUnits = GameConstants.AllUnitNames
                .Select(unitName => new UserUnit
                {
                    userId = dbUser.Id,
                    unit = unitName,
                    rank = 1,
                    exp = 0,
                    totalExp = 0,
                })
                .ToArray(),

            // ── 剧情 ─────────────────────────────────────────────────────────────
            userUnitEpisodeStatuses = dbUser.UnitEpisodeStatuses?.ToArray() ?? [],
            userSpecialEpisodeStatuses = dbUser.SpecialEpisodeStatuses?.ToArray() ?? [],
            userCharacterProfileEpisodeStatuses = dbUser.CharacterProfileEpisodeStatuses?.ToArray() ?? [],
            userStoryMission = new UserStoryMission { progress = 0 },
            userStoryFavorites = [],
            userBookmarkedStories = [],
            userEventArchiveCompleteReadRewards = [],

            // ── 礼物/登录奖励 ────────────────────────────────────────────────────
            userPresents = dbUser.Presents.Select(p => new UserPresentData
                {
                    presentId = p.PresentId,
                    seq = p.Seq,
                    resourceType = p.ResourceType,
                    resourceId = p.ResourceId,
                    resourceLevel = p.ResourceLevel,
                    resourceQuantity = p.ResourceQuantity,
                    expiredAt = p.ExpiredAt ?? 0,
                    reason = p.Reason,
                })
                .ToList(),
            userLoginBonuses = dbUser.LoginBonuses.Select(lb => new SekaiDto.UserLoginBonus
                {
                    userId = lb.UserId,
                    loginBonusId = lb.LoginBonusId,
                    loginBonusType = lb.LoginBonusType,
                    progress = lb.Progress,
                    receivedAt = lb.ReceivedAt,
                    displayTexts = lb.DisplayTexts.ToArray(),
                })
                .ToArray(),

            // ── 角色 ─────────────────────────────────────────────────────────────
            userCharacters = dbUser.Characters.Select(c => new SekaiDto.UserCharacter
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

            // ── 任务系统 ─────────────────────────────────────────────────────────
            userMissionStatuses = [],
            userNormalMissions = [],
            userBeginnerMissions = [],
            userBeginnerMissionV2s = [],
            userLiveMissions = [],
            userEventMissions = [],
            userBeginnerMissionBehavior = new UserBeginnerMissionBehavior
            {
                userBeginnerMissionBehaviorType = "beginner_mission_v2"
            },

            // 荣誉任务：按 honorMissionType 分组，进度和已完成列表均为初始值
            userHonorMissions = _masterDb.HonorMissions.Value.All
                .GroupBy(m => m.honorMissionType)
                .Select(g => new UserHonorMission
                {
                    userId = dbUser.Id,
                    honorMissionType = g.Key,
                    progress = 0,
                    achievedMissionIds = [],
                })
                .ToArray(),

            // ── 个人资料 ─────────────────────────────────────────────────────────
            userProfile = dbUser.Profile,
            viewableAppeal = dbUser.ViewableAppeal,

            // ── 头像 ─────────────────────────────────────────────────────────────
            userAvatar = dbUser.Avatar,
            userAvatarAccessories = [],
            // 全部 Avatar 服装/动作/皮肤色初始均解锁
            userAvatarCostumes = _masterDb.AvatarCostumes.Value.All
                .Select(c => new UserAvatarCostume { avatarCostumeId = c.id })
                .ToArray(),
            userAvatarSkinColors = _masterDb.AvatarSkinColors.Value.All
                .Select(c => new UserAvatarSkinColor { avatarSkinColorId = c.id })
                .ToArray(),
            userAvatarCoordinates = [],
            userAvatarMotions = _masterDb.AvatarMotions.Value.All
                .Select(m => new UserAvatarMotion { avatarMotionId = m.id })
                .ToArray(),
            userAvatarMotionFavorites = _masterDb.AvatarMotions.Value.All
                .Select((m, idx) => new UserAvatarMotionFavorite
                {
                    avatarMotionId = m.id,
                    num = idx + 1
                })
                .ToArray(),

            // ── 荧光棒 ───────────────────────────────────────────────────────────
            userPenlights = _masterDb.Penlights.Value.All
                .Select(p => new UserPenlight { penlightId = p.id, favoriteFlg = false })
                .ToArray(),

            // ── 虚拟现场/票务 ─────────────────────────────────────────────────────
            userVirtualLiveScheduleStatuses = [],
            userVirtualLiveBeginnerScheduleStatuses = [],
            userArchiveVirtualLiveStatuses = [],
            userVirtualLiveRewards = [],
            userVirtualLivePamphlets = [],
            userVirtualLiveTransitionItems = [],
            userStreamingLiveTickets = [],
            userUsedStreamingLiveTickets = [],
            userPaidVirtualLives = new UserPaidVirtualLive { paidVirtualLiveIds = [] },
            userPaidVirtualLiveShopItems = new UserPaidVirtualLiveShopItem { paidVirtualLiveShopItemIds = [] },
            userPaidVirtualLiveStatuses = [],

            // ── 公告/横幅 ────────────────────────────────────────────────────────
            unreadUserTopics = dbUser.UnreadTopics?.ToArray() ?? [],
            userHomeBanners = GameConstants.MockHomeBanners,
            userNews = GameConstants.MockUserNews,
            userOneTimeBehaviors = [],

            // ── 面板任务活动 ─────────────────────────────────────────────────────
            userPanelMissionCampaigns = default,
            userPanelMissions = [],
            userPanelMissionSheets = [],
            userPanelMissionAchievedElements = [],

            // ── 活动 ─────────────────────────────────────────────────────────────
            userEvents = dbUser.Events?.ToArray() ?? [],
            userEventItems = [],
            userEventEpisodeStatuses = [],
            userEventExchanges = [],
            userEventBreakTime = dbUser.EventBreakTime,
            userArchiveEventEpisodeStatuses = [],
            userCheerfulCarnivals = [],
            userCheerfulCarnivalBehaviours = [],
            userCheerfulCarnivalResultRewards = [],
            userOfflineEvents = [],
            userWorldBloomSupportDecks = [],
            userWorldBlooms = [],

            // ── 助力/乘数惩罚 ────────────────────────────────────────────────────
            userBoostGranteds = [],
            userBoostReceivables = [],
            userBoostReceived = new UserBoostReceived { receivedCount = 0, resetAt = 0 },
            userMultiLivePenalty = new UserMultiLivePenalty { penaltyEndAt = 0 },
            userBillingRefundPenalty = new BillingRefundPenalty(),
            userBillingRefunds = [],
            userColorfulPassV2 = new UserColorfulPassV2(),

            // ── 挑战 Live ────────────────────────────────────────────────────────
            userChallengeLivePlayStatuses = [],
            userChallengeLivePlayDay = dbUser.ChallengeLivePlayDay,
            userChallengeLiveSoloDecks = [],
            userChallengeLiveSoloResults = [],
            userChallengeLiveSoloStages = [],
            userChallengeLiveSoloHighScoreRewards = [],
            userCharacterLiveUsageCounts = [],

            // 角色存档语音：收集所有 groupId 的去重列表
            userLiveCharacterArchiveVoice = new UserLiveCharacterArchiveVoice
            {
                characterArchiveVoiceGroupIds = _masterDb.CharacterArchiveVoices.Value.All
                    .Select(v => v.groupId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList()
            },

            // ── 段位赛 ───────────────────────────────────────────────────────────
            userRankMatchSeasons = [],
            userRankMatchResult = new UserRankMatchResult
            {
                liveId = "",
                liveStatus = "none",
            },
            userPreliminaryTournamentLiveResults = [],

            // ── 自动 Live ────────────────────────────────────────────────────────
            userAutoLive = dbUser.AutoLive,

            // ── 好友/社交 ────────────────────────────────────────────────────────
            userFriends = [],
            userBlocks = [],
            userFriendInvitationCampaigns = [],
            userFriendInvitationCampaignMissionRewardCounts = [],

            // ── 自定义名片 ───────────────────────────────────────────────────────
            userCustomProfiles = [],
            userCustomProfileCards = [],
            userCustomProfileResources = [],
            userCustomProfileResourceUsages = [],

            // ── 杂项 ─────────────────────────────────────────────────────────────
            userPlatformInheritIos = new UserPlatformInherit(),
            userPlatformInheritAndroid = new UserPlatformInherit(),
            userInherit = new UserInherit(),
            userPlatforms = [],
            userUnprocessedOrders = [],
            userAdRewards = [],
            userMusicMyList = [],
            userOmikujis = [],
            userSerialCodeItems = [],
            userActionSets = [],

            // ── Mysekai（全部初始为空）───────────────────────────────────────────
            userMysekaiTreasureBoxes = default,
            userMysekaiMaterialPossession = default,
            userMysekaiMaterials = [],
            userMysekaiBlueprints = default,
            userMysekaiItems = default,
            userMysekaiTools = default,
            userMysekaiFixtures = default,
            userMysekaiColorfulPass = new ApiData.UserMysekaiColorfulPass(),
            userMysekaiCanvases = [],
            userMysekaiHarvestMaps = default,
            userMysekaiGamedata = new ApiData.UserMysekaiGamedata(),
            userMysekaiStamina = new ApiData.UserMysekaiStamina(),
            userMysekaiSiteHousingLayouts = default,
            userMysekaiGates = [],
            userMysekaiGateSkin = new ApiData.UserMysekaiGateSkin(),
            userMysekaiGateCharacters = default,
            userMysekaiGateCommonInfo = new ApiData.UserMysekaiGateCommonInfo(),
            userMysekaiMusicRecords = default,
            userMysekaiMusicPlayFixtureSettings = default,
            userMysekaiConvertSlots = default,
            userMysekaiConvertItemHistories = default,
            userMysekaiPhenomenas = default,
            userMysekaiPhotoDecorations = default,
            userMysekaiPhotos = default,
            userMysekaiSiteHousingPresetSlots = default,
            userMysekaiNormalMissionSheet = default,
            userMysekaiNormalMissions = [],
            userMysekaiVisitSetting = new ApiData.UserMysekaiVisitSetting(),
            userMysekaiReleaseElements = [],
            userMysekaiBlueprintShopItems = default,
            userMysekaiFixtureGameCharacterPerformanceBonuses = [],
            userMysekaiCharacterTalks = [],
            userMysekaiHousingCompetitions = [],
            userMysekaiSystemFixtureActions = default,

            // ── 其他新字段 ───────────────────────────────────────────────────────
            userPlayerFrames = [],
            userBirthdayParties = [],
        };
    }
}
