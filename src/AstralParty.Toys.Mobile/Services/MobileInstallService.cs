using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AstralParty.Toys.Mobile.Bundles;
using AstralParty.Toys.Mobile.Channels;
using AstralParty.Toys.Mobile.Logging;
using AstralParty.Toys.Mobile.Models;
using AstralParty.Toys.Mobile.Patching;
using AstralParty.Toys.Mobile.Payload;
using AstralParty.Toys.Mobile.Targets;
using CesiumLoader.Mobile.Patcher;

namespace AstralParty.Toys.Mobile.Services;

/// <summary>
/// M2: backup -> inject -> splice -> verify -> write back -> read back -> record state.
/// Writes are only performed on the device's code-bundle cache; no other device files are touched.
/// </summary>
public sealed class MobileInstallService
{
    public const string RuntimeAsset = AddressablesCatalog.RuntimeAssetKey;

    // Kept at the iOS reference values until the Mobile loader rename ships (M8).
    // 改名时改这两行（连同 MobilePayload.BootstrapFile）；改了要同时换加载器产物，否则注入的 cctor 找不到程序集
    public const string BootDll = "CesiumLoader.Mobile.Bootstrap.dll";
    public const string BootType = "CesiumLoader.Mobile.Bootstrap.Boot";

    private readonly IDeviceChannel _channel;
    private readonly MobileInstallStore _store;
    private readonly MobileTargetStore _targets;
    private readonly MobileOperationLog? _log;

    public MobileInstallService(
        IDeviceChannel channel,
        MobileInstallStore store,
        MobileTargetStore targets,
        MobileOperationLog? log = null)
    {
        _channel = channel;
        _store = store;
        _targets = targets;
        _log = log;
    }

    /// <summary>给设备文件系统套上日志：写回/还原读了哪些文件、多大、哈希都会留痕。</summary>
    private IDeviceFileSystem Wrap(IDeviceFileSystem fileSystem) =>
        MobileDeviceAccess.Wrap(fileSystem, _log);

    /// <summary>
    /// 安装记录按「设备 + 游戏包」存：同一台设备上往往装了多个渠道服，切换当前游戏时
    /// 不能把别那个包的注入/备份状态弄丢（备份文件本身按内容哈希命名，不会丢）。
    /// </summary>
    public static string Key(MobileTarget target) =>
        string.IsNullOrEmpty(target.AppId) ? target.Id : $"{target.Id}|{target.AppId}";

    /// <summary>把该目标对应包的安装记录回填到展示字段（切换当前游戏后调用）。</summary>
    public MobileTarget ApplyRecord(MobileTarget target)
    {
        var record = _store.Get(Key(target));
        target.InstalledInner = record?.Installed.Inner ?? "";
        target.InstalledPatchedSha256 = record?.Installed.PatchedSha256 ?? "";
        target.OriginalSha256 = record?.Installed.OriginalSha256 ?? "";
        if (!string.IsNullOrEmpty(record?.BundleOuter))
            target.BundleOuter = record!.BundleOuter;
        target.Installed = record is not null && !string.IsNullOrEmpty(record.Installed.At);
        return target;
    }

    private void Step(string message, int percent, Action<string, int>? progress)
    {
        _log?.Info(message);
        progress?.Invoke(message, percent);
    }

    /// <summary>
    /// 失败也留痕：界面看到的失败原因必须能在日志文件里找到。
    /// 名字不要叫 <c>Fail</c>：同名的静态工厂 <see cref="MobileOperationResult.Fail"/> 会被实例方法遮蔽，
    /// 写成 <c>Fail(msg)</c> 就是无限递归（栈溢出，进程直接死，catch 不住）。
    /// </summary>
    private MobileOperationResult FailWith(string message)
    {
        _log?.Error(message);
        return MobileOperationResult.Fail(message);
    }

    private static string Short(string sha) => sha.Length >= 16 ? sha[..16] : sha;

