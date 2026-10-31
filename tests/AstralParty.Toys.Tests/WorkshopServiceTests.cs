using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 创意工坊客户端：工坊地址规范化、版本比较、地址拼接、目录/详情解析、封面缓存与包下载。
/// 全部走本地假 HTTP 管道，默认离线可跑；不碰用户真实的封面缓存目录。
/// </summary>
public sealed class WorkshopServiceTests
{
    private const string Default = WorkshopService.DefaultBaseUrl;
    private const string Forge = "http://forge.test";

    // ============================== 使用同意 ==============================

    [Fact]
    public void Consent_IsGrantedOnlyForTheCurrentNoticeVersion()
    {
        Assert.False(WorkshopConsent.IsGranted(new ReplayLibrarySettings()));
        Assert.False(WorkshopConsent.IsGranted(new ReplayLibrarySettings
        {
            WorkshopConsentVersion = WorkshopConsent.CurrentVersion - 1
        }));
        Assert.True(WorkshopConsent.IsGranted(new ReplayLibrarySettings
        {
            WorkshopConsentVersion = WorkshopConsent.CurrentVersion
        }));
        Assert.True(WorkshopConsent.IsGranted(new ReplayLibrarySettings
        {
            WorkshopConsentVersion = WorkshopConsent.CurrentVersion + 1
        }));
    }

