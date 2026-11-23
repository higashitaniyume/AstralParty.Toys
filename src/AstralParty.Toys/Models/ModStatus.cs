namespace AstralParty.Toys.Models;

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
    public bool LoaderDisabled { get; set; }
    public bool LoaderMissing { get; set; }
    public bool CanRestoreLoader { get; set; }

    /// <summary>加载器核心 <c>CesiumLoader.dll</c> 的完整路径（与 version.dll 同目录）。</summary>
    public string CorePath { get; set; } = "";

    /// <summary>盘上这个 version.dll 是不是"拆分后的薄代理"—— 它必须有同目录的核心才算完整。
    /// 旧版单 DLL 加载器为 false：那种安装没有核心文件也是能用的，不能误报。</summary>
    public bool LoaderRequiresCore { get; set; }

    /// <summary>核心文件是否在盘上。</summary>
    public bool CorePresent { get; set; }

    /// <summary>代理在、核心缺失或损坏：游戏能启动，但不会加载任何模组。
    /// 恢复入口据此打开（恢复会同时写回核心）。</summary>
    public bool CoreMissing { get; set; }

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

    /// <summary>当前游戏目录 doorstop_config.json / SDK DLL 标注的 SDK 版本；读不到就是空串。</summary>
    public string InstalledSdkVersion { get; set; } = "";

    /// <summary>随 Toys 内置的 SDK/doorstop_config sdkVersion；空 = 本地开发构建没内嵌发布包。</summary>
    public string EmbeddedSdkVersion { get; set; } = "";

    public string Message { get; set; } = "";
    public List<ModEntryInfo> Mods { get; set; } = new();
    public List<ModEntryInfo> Sdk { get; set; } = new();

    public string RunningInstallHint => ModManager.DescribeRunningInstall();
    public string RunningUninstallHint => ModManager.DescribeRunningUninstall();
}
