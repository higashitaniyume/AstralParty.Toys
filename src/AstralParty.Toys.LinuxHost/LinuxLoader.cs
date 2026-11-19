using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using AstralParty.Toys.Services.Modding;
using FileChange = AstralParty.Toys.LinuxHost.LinuxLoaderFiles.FileChange;
using FileSnapshot = AstralParty.Toys.LinuxHost.LinuxLoaderFiles.FileSnapshot;

namespace AstralParty.Toys.LinuxHost;

internal static partial class LinuxLoader
{
    private const string ReleaseDescriptor = "AstralParty.Toys.LinuxHost.LoaderRelease.json";
    private const string ReleaseArchive = "AstralParty.Toys.LinuxHost.CesiumLoader.zip";
    private const string Repository = "higashitaniyume/CesiumLoader";
    private const string LoaderRoot = ModManager.LoaderFolderName;
    private const string LoaderDll = ModManager.LoaderDllName;
    private const string LoaderConfig = LoaderRoot + "/" + ModManager.ConfigFileName;
    private const string LoaderSdk = LoaderRoot + "/sdk/CesiumLoader.SDK.dll";
    private const string LoaderBootstrap = LoaderRoot + "/bootstrap/CesiumLoader.Bootstrap.dll";
    private const string ProxySymbols = "version.pdb";
    private const string ActiveMods = LoaderRoot + "/" + ModManager.ModsFolderName;
    private const string DisabledMods = LoaderRoot + "/" + ModManager.DisabledModsFolderName;
    private const string ManifestName = ModManager.InstalledManifestFileName;
    private const string ManifestPath = LoaderRoot + "/" + ManifestName;
    private static readonly string[] WorkingDirectories = [ActiveMods, DisabledMods, LoaderRoot + "/logs"];
    private static readonly Lazy<Release> PinnedRelease = new(ReadReleaseDescriptor);

    internal static string AvailableVersion => PinnedRelease.Value.Version.ToString();

    internal static void Apply(string requestType, string gameDirectory)
    {
        if (requestType is not ("linuxInstallLoader" or "linuxUpdateLoader" or "linuxUninstallLoader"))
            throw new ArgumentException("不支持的加载器操作。", nameof(requestType));
        using var game = new LinuxLoaderFiles(gameDirectory);
        if (!game.IsRegularFile("AstralParty_CN.exe") && !game.IsRegularFile("AstralParty_INT.exe") &&
            !game.IsRegularFile("AstralParty.exe"))
            throw new InvalidOperationException("所选目录没有国服或国际服 Steam 游戏主程序。");

        var manifestFile = game.Inspect(ManifestPath, readMetadata: true);
        var manifest = manifestFile is null ? null : ReadManifest(manifestFile.Bytes!);
        var uninstall = requestType == "linuxUninstallLoader";
        var release = PinnedRelease.Value;
        if (!uninstall && manifest is not null && manifest.Version > release.Version)
            throw new InvalidOperationException($"已安装的 CesiumLoader {manifest.Version} 比支持的 {release.Version} 更新，未执行降级。");

        var proxy = game.Inspect(LoaderDll);
        if (proxy is not null && !MatchesRecord(manifest, LoaderDll, proxy))
            throw new InvalidOperationException("游戏目录里的 version.dll 来源不明或已被替换，未改动任何文件。");

        // A game-writable inventory is not evidence of ownership. Only manifests from
        // the exact verified bundle or explicitly pinned official predecessors are trusted.
        using var archive = !uninstall || manifest?.Version == release.Version ? OpenVerifiedArchive(release) : null;
        var packageManifestBytes = archive is null ? null : ReadPackagedManifest(archive);
        if (manifest is not null && !MatchesPublishedManifest(release, manifest.Version, manifestFile!.Sha256, packageManifestBytes))
            throw new InvalidDataException("无法验证已安装的 CesiumLoader 发布清单，未改动任何文件。");

        if (uninstall)
        {
            if (manifest is null)
            {
                if (game.Inspect(LoaderConfig) is not null)
                    throw new InvalidOperationException("已有加载器配置，但缺少可验证的安装清单，未删除任何文件。");
                return;
            }
            Uninstall(game, manifest, manifestFile!, proxy);
            return;
        }

        var package = ReadManifest(packageManifestBytes!);
        if (package.Version != release.Version)
            throw new InvalidDataException("CesiumLoader 发布包与固定版本不一致。");
        var entries = VerifyPackageContents(archive!, package);
        InstallOrUpdate(game, manifest, manifestFile, proxy, package, packageManifestBytes!, entries);
    }

