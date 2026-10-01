using System.Net;
using System.Net.Http;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 「按回放 ID 从官方 CDN 下载到回放库」：地址拼法、ID 校验、落库、重复、坏响应、取消。
///
/// 除了一条按需开启的真机用例外，全部走本地假 HTTP 管道（<see cref="StubHandler"/>），
/// 所以默认离线可跑；回放内容由 <see cref="ReplayFixture"/> 用游戏自己的 protobuf 生成类合成。
/// </summary>
public sealed class ReplayDownloadTests
{
    /// <summary>
    /// 测试用的合成回放 ID（16 位十进制，与真实 ID 同形）。
    /// 刻意**不**写任何真实回放 ID：ID 一旦进仓库就等于把某一局对局公开钉在这里。
    /// 真机用例的 ID 由 <see cref="RealDownload"/> 从环境变量取，不落到代码里。
    /// </summary>
    private const string SampleReplayId = "1900000000000001";

    [Fact]
    public void CdnBaseUrls_MatchTheGameConstants()
    {
        // 四个 base 与反编译出来的 ReplayLogic 常量逐字一致；编号也要与 ChangeReplayCDNUrl 的入参一致
        Assert.Equal("https://sereplayjp.feimogames.com/dev/", ReplayCdn.BaseUrl(ReplayCdnEndpoint.JpDev));
        Assert.Equal("https://sereplayjp.feimogames.com/prod/", ReplayCdn.BaseUrl(ReplayCdnEndpoint.JpProd));
        Assert.Equal("https://sereplaycn.feimogames.com/dev/", ReplayCdn.BaseUrl(ReplayCdnEndpoint.CnDev));
        Assert.Equal("https://sereplaycn.feimogames.com/prod/", ReplayCdn.BaseUrl(ReplayCdnEndpoint.CnProd));
        Assert.Equal(0, (int)ReplayCdnEndpoint.JpDev);
        Assert.Equal(1, (int)ReplayCdnEndpoint.JpProd);
        Assert.Equal(2, (int)ReplayCdnEndpoint.CnDev);
        Assert.Equal(3, (int)ReplayCdnEndpoint.CnProd);
    }

    [Fact]
    public void DownloadUrl_IsBaseUrlPlusReplayId()
    {
        // 游戏侧就是 _cdnBaseUrl + replayId：没有扩展名、没有查询串
        Assert.Equal("https://sereplaycn.feimogames.com/prod/" + SampleReplayId,
            ReplayCdn.Url(ReplayCdnEndpoint.CnProd, SampleReplayId));
    }

    [Theory]
    [InlineData("global", ReplayCdnEndpoint.JpProd)]
    [InlineData("Global", ReplayCdnEndpoint.JpProd)]
    [InlineData("cn", ReplayCdnEndpoint.CnProd)]
    [InlineData("taptap", ReplayCdnEndpoint.CnProd)]
    [InlineData("custom", ReplayCdnEndpoint.CnProd)]
    [InlineData(null, ReplayCdnEndpoint.CnProd)]
    public void DefaultEndpoint_FollowsActiveGameEdition(string? edition, ReplayCdnEndpoint expected)
        => Assert.Equal(expected, ReplayCdn.FromEdition(edition));

