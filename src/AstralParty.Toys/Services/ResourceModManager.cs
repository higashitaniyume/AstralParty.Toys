using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ResourceModLoader;

namespace AstralParty.Toys.Services;

public sealed class ResourceModBundle
{
    public string FileName { get; set; } = "";
    public string BundleName { get; set; } = "";
    public long Size { get; set; }
}

public sealed class ResourceModItem
{
    public string Kind { get; set; } = "redirect";
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? Container { get; set; }
    public string? Reference { get; set; }
}

public sealed class ResourceModPackage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; }
    public bool Enabled { get; set; } = true;
    public int FileCount { get; set; }
    public long Size { get; set; }
    public bool HasModJson { get; set; }
    public bool HasPatch { get; set; }
    /// <summary>旧清单字段，保留读取兼容；新导入不再单独解析。</summary>
    public List<ResourceModBundle> Bundles { get; set; } = new();
    public List<ResourceModItem> Items { get; set; } = new();
    public List<string> SkippedEntries { get; set; } = new();
    public List<string> Unsupported { get; set; } = new();
}

public sealed class ResourceModAppliedState
{
    public string CatalogVersion { get; set; } = "";
    public DateTimeOffset AppliedAt { get; set; }
    public int Redirects { get; set; }
    public int Bundles { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();
    public string Signature { get; set; } = "";
}

public sealed class ResourceModManifest
{
    public int Schema { get; set; } = 1;
    public List<ResourceModPackage> Mods { get; set; } = new();
    public ResourceModAppliedState? Applied { get; set; }
}

public sealed class ResourceModPackageStatus
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public bool Enabled { get; set; }
    public int BundleCount { get; set; }
    public int MatchedBundles { get; set; }
    public int UnmatchedBundles { get; set; }
    public long Size { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public string[] UnmatchedNames { get; set; } = Array.Empty<string>();
}

public sealed class ResourceModStatus
{
    public string GameDirectory { get; set; } = "";
    public string StoreDirectory { get; set; } = "";
    public bool StoreExists { get; set; }
    public string CatalogVersion { get; set; } = "";
    public bool CatalogAvailable { get; set; }
    public string CatalogError { get; set; } = "";
    public bool GameRunning { get; set; }
    public bool NeedsApply { get; set; }
    public string? AppliedVersion { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public int? AppliedRedirects { get; set; }
    public int AppliedErrors { get; set; }
    public int AppliedWarnings { get; set; }
    public string[] AppliedMessages { get; set; } = Array.Empty<string>();
    public long TotalSize { get; set; }
    public ResourceModPackageStatus[] Mods { get; set; } = Array.Empty<ResourceModPackageStatus>();
}

public sealed class ResourceModImportResult
{
    public ResourceModPackage Package { get; set; } = new();
    public bool Applied { get; set; }
    public int Redirects { get; set; }
    public int ApplyErrors { get; set; }
    public int ApplyWarnings { get; set; }
    public string[] ApplyMessages { get; set; } = Array.Empty<string>();
    public string? ApplyError { get; set; }
    public string? PendingReason { get; set; }
}

public sealed class ResourceModApplyResult
{
    public string CatalogVersion { get; set; } = "";
    public int Redirects { get; set; }
    public int Bundles { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();
}

public sealed class ResourceModProgress
{
    public string Phase { get; set; } = "";
    public int Percent { get; set; }
    public string Task { get; set; } = "";
    public string Detail { get; set; } = "";
    public bool Paused { get; set; }
    public bool CanPause { get; set; } = true;
    public bool CanCancel { get; set; } = true;
}

/// <summary>
/// 资源模组安装器：ZIP 按原目录结构落地到游戏目录，交给内置的 ResourceModLoader 引擎
/// 解析 mod.json / replace.txt / 图片 / Patch，并改写 Addressables catalog。
/// </summary>
public sealed class ResourceModManager
{
    public const string StoreFolderName = "AstralParty_ResourceMods";
    private const string ManifestFileName = "installed.json";
    private const string PackagesFolderName = "packages";
    private const string DisabledFolderName = "disabled";
    private const string BackupFolderName = "backup";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();

