using System.IO.Compression;
using AstralParty.Toys.Mobile;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Mobile.Payload;
using AstralParty.Toys.Mobile.Services;
using AstralParty.Toys.Mobile.Targets;
using AstralParty.Toys.Services.Mobile;
using AstralParty.Toys.Tests.Support;
using Xunit.Abstractions;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 会真正改动设备上的游戏代码包，所以与只读的真机检测分开、单独用环境变量开关。
/// 每个用例都在 finally 里还原，跑完设备必须回到干净态。
/// </summary>
public sealed class RealMobileWriteFactAttribute : FactAttribute
{
    public RealMobileWriteFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ASTRAL_TEST_MOBILE_WRITE") != "1")
            Skip = "设置 ASTRAL_TEST_MOBILE_WRITE=1 才会改动设备上的游戏代码包（用例内会自动还原）";
    }
}

public class MobileDeviceWriteTests
{
    private readonly ITestOutputHelper _output;

    public MobileDeviceWriteTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [RealMobileWriteFact]
    public Task Ios_PatchThenRestore_RoundTripsOnDevice() => PatchThenRestoreAsync("ios");

    [RealMobileWriteFact]
    public Task Android_PatchThenRestore_RoundTripsOnDevice() => PatchThenRestoreAsync("android");

    [RealMobileWriteFact]
    public Task Ios_DeployPayloadThenRemove() => DeployPayloadRoundTripAsync("ios");

    [RealMobileWriteFact]
    public Task Android_DeployPayloadThenRemove() => DeployPayloadRoundTripAsync("android");

    [RealMobileWriteFact]
    public Task Ios_ModListAndToggle_RoundTrips() => ModListRoundTripAsync("ios");

    [RealMobileWriteFact]
    public Task Android_ModListAndToggle_RoundTrips() => ModListRoundTripAsync("android");

    [RealMobileWriteFact]
    public Task Ios_ModConfig_RoundTrips() => ModConfigRoundTripAsync("ios");

    [RealMobileWriteFact]
    public Task Android_ModConfig_RoundTrips() => ModConfigRoundTripAsync("android");

    [RealMobileWriteFact]
    public Task Ios_ImportModPackage_RoundTrips() => ImportModPackageAsync("ios");

    [RealMobileWriteFact]
    public Task Android_ImportModPackage_RoundTrips() => ImportModPackageAsync("android");

    [RealMobileWriteFact]
    public Task Ios_SkinProfile_RoundTrips() => SkinProfileRoundTripAsync("ios");

    [RealMobileWriteFact]
    public Task Android_SkinProfile_RoundTrips() => SkinProfileRoundTripAsync("android");