    private static void InstallOrUpdate(LinuxLoaderFiles game, Manifest? installed, FileSnapshot? manifestFile,
        FileSnapshot? proxy, Manifest package, byte[] manifestBytes, Dictionary<string, ZipArchiveEntry> entries)
    {
        foreach (var directory in WorkingDirectories) game.DirectoryExists(directory); // Reject links before staging.

        var disabledProxy = installed is not null && proxy is null;
        var disabledMods = new Dictionary<string, bool>(StringComparer.Ordinal);
        var changes = new List<FileChange>();
        var defaultConfig = ReadEntry(entries[LoaderConfig]);
        var sdkVersion = ValidateDefaultConfig(defaultConfig);

        foreach (var (relative, entry) in entries)
        {
            if (relative == LoaderDll) continue; // Enable the loader only after all components are ready.
            var target = ResolveTarget(game, relative, disabledMods);
            var mod = ModId(relative);
            var isSidecar = mod is not null && relative == $"{ActiveMods}/{mod}/{mod}.json";
            var before = game.Inspect(target, readMetadata: relative == LoaderConfig || isSidecar);
            if (before?.Sha256 == package.Files[relative])
            {
                if (!MatchesRecord(installed, relative, before)) throw ForeignFile(target);
                continue;
            }

            if (relative == LoaderConfig && before is not null)
            {
                var updated = RaiseSdkVersion(before.Bytes!, sdkVersion);
                if (updated is not null)
                {
                    if (installed is null || !installed.Files.ContainsKey(relative)) throw ForeignFile(target);
                    AddBytes(changes, target, before, updated);
                }
                continue; // Player settings and comments are not replaced by release defaults.
            }

            if (isSidecar && before is not null)
            {
                var updated = MergeSidecar(before.Bytes!, ReadEntry(entry), mod!);
                if (updated is not null)
                {
                    // An edited enabled flag is not itself proof of ownership; require the
                    // original inventory and the verified DLL of this same mod installation.
                    var modDll = $"{ActiveMods}/{mod}/{mod}.dll";
                    if (!MatchesRecord(installed, relative, before) &&
                        (installed is null || !installed.Files.ContainsKey(relative) ||
                         !MatchesRecord(installed, modDll, game.Inspect(ResolveTarget(game, modDll, disabledMods)))))
                        throw ForeignFile(target);
                    AddBytes(changes, target, before, updated);
                }
                continue;
            }

            if (before is not null && !MatchesRecord(installed, relative, before)) throw ForeignFile(target);
            changes.Add(new FileChange(target, before, output => CopyEntry(entry, output)));
        }

        if (installed is not null)
            AddOwnedRemovals(game, installed, disabledMods, changes, package.Files);
        if (!disabledProxy && proxy?.Sha256 != package.Files[LoaderDll])
            changes.Add(new FileChange(LoaderDll, proxy, output => CopyEntry(entries[LoaderDll], output)));
        if (manifestFile?.Sha256 != Convert.ToHexString(SHA256.HashData(manifestBytes)))
            AddBytes(changes, ManifestPath, manifestFile, manifestBytes);
        // Keep the original release inventory. User-edited configuration/sidecars deliberately
        // don't acquire a new package hash, and are therefore preserved during uninstall.
        game.Commit(changes, WorkingDirectories);
    }

    private static void Uninstall(LinuxLoaderFiles game, Manifest installed, FileSnapshot manifestFile, FileSnapshot? proxy)
    {
        var changes = new List<FileChange>();
        if (proxy is not null) changes.Add(new FileChange(LoaderDll, proxy, null));
        AddOwnedRemovals(game, installed, new Dictionary<string, bool>(StringComparer.Ordinal), changes);
        changes.Add(new FileChange(ManifestPath, manifestFile, null));
        game.Commit(changes, []);
        game.PruneEmptyDirectories(changes.SelectMany(change => ParentDirectories(change.Path)).Concat(WorkingDirectories));
    }

    private static void AddOwnedRemovals(LinuxLoaderFiles game, Manifest installed, Dictionary<string, bool> disabledMods,
        List<FileChange> changes, Dictionary<string, string>? keep = null)
    {
        foreach (var (relative, expectedHash) in installed.Files)
        {
            if (relative == LoaderDll || (keep is not null && keep.ContainsKey(relative))) continue;
            var target = ResolveTarget(game, relative, disabledMods);
            var before = game.Inspect(target);
            if (before?.Sha256 == expectedHash)
                changes.Add(new FileChange(target, before, null));
        }
    }

    private static IEnumerable<string> ParentDirectories(string path)
    {
        for (var slash = path.LastIndexOf('/'); slash > 0; slash = path.LastIndexOf('/', slash - 1))
            yield return path[..slash];
    }

