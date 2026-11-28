using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AstralParty.Toys.Services;
using AstralParty.Toys.Services.Common;
using AstralParty.Toys.Services.ResourceMods;
using AstralParty.Toys.Services.Skins;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public sealed class WorkshopResourceTests
{
    private const string Forge = "http://forge.test";

    // ============================== 多线程 & 断点续传 ==============================

    [Fact]
    public async Task DownloadEngine_MultiThreadRange_DownloadsAllChunksAndVerifiesSha256()
    {
        using var sandbox = new TestSandbox("ap-dl-mt");
        var payload = new byte[1800 * 1024]; // ~1.8 MB，触发多分片并发
        Random.Shared.NextBytes(payload);
        var expectedSha = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

        var handler = new RangeStubHandler(payload) { ArtificialDelayMs = 8 };
        var service = CreateService(sandbox, handler);
        var target = sandbox.Combine("out.zip");
        var reports = new List<WorkshopDownloadProgress>();

        var (sha, size) = await service.DownloadPackageAsync(
            $"{Forge}/api/resources/pack/download?version=1.0.0",
            target,
            new InlineProgress<WorkshopDownloadProgress>(reports.Add),
            CancellationToken.None,
            WorkshopService.MaxResourcePackageBytes,
            maxThreads: 4,
            expectedSha256: expectedSha,
            resumeCacheDirectory: sandbox.EnsureDirectory("dl-cache"));

        Assert.Equal(payload.Length, size);
        Assert.Equal(expectedSha, sha);
        Assert.Equal(payload, await File.ReadAllBytesAsync(target));
        Assert.True(handler.RangeRequestCount >= 2, $"期望发送多个 Range 请求，实际只有 {handler.RangeRequestCount} 次");
        Assert.Contains(reports, r => r.Resumable);
        Assert.Contains(reports, r => r.ActiveThreads >= 2);
        Assert.Equal(100, reports[^1].Percent);
    }

    [Fact]
    public async Task DownloadEngine_ResumeAfterInterruption_ReusesPartAndFinishes()
    {
        using var sandbox = new TestSandbox("ap-dl-resume");
        var payload = new byte[1700 * 1024];
        Random.Shared.NextBytes(payload);
        var expectedSha = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();

        var cacheDir = sandbox.EnsureDirectory("dl-cache");
        var handler = new RangeStubHandler(payload) { FailOnOffsetOver = 1100 * 1024 };
        var service = CreateService(sandbox, handler);
        var target = sandbox.Combine("resumed.zip");

        await Assert.ThrowsAnyAsync<Exception>(() => service.DownloadPackageAsync(
            $"{Forge}/api/resources/big/download?version=1.0.0",
            target,
            null,
            CancellationToken.None,
            WorkshopService.MaxResourcePackageBytes,
            maxThreads: 4,
            expectedSha256: expectedSha,
            resumeCacheDirectory: cacheDir));

        Assert.False(File.Exists(target));
        Assert.NotEmpty(Directory.GetFiles(cacheDir, "*.part"));
        Assert.NotEmpty(Directory.GetFiles(cacheDir, "*.part.json"));

        // 恢复网络后再次下载：只请求未完成分片，不应从 0 重下全部字节
        handler.FailOnOffsetOver = null;
        handler.BytesServed = 0;
        var reports = new List<WorkshopDownloadProgress>();

        var (sha, size) = await service.DownloadPackageAsync(
            $"{Forge}/api/resources/big/download?version=1.0.0",
            target,
            new InlineProgress<WorkshopDownloadProgress>(reports.Add),
            CancellationToken.None,
            WorkshopService.MaxResourcePackageBytes,
            maxThreads: 4,
            expectedSha256: expectedSha,
            resumeCacheDirectory: cacheDir);

        Assert.Equal(expectedSha, sha);
        Assert.Equal(payload.Length, size);
        Assert.Equal(payload, await File.ReadAllBytesAsync(target));
        Assert.Contains(reports, r => r.Resumed);
        Assert.True(handler.BytesServed < payload.Length,
            $"续传应跳过已完成分片，但实际传输了 {handler.BytesServed} / {payload.Length} 字节");
        Assert.Empty(Directory.GetFiles(cacheDir, "*.part.json"));
    }

    [Fact]
    public async Task DownloadEngine_ServerWithoutRange_FallsBackToSingleStreamAndEnforcesMax()
    {
        using var sandbox = new TestSandbox("ap-dl-fallback");
        var payload = new byte[128 * 1024];
        Random.Shared.NextBytes(payload);
        var handler = new RangeStubHandler(payload) { SupportRange = false };
        var service = CreateService(sandbox, handler);

        var target = sandbox.Combine("single.zip");
        var reports = new List<WorkshopDownloadProgress>();
        var (sha, size) = await service.DownloadPackageAsync(
            "https://cdn.test/single.zip",
            target,
            new InlineProgress<WorkshopDownloadProgress>(reports.Add),
            CancellationToken.None,
            WorkshopService.MaxResourcePackageBytes,
            maxThreads: 8);

        Assert.Equal(payload.Length, size);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), sha);
        Assert.All(reports, r => Assert.False(r.Resumable));

        var ex = await Assert.ThrowsAsync<WorkshopException>(() => service.DownloadPackageAsync(
            "https://cdn.test/single.zip",
            sandbox.Combine("too-big.zip"),
            null,
            CancellationToken.None,
            maxPackageBytes: 1024));
        Assert.Contains("上限", ex.Message);
    }

    // ============================== 资源市场 API ==============================

    [Fact]
    public async Task WorkshopService_ResourceApis_ParseTypesListDetailAndPreviews()
    {
        using var sandbox = new TestSandbox("ap-res-api");
        var webp = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x1A, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50, 0x38, 0x20 };
        var routes = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["/api/resource-types"] = """[{"type":"card_skin","displayName":"卡牌皮肤","enabled":true,"maxPackageBytes":536870912}]"""u8.ToArray(),
            ["/api/resources"] = """
            {
              "apiVersion": 1,
              "total": 1,
              "page": 1,
              "pageSize": 24,
              "tags": ["卡面"],
              "types": [{"type":"card_skin","displayName":"卡牌皮肤","enabled":true}],
              "items": [{
                "id": "deepseek_chan",
                "type": "card_skin",
                "typeName": "卡牌皮肤",
                "name": "DeepSeek 小鲸娘",
                "summary": "全套卡面替换",
                "author": "hyumerin",
                "tags": ["卡面"],
                "contentRating": "general",
                "coverUrl": "/api/resources/deepseek_chan/cover?v=12345678",
                "latestVersion": "1.0.0",
                "sizeBytes": 123456,
                "fileCount": 12,
                "hosted": true,
                "sha256": "abcd1234",
                "downloads": 42
              }]
            }
            """u8.ToArray(),
            ["/api/resources/deepseek_chan"] = """
            {
              "id": "deepseek_chan",
              "type": "card_skin",
              "typeName": "卡牌皮肤",
              "name": "DeepSeek 小鲸娘",
              "author": "hyumerin",
              "contentRating": "general",
              "latestVersion": "1.0.0",
              "previews": [{"sha256":"aabbccdd1122","url":"/api/resources/deepseek_chan/previews/aabbccdd1122","width":640,"height":360,"caption":"手牌预览"}],
              "versions": [{
                "version": "1.0.0",
                "downloadUrl": "/api/resources/deepseek_chan/download",
                "hosted": true,
                "sha256": "abcd1234",
                "sizeBytes": 123456,
                "fileCount": 12,
                "modDependencies": [{"id":"CardSkinMod","minVersion":"1.0.0"}]
              }]
            }
            """u8.ToArray(),
            ["/api/resources/deepseek_chan/cover"] = webp,
            ["/api/resources/deepseek_chan/previews/aabbccdd1122"] = webp
        };

        var handler = new JsonRouteHandler(routes);
        var service = CreateService(sandbox, handler);

        var types = await service.GetResourceTypesAsync(Forge);
        Assert.Single(types);
        Assert.Equal("card_skin", types[0].Type);

        var list = await service.GetResourceListAsync(Forge, "zh-Hans", "card_skin", "鲸", "卡面", "general", "size", 1, 24);
        Assert.Equal(1, list.Total);
        Assert.Equal("deepseek_chan", Assert.Single(list.Items).Id);
        var listUri = handler.RequestedUris.First(u => u.Contains("/api/resources?"));
        Assert.Contains("type=card_skin", listUri);
        Assert.Contains("rating=general", listUri);
        Assert.Contains("sort=size", listUri);

        var detail = await service.GetResourceDetailAsync(Forge, "deepseek_chan", "zh-Hans");
        Assert.Equal("DeepSeek 小鲸娘", detail.Name);
        Assert.Single(detail.Previews);
        Assert.Equal("CardSkinMod", detail.Versions[0].ModDependencies[0].Id);

        var cachedPreview = await service.CacheResourcePreviewAsync(
            Forge, detail.Id, detail.Previews[0].Sha256, detail.Previews[0].Url);
        Assert.StartsWith(WorkshopService.CoverOrigin, cachedPreview);
    }

    // ============================== 资源安装路由 ==============================

    [Fact]
    public void Installer_InstallsCardAudioCharacterHomeBgRml_AndUpdatesInPlace()
    {
        using var sandbox = new TestSandbox("ap-res-install");
        var gameDir = CreateFakeGameDirectory(sandbox);
        var modManager = new ModManager(TestPaths.AppDirectory);
        var skinManager = new SkinManager();
        var characterSkins = new CharacterSkinManager();
        var audioSkins = new AudioSkinSchemeManager();
        var resourceMods = new ResourceModManager();
        var homeBg = new HomeBackgroundService(modManager);

        var installer = new WorkshopResourceInstaller(
            skinManager,
            characterSkins,
            () => new CharacterSkinSchemeManager(CharacterSkinCatalog.Load(
                new GameProtocolContext(Path.Combine(TestPaths.AppDirectory, "Protocol")),
                Path.Combine(TestPaths.AppDirectory, "GameData"))),
            audioSkins,
            resourceMods,
            homeBg);

        var png = CreatePngBytes(64, 36);

        // 1. card_skin（含传统无 resource.json 目录格式 & 原地更新）
        var cardZip = sandbox.Combine("deepseek.zip");
        CreateZip(cardZip, new Dictionary<string, byte[]>
        {
            ["deepseek_chan/skin.json"] = """{"name":"DeepSeek卡面","author":"hyumerin","description":"test"}"""u8.ToArray(),
            ["deepseek_chan/UT_HandCard_10001.png"] = png
        });
        var cardDetail = new WorkshopResourceDetail { Id = "deepseek_chan", Type = "card_skin", Name = "DeepSeek卡面", Author = "hyumerin" };
        var cardV1 = new WorkshopResourceVersion { Version = "1.0.0", Sha256 = "sha1" };

        var r1 = installer.Install(gameDir, cardDetail, cardV1, cardZip);
        Assert.Equal("deepseek_chan", r1.TargetId);
        Assert.True(File.Exists(Path.Combine(skinManager.GetSkinsDirectory(gameDir), "deepseek_chan", "UT_HandCard_10001.png")));

        // 更新 v1.1.0 应原地覆盖而不是生成 deepseek_chan_2
        var cardZipV2 = sandbox.Combine("deepseek-v2.zip");
        CreateZip(cardZipV2, new Dictionary<string, byte[]>
        {
            ["wrapper/resource.json"] = """{"schemaVersion":1,"id":"deepseek_chan","type":"card_skin","version":"1.1.0","contentRoot":"assets"}"""u8.ToArray(),
            ["wrapper/assets/UT_HandCard_10001.png"] = png,
            ["wrapper/assets/UT_HandCard_10002.png"] = png
        });
        var r1v2 = installer.Install(gameDir, cardDetail, new WorkshopResourceVersion { Version = "1.1.0", Sha256 = "sha2" }, cardZipV2);
        Assert.Equal("deepseek_chan", r1v2.TargetId);
        Assert.False(Directory.Exists(Path.Combine(skinManager.GetSkinsDirectory(gameDir), "deepseek_chan_2")));
        Assert.True(File.Exists(Path.Combine(skinManager.GetSkinsDirectory(gameDir), "deepseek_chan", "UT_HandCard_10002.png")));

        // 2. character_skin（单角色皮肤 + resource.json 剥离 + 更新不报重复）
        var charZip = sandbox.Combine("char.zip");
        CreateZip(charZip, new Dictionary<string, byte[]>
        {
            ["char_pack/resource.json"] = """{"schemaVersion":1,"id":"parunan_alt","type":"character_skin","version":"1.0.0"}"""u8.ToArray(),
            ["char_pack/character-skin.json"] = """{"schemaVersion":1,"id":"parunan_alt","name":"帕露南换装","heroId":101,"skinItemId":0,"images":{"Character":"images/char.png"}}"""u8.ToArray(),
            ["char_pack/images/char.png"] = png
        });
        var charDetail = new WorkshopResourceDetail { Id = "parunan_alt", Type = "character_skin", Name = "帕露南换装" };
        var r2 = installer.Install(gameDir, charDetail, new WorkshopResourceVersion { Version = "1.0.0", Sha256 = "sha_c1" }, charZip);
        Assert.Equal("parunan_alt", r2.TargetId);
        Assert.True(File.Exists(Path.Combine(characterSkins.Package(gameDir, "parunan_alt"), "character-skin.json")));
        // 再次安装同一方案（更新）也必须顺利通过
        var r2v2 = installer.Install(gameDir, charDetail, new WorkshopResourceVersion { Version = "1.0.1", Sha256 = "sha_c2" }, charZip);
        Assert.Equal("parunan_alt", r2v2.TargetId);

        // 3. audio_skin（剥离顶层目录与 resource.json）
        var audioZip = sandbox.Combine("audio.zip");
        CreateZip(audioZip, new Dictionary<string, byte[]>
        {
            ["my_audio_root/resource.json"] = """{"schemaVersion":1,"id":"kuro_voice","type":"audio_skin","version":"1.0.0"}"""u8.ToArray(),
            ["my_audio_root/audio-scheme.json"] = """{"id":"kuro_voice","name":"库洛语音包","rules":[]}"""u8.ToArray(),
            ["my_audio_root/voice/hello.ogg"] = [0x4F, 0x67, 0x67, 0x53]
        });
        var r3 = installer.Install(
            gameDir,
            new WorkshopResourceDetail { Id = "kuro_voice", Type = "audio_skin", Name = "库洛语音包" },
            new WorkshopResourceVersion { Version = "1.0.0", Sha256 = "sha_a1" },
            audioZip);
        Assert.Equal("kuro_voice", r3.TargetId);
        Assert.True(File.Exists(Path.Combine(AudioSkinSchemeManager.SchemeDirectory(gameDir, "kuro_voice"), "voice", "hello.ogg")));
        Assert.False(File.Exists(Path.Combine(AudioSkinSchemeManager.SchemeDirectory(gameDir, "kuro_voice"), "resource.json")));

        // 4. home_background（图片安装 + 拒绝 VP9 视频编码）
        var bgImgZip = sandbox.Combine("bg-img.zip");
        CreateZip(bgImgZip, new Dictionary<string, byte[]>
        {
            ["resource.json"] = """{"schemaVersion":1,"id":"starry_bg","type":"home_background","version":"1.0.0"}"""u8.ToArray(),
            ["wallpaper.png"] = png
        });
        var r4 = installer.Install(
            gameDir,
            new WorkshopResourceDetail { Id = "starry_bg", Type = "home_background", Name = "星空壁纸" },
            new WorkshopResourceVersion { Version = "1.0.0", Sha256 = "sha_b1" },
            bgImgZip);
        Assert.Equal("homeBackground", r4.JumpPage);
        Assert.Equal("image", homeBg.GetStatus(gameDir).Mode);

        // 5. rml_bundle
        var rmlZip = sandbox.Combine("rml.zip");
        CreateZip(rmlZip, new Dictionary<string, byte[]>
        {
            ["bundle_root/resource.json"] = """{"schemaVersion":1,"id":"ui_reskin","type":"rml_bundle","version":"1.0.0"}"""u8.ToArray(),
            ["bundle_root/mod.json"] = """{"name":"UI Reskin"}"""u8.ToArray(),
            ["bundle_root/replace/hud.png"] = png
        });
        var r5 = installer.Install(
            gameDir,
            new WorkshopResourceDetail { Id = "ui_reskin", Type = "rml_bundle", Name = "UI 美化包" },
            new WorkshopResourceVersion { Version = "1.0.0", Sha256 = "sha_r1" },
            rmlZip);
        Assert.Equal("resourceMods", r5.JumpPage);
        Assert.NotEmpty(resourceMods.GetStatus(gameDir).Mods);

        // 6. 读取已安装状态（全部 5 个均在磁盘上且可查）
        var installed = installer.LoadInstalled(gameDir);
        Assert.Equal(5, installed.Count);
        Assert.Equal("1.1.0", installed["deepseek_chan"].Version);

        // 7. 未知类型清晰拒绝
        var unknownZip = sandbox.Combine("unknown.zip");
        CreateZip(unknownZip, new Dictionary<string, byte[]> { ["readme.txt"] = "hi"u8.ToArray() });
        var ex = Assert.Throws<InvalidOperationException>(() => installer.Install(
            gameDir,
            new WorkshopResourceDetail { Id = "future_res", Type = "shader_pack", Name = "Future" },
            new WorkshopResourceVersion { Version = "1.0.0", Sha256 = "x" },
            unknownZip));
        Assert.Contains("shader_pack", ex.Message);
    }

    // ============================== 测试夹具与辅助 ==============================

    private static WorkshopService CreateService(TestSandbox sandbox, HttpMessageHandler handler) => new()
    {
        ApiHandlerOverride = handler,
        DownloadHandlerOverride = handler,
        CoverDirectoryOverride = sandbox.EnsureDirectory("covers")
    };

    private static string CreateFakeGameDirectory(TestSandbox sandbox)
    {
        var game = sandbox.EnsureDirectory("game");
        sandbox.EnsureDirectory("game/AstralParty_ModLoader/mods");
        sandbox.EnsureDirectory("game/AstralParty_CN_Data");
        File.WriteAllBytes(Path.Combine(game, "version.dll"), [0x4D, 0x5A]);
        return game;
    }

    private static byte[] CreatePngBytes(int width, int height)
    {
        var stride = width * 4;
        var pixels = new byte[height * stride];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 200;
            pixels[i + 1] = 120;
            pixels[i + 2] = 60;
            pixels[i + 3] = 255;
        }

        var bmp = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private static void CreateZip(string path, Dictionary<string, byte[]> entries)
    {
        if (File.Exists(path)) File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, data) in entries)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(data, 0, data.Length);
        }
    }

    private sealed class RangeStubHandler(byte[] fullPayload) : HttpMessageHandler
    {
        private int _rangeRequestCount;
        private long _bytesServed;

        public bool SupportRange { get; set; } = true;
        public int ArtificialDelayMs { get; set; }
        public long? FailOnOffsetOver { get; set; }
        public int RangeRequestCount => Volatile.Read(ref _rangeRequestCount);
        public long BytesServed
        {
            get => Interlocked.Read(ref _bytesServed);
            set => Interlocked.Exchange(ref _bytesServed, value);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ArtificialDelayMs > 0) await Task.Delay(ArtificialDelayMs, cancellationToken);

            var range = request.Headers.Range?.Ranges.FirstOrDefault();
            if (SupportRange && range is not null)
            {
                Interlocked.Increment(ref _rangeRequestCount);
                var from = range.From ?? 0;
                var to = range.To ?? (fullPayload.Length - 1);
                to = Math.Min(to, fullPayload.Length - 1);

                if (FailOnOffsetOver is { } limit && from >= limit)
                    throw new IOException("模拟断点中断");

                var len = (int)(to - from + 1);
                var slice = new byte[len];
                Buffer.BlockCopy(fullPayload, (int)from, slice, 0, len);
                Interlocked.Add(ref _bytesServed, len);

                var res = new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(slice)
                };
                res.Headers.ETag = new EntityTagHeaderValue("\"test-etag\"");
                res.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, to, fullPayload.Length);
                return res;
            }

            Interlocked.Add(ref _bytesServed, fullPayload.Length);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(fullPayload)
            };
        }
    }

    private sealed class JsonRouteHandler(Dictionary<string, byte[]> routes) : HttpMessageHandler
    {
        public List<string> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!.AbsoluteUri);
            var path = request.RequestUri!.AbsolutePath;
            if (!routes.TryGetValue(path, out var body))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(body)
            };
            return Task.FromResult(response);
        }
    }

    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