    /// <summary>
    /// M5：往设备上导入一个卡面方案、激活、删除，并把 <c>CardSkinMod/config.json</c> 恢复原样。
    /// 设备上没有 <c>CardSkinMod</c> 时只验证「导入能落盘 + 激活会给出可读原因」。
    /// </summary>
    private async Task SkinProfileRoundTripAsync(string kind)
    {
        var output = _output;
        using var sandbox = new TestSandbox($"ap-mobile-skin-{kind}");

        IDeviceChannel channel = kind == "ios" ? new IosChannel() : new AndroidChannel();
        var targets = new MobileTargetStore(sandbox.Root);
        var devices = new MobileDeviceService(sandbox.Root, [channel]);
        await devices.DiscoverAsync();
        var target = devices.GetTargets().FirstOrDefault(t => t.Kind == kind)
                     ?? throw new InvalidOperationException($"没有 {kind} 设备");
        if (kind == "android") target.AppId = "com.feimo.astralparty.haoyou";
        targets.Upsert(target);

        var skins = new MobileSkinService(channel);

        const string profile = "ApSkinTestProfile";
        var zip = Path.Combine(sandbox.Root, "skin-package.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            foreach (var name in new[] { "UT_HandCard_1.png", "UT_HandCard_2.png" })
            {
                var entry = archive.CreateEntry($"{profile}/{name}");
                using var stream = entry.Open();
                stream.Write([1, 2, 3, 4]);
            }
        }

        // 注意：`?? throw` 之后 opened 已经是元组本身，不是 Nullable，不能再点 .Value
        var (fs, root, _) = await MobileDeviceAccess.TryOpenRootAsync(channel, target, null)
                            ?? throw new InvalidOperationException("找不到游戏容器");
        var loaderRoot = root + MobilePaths.LoaderRelative;
        var profileRoot = $"{loaderRoot}/skins/{profile}";
        var configPath = $"{loaderRoot}/mods/CardSkinMod/config.json";

        byte[]? configBackup = null;
        var configExisted = false;
        await using (fs)
        {
            configExisted = await fs.ExistsAsync(configPath);
            if (configExisted)
                configBackup = await fs.ReadAllBytesAsync(configPath);
        }

        var before = await skins.GetStateAsync(target);
        output.WriteLine($"{kind} 导入前：{before.Profiles.Count} 个方案，CardSkinMod={before.CardSkinModPresent}，" +
                         $"当前生效={before.ActiveSkin}，有默认池={before.HasDefaultPool}");
        foreach (var issue in before.Issues) output.WriteLine("  · " + issue);
        Assert.DoesNotContain(before.Profiles, p => p.Name == profile);

        try
        {
            var imported = await skins.ImportAsync(target, zip, activate: true);
            output.WriteLine($"导入结果：ok={imported.Ok} {imported.Message} {imported.Detail}");

            if (!imported.Ok && imported.Message.Contains("2750"))
            {
                // 游戏先跑过一次：CesiumLoader/ 归游戏且组无写位，shell 建不了新目录，也 chmod/chown 不了
                //（实测都是 Operation not permitted）。这是设备状态问题，不是本用例要验证的逻辑；
                // 但工具必须给出能照做的说明，而不是一句 mkdir: Permission denied。
                Assert.Contains("清除", imported.Message);
                Assert.False((await skins.GetStateAsync(target)).Profiles.Any(p => p.Name == profile));
                output.WriteLine("设备上的加载器目录由游戏创建（2750），本次只验证到「导入被明确拒绝并给出出路」");
                return;
            }

            var afterImport = await skins.GetStateAsync(target);
            var scheme = afterImport.Profiles.FirstOrDefault(p => p.Name == profile);
            Assert.NotNull(scheme);
            output.WriteLine($"导入后：方案 {scheme!.Name} 有 {scheme.CardCount} 张卡面，生效={afterImport.ActiveSkin}");
            Assert.Equal(2, scheme.CardCount);

            if (!before.CardSkinModPresent)
            {
                // 没有 CardSkinMod 时不许假装成功：方案落了盘，但激活必须说清原因
                Assert.False(imported.Ok, "设备上没有 CardSkinMod，激活不应该报成功");
                output.WriteLine("设备上没有 CardSkinMod，只验证到「导入落盘 + 激活有可读原因」");
                return;
            }

            Assert.True(imported.Ok, "导入并激活失败：" + imported.Message);
            Assert.Equal(profile, afterImport.ActiveSkin);
            Assert.True(scheme.Active, "方案应处于生效态");

            var backToDefault = await skins.SetActiveAsync(target, MobileSkinService.GameDefault);
            Assert.True(backToDefault.Ok, "切回游戏默认失败：" + backToDefault.Message);
            Assert.Equal(MobileSkinService.GameDefault, (await skins.GetStateAsync(target)).ActiveSkin);

            // 生效中的方案拒绝删除：先切回默认了，这次应该成功
            var deleted = await skins.DeleteAsync(target, profile);
            Assert.True(deleted.Ok, "删除方案失败：" + deleted.Message);
            Assert.False((await skins.GetStateAsync(target)).Profiles.Any(p => p.Name == profile));
        }
        finally
        {
            // 设备必须回到干净态：删掉测试方案，并把 config.json 恢复成导入前的样子（原来没有就删掉）
            try
            {
                if (await MobileDeviceAccess.TryOpenRootAsync(channel, target, null) is not { } reopened)
                    throw new InvalidOperationException("清理时找不到游戏容器");
                var (cleanup, _, _) = reopened;
                await using (cleanup)
                {
                    if (await cleanup.ExistsAsync(profileRoot))
                        await cleanup.DeleteAsync(profileRoot);
                    if (configExisted && configBackup is not null)
                        await cleanup.WriteAllBytesAsync(configPath, configBackup);
                    else if (!configExisted && await cleanup.ExistsAsync(configPath))
                        await cleanup.DeleteAsync(configPath);
                    output.WriteLine($"清理后：方案存在={await cleanup.ExistsAsync(profileRoot)}，" +
                                     $"config.json 存在={await cleanup.ExistsAsync(configPath)}");
                }
            }
            catch (Exception ex)
            {
                output.WriteLine("清理失败（设备可能留下测试方案）：" + ex);
            }
        }
    }

