using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using AstralParty.Toys.Models;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class ModManager
{
    private string HistoryRoot => Path.Combine(ProfileDirectory, ToysPaths.InstallHistoryFolderName);
    private static readonly JsonSerializerOptions HistoryJson = new() { WriteIndented = true };

    public sealed class InstallationRecord
    {
        public string GameDirectory { get; set; } = "";
        public string Version { get; set; } = "";
        public string LoaderHash { get; set; } = "";
        public string PackageHash { get; set; } = "";
        public bool Disabled { get; set; }
        public bool Removed { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private string GameHistoryDirectory(string directory)
    {
        var canonical = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        return Path.Combine(HistoryRoot, "games", HashBytes(System.Text.Encoding.UTF8.GetBytes(canonical)));
    }

    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>是否是 64 位十六进制 SHA256（安装清单 / 安装记录里的值可能是空的或损坏的）。</summary>
    private static bool IsSha256Hex(string value) => Regex.IsMatch(value, "^[a-fA-F0-9]{64}$");

    private string BlobPath(string hash)
    {
        if (!IsSha256Hex(hash)) throw new InvalidDataException("备份哈希无效。");
        return Path.Combine(HistoryRoot, "files", hash.ToLowerInvariant());
    }

    private string CacheBytes(byte[] bytes)
    {
        var hash = HashBytes(bytes);
        var path = BlobPath(hash);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path) || !MatchesHash(path, hash)) AtomicWrite(path, bytes);
        if (!MatchesHash(path, hash)) throw new IOException("本地备份校验失败，已停止操作。");
        return hash;
    }

    private static void AtomicWrite(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private InstallationRecord? ReadRetainedInstallation(string directory)
    {
        var path = Path.Combine(GameHistoryDirectory(directory), "current.json");
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<InstallationRecord>(File.ReadAllText(path));
    }

    private void SaveRetainedInstallation(InstallationRecord record)
    {
        AtomicWrite(Path.Combine(GameHistoryDirectory(record.GameDirectory), "current.json"),
            JsonSerializer.SerializeToUtf8Bytes(record, HistoryJson));
    }

    private InstallationRecord CaptureInstallation(string directory)
    {
        var previous = ReadRetainedInstallation(directory);
        var record = new InstallationRecord
        {
            GameDirectory = Path.GetFullPath(directory),
            Version = ReadInstalledVersion(directory),
            LoaderHash = previous?.LoaderHash is { Length: > 0 } savedHash ? savedHash : ReadManifestLoaderHash(directory),
            PackageHash = previous?.PackageHash ?? "",
            Disabled = previous?.Disabled == true,
            Removed = previous?.Removed == true,
            UpdatedUtc = DateTime.UtcNow
        };
        if (record.Version.Length == 0) record.Version = previous?.Version ?? "";
        if (record.Version.Length == 0 && MatchesEmbeddedLoader(Path.Combine(directory, LoaderDllName)))
            record.Version = EmbeddedVersion;
        var root = Path.Combine(directory, LoaderFolderName);
        var files = new List<string>();
        // 发布包落在游戏 exe 目录的文件：version.dll / version.pdb / NOTICE / LICENSE 按名字认定，
        // README.txt / CHANGELOG.md 这类通用名只有在安装记录里出现过（= 本工具落过）才算 ——
        // 备份与回滚都不该把玩家自己的同名文件卷进来。
        foreach (var name in RootFilesOwnedByLoader(previous).Distinct(StringComparer.OrdinalIgnoreCase))
            if (File.Exists(Path.Combine(directory, name))) files.Add(Path.Combine(directory, name));
        if (Directory.Exists(root))
        {
            // Runtime logs, bridge files and speed requests are not installation artifacts.
            files.AddRange(Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly));
            foreach (var folder in new[] { "sdk", "bootstrap", "mods" })
            {
                var component = Path.Combine(root, folder);
                if (Directory.Exists(component)) files.AddRange(Directory.EnumerateFiles(component, "*", SearchOption.AllDirectories));
            }
        }
        foreach (var file in files)
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("安装文件包含符号链接，无法可靠备份。");
            var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            record.Files[relative] = CacheBytes(File.ReadAllBytes(file));
        }
        if (record.Files.TryGetValue(LoaderDllName, out var hash)) record.LoaderHash = hash;
        return record;
    }

    private RetainedOperation BeginRetainedOperation(string directory, string action, params byte[][] incoming)
    {
        PruneHistoryIfDue();   // 历史就是在这里长的：每次操作前顺手清一次旧的
        foreach (var bytes in incoming) CacheBytes(bytes);
        var before = CaptureInstallation(directory);
        return new RetainedOperation(this, before, action, incoming.Select(HashBytes).ToArray());
    }

    private sealed class RetainedOperation : IDisposable
    {
        private readonly ModManager _owner;
        private readonly InstallationRecord _before;
        private readonly string _path;
        private readonly string _action;
        private readonly string[] _inputs;
        private bool _done;
        public RetainedOperation(ModManager owner, InstallationRecord before, string action, string[] inputs)
        {
            _owner = owner; _before = before; _action = action; _inputs = inputs;
            _path = Path.Combine(owner.GameHistoryDirectory(before.GameDirectory), "operations",
                DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N") + ".json");
            Write("running", null);
        }
        private void Write(string state, InstallationRecord? after) => AtomicWrite(_path,
            JsonSerializer.SerializeToUtf8Bytes(new { action = _action, state, inputs = _inputs, before = _before, after, timeUtc = DateTime.UtcNow }, HistoryJson));
        public void Complete(string? version = null, string? loaderHash = null, bool? disabled = null, bool removed = false, string? packageHash = null)
        {
            var after = _owner.CaptureInstallation(_before.GameDirectory);
            if (version is not null) after.Version = version;
            if (loaderHash is not null) after.LoaderHash = loaderHash;
            if (packageHash is not null) after.PackageHash = packageHash;
            else if (version is not null && version != _before.Version) after.PackageHash = "";
            if (disabled is not null) after.Disabled = disabled.Value;
            after.Removed = removed;
            _owner.SaveRetainedInstallation(after);
            Write("done", after);
            _done = true;
        }
        public void Dispose()
        {
            if (!_done) Write("failed", null);
        }
    }

    /// <summary>
    /// 不切换当前游戏，查某个目录里的加载器（卸载弹窗要列出所有目录）。
    /// 只有加载器文件夹、没有 dll（停用 / 缺失）时也算本工具的，和 Uninstall 的判断一致。
    /// </summary>
    public (bool HasFiles, bool Managed, string Version) InspectLoader(string gameDirectory)
    {
        var dll = Path.Combine(gameDirectory, LoaderDllName);
        var dllExists = File.Exists(dll);
        if (!dllExists && !Directory.Exists(Path.Combine(gameDirectory, LoaderFolderName))) return (false, false, "");
        return (true, !dllExists || ClassifyLoaderOrigin(gameDirectory, dll).Length > 0, ReadCurrentInstallationVersion(gameDirectory));
    }

    private string ReadCurrentInstallationVersion(string directory)
    {
        var version = ReadInstalledVersion(directory);
        if (version.Length > 0) return version;
        var record = ReadRetainedInstallation(directory);
        return record is { Removed: false } ? record.Version : "";
    }

    /// <summary>加载器是否处于「停用但保留」状态：历史记录说停用过、version.dll 不在、**加载器目录还在**。
    /// 目录都不在了（玩家手工删干净）就不算停用 —— 否则重装时不会写回 version.dll，界面却说装好了。</summary>
    private bool IsRetainedDisabled(string directory) => ReadRetainedInstallation(directory) is { Disabled: true, Removed: false }
        && !File.Exists(Path.Combine(directory, LoaderDllName))
        && Directory.Exists(Path.Combine(directory, LoaderFolderName));

    private void ApplyRetainedStatus(ModStatus status)
    {
        if (status.GameDirectory.Length == 0) return;
        var record = ReadRetainedInstallation(status.GameDirectory);
        if (!status.LoaderPresent && status.LoaderRootExists && record?.Removed != true)
        {
            status.InstalledVersion = ReadInstalledVersion(status.GameDirectory);
            if (status.InstalledVersion.Length == 0) status.InstalledVersion = record?.Version ?? "";
            status.LoaderDisabled = record?.Disabled == true;
            status.CanRestoreLoader = status.InstalledVersion.Length > 0 || record?.LoaderHash.Length > 0;
            status.LoaderMissing = true;
        }
        // 代理在、核心缺：这也是一种"可恢复"状态（恢复会同时补回核心）。
        // 不动 LoaderMissing —— 那是"代理不存在"的语义，界面靠它区分停用/缺失。
        if (status.CoreMissing)
        {
            status.CanRestoreLoader = status.InstalledVersion.Length > 0
                || record?.LoaderHash is { Length: > 0 }
                || record?.PackageHash is { Length: > 0 };
        }
    }

    /// <summary>备份并仅删除游戏根目录的 version.dll（代理）。CesiumLoader.dll（核心）与加载器目录
    /// 都保留：没有代理就不会加载核心，效果等同停用；想彻底清掉请用「卸载」。</summary>
    public string DisableLoader(string gameDirectory)
    {
        RequireLoaderGameDirectory(gameDirectory);
        var dll = Path.Combine(gameDirectory, LoaderDllName);
        if (!File.Exists(dll)) return "version.dll 已缺失；其他文件未改动，可点击恢复。";
        if (ClassifyLoaderOrigin(gameDirectory, dll).Length == 0)
            throw new InvalidOperationException("version.dll 来源不明，不能作为 CesiumLoader 停用。请先确认文件来源。");
        using var operation = BeginRetainedOperation(gameDirectory, "disable-loader");
        try { File.Delete(dll); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new IOException("删除 version.dll 失败，请退出游戏并检查文件权限后重试。", ex); }
        operation.Complete(disabled: true);
        return "仅 version.dll（代理）已删除；CesiumLoader.dll（核心）与模组、配置、SDK、日志都保留 —— "
             + "没有代理时游戏不会加载核心，效果等同停用。" +
            (SpeedhackManager.IsGameRunning() ? "当前游戏进程仍加载着加载器，下次启动才停用。" : "可随时点击恢复 version.dll。");
    }

    private static void RequireLoaderGameDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
            || SpeedhackManager.ContainsGameExe(directory) is null)
            throw new InvalidOperationException("请选择有效的游戏主程序目录。");
    }

    public static string VersionPackageUrl(string version)
    {
        if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?$"))
            throw new InvalidOperationException("当前加载器版本未知或格式无效，无法下载对应发布包。请重新安装或更新加载器。");
        return $"https://github.com/higashitaniyume/CesiumLoader/releases/download/modloader-{version}/cesium-loader-{version}.zip";
    }

    /// <summary>只恢复对应版本的 DLL；本地无可用文件时下载固定版本发布包。</summary>
    public Task<string> RestoreLoaderAsync(string gameDirectory, HttpClient? client, CancellationToken cancellationToken = default)
        => RestoreLoaderAsync(gameDirectory, mirrorType: null, customMirrorUrl: null, client, cancellationToken);

    /// <summary>只恢复对应版本的 DLL；本地无可用文件时下载固定版本发布包。</summary>
    public Task<string> RestoreLoaderAsync(string gameDirectory, CancellationToken cancellationToken)
        => RestoreLoaderAsync(gameDirectory, mirrorType: null, customMirrorUrl: null, client: null, cancellationToken);

    /// <summary>只恢复对应版本的 DLL；本地无可用文件时通过镜像源下载固定版本发布包。</summary>
    public async Task<string> RestoreLoaderAsync(
        string gameDirectory,
        string? mirrorType = null,
        string? customMirrorUrl = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        RequireLoaderGameDirectory(gameDirectory);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        cancellationToken = timeout.Token;
        var target = Path.Combine(gameDirectory, LoaderDllName);
        var coreTarget = Path.Combine(gameDirectory, CoreDllName);
        if (!Directory.Exists(Path.Combine(gameDirectory, LoaderFolderName)))
            throw new InvalidOperationException("加载器目录已缺失，请使用安装功能恢复完整组件。");
        var record = ReadRetainedInstallation(gameDirectory);
        if (record?.Removed == true) throw new InvalidOperationException("加载器已完整卸载，请重新安装。");

        // 这次安装应由几个原生文件：以游戏目录里的安装清单为准，退回安装记录。
        // 旧版单 DLL 安装两处都没有核心条目 → coreExpected 为空，下面的恢复行为与以前完全一致。
        var coreExpected = ReadManifestFileHash(gameDirectory, CoreDllName);
        if (!IsSha256Hex(coreExpected))
        {
            coreExpected = "";
            if (record is not null && record.Files.TryGetValue(CoreDllName, out var recordedCore) && IsSha256Hex(recordedCore))
                coreExpected = recordedCore;
        }
        // 核心在盘上但哈希对不上（被杀软截断 / 被替换过）按"需要修复"处理，恢复会覆盖它。
        var coreDamaged = coreExpected.Length > 0 && File.Exists(coreTarget) && !MatchesHash(coreTarget, coreExpected);
        var coreNeeded = coreExpected.Length > 0 && (!File.Exists(coreTarget) || coreDamaged);

        var proxyPresent = File.Exists(target);
        if (proxyPresent && !coreNeeded)
            throw new InvalidOperationException("游戏目录已有 version.dll，未覆盖任何文件。请刷新状态并确认来源。");
        // 「只补核心」这条路径不动代理，但要求盘上那个代理确实是本工具装的 ——
        // 来源不明时不该往游戏目录里写任何东西（那种情况应该整包重装）。
        if (proxyPresent && ClassifyLoaderOrigin(gameDirectory, target).Length == 0)
            throw new InvalidOperationException("游戏目录里的 version.dll 不是本工具装的（来源不明），未写入任何文件。请重新安装完整加载器。");

        var installedVersion = ReadInstalledVersion(gameDirectory);
        var installedHash = ReadManifestLoaderHash(gameDirectory);
        var version = installedVersion.Length > 0 ? installedVersion : record?.Version ?? "";
        var expected = CurrentLoaderHash(gameDirectory, record);
        byte[]? dll = null;        // version.dll（代理）
        byte[]? coreDll = null;    // CesiumLoader.dll（加载器核心）
        var source = "本地备份";
        var packageHash = record?.PackageHash ?? "";

        // 1) 本地备份：按清单哈希寻址，代理与核心各取各的
        if (expected.Length > 0)
        {
            var blob = BlobPath(expected);
            if (File.Exists(blob) && MatchesHash(blob, expected)) dll = File.ReadAllBytes(blob);
        }
        if (coreNeeded)
        {
            var coreBlob = BlobPath(coreExpected);
            if (File.Exists(coreBlob) && MatchesHash(coreBlob, coreExpected)) coreDll = File.ReadAllBytes(coreBlob);
        }

        // 2) 还缺 → 用缓存下来的发布包（必须描述"当前这次安装"，不能是上一个构建）
        if ((dll is null || (coreNeeded && coreDll is null)) && packageHash.Length > 0 && record?.Version == version)
        {
            var blob = BlobPath(packageHash);
            if (File.Exists(blob) && MatchesHash(blob, packageHash))
            {
                try
                {
                    var restored = ReadRestorePackage(File.ReadAllBytes(blob), version);
                    var proxyMatches = expected.Length == 0
                                       || string.Equals(HashBytes(restored.Proxy), expected, StringComparison.OrdinalIgnoreCase);
                    // 核心只接受与清单哈希一致的那一份：缓存的包可能是同版本的另一个构建。
                    var coreMatches = restored.Core is not null
                                      && string.Equals(HashBytes(restored.Core), coreExpected, StringComparison.OrdinalIgnoreCase);
                    if (dll is null && proxyMatches) dll = restored.Proxy;
                    if (coreNeeded && coreDll is null && coreMatches) coreDll = restored.Core;
                }
                catch (InvalidDataException)
                {
                    // 缓存包坏了 / 不属于这次安装：当作没有缓存，走下面的下载
                }
            }
        }

        // 3) 还缺 → 下载当前版本的完整发布包（下载来的包就是权威内容，核心一并取）
        if (dll is null || (coreNeeded && coreDll is null))
        {
            var rawUrl = VersionPackageUrl(version);
            var url = string.IsNullOrEmpty(mirrorType)
                ? rawUrl
                : GitHubMirrorHelper.ApplyMirror(rawUrl, mirrorType, customMirrorUrl);
            using var response = await (client ?? Http).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"无法下载当前版本 {version} 的加载器包：HTTP {(int)response.StatusCode}。可重试或使用更新功能安装新版。");
            var package = await ReadLimitedAsync(response.Content, 64 * 1024 * 1024, cancellationToken).ConfigureAwait(false);
            var restored = ReadRestorePackage(package, version);
            if (dll is null) dll = restored.Proxy;
            if (coreNeeded && restored.Core is not null) coreDll = restored.Core;
            // Embedded and release DLLs of the same version can be different builds.
            // The exact release version and its own manifest hash are authoritative for online recovery.
            packageHash = CacheBytes(package);
            source = "对应版本的在线发布包";
        }
        if (dll is null) throw new InvalidDataException("无法确定要恢复的 version.dll：本地备份与发布包都对不上。");

        CacheBytes(dll);
        if (coreDll is not null) CacheBytes(coreDll);
        cancellationToken.ThrowIfCancellationRequested();

        // 状态漂移检查：下载期间安装状态变了就放弃（「只补核心」的场景里代理存在是预期状态）
        if ((!proxyPresent && File.Exists(target))
            || ReadInstalledVersion(gameDirectory) != installedVersion
            || ReadManifestLoaderHash(gameDirectory) != installedHash
            || ReadRetainedInstallation(gameDirectory)?.UpdatedUtc != record?.UpdatedUtc)
            throw new InvalidOperationException("下载期间安装状态发生变化，请刷新后重试。");

        using var operation = coreDll is null
            ? BeginRetainedOperation(gameDirectory, "restore-loader", dll)
            : BeginRetainedOperation(gameDirectory, "restore-loader", dll, coreDll);
        if (!proxyPresent)
        {
            var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, dll);
                File.Move(temporary, target, false);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        if (coreDll is not null)
        {
            // 覆盖写：这条路径也用来修好被杀软截断的核心文件
            var coreTemp = coreTarget + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(coreTemp, coreDll);
                File.Move(coreTemp, coreTarget, true);
            }
            finally { if (File.Exists(coreTemp)) File.Delete(coreTemp); }
        }
        // proxyPresent 时不写代理、也不改它的哈希记录，让 Complete 用盘上的实际文件重新采集。
        operation.Complete(version, proxyPresent ? null : HashBytes(dll), disabled: false, packageHash: packageHash);
        var restoredFiles = coreDll is null ? "version.dll" : "version.dll + CesiumLoader.dll（加载器核心）";
        return $"已从{source}恢复 {restoredFiles}（{(version.Length > 0 ? version : "原安装版本")}），重启游戏后生效。";
    }

    private string CurrentLoaderHash(string directory, InstallationRecord? record)
    {
        var manifestHash = ReadManifestLoaderHash(directory);
        var manifest = Path.Combine(directory, LoaderFolderName, InstalledManifestFileName);
        var relative = LoaderFolderName + "/" + InstalledManifestFileName;
        if (record is not null && record.LoaderHash.Length > 0 && record.Version == ReadInstalledVersion(directory)
            && record.Files.TryGetValue(relative, out var hash) && MatchesHash(manifest, hash))
            return record.LoaderHash;
        return manifestHash.Length > 0 ? manifestHash : record?.LoaderHash ?? "";
    }

    /// <summary>从发布包里取出可恢复的原生文件：代理 <c>version.dll</c> 与（拆分后才有）核心
    /// <c>CesiumLoader.dll</c>。旧包没有核心条目 → <c>Core</c> 为 null，恢复只写代理，行为与以前一致。</summary>
    private static (byte[] Proxy, byte[]? Core) ReadRestorePackage(byte[] package, string version)
    {
        var manifest = ParsePackageManifest(package) ?? throw new InvalidDataException("发布包缺少版本清单。");
        if (!string.Equals(manifest.Version, version, StringComparison.Ordinal)) throw new InvalidDataException("下载包版本与当前安装版本不一致。");
        ValidatePackage(package, manifest);
        if (!manifest.Files.TryGetValue(LoaderDllName, out var hash) || !IsSha256Hex(hash))
            throw new InvalidDataException("发布包未提供 version.dll 的有效哈希。");
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        var proxy = ReadVerifiedEntry(zip, LoaderDllName, hash);
        byte[]? core = null;
        if (manifest.Files.TryGetValue(CoreDllName, out var coreHash) && IsSha256Hex(coreHash))
            core = ReadVerifiedEntry(zip, CoreDllName, coreHash);
        return (proxy, core);
    }

    private static byte[] ReadVerifiedEntry(ZipArchive zip, string name, string expectedHash)
    {
        var entry = zip.GetEntry(name) ?? throw new InvalidDataException($"发布包缺少 {name}。");
        var bytes = ReadEntryBytes(entry);
        if (!string.Equals(HashBytes(bytes), expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"发布包 {name} 哈希校验失败。");
        return bytes;
    }

    internal static async Task<byte[]> ReadLimitedAsync(HttpContent content, int maximum, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > maximum) throw new InvalidDataException("下载内容超过大小限制。");
        using var input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (output.Length + count > maximum) throw new InvalidDataException("下载内容超过大小限制。");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}
