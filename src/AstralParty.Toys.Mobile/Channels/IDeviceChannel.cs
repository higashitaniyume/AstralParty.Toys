using AstralParty.Toys.Mobile.Models;

namespace AstralParty.Toys.Mobile.Channels;

/// <summary>
/// Surface shared by the iOS (house_arrest/AFC) and Android (adb) channels. Reads, writes,
/// directory creation and rename are all provided; both channels implement them on top of the
/// primitives each device actually offers (AFC packets, adb shell).
/// </summary>
public interface IDeviceChannel
{
    string Kind { get; }

    /// <summary>Container path prefix for a resolved target (iOS: "Documents/", Android: the app's files dir).</summary>
    string BuildRoot(MobileTarget target);

    Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 扫出这台设备上所有「可能是这款游戏」的包（渠道服很多，不能只认一份写死的包名清单）。
    /// 调用方据此让玩家把某一个设成当前游戏。
    /// </summary>
    Task<IReadOnlyList<MobilePackageCandidate>> FindGamePackagesAsync(string serial,
        CancellationToken cancellationToken = default);

    Task<IDeviceFileSystem> OpenAsync(MobileTarget target, CancellationToken cancellationToken = default);
}

public interface IDeviceFileSystem : IAsyncDisposable
{
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

    Task<DeviceFileInfo?> StatAsync(string path, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken cancellationToken = default);

    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);

    Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default);

    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    Task MakeDirectoriesAsync(string path, CancellationToken cancellationToken = default);

    Task RenameAsync(string from, string to, CancellationToken cancellationToken = default);

    /// <summary>Finds the outer bundle-directory name holding <paramref name="inner"/> under a bundles root.</summary>
    Task<string?> FindBundleOuterAsync(string bundlesRoot, string inner, CancellationToken cancellationToken = default)
    {
        var outers = ListAsync(bundlesRoot, cancellationToken);
        // Kept async so implementations can override with a single device-side search.
        return FindAsync(outers, inner, bundlesRoot, cancellationToken);
    }

    private async Task<string?> FindAsync(
        Task<IReadOnlyList<string>> outersTask, string inner, string bundlesRoot, CancellationToken cancellationToken)
    {
        var outers = await outersTask;
        foreach (var outer in outers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await ExistsAsync($"{bundlesRoot.TrimEnd('/')}/{outer}/{inner}/__data", cancellationToken))
                return outer;
        }

        return null;
    }
    /// <summary>Lists one directory level with entry metadata (folder flag, size, modified).</summary>
    async Task<IReadOnlyList<DeviceDirectoryEntry>> ListDetailedAsync(
        string path, bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        var names = await ListAsync(path, cancellationToken);
        var entries = new List<DeviceDirectoryEntry>();
        var prefix = path.TrimEnd('/');
        foreach (var name in names)
        {
            if (name is "." or ".." || (!includeHidden && name.StartsWith('.')))
                continue;
            cancellationToken.ThrowIfCancellationRequested();
            var full = string.IsNullOrEmpty(prefix) ? name : $"{prefix}/{name}";
            DeviceFileInfo? stat = null;
            try
            {
                stat = await StatAsync(full, cancellationToken);
            }
            catch
            {
                // Some restricted files refuse stat; still return entry cleanly.
            }
            entries.Add(new DeviceDirectoryEntry(
                name,
                stat?.IsDirectory ?? false,
                stat?.Size ?? 0,
                stat?.Modified));
        }

        return entries;
    }
}

public sealed record DeviceFileInfo(long Size, DateTimeOffset? Modified, bool IsDirectory = false);

public sealed record DeviceDirectoryEntry(string Name, bool IsDirectory, long Size, DateTimeOffset? Modified);
