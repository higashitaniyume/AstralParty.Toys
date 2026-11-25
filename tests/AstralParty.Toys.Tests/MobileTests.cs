using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstralParty.Toys.Mobile;
using AstralParty.Toys.Mobile.Bundles;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Logging;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Mobile.Patching;
using AstralParty.Toys.Mobile.Services;
using AstralParty.Toys.Mobile.Targets;
using Xunit.Abstractions;
using AstralParty.Toys.Mobile.Payload;
using AstralParty.Toys.Services.Mobile;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public sealed class RealBundleFactAttribute : FactAttribute
{
    public RealBundleFactAttribute()
    {
        var path = Environment.GetEnvironmentVariable("ASTRAL_TEST_BUNDLE");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            Skip = "设置 ASTRAL_TEST_BUNDLE 指向真实 __data 后可验证 splice 换回";
    }
}

public class MobileTests
{
    private readonly ITestOutputHelper _output;

    public MobileTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CatalogReader_ResolvesRuntimeBundleFromSyntheticCatalog()
    {
        var json = BuildCatalog("AstralParty.Runtime.dll", "deadbeef.bundle");
        var catalog = AddressablesCatalog.Parse(Encoding.UTF8.GetBytes(json));

        Assert.Equal("build-123", catalog.BuildResultHash);
        Assert.Equal("deadbeef", catalog.BundleHashFor(AddressablesCatalog.RuntimeAssetKey));
        Assert.Null(catalog.BundleHashFor("NoSuchAsset.dll"));
    }

    [Fact]
    public void AndroidChannel_QuotesPathsSoTheyCannotBreakOutOfTheShell()
    {
        Assert.Equal("'/sdcard/a b'", AndroidChannel.Quote("/sdcard/a b"));
        Assert.Equal("'/sdcard/__proto__'", AndroidChannel.Quote("/sdcard/__proto__"));
        // A quote inside the path must not be able to close the literal and start a new command.
        Assert.Equal("'/sdcard/it'\\''s'", AndroidChannel.Quote("/sdcard/it's"));
    }

    [Fact]
    public void ModPackage_ReadsAndStripsTheTopDirectory()
    {
        using var sandbox = new TestSandbox("ap-mobile-zip-ok");
        var zip = sandbox.Combine("mod.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "DemoMod/DemoMod.dll", [1, 2, 3]);
            WriteZipEntry(archive, "DemoMod/DemoMod.json",
                Encoding.UTF8.GetBytes("{\"id\":\"DemoMod\",\"name\":\"演示\",\"version\":\"1.0.0\"}"));
            WriteZipEntry(archive, "DemoMod/data/table.txt", [9]);
        }

        var package = MobileModPackage.Read(zip);
        Assert.Equal("DemoMod", package.ModId);
        Assert.Equal("演示", package.Name);
        Assert.Equal("1.0.0", package.Version);
        Assert.Equal(new[] { "DemoMod.dll", "DemoMod.json", "data/table.txt" },
            package.Entries.Select(e => e.RelativePath).ToArray());
    }

    [Fact]
    public void ModPackage_RejectsDangerousOrIncompleteArchives()
    {
        using var sandbox = new TestSandbox("ap-mobile-zip-bad");

        // 路径穿越
        var traversal = sandbox.Combine("t.zip");
        using (var archive = ZipFile.Open(traversal, ZipArchiveMode.Create))
        {
            WriteZipEntry(archive, "DemoMod/DemoMod.dll", [1]);
            WriteZipEntry(archive, "DemoMod/../../evil.txt", [1]);
        }
        Assert.Throws<InvalidDataException>(() => MobileModPackage.Read(traversal));

        // 缺 <ModId>.dll
        var noDll = sandbox.Combine("n.zip");
        using (var archive = ZipFile.Open(noDll, ZipArchiveMode.Create))
            WriteZipEntry(archive, "DemoMod/DemoMod.json", Encoding.UTF8.GetBytes("{\"id\":\"DemoMod\"}"));
        Assert.Throws<InvalidDataException>(() => MobileModPackage.Read(noDll));

        // 空包
        var empty = sandbox.Combine("e.zip");
        using (var archive = ZipFile.Open(empty, ZipArchiveMode.Create))
        {
        }
        Assert.Throws<InvalidDataException>(() => MobileModPackage.Read(empty));
    }

