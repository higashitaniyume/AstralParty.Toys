using System.Text;
using AstralParty.Toys.Mobile.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Netimobiledevice;
using Netimobiledevice.Afc;
using Netimobiledevice.InstallationProxy;
using Netimobiledevice.Lockdown;
using Netimobiledevice.Plist;
using Netimobiledevice.Usbmuxd;

namespace AstralParty.Toys.Mobile.Channels;

/// <summary>
/// iOS device channel: usbmux -> lockdown -> house_arrest(documentsOnly: true) -> AFC.
/// Read-only in M1. All calls are ported verbatim from the M0 probe (tools/IosProbe).
/// </summary>
public sealed class IosChannel : IDeviceChannel
{
    /// <summary>iOS bundle ids in the reference order; com.feimo.astralpartycn is the CN App Store build.</summary>
    public static readonly string[] CandidateBundleIds =
    [
        "com.feimo.astralpartycn",
        "com.feimo.astralparty",
        "com.feimo.jxpd",
        "com.feimo.astralparty.cesium",
    ];

    public string Kind => "ios";

    public string BuildRoot(MobileTarget target) => "Documents/";

    public Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<DeviceInfo>();
        foreach (var device in Usbmux.GetDeviceList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            LockdownClient? lockdown = null;
            try
            {
                lockdown = MobileDevice.CreateUsingUsbmux(device.Serial, autopair: true, logger: NullLogger.Instance);
                var nameNode = lockdown.GetValue("DeviceName");
                var name = nameNode is StringNode stringNode ? stringNode.Value : device.Serial;
                result.Add(new DeviceInfo
                {
                    Kind = "ios",
                    Id = device.Serial,
                    Label = name,
                    Model = lockdown.ProductType?.ToString() ?? "",
                    OsVersion = lockdown.OsVersion?.ToString() ?? "",
                    Paired = true,
                    State = "online",
                    LastSeen = DateTimeOffset.Now.ToString("o"),
                });
            }
            catch
            {
                // A device that cannot be locked down (still locked / not trusted) is still listed
                // so the user gets a card explaining what to do.
                result.Add(new DeviceInfo
                {
                    Kind = "ios",
                    Id = device.Serial,
                    Label = device.DeviceId.ToString(),
                    Model = "",
                    OsVersion = "",
                    Paired = false,
                    State = "untrusted",
                    LastSeen = DateTimeOffset.Now.ToString("o"),
                });
            }
            finally
            {
                lockdown?.Dispose();
            }
        }

