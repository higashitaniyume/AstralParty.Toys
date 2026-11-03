using System.Text.Json.Serialization;

namespace AstralParty.Toys.Services;

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

    public string TargetFileName { get; set; } = "";

    public string? DownloadedFilePath { get; set; }

    public int? Percent { get; set; }

    public long ReceivedBytes { get; set; }

    public long TotalBytes { get; set; }

    public string? Error { get; set; }
}
