namespace AstralParty.Toys.Models;

/// <summary>GitHub 上 CesiumLoader 的最新 Release（轻量检查用，不下载整个发布包）。</summary>
public sealed class LoaderLatestReleaseInfo
{
    /// <summary>版本号（不带 <c>modloader-</c> 前缀，例如 <c>3.3.2</c>）。</summary>
    public string Version { get; set; } = "";

    /// <summary>GitHub tag 名（例如 <c>modloader-3.3.2</c>）。</summary>
    public string Tag { get; set; } = "";

    /// <summary>Release 发布时间（ISO 8601，取不到时为空）。</summary>
    public string PublishedAt { get; set; } = "";

    /// <summary>Release 页面地址（https://github.com/higashitaniyume/CesiumLoader/releases/latest）。</summary>
    public string HtmlUrl { get; set; } = "";
}
