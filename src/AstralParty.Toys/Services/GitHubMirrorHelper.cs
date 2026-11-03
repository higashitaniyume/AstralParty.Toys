namespace AstralParty.Toys.Services;

/// <summary>
/// GitHub 加速代理助手。
/// 支持 gh-proxy.com、ghfast 等前缀加速，解决国内网络下载 Release 慢或超时的痛点。
/// </summary>
public static class GitHubMirrorHelper
{
    public const string TypeDirect = "direct";
    public const string TypeGhProxy = "gh-proxy";
    public const string TypeGhFast = "ghfast";
    public const string TypeCustom = "custom";

    public const string DefaultMirrorType = TypeGhProxy;
    public const string GhProxyPrefix = "https://gh-proxy.com/";
    public const string GhFastPrefix = "https://ghfast.top/";

    /// <summary>
    /// 标准化镜像前缀，确保以 https:// 开头并以 / 结尾。
    /// </summary>
    public static string NormalizePrefix(string? prefix)
    {
        var trimmed = (prefix ?? "").Trim();
        if (string.IsNullOrEmpty(trimmed)) return "";

        if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = "https://" + trimmed;
        }

        if (!trimmed.EndsWith('/'))
        {
            trimmed += "/";
        }

        return trimmed;
    }

    /// <summary>
    /// 根据配置的镜像类型和自定义地址获取前缀字符串。
    /// </summary>
    public static string GetPrefix(string? mirrorType, string? customUrl = null)
    {
        var normalized = (mirrorType ?? "").Trim().ToLowerInvariant();
        return normalized switch
        {
            TypeDirect or "none" => "",
            TypeGhFast => GhFastPrefix,
            TypeCustom => NormalizePrefix(customUrl),
            TypeGhProxy => GhProxyPrefix,
            _ => GhProxyPrefix // 默认推荐 gh-proxy
        };
    }

    /// <summary>
    /// 将原始 GitHub 链接转换为带代理前缀的加速链接。
    /// </summary>
    public static string ApplyMirror(string originalUrl, string? mirrorType, string? customUrl = null)
    {
        if (string.IsNullOrWhiteSpace(originalUrl)) return originalUrl;

        var prefix = GetPrefix(mirrorType, customUrl);
        if (string.IsNullOrEmpty(prefix)) return originalUrl;

        // 若链接已经包含该前缀，避免重复拼接
        if (originalUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return originalUrl;

        // 只对 GitHub 相关域名做镜像代理拼接
        if (!IsGitHubUrl(originalUrl))
            return originalUrl;

        return $"{prefix}{originalUrl}";
    }

    /// <summary>
    /// 判断是否为可代理的 GitHub 相关链接。
    /// </summary>
    public static bool IsGitHubUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://raw.githubusercontent.com/", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://objects.githubusercontent.com/", StringComparison.OrdinalIgnoreCase);
    }
}
