using System.Text.Json;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private volatile bool _gameDataMigrationBusy;
    private CancellationTokenSource? _gameDataMigrationCts;

    private bool HandleGameDataMigrationMessage(string? type, JsonElement root)
    {
        switch (type)
        {
            case "gameDataMigrationStatus":
                _ = PushGameDataMigrationStatusAsync();
                return true;
            case "gameDataMigrationBrowse":
                _ = HandleGameDataMigrationBrowseAsync();
                return true;
            case "gameDataMigrationPreview":
            {
                var target = root.TryGetProperty("targetFolder", out var el) ? el.GetString() ?? "" : "";
                _ = HandleGameDataMigrationPreviewAsync(target);
                return true;
            }
            case "gameDataMigrationStart":
            {
                var target = root.TryGetProperty("targetFolder", out var el) ? el.GetString() ?? "" : "";
                _ = HandleGameDataMigrationStartAsync(target);
                return true;
            }
            case "gameDataMigrationRestore":
                _ = HandleGameDataMigrationRestoreAsync();
                return true;
            case "gameDataMigrationCancel":
                _gameDataMigrationCts?.Cancel();
                return true;
            case "gameDataMigrationOpenSource":
                OpenGameDataFolder(target: false);
                return true;
            case "gameDataMigrationOpenTarget":
                OpenGameDataFolder(target: true);
                return true;
            case "gameDataCleanupStatus":
                _ = PushGameDataCleanupStatusAsync();
                return true;
            case "gameDataCleanupRun":
            {
                var itemIds = new List<string>();
                if (root.TryGetProperty("itemIds", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in arr.EnumerateArray())
                    {
                        var id = el.GetString();
                        if (!string.IsNullOrWhiteSpace(id)) itemIds.Add(id);
                    }
                }
                var toRecycleBin = root.TryGetProperty("toRecycleBin", out var recEl) &&
                                   recEl.ValueKind == JsonValueKind.True;
                _ = HandleGameDataCleanupRunAsync(itemIds, toRecycleBin);
                return true;
            }
            case "gameDataOpenGameBundles":
                OpenGameBundlesFolder();
                return true;
            default:
                return false;
        }
    }

    private string? ResolveMigrationGameDirectory()
    {
        try
        {
            var active = _gameLibrary.GetActiveDirectory();
            if (!string.IsNullOrWhiteSpace(active)) return active;
        }
        catch { }
        try
        {
            return _modManager.GetStoredGameDirectory();
        }
        catch
        {
            return null;
        }
    }

    private async Task PushGameDataMigrationStatusAsync()
    {
        try
        {
            var gameDirectory = ResolveMigrationGameDirectory();
            var status = await Task.Run(() => GameDataMigrationService.Inspect(gameDirectory));
            Post(new { type = "gameDataMigrationStatus", payload = status });
        }
        catch (Exception ex)
        {
            Post(new
            {
                type = "gameDataMigrationStatus",
                payload = new GameDataStorageStatus(
                    Available: false,
                    Error: $"读取游戏数据占用失败：{ex.Message}",
                    Company: "",
                    Product: null,
                    SourcePath: "",
                    SourceExists: false,
                    IsMigrated: false,
                    TargetPath: null,
                    TargetExists: false,
                    TotalBytes: 0,
                    FileCount: 0,
                    GameRunning: false,
                    LocalLowDrive: null,
                    LocalLowFreeBytes: 0,
                    Drives: Array.Empty<GameDataDriveOption>())
            });
        }
    }

    private async Task HandleGameDataMigrationBrowseAsync()
    {
        var gameDirectory = ResolveMigrationGameDirectory();
        var status = await Task.Run(() => GameDataMigrationService.Inspect(gameDirectory));
        if (!status.Available)
        {
            Post(new
            {
                type = "gameDataMigrationPicked",
                payload = new GameDataTargetPreview(false, status.Error ?? "无法识别游戏数据目录。", "", "", null, null, 0, 0)
            });
            return;
        }

        var initial = status.Drives.FirstOrDefault()?.Root;
        string? picked;
        try
        {
            picked = PickFolderWithFallback("选择游戏数据存放文件夹（建议选择非 C 盘 NTFS 分区）", initial);
        }
        catch (Exception ex)
        {
            Post(new
            {
                type = "gameDataMigrationPicked",
                payload = new GameDataTargetPreview(false, $"打开文件夹选择框失败：{ex.Message}", "", "", null, null, 0, 0)
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(picked)) return;
        await HandleGameDataMigrationPreviewAsync(picked);
    }

    private async Task HandleGameDataMigrationPreviewAsync(string targetFolder)
    {
        var gameDirectory = ResolveMigrationGameDirectory();
        var preview = await Task.Run(() => GameDataMigrationService.ValidateTarget(gameDirectory, targetFolder));
        Post(new { type = "gameDataMigrationPicked", payload = preview });
    }

    private async Task HandleGameDataMigrationStartAsync(string targetFolder)
    {
        if (GameFileOperationBusy)
        {
            Post(new
            {
                type = "gameDataMigrationDone",
                payload = new GameDataMigrationResult(false, "另一个游戏文件或迁移操作正在进行中，请等待完成后再试。")
            });
            return;
        }

        var gameDirectory = ResolveMigrationGameDirectory();
        _gameDataMigrationBusy = true;
        using var cts = new CancellationTokenSource();
        _gameDataMigrationCts = cts;
        var lastReportTicks = 0L;
        string? lastStage = null;

        try
        {
            var progress = new InlineProgress<GameDataMigrationProgress>(p =>
            {
                var now = Environment.TickCount64;
                if (p.Stage == lastStage && p.Percent < 100 && now - lastReportTicks < 120) return;
                lastStage = p.Stage;
                lastReportTicks = now;
                Post(new { type = "gameDataMigrationProgress", payload = p });
            });

            var result = await GameDataMigrationService.MigrateAsync(
                gameDirectory,
                targetFolder,
                progress: progress,
                cancellationToken: cts.Token);

            if (result.Ok) ToysLog.Info("storage", result.Message);
            else ToysLog.Warn("storage", result.Message);
            if (!string.IsNullOrWhiteSpace(result.Warning)) ToysLog.Warn("storage", result.Warning);

            Post(new { type = "gameDataMigrationDone", payload = result });
            _ = PushAccountProfilesAsync();
        }
        catch (Exception ex)
        {
            ToysLog.Error("storage", "游戏数据迁移异常", ex);
            Post(new
            {
                type = "gameDataMigrationDone",
                payload = new GameDataMigrationResult(false, $"迁移过程发生异常：{ex.Message}")
            });
        }
        finally
        {
            _gameDataMigrationCts = null;
            _gameDataMigrationBusy = false;
        }
    }

    private async Task HandleGameDataMigrationRestoreAsync()
    {
        if (GameFileOperationBusy)
        {
            Post(new
            {
                type = "gameDataMigrationDone",
                payload = new GameDataMigrationResult(false, "另一个游戏文件或迁移操作正在进行中，请等待完成后再试。")
            });
            return;
        }

        var gameDirectory = ResolveMigrationGameDirectory();
        _gameDataMigrationBusy = true;
        using var cts = new CancellationTokenSource();
        _gameDataMigrationCts = cts;
        var lastReportTicks = 0L;
        string? lastStage = null;

        try
        {
            var progress = new InlineProgress<GameDataMigrationProgress>(p =>
            {
                var now = Environment.TickCount64;
                if (p.Stage == lastStage && p.Percent < 100 && now - lastReportTicks < 120) return;
                lastStage = p.Stage;
                lastReportTicks = now;
                Post(new { type = "gameDataMigrationProgress", payload = p });
            });

            var result = await GameDataMigrationService.RestoreToLocalAsync(
                gameDirectory,
                progress: progress,
                cancellationToken: cts.Token);

            if (result.Ok) ToysLog.Info("storage", result.Message);
            else ToysLog.Warn("storage", result.Message);
            if (!string.IsNullOrWhiteSpace(result.Warning)) ToysLog.Warn("storage", result.Warning);

            Post(new { type = "gameDataMigrationDone", payload = result });
            _ = PushAccountProfilesAsync();
        }
        catch (Exception ex)
        {
            ToysLog.Error("storage", "游戏数据迁回异常", ex);
            Post(new
            {
                type = "gameDataMigrationDone",
                payload = new GameDataMigrationResult(false, $"迁回过程发生异常：{ex.Message}")
            });
        }
        finally
        {
            _gameDataMigrationCts = null;
            _gameDataMigrationBusy = false;
        }
    }

    private void OpenGameDataFolder(bool target)
    {
        try
        {
            var status = GameDataMigrationService.Inspect(ResolveMigrationGameDirectory());
            var path = target ? status.TargetPath : status.SourcePath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                Post(new { type = "toast", message = "对应目录当前不存在。" });
                return;
            }
            SpeedhackManager.OpenInExplorer(path);
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"打开目录失败：{ex.Message}" });
        }
    }

    private async Task PushGameDataCleanupStatusAsync()
    {
        try
        {
            var gameDirectory = ResolveMigrationGameDirectory();
            var status = await Task.Run(() => GameDataCleanupService.Inspect(gameDirectory));
            Post(new { type = "gameDataCleanupStatus", payload = status });
        }
        catch (Exception ex)
        {
            Post(new
            {
                type = "gameDataCleanupStatus",
                payload = new GameDataCleanupStatus(
                    Available: false,
                    Error: $"扫描废弃缓存失败：{ex.Message}",
                    Company: "",
                    Product: null,
                    GameDirectory: null,
                    GameDataDirectory: null,
                    ActiveCatalogVersion: null,
                    CacheRedirectedToGameDir: false,
                    GameAddressablesBundlesPath: null,
                    GameAddressablesBundlesBytes: 0,
                    GameRunning: false,
                    TotalReclaimableBytes: 0,
                    Items: Array.Empty<GameDataCleanupItem>())
            });
        }
    }

    private async Task HandleGameDataCleanupRunAsync(IReadOnlyCollection<string> itemIds, bool toRecycleBin)
    {
        if (GameFileOperationBusy)
        {
            Post(new
            {
                type = "gameDataCleanupDone",
                payload = new GameDataCleanupResult(false, "另一个游戏文件或存储操作正在进行中，请等待完成后再试。", 0, 0)
            });
            return;
        }

        var gameDirectory = ResolveMigrationGameDirectory();
        _gameDataMigrationBusy = true;
        try
        {
            var result = await GameDataCleanupService.CleanAsync(gameDirectory, itemIds, toRecycleBin);
            if (result.Ok) ToysLog.Info("storage", result.Message);
            else ToysLog.Warn("storage", result.Message);
            if (!string.IsNullOrWhiteSpace(result.Warning)) ToysLog.Warn("storage", result.Warning);

            Post(new { type = "gameDataCleanupDone", payload = result });
            _ = PushGameDataMigrationStatusAsync();
            _ = PushAccountProfilesAsync();
        }
        catch (Exception ex)
        {
            ToysLog.Error("storage", "清理旧缓存异常", ex);
            Post(new
            {
                type = "gameDataCleanupDone",
                payload = new GameDataCleanupResult(false, $"清理过程发生异常：{ex.Message}", 0, 0)
            });
        }
        finally
        {
            _gameDataMigrationBusy = false;
        }
    }

    private void OpenGameBundlesFolder()
    {
        try
        {
            var status = GameDataCleanupService.Inspect(ResolveMigrationGameDirectory());
            var path = status.GameAddressablesBundlesPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                Post(new { type = "toast", message = "游戏目录内的缓存文件夹当前不存在。" });
                return;
            }
            SpeedhackManager.OpenInExplorer(path);
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"打开目录失败：{ex.Message}" });
        }
    }
}