    private async Task ImportModPackageAsync(string kind)
    {
        var output = _output;
        using var sandbox = new TestSandbox($"ap-mobile-import-{kind}");

        IDeviceChannel channel = kind == "ios" ? new IosChannel() : new AndroidChannel();
        var targets = new MobileTargetStore(sandbox.Root);
        var installs = new MobileInstallStore(sandbox.Root);
        var devices = new MobileDeviceService(sandbox.Root, [channel]);
        await devices.DiscoverAsync();
        var target = devices.GetTargets().FirstOrDefault(t => t.Kind == kind)
                     ?? throw new InvalidOperationException($"没有 {kind} 设备");
        if (kind == "android") target.AppId = "com.feimo.astralparty.haoyou";
        targets.Upsert(target);

        var repo = Path.GetFullPath(Path.Combine(TestPaths.AppSourceDirectory, "..", ".."));
        var install = new MobileInstallService(channel, installs, targets, Path.Combine(
            repo, "src", "CesiumLoader.Mobile.Patcher", "bin", "Release", "net8.0", "CesiumLoader.Mobile.Patcher.dll"));

        // 用内嵌载荷里的一个模组打一个本地包
        var modId = "AudioSkinMod";
        var zip = Path.Combine(sandbox.Root, "mod-package.zip");
        using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            foreach (var entry in DesktopModEntries(modId))
            {
                var modEntry = archive.CreateEntry($"{modId}/{entry.FullName.Split('/')[^1]}");
                using var destination = modEntry.Open();
                using var source = entry.Open();
                source.CopyTo(destination);
            }
            var config = archive.CreateEntry($"{modId}/config.json");
            using var writer = new StreamWriter(config.Open());
            writer.Write("{\n  // 导入前的玩家配置\n  \"enabled\": true\n}\n");
        }

