using SekaiApiModel.Sekai;

using SelfHostSekai.Models;

namespace SelfHostSekai.Constants;

public static class GameConstants
{
    public static readonly string AppVersion = "6.4.0";
    public static readonly string MultiPlayVersion = "kaito";
    public static readonly string DataVersion = "6.4.0.11";
    public static readonly string AssetVersion = "6.4.0.10";
    public static readonly string RemoveAssetVersion = "1.13.0.30";
    public static readonly string AssetHash = "8f97a588-5f3a-5e8c-248d-bc49ae996309";
    public static readonly string AppVersionStatus = "available";
    public static readonly bool IsStreamingVirtualLiveForceOpenUser = false;
    
    public static readonly string[] SuiteMasterSplitPath =
    [
        "suitemasterfile/6.4.0.11/00_246ca06e2b3903ca820916fba50625746f24c71ccc1e105e7e8bb221432ea6d5",
        "suitemasterfile/6.4.0.11/01_246ca06e2b3903ca820916fba50625746f24c71ccc1e105e7e8bb221432ea6d5",
        "suitemasterfile/6.4.0.11/02_246ca06e2b3903ca820916fba50625746f24c71ccc1e105e7e8bb221432ea6d5",
        "suitemasterfile/6.4.0.11/03_246ca06e2b3903ca820916fba50625746f24c71ccc1e105e7e8bb221432ea6d5",
        "suitemasterfile/6.4.0.11/04_246ca06e2b3903ca820916fba50625746f24c71ccc1e105e7e8bb221432ea6d5",
        "suitemasterfile/6.4.0.11/05_246ca06e2b3903ca820916fba50625746f24c71ccc1e105e7e8bb221432ea6d5",
        "suitemasterfile/6.4.0.11/06_246ca06e2b3903ca820916fba50625746f24c71ccc1e105e7e8bb221432ea6d5"
    ];

    public static readonly int[] ObtainedBondsRewardIds = [];

    /// <summary>
    /// 全部7个单位名称（对应 unitProfiles.json + "any" 虚拟单位）
    /// </summary>
    public static readonly string[] AllUnitNames =
    [
        "light_sound",
        "idol",
        "street",
        "theme_park",
        "school_refusal",
        "piapro",
        "any",
    ];
    public static readonly SystemAppVersion LatestSystemAppVersion = new()
    {
        systemProfile = "production",
        appVersion = AppVersion,
        multiPlayVersion = MultiPlayVersion,
        assetVersion = AssetVersion,
        appVersionStatus = AppVersionStatus
    };

    public static readonly UserConfig UserInitUserConfig = new()
    {
        defaultMusicType = "sekai",
        isDisplayLoginStatus = true,
        friendRequestScope = "all",
    };
    
    public static readonly List<CharacterCostume3D> UserInitMikuCostume3Ds =
    [
        new()
        {
            Unit = CharacterCostume3D.UnitType.Idol,
            HeadId = 342,
            HairId = 221,
            BodyId = 342,
        },

        new()
        {
            Unit = CharacterCostume3D.UnitType.LightSound,
            HeadId = 340,
            HairId = 221,
            BodyId = 341,
        },

        new()
        {
            Unit = CharacterCostume3D.UnitType.Piapro,
            HeadId = 121,
            HairId = 221,
            BodyId = 42,
        },

        new()
        {
            Unit = CharacterCostume3D.UnitType.SchoolRefusal,
            HeadId = 348,
            HairId = 221,
            BodyId = 349,
        },

        new()
        {
            Unit = CharacterCostume3D.UnitType.Street,
            HeadId = 344,
            HairId = 221,
            BodyId = 345,
        },

        new()
        {
            Unit = CharacterCostume3D.UnitType.ThemePark,
            HeadId = 346,
            HairId = 221,
            BodyId = 347,
        }
    ];

    public static readonly UserHomeBanner[] MockHomeBanners =
    [
        new UserHomeBanner
        {
            homeBannerId = 1342,
            seq = 1,
            homeBannerType = "general",
            name = "公式サイト",
            assetbundleName = "banner_official_store",
            transitionDestinationType = "web",
            transitionDestinationId = 0,
            startAt = 1601391600000,
            endAt = 4102412399000,
            fromUserRank = 0,
            toUserRank = 0,
            url = "https://pjsekai.sega.jp/index.html"
        }
    ];

    public static readonly UserNews[] MockUserNews =
    [
        new UserNews
        {
            id = 4,
            seq = 40,
            displayOrder = 19990,
            informationType = "content",
            informationTag = "update",
            browseType = "external",
            platform = "all",
            title = "プロジェクトセカイ公式サイト",
            path = "https://pjsekai.sega.jp/",
            startAt = 1601391600000,
            endAt = null,
            bannerAssetbundleName = "content_banner_pjsekai_site_v2"
        }
    ];

    public static SelfHostSekai.Models.UserArea[] BuildUserInitUserAreas(long userId) =>
    [
        new()
        {
            UserId = userId,
            AreaId = 1,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = null,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 2,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = null,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 3,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = null,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 4,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = null,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 5,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 1,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 7,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 2,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 8,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 3,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 9,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 4,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 10,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 5,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 11,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = null,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 12,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = null,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 13,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = null,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 14,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 6,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 15,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = null,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 16,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = null,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 17,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 7,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 18,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 8,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 19,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 9,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 20,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 10,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 21,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 11,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 22,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 12,
            PlaylistStatus = Models.AreaStatusType.Unreleased
        },
        new()
        {
            UserId = userId,
            AreaId = 23,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 13,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 24,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 14,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 25,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 15,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 26,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 16,
            PlaylistStatus = Models.AreaStatusType.Released
        },
        new()
        {
            UserId = userId,
            AreaId = 27,
            ActionSets = [],
            AreaItems = [],
            Status = Models.AreaStatusType.Released,
            PlaylistId = 17,
            PlaylistStatus = Models.AreaStatusType.Released
        },
    ];
}