    public string GetStoreDirectory(string gameDirectory)
    {
        var layout = ResourceModCatalog.TryResolve(gameDirectory)
            ?? throw new InvalidOperationException("未能识别游戏目录，请先在「模组管理」里选择游戏目录。");
        return Path.Combine(layout.GameDirectory, StoreFolderName);
    }

    public ResourceModStatus GetStatus(string? gameDirectory)
    {
        var status = new ResourceModStatus { GameDirectory = gameDirectory ?? "" };
        try
        {
            var layout = ResourceModCatalog.TryResolve(gameDirectory);
            if (layout is null)
            {
                status.CatalogError = "尚未选择有效的游戏目录。";
                return status;
            }

            status.GameDirectory = layout.GameDirectory;
            status.StoreDirectory = Path.Combine(layout.GameDirectory, StoreFolderName);
            status.StoreExists = Directory.Exists(status.StoreDirectory);
            status.CatalogVersion = layout.CatalogVersion;
            status.GameRunning = SpeedhackManager.IsGameRunning();

            if (layout.CatalogVersion.Length == 0)
                status.CatalogError = "未能从 settings.json 读取资源版本。";
            else if (!layout.CatalogJsonExists)
                status.CatalogError = "尚未找到资源目录，请先启动一次游戏，让客户端下载资源。";
            else
                status.CatalogAvailable = true;

            var manifest = LoadManifest(status.StoreDirectory);
            var packages = new List<ResourceModPackageStatus>();
            long totalSize = 0;
            foreach (var mod in manifest.Mods)
            {
                var dir = PackageDirectory(status.StoreDirectory, mod);
                var (fileCount, size) = MeasureDirectory(dir);
                totalSize += size;
                packages.Add(new ResourceModPackageStatus
                {
                    Id = mod.Id,
                    Name = mod.Name,
                    SourceFile = mod.SourceFile,
                    Enabled = mod.Enabled,
                    BundleCount = fileCount,
                    MatchedBundles = fileCount,
                    UnmatchedBundles = 0,
                    Size = size,
                    ImportedAt = mod.ImportedAt
                });
            }

            status.Mods = packages.ToArray();
            status.TotalSize = totalSize;
            status.AppliedVersion = manifest.Applied?.CatalogVersion;
            status.AppliedAt = manifest.Applied?.AppliedAt;
            status.AppliedRedirects = manifest.Applied?.Redirects;
            status.AppliedErrors = manifest.Applied?.Errors ?? 0;
            status.AppliedWarnings = manifest.Applied?.Warnings ?? 0;
            status.AppliedMessages = manifest.Applied?.Messages ?? Array.Empty<string>();

            var enabled = manifest.Mods.Where(mod => mod.Enabled).ToList();
            if (enabled.Count > 0)
            {
                status.NeedsApply = manifest.Applied is null
                    || !string.Equals(manifest.Applied.CatalogVersion, layout.CatalogVersion, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(manifest.Applied.Signature, ComputeSignature(status.StoreDirectory, enabled), StringComparison.Ordinal);
            }
        }
        catch (Exception ex)
        {
            status.CatalogError = "读取资源模组状态失败：" + ex.Message;
        }
        return status;
    }

    public ResourceModImportResult Import(string gameDirectory, string zipPath, IProgress<ResourceModProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                throw new FileNotFoundException("找不到要导入的压缩包。", zipPath);
            if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("只支持 .zip 格式的资源模组包。");

            var store = GetStoreDirectory(gameDirectory);
            var name = Path.GetFileNameWithoutExtension(zipPath);
            var id = BuildPackageId(name, new FileInfo(zipPath).Length);
            var manifest = LoadManifest(store);
            if (manifest.Applied is not null
                && manifest.Mods.Any(mod => string.Equals(mod.Id, id, StringComparison.OrdinalIgnoreCase))
                && SpeedhackManager.IsGameRunning())
                throw new InvalidOperationException("游戏正在运行，请先退出游戏再覆盖已应用的资源模组。");

            var packagesDir = Path.Combine(store, PackagesFolderName);
            var finalDir = Path.Combine(packagesDir, id);
            var tempDir = Path.Combine(packagesDir, ".import-" + id);
            Directory.CreateDirectory(packagesDir);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            Directory.CreateDirectory(tempDir);

            var package = new ResourceModPackage
            {
                Id = id,
                Name = name,
                SourceFile = Path.GetFileName(zipPath),
                ImportedAt = DateTimeOffset.Now,
                Enabled = true
            };

            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var files = archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)).ToList();
                ImportLimits.EnsureFreeSpace(store, files.Sum(entry => entry.Length) + (32L << 20));

