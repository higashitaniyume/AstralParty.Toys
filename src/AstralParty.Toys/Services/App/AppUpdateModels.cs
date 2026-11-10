using System.Text.Json.Serialization;

namespace AstralParty.Toys.Services.App;

/// <summary>
/// 随 GitHub Release 分发的静态 version.json 根结构。
/// 不依赖 GitHub API，直接通过静态 Release Asset 获取。
/// </summary>
public sealed class AppVersionManifest
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = "";

    [JsonPropertyName("releaseTag")]
    public string ReleaseTag { get; set; } = "";

    [JsonPropertyName("releaseDate")]
    public string ReleaseDate { get; set; } = "";

    [JsonPropertyName("releaseNotes")]
    public string ReleaseNotes { get; set; } = "";

    [JsonPropertyName("packages")]
    public AppPackageAssets? Packages { get; set; }
}

/// <summary>
/// 安装包资产映射表。
/// </summary>
public sealed class AppPackageAssets
{
    [JsonPropertyName("frameworkDependentInstaller")]
    public AppAssetPackageInfo? FrameworkDependentInstaller { get; set; }

    [JsonPropertyName("selfContainedInstaller")]
    public AppAssetPackageInfo? SelfContainedInstaller { get; set; }

    // 单文件版和便携版原地替换用。旧版程序不认识这些字段，读 version.json 时会直接忽略。
    [JsonPropertyName("frameworkDependentSingleFile")]
    public AppAssetPackageInfo? FrameworkDependentSingleFile { get; set; }

    [JsonPropertyName("selfContainedSingleFile")]
    public AppAssetPackageInfo? SelfContainedSingleFile { get; set; }

    [JsonPropertyName("frameworkDependentPortable")]
    public AppAssetPackageInfo? FrameworkDependentPortable { get; set; }

    [JsonPropertyName("selfContainedPortable")]
    public AppAssetPackageInfo? SelfContainedPortable { get; set; }
}

/// <summary>
/// 这次更新用哪个包、怎么装。Mode 是 Manual 时 Package 为空，ManualReason 写原因。
/// </summary>
public sealed record AppUpdatePlan(AppAssetPackageInfo? Package, AppUpdateMode Mode, string ManualReason)
{
    public static AppUpdatePlan Manual(string reason) => new(null, AppUpdateMode.Manual, reason);
}

/// <summary>
/// 这次更新怎么装。
/// </summary>
public enum AppUpdateMode
{
    /// <summary>没有能用的方式（调试版、缺包、没有写入权限）：界面让用户选「改用安装版」或自己下载。</summary>
    Manual,
    /// <summary>运行安装程序（安装版，或用户选了「改用安装版」）。</summary>
    Installer,
    /// <summary>单文件版：退出后替换 exe。</summary>
    ReplaceExe,
    /// <summary>便携版：退出后把新文件覆盖进程序文件夹。</summary>
    ReplaceFolder
}

/// <summary>
/// 单个安装包的详细下载与安全校验信息。
/// </summary>
public sealed class AppAssetPackageInfo
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("sha256")]
    public string Sha256 { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

/// <summary>
/// 自动更新流程阶段状态。
/// </summary>
public enum AppUpdateStatus
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    ReadyToInstall,
    Installing,
    Error
}

/// <summary>
/// 推送给界面的更新状态与进度载荷。
/// </summary>
public sealed class AppUpdateState
{
    public string Status { get; set; } = AppUpdateStatus.Idle.ToString().ToLowerInvariant();

    public string CurrentVersion { get; set; } = "";

    public string LatestVersion { get; set; } = "";

    public string ReleaseNotes { get; set; } = "";

    public string ReleaseDate { get; set; } = "";

    public string RuntimeKind { get; set; } = "";

    public bool IsInstaller { get; set; }

    /// <summary>installer / single-file / portable / development</summary>
    public string PackageForm { get; set; } = "";

    /// <summary>manual / installer / replace-exe / replace-folder</summary>
    public string UpdateMode { get; set; } = "";

    /// <summary>为什么不能原地更新（UpdateMode 是 manual 时）。</summary>
    public string ManualReason { get; set; } = "";

    /// <summary>用户选了「改用安装版」。</summary>
    public bool SwitchToInstaller { get; set; }

    /// <summary>这份程序已经被安装版取代，不再提示更新。</summary>
    public bool Superseded { get; set; }

    /// <summary>换了更新方式，之前下载的包作废了，需要重新下载。</summary>
    public bool NeedsRedownload { get; set; }

    /// <summary>当前程序所在位置（单文件版是 exe，便携版是文件夹），给界面说明「会替换哪里」。</summary>
    public string TargetLocation { get; set; } = "";

    /// <summary>新版本的发布页，手动下载用。</summary>
    public string ReleaseUrl { get; set; } = "";

    public string TargetFileName { get; set; } = "";

    public string? DownloadedFilePath { get; set; }

    public int? Percent { get; set; }

    public long ReceivedBytes { get; set; }

    public long TotalBytes { get; set; }

    public string? Error { get; set; }
}