    public async Task<MobileOperationResult> PatchAsync(
        MobileTarget target, Action<string, int>? progress = null, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(target, progress, cancellationToken);
        if (resolved is null)
            return FailWith("找不到游戏代码包（先做一次只读检测确认状态）");
        var (fileSystem, root, bundleDir, inner, outer, buildHash) = resolved.Value;

        await using (fileSystem)
        {
            Step("读取原始代码包…", 20, progress);
            var original = await fileSystem.ReadAllBytesAsync(bundleDir + "/__data", cancellationToken);
            var originalSha = Sha(original);
            var alreadyInjected = IsInjected(original);

            if (!alreadyInjected)
            {
                Step("备份原始代码包…", 25, progress);
                _store.SaveBackup(inner, original);
            }
            else if (_store.HasBackup(inner))
            {
                Step("使用本机原始备份重新注入…", 25, progress);
                original = _store.ReadBackup(inner);
                originalSha = Sha(original);
            }
            else
            {
                return FailWith(
                    "设备上的代码包已带注入标记且本机无原始备份，不能重复叠加注入。" +
                    "请先点击「还原」清理缓存并启动一次游戏重新下载原始代码包，然后再执行一键安装或注入。");
            }

            var work = Path.Combine(_store.WorkDirectory, "install", inner);
            var assetsDir = Path.Combine(work, "assets");
            Directory.CreateDirectory(assetsDir);

            Step("解出 TextAsset…", 35, progress);
            var assets = RuntimeAssemblyReader.ReadAllTextAssets(original, out _);
            foreach (var (name, bytes) in assets)
                File.WriteAllBytes(Path.Combine(assetsDir, SafeFileName(name)), bytes);
            if (!assets.ContainsKey(RuntimeAsset))
                return FailWith("代码包里没有 " + RuntimeAsset);

            var inputDll = Path.Combine(assetsDir, SafeFileName(RuntimeAsset));
            var patchedDll = Path.Combine(work, "patched.dll");
            Step("注入引导…", 45, progress);
            var patcherResult = await RunPatcherAsync(inputDll, patchedDll, assetsDir, cancellationToken);
            if (!patcherResult.Ok)
                return FailWith("注入器失败：" + patcherResult.Message);

            var patchedAssembly = await File.ReadAllBytesAsync(patchedDll, cancellationToken);
            if (!PatchMarkerDetector.ContainsMarker(patchedAssembly))
                return FailWith("注入后的程序集里找不到引导标记");

            var patchedBundle = Path.Combine(work, "patched.data");
            Step("换回代码包…", 60, progress);
            BundleSplicer.Splice(original, RuntimeAsset, patchedAssembly, patchedBundle);
            var patchedBytes = await File.ReadAllBytesAsync(patchedBundle, cancellationToken);

            Step("校验其余资产…", 75, progress);
            var before = RuntimeAssemblyReader.Fingerprints(original, out _);
            var after = RuntimeAssemblyReader.Fingerprints(patchedBytes, out _);
            // 目标资产本来就是要变的，比「其余资产」时必须先把它排掉，
            // 否则任何一次注入都会在这里假失败（报「换回后资产发生变化：AstralParty.Runtime.dll」）。
            var changed = before.Keys
                .Where(name => !string.Equals(name, RuntimeAsset, StringComparison.Ordinal))
                .Where(name => !after.TryGetValue(name, out var hash) ||
                               !string.Equals(hash, before[name], StringComparison.Ordinal))
                .ToList();
            if (changed.Count != 0)
                return FailWith("换回后资产发生变化：" + string.Join("、", changed));
            if (!after.TryGetValue(RuntimeAsset, out var targetHash) ||
                !string.Equals(targetHash, Sha(patchedAssembly), StringComparison.Ordinal))
                return FailWith("换回后目标资产与注入结果不一致");

            Step("写回设备…", 85, progress);
            await fileSystem.WriteAllBytesAsync(bundleDir + "/__data", patchedBytes, cancellationToken);
            var readback = await fileSystem.ReadAllBytesAsync(bundleDir + "/__data", cancellationToken);
            var patchedSha = Sha(readback);
            if (!string.Equals(patchedSha, Sha(patchedBytes), StringComparison.Ordinal))
                return FailWith("回读校验失败：设备上的代码包与写入内容不一致");

            _store.Update(Key(target), record =>
            {
                record.BundleOuter = outer;
                record.CatalogBuildHash = buildHash;
                record.Installed.Inner = inner;
                record.Installed.OriginalSha256 = alreadyInjected
                    ? _store.Get(Key(target))?.Installed.OriginalSha256 ?? ""
                    : originalSha;
                record.Installed.PatchedSha256 = patchedSha;
                record.Installed.At = DateTimeOffset.Now.ToString("o");
            });
            target.BundleOuter = outer;
            target.InstalledInner = inner;
            target.OriginalSha256 = alreadyInjected ? target.OriginalSha256 : originalSha;
            target.InstalledPatchedSha256 = patchedSha;
            target.Installed = true;
            _targets.Upsert(target);

            Step("注入完成", 100, progress);
            _log?.Info($"注入完成：包名={target.AppId} inner={inner} 原始 sha={Short(originalSha)} " +
                       $"注入后 sha={Short(patchedSha)} 备份={(alreadyInjected ? "沿用已有记录" : "已保存")}");
            return new MobileOperationResult
            {
                Ok = true,
                Message = alreadyInjected ? "代码包已是注入态，已刷新安装记录" : "已注入并写回代码包",
                Detail = $"inner={inner}  sha256={patchedSha}",
            };
        }
    }

