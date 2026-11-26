using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services.Game;

public sealed record GameDataCleanupItem(
    string Id,
    string Title,
    string Description,
    long Bytes,
    int FileCount,
    bool SafeToClean,
    string? BlockedReason,
    bool DefaultSelected,
    string? LatestWriteTime,
    IReadOnlyList<string> Paths);

public sealed record GameDataCleanupStatus(
    bool Available,
    string? Error,
    string Company,
    string? Product,
    string? GameDirectory,
    string? GameDataDirectory,
    string? ActiveCatalogVersion,
    bool CacheRedirectedToGameDir,
    string? GameAddressablesBundlesPath,
    long GameAddressablesBundlesBytes,
    bool GameRunning,
    long TotalReclaimableBytes,
    IReadOnlyList<GameDataCleanupItem> Items);

public sealed record GameDataCleanupResult(
    bool Ok,
    string Message,
    long FreedBytes,
    int DeletedFiles,
    string? Warning = null,
    GameDataCleanupStatus? Status = null);

/// <summary>
/// 识别并清理 C 盘 LocalLow 中的旧版 Unity Addressables 废弃缓存与旧版 catalog，
/// 以及游戏目录内不再被当前 catalog 引用的过期 bundle 文件夹。
/// 严格保留当前激活版本 catalog、玩家存档、BnSdk、回放与多开档案。
/// </summary>
public static class GameDataCleanupService
{
    public const string ItemLegacyBundles = "legacy_addressables_bundles";
    public const string ItemLegacyCatalogs = "legacy_catalogs";
    public const string ItemGameOrphanBundles = "game_dir_orphan_bundles";

    private static readonly Regex CatalogVersionRegex = new(
        @"catalog_(?<v>[0-9A-Za-z._\-]+)\.hash",
        RegexOptions.Compiled);

    private static readonly Regex Hex32Regex = new(
        @"\b[0-9a-fA-F]{32}\b",
        RegexOptions.Compiled);

    private static readonly TimeSpan DefaultOrphanMinAge = TimeSpan.FromHours(1);

