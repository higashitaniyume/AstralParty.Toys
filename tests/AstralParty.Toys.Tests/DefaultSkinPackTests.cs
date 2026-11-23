using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 默认皮肤包（GitHub Release 的 <c>cesium-default-skins.zip</c>）：地址解析要按
/// 「version.json → tag 拼版本化资产 → 同 tag 稳定名 → latest 别名」的顺序，每条都先镜像后直连；
/// 下载完整后才做内容预检。这里只测纯逻辑，真正的联网下载在实机验证。
/// </summary>
public sealed class DefaultSkinPackTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "toys-skinpack-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { /* 临时目录删不掉不影响断言 */ }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<string> RequestedUrls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUrls.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(respond(request));
        }
    }

    private static string Manifest(string version = "3.8.10", string tag = "modloader-3.8.10")
        => JsonSerializer.Serialize(new { version, releaseTag = tag, releaseDate = "2026-11-21T05:23:39Z" });

    private static HttpResponseMessage Ok(byte[] content)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) };
        response.Content.Headers.ContentLength = content.Length;
        return response;
    }

    // ============================== 地址 ==============================

    [Fact]
    public void CandidateUrls_PrefersVersionedAssetThenStableThenLatestAlias()
    {
        var urls = DefaultSkinPack.CandidateUrls("modloader-3.8.10", "3.8.10");

        Assert.Equal(3, urls.Count);
        Assert.EndsWith("/releases/download/modloader-3.8.10/cesium-default-skins-3.8.10.zip", urls[0]);
        Assert.EndsWith("/releases/download/modloader-3.8.10/cesium-default-skins.zip", urls[1]);
        Assert.EndsWith("/releases/latest/download/cesium-default-skins.zip", urls[2]);
    }

    [Fact]
    public void CandidateUrls_WithoutTag_KeepsOnlyLatestAlias()
    {
        var urls = DefaultSkinPack.CandidateUrls(null, null);

        Assert.Single(urls);
        Assert.EndsWith("/releases/latest/download/cesium-default-skins.zip", urls[0]);
    }

    [Theory]
    [InlineData("gh-proxy", null, 2, "https://gh-proxy.com/")]
    [InlineData("ghfast", null, 2, "https://ghfast.top/")]
    [InlineData("custom", "https://mirror.example/", 2, "https://mirror.example/")]
    [InlineData("custom", "mirror.example", 2, "https://mirror.example/")]
    [InlineData("direct", null, 1, null)]
    [InlineData("", null, 1, null)]
    [InlineData(null, null, 1, null)]
    public void UrlAttempts_MirrorsFirstThenFallsBackToDirect(string? mirrorType, string? customUrl, int expected, string? prefix)
    {
        const string url = "https://github.com/higashitaniyume/CesiumLoader/releases/download/modloader-3.8.10/cesium-default-skins.zip";

        var attempts = DefaultSkinPack.UrlAttempts(url, mirrorType, customUrl);

        Assert.Equal(expected, attempts.Count);
        if (prefix is null)
        {
            // 明确直连（含空值）时不能再套 gh-proxy —— GetPrefix 对空值会回落到它
            Assert.Equal(url, attempts[0]);
        }
        else
        {
            Assert.Equal(prefix + url, attempts[0]);
            Assert.Equal(url, attempts[1]);
        }
    }

    // ============================== 版本解析 ==============================

    [Fact]
    public async Task PlanAsync_UsesMirrorThenDirect_AndParsesManifest()
    {
        var handler = new FakeHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url.StartsWith("https://gh-proxy.com/", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.BadGateway);   // 镜像挂了 → 直连兜底
            return url.EndsWith("version.json", StringComparison.Ordinal)
                ? Ok(Encoding.UTF8.GetBytes(Manifest()))
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var client = new HttpClient(handler);

        var plan = await DefaultSkinPack.PlanAsync("gh-proxy", null, _root, client);

        Assert.Equal("3.8.10", plan.LatestVersion);
        Assert.Equal("modloader-3.8.10", plan.LatestTag);
        Assert.Equal("", plan.Error);
        Assert.Null(plan.Local);
        Assert.Equal(2, handler.RequestedUrls.Count);
        Assert.StartsWith("https://gh-proxy.com/", handler.RequestedUrls[0]);
        Assert.EndsWith("cesium-default-skins-3.8.10.zip", plan.Candidates[0]);
    }

    [Fact]
    public async Task PlanAsync_WithoutManifest_FallsBackToLatestAlias_AndKeepsTheReason()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(handler);

        var plan = await DefaultSkinPack.PlanAsync("direct", null, _root, client);

        Assert.Equal("", plan.LatestVersion);
        Assert.Single(plan.Candidates);
        Assert.EndsWith("/releases/latest/download/cesium-default-skins.zip", plan.Candidates[0]);
        Assert.Contains("404", plan.Error);
    }

    [Fact]
    public async Task PlanAsync_ReadsTheLocalRecord()
    {
        DefaultSkinPack.TryWriteRecord(_root, new DefaultSkinPack.Record { Version = "3.8.5", Tag = "modloader-3.8.5" });
        var handler = new FakeHandler(_ => Ok(Encoding.UTF8.GetBytes(Manifest())));
        using var client = new HttpClient(handler);

        var plan = await DefaultSkinPack.PlanAsync("direct", null, _root, client);

        Assert.Equal("3.8.5", plan.Local?.Version);
        Assert.Equal("3.8.10", plan.LatestVersion);
    }

    // ============================== 下载 ==============================

    [Fact]
    public async Task DownloadAsync_FallsBackToTheStableAsset_WhenTheVersionedOneIsMissing()
    {
        var payload = Encoding.UTF8.GetBytes("fake-pack-bytes");
        var handler = new FakeHandler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            if (url.EndsWith("cesium-default-skins-3.8.10.zip", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            if (url.EndsWith(DefaultSkinPack.AssetName, StringComparison.Ordinal)) return Ok(payload);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        using var client = new HttpClient(handler);
        var plan = new DefaultSkinPack.Plan
        {
            LatestVersion = "3.8.10",
            LatestTag = "modloader-3.8.10",
            Candidates = DefaultSkinPack.CandidateUrls("modloader-3.8.10", "3.8.10")
        };
        var path = Path.Combine(_root, "pack.zip");
        Directory.CreateDirectory(_root);
        var percents = new List<int>();

        var result = await DefaultSkinPack.DownloadAsync(plan, path, "direct", null, percents.Add, client);

        Assert.Contains("/releases/download/modloader-3.8.10/", result.Url);
        Assert.Equal(payload.Length, result.Bytes);
        Assert.Equal(payload, await File.ReadAllBytesAsync(path));
        Assert.Equal(64, result.Sha256.Length);
        Assert.Equal(100, percents[^1]);
    }

    [Fact]
    public async Task DownloadAsync_WhenEveryCandidateFails_ReportsEachChannel()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = new HttpClient(handler);
        var plan = new DefaultSkinPack.Plan { Candidates = DefaultSkinPack.CandidateUrls(null, null) };
        var path = Path.Combine(_root, "pack.zip");
        Directory.CreateDirectory(_root);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => DefaultSkinPack.DownloadAsync(plan, path, "ghfast", null, null, client));

        Assert.Contains("HTTP 404", error.Message);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DownloadAsync_Cancelled_DoesNotLeaveAPartialFile()
    {
        var handler = new FakeHandler(_ => Ok(new byte[4096]));
        using var client = new HttpClient(handler);
        var plan = new DefaultSkinPack.Plan { Candidates = DefaultSkinPack.CandidateUrls(null, null) };
        var path = Path.Combine(_root, "cancelled.zip");
        Directory.CreateDirectory(_root);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => DefaultSkinPack.DownloadAsync(plan, path, "direct", null, null, client, cts.Token));

        Assert.False(File.Exists(path));
    }

    // ============================== 内容预检 ==============================

    private string WriteZip(string name, params (string Path, byte[] Bytes)[] entries)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, name);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryPath, bytes) in entries)
        {
            using var stream = archive.CreateEntry(entryPath).Open();
            stream.Write(bytes);
        }
        return path;
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] Metadata = Encoding.UTF8.GetBytes("{\"name\":\"原版卡面(默认方案)\"}");

    [Fact]
    public void ValidateZip_AcceptsTheRealReleaseLayout()
    {
        var path = WriteZip("good.zip",
            (DefaultSkinPack.PackPrefix + "skin.json", Metadata),
            (DefaultSkinPack.PackPrefix + "UT_Destiny_40001.png", Png),
            (DefaultSkinPack.PackPrefix + "frames/0.png", Png));

        DefaultSkinPack.ValidateZip(path);   // 不抛就算通过
    }

    [Fact]
    public void ValidateZip_RejectsEntriesOutsideTheDefaultFolder()
    {
        var path = WriteZip("outside.zip",
            (DefaultSkinPack.PackPrefix + "skin.json", Metadata),
            ("AstralParty_ModLoader/skins/other/card.png", Png));

        Assert.Throws<InvalidDataException>(() => DefaultSkinPack.ValidateZip(path));
    }

    [Fact]
    public void ValidateZip_RejectsAPackWithoutImages()
    {
        var path = WriteZip("no-image.zip", (DefaultSkinPack.PackPrefix + "skin.json", Metadata));

        Assert.Throws<InvalidDataException>(() => DefaultSkinPack.ValidateZip(path));
    }

    [Fact]
    public void ValidateZip_RejectsAPackWithoutMetadata()
    {
        var path = WriteZip("no-meta.zip", (DefaultSkinPack.PackPrefix + "UT_Destiny_40001.png", Png));

        Assert.Throws<InvalidDataException>(() => DefaultSkinPack.ValidateZip(path));
    }

    [Fact]
    public void ValidateZip_RejectsAnOversizedEntry()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "huge.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (var stream = archive.CreateEntry(DefaultSkinPack.PackPrefix + "skin.json").Open())
                stream.Write(Metadata);
            using (var stream = archive.CreateEntry(DefaultSkinPack.PackPrefix + "UT_Destiny_40001.png").Open())
            {
                var chunk = new byte[1 << 20];
                for (var i = 0; i < 33; i++) stream.Write(chunk);   // 33 MB > 32 MB 单条目上限
            }
        }

        Assert.Throws<InvalidDataException>(() => DefaultSkinPack.ValidateZip(path));
    }

    // ============================== 版本记录 ==============================

    [Fact]
    public void Record_RoundTrips_AndBadFilesAreIgnored()
    {
        Assert.Null(DefaultSkinPack.ReadRecord(_root));

        Assert.True(DefaultSkinPack.TryWriteRecord(_root, new DefaultSkinPack.Record
        {
            Version = "3.8.10",
            Tag = "modloader-3.8.10",
            Url = "https://github.com/higashitaniyume/CesiumLoader/releases/download/modloader-3.8.10/cesium-default-skins.zip",
            Sha256 = "abc",
            DownloadedAt = "2026-11-23T04:00:00Z"
        }));

        var record = DefaultSkinPack.ReadRecord(_root);
        Assert.NotNull(record);
        Assert.Equal("3.8.10", record!.Version);
        Assert.Equal("modloader-3.8.10", record.Tag);

        File.WriteAllText(DefaultSkinPack.RecordPath(_root), "{ not json");
        Assert.Null(DefaultSkinPack.ReadRecord(_root));

        // 没有版本的记录不算「已下载过」——不能拿它去比较新旧
        File.WriteAllText(DefaultSkinPack.RecordPath(_root), "{\"version\":\"\",\"tag\":\"modloader-3.8.10\"}");
        Assert.Null(DefaultSkinPack.ReadRecord(_root));
    }
}