    public async Task<MobileOperationResult> RestoreAsync(
        MobileTarget target, bool removeLoader,
        Action<string, int>? progress = null, CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAsync(target, progress, cancellationToken);
        if (resolved is null)
            return FailWith("找不到游戏代码包");
        var (fileSystem, root, bundleDir, inner, outer, _) = resolved.Value;

        await using (fileSystem)
        {
            Step("还原代码包…", 30, progress);
            if (_store.HasBackup(inner))
            {
                var original = _store.ReadBackup(inner);
                await fileSystem.WriteAllBytesAsync(bundleDir + "/__data", original, cancellationToken);
                var readback = await fileSystem.ReadAllBytesAsync(bundleDir + "/__data", cancellationToken);
                if (!Sha(readback).Equals(Sha(original), StringComparison.Ordinal))
                    return FailWith("还原回读校验失败");
            }
            else
            {
                // 没有本地备份时只能删掉这个内容哈希目录，让游戏重新下载。
                // 安卓上该目录由游戏创建（drwxr-s---，shell 无写权限），删不掉要给能看懂的提示。
                try
                {
                    await fileSystem.DeleteAsync(bundleDir, cancellationToken);
                }
                catch (Exception ex)
                {
                    return FailWith("没有本地备份，设备也不允许删除这个缓存条目（安卓上它是游戏自己建的目录，shell 没有写权限）。" +
                                "请先用本工具注入一次（会自动备份原始代码包）再还原，" +
                                $"或在设备上清除游戏数据让它重新下载。底层错误：{ex.Message}");
                }
            }

            if (removeLoader)
            {
                Step("清理加载器与标记…", 70, progress);
                await fileSystem.DeleteAsync(root + MobilePaths.LoaderRelative, cancellationToken);
                foreach (var name in await fileSystem.ListAsync(root, cancellationToken))
                {
                    if (name.StartsWith("cesium_", StringComparison.Ordinal))
                        await fileSystem.DeleteAsync(root + name, cancellationToken);
                }
            }

            _store.Update(Key(target), record =>
            {
                record.Installed = new InstalledBundleState();
                if (removeLoader)
                    record.Payload = new PayloadState();
            });
            target.InstalledInner = "";
            target.InstalledPatchedSha256 = "";
            target.OriginalSha256 = "";
            target.Installed = false;
            _targets.Upsert(target);

            Step("还原完成", 100, progress);
            _log?.Info($"还原完成：包名={target.AppId} inner={inner} " +
                       $"方式={(_store.HasBackup(inner) ? "本地备份" : "删除缓存条目，游戏下次启动重下")} 清理加载器={removeLoader}");
            return new MobileOperationResult
            {
                Ok = true,
                Message = _store.HasBackup(inner) ? "已用本地备份还原代码包" : "已删除缓存条目，游戏下次启动会重新下载原始包",
                Detail = "inner=" + inner,
            };
        }
    }

    /// <summary>
    /// M3：把载荷（加载器运行时：引导 DLL、SDK、模组）投放到 <c>&lt;容器&gt;/CesiumLoader/</c>。
    /// 只碰这个目录，不动游戏的任何既有文件。
    /// </summary>
    public async Task<MobileOperationResult> DeployPayloadAsync(
        MobileTarget target, string payloadRoot, Action<string, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        MobilePayload payload;
        try
        {
            payload = MobilePayload.Read(payloadRoot);
            payload.EnsureSdkUsable(allowDesktopSdk: !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable(MobilePayload.AllowDesktopSdkEnv)));
        }
        catch (Exception ex)
        {
            return FailWith("载荷不可用：" + ex.Message);
        }

        Step($"载荷 {payload.Version}：{payload.Files.Count} 个文件" +
             (payload.Skipped > 0 ? $"（跳过 {payload.Skipped} 个非运行时文件）" : ""), 5, progress);

        var opened = await OpenRootAsync(target, cancellationToken);
        if (opened is null)
            return FailWith("找不到游戏容器（先做一次「检测」确认包名）");