        var root = channel.BuildRoot(targets.LoadTargets().First(t => t.Id == target.Id));
        try
        {
            var imported = await install.ImportModPackageAsync(target, zip);
            Assert.True(imported.Ok, "导入失败：" + imported.Message);
            output.WriteLine($"{kind} 导入结果：{imported.Message}　{imported.Detail}");

            var mods = await install.ListModsAsync(target);
            var mod = Assert.Single(mods);
            Assert.Equal(modId, mod.Id);
            Assert.True(mod.Enabled);
            Assert.True(mod.HasConfig, "导入后应有 config.json");

            // 改一个配置项，再导入一次：包里的 config.json 不能覆盖玩家配置
            var (_, fields, _) = await install.ReadModConfigAsync(target, modId, "zh-Hans");
            var scalar = fields.First(f => f.Kind is "bool" or "number" or "string");
            var next = scalar.Kind switch
            {
                "bool" => scalar.BoolValue ? "false" : "true",
                "number" => (scalar.NumberValue + 7).ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => scalar.StringValue + "-kept",
            };
            var write = await install.WriteModConfigValuesAsync(target, modId, [(scalar.Name, scalar.Kind, next)]);
            Assert.True(write.Ok, write.Message);

            var again = await install.ImportModPackageAsync(target, zip);
            Assert.True(again.Ok, again.Message);
            var (_, after, _) = await install.ReadModConfigAsync(target, modId, "zh-Hans");
            var kept = after.First(f => f.Name == scalar.Name);
            var actual = kept.Kind switch
            {
                "bool" => kept.BoolValue ? "true" : "false",
                "number" => kept.NumberValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => kept.StringValue,
            };
            Assert.Equal(next, actual);
            output.WriteLine("导入 / 重复导入 / 保留玩家配置 全部通过");
        }
        finally
        {
            await using var fileSystem = await channel.OpenAsync(targets.LoadTargets().First(t => t.Id == target.Id));
            await fileSystem.DeleteAsync(root + MobilePaths.LoaderRelative);
        }
    }

    private async Task ModConfigRoundTripAsync(string kind)
    {
        var output = _output;
        using var sandbox = new TestSandbox($"ap-mobile-config-{kind}");

        IDeviceChannel channel = kind == "ios" ? new IosChannel() : new AndroidChannel();
        var targets = new MobileTargetStore(sandbox.Root);
        var installs = new MobileInstallStore(sandbox.Root);
        var devices = new MobileDeviceService(sandbox.Root, [channel]);
        await devices.DiscoverAsync();

        var target = devices.GetTargets().FirstOrDefault(t => t.Kind == kind)
                     ?? throw new InvalidOperationException($"没有 {kind} 设备");
        if (kind == "android") target.AppId = "com.feimo.astralparty.haoyou";
        targets.Upsert(target);

        // 载荷里挑一个自带 config.json 的模组（AudioSkinMod 有标量字段）
        var modId = "AudioSkinMod";
        var payload = BuildTestPayload(sandbox, modId, withConfig: true);

        var repo = Path.GetFullPath(Path.Combine(TestPaths.AppSourceDirectory, "..", ".."));
        var install = new MobileInstallService(channel, installs, targets, Path.Combine(
            repo, "src", "CesiumLoader.Mobile.Patcher", "bin", "Release", "net8.0", "CesiumLoader.Mobile.Patcher.dll"));

        var root = channel.BuildRoot(targets.LoadTargets().First(t => t.Id == target.Id));
        try
        {
            var deploy = await install.DeployPayloadAsync(target, payload);
            Assert.True(deploy.Ok, "投放失败：" + deploy.Message);

            var (text, fields, path) = await install.ReadModConfigAsync(target, modId, "zh-Hans");
            output.WriteLine($"{kind} 读到 {path}，{fields.Count} 个字段");
            Assert.NotEmpty(text);
            Assert.True(fields.Count > 0, "config.json 里没有字段");

            var scalar = fields.FirstOrDefault(f => f.Kind is "bool" or "number" or "string");
            Assert.NotNull(scalar);
            var next = scalar!.Kind switch
            {
                "bool" => scalar.BoolValue ? "false" : "true",
                "number" => (scalar.NumberValue + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => scalar.StringValue + "x",
            };
            output.WriteLine($"改 {scalar.Name}（{scalar.Kind}）：{scalar.StringValue}{(scalar.Kind == "bool" ? scalar.BoolValue.ToString() : "")} → {next}");

            var write = await install.WriteModConfigValuesAsync(target, modId,
                [(scalar.Name, scalar.Kind, next)]);
            Assert.True(write.Ok, "写配置失败：" + write.Message);

            var (afterText, afterFields, _) = await install.ReadModConfigAsync(target, modId, "zh-Hans");
            var changed = afterFields.First(f => f.Name == scalar.Name);
            var actual = changed.Kind switch
            {
                "bool" => changed.BoolValue ? "true" : "false",
                "number" => changed.NumberValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => changed.StringValue,
            };
            Assert.Equal(next, actual);

            // 文本定点替换：注释与空行不能被吃掉
            Assert.Equal(text.Count(c => c == '\n'), afterText.Count(c => c == '\n'));
            if (text.Contains("//")) Assert.Contains("//", afterText);
            output.WriteLine("读配置 / 定点改值 / 注释保留 全部通过");
        }
        finally
        {
            await using var fileSystem = await channel.OpenAsync(targets.LoadTargets().First(t => t.Id == target.Id));
            await fileSystem.DeleteAsync(root + MobilePaths.LoaderRelative);
        }
    }

    private async Task ModListRoundTripAsync(string kind)
    {
        var output = _output;
        using var sandbox = new TestSandbox($"ap-mobile-mods-{kind}");

        IDeviceChannel channel = kind == "ios" ? new IosChannel() : new AndroidChannel();
        var targets = new MobileTargetStore(sandbox.Root);
        var installs = new MobileInstallStore(sandbox.Root);
        var devices = new MobileDeviceService(sandbox.Root, [channel]);
        await devices.DiscoverAsync();

        var target = devices.GetTargets().FirstOrDefault(t => t.Kind == kind)
                     ?? throw new InvalidOperationException($"没有 {kind} 设备");
        if (kind == "android") target.AppId = "com.feimo.astralparty.haoyou";
        targets.Upsert(target);

        // 载荷里只要引导 + SDK + 一个模组，就能验证「列模组 / 启停」这条链。
        var modId = "ActivityLogMod";
        var payload = BuildTestPayload(sandbox, modId, withConfig: false);

        var repo = Path.GetFullPath(Path.Combine(TestPaths.AppSourceDirectory, "..", ".."));
        var install = new MobileInstallService(channel, installs, targets, Path.Combine(
            repo, "src", "CesiumLoader.Mobile.Patcher", "bin", "Release", "net8.0", "CesiumLoader.Mobile.Patcher.dll"));

        var root = channel.BuildRoot(targets.LoadTargets().First(t => t.Id == target.Id));
        try
        {
            var deploy = await install.DeployPayloadAsync(target, payload);
            Assert.True(deploy.Ok, "投放失败：" + deploy.Message);

            var mods = await install.ListModsAsync(target);
            output.WriteLine($"{kind} 模组 {mods.Count} 个：" + string.Join("、",
                mods.Select(m => $"{m.Id}{(m.Enabled ? "✓" : "✗")} {m.Name} v{m.Version}")));
            var mod = Assert.Single(mods);
            Assert.Equal(modId, mod.Id);
            Assert.True(mod.Enabled);
            Assert.Equal(MobilePaths.ModsRelative, mod.Directory);
            Assert.False(string.IsNullOrEmpty(mod.Name), "没从 sidecar 读到模组名");

            var off = await install.SetModEnabledAsync(target, modId, enabled: false);
            Assert.True(off.Ok, "停用失败：" + off.Message);
            var disabled = Assert.Single(await install.ListModsAsync(target));
            Assert.False(disabled.Enabled);
            Assert.Equal(MobilePaths.DisabledModsRelative, disabled.Directory);

            var on = await install.SetModEnabledAsync(target, modId, enabled: true);
            Assert.True(on.Ok, "重新启用失败：" + on.Message);
            var reenabled = Assert.Single(await install.ListModsAsync(target));
            Assert.True(reenabled.Enabled);
            Assert.Equal(MobilePaths.ModsRelative, reenabled.Directory);
            output.WriteLine("列模组 / 停用 / 重新启用 全部通过");
        }
        finally
        {
            await using var fileSystem = await channel.OpenAsync(targets.LoadTargets().First(t => t.Id == target.Id));
            await fileSystem.DeleteAsync(root + MobilePaths.LoaderRelative);
        }

        var cleaned = await install.ListModsAsync(target);
        Assert.Empty(cleaned);
    }

    /// <summary>拼一份测试用载荷包：托管的运行时三件套 + 一个模组（模组文件取自桌面发布包，只用于目录与配置解析）。</summary>
    private static string BuildTestPayload(TestSandbox sandbox, string modId, bool withConfig)
    {
        var managed = MobilePayloadProvisioner.Ensure(sandbox.Root);
        var zip = sandbox.Combine("payload-test.zip");
        using var output = ZipFile.Open(zip, ZipArchiveMode.Create);
        using (var source = ZipFile.OpenRead(managed))
            foreach (var entry in source.Entries)
                CopyEntry(entry, output, entry.FullName);

        foreach (var entry in DesktopModEntries(modId))
            CopyEntry(entry, output, $"mods/{modId}/{entry.FullName.Split('/')[^1]}");

        if (withConfig)
        {
            var config = output.CreateEntry($"mods/{modId}/config.json");
            using var writer = new StreamWriter(config.Open());
            writer.Write("{\n  // 测试用配置\n  \"enabled\": true,\n  \"speed\": 1.0\n}\n");
        }

        return zip;
    }

    private static List<ZipArchiveEntry> DesktopModEntries(string modId)
    {
        var desktop = Path.Combine(TestPaths.AppSourceDirectory, "Resources", "ModLoader", "cesium-loader.zip");
        var archive = ZipFile.OpenRead(desktop);
        return archive.Entries
            .Where(e => e.FullName.StartsWith($"AstralParty_ModLoader/mods/{modId}/", StringComparison.Ordinal) &&
                        !e.FullName.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static void CopyEntry(ZipArchiveEntry source, ZipArchive output, string name)
    {
        var target = output.CreateEntry(name);
        using var input = source.Open();
        using var destination = target.Open();
        input.CopyTo(destination);
    }
    private async Task DeployPayloadRoundTripAsync(string kind)
    {
        var output = _output;
        using var sandbox = new TestSandbox($"ap-mobile-payload-{kind}");

        IDeviceChannel channel = kind == "ios" ? new IosChannel() : new AndroidChannel();
        var targets = new MobileTargetStore(sandbox.Root);
        var installs = new MobileInstallStore(sandbox.Root);
        var devices = new MobileDeviceService(sandbox.Root, [channel]);
        await devices.DiscoverAsync();

        var target = devices.GetTargets().FirstOrDefault(t => t.Kind == kind)
                     ?? throw new InvalidOperationException($"没有 {kind} 设备");
        if (kind == "android") target.AppId = "com.feimo.astralparty.haoyou";
        targets.Upsert(target);

        var repo = Path.GetFullPath(Path.Combine(TestPaths.AppSourceDirectory, "..", ".."));
        // 载荷与线上同源：内嵌的托管包（CI 按安卓仓库 version.json → packages.loaderManaged 同步）
        var payload = MobilePayloadProvisioner.Ensure(sandbox.Root);

        var patcher = Path.Combine(repo, "src", "CesiumLoader.Mobile.Patcher", "bin", "Release", "net8.0",
            "CesiumLoader.Mobile.Patcher.dll");
        var install = new MobileInstallService(channel, installs, targets, patcher);

        var before = await devices.DetectAsync(target.Id);
        output.WriteLine($"投放前：包名={before.BundleId} loaderPresent={before.LoaderPresent} " +
                         $"loaderVersion={before.LoaderVersion} mods={before.ModCount}");
        Assert.False(before.LoaderPresent, "设备上已经有 CesiumLoader 目录，先清理再跑本用例");

        var root = channel.BuildRoot(targets.LoadTargets().First(t => t.Id == target.Id));
        MobileOperationResult? result = null;
        var cleaned = false;
        try
        {
            result = await install.DeployPayloadAsync(target, payload,
                (message, percent) => output.WriteLine($"  [{percent}%] {message}"));
            Assert.True(result.Ok, $"投放失败：{result.Message}");
            output.WriteLine($"投放结果：{result.Message}　{result.Detail}");

            var after = await devices.DetectAsync(target.Id);
            output.WriteLine($"投放后：loaderPresent={after.LoaderPresent} loaderVersion={after.LoaderVersion} " +
                             $"sdkVersion={after.SdkVersion} bootstrap={after.BootstrapAssembly}");
            Assert.True(after.LoaderPresent, "投放后检测不到加载器目录");
            Assert.False(string.IsNullOrEmpty(after.LoaderVersion), "投放后读不到加载器版本");
        }
        finally
        {
            await using var fileSystem = await channel.OpenAsync(targets.LoadTargets().First(t => t.Id == target.Id));
            await fileSystem.DeleteAsync(root + MobilePaths.LoaderRelative);
            cleaned = !await fileSystem.ExistsAsync(root + MobilePaths.LoaderRelative, default);
        }

        Assert.True(cleaned, "清理后不该还有 CesiumLoader 目录");
        var cleanedReport = await devices.DetectAsync(target.Id);
        output.WriteLine($"清理后：loaderPresent={cleanedReport.LoaderPresent}");
        Assert.False(cleanedReport.LoaderPresent, "清理后检测仍报告加载器存在");
    }

    private async Task PatchThenRestoreAsync(string kind)
    {
        var output = _output;
        using var sandbox = new TestSandbox($"ap-mobile-write-{kind}");

        IDeviceChannel channel = kind == "ios" ? new IosChannel() : new AndroidChannel();
        var targets = new MobileTargetStore(sandbox.Root);
        var installs = new MobileInstallStore(sandbox.Root);
        var devices = new MobileDeviceService(sandbox.Root, [channel]);
        await devices.DiscoverAsync();

        var target = devices.GetTargets().FirstOrDefault(t => t.Kind == kind)
                     ?? throw new InvalidOperationException($"没有 {kind} 设备");
        // 安卓侧固定测 haoyou 那个包（本机三个包名都装着，自动识别会先命中 com.feimo.astralparty）。
        if (kind == "android") target.AppId = "com.feimo.astralparty.haoyou";
        targets.Upsert(target);
        output.WriteLine($"{kind}: target={target.Id} appId={(string.IsNullOrEmpty(target.AppId) ? "(自动识别)" : target.AppId)}");

        var repo = Path.GetFullPath(Path.Combine(TestPaths.AppSourceDirectory, "..", ".."));
        var patcher = Path.Combine(repo, "src", "CesiumLoader.Mobile.Patcher", "bin", "Release", "net8.0",
            "CesiumLoader.Mobile.Patcher.dll");
        Assert.True(File.Exists(patcher), $"先构建注入器：dotnet build src/CesiumLoader.Mobile.Patcher -c Release");
        var install = new MobileInstallService(channel, installs, targets, patcher);

        var before = await devices.DetectAsync(target.Id, (m, p) => output.WriteLine($"  [{p}%] {m}"));
        output.WriteLine($"注入前：state={before.PatchState} inner={before.CodeBundleHash} sha={Short(before.BundleSha256)} " +
                         $"bundle={before.BundleBytes} B assets={before.AssetCount}");
        Assert.False(string.IsNullOrEmpty(before.CodeBundleHash), "没定位到代码包");
        if (before.PatchState != PatchStates.Clean)
        {
            Assert.Fail($"设备当前不是干净态（state={before.PatchState}）。请先手动还原再跑本用例，" +
                        $"避免在没有原始备份的情况下覆盖。");
        }

        var originalSha = before.BundleSha256;
        MobileOperationResult? restore = null;
        try
        {
            var patch = await install.PatchAsync(target, (m, p) => output.WriteLine($"  [{p}%] {m}"));
            Assert.True(patch.Ok, $"注入失败：{patch.Message}");

            var patched = await devices.DetectAsync(target.Id);
            output.WriteLine($"注入后：state={patched.PatchState} sha={Short(patched.BundleSha256)} " +
                             $"bundle={patched.BundleBytes} B（原 {before.BundleBytes} B）" +
                             $" legacy={patched.LegacyMarker} current={patched.CurrentMarker} issues={patched.Issues.Count}");
            Assert.True(patched.Injected, "回读的代码包里没有注入标记");
            Assert.Equal(PatchStates.Patched, patched.PatchState);
            Assert.NotEqual(originalSha, patched.BundleSha256);
        }
        finally
        {
            // 只有确实留下备份时才还原：RestoreAsync 在没有备份时会删掉缓存条目让游戏重下，
            // 那对「其实没改成」的情况反而是破坏性的。
            var hasBackup = Directory.Exists(installs.BackupDirectory) &&
                            Directory.EnumerateFiles(installs.BackupDirectory, "*.data").Any();
            if (hasBackup)
            {
                try
                {
                    restore = await install.RestoreAsync(target, removeLoader: false);
                }
                catch (Exception ex)
                {
                    output.WriteLine("还原抛异常：" + ex);
                }
            }
            else
            {
                output.WriteLine("没有备份文件，跳过还原（注入没有走到写回那一步）");
            }
        }

        Assert.NotNull(restore);
        Assert.True(restore!.Ok, $"还原失败：{restore.Message}");

        var restored = await devices.DetectAsync(target.Id);
        output.WriteLine($"还原后：state={restored.PatchState} sha={Short(restored.BundleSha256)}");
        Assert.Equal(PatchStates.Clean, restored.PatchState);
        Assert.Equal(originalSha, restored.BundleSha256);
    }

    private static string Short(string sha) => sha.Length >= 16 ? sha[..16] : sha;
}