        return Task.FromResult<IReadOnlyList<DeviceInfo>>(result);
    }

    /// <summary>
    /// 列出设备上所有已安装 app，按包名特征挑出候选，并逐个开一次 house_arrest 容器探缓存。
    /// 渠道服很多，所以这里不认写死的清单，只认特征，最终由玩家选定当前游戏。
    /// </summary>
    public async Task<IReadOnlyList<MobilePackageCandidate>> FindGamePackagesAsync(
        string serial, CancellationToken cancellationToken = default)
    {
        var result = new List<MobilePackageCandidate>();
        using var lockdown = MobileDevice.CreateUsingUsbmux(serial, autopair: true, logger: NullLogger.Instance);
        using var proxy = new InstallationProxyService(lockdown, NullLogger.Instance);
        var node = await proxy.Browse(null, null, cancellationToken);

        foreach (var item in node)
        {
            if (item is not DictionaryNode dict)
                continue;
            var appId = dict.Text("CFBundleIdentifier");
            if (string.IsNullOrEmpty(appId))
                continue;
            var matchedBy = MobilePaths.MatchGameIdMarker(appId);
            if (matchedBy is null)
                continue;

            var candidate = new MobilePackageCandidate
            {
                AppId = appId,
                Name = FirstNonEmpty(dict.Text("CFBundleDisplayName"), dict.Text("CFBundleName"), appId),
                Version = dict.Text("CFBundleShortVersionString") ?? "",
                MatchedBy = matchedBy,
            };

            try
            {
                using var house = await HouseArrestService.Create(lockdown, appId, documentsOnly: true,
                    logger: NullLogger.Instance, cancellationToken: cancellationToken);
                candidate.ContainerAccessible = true;
                candidate.HasCache = await house.Exists(
                    "Documents/" + MobilePaths.AddressablesRelative, cancellationToken);
            }
            catch (Exception)
            {
                candidate.ContainerAccessible = false;
            }

            result.Add(candidate);
        }

        return result;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value!;
        }

        return "";
    }

    public async Task<IDeviceFileSystem> OpenAsync(MobileTarget target, CancellationToken cancellationToken = default)
    {
        var lockdown = MobileDevice.CreateUsingUsbmux(target.Serial, autopair: true, logger: NullLogger.Instance);
        try
        {
            var bundleId = string.IsNullOrEmpty(target.AppId)
                ? await ResolveBundleAsync(lockdown, cancellationToken)
                : target.AppId;

            var house = await HouseArrestService.Create(
                lockdown, bundleId, documentsOnly: true,
                logger: NullLogger.Instance, cancellationToken: cancellationToken);

            ServiceConnection? rawConnection = null;
            try
            {
                rawConnection = lockdown.StartLockdownService("com.apple.mobile.house_arrest");
                var request = new DictionaryNode
                {
                    { "Command", new StringNode("VendDocuments") },
                    { "Identifier", new StringNode(bundleId) },
                };
                var reply = (await rawConnection.SendReceivePlistAsync(request, cancellationToken))?.AsDictionaryNode();
                if (reply is not null && reply.TryGetValue("Error", out var error))
                    throw new InvalidOperationException($"house_arrest VendDocuments 失败：{error.AsStringNode().Value}");
            }
            catch
            {
                rawConnection?.Dispose();
                throw;
            }

            return new AfcFileSystem(lockdown, house, rawConnection);
        }
        catch
        {
            lockdown.Dispose();
            throw;
        }
    }

    private static async Task<string> ResolveBundleAsync(LockdownClient lockdown, CancellationToken cancellationToken)
    {
        using var proxy = new InstallationProxyService(lockdown, NullLogger.Instance);
        var node = await proxy.Browse(null, null, cancellationToken);
        var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in node)
        {
            if (item is not DictionaryNode dict)
                continue;
            var id = dict.Text("CFBundleIdentifier");
            if (!string.IsNullOrEmpty(id))
                installed.Add(id);
        }

        foreach (var candidate in CandidateBundleIds)
        {
            if (installed.Contains(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            "设备上没找到游戏（候选包名：" + string.Join("、", CandidateBundleIds) + "）。请确认游戏已安装。");
    }

    private sealed class AfcFileSystem(
        LockdownClient lockdown,
        HouseArrestService house,
        ServiceConnection rawConnection) : IDeviceFileSystem
    {
        private readonly RawAfcClient _raw = new(rawConnection);

        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
            house.Exists(path, cancellationToken);

        public async Task<DeviceFileInfo?> StatAsync(string path, CancellationToken cancellationToken = default)
        {
            var info = await house.GetFileInfo(path, cancellationToken);
            if (info is null)
                return null;
            var size = info.Number("st_size");
            var modifiedSeconds = info.Number("st_mtime");
            var isDir = string.Equals(info.Text("st_ifmt"), "S_IFDIR", StringComparison.OrdinalIgnoreCase);
            var modified = modifiedSeconds > 0
                ? DateTimeOffset.FromUnixTimeSeconds(modifiedSeconds)
                : (DateTimeOffset?)null;
            return new DeviceFileInfo(size, modified, isDir);
        }

        public async Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _raw.ReadDirectoryAsync(path, cancellationToken);
            }
            catch (IOException)
            {
                // Fall back to the library. Two caveats are handled here: LsDirectory always walks the
                // whole tree (so descendants must be filtered out, or grandchildren would be reported
                // as children), and the first entry it yields is the queried path itself.
                var depth = path.TrimEnd('/').Count(ch => ch == '/') + 1;
                var names = new List<string>();
                await foreach (var entry in house.LsDirectory(path, cancellationToken, depth: 1))
                {
                    var text = entry.Replace('\\', '/').TrimEnd('/');
                    if (text.Count(ch => ch == '/') != depth)
                        continue;
                    var name = text[(text.LastIndexOf('/') + 1)..];
                    if (!string.IsNullOrEmpty(name) && name is not ("." or ".."))
                        names.Add(name);
                }

                return names;
            }
        }

        public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default) =>
            await house.GetFileContents(path, cancellationToken) ?? [];

        public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default) =>
            Encoding.UTF8.GetString(await ReadAllBytesAsync(path, cancellationToken));

        public async Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) =>
            await house.SetFileContents(path, data, cancellationToken);

        public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            var leftovers = await house.Rm(path, cancellationToken, force: true);
            if (leftovers.Count > 0 || await house.Exists(path, cancellationToken))
                throw new IOException($"删除失败：{path}（残留 {string.Join(", ", leftovers)}）");
        }

        public async Task MakeDirectoriesAsync(string path, CancellationToken cancellationToken = default)
        {
            var status = await _raw.MakeDirectoryAsync(path, cancellationToken);
            if (status != 0)
                throw new IOException($"MAKE_DIR 失败：{path}（status={status}）");
        }

        public async Task RenameAsync(string from, string to, CancellationToken cancellationToken = default)
        {
            var status = await _raw.RenameAsync(from, to, cancellationToken);
            if (status != 0)
                throw new IOException($"RENAME_PATH 失败：{from} -> {to}（status={status}）");
        }

        public ValueTask DisposeAsync()
        {
            house.Dispose();
            rawConnection.Dispose();
            lockdown.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
