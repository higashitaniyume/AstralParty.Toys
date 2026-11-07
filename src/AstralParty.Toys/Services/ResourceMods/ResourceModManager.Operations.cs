using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ResourceModLoader;

namespace AstralParty.Toys.Services.ResourceMods;

public sealed partial class ResourceModManager
{
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

            // 引擎按目录扫描 mods，不读 installed.json；先让目录与启用状态一致，
            // 否则已停用但仍留在 packages 里的模组也会被应用。
            SyncPackageFolders(store, manifest);

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
            if (redirects == 0)
            {
                // 引擎把错误吞在日志里时，catalog 不会被写入；此时不能记为“已应用”。
                var detail = messages.Count > 0
                    ? "\n" + string.Join("\n", messages.TakeLast(6))
                    : "\n引擎没有改写任何 bundle 地址。";
                throw new InvalidOperationException("没有任何资源替换写入 catalog，资源模组未生效。" + detail);
            }
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

    /// <summary>让 packages / disabled 两个目录与清单里的启用状态一致，引擎只扫描 packages。</summary>
    private static void SyncPackageFolders(string store, ResourceModManifest manifest)
    {
        foreach (var mod in manifest.Mods)
        {
            var expected = Path.Combine(store, mod.Enabled ? PackagesFolderName : DisabledFolderName, mod.Id);
            var other = Path.Combine(store, mod.Enabled ? DisabledFolderName : PackagesFolderName, mod.Id);
            if (Directory.Exists(expected))
            {
                if (Directory.Exists(other)) TryDeleteDirectory(other);
                continue;
            }
            if (!Directory.Exists(other)) continue;
            Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
            Directory.Move(other, expected);
        }
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

    /// <summary>旧版本按 UTF-8 解码 GBK 文件名，解压后名字里会留下 U+FFFD；命中时提示玩家重新导入同一 ZIP。</summary>
    private static bool ContainsMojibakeNames(string dir)
    {
        if (!Directory.Exists(dir)) return false;
        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(dir, "*", SearchOption.AllDirectories))
                if (entry.Contains('\uFFFD')) return true;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return false;
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
