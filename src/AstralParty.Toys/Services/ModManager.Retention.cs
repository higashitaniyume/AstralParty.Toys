using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services;

public sealed partial class ModManager
{
    private string HistoryRoot => Path.Combine(_profileDirectory, "modloader-history");
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
    private string BlobPath(string hash)
    {
        if (!Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("备份哈希无效。");
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
        foreach (var name in new[] { LoaderDllName, "version.pdb" })
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

    private static void SetInstalledLoaderHash(string directory, string hash)
    {
        var path = Path.Combine(directory, LoaderFolderName, InstalledManifestFileName);
        if (!File.Exists(path)) return;
        var manifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        manifest["files"] ??= new System.Text.Json.Nodes.JsonObject();
        manifest["files"]![LoaderDllName] = hash.ToLowerInvariant();
        AtomicWrite(path, System.Text.Encoding.UTF8.GetBytes(manifest.ToJsonString(HistoryJson)));
    }

    private string ReadCurrentInstallationVersion(string directory)
    {
        var version = ReadInstalledVersion(directory);
        if (version.Length > 0) return version;
        var record = ReadRetainedInstallation(directory);
        return record is { Removed: false } ? record.Version : "";
    }

    private bool IsRetainedDisabled(string directory) => ReadRetainedInstallation(directory) is { Disabled: true, Removed: false }
        && !File.Exists(Path.Combine(directory, LoaderDllName));

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
    }

    /// <summary>备份并仅删除游戏根目录的 version.dll。</summary>
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
        return "仅 version.dll 已删除，模组、配置、SDK 和日志已保留。" +
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
    public async Task<string> RestoreLoaderAsync(string gameDirectory, HttpClient? client = null, CancellationToken cancellationToken = default)
    {
        RequireLoaderGameDirectory(gameDirectory);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        cancellationToken = timeout.Token;
        var target = Path.Combine(gameDirectory, LoaderDllName);
        if (File.Exists(target)) throw new InvalidOperationException("游戏目录已有 version.dll，未覆盖任何文件。请刷新状态并确认来源。");
        if (!Directory.Exists(Path.Combine(gameDirectory, LoaderFolderName)))
            throw new InvalidOperationException("加载器目录已缺失，请使用安装功能恢复完整组件。");
        var record = ReadRetainedInstallation(gameDirectory);
        if (record?.Removed == true) throw new InvalidOperationException("加载器已完整卸载，请重新安装。");
        var installedVersion = ReadInstalledVersion(gameDirectory);
        var installedHash = ReadManifestLoaderHash(gameDirectory);
        var version = installedVersion.Length > 0 ? installedVersion : record?.Version ?? "";
        var expected = CurrentLoaderHash(gameDirectory, record);
        byte[]? dll = null;
        var source = "本地备份";
        var packageHash = record?.PackageHash ?? "";
        if (expected.Length > 0)
        {
            var blob = BlobPath(expected);
            if (File.Exists(blob) && MatchesHash(blob, expected)) dll = File.ReadAllBytes(blob);
        }
        if (dll is null && packageHash.Length > 0 && record?.Version == version)
        {
            var blob = BlobPath(packageHash);
            if (File.Exists(blob) && MatchesHash(blob, packageHash))
            {
                try
                {
                    dll = ReadRestorePackage(File.ReadAllBytes(blob), version);
                    // A cached package must describe the current installation, not a previous build.
                    if (expected.Length > 0 && !string.Equals(HashBytes(dll), expected, StringComparison.OrdinalIgnoreCase)) dll = null;
                }
                catch (InvalidDataException) { dll = null; }
            }
        }
        if (dll is null)
        {
            var url = VersionPackageUrl(version);
            using var response = await (client ?? Http).GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"无法下载当前版本 {version} 的加载器包：HTTP {(int)response.StatusCode}。可重试或使用更新功能安装新版。");
            var package = await ReadLimitedAsync(response.Content, 64 * 1024 * 1024, cancellationToken).ConfigureAwait(false);
            dll = ReadRestorePackage(package, version);
            // Embedded and release DLLs of the same version can be different builds.
            // The exact release version and its own manifest hash are authoritative for online recovery.
            packageHash = CacheBytes(package);
            source = "对应版本的在线发布包";
        }
        CacheBytes(dll);
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(target) || ReadInstalledVersion(gameDirectory) != installedVersion
            || ReadManifestLoaderHash(gameDirectory) != installedHash
            || ReadRetainedInstallation(gameDirectory)?.UpdatedUtc != record?.UpdatedUtc)
            throw new InvalidOperationException("下载期间安装状态发生变化，请刷新后重试。");
        using var operation = BeginRetainedOperation(gameDirectory, "restore-loader", dll);
        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, dll);
            File.Move(temporary, target, false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        operation.Complete(version, HashBytes(dll), disabled: false, packageHash: packageHash);
        return $"已从{source}恢复 version.dll（{(version.Length > 0 ? version : "原安装版本")}），重启游戏后生效。";
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

    private static byte[] ReadRestorePackage(byte[] package, string version)
    {
        var manifest = ParsePackageManifest(package) ?? throw new InvalidDataException("发布包缺少版本清单。");
        if (!string.Equals(manifest.Version, version, StringComparison.Ordinal)) throw new InvalidDataException("下载包版本与当前安装版本不一致。");
        ValidatePackage(package, manifest);
        if (!manifest.Files.TryGetValue(LoaderDllName, out var hash) || !Regex.IsMatch(hash, "^[a-fA-F0-9]{64}$"))
            throw new InvalidDataException("发布包未提供 version.dll 的有效哈希。");
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        var entry = zip.GetEntry(LoaderDllName) ?? throw new InvalidDataException("发布包缺少 version.dll。");
        var dll = ReadEntryBytes(entry);
        if (!string.Equals(HashBytes(dll), hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("发布包 DLL 哈希校验失败。");
        return dll;
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