                var root = Path.GetFullPath(tempDir) + Path.DirectorySeparatorChar;
                var index = 0;
                foreach (var entry in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    index++;
                    ReportProgress(progress, "import", 2 + (int)(38.0 * index / Math.Max(1, files.Count)),
                        "解压模组文件", entry.FullName, canPause: false);
                    var relative = entry.FullName.Replace('\\', '/').TrimStart('/');
                    if (relative.Length == 0 || relative.Contains("..", StringComparison.Ordinal)) continue;
                    var destination = Path.GetFullPath(Path.Combine(tempDir, relative.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                    try { entry.ExtractToFile(destination, overwrite: true); }
                    catch (InvalidDataException) { package.SkippedEntries.Add(entry.FullName); }
                    catch (IOException) { package.SkippedEntries.Add(entry.FullName); }
                }

                package.FileCount = files.Count - package.SkippedEntries.Count;
                package.Size = Directory.EnumerateFiles(tempDir, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
                package.HasModJson = fileExists(tempDir, "mod.json");
                package.HasPatch = fileExists(tempDir, ".patch.");

                if (package.FileCount == 0)
                    throw new InvalidDataException("压缩包里没有可安装的文件。");
                if (!HasRecognizableContent(tempDir))
                    throw new InvalidDataException("压缩包里没有可识别的资源模组内容（bundle / mod.json / replace.txt / 图片 / patch）。");

                TryDeleteDirectory(finalDir + ".old");
                if (Directory.Exists(finalDir)) Directory.Move(finalDir, finalDir + ".old");
                Directory.Move(tempDir, finalDir);
            }
            catch (OperationCanceledException)
            {
                TryDeleteDirectory(tempDir);
                ReportProgress(progress, "cancelled", 100, "已取消并还原", "", canPause: false, canCancel: false);
                throw;
            }
            catch
            {
                TryDeleteDirectory(tempDir);
                throw;
            }

            var previous = manifest.Mods.FirstOrDefault(mod => string.Equals(mod.Id, id, StringComparison.OrdinalIgnoreCase));
            manifest.Mods.RemoveAll(mod => string.Equals(mod.Id, id, StringComparison.OrdinalIgnoreCase));
            manifest.Mods.Add(package);
            SaveManifest(store, manifest);

            var result = new ResourceModImportResult { Package = package };
            var layout = ResourceModCatalog.TryResolve(gameDirectory);
            if (layout is not { CatalogJsonExists: true })
                result.PendingReason = "尚未找到游戏资源目录，请先启动一次游戏后再点「应用」。";
            else if (SpeedhackManager.IsGameRunning())
                result.PendingReason = "游戏正在运行，退出游戏后点「应用」即可生效。";
            else
            {
                try
                {
                    var applied = Apply(gameDirectory, progress, cancellationToken);
                    result.Applied = true;
                    result.Redirects = applied.Redirects;
                    result.ApplyErrors = applied.Errors;
                    result.ApplyWarnings = applied.Warnings;
                    result.ApplyMessages = applied.Messages;
                    TryDeleteDirectory(finalDir + ".old");
                }
                catch (OperationCanceledException)
                {
                    // 回滚：恢复被替换的旧包，撤销本次清单变更，catalog 已由 Apply 还原。
                    TryDeleteDirectory(finalDir);
                    if (Directory.Exists(finalDir + ".old")) Directory.Move(finalDir + ".old", finalDir);
                    manifest.Mods.RemoveAll(mod => string.Equals(mod.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (previous is not null) manifest.Mods.Add(previous);
                    SaveManifest(store, manifest);
                    ReportProgress(progress, "cancelled", 100, "已取消并还原", "", canPause: false, canCancel: false);
                    throw;
                }
                catch (Exception ex)
                {
                    result.ApplyError = ex.Message;
                    TryDeleteDirectory(finalDir + ".old");
                }
            }
            if (!result.Applied)
                ReportProgress(progress, "done", 100, "安装完成", result.PendingReason ?? "", canPause: false, canCancel: false);
            return result;
        }
    }

    public ResourceModApplyResult Apply(string gameDirectory, IProgress<ResourceModProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var layout = ResourceModCatalog.TryResolve(gameDirectory)
                ?? throw new InvalidOperationException("未能识别游戏目录，请先在「模组管理」里选择游戏目录。");
            if (layout.CatalogVersion.Length == 0)
                throw new InvalidOperationException("未能从 settings.json 读取资源版本，请先启动一次游戏。");
            if (!layout.CatalogJsonExists)
                throw new InvalidOperationException("尚未找到 catalog_" + layout.CatalogVersion + ".json，请先启动一次游戏，让客户端下载资源。");
            if (SpeedhackManager.IsGameRunning())
                throw new InvalidOperationException("游戏正在运行，请先退出游戏再应用资源模组。");

            var store = Path.Combine(layout.GameDirectory, StoreFolderName);
            var manifest = LoadManifest(store);
            var enabled = manifest.Mods.Where(mod => mod.Enabled).ToList();
            if (enabled.Count == 0)
                throw new InvalidOperationException("没有已启用的资源模组。");

            var packagesDir = Path.Combine(store, PackagesFolderName);
            Directory.CreateDirectory(packagesDir);

            ReportProgress(progress, "apply", 3, "准备应用", "检查资源目录");
            var backupDir = Path.Combine(store, BackupFolderName, layout.CatalogVersion);
            Directory.CreateDirectory(backupDir);
            var backupJson = Path.Combine(backupDir, "catalog_" + layout.CatalogVersion + ".json");
            var backupBundle = Path.Combine(backupDir, "catalog.bundle");
            if (!File.Exists(backupJson)) File.Copy(layout.CatalogJsonPath, backupJson);
            if (layout.CatalogBundleExists && !File.Exists(backupBundle)) File.Copy(layout.CatalogBundlePath, backupBundle);
            ReportProgress(progress, "apply", 8, "备份原始 catalog", layout.CatalogVersion);

            var log = new List<string>();
            ResourceModLoader.EngineControl.Begin(cancellationToken);
            try
            {
                RmlEngine.Apply(layout.GameDirectory, packagesDir, line =>
                {
                    lock (log)
                    {
                        log.Add(line);
                        if (log.Count > 200) log.RemoveAt(0);
                    }
                    UpdateProgressFromLog(progress, line);
                });
            }
            catch (OperationCanceledException)
            {
                RestoreFiles(layout, backupJson, backupBundle);
                TryDeleteDirectory(Path.Combine(layout.GameDirectory, "_generated"));
                ReportProgress(progress, "cancelled", 100, "已取消并还原", "", canPause: false, canCancel: false);
                throw;
            }
            catch (Exception ex)
            {
                RestoreFiles(layout, backupJson, backupBundle);
                TryDeleteDirectory(Path.Combine(layout.GameDirectory, "_generated"));
                var tail = TailLog(log);
                throw new InvalidOperationException(tail.Length > 0 ? ex.Message + "\n" + tail : ex.Message, ex);
            }
            finally
            {
                ResourceModLoader.EngineControl.End();
            }

            var errors = 0;
            var warnings = 0;
            var messages = new List<string>();
            lock (log)
            {
                foreach (var line in log)
                {
                    if (line.StartsWith("[E]", StringComparison.Ordinal)) errors++;
                    else if (line.StartsWith("[W]", StringComparison.Ordinal)) warnings++;
                    if (line.StartsWith("[E]", StringComparison.Ordinal) || line.StartsWith("[W]", StringComparison.Ordinal))
                        messages.Add(line);
                }
            }

            var report = RmlEngine.LastReport;
            errors = Math.Max(errors, report.Errors);
            warnings = Math.Max(warnings, report.Warnings);
            foreach (var message in report.Messages)
                if (!messages.Contains(message)) messages.Add(message);

            ReportProgress(progress, "apply", 98, "写入模组清单", "");
            if (cancellationToken.IsCancellationRequested)
            {
                RestoreFiles(layout, backupJson, backupBundle);
                TryDeleteDirectory(Path.Combine(layout.GameDirectory, "_generated"));
                throw new OperationCanceledException(cancellationToken);
            }
            var redirects = CountStoreRedirects(layout.CatalogJsonPath, store, layout.GameDirectory);
            manifest.Applied = new ResourceModAppliedState
            {
                CatalogVersion = layout.CatalogVersion,
                AppliedAt = DateTimeOffset.Now,
                Redirects = redirects,
                Bundles = enabled.Count,
                Errors = errors,
                Warnings = warnings,
                Messages = messages.TakeLast(8).ToArray(),
                Signature = ComputeSignature(store, enabled)
            };
            SaveManifest(store, manifest);

            ReportProgress(progress, "done", 100, "应用完成", "", canPause: false, canCancel: false);
            return new ResourceModApplyResult
            {
                CatalogVersion = layout.CatalogVersion,
                Redirects = redirects,
                Bundles = enabled.Count,
                Errors = errors,
                Warnings = warnings,
                Messages = messages.TakeLast(8).ToArray()
            };
        }
    }

    public void Restore(string gameDirectory)
    {
        lock (_gate)
        {
            var layout = ResourceModCatalog.TryResolve(gameDirectory)
                ?? throw new InvalidOperationException("未能识别游戏目录。");
            var store = Path.Combine(layout.GameDirectory, StoreFolderName);
            var manifest = LoadManifest(store);
            if (manifest.Applied is null)
                throw new InvalidOperationException("当前没有已应用的资源模组。");

            if (string.Equals(manifest.Applied.CatalogVersion, layout.CatalogVersion, StringComparison.OrdinalIgnoreCase))
            {
                var backupDir = Path.Combine(store, BackupFolderName, layout.CatalogVersion);
                var backupJson = Path.Combine(backupDir, "catalog_" + layout.CatalogVersion + ".json");
                var backupBundle = Path.Combine(backupDir, "catalog.bundle");
                if (!File.Exists(backupJson))
                    throw new InvalidOperationException("找不到 catalog 备份，无法还原。请让 Steam 校验游戏文件。");
                RestoreFiles(layout, backupJson, backupBundle);
            }

            manifest.Applied = null;
            SaveManifest(store, manifest);
        }
    }

    public void SetEnabled(string gameDirectory, string id, bool enabled, IProgress<ResourceModProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var store = GetStoreDirectory(gameDirectory);
            var manifest = LoadManifest(store);
            var mod = manifest.Mods.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("找不到该资源模组。");
            if (mod.Enabled == enabled) return;
            if (manifest.Applied is not null && SpeedhackManager.IsGameRunning())
                throw new InvalidOperationException("游戏正在运行，请先退出游戏再启停已应用的资源模组。");

            var actualFrom = PackageDirectory(store, mod);
            var to = Path.Combine(store, enabled ? PackagesFolderName : DisabledFolderName, id);
            if (Directory.Exists(actualFrom) && !string.Equals(actualFrom, to, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if (Directory.Exists(to)) Directory.Delete(to, true);
                Directory.Move(actualFrom, to);
            }

            mod.Enabled = enabled;
            SaveManifest(store, manifest);
            try
            {
                ReapplyOrRestore(gameDirectory, store, manifest, progress, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (Directory.Exists(to) && !string.Equals(actualFrom, to, StringComparison.OrdinalIgnoreCase))
                    Directory.Move(to, actualFrom);
                mod.Enabled = !enabled;
                SaveManifest(store, manifest);
                throw;
            }
        }
    }

    public void Remove(string gameDirectory, string id, IProgress<ResourceModProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var store = GetStoreDirectory(gameDirectory);
            var manifest = LoadManifest(store);
            var mod = manifest.Mods.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            if (mod is null) return;
            if (manifest.Applied is not null && SpeedhackManager.IsGameRunning())
                throw new InvalidOperationException("游戏正在运行，请先退出游戏再删除已应用的资源模组。");

            manifest.Mods.Remove(mod);
            SaveManifest(store, manifest);
            try
            {
                ReapplyOrRestore(gameDirectory, store, manifest, progress, cancellationToken);
            }
            catch
            {
                manifest.Mods.Add(mod);
                SaveManifest(store, manifest);
                throw;
            }
            TryDeleteDirectory(PackageDirectory(store, mod));
        }
    }

    private void ReapplyOrRestore(
        string gameDirectory,
        string store,
        ResourceModManifest manifest,
        IProgress<ResourceModProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (manifest.Mods.Any(mod => mod.Enabled))
        {
            var layout = ResourceModCatalog.TryResolve(gameDirectory);
            if (layout is null || SpeedhackManager.IsGameRunning()) return;
            Apply(gameDirectory, progress, cancellationToken);
            return;
        }

        if (manifest.Applied is null) return;
        Restore(gameDirectory);
    }

    private static void RestoreFiles(ResourceModGameLayout layout, string backupJson, string backupBundle)
    {
        if (File.Exists(backupJson))
        {
            var temp = layout.CatalogJsonPath + ".toys-restore";
            File.Copy(backupJson, temp, overwrite: true);
            _ = ResourceModCatalog.LoadJson(temp);
            File.Move(temp, layout.CatalogJsonPath, overwrite: true);
        }

        if (File.Exists(backupBundle) && layout.CatalogBundleExists)
        {
            var temp = layout.CatalogBundlePath + ".toys-restore";
            File.Copy(backupBundle, temp, overwrite: true);
            _ = ResourceModCatalog.TryLoadBundle(temp)
                ?? throw new InvalidDataException("catalog.bundle 备份已损坏。");
            File.Move(temp, layout.CatalogBundlePath, overwrite: true);
        }
    }

    private static int CountStoreRedirects(string catalogPath, string store, string gameDirectory)
    {
        try
        {
            var catalog = ResourceModCatalog.LoadJson(catalogPath);
            var prefix = store.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var generated = Path.Combine(gameDirectory, "_generated").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var count = 0;
            foreach (var locations in catalog.Resources.Values)
                foreach (var location in locations)
                    if (location.ProviderId == ResourceModCatalog.AssetBundleProviderId
                        && (location.InternalId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                            || location.InternalId.StartsWith(generated, StringComparison.OrdinalIgnoreCase)))
                        count++;
            return count;
        }
        catch
        {
            return 0;
        }
    }

    private static string ComputeSignature(string store, IEnumerable<ResourceModPackage> mods)
    {
        var builder = new StringBuilder();
        foreach (var mod in mods.OrderBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(mod.Id).Append('/').Append(mod.Enabled ? '1' : '0').Append('/').Append(mod.FileCount).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static string BuildPackageId(string name, long length)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name + "\n" + length.ToString()));
        return "rm-" + Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    private static string PackageDirectory(string store, ResourceModPackage mod)
    {
        var primary = Path.Combine(store, mod.Enabled ? PackagesFolderName : DisabledFolderName, mod.Id);
        if (Directory.Exists(primary)) return primary;
        var fallback = Path.Combine(store, mod.Enabled ? DisabledFolderName : PackagesFolderName, mod.Id);
        return Directory.Exists(fallback) ? fallback : primary;
    }

    private static (int Files, long Size) MeasureDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return (0, 0);
        try
        {
            var files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).ToList();
            return (files.Count, files.Sum(file => new FileInfo(file).Length));
        }
        catch (IOException) { return (0, 0); }
        catch (UnauthorizedAccessException) { return (0, 0); }
    }

