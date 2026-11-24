using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Logging;
using AstralParty.Toys.Mobile.Models;

namespace AstralParty.Toys.Mobile.Services;

/// <summary>
/// 「找到这台设备上当前这个游戏包的容器根」这件事，检测、投放、模组、卡面、市场都要做一遍。
/// 之前每个服务各写一份候选试探循环，改一处容易漏另外几处，集中到这里。
/// </summary>
public static class MobileDeviceAccess
{
    /// <summary>给设备文件系统套上日志；把「读了哪个文件」这件事记全。</summary>
    public static IDeviceFileSystem Wrap(IDeviceFileSystem fileSystem, MobileOperationLog? log) =>
        log is null ? fileSystem : new LoggingFileSystem(fileSystem, log);

    /// <summary>
    /// 按候选包名逐个试探，返回第一个「有 Addressables 容器」的包。
    /// 调用方负责 Dispose 返回的文件系统（失败时这里已经 Disposed）。
    /// </summary>
    public static async Task<(IDeviceFileSystem FileSystem, string Root, string AppId)?> TryOpenRootAsync(
        IDeviceChannel channel,
        MobileTarget target,
        MobileOperationLog? log,
        CancellationToken cancellationToken = default)
    {
        string[] candidates = string.IsNullOrEmpty(target.AppId)
            ? target.Kind == "ios" ? MobilePaths.IosCandidateIds : MobilePaths.AndroidCandidateIds
            : [target.AppId];

        foreach (var candidate in candidates)
        {
            var candidateTarget = new MobileTarget { Kind = target.Kind, Serial = target.Serial, AppId = candidate };
            try
            {
                log?.Info($"试探游戏包 {candidate}");
                var probe = Wrap(await channel.OpenAsync(candidateTarget, cancellationToken), log);
                var root = channel.BuildRoot(candidateTarget);
                if (await probe.ExistsAsync(root + MobilePaths.AddressablesRelative, cancellationToken))
                    return (probe, root, candidate);
                await probe.DisposeAsync();
            }
            catch (Exception ex)
            {
                log?.Info($"候选 {candidate} 不可用：{ex.GetType().Name}: {ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    /// 加载器目录是不是**游戏进程**建出来的（组无写位）。是的话，任何「新建文件 / 新建目录 / 删除目录」
    /// 都会失败，而「覆盖已有文件」还能成功 —— 失败看起来就会像随机现象。
    ///
    /// 实测（2026-11-24，M2007J17C / Android 13）：游戏先跑过一次后，
    /// <c>files/CesiumLoader</c> 与它下面的 <c>mods</c>、<c>skins</c> 都是
    /// <c>drwxr-s--- u0_a191 ext_data_rw</c>（2750，组只有 r-x），里面的**文件**是 <c>-rw-rw----</c>。
    /// 于是 shell（uid 2000，在 <c>ext_data_rw</c> 组里）能覆盖已有文件，但
    /// <c>mkdir</c> / <c>touch</c> 报 Permission denied，而且 <c>chmod</c> 与 <c>chown</c>
    /// **都**报 Operation not permitted（不是 owner，这个挂载点也不允许）。
    /// 所以出路只有两条：先把目录建出来再让游戏跑，或者清除游戏数据重来。
    ///
    /// @return 不可写时返回一句可读的说明；可写或目录还不存在返回 <c>null</c>。
    /// </summary>
    public static async Task<string?> DescribeLoaderWritabilityAsync(
        IDeviceFileSystem fileSystem, string loaderRoot, CancellationToken cancellationToken = default)
    {
        if (!await fileSystem.ExistsAsync(loaderRoot, cancellationToken))
            return null;   // 还不存在：由调用方去建，建出来的目录是可写的

        var probe = loaderRoot + "/.aspatoys-write-probe";
        try
        {
            await fileSystem.WriteAllBytesAsync(probe, [], cancellationToken);
            if (await fileSystem.StatAsync(probe, cancellationToken) is null)
                return Explain(loaderRoot);
            await fileSystem.DeleteAsync(probe, cancellationToken);
            return null;
        }
        catch (Exception)
        {
            return Explain(loaderRoot);
        }
    }

    private static string Explain(string loaderRoot) =>
        $"加载器目录 {loaderRoot} 由游戏进程创建（模式 2750，ext_data_rw 组没有写位）：" +
        "新增文件、新建目录和删除都会被拒绝，而改写已有文件仍然成功。" +
        "Shizuku 身份既 chmod 不了也 chown 不了（实测都是 Operation not permitted），重试不会成功。" +
        "请在设备上清除该游戏的数据，或先在游戏内启动一次让加载器自己整理目录之后再操作。";
}