    [Theory]
    [InlineData("CnProd", ReplayCdnEndpoint.CnProd)]
    [InlineData("cnprod", ReplayCdnEndpoint.CnProd)]
    [InlineData("3", ReplayCdnEndpoint.CnProd)]
    [InlineData("JpDev", ReplayCdnEndpoint.JpDev)]
    [InlineData("0", ReplayCdnEndpoint.JpDev)]
    [InlineData("nonsense", null)]
    [InlineData("9", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void CdnEndpoint_ParsesNameOrIndex(string? text, ReplayCdnEndpoint? expected)
        => Assert.Equal(expected, ReplayCdn.TryParse(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData(SampleReplayId + "abc")]
    [InlineData("../" + SampleReplayId)]
    [InlineData(SampleReplayId + "/../../x")]
    [InlineData("..\\" + SampleReplayId)]
    [InlineData(SampleReplayId + "?x=1")]
    [InlineData(SampleReplayId + "#x")]
    [InlineData("+1900000000000001")]
    [InlineData("-1900000000000001")]
    [InlineData("1900000000000001.0")]
    [InlineData("１２３４")]                                        // 全角数字：不做宽松转换
    [InlineData("190000000000000100000000000000000")]              // 33 位，超过上限
    public void InvalidReplayIds_AreRejected(string? replayId)
    {
        Assert.False(ReplayCdn.IsValidReplayId(replayId, out var error));
        Assert.NotEmpty(error);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1234")]
    [InlineData(SampleReplayId)]
    [InlineData(" 1900000000000001 ")]                             // 前后空白会被去掉
    [InlineData("19000000000000010000000000000000")]               // 恰好 32 位
    public void ValidReplayIds_AreAccepted(string replayId)
    {
        Assert.True(ReplayCdn.IsValidReplayId(replayId, out var error), error);
        Assert.Empty(error);
    }

    [Fact]
    public async Task DownloadAsync_StoresHealthyReplayInLibrary()
    {
        using var harness = new DownloadHarness();
        var bytes = ReplayFixture.Create(new ReplayFixtureOptions { ReplayId = SampleReplayId });
        var handler = new StubHandler(bytes);
        harness.Service.DownloadHandlerOverride = handler;
        var progress = new List<ReplayDownloadProgress>();

        var result = await harness.Service.DownloadAsync(
            SampleReplayId, ReplayCdnEndpoint.CnProd, new ListProgress(progress.Add));

        Assert.True(result.Ok, string.Join(" | ", result.Messages));
        Assert.Equal(1, result.Applied);
        Assert.Equal(0, result.Failed);

        var file = Path.Combine(harness.LibraryDirectory, SampleReplayId, SampleReplayId);
        Assert.True(File.Exists(file), "下载后库里没有 <id>\\<id> 这份文件");
        Assert.Equal(bytes, await File.ReadAllBytesAsync(file));
        Assert.True(File.Exists(Path.Combine(harness.Service.IndexDirectory, SampleReplayId + ".json")),
            "下载后没有写元数据 sidecar");
        Assert.Empty(Directory.GetFiles(harness.LibraryDirectory, "*.part", SearchOption.AllDirectories));

        var entry = Assert.Single(harness.Service.Snapshot().Entries, item => item.ReplayId == SampleReplayId);
        Assert.True(entry.InLibrary && !entry.InGame, "下载的回放不该出现在游戏目录里");
        Assert.True(entry.Meta is { Healthy: true }, entry.Meta?.Error);

        Assert.Equal([ReplayCdn.Url(ReplayCdnEndpoint.CnProd, SampleReplayId)], handler.Requests);
        Assert.NotEmpty(progress);
        Assert.Equal(bytes.Length, progress[^1].ReceivedBytes);
        Assert.Equal((long)bytes.Length, progress[^1].TotalBytes ?? -1);
        Assert.Equal(100, progress[^1].Percent ?? -1);
        Assert.Contains("download", await File.ReadAllTextAsync(harness.Service.OperationsLogPath));
    }

    [Fact]
    public async Task DownloadAsync_SameContentAsLibraryCopy_IsSkipped()
    {
        using var harness = new DownloadHarness();
        var bytes = ReplayFixture.Create(new ReplayFixtureOptions { ReplayId = SampleReplayId });
        harness.SeedLibraryReplay(SampleReplayId, bytes);
        harness.Service.DownloadHandlerOverride = new StubHandler(bytes);

        var result = await harness.Service.DownloadAsync(SampleReplayId, ReplayCdnEndpoint.CnProd);

        Assert.True(result.Ok, string.Join(" | ", result.Messages));
        Assert.Equal(0, result.Applied);
        Assert.Equal(1, result.Skipped);
        Assert.Contains("已经有同一份回放", string.Join(" | ", result.Messages));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(
            Path.Combine(harness.LibraryDirectory, SampleReplayId, SampleReplayId)));
    }

    [Fact]
    public async Task DownloadAsync_DifferentContentThanLibraryCopy_IsRefused()
    {
        using var harness = new DownloadHarness();
        var existing = ReplayFixture.Create(new ReplayFixtureOptions { ReplayId = SampleReplayId, RoundCount = 5 });
        harness.SeedLibraryReplay(SampleReplayId, existing);
        var downloaded = ReplayFixture.Create(new ReplayFixtureOptions { ReplayId = SampleReplayId, RoundCount = 2 });
        harness.Service.DownloadHandlerOverride = new StubHandler(downloaded);

        var result = await harness.Service.DownloadAsync(SampleReplayId, ReplayCdnEndpoint.CnProd);

        Assert.False(result.Ok);
        Assert.Equal(0, result.Applied);
        Assert.Contains("内容不同", string.Join(" | ", result.Messages));
        // 库里那份必须一个字节都没变
        Assert.Equal(existing, await File.ReadAllBytesAsync(
            Path.Combine(harness.LibraryDirectory, SampleReplayId, SampleReplayId)));
        Assert.Empty(Directory.GetFiles(harness.LibraryDirectory, "*.part", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task DownloadAsync_Http404_ReportsCdnMissAndLeavesNothing()
    {
        using var harness = new DownloadHarness();
        harness.Service.DownloadHandlerOverride = new StubHandler(null, HttpStatusCode.NotFound);

        var result = await harness.Service.DownloadAsync(SampleReplayId, ReplayCdnEndpoint.CnProd);

        Assert.False(result.Ok);
        Assert.Equal(1, result.Failed);
        var message = Assert.Single(result.Messages);
        Assert.Contains("404", message);
        Assert.Contains("国服（正式服）", message);
        Assert.False(Directory.Exists(Path.Combine(harness.LibraryDirectory, SampleReplayId)),
            "下载失败后不该在库里留下空目录");
        Assert.Empty(Directory.GetFiles(harness.LibraryDirectory, "*.part", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task DownloadAsync_NonReplayBody_IsRejected()
    {
        using var harness = new DownloadHarness();
        // CDN 偶尔会拿错误页当 200 返回：内容不是帧流就不能入库
        harness.Service.DownloadHandlerOverride = new StubHandler("<html>not a replay</html>"u8.ToArray());

        var result = await harness.Service.DownloadAsync(SampleReplayId, ReplayCdnEndpoint.CnProd);

        Assert.False(result.Ok);
        Assert.Contains("帧流", Assert.Single(result.Messages));
        Assert.False(Directory.Exists(Path.Combine(harness.LibraryDirectory, SampleReplayId)));
    }

    [Fact]
    public async Task DownloadAsync_EmptyBody_IsRejected()
    {
        using var harness = new DownloadHarness();
        harness.Service.DownloadHandlerOverride = new StubHandler([]);

        var result = await harness.Service.DownloadAsync(SampleReplayId, ReplayCdnEndpoint.CnProd);

        Assert.False(result.Ok);
        Assert.Contains("空文件", Assert.Single(result.Messages));
        Assert.False(Directory.Exists(Path.Combine(harness.LibraryDirectory, SampleReplayId)));
    }

    [Fact]
    public async Task DownloadAsync_UnparseableButWellFormed_IsKeptWithAWarning()
    {
        using var harness = new DownloadHarness();
        // 有合法帧结构、但没有 1016 结算帧：工具读不出来，文件仍收下（回放库不是游戏目录，游戏不会扫它）
        var bytes = ReplayFixture.FrameStream([(1003, Array.Empty<byte>())]);
        harness.Service.DownloadHandlerOverride = new StubHandler(bytes);

        var result = await harness.Service.DownloadAsync(SampleReplayId, ReplayCdnEndpoint.CnProd);

        Assert.True(result.Ok, string.Join(" | ", result.Messages));
        Assert.Equal(1, result.Applied);
        Assert.Contains(result.Messages, message => message.Contains("解析不出它的结算帧"));
        Assert.True(File.Exists(Path.Combine(harness.LibraryDirectory, SampleReplayId, SampleReplayId)));

        var entry = Assert.Single(harness.Service.Snapshot().Entries, item => item.ReplayId == SampleReplayId);
        Assert.True(entry.Meta is { Healthy: false }, "读不出来的回放必须被标成不可解析");
    }

    [Fact]
    public async Task DownloadAsync_MismatchedInnerReplayId_WarnsButKeepsRequestedId()
    {
        using var harness = new DownloadHarness();
        // 文件里记录的 ID 与请求的 ID 不同：仍按请求的 ID 入库，但要如实提醒
        var bytes = ReplayFixture.Create(new ReplayFixtureOptions { ReplayId = "1800000000000009" });
        harness.Service.DownloadHandlerOverride = new StubHandler(bytes);

        var result = await harness.Service.DownloadAsync(SampleReplayId, ReplayCdnEndpoint.CnProd);

        Assert.True(result.Ok, string.Join(" | ", result.Messages));
        Assert.Contains(result.Messages, message => message.Contains("1800000000000009"));
        Assert.True(File.Exists(Path.Combine(harness.LibraryDirectory, SampleReplayId, SampleReplayId)));
    }

    [Fact]
    public async Task DownloadAsync_Canceled_LeavesNothing()
    {
        using var harness = new DownloadHarness();
        harness.Service.DownloadHandlerOverride = new StubHandler([1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await harness.Service.DownloadAsync(
            SampleReplayId, ReplayCdnEndpoint.CnProd, null, cancellation.Token);

        Assert.False(result.Ok);
        Assert.True(result.Canceled);
        Assert.Contains("已取消", Assert.Single(result.Messages));
        Assert.False(Directory.Exists(Path.Combine(harness.LibraryDirectory, SampleReplayId)));
    }

    [Fact]
    public async Task DownloadAsync_BadId_IsRejectedBeforeAnyRequest()
    {
        using var harness = new DownloadHarness();
        var handler = new StubHandler([1, 2, 3]);
        harness.Service.DownloadHandlerOverride = handler;

        var result = await harness.Service.DownloadAsync("../evil", ReplayCdnEndpoint.CnProd);

        Assert.False(result.Ok);
        Assert.Empty(handler.Requests);
        Assert.False(Directory.Exists(Path.Combine(harness.LibraryDirectory, "..", "evil")));
    }

    [Fact]
    public async Task DownloadAsync_LibraryRootEqualToGameRoot_IsRefused()
    {
        using var harness = new DownloadHarness();
        harness.Service.SaveSettings(new ReplayLibrarySettings
        {
            LibraryRoot = harness.GameDirectory,
            KeepInGame = ReplayLibraryService.GameSlotCapacity
        });
        var handler = new StubHandler([1, 2, 3]);
        harness.Service.DownloadHandlerOverride = handler;

        var result = await harness.Service.DownloadAsync(SampleReplayId, ReplayCdnEndpoint.CnProd);

        Assert.False(result.Ok);
        Assert.Contains("不能是同一个路径", string.Join(" | ", result.Messages));
        Assert.Empty(handler.Requests);
    }

    /// <summary>
    /// 真机用例：真去官方 CDN 拉一局，确认真实回放能落库并被游戏自己的结算解析器读出来。
    /// 默认跳过，用法见 <see cref="RealDownload"/>。
    /// </summary>
    [RealDownloadFact]
    public async Task DownloadAsync_FromRealCdn_StoresHealthyReplay()
    {
        var replayId = RealDownload.ReplayId!;
        using var harness = new DownloadHarness();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(2));

        var result = await harness.Service.DownloadAsync(replayId, RealDownload.Endpoint, null, cancellation.Token);

        Assert.True(result.Ok, string.Join(" | ", result.Messages));
        Assert.Equal(1, result.Applied);
        var entry = Assert.Single(harness.Service.Snapshot().Entries, item => item.ReplayId == replayId);
        Assert.True(entry.Meta is { Healthy: true },
            $"真机下载的回放解析不出结算帧：{entry.Meta?.Error}（{string.Join(" | ", result.Messages)}）");
        Assert.Equal(replayId, entry.Meta!.FileReplayId);
    }

    // ============================== 夹具 ==============================

    /// <summary>沙箱化的下载环境：空游戏目录 + 独立库目录 + 独立配置目录。</summary>
    private sealed class DownloadHarness : IDisposable
    {
        public DownloadHarness()
        {
            Sandbox = new TestSandbox("ap-download");
            GameDirectory = Sandbox.EnsureDirectory("game");
            LibraryDirectory = Sandbox.EnsureDirectory("library");
            ProfileDirectory = Sandbox.EnsureDirectory("profile");
            Service = new ReplayLibraryService(TestPaths.AppDirectory, GameDirectory, ProfileDirectory);
            Service.SaveSettings(new ReplayLibrarySettings
            {
                LibraryRoot = LibraryDirectory,
                AutoMaintain = false,
                KeepInGame = ReplayLibraryService.GameSlotCapacity
            });
            Service.EnsureLibrary();
        }

        public TestSandbox Sandbox { get; }
        public string GameDirectory { get; }
        public string LibraryDirectory { get; }
        public string ProfileDirectory { get; }
        public ReplayLibraryService Service { get; }

        /// <summary>按游戏的目录约定预置一份库内回放（<c>&lt;库&gt;\&lt;id&gt;\&lt;id&gt;</c>）。</summary>
        public void SeedLibraryReplay(string replayId, byte[] bytes)
            => Sandbox.WriteFile(Path.Combine("library", replayId, replayId), bytes);

        public void Dispose() => Sandbox.Dispose();
    }

    /// <summary>本地假 HTTP 管道：把测试想要的字节当作 CDN 响应发回来。</summary>
    private sealed class StubHandler(byte[]? payload, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            cancellationToken.ThrowIfCancellationRequested();
            var response = new HttpResponseMessage(status);
            if (payload is not null) response.Content = new ByteArrayContent(payload);
            return Task.FromResult(response);
        }
    }

    private sealed class ListProgress(Action<ReplayDownloadProgress> callback) : IProgress<ReplayDownloadProgress>
    {
        public void Report(ReplayDownloadProgress value) => callback(value);
    }
}
