using System.Text.Json;
using Xunit.Abstractions;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Logging;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Services.Mobile;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>真机只读检测：设置 ASTRAL_TEST_MOBILE=1 后才会跑，平时自动跳过。</summary>
public sealed class RealMobileFactAttribute : FactAttribute
{
    public RealMobileFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ASTRAL_TEST_MOBILE") != "1")
            Skip = "设置 ASTRAL_TEST_MOBILE=1 并连接设备后运行真机检测";
    }
}

public class MobileRealDeviceTests
{
    private readonly ITestOutputHelper _output;

    public MobileRealDeviceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [RealMobileFact]
    public async Task Detect_OnConnectedDevices()
    {
        var output = _output;
        using var sandbox = new TestSandbox("ap-mobile-real");
        var service = new MobileDeviceService(sandbox.Root, [new IosChannel(), new AndroidChannel()]);

        var devices = await service.DiscoverAsync();
        output.WriteLine($"发现设备 {devices.Count} 台");
        foreach (var device in devices)
            output.WriteLine($"  {device.Kind} {device.Id} {device.Label} {device.Model} {device.OsVersion} paired={device.Paired}");
        Assert.NotEmpty(devices);

        foreach (var target in service.GetTargets().Where(t => t.Online).ToList())
        {
            output.WriteLine($"=== 检测 {target.Id} ===");
            var report = await service.DetectAsync(target.Id, (message, percent) =>
                output.WriteLine($"[{percent}%] {message}"));
            output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Assert.False(string.IsNullOrEmpty(report.BundleId), $"{target.Id} 未找到游戏包名");
            Assert.False(string.IsNullOrEmpty(report.CodeBundleHash), $"{target.Id} 未定位到代码包");
            Assert.True(report.BundleBytes > 0, $"{target.Id} 代码包为空");
            Assert.False(string.IsNullOrEmpty(report.BundleSha256), $"{target.Id} 未算出代码包哈希");
        }
    }

    [RealMobileFact]
    public async Task Detect_AndroidHaoyouPackage()
    {
        var output = _output;
        using var sandbox = new TestSandbox("ap-mobile-real-haoyou");
        var service = new MobileDeviceService(sandbox.Root, [new IosChannel(), new AndroidChannel()]);
        await service.DiscoverAsync();
        var target = service.GetTargets().FirstOrDefault(t => t.Kind == "android" && t.Online)
                     ?? throw new InvalidOperationException("没有在线安卓设备");
        target.AppId = "com.feimo.astralparty.haoyou";
        service.Store.Upsert(target);

        var report = await service.DetectAsync(target.Id, (message, percent) =>
            output.WriteLine($"[{percent}%] {message}"));
        output.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Assert.Equal("com.feimo.astralparty.haoyou", report.BundleId);
        Assert.False(string.IsNullOrEmpty(report.CodeBundleHash));
        Assert.True(report.BundleBytes > 0);
    }