    /// <summary>扫描当前游戏数据与 C 盘残留缓存状态。</summary>
    public static GameDataCleanupStatus Inspect(
        string? gameDirectory,
        string? localLowRoot = null,
        Func<bool>? isGameRunning = null,
        TimeSpan? orphanMinAge = null)
    {
        var running = GameDataMigrationService.SafeCheckRunning(gameDirectory, isGameRunning);
        var low = string.IsNullOrWhiteSpace(localLowRoot)
            ? AccountProfileService.LocalLowRoot()
            : Path.GetFullPath(localLowRoot.Trim());

        if (string.IsNullOrWhiteSpace(low))
        {
            return EmptyUnavailable("无法定位系统 LocalLow 目录。", running);
        }

        string? normalizedGameDir = null;
        string? dataDirectory = null;
        string company = "";
        string product = "";

        if (!string.IsNullOrWhiteSpace(gameDirectory) && Directory.Exists(gameDirectory))
        {
            normalizedGameDir = Path.GetFullPath(gameDirectory.Trim());
            dataDirectory = FindDataDirectory(normalizedGameDir);
            if (AccountProfileService.TryReadAppInfo(normalizedGameDir, out var appCompany, out var appProduct))
            {
                company = appCompany;
                product = appProduct;
            }
        }

        if (string.IsNullOrEmpty(company))
        {
            var defaultFeimo = Path.Combine(low, "feimo");
            var defaultCn = Path.Combine(defaultFeimo, "AstralParty_CN");
            if (Directory.Exists(defaultCn))
            {
                company = "feimo";
                product = "AstralParty_CN";
            }
            else if (Directory.Exists(defaultFeimo))
            {
                company = "feimo";
                product = Directory.GetDirectories(defaultFeimo)
                    .Select(Path.GetFileName)
                    .FirstOrDefault(n => !string.IsNullOrEmpty(n) && !n.EndsWith("_profiles", StringComparison.OrdinalIgnoreCase)) ?? "";
            }
        }

        if (string.IsNullOrEmpty(company) || string.IsNullOrEmpty(product))
        {
            return EmptyUnavailable(
                "未检测到游戏目录或本地游戏数据，请先在「设置 → 游戏目录」中添加游戏。",
                running);
        }

        string? activeCatalogVersion = null;
        var cacheRedirectedToGameDir = false;
        if (!string.IsNullOrEmpty(dataDirectory))
        {
            var settingsPath = Path.Combine(dataDirectory, "StreamingAssets", "aa", "settings.json");
            ReadAddressablesSettings(settingsPath, out activeCatalogVersion, out cacheRedirectedToGameDir);
        }

        var localProductDir = Path.Combine(low, company, product);
        var localAaDir = Path.Combine(localProductDir, "com.unity.addressables");
        string? gameBundlesPath = null;
        long gameBundlesBytes = 0;

        if (!string.IsNullOrEmpty(dataDirectory))
        {
            var candidate = Path.Combine(dataDirectory, "com.unity.addressables", "AssetBundles");
            if (Directory.Exists(candidate))
            {
                gameBundlesPath = candidate;
                var (gbBytes, _, _, _) = GameDataMigrationService.ScanDirectoriesStats(new[] { candidate });
                gameBundlesBytes = gbBytes;
            }
        }

        var items = new List<GameDataCleanupItem>();

        // 1. C 盘废弃 Addressables AssetBundles（含多开档案下的旧缓存）
        var legacyBundleDirs = CollectLegacyAssetBundlesDirs(low, company, product);
        if (legacyBundleDirs.Count > 0)
        {
            var (bytes, files, _, latestUtc) = GameDataMigrationService.ScanDirectoriesStats(legacyBundleDirs);
            if (bytes > 0 || files > 0)
            {
                var safe = cacheRedirectedToGameDir;
                items.Add(new GameDataCleanupItem(
                    Id: ItemLegacyBundles,
                    Title: "C 盘旧版热更资源缓存 (AssetBundles)",
                    Description: safe
                        ? "当前游戏版本已将热更资源缓存迁入游戏安装目录，C 盘此处为旧版本遗留资源，可安全清理以释放空间。"
                        : "检测到 C 盘存在 AssetBundles 资源缓存，但尚未确认当前游戏已将缓存迁至游戏目录。",
                    Bytes: bytes,
                    FileCount: files,
                    SafeToClean: safe,
                    BlockedReason: safe ? null : "未检测到游戏目录 settings.json 开启目录重定向（m_CacheDirectoryOverride），暂不建议自动删除。请先确认已在「设置 → 游戏目录」选择新版游戏，或使用下方目录联接迁移。",
                    DefaultSelected: safe && bytes > 0,
                    LatestWriteTime: FormatDate(latestUtc),
                    Paths: legacyBundleDirs));
            }
        }

        // 2. C 盘旧版本 catalog_<旧版本>.*（严格保留当前激活版本）
        if (Directory.Exists(localAaDir) && !string.IsNullOrWhiteSpace(activeCatalogVersion))
        {
            var oldCatalogFiles = CollectLegacyCatalogFiles(localAaDir, activeCatalogVersion);
            if (oldCatalogFiles.Count > 0)
            {
                long catBytes = 0;
                var latestUtc = DateTime.MinValue;
                foreach (var f in oldCatalogFiles)
                {
                    try
                    {
                        var info = new FileInfo(f);
                        catBytes += info.Length;
                        if (info.LastWriteTimeUtc > latestUtc) latestUtc = info.LastWriteTimeUtc;
                    }
                    catch { }
                }

                items.Add(new GameDataCleanupItem(
                    Id: ItemLegacyCatalogs,
                    Title: "旧版本资源索引文件 (catalog_*)",
                    Description: $"保留当前使用的 catalog_{activeCatalogVersion}.*，清理更早版本的残留索引与备份文件。",
                    Bytes: catBytes,
                    FileCount: oldCatalogFiles.Count,
                    SafeToClean: true,
                    BlockedReason: null,
                    DefaultSelected: catBytes > 0,
                    LatestWriteTime: FormatDate(latestUtc),
                    Paths: oldCatalogFiles));
            }
        }

        // 3. 游戏目录内不被当前 catalog 引用的过期孤儿 bundle
        if (!string.IsNullOrEmpty(gameBundlesPath) && !string.IsNullOrWhiteSpace(activeCatalogVersion))
        {
            var activeCatalogJson = Path.Combine(localAaDir, $"catalog_{activeCatalogVersion}.json");
            var orphanDirs = CollectGameDirOrphanBundles(gameBundlesPath, activeCatalogJson, orphanMinAge ?? DefaultOrphanMinAge);
            if (orphanDirs.Count > 0)
            {
                var (orphanBytes, orphanFiles, _, latestUtc) = GameDataMigrationService.ScanDirectoriesStats(orphanDirs);
                if (orphanBytes > 0 || orphanFiles > 0)
                {
                    items.Add(new GameDataCleanupItem(
                        Id: ItemGameOrphanBundles,
                        Title: "游戏目录内过期旧版资源 (AssetBundles)",
                        Description: $"游戏更新不会自动删除旧版本资源；以下子目录未在当前 catalog_{activeCatalogVersion}.json 中引用。",
                        Bytes: orphanBytes,
                        FileCount: orphanFiles,
                        SafeToClean: true,
                        BlockedReason: null,
                        DefaultSelected: orphanBytes > 0,
                        LatestWriteTime: FormatDate(latestUtc),
                        Paths: orphanDirs));
                }
            }
        }

        var reclaimable = items.Where(i => i.SafeToClean).Sum(i => i.Bytes);

        return new GameDataCleanupStatus(
            Available: true,
            Error: null,
            Company: company,
            Product: product,
            GameDirectory: normalizedGameDir,
            GameDataDirectory: dataDirectory,
            ActiveCatalogVersion: activeCatalogVersion,
            CacheRedirectedToGameDir: cacheRedirectedToGameDir,
            GameAddressablesBundlesPath: gameBundlesPath,
            GameAddressablesBundlesBytes: gameBundlesBytes,
            GameRunning: running,
            TotalReclaimableBytes: reclaimable,
            Items: items);
    }

