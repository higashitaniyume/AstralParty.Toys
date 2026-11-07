using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services.Common;

/// <summary>
/// 内嵌前端页面（<c>WebUI\**</c>）的读取入口。资源名由 csproj 的 <c>LogicalName</c> 固定为
/// <c>AstralParty.Toys.WebUI/&lt;相对路径&gt;</c>，所以可以直接按相对路径查表，不需要猜扩展名或目录层级。
/// 页面由 <c>HybridWindow</c> 通过 WebView2 的 <c>WebResourceRequested</c> 从内存提供，
/// 发布目录因此不再需要 WebUI 文件夹（开发时的磁盘覆盖见 <c>HybridWindow</c>）。
/// 支持通过 include/import 指令将 HTML、CSS 与 JS 分割为小文件并在读取时透明组装。
/// </summary>
public static class EmbeddedWebStore
{
    /// <summary>磁盘覆盖目录名（exe 同目录下的同名文件夹优先于内嵌资源）。</summary>
    public const string DirectoryName = "WebUI";

    private const string ResourcePrefix = "AstralParty.Toys.WebUI/";
    private static readonly Assembly Assembly = typeof(EmbeddedWebStore).Assembly;
    private static readonly Dictionary<string, string> Resources = BuildIndex();
    private static readonly ConcurrentDictionary<string, string> AssembledCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex HtmlIncludeRegex = new(
        """<!--\s*@include\s+["']?([^"'>\s]+)["']?\s*-->""",
        RegexOptions.Compiled);

    private static readonly Regex CssImportRegex = new(
        """@import\s+(?:url\(\s*["']?([^"')]+)["']?\s*\)|["']([^"']+)["'])\s*;?""",
        RegexOptions.Compiled);

    private static readonly Regex JsIncludeRegex = new(
        """(?://\s*@include\s+["']([^"']+)["']|/\*\s*@include\s+["']([^"']+)["']\s*\*/)""",
        RegexOptions.Compiled);

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
        if (!Resources.TryGetValue(key, out var resourceName))
            return null;

        var ext = Path.GetExtension(key).ToLowerInvariant();
        if (ext is not (".html" or ".htm" or ".css" or ".js" or ".mjs"))
        {
            return Assembly.GetManifestResourceStream(resourceName);
        }

        var assembled = GetOrAssembleText(key);
        return new MemoryStream(Encoding.UTF8.GetBytes(assembled));
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

    private static string GetOrAssembleText(string key)
    {
        return AssembledCache.GetOrAdd(key, k =>
        {
            var stack = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return AssembleFile(k, stack);
        });
    }

    private static string AssembleFile(string key, HashSet<string> stack)
    {
        if (!stack.Add(key)) return "";
        try
        {
            if (!Resources.TryGetValue(key, out var resourceName)) return "";

            string raw;
            using (var stream = Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null) return "";
                using var reader = new StreamReader(stream, Encoding.UTF8);
                raw = reader.ReadToEnd();
            }

            var ext = Path.GetExtension(key).ToLowerInvariant();
            if (ext is ".html" or ".htm")
            {
                if (raw.Contains("@include"))
                {
                    return HtmlIncludeRegex.Replace(raw, match =>
                    {
                        var target = match.Groups[1].Value.Trim();
                        var resolved = ResolveRelative(key, target);
                        return AssembleFile(resolved, stack);
                    });
                }
            }
            else if (ext == ".css")
            {
                if (raw.Contains("@import"))
                {
                    return CssImportRegex.Replace(raw, match =>
                    {
                        var target = match.Groups[1].Success && match.Groups[1].Length > 0
                            ? match.Groups[1].Value.Trim()
                            : match.Groups[2].Value.Trim();
                        if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            target.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                            target.StartsWith("//", StringComparison.OrdinalIgnoreCase) ||
                            target.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            return match.Value;
                        }
                        var resolved = ResolveRelative(key, target);
                        if (!Resources.ContainsKey(resolved)) return match.Value;
                        return AssembleFile(resolved, stack);
                    });
                }
            }
            else if (ext is ".js" or ".mjs")
            {
                if (raw.Contains("@include"))
                {
                    return JsIncludeRegex.Replace(raw, match =>
                    {
                        var target = match.Groups[1].Success && match.Groups[1].Length > 0
                            ? match.Groups[1].Value.Trim()
                            : match.Groups[2].Value.Trim();
                        var resolved = ResolveRelative(key, target);
                        return AssembleFile(resolved, stack);
                    });
                }
            }

            return raw;
        }
        finally
        {
            stack.Remove(key);
        }
    }

    private static string ResolveRelative(string currentFile, string target)
    {
        var dir = Path.GetDirectoryName(currentFile)?.Replace('\\', '/') ?? "";
        var combined = string.IsNullOrEmpty(dir) ? target : $"{dir}/{target}";
        var segments = new List<string>();
        foreach (var segment in combined.Replace('\\', '/').Split('/'))
        {
            if (segment is "" or ".") continue;
            if (segment == "..")
            {
                if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
                continue;
            }
            segments.Add(segment);
        }
        return string.Join('/', segments);
    }

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