    private static void AddBytes(List<FileChange> changes, string target, FileSnapshot? before, byte[] bytes) =>
        changes.Add(new FileChange(target, before, output => output.Write(bytes)));

    private static bool MatchesRecord(Manifest? manifest, string relative, FileSnapshot? file) =>
        file is not null && manifest is not null && manifest.Files.TryGetValue(relative, out var hash) && file.Sha256 == hash;

    private static InvalidOperationException ForeignFile(string path) =>
        new($"现有文件来源不明或内容已变更，未覆盖：{path}");

    private static string ResolveTarget(LinuxLoaderFiles game, string relative, Dictionary<string, bool> disabledMods)
    {
        var mod = ModId(relative);
        if (mod is null) return relative;
        if (!disabledMods.TryGetValue(mod, out var disabled))
        {
            disabled = game.DirectoryExists($"{DisabledMods}/{mod}");
            if (disabled && game.DirectoryExists($"{ActiveMods}/{mod}"))
                throw new InvalidOperationException($"模组 {mod} 同时存在于启用和停用目录，未改动任何文件。");
            disabledMods.Add(mod, disabled);
        }
        return disabled ? DisabledMods + relative[ActiveMods.Length..] : relative;
    }

    private static string? ModId(string path)
    {
        if (!path.StartsWith(ActiveMods + "/", StringComparison.Ordinal)) return null;
        var slash = path.IndexOf('/', ActiveMods.Length + 1);
        return slash < 0 ? null : path[(ActiveMods.Length + 1)..slash];
    }