    /// <summary>清理用户勾选且经过安全校验的残留缓存项。</summary>
    public static async Task<GameDataCleanupResult> CleanAsync(
        string? gameDirectory,
        IReadOnlyCollection<string> selectedItemIds,
        bool toRecycleBin = false,
        string? localLowRoot = null,
        Func<bool>? isGameRunning = null,
        TimeSpan? orphanMinAge = null,
        CancellationToken cancellationToken = default)
    {
        var initial = Inspect(gameDirectory, localLowRoot, isGameRunning, orphanMinAge);
        if (!initial.Available)
        {
            return new GameDataCleanupResult(false, initial.Error ?? "无法识别游戏数据目录。", 0, 0, Status: initial);
        }
        if (initial.GameRunning)
        {
            return new GameDataCleanupResult(false, "检测到游戏正在运行，请先完全退出游戏后再执行清理。", 0, 0, Status: initial);
        }

        var selectedSet = new HashSet<string>(selectedItemIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var targets = initial.Items.Where(i => selectedSet.Contains(i.Id) && i.SafeToClean).ToList();
        if (targets.Count == 0)
        {
            return new GameDataCleanupResult(false, "没有可清理的已选安全项目。", 0, 0, Status: initial);
        }

        return await Task.Run(() =>
        {
            if (GameDataMigrationService.SafeCheckRunning(gameDirectory, isGameRunning))
            {
                var runningStatus = Inspect(gameDirectory, localLowRoot, isGameRunning, orphanMinAge);
                return new GameDataCleanupResult(false, "检测到游戏正在运行，已中止清理。", 0, 0, Status: runningStatus);
            }

            long freedBytes = 0;
            var deletedFiles = 0;
            var errors = new List<string>();

            foreach (var item in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.Equals(item.Id, ItemLegacyCatalogs, StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var filePath in item.Paths)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!File.Exists(filePath)) continue;
                        long len = 0;
                        try { len = new FileInfo(filePath).Length; } catch { }

                        if (DeleteFileSafe(filePath, toRecycleBin, out var fileErr))
                        {
                            freedBytes += len;
                            deletedFiles++;
                        }
                        else if (!string.IsNullOrEmpty(fileErr))
                        {
                            errors.Add($"{Path.GetFileName(filePath)}: {fileErr}");
                        }
                    }
                    continue;
                }

                // 目录类清理（AssetBundles / 孤儿版本子目录）：只算本项内实得释放量
                var (beforeBytes, beforeFiles, _, _) = GameDataMigrationService.ScanDirectoriesStats(item.Paths);
                var hadAnyFailure = false;

                foreach (var dirPath in item.Paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!Directory.Exists(dirPath)) continue;

                    if (GameDataMigrationService.IsDirectoryReparsePoint(dirPath))
                    {
                        errors.Add($"安全拦截：跳过目录联接 {dirPath}");
                        hadAnyFailure = true;
                        continue;
                    }

                    if (!DeleteDirectorySafe(dirPath, toRecycleBin, out var dirErr))
                    {
                        hadAnyFailure = true;
                        if (!string.IsNullOrEmpty(dirErr))
                            errors.Add($"{Path.GetFileName(dirPath)}: {dirErr}");
                        continue;
                    }

                    if (string.Equals(item.Id, ItemGameOrphanBundles, StringComparison.OrdinalIgnoreCase))
                    {
                        TryDeleteEmptyParent(dirPath);
                    }
                }

                if (!hadAnyFailure)
                {
                    freedBytes += beforeBytes;
                    deletedFiles += beforeFiles;
                }
                else
                {
                    var (afterBytes, afterFiles, _, _) = GameDataMigrationService.ScanDirectoriesStats(item.Paths);
                    freedBytes += Math.Max(0, beforeBytes - afterBytes);
                    deletedFiles += Math.Max(0, beforeFiles - afterFiles);
                }
            }

