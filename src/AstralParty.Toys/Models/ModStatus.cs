namespace AstralParty.Toys.Services;

/// <summary>Mod 加载器与已安装 Mod 状态模型。</summary>
public sealed class ModStatus
{
    public bool BundleLoaderPresent { get; set; }
    public bool BundleConfigPresent { get; set; }
    public bool BundleSdkPresent { get; set; }
    public bool BundleSampleModPresent { get; set; }
    public string GameDirectory { get; set; } = "";
    public string AutoDetectedDirectory { get; set; } = "";
    public bool GameExeFound { get; set; }
    public bool GameRunning { get; set; }
    public bool Installed { get; set; }
    public bool LoaderPresent { get; set; }

    /// <summary>磁盘上的 version.dll 是否与随程序集内置的那份逐字节一致。</summary>
    public bool LoaderMatchesBundle { get; set; }

    /// <summary>来源："bundle"（内置）/ "installed"（安装清单记录的、通常是更新过的）/ ""（来源不明）。</summary>
    public string LoaderOrigin { get; set; } = "";

    /// <summary>磁盘上的 version.dll 是否确实是本工具装的（bundle 或 installed）。
    /// 界面据此区分「已安装（正常）」与「不是本工具装的（冲突）」——二者都不该被含糊成"文件不一致"。</summary>
    public bool LoaderManaged { get; set; }

    public string LoaderPath { get; set; } = "";
    public string LoaderRoot { get; set; } = "";
    public bool LoaderRootExists { get; set; }
    public string BundleHash { get; set; } = "";
    public string EmbeddedVersion { get; set; } = "";

    /// <summary>已安装的加载器版本 —— 只来自安装清单，读不到就是空串（界面显示「版本未知」）。</summary>
    public string InstalledVersion { get; set; } = "";

    public string Message { get; set; } = "";
    public List<ModEntryInfo> Mods { get; set; } = new();
    public List<ModEntryInfo> Sdk { get; set; } = new();

    public string RunningInstallHint => ModManager.DescribeRunningInstall();
    public string RunningUninstallHint => ModManager.DescribeRunningUninstall();
}