    /// <summary>渠道服很多，设备上所有可能的游戏包都要能扫出来，交给玩家选定当前那个。</summary>
    [RealMobileFact]
    public async Task PackageScan_ListsEveryChannelBuild()
    {
        foreach (var kind in new[] { "ios", "android" })
        {
            IDeviceChannel channel = kind == "ios" ? new IosChannel() : new AndroidChannel();
            var devices = await channel.DiscoverAsync();
            if (devices.Count == 0)
            {
                _output.WriteLine($"{kind}: 没有在线设备，跳过");
                continue;
            }

            var packages = await channel.FindGamePackagesAsync(devices[0].Id);
            _output.WriteLine($"{kind} 扫到 {packages.Count} 个候选：");
            foreach (var package in packages)
            {
                _output.WriteLine($"  {package.AppId}  {package.Name}  {package.Version}  " +
                                  $"缓存={package.HasCache} 容器={package.ContainerAccessible} 命中={package.MatchedBy}");
            }

            Assert.NotEmpty(packages);
            Assert.Contains(packages, p => p.AppId.Contains("astralparty", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>检测过程中的每一步（读了哪个文件、多大、多久、哈希）都要落进日志文件。</summary>
    [RealMobileFact]
    public async Task Detect_WritesOperationLogWithFileReads()
    {
        using var sandbox = new TestSandbox("ap-mobile-log-real");
        var log = new MobileOperationLog(sandbox.Combine("logs"), "real-detect");
        var service = new MobileDeviceService(sandbox.Root, [new IosChannel(), new AndroidChannel()], log);

        await service.DiscoverAsync();
        var target = service.GetTargets().FirstOrDefault(t => t.Kind == "ios" && t.Online)
                     ?? service.GetTargets().First(t => t.Online);
        await service.DetectAsync(target.Id);

        Assert.True(File.Exists(log.FilePath), "日志文件没有生成");
        var text = File.ReadAllText(log.FilePath);
        _output.WriteLine($"日志 {text.Length} 字符，{log.Count} 条");
        _output.WriteLine(text.Length > 3000 ? text[..3000] : text);

        Assert.Contains("列目录", text);
        Assert.Contains("读取", text);
        Assert.Contains("catalog_", text);
        Assert.Contains("检测结果", text);
    }

    private static async Task<(AndroidChannel Channel, MobileTarget Target)> OpenAndroidAsync()
    {
        var channel = new AndroidChannel();
        var devices = await channel.DiscoverAsync();
        var device = devices.FirstOrDefault() ?? throw new InvalidOperationException("没有在线安卓设备");
        return (channel, new MobileTarget { Kind = "android", Serial = device.Id, AppId = "com.feimo.astralparty.haoyou" });
    }

    /// <summary>
    /// M7 写入通道：在设备上**游戏之外**的临时目录里跑一遍，只验证通道本身，不碰游戏数据。
    /// </summary>
    [RealMobileFact]
    public async Task AndroidWriteChannel_RoundTripsInScratchDirectory()
    {
        var output = _output;
        var (channel, target) = await OpenAndroidAsync();
        await using var fileSystem = await channel.OpenAsync(target);

        var dir = $"/sdcard/Download/cesium-probe-m7-{DateTime.Now:MMdd-HHmmss}";
        var file = dir + "/payload.bin";
        var renamed = dir + "/renamed.bin";

        // 载荷覆盖全部 256 个字节值：任何换行/回车/NUL 转换都会让回读对不上。
        var payload = new byte[64 * 1024];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)i;

        try
        {
            await fileSystem.MakeDirectoriesAsync(dir);
            Assert.True(await fileSystem.ExistsAsync(dir), "mkdir 之后目录应当存在");

            await fileSystem.WriteAllBytesAsync(file, payload);
            Assert.Equal(payload.Length, (await fileSystem.StatAsync(file))?.Size);
            Assert.Equal(payload, await fileSystem.ReadAllBytesAsync(file));

            await fileSystem.RenameAsync(file, renamed);
            Assert.False(await fileSystem.ExistsAsync(file));
            Assert.Equal(payload, await fileSystem.ReadAllBytesAsync(renamed));

            // 目标已存在时必须拒绝改名，否则 mv 会嵌一层目录
            await Assert.ThrowsAsync<IOException>(() => fileSystem.RenameAsync(renamed, dir));

            // 覆盖写：长度要跟着变，不能留旧尾巴
            var shorter = payload[..100];
            await fileSystem.WriteAllBytesAsync(renamed, shorter);
            Assert.Equal(shorter, await fileSystem.ReadAllBytesAsync(renamed));
        }
        finally
        {
            await fileSystem.DeleteAsync(dir);
        }

        Assert.False(await fileSystem.ExistsAsync(dir), "清理后不应有残留");
        output.WriteLine("建目录 / 二进制往返 / 改名 / 拒绝嵌套 / 覆盖写 / 删除 / 残留检查 全部通过");
    }

    /// <summary>M7 前置条件：adb 的 shell 身份要能写游戏私有目录（普通应用身份不行，shell 在 ext_data_rw 组里）。</summary>
    [RealMobileFact]
    public async Task AndroidGameFilesDirectory_IsWritableByShellIdentity()
    {
        var (channel, target) = await OpenAndroidAsync();
        var root = channel.BuildRoot(target);
        Assert.True(await channel.CanWriteAsync(target, root),
            $"shell 身份写不了 {root}：该机型上安卓侧写入需要 Shizuku / root");
        _output.WriteLine($"{root} 可写（无需 Shizuku）");
    }
}
