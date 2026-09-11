using System.Reflection;

namespace AstralParty.Toys.Services;

/// <summary>
/// 内嵌前端页面（<c>WebUI\**</c>）的读取入口。资源名由 csproj 的 <c>LogicalName</c> 固定为
/// <c>AstralParty.Toys.WebUI/&lt;相对路径&gt;</c>，所以可以直接按相对路径查表，不需要猜扩展名或目录层级。
/// 页面由 <c>HybridWindow</c> 通过 WebView2 的 <c>WebResourceRequested</c> 从内存提供，
/// 发布目录因此不再需要 WebUI 文件夹（开发时的磁盘覆盖见 <c>HybridWindow</c>）。
/// </summary>
public static class EmbeddedWebStore
{
    /// <summary>磁盘覆盖目录名（exe 同目录下的同名文件夹优先于内嵌资源）。</summary>
    public const string DirectoryName = "WebUI";

    private const string ResourcePrefix = "AstralParty.Toys.WebUI/";
    private static readonly Assembly Assembly = typeof(EmbeddedWebStore).Assembly;
    private static readonly Dictionary<string, string> Resources = BuildIndex();

    /// <summary>内嵌的页面文件数量。</summary>
    public static int Count => Resources.Count;

    /// <summary>内嵌资源里是否有 index.html（没有就只能退回经典窗口）。</summary>
    public static bool HasIndex => Resources.ContainsKey("index.html");

    /// <summary>全部内嵌页面的相对路径（正斜杠分隔），供测试核对。</summary>
    public static IReadOnlyCollection<string> Paths => Resources.Keys;

    /// <summary>按相对路径取内嵌页面；找不到返回 null（调用方据此回 404）。</summary>
    public static Stream? Open(string relativePath)
    {
        var key = Normalize(Uri.UnescapeDataString(relativePath));
        return Resources.TryGetValue(key, out var resourceName)
            ? Assembly.GetManifestResourceStream(resourceName)
            : null;
    }

    public static string ContentType(string relativePath) => Path.GetExtension(relativePath).ToLowerInvariant() switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" or ".mjs" => "application/javascript; charset=utf-8",
        ".json" or ".map" => "application/json; charset=utf-8",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        ".woff2" => "font/woff2",
        _ => "application/octet-stream"
    };

    private static Dictionary<string, string> BuildIndex()
    {
        var resources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resourceName in Assembly.GetManifestResourceNames())
        {
            if (!resourceName.StartsWith(ResourcePrefix, StringComparison.Ordinal)) continue;
            var relativePath = Normalize(resourceName[ResourcePrefix.Length..]);
            if (relativePath.Length > 0) resources[relativePath] = resourceName;
        }

        return resources;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
