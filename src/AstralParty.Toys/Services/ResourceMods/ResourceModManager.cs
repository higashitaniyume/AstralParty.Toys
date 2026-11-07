using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ResourceModLoader;

namespace AstralParty.Toys.Services.ResourceMods;

/// <summary>
/// 资源模组安装器：ZIP 按原目录结构落地到游戏目录，交给内置的 ResourceModLoader 引擎
/// 解析 mod.json / replace.txt / 图片 / Patch，并改写 Addressables catalog。
/// </summary>
public sealed partial class ResourceModManager
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
                    ImportedAt = mod.ImportedAt,
                    HasMojibakeNames = ContainsMojibakeNames(dir)
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
            // 被替换的旧包要移出 packages：RML 引擎递归扫描该目录，留在里面会被当成第二个模组一起应用。
            var retiredDir = Path.Combine(store, ".replaced-" + id);
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
                using var archive = CompatZipArchive.OpenRead(zipPath);
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

                TryDeleteDirectory(retiredDir);
                if (Directory.Exists(finalDir)) Directory.Move(finalDir, retiredDir);
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
                    TryDeleteDirectory(retiredDir);
                }
                catch (OperationCanceledException)
                {
                    // 回滚：恢复被替换的旧包，撤销本次清单变更，catalog 已由 Apply 还原。
                    TryDeleteDirectory(finalDir);
                    if (Directory.Exists(retiredDir)) Directory.Move(retiredDir, finalDir);
                    manifest.Mods.RemoveAll(mod => string.Equals(mod.Id, id, StringComparison.OrdinalIgnoreCase));
                    if (previous is not null) manifest.Mods.Add(previous);
                    SaveManifest(store, manifest);
                    ReportProgress(progress, "cancelled", 100, "已取消并还原", "", canPause: false, canCancel: false);
                    throw;
                }
                catch (Exception ex)
                {
                    result.ApplyError = ex.Message;
                    TryDeleteDirectory(retiredDir);
                }
            }
            // 未应用（等待稍后应用）时旧包也已用不上，一并清掉，避免残留在 store 里。
            TryDeleteDirectory(retiredDir);
            if (!result.Applied)
                ReportProgress(progress, "done", 100, "安装完成", result.PendingReason ?? "", canPause: false, canCancel: false);
            return result;
        }
    }
}