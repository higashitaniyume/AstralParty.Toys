using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstralParty.Toys.Mobile;
using AstralParty.Toys.Mobile.Bundles;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Logging;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Mobile.Patching;
using AstralParty.Toys.Mobile.Payload;
using AstralParty.Toys.Mobile.Services;
using AstralParty.Toys.Mobile.Targets;

namespace AstralParty.Toys.Services.Mobile;

/// <summary>
/// Read-only detection orchestration shared by all device pages. No write operations live here.
/// </summary>
public sealed class MobileDeviceService
{
    public const string AddressablesRelative = MobilePaths.AddressablesRelative;
    public const string BundlesRelative = MobilePaths.BundlesRelative;
    public const string LoaderRelative = MobilePaths.LoaderRelative;

    /// <summary>iOS bundle ids in the same order as the reference implementation.</summary>
    public static readonly string[] IosCandidateIds = MobilePaths.IosCandidateIds;

    /// <summary>Android packages seen on real devices; the loader is usually in the first one.</summary>
    public static readonly string[] AndroidCandidateIds = MobilePaths.AndroidCandidateIds;

    private readonly MobileTargetStore _store;
    private readonly Dictionary<string, IDeviceChannel> _channels;
    private readonly MobileOperationLog? _log;
    private readonly MobileInstallStore? _installStore;
    private readonly object _gate = new();
    private readonly Dictionary<string, MobileDetectReport> _lastReports = new();

    public MobileDeviceService(string stateDirectory, IEnumerable<IDeviceChannel> channels,
        MobileOperationLog? log = null, MobileInstallStore? installStore = null)
    {
        _store = new MobileTargetStore(stateDirectory);
        _channels = channels.ToDictionary(c => c.Kind, StringComparer.OrdinalIgnoreCase);
        _log = log;
        _installStore = installStore;
    }

    /// <summary>全程日志（可能为 null：单元测试里不落盘）。</summary>
    public MobileOperationLog? Log => _log;

    /// <summary>给设备文件系统套上日志；把「读了哪个文件」这件事记全。</summary>
    private IDeviceFileSystem Wrap(IDeviceFileSystem fileSystem) =>
        _log is null ? fileSystem : new LoggingFileSystem(fileSystem, _log);