    private static bool IsSafeArchivePath(string path)
    {
        if (path.Length > 512) return false;
        return path.Split('/').All(part => part.Length is > 0 and <= 120 && part[^1] != '.' &&
            part.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'));
    }

    private static bool IsPackageFile(string path)
    {
        if (!IsSafeArchivePath(path)) return false;
        if (path is LoaderDll or ProxySymbols or LoaderConfig or LoaderSdk or LoaderBootstrap or
            "CesiumLoader-LICENSE.txt" or "CesiumLoader-NOTICE.txt" or
            LoaderRoot + "/CesiumLoader-LICENSE.txt" or LoaderRoot + "/CesiumLoader-NOTICE.txt" or
            LoaderRoot + "/sdk/CesiumLoader.SDK.pdb" or LoaderRoot + "/sdk/CesiumLoader.SDK.xml" or
            LoaderRoot + "/bootstrap/CesiumLoader.Bootstrap.pdb") return true;
        var mod = ModId(path);
        if (mod is null || !mod.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-')) return false;
        var member = path[(ActiveMods.Length + mod.Length + 2)..];
        return member == mod + ".dll" || member == mod + ".pdb" || member == mod + ".json" ||
            member.StartsWith("data/", StringComparison.Ordinal) || member.StartsWith("sounds/", StringComparison.Ordinal);
    }

    private static Manifest ReadManifest(byte[] bytes)
    {
        using var json = ReadJsonObject(bytes, ManifestName);
        var root = json.RootElement;
        var version = ReadString(root, "version");
        if (!TryReleaseVersion(version, out var parsed) || ReadString(root, "name") != "CesiumLoader" ||
            ReadString(root, "layout") != "game-dir" || ReadString(root, "repo") != Repository ||
            ReadString(root, "tag") != "modloader-" + version ||
            !root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("CesiumLoader 安装清单无效，未改动任何文件。");
        var inventory = new Dictionary<string, string>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in files.EnumerateObject())
        {
            var path = property.Name;
            var sha256 = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
            if (!IsPackageFile(path) || !paths.Add(path) || !IsDigest(sha256) || inventory.Count >= 4096)
                throw new InvalidDataException($"CesiumLoader 安装清单包含不安全的文件记录：{path}");
            inventory.Add(path, sha256!.ToUpperInvariant());
        }
        if (!inventory.ContainsKey(LoaderDll) || !inventory.ContainsKey(LoaderConfig))
            throw new InvalidDataException("CesiumLoader 安装清单缺少加载器或配置的哈希。");
        return new Manifest(parsed!, inventory);
    }

    private static Dictionary<string, ZipArchiveEntry> VerifyPackageContents(ZipArchive archive, Manifest manifest)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expandedSize = 0;
        foreach (var entry in archive.Entries)
        {
            var isDirectory = entry.FullName.EndsWith('/');
            var path = isDirectory ? entry.FullName[..^1] : entry.FullName;
            var type = (entry.ExternalAttributes >> 16) & 0xf000;
            if (!IsSafeArchivePath(path) || !paths.Add(path) || paths.Count > 4096 ||
                entry.Length > LinuxLoaderFiles.MaxFileBytes || (expandedSize += entry.Length) > 128L * 1024 * 1024 ||
                (type != 0 && type != (isDirectory ? 0x4000 : 0x8000)))
                throw new InvalidDataException($"CesiumLoader 发布包包含不安全的条目：{entry.FullName}");
            if (isDirectory || path is ManifestName or "CHANGELOG.md" or "README.txt") continue;
            if (!manifest.Files.TryGetValue(path, out var expected))
                throw new InvalidDataException($"CesiumLoader 发布包含有未列入清单的文件：{path}");
            using var input = entry.Open();
            if (Convert.ToHexString(SHA256.HashData(input)) != expected)
                throw new InvalidDataException($"CesiumLoader 发布包文件校验失败：{path}");
            entries.Add(path, entry);
        }
        if (entries.Count != manifest.Files.Count || !entries.ContainsKey(LoaderSdk) || !entries.ContainsKey(LoaderBootstrap) ||
            !entries.Keys.Any(path => ModId(path) is not null && path.EndsWith(".dll", StringComparison.Ordinal)))
            throw new InvalidDataException("CesiumLoader 发布包缺少清单文件、SDK、引导程序或模组。");
        return entries;
    }

    private static ZipArchive OpenVerifiedArchive(Release release)
    {
        var stream = ReadResource(ReleaseArchive);
        try
        {
            if (stream.Length is <= 0 or > (16L * 1024 * 1024) ||
                Convert.ToHexString(SHA256.HashData(stream)) != release.Sha256)
                throw new InvalidDataException("内置 CesiumLoader 发布包的 SHA-256 不匹配，未写入游戏目录。");
            stream.Position = 0;
            return new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static bool MatchesPublishedManifest(Release release, Version version, string hash, byte[]? packagedManifest = null)
    {
        if (version != release.Version)
            return release.LegacyManifestDigests.TryGetValue(version, out var expected) && hash == expected;
        if (packagedManifest is null)
        {
            using var archive = OpenVerifiedArchive(release);
            packagedManifest = ReadPackagedManifest(archive);
        }
        return hash == Convert.ToHexString(SHA256.HashData(packagedManifest));
    }

    private static byte[] ReadPackagedManifest(ZipArchive archive) =>
        ReadEntry(archive.GetEntry(ManifestName)
            ?? throw new InvalidDataException("CesiumLoader 发布包缺少安装清单。"));

    private static Release ReadReleaseDescriptor()
    {
        using var stream = ReadResource(ReleaseDescriptor);
        using var json = JsonDocument.Parse(stream);
        var root = json.RootElement;
        var hash = ReadString(root, "sha256");
        if (!TryReleaseVersion(ReadString(root, "version"), out var version) || !IsDigest(hash) ||
            !root.TryGetProperty("legacyManifestSha256", out var legacy) || legacy.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("内置 CesiumLoader 发布版本说明无效。");
        var legacyDigests = new Dictionary<Version, string>();
        foreach (var entry in legacy.EnumerateObject())
        {
            var digest = entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString() : null;
            if (!TryReleaseVersion(entry.Name, out var earlier) || earlier >= version || !IsDigest(digest) ||
                !legacyDigests.TryAdd(earlier!, digest!.ToUpperInvariant()))
                throw new InvalidDataException("内置 CesiumLoader 旧版发布清单的校验记录无效。");
        }
        return new Release(version!, hash!.ToUpperInvariant(), legacyDigests);
    }

    private static Stream ReadResource(string name) => typeof(LinuxLoader).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidDataException($"Linux 宿主缺少 CesiumLoader 发布资源：{name}。请重新构建安装包。");

    private static bool IsDigest(string? digest) => digest is { Length: 64 } && digest.All(Uri.IsHexDigit);

    private static bool TryReleaseVersion(string? text, out Version? version) =>
        Version.TryParse(text, out version) && version.Build >= 0 && version.Revision < 0 && version.ToString() == text;

    private static byte[] ReadEntry(ZipArchiveEntry entry)
    {
        if (entry.Length is < 0 or > 1024 * 1024)
            throw new InvalidDataException($"CesiumLoader 发布包的配置或清单文件大小不合法：{entry.FullName}");
        var bytes = new byte[(int)entry.Length];
        using var stream = entry.Open();
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw new InvalidDataException($"CesiumLoader 发布包条目长度不一致：{entry.FullName}");
        return bytes;
    }

    private static void CopyEntry(ZipArchiveEntry entry, Stream destination)
    {
        using var input = entry.Open();
        input.CopyTo(destination);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private sealed record Release(Version Version, string Sha256, Dictionary<Version, string> LegacyManifestDigests);
    private sealed record Manifest(Version Version, Dictionary<string, string> Files);
}