    [Fact]
    public void Consent_IsPersistedWithTheOtherSettings()
    {
        using var sandbox = new TestSandbox("ap-workshop-consent");
        var game = sandbox.EnsureDirectory("game");
        var profile = sandbox.EnsureDirectory("profile");

        var service = new ReplayLibraryService(TestPaths.AppDirectory, game, profile);
        var settings = service.Settings;
        settings.WorkshopConsentVersion = WorkshopConsent.CurrentVersion;
        settings.WorkshopConsentAt = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        service.SaveSettings(settings);

        // 新实例重新读取配置文件：同意版本与时间都在。
        var reloaded = new ReplayLibraryService(TestPaths.AppDirectory, game, profile);
        Assert.Equal(WorkshopConsent.CurrentVersion, reloaded.Settings.WorkshopConsentVersion);
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero), reloaded.Settings.WorkshopConsentAt);
        Assert.True(WorkshopConsent.IsGranted(reloaded.Settings));

        // 保存其它设置（不含同意字段）也必须保留同意记录。
        var other = reloaded.Settings;
        reloaded.SaveSettings(new ReplayLibrarySettings
        {
            LibraryRoot = other.LibraryRoot,
            AutoMaintain = other.AutoMaintain,
            KeepInGame = other.KeepInGame,
            Language = other.Language,
            WorkshopUrl = other.WorkshopUrl
        });
        Assert.True(WorkshopConsent.IsGranted(reloaded.Settings));
        Assert.Equal(WorkshopConsent.CurrentVersion, reloaded.Settings.WorkshopConsentVersion);
    }

    [Fact]
    public async Task WorkshopRequests_CarryTheToysUserAgent()
    {
        using var harness = new Harness();
        harness.Handler.Payload = Encoding.UTF8.GetBytes("""{"total":0,"items":[]}""");

        await harness.Service.GetListAsync(Forge, "zh-Hans", null, null, "updated", 1, 24);
        await harness.Service.DownloadPackageAsync(
            "https://example.test/mod.zip", harness.Sandbox.Combine("ua.zip"), null, CancellationToken.None);

        Assert.Equal(2, harness.Handler.UserAgents.Count);
        Assert.All(harness.Handler.UserAgents, agent =>
        {
            Assert.StartsWith("AstralParty Toys/", agent);
            Assert.Matches(@"^AstralParty Toys/\d+\.\d+", agent);
        });
        Assert.Equal(WorkshopService.UserAgent, harness.Handler.UserAgents[0]);
    }

    // ============================== 地址规范化 ==============================

    [Theory]
    [InlineData("http://localhost:15555", Default)]
    [InlineData("http://localhost:15555/", Default)]
    [InlineData(" https://mods.example.com/workshop/ ", "https://mods.example.com/workshop")]
    [InlineData("HTTPS://Mods.Example.com", "HTTPS://Mods.Example.com")]
    [InlineData("", Default)]
    [InlineData(null, Default)]
    [InlineData("localhost:15555", Default)]      // 没有协议头：Uri 会把它当成 scheme=localhost
    [InlineData("ftp://example.com", Default)]
    [InlineData("file:///C:/mods", Default)]
    [InlineData("javascript:alert(1)", Default)]
    [InlineData("   ", Default)]
    public void NormalizeBaseUrl_FallsBackToDefaultForUnusableInput(string? value, string expected)
        => Assert.Equal(expected, WorkshopService.NormalizeBaseUrl(value));

    // ============================== 版本比较 ==============================

    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("1.2", "1.10", -1)]          // 按数字比，不是按字符串比
    [InlineData("2.0.0", "10.0.0", -1)]
    [InlineData("1.2", "1.2.0", 0)]
    [InlineData("1.2.3.4", "1.2.3", 0)]      // 只比前三段
    [InlineData("1..2", "1.0.2", 0)]         // 空段按 0
    [InlineData("abc", "1.0.0", -1)]
    [InlineData("", "0.0.0", 0)]
    [InlineData(null, "0.0.0", 0)]
    [InlineData(" 1.0 ", "1.0.0", 0)]
    public void CompareVersions_ComparesNumericSegments(string? left, string? right, int expected)
        => Assert.Equal(expected, WorkshopService.CompareVersions(left, right));

    // ============================== 地址拼接 ==============================

    [Theory]
    [InlineData(Forge, "/api/mods/x/download", Forge + "/api/mods/x/download")]
    [InlineData(Forge, "api/mods/x/download", Forge + "/api/mods/x/download")]
    [InlineData(Forge + "/", "api/mods/x/download", Forge + "/api/mods/x/download")]
    [InlineData(Forge, "https://github.com/a/b/releases/download/v1/mod.zip",
        "https://github.com/a/b/releases/download/v1/mod.zip")]
    [InlineData(Forge, "HTTP://Example.com/x.zip", "HTTP://Example.com/x.zip")]
    [InlineData(Forge, "", "")]
    [InlineData(Forge, "   ", "")]
    public void ResolveUrl_LeavesAbsoluteLinksAndJoinsRelativeOnes(string baseUrl, string url, string expected)
        => Assert.Equal(expected, WorkshopService.ResolveUrl(baseUrl, url));

    [Fact]
    public void ResolveUrl_RepairsInvalidBaseUrl()
        => Assert.Equal(Default + "/api/mods/x/download",
            WorkshopService.ResolveUrl("not a url", "/api/mods/x/download"));

    // ============================== 进度 ==============================

    [Fact]
    public void DownloadProgress_PercentIsUnknownWithoutTotalAndClampedOtherwise()
    {
        Assert.Null(new WorkshopDownloadProgress(1024, null).Percent);
        Assert.Null(new WorkshopDownloadProgress(1024, 0).Percent);
        Assert.Equal(0, new WorkshopDownloadProgress(0, 2048).Percent);
        Assert.Equal(50, new WorkshopDownloadProgress(1024, 2048).Percent);
        Assert.Equal(100, new WorkshopDownloadProgress(4096, 2048).Percent);
    }

    // ============================== 目录与详情 ==============================

    [Fact]
    public async Task GetListAsync_SendsFilterAndPagingAndParsesItems()
    {
        using var harness = new Harness();
        harness.Handler.Payload = Encoding.UTF8.GetBytes("""
        {
          "apiVersion": 1,
          "generatedAt": "2026-10-31T00:00:00Z",
          "total": 1,
          "page": 2,
          "pageSize": 10,
          "tags": ["界面", "UI"],
          "items": [{
            "id": "HandViewerMod",
            "name": "手牌查看器",
            "summary": "看对手手牌",
            "author": "某人",
            "tags": ["界面"],
            "coverUrl": "/api/mods/HandViewerMod/cover?v=abc12345",
            "latestVersion": "1.2.3",
            "sdkVersion": "2.5.1",
            "sizeBytes": 2048,
            "sha256": "deadbeef",
            "downloadUrl": "/api/mods/HandViewerMod/download",
            "updatedAt": "2026-10-01T00:00:00Z",
            "downloads": 12,
            "featured": true
          }]
        }
        """);

        var list = await harness.Service.GetListAsync(
            Forge + "/", "zh-Hans", "手牌 查看", "界面", "downloads", 2, 10);

        var uri = Assert.Single(harness.Handler.Requests);
        Assert.StartsWith(Forge + "/api/mods?", uri);
        Assert.Contains("lang=zh-Hans", uri);
        Assert.Contains("sort=downloads", uri);
        Assert.Contains("page=2", uri);
        Assert.Contains("pageSize=10", uri);
        Assert.Contains("query=" + Uri.EscapeDataString("手牌 查看"), uri);
        Assert.Contains("tag=" + Uri.EscapeDataString("界面"), uri);

        Assert.Equal(1, list.Total);
        Assert.Equal(2, list.Page);
        Assert.Equal(["界面", "UI"], list.Tags);
        var item = Assert.Single(list.Items);
        Assert.Equal("HandViewerMod", item.Id);
        Assert.Equal("1.2.3", item.LatestVersion);
        Assert.Equal("/api/mods/HandViewerMod/download", item.DownloadUrl);
        Assert.True(item.Featured);
    }

    [Fact]
    public async Task GetListAsync_OmitsEmptyFilters()
    {
        using var harness = new Harness();
        harness.Handler.Payload = Encoding.UTF8.GetBytes("""{"total":0,"items":[]}""");

        await harness.Service.GetListAsync(Forge, "en", null, "  ", "updated", 1, 24);

        var uri = Assert.Single(harness.Handler.Requests);
        Assert.DoesNotContain("query=", uri);
        Assert.DoesNotContain("tag=", uri);
        Assert.Contains("page=1", uri);
        Assert.Contains("lang=en", uri);
    }

    [Fact]
    public async Task GetDetailAsync_ParsesVersionsAndKeepsRelativeUrls()
    {
        using var harness = new Harness();
        harness.Handler.Payload = Encoding.UTF8.GetBytes("""
        {
          "id": "HandViewerMod",
          "name": "手牌查看器",
          "summary": "看对手手牌",
          "description": "详细说明",
          "author": "某人",
          "tags": ["界面"],
          "coverUrl": "/api/mods/HandViewerMod/cover?v=abc12345",
          "featured": false,
          "latestVersion": "2.0.0",
          "downloads": 3,
          "versions": [{
            "version": "2.0.0",
            "downloadUrl": "/api/mods/HandViewerMod/download?v=2.0.0",
            "sha256": "abc123",
            "sizeBytes": 4096,
            "sdkVersion": "2.5.1",
            "permissions": 3,
            "dependencies": [{ "id": "LanguageUnlockMod", "minVersion": "1.0.0" }],
            "changelog": "修好了",
            "publishedAt": "2026-10-02T00:00:00Z"
          }]
        }
        """);

        var detail = await harness.Service.GetDetailAsync(Forge, "HandViewerMod", "ja");

        Assert.Equal(Forge + "/api/mods/HandViewerMod?lang=ja", Assert.Single(harness.Handler.Requests));
        Assert.Equal("2.0.0", detail.LatestVersion);
        var version = Assert.Single(detail.Versions);
        Assert.Equal(3, version.Permissions);
        Assert.Equal("/api/mods/HandViewerMod/download?v=2.0.0", version.DownloadUrl);
        Assert.Equal("LanguageUnlockMod", Assert.Single(version.Dependencies).Id);
    }

    [Fact]
    public async Task GetDetailAsync_ReportsMissingModAsReadableError()
    {
        using var harness = new Harness();
        harness.Handler.Status = HttpStatusCode.NotFound;

        var error = await Assert.ThrowsAsync<WorkshopException>(
            () => harness.Service.GetDetailAsync(Forge, "Nope", "zh-Hans"));

        Assert.Contains("没有这个模组", error.Message);
    }

    // ============================== 封面缓存 ==============================

    [Fact]
    public async Task CacheCoverAsync_StoresCoverUnderVersionedNameAndReusesIt()
    {
        using var harness = new Harness();
        harness.Handler.Payload = Harness.WebpBytes;

        var first = await harness.Service.CacheCoverAsync(
            Forge, "HandViewerMod", "/api/mods/HandViewerMod/cover?v=abc12345");

        Assert.Equal(WorkshopService.CoverOrigin + "HandViewerMod-abc12345.webp", first);
        var file = Path.Combine(harness.CoverDirectory, "HandViewerMod-abc12345.webp");
        Assert.Equal(Harness.WebpBytes, File.ReadAllBytes(file));
        Assert.Equal(Forge + "/api/mods/HandViewerMod/cover?v=abc12345",
            Assert.Single(harness.Handler.Requests));

        // 第二次：本地已有就直接用，不再发请求（列表每刷新一次都不应该重下封面）
        var second = await harness.Service.CacheCoverAsync(
            Forge, "HandViewerMod", "/api/mods/HandViewerMod/cover?v=abc12345");
        Assert.Equal(first, second);
        Assert.Single(harness.Handler.Requests);
    }

    [Fact]
    public async Task CacheCoverAsync_WithoutCoverReturnsNullAndSkipsRequest()
    {
        using var harness = new Harness();

        Assert.Null(await harness.Service.CacheCoverAsync(Forge, "HandViewerMod", null));
        Assert.Null(await harness.Service.CacheCoverAsync(Forge, "HandViewerMod", "  "));
        Assert.Empty(harness.Handler.Requests);
    }

    [Fact]
    public async Task CacheCoverAsync_KeepsAbsoluteCoverUrlAsIs()
    {
        using var harness = new Harness();
        harness.Handler.Payload = Harness.WebpBytes;

        var url = await harness.Service.CacheCoverAsync(
            Forge, "Mod", "https://cdn.example.com/cover.webp?v=xyz");

        Assert.StartsWith(WorkshopService.CoverOrigin, url);
        Assert.Equal("https://cdn.example.com/cover.webp?v=xyz", Assert.Single(harness.Handler.Requests));
    }

    [Fact]
    public async Task CacheCoverAsync_HidesFailuresBehindPlaceholder()
    {
        using var harness = new Harness();

        harness.Handler.Status = HttpStatusCode.NotFound;
        Assert.Null(await harness.Service.CacheCoverAsync(Forge, "Mod", "/api/mods/Mod/cover?v=a"));

        harness.Handler.Status = HttpStatusCode.OK;
        harness.Handler.Payload = [];
        Assert.Null(await harness.Service.CacheCoverAsync(Forge, "Mod", "/api/mods/Mod/cover?v=b"));

        // 反向代理的错误页（HTML）也是 200：不是 WebP 就不缓存。
        harness.Handler.Payload = "<html>Bad gateway</html>"u8.ToArray();
        Assert.Null(await harness.Service.CacheCoverAsync(Forge, "Mod", "/api/mods/Mod/cover?v=d"));

        Assert.Empty(Directory.GetFiles(harness.CoverDirectory));
    }

    [Fact]
    public async Task CacheCoverAsync_RejectsOversizedContent()
    {
        using var harness = new Harness();
        harness.Handler.Payload = Harness.WebpBytes;
        harness.Handler.ContentLength = WorkshopService.MaxCoverBytes + 1L;

        Assert.Null(await harness.Service.CacheCoverAsync(Forge, "Mod", "/api/mods/Mod/cover?v=c"));
        Assert.Empty(Directory.GetFiles(harness.CoverDirectory));
    }

    [Fact]
    public async Task CacheCoverAsync_SanitizesModIdIntoFileName()
    {
        using var harness = new Harness();
        harness.Handler.Payload = Harness.WebpBytes;

        var url = await harness.Service.CacheCoverAsync(Forge, "../evil/id", "/api/mods/x/cover?v=ok");

        Assert.NotNull(url);
        var fileName = url![WorkshopService.CoverOrigin.Length..];
        Assert.DoesNotContain('/', fileName);
        Assert.DoesNotContain('\\', fileName);
        Assert.EndsWith("-ok.webp", fileName);
        Assert.Single(Directory.GetFiles(harness.CoverDirectory));
    }

    // ============================== 包下载 ==============================

    [Fact]
    public async Task DownloadPackageAsync_WritesPackageAndReportsHashAndProgress()
    {
        using var harness = new Harness();
        var payload = new byte[300 * 1024];
        Random.Shared.NextBytes(payload);
        harness.Handler.Payload = payload;
        var target = harness.Sandbox.Combine("package.zip");

        var reports = new List<WorkshopDownloadProgress>();
        var (sha, size) = await harness.Service.DownloadPackageAsync(
            "https://github.com/a/b/releases/download/v1/mod.zip", target,
            new InlineProgress<WorkshopDownloadProgress>(reports.Add), CancellationToken.None);

        Assert.Equal(payload.Length, size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), sha);
        Assert.Equal(payload, await File.ReadAllBytesAsync(target));
        Assert.NotEmpty(reports);
        Assert.Equal(payload.Length, reports[^1].ReceivedBytes);
        Assert.Equal(100, reports[^1].Percent);
    }

    [Fact]
    public async Task DownloadPackageAsync_ReportsMissingVersionAsReadableError()
    {
        using var harness = new Harness();
        harness.Handler.Status = HttpStatusCode.NotFound;
        var target = harness.Sandbox.Combine("package.zip");

        var error = await Assert.ThrowsAsync<WorkshopException>(() => harness.Service.DownloadPackageAsync(
            "https://example.test/mod.zip", target, null, CancellationToken.None));

        Assert.Contains("404", error.Message);
    }

    [Fact]
    public async Task DownloadPackageAsync_RejectsTooLargeContentLengthBeforeReading()
    {
        using var harness = new Harness();
        harness.Handler.Payload = [1, 2, 3];
        harness.Handler.ContentLength = WorkshopService.MaxPackageBytes + 1L;
        var target = harness.Sandbox.Combine("package.zip");

        var error = await Assert.ThrowsAsync<WorkshopException>(() => harness.Service.DownloadPackageAsync(
            "https://example.test/mod.zip", target, null, CancellationToken.None));

        Assert.Contains("上限", error.Message);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public async Task DownloadPackageAsync_StopsWhenCancelled()
    {
        using var harness = new Harness();
        harness.Handler.Payload = [1, 2, 3];
        var target = harness.Sandbox.Combine("package.zip");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Service.DownloadPackageAsync(
            "https://example.test/mod.zip", target, null, cts.Token));
        Assert.False(File.Exists(target));
    }

    // ============================== 夹具 ==============================

    /// <summary>沙箱化的工坊客户端：假 HTTP 管道 + 独立封面目录。</summary>
    private sealed class Harness : IDisposable
    {
        /// <summary>最小合法 WebP 头（RIFF....WEBP），封面缓存只接受这种内容。</summary>
        public static readonly byte[] WebpBytes =
            [0x52, 0x49, 0x46, 0x46, 0x1A, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20];

        public Harness()
        {
            Sandbox = new TestSandbox("ap-workshop");
            CoverDirectory = Sandbox.EnsureDirectory("covers");
            Service = new WorkshopService
            {
                ApiHandlerOverride = Handler,
                DownloadHandlerOverride = Handler,
                CoverDirectoryOverride = CoverDirectory
            };
        }

        public TestSandbox Sandbox { get; }
        public string CoverDirectory { get; }
        public WorkshopService Service { get; }
        public StubHandler Handler { get; } = new();

        public void Dispose() => Sandbox.Dispose();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public List<string> UserAgents { get; } = [];
        public byte[] Payload { get; set; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public long? ContentLength { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            UserAgents.Add(request.Headers.TryGetValues("User-Agent", out var agents)
                ? string.Join(" ", agents)
                : "");
            cancellationToken.ThrowIfCancellationRequested();

            var response = new HttpResponseMessage(Status);
            if (Status == HttpStatusCode.OK)
            {
                response.Content = new ByteArrayContent(Payload);
                if (ContentLength is { } length) response.Content.Headers.ContentLength = length;
            }

            return Task.FromResult(response);
        }
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
