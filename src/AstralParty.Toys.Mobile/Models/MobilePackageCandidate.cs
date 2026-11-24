namespace AstralParty.Toys.Mobile.Models;

/// <summary>
/// 设备上「可能装了这款游戏」的一个包。渠道服很多（国服 / 好友快爆 / 4399 / 国际服…），
/// 不能只认一份写死的包名清单，所以按名字特征扫出来让玩家自己选当前那个。
/// </summary>
public sealed class MobilePackageCandidate
{
    /// <summary>包名 / bundle id。</summary>
    public string AppId { get; set; } = "";

    /// <summary>显示名（iOS 取 CFBundleDisplayName / CFBundleName，安卓取 label）。</summary>
    public string Name { get; set; } = "";

    /// <summary>版本号（能取到才有）。</summary>
    public string Version { get; set; } = "";

    /// <summary>容器里已经有 Addressables 缓存（说明进过游戏、热更下完了）。</summary>
    public bool HasCache { get; set; }

    /// <summary>container 是否能打开（iOS 需要 house_arrest 成功；安卓看目录是否可读）。</summary>
    public bool ContainerAccessible { get; set; }

    /// <summary>命中原因，例如 “包名含 astralparty”，让玩家看得懂为什么列出来。</summary>
    public string MatchedBy { get; set; } = "";
}
