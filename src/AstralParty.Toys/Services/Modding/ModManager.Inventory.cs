using System.Security.Cryptography;
using System.Text.Json;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Common;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class ModManager
{
    public const string LoaderDllName = "version.dll";
    public const string LoaderFolderName = "AstralParty_ModLoader";
    public const string ModsFolderName = "mods";
    /// <summary>停用的 mod 搬到这里：加载器只扫 mods，等于彻底不进游戏，DLL 也不会被游戏进程占用。</summary>
    public const string DisabledModsFolderName = "disabled_mods";
    public const string SdkFolderName = "sdk";
    public const string LogsFolderName = "logs";
    public const string ConfigFileName = "doorstop_config.json";
    public const string SdkDllName = "CesiumLoader.SDK.dll";

    // ============================== mod 目录（启用 / 停用） ==============================

    /// <summary>启用的 mod 目录：<c>AstralParty_ModLoader\mods</c>。</summary>
    public static string ModsRoot(string gameDirectory) => Path.Combine(gameDirectory, LoaderFolderName, ModsFolderName);

    /// <summary>停用的 mod 目录：<c>AstralParty_ModLoader\disabled_mods</c>。</summary>
    public static string DisabledModsRoot(string gameDirectory) => Path.Combine(gameDirectory, LoaderFolderName, DisabledModsFolderName);

    internal static ModStatus ReadDiskStatus(string directory, bool gameExeFound, bool? gameRunning,
        out InstalledManifest manifest)
    {
        var status = new ModStatus { GameDirectory = directory, GameExeFound = gameExeFound, GameRunning = gameRunning };
        manifest = new InstalledManifest("", "");
        if (directory.Length == 0) return status;

        if (InspectionFiles.DirectoryExists(directory))
        {
            status.LoaderPath = Path.Combine(directory, LoaderDllName);
            status.LoaderPresent = InspectionFiles.FileExists(status.LoaderPath);
            status.Installed = status.LoaderPresent;
            if (TryReadInstalledSdkVersion(directory, out var sdkVersion)) status.InstalledSdkVersion = sdkVersion;
        }

        var root = Path.Combine(directory, LoaderFolderName);
        status.LoaderRoot = root;
        status.LoaderRootExists = InspectionFiles.DirectoryExists(root);
        if (status.LoaderRootExists)
        {
            manifest = ReadInstalledManifest(directory);
            if (status.LoaderPresent) status.InstalledVersion = manifest.Version;
            status.Mods = ScanMods(Path.Combine(root, ModsFolderName), Path.Combine(root, DisabledModsFolderName));
            status.Sdk = ScanMods(Path.Combine(root, SdkFolderName));
        }
        return status;
    }

    /// <summary>Checks a SHA-256 claim against a file's actual contents.</summary>
    internal static bool MatchesHash(string path, string expectedHex)
    {
        try
        {
            if (expectedHex.Length != 64) return false;
            using var stream = InspectionFiles.OpenRead(path, maxBytes: 64 * 1024 * 1024);
            return stream is not null && CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(stream), Convert.FromHexString(expectedHex));
        }
        catch
        {
            return false;
        }
    }

    public const string InstalledManifestFileName = "cesium-loader.json";

    internal readonly record struct InstalledManifest(string Version, string LoaderHash);

    // The on-disk manifest, not the SDK version or an older log, identifies the installation.
    internal static InstalledManifest ReadInstalledManifest(string gameDirectory)
    {
        try
        {
            var text = InspectionFiles.ReadText(Path.Combine(gameDirectory, LoaderFolderName, InstalledManifestFileName));
            if (text is not null)
            {
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;
                var version = ReadString(root, "version");
                var hash = root.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Object
                    ? ReadString(files, LoaderDllName) : "";
                return new InstalledManifest(version, hash);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            // Missing or invalid metadata is not evidence of a version or owner.
        }
        return new InstalledManifest("", "");

        static string ReadString(JsonElement element, string key) =>
            element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    }

    /// <summary>扫描 mod 目录。给了 <paramref name="disabledDirectory"/> 时把停用的 mod 一起列出来：
    /// 启用的排在前面，停用的强制 <see cref="ModEntryInfo.Enabled"/> = false 并标记来源目录。</summary>
    private static List<ModEntryInfo> ScanMods(string directory, string? disabledDirectory = null)
    {
        var list = ScanModsIn(directory, disabled: false);
        if (string.IsNullOrEmpty(disabledDirectory)) return list;
        foreach (var entry in ScanModsIn(disabledDirectory, disabled: true))
        {
            if (list.Any(m => string.Equals(m.Name, entry.Name, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add(entry);
        }
        return list;
    }

    private static List<ModEntryInfo> ScanModsIn(string directory, bool disabled)
    {
        var list = new List<ModEntryInfo>();
        try
        {
            if (!InspectionFiles.DirectoryExists(directory)) return list;
            // 新布局: {ModId}\{ModId}.dll (每 mod 一个文件夹, sidecar 在文件夹内)
            foreach (var sub in Directory.EnumerateDirectories(directory).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
            {
                var dirName = Path.GetFileName(sub);
                var dllPath = Path.Combine(sub, dirName + ".dll");
                if (!InspectionFiles.FileExists(dllPath)) continue;
                list.Add(ScanModEntry(dllPath, dirName, disabled));
            }
            // 旧布局兼容: 根下平铺的 .dll (sidecar 在同级)
            foreach (var file in Directory.EnumerateFiles(directory, "*.dll").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                if (OperatingSystem.IsLinux() && !InspectionFiles.FileExists(file)) continue;
                if (list.Any(m => string.Equals(m.Name, Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase)))
                    continue;
                list.Add(ScanModEntry(file, null, disabled));
            }
        }
        catch
        {
            // 扫描失败按空列表处理
        }
        return list;
    }

    private static ModEntryInfo ScanModEntry(string file, string? directoryName, bool disabled = false)
    {
        var info = new FileInfo(file);
        var entry = new ModEntryInfo
        {
            Name = Path.GetFileNameWithoutExtension(file),
            FileName = Path.GetFileName(file),
            DirectoryName = directoryName,
            SizeBytes = info.Length,
            ModifiedUtc = info.LastWriteTimeUtc,
            DisabledFolder = disabled
        };
        // 同名 sidecar(新布局: mod 文件夹内 {Name}.json; 旧布局: 同目录 {Name}.json):
        // {id,name,version,author,description,permissions,sdkVersion,dependencies}
        var sidecar = Path.ChangeExtension(file, ".json");
        try
        {
            var text = InspectionFiles.ReadText(sidecar);
            if (text is null) return entry;
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            entry.Id = root.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
            entry.DisplayName = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            entry.Version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
            entry.Author = root.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "";
            entry.Description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            entry.SdkVersion = root.TryGetProperty("sdkVersion", out var s) ? s.GetString() ?? "" : "";
            if (root.TryGetProperty("platforms", out var plats) && plats.ValueKind == JsonValueKind.Array)
            {
                var declared = new List<string>();
                foreach (var item in plats.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String) declared.Add(item.GetString() ?? "");
                }
                entry.Platforms = ModPlatforms.Normalize(declared);
            }
            entry.Enabled = !root.TryGetProperty("enabled", out var en) || en.ValueKind != JsonValueKind.False;
            entry.Permissions = root.TryGetProperty("permissions", out var p) && p.ValueKind == JsonValueKind.Number
                ? p.GetInt32() : 0;
            if (root.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
            {
                foreach (var dep in deps.EnumerateArray())
                {
                    var id = dep.TryGetProperty("id", out var dId) ? dId.GetString() ?? "" : "";
                    var minV = dep.TryGetProperty("minVersion", out var dMin) ? dMin.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(id))
                        entry.Dependencies.Add(new ModDependencyInfo { Id = id, MinVersion = minV });
                }
            }
        }
        catch
        {
            // An unreadable sidecar does not hide the DLL.
        }
        return entry;
    }

}
