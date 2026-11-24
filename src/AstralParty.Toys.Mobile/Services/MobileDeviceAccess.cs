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
}