    /// <summary>
    /// 扫出这台设备上所有可能的游戏包（渠道服很多），供玩家选定当前游戏。
    /// </summary>
    public async Task<IReadOnlyList<MobilePackageCandidate>> FindPackagesAsync(string targetId,
        CancellationToken cancellationToken = default)
    {
        var target = _store.LoadTargets().FirstOrDefault(t =>
                         string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException($"未找到设备目标：{targetId}");
        if (!_channels.TryGetValue(target.Kind, out var channel))
            throw new InvalidOperationException($"不支持的设备通道：{target.Kind}");

        _log?.Info($"=== 扫描游戏包 {target.Kind} {target.Serial} ===");
        var packages = await channel.FindGamePackagesAsync(target.Serial, cancellationToken);
        foreach (var package in packages)
        {
            _log?.Info($"候选 {package.AppId}  名称={package.Name}  版本={package.Version}  " +
                       $"缓存={(package.HasCache ? "有" : "无")}  容器={(package.ContainerAccessible ? "可访问" : "不可访问")}  " +
                       $"命中={package.MatchedBy}");
        }

        _log?.Info($"扫描到 {packages.Count} 个候选包");
        return packages;
    }

    /// <summary>把某个包设成这台设备上的当前游戏。</summary>
    public MobileTarget SetApp(string targetId, string appId)
    {
        var target = _store.LoadTargets().FirstOrDefault(t =>
                         string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException($"未找到设备目标：{targetId}");
        target.AppId = appId;

        // 记录按「设备 + 包」存，切回来时把那个包自己的状态取回来，而不是一律清空。
        var record = _installStore?.Get(MobileInstallService.Key(target));
        target.InstalledInner = record?.Installed.Inner ?? "";
        target.InstalledPatchedSha256 = record?.Installed.PatchedSha256 ?? "";
        target.OriginalSha256 = record?.Installed.OriginalSha256 ?? "";
        target.BundleOuter = record?.BundleOuter ?? "";
        target.Installed = record is not null && !string.IsNullOrEmpty(record.Installed.At);

        _store.Upsert(target);
        _log?.Info($"当前游戏设为 {appId}（该包的历史记录：{(target.Installed ? "有" : "无")}）");
        return target;
    }

    public MobileTargetStore Store => _store;

    public IReadOnlyList<MobileTarget> GetTargets() => _store.LoadTargets();

    public ActiveTargetState GetActive() => _store.LoadActive();

    public void SetActiveLocal(string profileId) => _store.SetActiveLocal(profileId);

    public void SetActiveMobile(string targetId) => _store.SetActiveMobile(targetId);

    public MobileDetectReport? GetLastReport(string targetId)
    {
        lock (_gate)
            return _lastReports.GetValueOrDefault(targetId);
    }

    public async Task<IReadOnlyList<DeviceInfo>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<DeviceInfo>();
        foreach (var channel in _channels.Values)
        {
            DeviceInfo[] found;
            try
            {
                found = [.. await channel.DiscoverAsync(cancellationToken)];
            }
            catch
            {
                found = [];
            }

            all.AddRange(found);

            var seen = found.Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in _store.LoadTargets().Where(t =>
                         string.Equals(t.Kind, channel.Kind, StringComparison.OrdinalIgnoreCase)))
            {
                if (!seen.Contains(existing.Serial))
                    _store.MarkOffline(existing.Id);
            }

            var existingTargets = _store.LoadTargets()
                .ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var device in found)
            {
                var id = device.Kind + ":" + device.Id;
                var label = string.IsNullOrEmpty(device.Label) ? device.Model : device.Label;
                var merged = existingTargets.TryGetValue(id, out var prior)
                    ? new MobileTarget
                    {
                        Id = prior.Id,
                        Kind = device.Kind,
                        Serial = device.Id,
                        Label = label,
                        Model = device.Model,
                        OsVersion = device.OsVersion,
                        State = device.State,
                        Paired = device.Paired,
                        AppId = prior.AppId,
                        BundleOuter = prior.BundleOuter,
                        Installed = prior.Installed,
                        InstalledInner = prior.InstalledInner,
                        InstalledPatchedSha256 = prior.InstalledPatchedSha256,
                        OriginalSha256 = prior.OriginalSha256,
                        Online = true,
                        LastSeen = DateTimeOffset.Now.ToString("o"),
                    }
                    : new MobileTarget
                    {
                        Id = id,
                        Kind = device.Kind,
                        Serial = device.Id,
                        Label = label,
                        Model = device.Model,
                        OsVersion = device.OsVersion,
                        State = device.State,
                        Paired = device.Paired,
                        AppId = "",
                        Online = true,
                        LastSeen = DateTimeOffset.Now.ToString("o"),
                    };
                _store.Upsert(merged);
            }
        }

