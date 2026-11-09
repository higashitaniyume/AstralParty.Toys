using System.Text.Json.Serialization;

namespace AstralParty.Toys.Models;

/// <summary>
/// CesiumLoader 静态 version.json（与 Toys 自身的 AppVersionManifest 格式对齐）。
/// 随 GitHub Release 分发，Toys 优先于 GitHub API 拉取它，不下载整个发布包。
/// </summary>
public sealed class LoaderVersionManifest
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
    public LoaderVersionPackages? Packages { get; set; }
}

public sealed class LoaderVersionPackages
{
    [JsonPropertyName("loaderZip")]
    public LoaderVersionAsset? LoaderZip { get; set; }
}

public sealed class LoaderVersionAsset
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
