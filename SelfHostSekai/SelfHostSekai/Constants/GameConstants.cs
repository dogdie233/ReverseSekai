using SekaiApiModel.Sekai;

namespace SelfHostSekai.Constants;

public static class GameConstants
{
    public static readonly string AppVersion = "6.3.5";
    public static readonly string MultiPlayVersion = "kaito";
    public static readonly string DataVersion = "6.3.5.50";
    public static readonly string AssetVersion = "6.3.5.50";
    public static readonly string RemoveAssetVersion = "1.13.0.30";
    public static readonly string AssetHash = "7bb38b4c-f954-adb8-9f04-7f653967ad6b";
    public static readonly string AppVersionStatus = "available";
    public static readonly bool IsStreamingVirtualLiveForceOpenUser = false;
    
    public static readonly string[] SuiteMasterSplitPath =
    {
        "suitemasterfile/6.3.5.50/00_d2c9bd1a8584b8343087a55908bfe20b5f527106deec31e28c647ce7f111a65c",
        "suitemasterfile/6.3.5.50/01_d2c9bd1a8584b8343087a55908bfe20b5f527106deec31e28c647ce7f111a65c",
        "suitemasterfile/6.3.5.50/02_d2c9bd1a8584b8343087a55908bfe20b5f527106deec31e28c647ce7f111a65c",
        "suitemasterfile/6.3.5.50/03_d2c9bd1a8584b8343087a55908bfe20b5f527106deec31e28c647ce7f111a65c",
        "suitemasterfile/6.3.5.50/04_d2c9bd1a8584b8343087a55908bfe20b5f527106deec31e28c647ce7f111a65c",
        "suitemasterfile/6.3.5.50/05_d2c9bd1a8584b8343087a55908bfe20b5f527106deec31e28c647ce7f111a65c",
        "suitemasterfile/6.3.5.50/06_d2c9bd1a8584b8343087a55908bfe20b5f527106deec31e28c647ce7f111a65c"
    };

    public static readonly int[] ObtainedBondsRewardIds = [];
    public static readonly SystemAppVersion LatestSystemAppVersion = new()
    {
        systemProfile = "production",
        appVersion = AppVersion,
        multiPlayVersion = MultiPlayVersion,
        assetVersion = AssetVersion,
        appVersionStatus = AppVersionStatus
    };
}