    private static void WriteZipEntry(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    [Fact]
    public void ModConfig_EditsScalarsByTextSpanAndTouchesNothingElse()
    {
        const string text = """
        {
          // 顶部说明：任何改动都不能动这行
          "enabled": true,
          "speed": 1.0,
          "count": 1,
          "name": "带, 逗号 和: 冒号 的字符串",
          "nested": { "a": 1, "b": [1, 2] },
          /* 块注释 */
          "_comments": { "enabled": { "zh-Hans": "开关" } }
        }
        """;

        var off = MobileModConfig.SetScalar(text, "enabled", "bool", "false");
        Assert.Contains("\"enabled\": false", off);
        Assert.Contains("// 顶部说明", off);
        Assert.Contains("/* 块注释 */", off);

        // 数字原样写回：1 不能变成 1.0
        Assert.Contains("\"count\": 2,", MobileModConfig.SetScalar(text, "count", "number", "2"));
        Assert.Contains("\"speed\": 1,", MobileModConfig.SetScalar(text, "speed", "number", "1"));

        // 字符串里的逗号/冒号不能把值区间截断
        var renamed = MobileModConfig.SetScalar(text, "name", "string", "新名字");
        Assert.Contains("\"name\": \"新名字\",", renamed);
        Assert.Contains("\"nested\": { \"a\": 1, \"b\": [1, 2] },", renamed);

        // 对象字段不走定点编辑
        Assert.Throws<InvalidOperationException>(() => MobileModConfig.SetScalar(text, "nested", "other", "x"));
    }

    [Fact]
    public void ModConfig_ParsesFieldsAndComments()
    {
        const string text = """
        {
          "enabled": true,
          "speed": 2.5,
          "name": "abc",
          "nested": { "a": 1 },
          "_comments": {
            "enabled": { "zh-Hans": "是否启用", "en": "enabled?" },
            "speed": { "en": "speed only" }
          }
        }
        """;

        var fields = MobileModConfig.Parse(text, "zh-Hans");
        Assert.Equal(new[] { "enabled", "speed", "name", "nested" }, fields.Select(f => f.Name).ToArray());

        Assert.Equal("bool", fields[0].Kind);
        Assert.True(fields[0].BoolValue);
        Assert.Equal("是否启用", fields[0].Comment);

        Assert.Equal("number", fields[1].Kind);
        Assert.Equal(2.5, fields[1].NumberValue);
        Assert.Equal("speed only", fields[1].Comment);   // 缺该语言时退回 en

        Assert.Equal("string", fields[2].Kind);
        Assert.Equal("abc", fields[2].StringValue);
        Assert.Equal("other", fields[3].Kind);
    }

    [Fact]
    public void MobilePayload_IsEmbeddedAndExtractsIntoTheDataRoot()
    {
        using var sandbox = new TestSandbox("ap-mobile-payload-embed");
        var path = MobilePayloadProvisioner.Ensure(sandbox.Root);
        Assert.Equal(MobilePayloadProvisioner.FileName, Path.GetFileName(path));

        var payload = MobilePayload.Read(path);
        // 引导名新旧两个都认；托管包里它在 bootstrap/ 下
        var bootstrapName = MobilePayload.BootstrapFileCandidates
            .FirstOrDefault(name => payload.Files.Any(f => f.RelativePath.EndsWith(name, StringComparison.Ordinal)));
        Assert.False(string.IsNullOrEmpty(bootstrapName), "载荷缺引导 DLL（注入的 cctor 找不到它）");
        Assert.Contains(payload.Files, f => f.RelativePath == "CesiumLoader.Mobile.API.dll");
        Assert.Contains(payload.Files, f => f.RelativePath == "mods/CesiumOverlayMod/CesiumOverlayMod.dll");
        Assert.Contains(payload.Files, f => f.RelativePath == "mods/CardSkinMod/CardSkinMod.dll");
        Assert.Contains(payload.Files, f => f.RelativePath == "mods/SpeedHackMod/SpeedHackMod.dll");
        Assert.Contains(payload.Files, f => f.RelativePath == "mods/FreeCameraMod/FreeCameraMod.dll");

        var builtInModIds = payload.Files
            .Select(f => f.RelativePath)
            .Where(p => p.StartsWith("mods/", StringComparison.Ordinal))
            .Select(p => p["mods/".Length..].Split('/')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        Assert.True(builtInModIds.Count >= 20, $"内嵌载荷内置模组数量过少：{builtInModIds.Count}");

        // 用投放时的同一套规则读一遍：会校验引导在根上、SDK 带移动端兜底
        Assert.True(payload.Files.Count >= 3, $"载荷文件太少：{payload.Files.Count}");
        payload.EnsureSdkUsable(allowDesktopSdk: false);
        Assert.False(string.IsNullOrEmpty(payload.SdkVersion), "读不到 SDK 程序集版本");

        // 版本/内容没变就不要再落一次（删掉包后 Ensure 会按标记重新写回，所以这里查标记而不是包）
        var probe = Path.Combine(Path.GetDirectoryName(path)!, "probe.tmp");
        File.WriteAllText(probe, "x");
        MobilePayloadProvisioner.Ensure(sandbox.Root);
        Assert.True(File.Exists(probe), "内容未变时不该清空载荷目录");
    }

    [Fact]
    public async Task InstallService_DeployPayloadDeploysBuiltinModsAndPreservesDisabledAndConfig()
    {
        using var sandbox = new TestSandbox("ap-mobile-deploy-mods");
        var payloadPath = MobilePayloadProvisioner.Ensure(sandbox.Root);
        var fs = new InMemoryDeployFileSystem();
        const string root = "files/";
        fs.Dirs.Add(root + "com.unity.addressables");
        fs.Dirs.Add(root + "CesiumLoader");
        fs.Dirs.Add(root + "CesiumLoader/disabled_mods");
        fs.Dirs.Add(root + "CesiumLoader/disabled_mods/SpeedHackMod");
        fs.Files[root + "CesiumLoader/disabled_mods/SpeedHackMod/config.json"] = Encoding.UTF8.GetBytes("{\"keepDisabledConfig\":true}");
        fs.Files[root + "CesiumLoader/mods/CesiumOverlayMod/config.json"] = Encoding.UTF8.GetBytes("{\"keepOverlayConfig\":true}");
        fs.Files[root + "CesiumLoader/CesiumAndroidApi.dll"] = [1, 2, 3];

        var service = new AstralParty.Toys.Mobile.Services.MobileInstallService(
            new SingleFsChannel("android", root, fs),
            new MobileInstallStore(sandbox.Root),
            new MobileTargetStore(sandbox.Root),
            "unused");

        var target = new MobileTarget
        {
            Id = "android:dev1",
            Kind = "android",
            Serial = "dev1",
            AppId = "com.example.game",
            Online = true,
        };

        var result = await service.DeployPayloadAsync(target, payloadPath);
        Assert.True(result.Ok, result.Message);

        Assert.True(fs.Files.ContainsKey(root + "CesiumLoader/bootstrap/CesiumLoader.Mobile.Bootstrap.dll"));
        Assert.True(fs.Files.ContainsKey(root + "CesiumLoader/sdk/CesiumLoader.SDK.dll"));
        Assert.True(fs.Files.ContainsKey(root + "CesiumLoader/CesiumLoader.Mobile.API.dll"));
        Assert.True(fs.Files.ContainsKey(root + "CesiumLoader/mods/CesiumOverlayMod/CesiumOverlayMod.dll"));
        Assert.True(fs.Files.ContainsKey(root + "CesiumLoader/disabled_mods/SpeedHackMod/SpeedHackMod.dll"));
        Assert.False(fs.Files.ContainsKey(root + "CesiumLoader/mods/SpeedHackMod/SpeedHackMod.dll"));
        Assert.False(fs.Files.ContainsKey(root + "CesiumLoader/CesiumAndroidApi.dll"));

        Assert.Equal("{\"keepDisabledConfig\":true}",
            Encoding.UTF8.GetString(fs.Files[root + "CesiumLoader/disabled_mods/SpeedHackMod/config.json"]));
        Assert.Equal("{\"keepOverlayConfig\":true}",
            Encoding.UTF8.GetString(fs.Files[root + "CesiumLoader/mods/CesiumOverlayMod/config.json"]));

        using var doc = JsonDocument.Parse(fs.Files[root + "CesiumLoader/installed.json"]);
        var builtInMods = doc.RootElement.GetProperty("builtInMods").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("CesiumOverlayMod", builtInMods);
        Assert.Contains("SpeedHackMod", builtInMods);
        Assert.Contains("CardSkinMod", builtInMods);
    }

    [Fact]
    public void OperationLog_WritesFileAndStreamsEveryEntry()
    {
        using var sandbox = new TestSandbox("ap-mobile-log");
        var streamed = new List<MobileLogEntry>();
        var log = new MobileOperationLog(sandbox.Combine("logs"), "mobile-test");
        log.Written += streamed.Add;

        log.Info("第一步");
        log.File("读取 Documents/x.bin  12 B");
        log.Error("失败样例");

        Assert.True(File.Exists(log.FilePath), "日志文件没有生成");
        var text = File.ReadAllText(log.FilePath);
        Assert.Contains("第一步", text);
        Assert.Contains("读取 Documents/x.bin  12 B", text);
        Assert.Contains("[ERROR] 失败样例", text);
        Assert.Equal("mobile-test.log", Path.GetFileName(log.FilePath));

        // 三条都要实时推给界面（构造时那条在订阅之前，不算）
        Assert.Equal(3, streamed.Count);
        Assert.Equal("第一步", streamed[0].Message);
        Assert.Equal("ERROR", streamed[2].Level);
        Assert.Equal(4, log.Count);
    }

    [Fact]
    public async Task LoggingFileSystem_RecordsEveryFileOperation()
    {
        using var sandbox = new TestSandbox("ap-mobile-logfs");
        var log = new MobileOperationLog(sandbox.Combine("logs"), "fs-test");
        await using var fileSystem = new LoggingFileSystem(new FakeFileSystem { Bytes = [1, 2, 3, 4] }, log);

        var bytes = await fileSystem.ReadAllBytesAsync("Documents/big.bin");
        await fileSystem.ExistsAsync("Documents/nope");
        await fileSystem.ListAsync("Documents");
        await fileSystem.WriteAllBytesAsync("Documents/out.bin", bytes);
        await fileSystem.RenameAsync("Documents/out.bin", "Documents/renamed.bin");
        await fileSystem.DeleteAsync("Documents/renamed.bin");

        Assert.Equal(4, bytes.Length);
        var text = File.ReadAllText(log.FilePath);
        var expectedHash = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3, 4 }))[..16].ToLowerInvariant();
        Assert.Contains("读取  Documents/big.bin  4 B", text);
        Assert.Contains($"sha256={expectedHash}", text);
        Assert.Contains("存在? 否  Documents/nope", text);
        Assert.Contains("列目录  Documents  → 2 项", text);
        Assert.Contains("写入  Documents/out.bin  4 B", text);
        Assert.Contains("改名  Documents/out.bin  →  Documents/renamed.bin", text);
        Assert.Contains("删除  Documents/renamed.bin", text);
    }

    private sealed class FakeFileSystem : IDeviceFileSystem
    {
        public byte[] Bytes { get; set; } = [];

        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<DeviceFileInfo?> StatAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<DeviceFileInfo?>(null);

        public Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(["aaa", "bbb"]);

        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Bytes);

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult("");

        public Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task MakeDirectoriesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RenameAsync(string from, string to, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void PatchMarkerDetector_RecognisesBothEncodingsAndBothNames()
    {
        const string current = "CesiumLoader.Mobile.Bootstrap.Boot";
        const string legacy = "CesiumLoader.Android.Bootstrap.Boot";

        Assert.True(PatchMarkerDetector.ContainsMarker(Encoding.ASCII.GetBytes("prefix" + current + "suffix")));
        Assert.True(PatchMarkerDetector.ContainsMarker(Encoding.Unicode.GetBytes(current)));
        Assert.True(PatchMarkerDetector.ContainsMarker(Encoding.ASCII.GetBytes(legacy)));
        Assert.True(PatchMarkerDetector.ContainsLegacyMarker(Encoding.Unicode.GetBytes(legacy)));
        Assert.False(PatchMarkerDetector.ContainsCurrentMarker(Encoding.Unicode.GetBytes(legacy)));
        Assert.False(PatchMarkerDetector.ContainsMarker(Encoding.ASCII.GetBytes("boot")));
    }

    [Fact]
    public void TargetStore_KeepsMobileStateSeparateFromLocalProfiles()
    {
        using var sandbox = new TestSandbox("ap-mobile-store");
        var store = new MobileTargetStore(sandbox.Root);

        Assert.Equal("local", store.LoadActive().Kind);

        store.SetActiveLocal("pc-profile");
        Assert.Equal("pc-profile", store.LoadActive().ProfileId);

        store.Upsert(new MobileTarget { Id = "ios:abc", Kind = "ios", Serial = "abc", Label = "iPhone" });
        store.SetActiveMobile("ios:abc");

        var active = store.LoadActive();
        Assert.Equal("mobile", active.Kind);
        Assert.Equal("ios:abc", active.TargetId);
        Assert.Empty(active.ProfileId);
        Assert.Equal("iPhone", Assert.Single(store.LoadTargets()).Label);
        Assert.False(File.Exists(sandbox.Combine("game-profiles.json")));

        store.MarkOffline("ios:abc");
        Assert.False(Assert.Single(store.LoadTargets()).Online);

        var reloaded = new MobileTargetStore(sandbox.Root);
        Assert.Equal("ios:abc", reloaded.LoadActive().TargetId);
    }

    [Fact]
    public async Task DeviceService_DiscoverPreservesAppIdAndInstallState()
    {
        using var sandbox = new TestSandbox("ap-mobile-discover-preserve");
        var store = new MobileTargetStore(sandbox.Root);
        store.Upsert(new MobileTarget
        {
            Id = "android:dev1",
            Kind = "android",
            Serial = "dev1",
            Label = "OldName",
            AppId = "com.custom.channel",
            BundleOuter = "outer-hash",
            Installed = true,
            InstalledInner = "inner-hash",
            InstalledPatchedSha256 = "patched-sha",
            OriginalSha256 = "orig-sha",
        });

        var channel = new FakeDiscoveryChannel("android", new DeviceInfo
        {
            Kind = "android",
            Id = "dev1",
            Label = "NewPhone",
            Model = "Pixel",
            OsVersion = "14",
            State = "device",
            Paired = true,
        });
        var service = new MobileDeviceService(sandbox.Root, [channel]);
        await service.DiscoverAsync();

        var after = Assert.Single(service.GetTargets());
        Assert.Equal("NewPhone", after.Label);
        Assert.Equal("com.custom.channel", after.AppId);
        Assert.Equal("outer-hash", after.BundleOuter);
        Assert.True(after.Installed);
        Assert.Equal("inner-hash", after.InstalledInner);
        Assert.Equal("patched-sha", after.InstalledPatchedSha256);
        Assert.Equal("orig-sha", after.OriginalSha256);
        Assert.True(after.Online);
    }

    [Fact]
    public void PayloadProvisioner_ReadSummaryReflectsEmbeddedZip()
    {
        using var sandbox = new TestSandbox("ap-mobile-payload-summary");
        var missing = MobilePayloadProvisioner.ReadSummary(sandbox.Root);
        Assert.False(missing.Present);

        MobilePayloadProvisioner.Ensure(sandbox.Root);
        var summary = MobilePayloadProvisioner.ReadSummary(sandbox.Root);
        Assert.True(summary.Present);
        Assert.False(string.IsNullOrEmpty(summary.SdkVersion));
        Assert.True(summary.FileCount >= 3);
    }

    private sealed class FakeDiscoveryChannel : IDeviceChannel
    {
        private readonly DeviceInfo _device;

        public FakeDiscoveryChannel(string kind, DeviceInfo device)
        {
            Kind = kind;
            _device = device;
        }

        public string Kind { get; }

        public Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeviceInfo>>([_device]);

        public Task<IReadOnlyList<MobilePackageCandidate>> FindGamePackagesAsync(
            string serial, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MobilePackageCandidate>>([]);

        public string BuildRoot(MobileTarget target) => "";

        public Task<IDeviceFileSystem> OpenAsync(MobileTarget target, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task InstallService_DeleteModRemovesFromModsAndDisabledMods()
    {
        using var sandbox = new TestSandbox("ap-mobile-delete-mod");
        var target = new MobileTarget
        {
            Id = "ios:dev1",
            Kind = "ios",
            Serial = "dev1",
            AppId = "com.example.game",
            Online = true,
        };
        var fs = new InMemoryDeleteFileSystem();
        fs.Existing.Add("Documents/com.unity.addressables");
        fs.Existing.Add("Documents/CesiumLoader");
        fs.Existing.Add("Documents/CesiumLoader/mods/DemoMod");
        fs.Existing.Add("Documents/CesiumLoader/disabled_mods/OtherMod");

        var service = new AstralParty.Toys.Mobile.Services.MobileInstallService(
            new SingleFsChannel("ios", "Documents/", fs),
            new MobileInstallStore(sandbox.Root),
            new MobileTargetStore(sandbox.Root),
            "unused");

        var bad = await service.DeleteModAsync(target, "../evil");
        Assert.False(bad.Ok);

        var okEnabled = await service.DeleteModAsync(target, "DemoMod");
        Assert.True(okEnabled.Ok);
        Assert.DoesNotContain("Documents/CesiumLoader/mods/DemoMod", fs.Existing);

        var okDisabled = await service.DeleteModAsync(target, "OtherMod");
        Assert.True(okDisabled.Ok);
        Assert.DoesNotContain("Documents/CesiumLoader/disabled_mods/OtherMod", fs.Existing);

        var missing = await service.DeleteModAsync(target, "MissingMod");
        Assert.False(missing.Ok);
    }

    private sealed class SingleFsChannel(string kind, string root, IDeviceFileSystem fs) : IDeviceChannel
    {
        public string Kind { get; } = kind;

        public Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeviceInfo>>([]);

        public Task<IReadOnlyList<MobilePackageCandidate>> FindGamePackagesAsync(
            string serial, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MobilePackageCandidate>>([]);

        public string BuildRoot(MobileTarget target) => root;

        public Task<IDeviceFileSystem> OpenAsync(MobileTarget target, CancellationToken cancellationToken = default) =>
            Task.FromResult(fs);
    }

    private sealed class InMemoryDeleteFileSystem : IDeviceFileSystem
    {
        public HashSet<string> Existing { get; } = new(StringComparer.Ordinal);

        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Existing.Contains(path));

        public Task<DeviceFileInfo?> StatAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Existing.Contains(path) ? new DeviceFileInfo(0, DateTimeOffset.UtcNow) : null);

        public Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]>([]);

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult("");

        public Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
        {
            Existing.Add(path);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            Existing.Remove(path);
            return Task.CompletedTask;
        }

        public Task MakeDirectoriesAsync(string path, CancellationToken cancellationToken = default)
        {
            Existing.Add(path);
            return Task.CompletedTask;
        }

        public Task RenameAsync(string from, string to, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public void DevicePage_IsWiredIntoNavigationDispatcherAndAllFourDictionaries()
    {
        var appSource = TestPaths.AppSourceDirectory;
        var webUi = Path.Combine(appSource, "WebUI");
        var dispatcher = File.ReadAllText(Path.Combine(appSource, "HybridWindow.Dispatcher.cs"));
        var router = File.ReadAllText(Path.Combine(webUi, "js", "core", "router.js"));
        var index = File.ReadAllText(Path.Combine(webUi, "index.html"));
        var deviceScripts = Directory.GetFiles(Path.Combine(webUi, "js", "v2"), "devices*.js")
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToList();
        var script = string.Join("\n", deviceScripts);
        var entryScript = File.ReadAllText(Path.Combine(webUi, "js", "v2", "devices.js"));

        Assert.Contains("HandleDeviceMessage(type, root)", dispatcher);
        Assert.Contains("pageName === 'devices'", router);
        Assert.Contains("data-page=\"devices\"", index);
        Assert.Contains("<!-- @include view-devices.html -->", index);
        Assert.Contains("js/v2/devices-helpers.js", index);
        Assert.Contains("js/v2/devices-files.js", index);
        Assert.Contains("js/v2/devices.js", index);
        Assert.Contains("window.V2Devices", entryScript);
        Assert.Contains("dv-env-link", entryScript);
        Assert.Contains("rel=\"noopener noreferrer\"", entryScript);
        Assert.Contains("dvExperimentalDialog", entryScript);
        Assert.Contains("dv-head-experimental", entryScript);
        Assert.Contains("showModal", entryScript);
        Assert.Contains("dev-busy", script);
        var componentsCss = File.ReadAllText(Path.Combine(webUi, "css", "v2-components.css"));
        Assert.Contains("#devicesView { display: block; overflow-y: auto; }", componentsCss);

        var keys = Regex.Matches(script, @"'devices\.[A-Za-z0-9]+'")
            .Select(m => m.Value.Trim('\''))
            .Distinct()
            .ToList();
        Assert.NotEmpty(keys);
        foreach (var lang in new[] { "zh-Hans", "en", "ja", "zh-Hant" })
        {
            var dict = File.ReadAllText(Path.Combine(webUi, "js", "i18n", $"dict-{lang}.js"));
            var missing = keys.Where(k => !dict.Contains($"'{k}':", StringComparison.Ordinal)).ToList();
            Assert.True(missing.Count == 0, $"{lang} 缺少文案：{string.Join(", ", missing)}");
        }
    }

    /// <summary>
    /// 设备页的样式是手写的，且没有可自动比对的快照；这里保证「脚本里出现的每个 dv- 类都在 devices.css 里有定义」，
    /// 免得改了标记却忘了配样式，页面悄悄变成没样式的一堆方块。
    /// </summary>
    [Fact]
    public void DevicePage_EveryRenderedClassIsStyledAndLoopIsInPlace()
    {
        var webUi = Path.Combine(TestPaths.AppSourceDirectory, "WebUI");
        var script = string.Join("\n", Directory.GetFiles(Path.Combine(webUi, "js", "v2"), "devices*.js")
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(File.ReadAllText));
        var css = File.ReadAllText(Path.Combine(webUi, "css", "devices.css"));

        var tokens = Regex.Matches(script, @"\bdv-[a-z0-9-]+")
            .Select(m => m.Value)
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        Assert.NotEmpty(tokens);
        var missing = tokens.Where(t => !css.Contains("." + t, StringComparison.Ordinal)).ToList();
        Assert.True(missing.Count == 0, "devices.css 缺少样式定义：" + string.Join(", ", missing));

        // 旧页面样式已搬到 devices.css，公共表里只留下滚动容器那一条。
        var components = File.ReadAllText(Path.Combine(webUi, "css", "v2-components.css"));
        Assert.Contains("#devicesView { display: block; overflow-y: auto; }", components);
        Assert.DoesNotContain(".dev-card {", components);
        Assert.DoesNotContain(".dev-busy {", components);

        // 新样式表必须真的被引入，否则页面会整片失去样式。
        Assert.Contains("@import \"devices.css\";", File.ReadAllText(Path.Combine(webUi, "css", "v2.css")));
    }

    [Fact]
    public void InstallStore_BackupIsIdempotentAndStateSurvivesReload()
    {
        using var sandbox = new TestSandbox("ap-mobile-install-store");
        var store = new MobileInstallStore(sandbox.Root);
        var inner = "f34d1f473cc7580b5d29def0211599a4";

        store.SaveBackup(inner, [1, 2, 3]);
        store.SaveBackup(inner, [9, 9, 9]);
        Assert.Equal(new byte[] { 1, 2, 3 }, store.ReadBackup(inner));

        store.Update("ios:abc", record =>
        {
            record.BundleOuter = "outer";
            record.Installed.Inner = inner;
            record.Installed.PatchedSha256 = "abc";
        });
        var reloaded = new MobileInstallStore(sandbox.Root).Get("ios:abc");
        Assert.NotNull(reloaded);
        Assert.Equal("outer", reloaded.BundleOuter);
        Assert.Equal("abc", reloaded.Installed.PatchedSha256);

        store.DeleteBackup(inner);
        Assert.False(store.HasBackup(inner));
    }

    [Fact]
    public void Patcher_IsShippedWithTheApp()
    {
        var repo = Path.GetFullPath(Path.Combine(TestPaths.AppSourceDirectory, "..", ".."));
        var patcherProject = Path.Combine(repo, "src", "CesiumLoader.Mobile.Patcher", "CesiumLoader.Mobile.Patcher.csproj");
        Assert.True(File.Exists(patcherProject), $"注入器工程不存在：{patcherProject}");
        var appProject = File.ReadAllText(Path.Combine(repo, "src", "AstralParty.Toys", "AstralParty.Toys.csproj"));
        Assert.Contains("mobile-patcher", appProject);
        Assert.Contains("CesiumLoader.Mobile.Patcher.csproj", appProject);
    }

    [RealBundleFact]
    public void BundleSplicer_PreservesEveryAssetWhenReplacingWithIdenticalBytes()
    {
        var source = Environment.GetEnvironmentVariable("ASTRAL_TEST_BUNDLE")!;
        using var sandbox = new TestSandbox("ap-mobile-splice");
        var original = File.ReadAllBytes(source);
        var assets = RuntimeAssemblyReader.ReadAllTextAssets(original, out var fileCount);
        Assert.True(fileCount > 0);
        var target = assets[AddressablesCatalog.RuntimeAssetKey];

        var output = sandbox.Combine("spliced.data");
        var result = BundleSplicer.Splice(original, AddressablesCatalog.RuntimeAssetKey, target, output);
        Assert.True(result.Bytes > 0);

        var before = RuntimeAssemblyReader.Fingerprints(original, out _);
        var after = RuntimeAssemblyReader.Fingerprints(File.ReadAllBytes(output), out _);
        Assert.Equal(before, after);

        // 设备页按 Read().AssetCount 显示「资产数」，它必须是 TextAsset 条数而不是 CAB 个数。
        var read = RuntimeAssemblyReader.Read(original, AddressablesCatalog.RuntimeAssetKey);
        Assert.Equal(before.Count, read.AssetCount);
        Assert.NotNull(read.Assembly);
        _output.WriteLine($"资产 {before.Count} 项全部一致，AssetCount={read.AssetCount}，输出 {result.Bytes} B");
    }

    [RealBundleFact]
    public async Task PatcherAndSplicer_EndToEndOffline()
    {
        var source = Environment.GetEnvironmentVariable("ASTRAL_TEST_BUNDLE")!;
        using var sandbox = new TestSandbox("ap-mobile-inject-offline");
        var repo = Path.GetFullPath(Path.Combine(TestPaths.AppSourceDirectory, "..", ".."));
        var patcher = Path.Combine(
            repo, "src", "CesiumLoader.Mobile.Patcher", "bin", "Release", "net8.0",
            "CesiumLoader.Mobile.Patcher.dll");
        Assert.True(File.Exists(patcher), "请先构建 CesiumLoader.Mobile.Patcher");

        var original = File.ReadAllBytes(source);
        var assets = RuntimeAssemblyReader.ReadAllTextAssets(original, out _);
        var before = RuntimeAssemblyReader.Fingerprints(original, out _);

        var info = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(patcher)!,
        };
        var inputDll = sandbox.Combine("AstralParty.Runtime.dll");
        var outputDll = sandbox.Combine("patched.dll");
        var refs = sandbox.Combine("refs");
        Directory.CreateDirectory(refs);
        File.WriteAllBytes(inputDll, assets[AddressablesCatalog.RuntimeAssetKey]);
        foreach (var (name, bytes) in assets)
            File.WriteAllBytes(Path.Combine(refs, name.Replace('/', '_')), bytes);
        foreach (var arg in new[]
                 {
                     patcher, "bootstrap", inputDll, outputDll,
                     "--dir", "CesiumLoader",
                     "--boot-dll", "CesiumLoader.Android.Bootstrap.dll",
                     "--boot-type", "CesiumLoader.Android.Bootstrap.Boot",
                     "--boot-method", "LoadForGame", "--boot-arg", "root",
                     "--refs", refs,
                 })
            info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, stdout + "\n" + stderr);

        var patchedDll = await File.ReadAllBytesAsync(outputDll);
        Assert.True(PatchMarkerDetector.ContainsMarker(patchedDll), "注入后找不到引导标记");

        var output = sandbox.Combine("patched.data");
        BundleSplicer.Splice(original, AddressablesCatalog.RuntimeAssetKey, patchedDll, output);
        var after = RuntimeAssemblyReader.Fingerprints(File.ReadAllBytes(output), out _);
        var changed = before.Keys.Where(n => before[n] != after[n]).ToList();
        Assert.Equal([AddressablesCatalog.RuntimeAssetKey], changed);
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(patchedDll)).ToLowerInvariant(),
            after[AddressablesCatalog.RuntimeAssetKey]);
        _output.WriteLine(stdout);
    }

    [Fact]
    public async Task MobileFileService_NormalizesPathsListsAndPreviewsTextOrBinary()
    {
        Assert.Equal("", MobileFileService.NormalizeRelativePath("/"));
        Assert.Equal("CesiumLoader/logs", MobileFileService.NormalizeRelativePath(@" \CesiumLoader\logs\ "));
        Assert.Throws<InvalidOperationException>(() => MobileFileService.NormalizeRelativePath("../secret"));
        Assert.Throws<InvalidOperationException>(() => MobileFileService.ValidateSingleName("a/b"));
        Assert.Throws<InvalidOperationException>(() => MobileFileService.ValidateSingleName(".."));

        var target = new MobileTarget { Id = "ios:u1", Kind = "ios", Serial = "u1", AppId = "com.feimo.astralpartycn" };
        var fs = new StubExplorerFileSystem { Bytes = Encoding.UTF8.GetBytes("hello cesium\nline2") };
        var service = new MobileFileService(new FakeFileChannel("ios", "Documents/", fs));

        var listed = await service.ListAsync(target, "CesiumLoader");
        Assert.Null(listed.Error);
        Assert.Equal("Documents", listed.RootLabel);
        Assert.Equal("CesiumLoader", listed.Path);
        Assert.Equal(["aaa", "bbb"], listed.Entries.Select(e => e.Name).ToArray());
        Assert.All(listed.Entries, e => Assert.StartsWith("CesiumLoader/", e.Path, StringComparison.Ordinal));

        var textPreview = await service.ReadPreviewAsync(target, "CesiumLoader/logs/boot.log");
        Assert.False(textPreview.Binary);
        Assert.Equal("hello cesium\nline2", textPreview.Text);

        fs.Bytes = [0x4D, 0x5A, 0x00, 0x01];
        var binPreview = await service.ReadPreviewAsync(target, "CesiumLoader/mods/a.dll");
        Assert.True(binPreview.Binary);
        Assert.Null(binPreview.Text);

        using var sandbox = new TestSandbox("ap-mobile-files-rw");
        var localIn = sandbox.Combine("upload.txt");
        await File.WriteAllTextAsync(localIn, "upload-body");
        var upResult = await service.UploadFilesAsync(target, "CesiumLoader/logs", [localIn]);
        Assert.True(upResult.Ok);

        var localOut = sandbox.Combine("sub", "saved.bin");
        var dlResult = await service.DownloadToFileAsync(target, "CesiumLoader/mods/a.dll", localOut);
        Assert.True(dlResult.Ok);
        Assert.Equal([0x4D, 0x5A, 0x00, 0x01], await File.ReadAllBytesAsync(localOut));
    }

    [Fact]
    public void EnvironmentService_ScansProcessListAndRespectsCustomAdbPath()
    {
        using var sandbox = new TestSandbox("ap-mobile-env-adb");
        var missingResult = MobileEnvironmentService.ScanAdbFromProcesses(() => []);
        Assert.False(missingResult.Found);
        Assert.Contains("找不到", missingResult.Message);

        var fakeAdb = sandbox.Combine("adb.exe");
        File.WriteAllText(fakeAdb, "stub");
        var hitResult = MobileEnvironmentService.ScanAdbFromProcesses(() => [fakeAdb, sandbox.Combine("gone.exe")]);
        Assert.True(hitResult.Found);
        Assert.Equal(fakeAdb, hitResult.Path);

        var resolvedCustom = AndroidChannel.ResolveAdbDetailed(fakeAdb);
        Assert.Equal("custom", resolvedCustom.Source);
        Assert.Equal(fakeAdb, resolvedCustom.Path);
        Assert.False(resolvedCustom.ConfigInvalid);

        var invalidFallback = AndroidChannel.ResolveAdbDetailed(sandbox.Combine("missing-adb.exe"));
        Assert.True(invalidFallback.ConfigInvalid);
        Assert.Equal("https://www.apple.com/itunes/download/win64", MobileEnvironmentService.ItunesDownloadUrl);
    }

    private sealed class FakeFileChannel(string kind, string root, IDeviceFileSystem fs) : IDeviceChannel
    {
        public string Kind => kind;
        public string BuildRoot(MobileTarget target) => root;
        public Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeviceInfo>>([]);
        public Task<IReadOnlyList<MobilePackageCandidate>> FindGamePackagesAsync(string serial, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MobilePackageCandidate>>([]);
        public Task<IDeviceFileSystem> OpenAsync(MobileTarget target, CancellationToken cancellationToken = default) =>
            Task.FromResult(fs);
    }

    private sealed class StubExplorerFileSystem : IDeviceFileSystem
    {
        public byte[] Bytes { get; set; } = [];
        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<DeviceFileInfo?> StatAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<DeviceFileInfo?>(new DeviceFileInfo(Bytes.Length, DateTimeOffset.UtcNow, false));
        public Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(["aaa", "bbb"]);
        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(Bytes);
        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult("");
        public Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task MakeDirectoriesAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RenameAsync(string from, string to, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class InMemoryDeployFileSystem : IDeviceFileSystem
    {
        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        public HashSet<string> Dirs { get; } = new(StringComparer.Ordinal);

        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Files.ContainsKey(path) || Dirs.Contains(path.TrimEnd('/')));

        public Task<DeviceFileInfo?> StatAsync(string path, CancellationToken cancellationToken = default)
        {
            if (Files.TryGetValue(path, out var data))
                return Task.FromResult<DeviceFileInfo?>(new DeviceFileInfo(data.Length, DateTimeOffset.UtcNow, false));
            if (Dirs.Contains(path.TrimEnd('/')))
                return Task.FromResult<DeviceFileInfo?>(new DeviceFileInfo(0, DateTimeOffset.UtcNow, true));
            return Task.FromResult<DeviceFileInfo?>(null);
        }

        public Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            var prefix = path.TrimEnd('/') + "/";
            var children = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in Files.Keys.Concat(Dirs))
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal) || key.Length == prefix.Length)
                    continue;
                var rest = key[prefix.Length..];
                var slash = rest.IndexOf('/');
                children.Add(slash >= 0 ? rest[..slash] : rest);
            }
            return Task.FromResult<IReadOnlyList<string>>(children.OrderBy(x => x, StringComparer.Ordinal).ToList());
        }

        public Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Files.TryGetValue(path, out var data) ? data : []);

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Files.TryGetValue(path, out var data) ? Encoding.UTF8.GetString(data) : "");

        public Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
        {
            Files[path] = data;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            Files.Remove(path);
            Dirs.Remove(path.TrimEnd('/'));
            return Task.CompletedTask;
        }

        public Task MakeDirectoriesAsync(string path, CancellationToken cancellationToken = default)
        {
            var normalized = path.TrimEnd('/');
            if (!string.IsNullOrEmpty(normalized))
                Dirs.Add(normalized);
            return Task.CompletedTask;
        }

        public Task RenameAsync(string from, string to, CancellationToken cancellationToken = default)
        {
            if (Files.Remove(from, out var data))
                Files[to] = data;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static string BuildCatalog(string assetKey, string bundleName)
    {
        static byte[] Key(string value)
        {
            var bytes = Encoding.ASCII.GetBytes(value);
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write((byte)0);
            writer.Write(bytes.Length);
            writer.Write(bytes);
            writer.Flush();
            return stream.ToArray();
        }

        var keys = new[] { Key(assetKey), Key(bundleName) };
        var keyOffsets = new List<int>();
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            foreach (var key in keys)
            {
                keyOffsets.Add((int)stream.Position);
                writer.Write(key);
            }
            writer.Flush();
            var keyData = stream.ToArray();

            using var bucketStream = new MemoryStream();
            using (var bucketWriter = new BinaryWriter(bucketStream, Encoding.UTF8, leaveOpen: true))
            {
                bucketWriter.Write(2);       // bucket count (asset key + bundle name key)
                bucketWriter.Write(keyOffsets[0]);
                bucketWriter.Write(1);       // entry count
                bucketWriter.Write(0);       // entry index for asset key
                bucketWriter.Write(keyOffsets[1]);
                bucketWriter.Write(0);       // bundle name key has no entries
                bucketWriter.Flush();

                using var entryStream = new MemoryStream();
                using (var entryWriter = new BinaryWriter(entryStream, Encoding.UTF8, leaveOpen: true))
                {
                    entryWriter.Write(1);    // entry count
                    entryWriter.Write(0);    // internal id index
                    entryWriter.Write(0);
                    entryWriter.Write(1);    // dependency -> bundle name key
                    for (var i = 0; i < 4; i++) entryWriter.Write(0);
                    entryWriter.Flush();

                    return JsonSerializer.Serialize(new
                    {
                        m_BuildResultHash = "build-123",
                        m_InternalIds = Array.Empty<string>(),
                        m_KeyDataString = Convert.ToBase64String(keyData),
                        m_BucketDataString = Convert.ToBase64String(bucketStream.ToArray()),
                        m_EntryDataString = Convert.ToBase64String(entryStream.ToArray()),
                    });
                }
            }
        }
    }
}