            var finalStatus = Inspect(gameDirectory, localLowRoot, isGameRunning, orphanMinAge);
            var warning = errors.Count > 0 ? string.Join("；", errors.Take(3)) : null;
            var modeText = toRecycleBin ? "已移入回收站" : "已清理完成";
            var msg = freedBytes > 0 || deletedFiles > 0
                ? $"{modeText}，共处理 {deletedFiles} 个文件（释放 {GameDataMigrationService.FormatBytes(freedBytes)}）。"
                : $"{modeText}。";

            return new GameDataCleanupResult(
                Ok: errors.Count == 0 || freedBytes > 0,
                Message: msg,
                FreedBytes: freedBytes,
                DeletedFiles: deletedFiles,
                Warning: warning,
                Status: finalStatus);
        }, CancellationToken.None).ConfigureAwait(false);
    }

    private static GameDataCleanupStatus EmptyUnavailable(string error, bool gameRunning) =>
        new(
            Available: false,
            Error: error,
            Company: "",
            Product: null,
            GameDirectory: null,
            GameDataDirectory: null,
            ActiveCatalogVersion: null,
            CacheRedirectedToGameDir: false,
            GameAddressablesBundlesPath: null,
            GameAddressablesBundlesBytes: 0,
            GameRunning: gameRunning,
            TotalReclaimableBytes: 0,
            Items: Array.Empty<GameDataCleanupItem>());

    private static string? FindDataDirectory(string gameDirectory)
    {
        try
        {
            foreach (var exe in Directory.GetFiles(gameDirectory, "*.exe", SearchOption.TopDirectoryOnly))
            {
                var candidate = Path.Combine(gameDirectory, Path.GetFileNameWithoutExtension(exe) + "_Data");
                if (Directory.Exists(candidate)) return candidate;
            }
            foreach (var dir in Directory.GetDirectories(gameDirectory, "*_Data", SearchOption.TopDirectoryOnly))
            {
                if (File.Exists(Path.Combine(dir, "app.info"))) return dir;
            }
        }
        catch { }
        return null;
    }

    private static void ReadAddressablesSettings(string settingsPath, out string? activeCatalogVersion, out bool cacheRedirectedToGameDir)
    {
        activeCatalogVersion = null;
        cacheRedirectedToGameDir = false;
        if (!File.Exists(settingsPath)) return;

        try
        {
            var raw = File.ReadAllText(settingsPath);
            var versionMatch = CatalogVersionRegex.Match(raw);
            if (versionMatch.Success)
            {
                activeCatalogVersion = versionMatch.Groups["v"].Value;
            }

            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("m_ExtraInitializationData", out var extraArr) ||
                extraArr.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var entry in extraArr.EnumerateArray())
            {
                var id = entry.TryGetProperty("m_Id", out var idEl) ? idEl.GetString() : null;
                if (!string.Equals(id, "CacheInitialization", StringComparison.OrdinalIgnoreCase)) continue;
                var mData = entry.TryGetProperty("m_Data", out var dataEl) ? dataEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(mData)) continue;

                using var inner = JsonDocument.Parse(mData);
                if (inner.RootElement.TryGetProperty("m_CacheDirectoryOverride", out var ovEl))
                {
                    var ov = ovEl.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(ov) &&
                        !ov.Contains("persistentDataPath", StringComparison.OrdinalIgnoreCase))
                    {
                        cacheRedirectedToGameDir = true;
                    }
                }
            }
        }
        catch
        {
            // settings.json 解析失败按保守处理（不放行自动清理）
        }
    }

    private static List<string> CollectLegacyAssetBundlesDirs(string localLowRoot, string company, string product)
    {
        var dirs = new List<string>();
        var mainBundles = Path.Combine(localLowRoot, company, product, "com.unity.addressables", "AssetBundles");
        if (Directory.Exists(mainBundles) && !GameDataMigrationService.IsDirectoryReparsePoint(mainBundles))
            dirs.Add(mainBundles);

        var profilesRoot = Path.Combine(localLowRoot, company, product + "_profiles");
        if (Directory.Exists(profilesRoot))
        {
            try
            {
                foreach (var profileDir in Directory.EnumerateDirectories(profilesRoot))
                {
                    var profileBundles = Path.Combine(profileDir, "com.unity.addressables", "AssetBundles");
                    if (Directory.Exists(profileBundles) && !GameDataMigrationService.IsDirectoryReparsePoint(profileBundles))
                        dirs.Add(profileBundles);
                }
            }
            catch { }
        }

        return dirs;
    }

    private static List<string> CollectLegacyCatalogFiles(string localAaDir, string activeCatalogVersion)
    {
        var keepPrefix = "catalog_" + activeCatalogVersion + ".";
        var oldFiles = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(localAaDir, "catalog_*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(file);
                if (name.StartsWith(keepPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                oldFiles.Add(file);
            }
        }
        catch { }
        return oldFiles;
    }

    private static List<string> CollectGameDirOrphanBundles(string gameBundlesDir, string activeCatalogJsonPath, TimeSpan minAge)
    {
        if (!File.Exists(activeCatalogJsonPath)) return new List<string>();

        HashSet<string> activeHashes;
        try
        {
            var json = File.ReadAllText(activeCatalogJsonPath);
            activeHashes = new HashSet<string>(
                Hex32Regex.Matches(json).Select(m => m.Value),
                StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new List<string>();
        }

        // 保护校验：合法 catalog 至少应包含大量 32 位 hex hash
        if (activeHashes.Count < 10) return new List<string>();

        var cutoffUtc = minAge <= TimeSpan.Zero ? DateTime.MaxValue : DateTime.UtcNow - minAge;
        var matchedCount = 0;
        var candidates = new List<string>();

        try
        {
            foreach (var bundleDir in Directory.EnumerateDirectories(gameBundlesDir))
            {
                if (GameDataMigrationService.IsDirectoryReparsePoint(bundleDir)) continue;

                foreach (var versionDir in Directory.EnumerateDirectories(bundleDir))
                {
                    if (GameDataMigrationService.IsDirectoryReparsePoint(versionDir)) continue;

                    var hashName = Path.GetFileName(versionDir);
                    if (!IsHex32(hashName)) continue;

                    if (activeHashes.Contains(hashName))
                    {
                        matchedCount++;
                        continue;
                    }

                    if (minAge > TimeSpan.Zero)
                    {
                        var newestWrite = GetNewestWriteUtc(versionDir);
                        if (newestWrite > cutoffUtc) continue;
                    }

                    candidates.Add(versionDir);
                }
            }
        }
        catch
        {
            return new List<string>();
        }

        // 必须至少有一个现役 bundle 命中 catalog，才证明格式匹配、允许识别孤儿目录
        return matchedCount > 0 ? candidates : new List<string>();
    }

    private static bool IsHex32(string name)
    {
        if (name.Length != 32) return false;
        foreach (var ch in name)
        {
            var isHex = (ch >= '0' && ch <= '9') ||
                        (ch >= 'a' && ch <= 'f') ||
                        (ch >= 'A' && ch <= 'F');
            if (!isHex) return false;
        }
        return true;
    }

    private static DateTime GetNewestWriteUtc(string dir)
    {
        var newest = DateTime.MinValue;
        try
        {
            var dirWrite = Directory.GetLastWriteTimeUtc(dir);
            if (dirWrite > newest) newest = dirWrite;

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                var t = File.GetLastWriteTimeUtc(file);
                if (t > newest) newest = t;
            }
        }
        catch { }
        return newest;
    }

    private static bool DeleteFileSafe(string path, bool toRecycleBin, out string error)
    {
        error = "";
        if (!File.Exists(path)) return true;

        if (toRecycleBin)
        {
            var (recycled, recycleErr) = RecycleBinHelper.TryRecycleFile(path);
            if (recycled) return true;
            error = recycleErr;
            return false;
        }

        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool DeleteDirectorySafe(string dirPath, bool toRecycleBin, out string error)
    {
        error = "";
        if (!Directory.Exists(dirPath)) return true;
        if (GameDataMigrationService.IsDirectoryReparsePoint(dirPath))
        {
            error = "拒绝删除目录联接。";
            return false;
        }

        if (toRecycleBin)
        {
            var outcome = RecycleBinHelper.Delete(dirPath, out error);
            return outcome != DeleteOutcome.Failed;
        }

        try
        {
            Directory.Delete(dirPath, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void TryDeleteEmptyParent(string childDir)
    {
        try
        {
            var parent = Path.GetDirectoryName(childDir);
            if (!string.IsNullOrEmpty(parent) &&
                Directory.Exists(parent) &&
                !GameDataMigrationService.IsDirectoryReparsePoint(parent) &&
                !Directory.EnumerateFileSystemEntries(parent).Any())
            {
                Directory.Delete(parent, recursive: false);
            }
        }
        catch { }
    }

    private static string? FormatDate(DateTime utc)
    {
        if (utc <= DateTime.MinValue.AddDays(1)) return null;
        return utc.ToLocalTime().ToString("yyyy-MM-dd");
    }
}
