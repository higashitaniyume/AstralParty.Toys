using System.Text.RegularExpressions;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;
using Xunit;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 前端页面（<c>src/AstralParty.Toys/WebUI</c>）现在整包内嵌进 exe，由 WebView2 的
/// <c>WebResourceRequested</c> 从内存提供，发布目录不再带 WebUI 文件夹。
/// 这里守住两件事：源码里的每个文件都真的进了程序集；页面里写到的本地引用都能取到。
/// </summary>
public sealed class EmbeddedWebUiTests
{
    private static readonly Regex AttributeReference = new(
        """(?:src|href)\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CssUrlReference = new(
        """url\(\s*["']?([^"')]+)["']?\s*\)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JsPathReference = new(
        """["']((?:images|css|js|data)/[^"']+)["']""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void EveryWebUiFile_IsEmbedded()
    {
        var files = Directory.GetFiles(TestPaths.WebUiSourceDirectory, "*", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        Assert.Equal(files.Length, EmbeddedWebStore.Count);

        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(TestPaths.WebUiSourceDirectory, file).Replace('\\', '/');
            Assert.True(EmbeddedWebStore.Paths.Contains(relativePath), $"没有内嵌：{relativePath}");
            using var stream = EmbeddedWebStore.Open(relativePath);
            Assert.NotNull(stream);
            Assert.True(stream!.Length > 0, $"内嵌内容是空的：{relativePath}");
        }
    }

    [Fact]
    public void Index_IsEmbeddedAndLooksLikeAPage()
    {
        Assert.True(EmbeddedWebStore.HasIndex);
        using var stream = EmbeddedWebStore.Open("index.html");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var html = reader.ReadToEnd();

        Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<title", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stylesheet", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<script", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("index.html", "text/html")]
    [InlineData("css/base.css", "text/css")]
    [InlineData("js/core.js", "application/javascript")]
    [InlineData("data/game-version-announcement.json", "application/json")]
    [InlineData("images/astral-party-logo.png", "image/png")]
    public void ContentTypes_MatchExtension(string relativePath, string expected)
    {
        Assert.StartsWith(expected, EmbeddedWebStore.ContentType(relativePath), StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownPath_IsNotFound()
    {
        Assert.Null(EmbeddedWebStore.Open("does-not-exist.css"));
        Assert.Null(EmbeddedWebStore.Open("../appsettings.json"));
    }

    [Fact]
    public void PageReferences_AllResolveToEmbeddedFiles()
    {
        var references = new List<(string From, string Target)>();

        foreach (var relativePath in EmbeddedWebStore.Paths)
        {
            using var stream = EmbeddedWebStore.Open(relativePath);
            Assert.NotNull(stream);
            using var reader = new StreamReader(stream!);
            var content = reader.ReadToEnd();

            var pattern = Path.GetExtension(relativePath).ToLowerInvariant() switch
            {
                ".html" => AttributeReference,
                ".css" => CssUrlReference,
                ".js" => JsPathReference,
                _ => null
            };
            if (pattern is null) continue;

            // JS 里的 fetch('data/...') 是相对**文档**解析的，不是相对脚本文件；页面在根目录，所以对 index.html 解析。
            var basePath = Path.GetExtension(relativePath).Equals(".js", StringComparison.OrdinalIgnoreCase)
                ? "index.html"
                : relativePath;

            references.AddRange(pattern.Matches(content)
                .Select(match => match.Groups[1].Value)
                .Where(IsLocalReference)
                .Select(target => (basePath, target)));
        }

        Assert.NotEmpty(references);
        foreach (var (from, target) in references)
        {
            var resolved = Resolve(from, target);
            Assert.True(EmbeddedWebStore.Open(resolved) is not null, $"{from} 引用了取不到的 {target}（解析为 {resolved}）");
        }
    }

    private static bool IsLocalReference(string reference) =>
        !reference.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
        !reference.StartsWith("data:", StringComparison.OrdinalIgnoreCase) &&
        !reference.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) &&
        !reference.StartsWith('#');

    /// <summary>把引用按「相对于引用者所在目录」解析成内嵌路径。</summary>
    private static string Resolve(string from, string target)
    {
        var path = target.Split('?')[0].Split('#')[0];
        var directory = Path.GetDirectoryName(from)?.Replace('\\', '/') ?? "";
        var combined = directory.Length == 0 ? path : $"{directory}/{path}";
        var segments = new List<string>();
        foreach (var segment in combined.Split('/'))
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
}