        return all;
    }

    public async Task<MobileDetectReport> DetectAsync(
        string targetId,
        Action<string, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var target = _store.LoadTargets().FirstOrDefault(t =>
                         string.Equals(t.Id, targetId, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException($"未找到设备目标：{targetId}");
        if (!_channels.TryGetValue(target.Kind, out var channel))
            throw new InvalidOperationException($"不支持的设备通道：{target.Kind}");

        var report = new MobileDetectReport
        {
            Kind = target.Kind,
            TargetId = target.Id,
            Label = target.Label,
            DetectedAt = DateTimeOffset.Now.ToString("o"),
        };

        progress?.Invoke("正在连接设备…", 5);
        _log?.Info($"=== 检测 {target.Kind} {target.Serial}" +
                   $"（包名 {(string.IsNullOrEmpty(target.AppId) ? "未指定，按候选逐个试" : target.AppId)}）===");
        var candidates = string.IsNullOrEmpty(target.AppId)
            ? target.Kind == "ios" ? IosCandidateIds : AndroidCandidateIds
            : [target.AppId];

        IDeviceFileSystem? fileSystem = null;
        MobileTarget? resolved = null;
        foreach (var candidate in candidates)
        {
            try
            {
                var candidateTarget = new MobileTarget
                {
                    Kind = target.Kind,
                    Serial = target.Serial,
                    AppId = candidate,
                };
                _log?.Info($"试探候选包 {candidate}");
                var probe = Wrap(await channel.OpenAsync(candidateTarget, cancellationToken));
                if (await probe.ExistsAsync(channel.BuildRoot(candidateTarget) + AddressablesRelative, cancellationToken))
                {
                    fileSystem = probe;
                    resolved = candidateTarget;
                    report.BundleId = candidate;
                    break;
                }

                await probe.DisposeAsync();
            }
            catch (Exception ex)
            {
                // Try the next candidate; the last one is reported as a generic failure below.
                _log?.Info($"候选 {candidate} 不可用：{ex.GetType().Name}: {ex.Message}");
            }
        }

        if (fileSystem is null || resolved is null)
        {
            report.Issues.Add("在设备上找不到游戏容器（请确认游戏已安装并信任此电脑 / 已授权 USB 调试）");
            Cache(target.Id, report);
            return report;
        }

        var root = channel.BuildRoot(resolved);
        await using (fileSystem)
        {
            progress?.Invoke("正在读取 catalog…", 15);
            var catalogNames = await fileSystem.ListAsync(root + AddressablesRelative, cancellationToken);
            var catalogs = catalogNames
                .Where(n => n.StartsWith("catalog_", StringComparison.Ordinal) &&
                            n.EndsWith(".json", StringComparison.Ordinal))
                .ToList();
            if (catalogs.Count == 0)
            {
                report.Issues.Add("容器里找不到 catalog（热更可能还没下完）");
                Cache(target.Id, report);
                return report;
            }

            string? catalogName = null;
            DateTimeOffset newest = DateTimeOffset.MinValue;
            foreach (var name in catalogs)
            {
                var stat = await fileSystem.StatAsync(root + AddressablesRelative + "/" + name, cancellationToken);
                var modified = stat?.Modified ?? DateTimeOffset.MinValue;
                if (modified >= newest)
                {
                    newest = modified;
                    catalogName = name;
                }
            }

            catalogName ??= catalogs[^1];
            report.Catalog = catalogName;
            var appVersionMatch = Regex.Match(catalogName, @"catalog_(?<v>.+)\.json");
            if (appVersionMatch.Success)
                report.AppVersion = appVersionMatch.Groups["v"].Value;

            var catalogBytes = await fileSystem.ReadAllBytesAsync(
                root + AddressablesRelative + "/" + catalogName, cancellationToken);
            report.CatalogSha256 = Sha256(catalogBytes);
            var catalog = AddressablesCatalog.Parse(catalogBytes);
            report.CatalogBuildHash = catalog.BuildResultHash;

            var inner = catalog.BundleHashFor(AddressablesCatalog.RuntimeAssetKey);
            if (string.IsNullOrEmpty(inner))
            {
                report.Issues.Add("catalog 里找不到 " + AddressablesCatalog.RuntimeAssetKey);
                Cache(target.Id, report);
                return report;
            }

            report.CodeBundleHash = inner;
            progress?.Invoke("正在定位代码包…", 30);
            var outer = await LocateOuterAsync(fileSystem, root, target, inner, progress, cancellationToken);
            if (string.IsNullOrEmpty(outer))
            {
                report.Issues.Add("缓存里找不到当前代码包（热更可能没下完）");
                Cache(target.Id, report);
                return report;
            }

            report.BundleDir = $"{root}{BundlesRelative}/{outer}/{inner}";
            var dataPath = report.BundleDir + "/__data";
            var statData = await fileSystem.StatAsync(dataPath, cancellationToken);
            report.BundleBytes = statData?.Size ?? 0;
            progress?.Invoke("正在校验代码包…", 55);
            var bundleBytes = await fileSystem.ReadAllBytesAsync(dataPath, cancellationToken);
            report.BundleSha256 = Sha256(bundleBytes);
            try
            {
                var assembly = RuntimeAssemblyReader.Read(bundleBytes, AddressablesCatalog.RuntimeAssetKey);
                report.AssetCount = assembly.AssetCount;
                var dll = assembly.Assembly ?? [];
                report.LegacyMarker = PatchMarkerDetector.ContainsLegacyMarker(dll);
                report.CurrentMarker = PatchMarkerDetector.ContainsCurrentMarker(dll);
                report.Injected = report.LegacyMarker || report.CurrentMarker;
            }
            catch (Exception ex)
            {
                report.Issues.Add("代码包解包失败：" + ex.Message);
            }

            progress?.Invoke("正在读取加载器状态…", 80);
            await ReadLoaderAsync(fileSystem, root, report, cancellationToken);

            // After the Mobile rename, a new bootstrap with a legacy-only marker is a stale patch.
            // Before the rename, the legacy marker is the valid injected form.
            if (report.LegacyMarker && !report.CurrentMarker &&
                report.BootstrapAssembly.Contains("CesiumLoader.Mobile.Bootstrap", StringComparison.OrdinalIgnoreCase))
            {
                report.Injected = false;
                report.PatchState = PatchStates.Stale;
            }
            else if (string.Equals(target.InstalledInner, inner, StringComparison.OrdinalIgnoreCase))
            {
                report.PatchState = string.Equals(target.InstalledPatchedSha256, report.BundleSha256, StringComparison.OrdinalIgnoreCase)
                    ? PatchStates.Patched
                    : string.Equals(target.OriginalSha256, report.BundleSha256, StringComparison.OrdinalIgnoreCase)
                        ? PatchStates.Clean
                        : report.Injected ? PatchStates.PatchedExternal : PatchStates.Stale;
            }
            else if (!string.IsNullOrEmpty(target.InstalledInner) && !report.Injected)
            {
                // 本机记录着曾给更早的 inner 打过补丁，现在 catalog 指向的新包却不含注入标记：
                // 通常是游戏热更下了新的代码包，原来的注入已失效，需要重新注入。
                report.PatchState = PatchStates.Stale;
            }
            else
            {
                // 没有本地安装记录时，仍然能凭内容下结论：包上带注入标记就是「外部注入」，
                // 不带就是原版。早先这里把「没记录且未注入」判成 Unknown，于是全新安装的设备
                // 设备页显示「未知」，而不是「未注入」。Unknown 现在只用于读不出来（下面的 catch）。
                report.PatchState = report.Injected ? PatchStates.PatchedExternal : PatchStates.Clean;
            }

            target.BundleOuter = outer;
            target.Installed = report.LoaderPresent;
            target.Online = true;
            target.LastSeen = DateTimeOffset.Now.ToString("o");
            if (string.IsNullOrEmpty(target.AppId))
                target.AppId = report.BundleId;
            _store.Upsert(target);

            progress?.Invoke("检测完成", 100);
        }

        Cache(target.Id, report);
        return report;
    }

    private static async Task<string?> LocateOuterAsync(
        IDeviceFileSystem fileSystem,
        string root,
        MobileTarget target,
        string inner,
        Action<string, int>? progress,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(target.BundleOuter) &&
            await fileSystem.ExistsAsync($"{root}{BundlesRelative}/{target.BundleOuter}/{inner}/__data", cancellationToken))
            return target.BundleOuter;

        progress?.Invoke("扫描缓存定位代码包…", 32);
        return await fileSystem.FindBundleOuterAsync(root + BundlesRelative, inner, cancellationToken);
    }

    private static async Task ReadLoaderAsync(
        IDeviceFileSystem fileSystem,
        string root,
        MobileDetectReport report,
        CancellationToken cancellationToken)
    {
        var loaderRoot = root + LoaderRelative;
        report.LoaderRoot = loaderRoot;
        if (!await fileSystem.ExistsAsync(loaderRoot, cancellationToken))
        {
            report.Issues.Add("容器里没有 CesiumLoader 目录（尚未安装加载器）");
            return;
        }

        report.LoaderPresent = true;
        _ = await SafeListAsync(fileSystem, loaderRoot, cancellationToken);
        if (await fileSystem.ExistsAsync(loaderRoot + "/installed.json", cancellationToken))
        {
            try
            {
                using var doc = JsonDocument.Parse(
                    await fileSystem.ReadAllBytesAsync(loaderRoot + "/installed.json", cancellationToken));
                var data = doc.RootElement;
                report.LoaderVersion = GetJsonString(data, "version") ?? GetJsonString(data, "loaderVersion") ?? "";
                report.SdkVersion = GetJsonString(data, "sdkVersion") ?? "";
            // 安卓把引导放在 bootstrap/ 下，报告里只显示文件名
            var bootstrapValue = GetJsonString(data, "bootstrap") ?? "";
            report.BootstrapAssembly = bootstrapValue.Contains('/')
                ? bootstrapValue[(bootstrapValue.LastIndexOf('/') + 1)..]
                : bootstrapValue;
            }
            catch (JsonException)
            {
                report.Issues.Add("installed.json 解析失败");
            }
        }

        // installed.json 是「安装器写的装载状态」，别人的安装方式可能没有它；
        // 这时按 AGENTS.md 的口径退回读 SDK 的程序集版本，引导 DLL 在不在也直接看文件。
        if (string.IsNullOrEmpty(report.SdkVersion))
            report.SdkVersion = await ReadAssemblyVersionAsync(
                fileSystem, loaderRoot + "/" + MobilePayload.SdkRelative, cancellationToken);
        if (string.IsNullOrEmpty(report.BootstrapAssembly))
        {
            foreach (var candidate in MobilePayload.BootstrapFileCandidates)
            {
                var nested = loaderRoot + "/bootstrap/" + candidate;
                if (!await fileSystem.ExistsAsync(loaderRoot + "/" + candidate, cancellationToken) &&
                    !await fileSystem.ExistsAsync(nested, cancellationToken))
                    continue;
                report.BootstrapAssembly = candidate;
                break;
            }
        }

        // 刚投放、或被玩家删过一部分的加载器目录是不完整的：缺 mods/ 或 logs/ 时不能把整个检测打断。
        var mods = await SafeListAsync(fileSystem, loaderRoot + "/mods", cancellationToken);
        report.ModCount = mods.Count;

        var logs = await SafeListAsync(fileSystem, loaderRoot + "/logs", cancellationToken);
        var bootstrap = logs
            .Where(n => n.StartsWith("bootstrap-", StringComparison.Ordinal) && n.EndsWith(".log", StringComparison.Ordinal))
            .OrderByDescending(n => n, StringComparer.Ordinal)
            .FirstOrDefault();
        if (bootstrap is null)
            return;

        report.BootstrapLog = bootstrap;
        var text = await fileSystem.ReadAllTextAsync(loaderRoot + "/logs/" + bootstrap, cancellationToken);
        report.BootstrapTail = text.Split('\n').Select(l => l.TrimEnd('\r')).TakeLast(12).ToList();
        var match = Regex.Match(text, @"done: ok=(\d+) failed=(\d+)");
        if (match.Success)
        {
            report.ModsOk = int.Parse(match.Groups[1].Value);
            report.ModsFailed = int.Parse(match.Groups[2].Value);
        }
    }

    /// <summary>列目录但允许不存在：加载器目录可能只投了一部分，缺子目录是正常状态。</summary>
    private static async Task<IReadOnlyList<string>> SafeListAsync(
        IDeviceFileSystem fileSystem, string path, CancellationToken cancellationToken)
    {
        try
        {
            return await fileSystem.ListAsync(path, cancellationToken);
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>读设备上一个托管 DLL 的程序集版本（落到临时文件再读元数据，不加载、不执行）。</summary>
    private static async Task<string> ReadAssemblyVersionAsync(
        IDeviceFileSystem fileSystem, string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!await fileSystem.ExistsAsync(path, cancellationToken))
                return "";
            var bytes = await fileSystem.ReadAllBytesAsync(path, cancellationToken);
            var temp = Path.Combine(Path.GetTempPath(), "cesium-ver-" + Guid.NewGuid().ToString("N") + ".dll");
            try
            {
                await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
                return AssemblyName.GetAssemblyName(temp).Version?.ToString() ?? "";
            }
            finally
            {
                try { File.Delete(temp); }
                catch (IOException) { /* 临时文件清理是尽力而为 */ }
            }
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static string? GetJsonString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private void Cache(string targetId, MobileDetectReport report)
    {
        lock (_gate)
            _lastReports[targetId] = report;

        // 所有返回路径都经过这里，检测结果在这里统一留痕，避免漏记早退分支。
        if (_log is null)
            return;
        _log.Info($"检测结果：包名={(string.IsNullOrEmpty(report.BundleId) ? "未识别" : report.BundleId)} " +
                  $"代码包={report.CodeBundleHash} {report.BundleBytes:N0} B sha={Short(report.BundleSha256)} " +
                  $"资产={report.AssetCount} 补丁状态={report.PatchState} " +
                  $"加载器={(report.LoaderPresent ? report.LoaderVersion : "未安装")}");
        foreach (var issue in report.Issues)
            _log.Warn("问题：" + issue);
    }

    private static string Short(string sha) => sha.Length >= 16 ? sha[..16] : sha;
}
