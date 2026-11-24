namespace AstralParty.Toys.Mobile;

/// <summary>Container-relative layout constants shared by detection and installation.</summary>
public static class MobilePaths
{
    public const string AddressablesRelative = "com.unity.addressables";
    public const string BundlesRelative = AddressablesRelative + "/AssetBundles";
    public const string LoaderRelative = "CesiumLoader";

    /// <summary>加载器目录下的启用模组目录。</summary>
    public const string ModsRelative = "mods";

    /// <summary>加载器目录下的停用模组目录（启停 = 整目录在这两个之间搬移）。</summary>
    public const string DisabledModsRelative = "disabled_mods";

    /// <summary>
    /// 包名 / bundle id 里出现这些字样之一，就认为「可能是这款游戏」。渠道服很多
    /// （国服、好友快爆、4399、国际服…），固定清单永远追不上，所以按特征扫、让玩家确认。
    /// </summary>
    public static readonly string[] GameIdMarkers =
    [
        "astralparty",
        "feimo",
        "jxpd",
        "star-engine",
    ];

    /// <summary>命中原因（用于界面展示）。</summary>
    public static string? MatchGameIdMarker(string appId)
    {
        foreach (var marker in GameIdMarkers)
        {
            if (appId.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return marker;
        }

        return null;
    }

    public static readonly string[] IosCandidateIds =
    [
        "com.feimo.astralpartycn",
        "com.feimo.astralparty",
        "com.feimo.jxpd",
        "com.feimo.astralparty.cesium",
    ];

    public static readonly string[] AndroidCandidateIds =
    [
        "com.feimo.astralparty",
        "com.jxpd.feimo.m4399",
        "com.feimo.astralparty.haoyou",
    ];
}
