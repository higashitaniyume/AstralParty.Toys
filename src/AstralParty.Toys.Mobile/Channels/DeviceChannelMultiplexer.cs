using AstralParty.Toys.Mobile.Models;

namespace AstralParty.Toys.Mobile.Channels;

/// <summary>
/// 按目标类型把调用转给真正的那条通道（ios / android）。
///
/// 安装、投放、模组、卡面这几个服务拿到的是**一个** <see cref="IDeviceChannel"/>，早前接的是
/// <c>IosChannel</c>；安卓目标走过去会得到「house_arrest 失败」这种看不懂的报错，
/// 而且检测页能列出设备、写操作却全走 iOS 通道。这里集中分派，免得每个服务各自判断。
/// </summary>
public sealed class DeviceChannelMultiplexer : IDeviceChannel
{
    private readonly Dictionary<string, IDeviceChannel> _byKind;

    public DeviceChannelMultiplexer(IEnumerable<IDeviceChannel> channels)
    {
        _byKind = channels.ToDictionary(c => c.Kind, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>通道本身不是一种设备，这个值不会被当成 <c>MobileTarget.Kind</c> 使用。</summary>
    public string Kind => "multiplex";

    public IReadOnlyCollection<string> Kinds => _byKind.Keys;

    private IDeviceChannel Resolve(string kind)
    {
        if (_byKind.TryGetValue(kind, out var channel))
            return channel;
        throw new InvalidOperationException($"不支持的设备通道：{kind}（可用：{string.Join("、", _byKind.Keys)}）");
    }

    public string BuildRoot(MobileTarget target) => Resolve(target.Kind).BuildRoot(target);

    public async Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<DeviceInfo>();
        foreach (var channel in _byKind.Values)
        {
            try
            {
                all.AddRange(await channel.DiscoverAsync(cancellationToken));
            }
            catch (Exception)
            {
                // 一条通道不可用（没装 adb / 没有设备）不影响另一条
            }
        }

        return all;
    }

    public async Task<IReadOnlyList<MobilePackageCandidate>> FindGamePackagesAsync(
        string serial, CancellationToken cancellationToken = default)
    {
        foreach (var channel in _byKind.Values)
        {
            try
            {
                var found = await channel.FindGamePackagesAsync(serial, cancellationToken);
                if (found.Count > 0)
                    return found;
            }
            catch (Exception)
            {
                // 这个串不属于这条通道，换下一条
            }
        }

        return [];
    }

    public Task<IDeviceFileSystem> OpenAsync(MobileTarget target, CancellationToken cancellationToken = default) =>
        Resolve(target.Kind).OpenAsync(target, cancellationToken);
}