        var (fileSystem, root, appId) = opened.Value;
        await using (fileSystem)
        {
            var loaderRoot = root + MobilePaths.LoaderRelative;
            // 安卓：引导在 bootstrap/ 下；iOS 平铺在根（见 MobilePayload.DeviceRelativePath）。
            var android = !string.Equals(target.Kind, "ios", StringComparison.OrdinalIgnoreCase);
            Step($"在设备上准备 {loaderRoot}", 8, progress);
            await fileSystem.MakeDirectoriesAsync(loaderRoot, cancellationToken);
            // 目录必须先由我们自己建：游戏进程建的加载器目录是 2750（ext_data_rw 组无写位），
            // 之后经 shell 的写入会 Permission denied，而且既 chmod 不了也删不掉。
            foreach (var directory in new[] { "bootstrap", "sdk", "mods", "disabled_mods", "skins", "logs", "data" })
                await fileSystem.MakeDirectoriesAsync($"{loaderRoot}/{directory}", cancellationToken);

            var probe = loaderRoot + "/.aspatoys-write-probe";
            var canCreate = false;
            try
            {
                await fileSystem.WriteAllBytesAsync(probe, [], cancellationToken);
                if (await fileSystem.StatAsync(probe, cancellationToken) is not null)
                {
                    canCreate = true;
                    await fileSystem.DeleteAsync(probe, cancellationToken);
                }
            }
            catch
            {
                canCreate = false;
            }

            var disabledMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var name in await fileSystem.ListAsync($"{loaderRoot}/disabled_mods", cancellationToken))
                    disabledMods.Add(name);
            }
            catch
            {
                // Best effort when disabled_mods is empty or newly created.
            }

            if (!canCreate)
            {
                // 安卓加载器目录若已被游戏进程建出（2750），无法新建文件但可覆盖已有文件；
                // 若有待投放文件尚不存在，则直接给出明确的权限原因与处理指引。
                foreach (var file in payload.Files)
                {
                    var remote = $"{loaderRoot}/{ResolveDeployRelativePath(file.RelativePath, android, disabledMods)}";
                    if (await fileSystem.StatAsync(remote, cancellationToken) is null)
                    {
                        var reason = await MobileDeviceAccess.DescribeLoaderWritabilityAsync(
                            fileSystem, loaderRoot, cancellationToken);
                        return FailWith(reason ?? $"加载器目录不可写：{loaderRoot}");
                    }
                }
            }
            else
            {
                // Clean up obsolete legacy API/bootstrap binaries so old assembly names cannot shadow CesiumLoader.Mobile.*.
                foreach (var stale in new[]
                         {
                             $"{loaderRoot}/CesiumAndroidApi.dll",
                             $"{loaderRoot}/{MobilePayload.LegacyBootstrapFile}",
                             $"{loaderRoot}/bootstrap/{MobilePayload.LegacyBootstrapFile}",
                         })
                {
                    try
                    {
                        if (await fileSystem.ExistsAsync(stale, cancellationToken))
                            await fileSystem.DeleteAsync(stale, cancellationToken);
                    }
                    catch
                    {
                        // Best-effort cleanup of stale binaries.
                    }
                }
            }

            var ensuredDirs = new HashSet<string>(StringComparer.Ordinal);
            var preservedFiles = new HashSet<string>(StringComparer.Ordinal);
            var written = 0;
            foreach (var file in payload.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var targetRel = ResolveDeployRelativePath(file.RelativePath, android, disabledMods);
                var remote = $"{loaderRoot}/{targetRel}";
                if (targetRel.EndsWith("/config.json", StringComparison.OrdinalIgnoreCase) &&
                    await fileSystem.ExistsAsync(remote, cancellationToken))
                {
                    preservedFiles.Add(targetRel);
                    written++;
                    continue;
                }

                var parentDir = remote[..remote.LastIndexOf('/')];
                if (ensuredDirs.Add(parentDir))
                    await fileSystem.MakeDirectoriesAsync(parentDir, cancellationToken);
                await fileSystem.WriteAllBytesAsync(
                    remote, payload.BytesOf(file), cancellationToken);
                written++;
                if (written % 5 == 0 || written == payload.Files.Count)
                    progress?.Invoke($"投放 {written}/{payload.Files.Count}",
                        8 + (int)(88.0 * written / payload.Files.Count));
            }

            // 装载状态：检测端读的就是它（version / sdkVersion / bootstrap / 模组清单）。
            // 别人的安装方式（安卓安装器）也写这个文件，所以它同时是「已装」的判定依据。
            var mods = payload.Files
                .Where(f => f.RelativePath.StartsWith("mods/", StringComparison.Ordinal))
                .Select(f => f.RelativePath.Split('/')[1])
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
            // 装载状态：检测端读它，安卓 App 也读它 —— 格式对齐才能互相接手。
            object state = android
                ? new
                {
                    layout = 2,
                    loaderPackage = "com.hiynet.cesiumloader.core",
                    sdkVersion = payload.SdkVersion,
                    bootstrap = MobilePayload.DeviceRelativePath(payload.Bootstrap, true),
                    sdk = MobilePayload.SdkRelative,
                    installedAt = DateTimeOffset.Now.ToString("o"),
                    builtInMods = mods,
                }
                : (object)new
                {
                    version = payload.Version,
                    sdkVersion = payload.SdkVersion,
                    bootstrap = payload.Bootstrap,
                    mods,
                    deployedAt = DateTimeOffset.Now.ToString("o"),
                };
            var manifest = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state,
                new JsonSerializerOptions { WriteIndented = true }));
            var manifestPath = loaderRoot + "/installed.json";
            await fileSystem.WriteAllBytesAsync(manifestPath, manifest, cancellationToken);

            Step("回读校验…", 96, progress);
            var mismatched = new List<string>();
            foreach (var file in payload.Files)
            {
                var relative = ResolveDeployRelativePath(file.RelativePath, android, disabledMods);
                if (preservedFiles.Contains(relative))
                    continue;
                var info = await fileSystem.StatAsync($"{loaderRoot}/{relative}", cancellationToken);
                if (info is null || info.Size != file.Length)
                    mismatched.Add($"{file.RelativePath}({info?.Size.ToString() ?? "缺"}/{file.Length})");
            }

            var manifestInfo = await fileSystem.StatAsync(manifestPath, cancellationToken);
            if (manifestInfo is null || manifestInfo.Size != manifest.Length)
                mismatched.Add($"installed.json({manifestInfo?.Size.ToString() ?? "缺"}/{manifest.Length})");

            if (mismatched.Count > 0)
                return FailWith("回读校验失败（设备上的文件大小不符）：" + string.Join("、", mismatched.Take(8)));

            var version = payload.Version;
            var sdkVersion = payload.SdkVersion;
            _store.Update(Key(target), record =>
            {
                record.Payload.Version = version;
                record.Payload.SdkVersion = sdkVersion;
                record.Payload.At = DateTimeOffset.Now.ToString("o");
            });
            target.Installed = true;
            _targets.Upsert(target);

            Step("载荷投放完成", 100, progress);
            _log?.Info($"载荷投放完成：包名={appId} 目录={loaderRoot} 文件={written} " +
                       $"版本={version} SDK={sdkVersion}");
            return new MobileOperationResult
            {
                Ok = true,
                Message = $"已投放 {written} 个文件到 CesiumLoader（内置 {mods.Count} 个模组）",
                Detail = $"version={version} sdk={sdkVersion} mods={mods.Count} skipped={payload.Skipped}",
            };
        }
    }

    private static string ResolveDeployRelativePath(
        string payloadRelative, bool android, HashSet<string> disabledMods)
    {
        var normalized = MobilePayload.DeviceRelativePath(payloadRelative, android);
        if (normalized.StartsWith("mods/", StringComparison.Ordinal))
        {
            var rest = normalized["mods/".Length..];
            var slash = rest.IndexOf('/');
            var modId = slash >= 0 ? rest[..slash] : rest;
            if (disabledMods.Contains(modId))
                return "disabled_mods/" + rest;
        }
        return normalized;
    }

    /// <summary>
    /// M6：列出设备上的模组。<c>mods/</c> 与 <c>disabled_mods/</c> 都列，启用状态由所在目录决定。
    /// </summary>
    public async Task<IReadOnlyList<MobileModEntry>> ListModsAsync(
        MobileTarget target, CancellationToken cancellationToken = default)
    {
        var opened = await OpenRootAsync(target, cancellationToken)
                     ?? throw new InvalidOperationException("找不到游戏容器（先做一次「检测」确认包名）");

        var (fileSystem, root, _) = opened;
        await using (fileSystem)
        {
            var loaderRoot = root + MobilePaths.LoaderRelative;
            _log?.Info($"列模组：{loaderRoot}");
            var enabled = await ListModsInAsync(fileSystem, loaderRoot, MobilePaths.ModsRelative, true, cancellationToken);
            var disabled = await ListModsInAsync(fileSystem, loaderRoot, MobilePaths.DisabledModsRelative, false, cancellationToken);
            var all = enabled.Concat(disabled).OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
            _log?.Info($"模组 {all.Count} 个（启用 {enabled.Count}、停用 {disabled.Count}）");
            return all;
        }
    }

    /// <summary>
    /// M6：启用 / 停用模组 —— **整个目录**在 <c>mods/</c> 与 <c>disabled_mods/</c> 之间搬移。
    /// 两边同时存在时拒绝（直接 <c>mv</c> 会把目录嵌进去一层）。sidecar 的 <c>enabled</c> 不改：运行期不读它。
    /// </summary>
    public async Task<MobileOperationResult> SetModEnabledAsync(
        MobileTarget target, string modId, bool enabled, Action<string, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modId) || modId.Contains('/') || modId.Contains('\\') || modId.Contains(".."))
            return FailWith($"模组 ID 非法：{modId}");

        var opened = await OpenRootAsync(target, cancellationToken);
        if (opened is null)
            return FailWith("找不到游戏容器（先做一次「检测」确认包名）");

        var (fileSystem, root, _) = opened.Value;
        await using (fileSystem)
        {
            var loaderRoot = root + MobilePaths.LoaderRelative;
            var fromFolder = enabled ? MobilePaths.DisabledModsRelative : MobilePaths.ModsRelative;
            var toFolder = enabled ? MobilePaths.ModsRelative : MobilePaths.DisabledModsRelative;
            var from = $"{loaderRoot}/{fromFolder}/{modId}";
            var to = $"{loaderRoot}/{toFolder}/{modId}";

            Step($"{(enabled ? "启用" : "停用")} {modId}：{fromFolder}/ → {toFolder}/", 20, progress);

            if (!await fileSystem.ExistsAsync(from, cancellationToken))
                return FailWith($"设备上的 {fromFolder}/ 里没有 {modId}");
            if (await fileSystem.ExistsAsync(to, cancellationToken))
                return FailWith($"目标已存在，拒绝搬移以免嵌一层目录：{to}");

            // 搬目录要写两边的父目录：游戏自建的 mods/ 会拒绝
            if (await MobileDeviceAccess.DescribeLoaderWritabilityAsync(fileSystem, loaderRoot, cancellationToken)
                    is { } blocked)
                return FailWith(blocked);

            await fileSystem.MakeDirectoriesAsync(loaderRoot + "/" + toFolder, cancellationToken);
            await fileSystem.RenameAsync(from, to, cancellationToken);

            var moved = await fileSystem.ExistsAsync(to, cancellationToken);
            var gone = !await fileSystem.ExistsAsync(from, cancellationToken);
            if (!moved || !gone)
                return FailWith($"搬移后回读不一致：目标存在={moved}，原位置已清空={gone}");

            Step($"{(enabled ? "已启用" : "已停用")} {modId}", 100, progress);
            _log?.Info($"模组 {modId} → {toFolder}（状态只看目录，sidecar 未改）");
            return new MobileOperationResult
            {
                Ok = true,
                Message = $"{(enabled ? "已启用" : "已停用")} {modId}",
                Detail = toFolder,
            };
        }
    }

    /// <summary>
    /// 删除设备上的某个模组目录（无论位于 <c>mods/</c> 还是 <c>disabled_mods/</c>）。
    /// 安卓加载器目录若为游戏进程自建（2750），删目录前会返回可理解的权限说明。
    /// </summary>
    public async Task<MobileOperationResult> DeleteModAsync(
        MobileTarget target, string modId, Action<string, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modId) || modId.Contains('/') || modId.Contains('\\') ||
            modId.Contains("..") || modId is "." or "..")
            return FailWith($"模组 ID 非法：{modId}");

        var opened = await OpenRootAsync(target, cancellationToken);
        if (opened is null)
            return FailWith("找不到游戏容器（先做一次「检测」确认包名）");

        var (fileSystem, root, _) = opened.Value;
        await using (fileSystem)
        {
            var loaderRoot = root + MobilePaths.LoaderRelative;
            var targets = new List<(string Folder, string Path)>();
            foreach (var folder in new[] { MobilePaths.ModsRelative, MobilePaths.DisabledModsRelative })
            {
                var path = $"{loaderRoot}/{folder}/{modId}";
                if (await fileSystem.ExistsAsync(path, cancellationToken))
                    targets.Add((folder, path));
            }

            if (targets.Count == 0)
                return FailWith($"设备上没有模组 {modId}");

            if (await MobileDeviceAccess.DescribeLoaderWritabilityAsync(fileSystem, loaderRoot, cancellationToken)
                    is { } blocked)
                return FailWith(blocked);

            var removed = new List<string>();
            for (var i = 0; i < targets.Count; i++)
            {
                var (folder, path) = targets[i];
                Step($"正在删除 {folder}/{modId}…", 20 + (i + 1) * 70 / targets.Count, progress);
                await fileSystem.DeleteAsync(path, cancellationToken);
                if (await fileSystem.ExistsAsync(path, cancellationToken))
                    return FailWith($"删除后目录仍在：{path}");
                removed.Add($"{folder}/{modId}");
            }

            Step($"已删除 {modId}", 100, progress);
            _log?.Info($"已删除设备上的模组 {modId}（{string.Join(", ", removed)}）");
            return new MobileOperationResult
            {
                Ok = true,
                Message = $"已删除模组 {modId}",
                Detail = string.Join(", ", removed),
            };
        }
    }

    private static async Task<List<MobileModEntry>> ListModsInAsync(
        IDeviceFileSystem fileSystem, string loaderRoot, string folder, bool enabled, CancellationToken cancellationToken)
    {
        var result = new List<MobileModEntry>();
        IReadOnlyList<string> ids;
        try
        {
            ids = await fileSystem.ListAsync($"{loaderRoot}/{folder}", cancellationToken);
        }
        catch (Exception)
        {
            return result;   // 目录还不存在（没投过载荷 / 没有停用模组）不算错误
        }

        foreach (var id in ids)
        {
            var entry = new MobileModEntry { Id = id, Enabled = enabled, Directory = folder };
            var modRoot = $"{loaderRoot}/{folder}/{id}";
            try
            {
                var sidecar = $"{modRoot}/{id}.json";
                if (await fileSystem.ExistsAsync(sidecar, cancellationToken))
                {
                    using var document = JsonDocument.Parse(await fileSystem.ReadAllBytesAsync(sidecar, cancellationToken));
                    var data = document.RootElement;
                    entry.Name = GetString(data, "name");
                    entry.Version = GetString(data, "version");
                    entry.SdkVersion = GetString(data, "sdkVersion");
                    entry.Description = GetString(data, "description");
                }

                entry.HasConfig = await fileSystem.ExistsAsync($"{modRoot}/config.json", cancellationToken);
            }
            catch (Exception)
            {
                // sidecar 坏了也要能列出来（只是没有名字/版本）
            }

            result.Add(entry);
        }

        return result;
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    /// <summary>
    /// M6：把本地 ZIP 模组包装到设备（<c>CesiumLoader/mods/&lt;ModId&gt;/</c>）。
    /// 已装同 ID 模组时**保留它的 config.json**（玩家配置不能被包覆盖），其余文件按包覆盖。
    /// </summary>
    public async Task<MobileOperationResult> ImportModPackageAsync(
        MobileTarget target, string zipPath, bool enabled = true, Action<string, int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        MobileModPackage package;
        try
        {
            package = MobileModPackage.Read(zipPath);
        }
        catch (Exception ex)
        {
            return FailWith("模组包不可用：" + ex.Message);
        }

        var platform = target.Kind == "ios" ? "ios" : "android";
        Step($"模组包 {package.ModId} {package.Version}：{package.Entries.Count} 个文件", 5, progress);
        if (package.Platforms.Count > 0 &&
            !package.Platforms.Contains(platform, StringComparer.OrdinalIgnoreCase))
        {
            // 平台只影响界面标签，不拦安装（与桌面版一致），但要留痕
            _log?.Info($"注意：包声明平台 [{string.Join(",", package.Platforms)}]，目标 {platform}；平台不作为安装门槛");
        }

        var opened = await OpenRootAsync(target, cancellationToken);
        if (opened is null)
            return FailWith("找不到游戏容器（先做一次「检测」确认包名）");

        var (fileSystem, root, _) = opened.Value;
        await using (fileSystem)
        {
            var loaderRoot = root + MobilePaths.LoaderRelative;
            var folder = enabled ? MobilePaths.ModsRelative : MobilePaths.DisabledModsRelative;
            var otherFolder = enabled ? MobilePaths.DisabledModsRelative : MobilePaths.ModsRelative;

            if (await fileSystem.ExistsAsync($"{loaderRoot}/{otherFolder}/{package.ModId}", cancellationToken))
                return FailWith($"{otherFolder}/ 里已经有 {package.ModId}，先在列表里切到{(enabled ? "停用" : "启用")}再导入，避免两份");

            var modRoot = $"{loaderRoot}/{folder}/{package.ModId}";
            var configPath = modRoot + "/config.json";

            // 只有「新建模组目录」会被游戏自建的 2750 目录挡住；覆盖已有模组照常能写
            if (!await fileSystem.ExistsAsync(modRoot, cancellationToken) &&
                await MobileDeviceAccess.DescribeLoaderWritabilityAsync(fileSystem, loaderRoot, cancellationToken)
                    is { } blocked)
                return FailWith(blocked);

            byte[]? existingConfig = null;
            try
            {
                if (await fileSystem.ExistsAsync(configPath, cancellationToken))
                    existingConfig = await fileSystem.ReadAllBytesAsync(configPath, cancellationToken);
            }
            catch (Exception)
            {
                // 读不到就当没有：下面按包内容写
            }

            await fileSystem.MakeDirectoriesAsync(modRoot, cancellationToken);
            var written = 0;
            foreach (var entry in package.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remote = $"{modRoot}/{entry.RelativePath}";
                await fileSystem.MakeDirectoriesAsync(remote[..remote.LastIndexOf('/')], cancellationToken);
                await fileSystem.WriteAllBytesAsync(remote, entry.Bytes, cancellationToken);
                written++;
                progress?.Invoke($"写入 {written}/{package.Entries.Count}",
                    10 + (int)(80.0 * written / package.Entries.Count));
            }

            if (existingConfig is not null)
            {
                await fileSystem.WriteAllBytesAsync(configPath, existingConfig, cancellationToken);
                _log?.Info($"保留了设备上已有的 config.json（{existingConfig.Length} B）");
            }

            Step("回读校验…", 94, progress);
            var mismatched = new List<string>();
            foreach (var entry in package.Entries)
            {
                if (string.Equals(entry.RelativePath, "config.json", StringComparison.OrdinalIgnoreCase))
                    continue;   // 上面可能已被玩家配置替换回来
                var info = await fileSystem.StatAsync($"{modRoot}/{entry.RelativePath}", cancellationToken);
                if (info is null || info.Size != entry.Bytes.Length)
                    mismatched.Add($"{entry.RelativePath}({info?.Size.ToString() ?? "缺"}/{entry.Bytes.Length})");
            }

            if (mismatched.Count > 0)
                return FailWith("回读校验失败（设备上的文件大小不符）：" + string.Join("、", mismatched.Take(8)));

            Step("导入完成", 100, progress);
            _log?.Info($"模组导入完成：{package.ModId} → {modRoot}（{written} 个文件，保留配置={existingConfig is not null}）");
            return new MobileOperationResult
            {
                Ok = true,
                Message = $"已导入 {package.ModId}（{package.Name} {package.Version}）",
                Detail = $"{written} 个文件 → {folder}/{package.ModId}",
            };
        }
    }

    /// <summary>读设备上某个模组的 <c>config.json</c> 原文与可编辑的标量字段。</summary>
    public async Task<(string Text, IReadOnlyList<MobileConfigField> Fields, string Path)> ReadModConfigAsync(
        MobileTarget target, string modId, string language, CancellationToken cancellationToken = default)
    {
        var opened = await OpenRootAsync(target, cancellationToken)
                     ?? throw new InvalidOperationException("找不到游戏容器（先做一次「检测」确认包名）");

        var (fileSystem, root, _) = opened;
        await using (fileSystem)
        {
            var path = await FindModConfigAsync(fileSystem, root, modId, cancellationToken)
                       ?? throw new InvalidOperationException($"设备上的模组 {modId} 没有 config.json");

            var text = await fileSystem.ReadAllTextAsync(path, cancellationToken);
            var fields = MobileModConfig.Parse(text, language);
            _log?.Info($"读取模组配置 {path}（{fields.Count} 个可编辑字段）");
            return (text, fields, path);
        }
    }

    /// <summary>
    /// 改若干标量字段：**逐个做文本定点替换后再写一次**，不重新序列化。
    /// 写回前要求整段文本仍能解析成 JSON 对象；写完回读比对。
    /// </summary>
    public async Task<MobileOperationResult> WriteModConfigValuesAsync(
        MobileTarget target, string modId, IReadOnlyList<(string Key, string Kind, string Value)> changes,
        CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
            return FailWith("没有要改的字段");

        var opened = await OpenRootAsync(target, cancellationToken);
        if (opened is null)
            return FailWith("找不到游戏容器（先做一次「检测」确认包名）");

        var (fileSystem, root, _) = opened.Value;
        await using (fileSystem)
        {
            var path = await FindModConfigAsync(fileSystem, root, modId, cancellationToken);
            if (path is null)
                return FailWith($"设备上的模组 {modId} 没有 config.json");

            var text = await fileSystem.ReadAllTextAsync(path, cancellationToken);
            try
            {
                foreach (var (key, kind, value) in changes)
                    text = MobileModConfig.SetScalar(text, key, kind, value);
            }
            catch (Exception ex)
            {
                return FailWith("改配置失败：" + ex.Message);
            }

            if (!CanParseObject(text))
                return FailWith("改完的文本解析不成 JSON 对象，已放弃写入（设备上的文件没动）");

            var bytes = Encoding.UTF8.GetBytes(text);
            await fileSystem.WriteAllBytesAsync(path, bytes, cancellationToken);

            var readback = await fileSystem.ReadAllBytesAsync(path, cancellationToken);
            if (!readback.AsSpan().SequenceEqual(bytes))
                return FailWith("回读校验失败：设备上的 config.json 与写入内容不一致");

            _log?.Info($"模组 {modId} 配置已更新：{string.Join("、", changes.Select(c => c.Key))}");
            return new MobileOperationResult
            {
                Ok = true,
                Message = $"已更新 {changes.Count} 个字段",
                Detail = string.Join(", ", changes.Select(c => c.Key)),
            };
        }
    }

    private static async Task<string?> FindModConfigAsync(
        IDeviceFileSystem fileSystem, string root, string modId, CancellationToken cancellationToken)
    {
        var loaderRoot = root + MobilePaths.LoaderRelative;
        foreach (var folder in new[] { MobilePaths.ModsRelative, MobilePaths.DisabledModsRelative })
        {
            var candidate = $"{loaderRoot}/{folder}/{modId}/config.json";
            if (await fileSystem.ExistsAsync(candidate, cancellationToken))
                return candidate;
        }

        return null;
    }

    private static bool CanParseObject(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(MobileModConfig.StripComments(text));
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>只定位容器根：载荷投放不需要 catalog 与代码包。</summary>
    private Task<(IDeviceFileSystem FileSystem, string Root, string AppId)?> OpenRootAsync(
        MobileTarget target, CancellationToken cancellationToken) =>
        MobileDeviceAccess.TryOpenRootAsync(_channel, target, _log, cancellationToken);

    private async Task<(IDeviceFileSystem FileSystem, string Root, string BundleDir, string Inner, string Outer, string BuildHash)?> ResolveAsync(
        MobileTarget target, Action<string, int>? progress, CancellationToken cancellationToken)
    {
        var opened = await MobileDeviceAccess.TryOpenRootAsync(_channel, target, _log, cancellationToken);
        if (opened is null)
            return null;
        var (fileSystem, root, _) = opened.Value;

        Step("读取 catalog…", 10, progress);
        var catalogs = (await fileSystem.ListAsync(root + MobilePaths.AddressablesRelative, cancellationToken))
            .Where(n => n.StartsWith("catalog_", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal))
            .ToList();
        if (catalogs.Count == 0)
        {
            await fileSystem.DisposeAsync();
            return null;
        }
        string? catalogName = null;
        DateTimeOffset newest = DateTimeOffset.MinValue;
        foreach (var name in catalogs)
        {
            var stat = await fileSystem.StatAsync(root + MobilePaths.AddressablesRelative + "/" + name, cancellationToken);
            var modified = stat?.Modified ?? DateTimeOffset.MinValue;
            if (modified >= newest)
            {
                newest = modified;
                catalogName = name;
            }
        }
        catalogName ??= catalogs[^1];
        var catalogBytes = await fileSystem.ReadAllBytesAsync(
            root + MobilePaths.AddressablesRelative + "/" + catalogName, cancellationToken);
        var catalog = AddressablesCatalog.Parse(catalogBytes);
        var inner = catalog.BundleHashFor(RuntimeAsset);
        if (string.IsNullOrEmpty(inner))
        {
            await fileSystem.DisposeAsync();
            return null;
        }

        Step("定位代码包…", 15, progress);
        var bundlesRoot = root + MobilePaths.BundlesRelative;
        string? outer = null;
        if (!string.IsNullOrEmpty(target.BundleOuter) &&
            await fileSystem.ExistsAsync($"{bundlesRoot}/{target.BundleOuter}/{inner}/__data", cancellationToken))
        {
            outer = target.BundleOuter;
        }
        outer ??= await fileSystem.FindBundleOuterAsync(bundlesRoot, inner, cancellationToken);
        if (string.IsNullOrEmpty(outer))
        {
            await fileSystem.DisposeAsync();
            return null;
        }

        return (fileSystem, root, $"{bundlesRoot}/{outer}/{inner}", inner, outer, catalog.BuildResultHash);
    }

    private static bool IsInjected(byte[] bundle)
    {
        try
        {
            var assets = RuntimeAssemblyReader.ReadAllTextAssets(bundle, out _);
            return assets.TryGetValue(RuntimeAsset, out var dll) && PatchMarkerDetector.ContainsMarker(dll);
        }
        catch
        {
            return false;
        }
    }

    private Task<(bool Ok, string Message)> RunPatcherAsync(
        string inputDll, string outputDll, string refsDirectory, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var result = MobileAssemblyPatcher.Bootstrap(
                inputDll,
                outputDll,
                MobilePaths.LoaderRelative,
                BootDll,
                BootType,
                "LoadForGame",
                "root",
                refsDirectory);
            return (result.Ok, result.Message);
        }, cancellationToken);
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

public sealed class MobileOperationResult
{
    public bool Ok { get; init; }
    public string Message { get; init; } = "";
    public string Detail { get; init; } = "";

    public static MobileOperationResult Fail(string message) => new() { Ok = false, Message = message };
}
