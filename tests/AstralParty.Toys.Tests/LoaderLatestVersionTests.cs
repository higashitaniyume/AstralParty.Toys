using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// GitHub 最新版轻量检查：只取 Release tag（不下载整个发布包），失败时才下载发布包读清单兜底。
/// </summary>
public sealed class LoaderLatestVersionTests
{
    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> RequestedUrls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(respond(request));
        }
    }

    private static string ReleaseJson(string tag, string publishedAt = "2026-11-06T08:40:27Z", string htmlUrl = "https://github.com/higashitaniyume/CesiumLoader/releases/tag/modloader-3.3.2")
        => JsonSerializer.Serialize(new { tag_name = tag, published_at = publishedAt, html_url = htmlUrl });

    private static byte[] PackageWithVersion(string version)
    {
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new { version, files = new Dictionary<string, string>() });
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            using var stream = archive.CreateEntry("cesium-loader.json").Open();
            stream.Write(manifest);
        }
        return output.ToArray();
    }

    [Theory]
    [InlineData("modloader-3.3.2", "3.3.2")]
    [InlineData("modloader-2.5.1", "2.5.1")]
    [InlineData("ModLoader-1.2.0", "1.2.0")]
    [InlineData("v1.2.0", "1.2.0")]
    [InlineData("modloader-v1.2.0", "1.2.0")]
    [InlineData("", "")]
    public void ParseReleaseVersion_StripsPrefix(string tag, string expected)
    {
        Assert.Equal(expected, ModManager.ParseReleaseVersion(tag));
    }

    [Fact]
    public void ParseReleaseVersion_Null_ReturnsEmpty()
    {
        Assert.Equal("", ModManager.ParseReleaseVersion(null));
    }

    [Fact]
    public void ParseLatestReleaseJson_ExtractsVersionTagUrlAndPublishedAt()
    {
        var info = ModManager.ParseLatestReleaseJson(ReleaseJson("modloader-3.3.2"));
        Assert.NotNull(info);
        Assert.Equal("3.3.2", info!.Version);
        Assert.Equal("modloader-3.3.2", info.Tag);
        Assert.Equal("2026-11-06T08:40:27Z", info.PublishedAt);
        Assert.Equal("https://github.com/higashitaniyume/CesiumLoader/releases/tag/modloader-3.3.2", info.HtmlUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ 这不是 JSON !!!")]
    [InlineData("{}")]
    [InlineData("{\"tag_name\":\"\"}")]
    public void ParseLatestReleaseJson_Invalid_ReturnsNull(string json)
    {
        Assert.Null(ModManager.ParseLatestReleaseJson(json));
    }

    [Fact]
    public void GitHubMirrorHelper_AppliesMirrorToApiUrl()
    {
        var raw = ModManager.LatestReleaseApiUrl;
        Assert.Equal("https://api.github.com/repos/higashitaniyume/CesiumLoader/releases/latest", raw);
        Assert.Equal("https://gh-proxy.com/https://api.github.com/repos/higashitaniyume/CesiumLoader/releases/latest",
            GitHubMirrorHelper.ApplyMirror(raw, "gh-proxy"));
        Assert.Equal(raw, GitHubMirrorHelper.ApplyMirror(raw, "direct"));
    }

    [Fact]
    public void ParseLoaderVersionManifestJson_ExtractsVersionTagAndDate()
    {
        var json = """{"version":"3.5.2","releaseTag":"modloader-3.5.2","releaseDate":"2026-11-09T00:00:00Z","packages":{"loaderZip":{"fileName":"cesium-loader-3.5.2.zip","url":"https://github.com/higashitaniyume/CesiumLoader/releases/download/modloader-3.5.2/cesium-loader-3.5.2.zip","sha256":"abc","size":123}}}""";
        var info = ModManager.ParseLoaderVersionManifestJson(json);
        Assert.NotNull(info);
        Assert.Equal("3.5.2", info!.Version);
        Assert.Equal("modloader-3.5.2", info.Tag);
        Assert.Equal("2026-11-09T00:00:00Z", info.PublishedAt);
        Assert.Equal(ModManager.LatestReleaseUrl, info.HtmlUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ 这不是 JSON !!!")]
    [InlineData("{}")]
    [InlineData("{\"version\":\"\"}")]
    public void ParseLoaderVersionManifestJson_Invalid_ReturnsNull(string json)
    {
        Assert.Null(ModManager.ParseLoaderVersionManifestJson(json));
    }

    [Fact]
    public async Task GetLatestVersionAsync_UsesVersionManifestFirst()
    {
        using var sandbox = new TestSandbox("loader-latest-manifest");
        var manager = new ModManager(sandbox.Root, sandbox.EnsureDirectory("profile"));
        var handler = new FakeHandler(req =>
        {
            var url = req.RequestUri!.AbsoluteUri;
            if (url.Contains("version.json", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"version":"3.5.2","releaseTag":"modloader-3.5.2","releaseDate":"2026-11-09T00:00:00Z"}""", Encoding.UTF8, "application/json")
                };
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
        using var http = new HttpClient(handler);

        var info = await manager.GetLatestVersionAsync("direct", null, http);

        Assert.Equal("3.5.2", info.Version);
        Assert.Equal("modloader-3.5.2", info.Tag);
        Assert.Single(handler.RequestedUrls);
        Assert.Contains("releases/latest/download/version.json", handler.RequestedUrls[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestVersionAsync_UsesApiAndDoesNotDownloadPackage()
    {
        using var sandbox = new TestSandbox("loader-latest-api");
        var manager = new ModManager(sandbox.Root, sandbox.EnsureDirectory("profile"));
        var handler = new FakeHandler(req =>
        {
            var url = req.RequestUri!.AbsoluteUri;
            if (url.Contains("version.json", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent("not found", Encoding.UTF8, "text/plain")
                };
            if (url.Contains("api.github.com", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(ReleaseJson("modloader-3.3.2"), Encoding.UTF8, "application/json")
                };
            // 不允许走发布包：走到了就让测试红
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });
        using var http = new HttpClient(handler);

        var info = await manager.GetLatestVersionAsync("direct", null, http);

        Assert.Equal("3.3.2", info.Version);
        Assert.Equal("modloader-3.3.2", info.Tag);
        Assert.Equal(2, handler.RequestedUrls.Count);
        Assert.Contains("version.json", handler.RequestedUrls[0], StringComparison.Ordinal);
        Assert.Contains("api.github.com/repos/higashitaniyume/CesiumLoader/releases/latest", handler.RequestedUrls[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLatestVersionAsync_PrefersMirrorWhenSelected()
    {
        using var sandbox = new TestSandbox("loader-latest-mirror");
        var manager = new ModManager(sandbox.Root, sandbox.EnsureDirectory("profile"));
        var handler = new FakeHandler(req =>
        {
            var url = req.RequestUri!.AbsoluteUri;
            if (url.Contains("version.json", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"version":"3.3.2","releaseTag":"modloader-3.3.2","releaseDate":"2026-11-06T08:40:27Z"}""", Encoding.UTF8, "application/json")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ReleaseJson("modloader-3.3.2"), Encoding.UTF8, "application/json")
            };
        });
        using var http = new HttpClient(handler);

        var info = await manager.GetLatestVersionAsync("gh-proxy", null, http);

        Assert.Equal("3.3.2", info.Version);
        Assert.Equal("https://gh-proxy.com/https://github.com/higashitaniyume/CesiumLoader/releases/latest/download/version.json", handler.RequestedUrls[0]);
    }

    [Fact]
    public async Task GetLatestVersionAsync_FallsBackToPackageManifestWhenApiFails()
    {
        using var sandbox = new TestSandbox("loader-latest-fallback");
        var manager = new ModManager(sandbox.Root, sandbox.EnsureDirectory("profile"));
        var handler = new FakeHandler(req =>
        {
            var url = req.RequestUri!.AbsoluteUri;
            if (url.Contains("api.github.com", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("{\"message\":\"API rate limit exceeded\"}", Encoding.UTF8, "application/json")
                };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(PackageWithVersion("9.2.0"))
            };
        });
        using var http = new HttpClient(handler);

        var info = await manager.GetLatestVersionAsync("direct", null, http);

        Assert.Equal("9.2.0", info.Version);
        Assert.Equal("modloader-9.2.0", info.Tag);
        Assert.Equal(ModManager.LatestReleaseUrl, info.HtmlUrl);
        // API 失败后应走发布包（cesium-loader.zip）
        Assert.Contains(handler.RequestedUrls, u => u.Contains("cesium-loader.zip", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetLatestVersionAsync_ThrowsWhenEverythingFails()
    {
        using var sandbox = new TestSandbox("loader-latest-fails");
        var manager = new ModManager(sandbox.Root, sandbox.EnsureDirectory("profile"));
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var http = new HttpClient(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.GetLatestVersionAsync("direct", null, http));
        Assert.Contains("无法确认 GitHub 最新版本", ex.Message);
        Assert.Contains("GitHub API", ex.Message);
    }
}