/// <summary>卡牌页「从 GitHub 下载默认包」的接线：菜单项、宿主消息、翻译目录都得对得上。</summary>
public sealed class DefaultSkinPackWebUiTests
{
    private static string Read(string relativePath)
    {
        using var stream = EmbeddedWebStore.Open(relativePath);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }

    private static readonly string[] NewSourceStrings =
    [
        "从 GitHub 下载默认包…",
        "从 GitHub 下载最新默认包…",
        "从 GitHub 下载默认包",
        "正在确认 GitHub 上的最新默认包…",
        "正在下载默认包…",
        "本地默认包：",
        "本地还没有默认包，新建方案需要先下载它",
        "GitHub 最新：",
        "暂时查不到 GitHub 上的最新版本，将直接下载最新发布包",
        "下载会覆盖 default 里的同名图片，不会切换当前方案",
        "本地已经是最新版本，不需要重复下载",
        "仍然重新下载",
        "下载最新默认包",
    ];

    [Fact]
    public void CardPage_DownloadsTheDefaultPackFromGitHub_WithProgressAndCancel()
    {
        var skins = Read("js/v2/skins.js");

        // 入口在「＋新建方案」下拉里，与「新建方案（复制 default）」并列
        Assert.Contains("从 GitHub 下载默认包…", skins);
        Assert.Contains("skinsDefaultPackInfo", skins);
        // 版本弹窗、逐百分比进度、取消都要接上
        Assert.Contains("skinsDefaultPackPlan", skins);
        Assert.Contains("skinsDownloadState", skins);
        Assert.Contains("skinsCancelDefaultPack", skins);
        // 旧界面的按钮在共用分发器里也要能显示进度
        Assert.Contains("skinsDownloadState", Read("js/core/dispatcher.js"));
    }

    [Fact]
    public void NewStrings_AreTranslatedInEveryCatalog()
    {
        foreach (var file in new[] { "js/i18n/source-en.js", "js/i18n/source-ja.js", "js/i18n/source-zh-Hant.js" })
        {
            var catalog = Read(file);
            foreach (var source in NewSourceStrings)
                Assert.True(catalog.Contains($"\"{source}\":", StringComparison.Ordinal), $"{file} 缺少 {source}");
        }

        // 宿主 toast 带版本号，属于模板；zh-Hans 是源语言，不进 this 列表
        foreach (var file in new[] { "js/i18n/templates-en.js", "js/i18n/templates-ja.js", "js/i18n/templates-zh-Hant.js" })
        {
            var templates = Read(file);
            Assert.Contains("默认皮肤包 {0} 已下载并安装，可选择 default 方案使用。", templates);
            Assert.Contains("默认皮肤包已下载并安装，可选择 default 方案使用。", templates);
            Assert.Contains("已取消下载默认包。", templates);
            Assert.Contains("已下载 {0}", templates);
        }
    }
}