    private static bool HasRecognizableContent(string dir)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            if (name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)
                || name.Equals("mod.json", StringComparison.OrdinalIgnoreCase)
                || name.Equals("replace.txt", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || name.Contains(".patch.", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool fileExists(string dir, string pattern) =>
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Any(file => Path.GetFileName(file).Contains(pattern, StringComparison.OrdinalIgnoreCase));

    private static ResourceModManifest LoadManifest(string store)
    {
        var path = Path.Combine(store, ManifestFileName);
        if (!File.Exists(path)) return new ResourceModManifest();
        try
        {
            return JsonSerializer.Deserialize<ResourceModManifest>(File.ReadAllText(path), JsonOptions)
                ?? new ResourceModManifest();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("资源模组清单已损坏：" + path, ex);
        }
    }

    private static void SaveManifest(string store, ResourceModManifest manifest)
    {
        Directory.CreateDirectory(store);
        var path = Path.Combine(store, ManifestFileName);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(manifest, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }

    private static readonly Regex ProgressPattern = new(@"^\[(\d+)/(\d+)\]", RegexOptions.Compiled);

    private static void ReportProgress(
        IProgress<ResourceModProgress>? progress,
        string phase,
        int percent,
        string task,
        string detail,
        bool canPause = true,
        bool canCancel = true)
    {
        progress?.Report(new ResourceModProgress
        {
            Phase = phase,
            Percent = Math.Clamp(percent, 0, 100),
            Task = task,
            Detail = detail,
            Paused = ResourceModLoader.EngineControl.IsPaused,
            CanPause = canPause,
            CanCancel = canCancel
        });
    }

    private static void UpdateProgressFromLog(IProgress<ResourceModProgress>? progress, string line)
    {
        var percent = 12;
        var match = ProgressPattern.Match(line);
        if (match.Success
            && int.TryParse(match.Groups[1].Value, out var current)
            && int.TryParse(match.Groups[2].Value, out var total)
            && total > 0)
            percent = 12 + (int)(82.0 * current / total);
        if (line.Contains("已保存", StringComparison.Ordinal)) percent = 95;

        var task = line.StartsWith("[E]", StringComparison.Ordinal) ? "错误"
            : line.StartsWith("[W]", StringComparison.Ordinal) ? "警告"
            : line.Contains("Mod扫描", StringComparison.Ordinal) ? "扫描模组"
            : "应用资源";
        ReportProgress(progress, "apply", percent, task, line);
    }

    private static string TailLog(List<string> log)
    {
        lock (log)
        {
            var errors = log.Where(line => line.StartsWith("[E]", StringComparison.Ordinal)
                                           || line.Contains("错误", StringComparison.Ordinal)
                                           || line.Contains("失败", StringComparison.Ordinal)).TakeLast(6).ToList();
            if (errors.Count == 0) errors = log.TakeLast(6).ToList();
            return string.Join("\n", errors);
        }
    }
}
