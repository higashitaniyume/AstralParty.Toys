using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using AstralParty.Toys.Models;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    // ============================== 变速器（游戏工具） ==============================

    private int _speedhackStatusRequest;

    private async Task PushSpeedhackStatusAsync()
    {
        var request = ++_speedhackStatusRequest;
        try
        {
            var payload = await Task.Run(() =>
            {
                var status = _speedhackManager.GetStatus();
                SpeedhackEditorModel? editor = null;
                var configBroken = false;
                try
                {
                    editor = SpeedhackEditorMapper.MapToEditor(_speedhackManager.LoadProfileConfig());
                }
                catch (Exception)
                {
                    configBroken = true;
                }
                return new { status, config = editor, configBroken };
            });
            // 切换游戏或刷新后，较早的读取结果不能覆盖最新状态。
            if (request == _speedhackStatusRequest)
                Post(new { type = "speedhackStatus", payload });
        }
        catch (Exception ex)
        {
            if (request == _speedhackStatusRequest)
                Post(new { type = "error", message = $"读取变速器状态失败：{ex.Message}" });
        }
    }

    private void HandleSpeedhackDetect()
    {
        var directory = SpeedhackManager.DetectGameDirectory();
        if (directory is null)
        {
            Post(new { type = "toast", message = "自动检测未找到游戏目录，请点「选择…」手动指定。" });
        }
        else
        {
            _gameLibrary.AddDirectory(directory, activate: true);
            SyncActiveGameToManagers();
            Post(new { type = "toast", message = $"已自动定位游戏目录：{directory}" });
            _ = PushGameProfilesAsync();
        }
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleSpeedhackBrowse()
    {
        string? current = null;
        try
        {
            current = _speedhackManager.ResolveGameDirectory();
            var folder = PickFolderWithFallback(
                "选择吉星派对游戏目录（AstralParty exe 所在文件夹）",
                current);
            if (folder is not { Length: > 0 }) return;
            _gameLibrary.AddDirectory(folder, activate: true);
            SyncActiveGameToManagers();
            Post(new { type = "toast", message = $"已选择游戏目录：{folder}" });
            _ = PushGameProfilesAsync();
            _ = PushSpeedhackStatusAsync();
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"打开文件夹选择框失败：{ex.Message}（起始目录：{current ?? "（无）"}）" });
        }
    }

    private string RequireGameDirectory()
    {
        var directory = _speedhackManager.GetStatus().GameDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new InvalidOperationException("尚未选择有效的游戏目录，请先自动检测或手动选择。");
        return directory;
    }

    private void HandleSpeedhackInstall(bool overwriteDll)
    {
        _speedhackManager.Install(RequireGameDirectory(), overwriteDll);
        Post(new { type = "toast", message = SpeedhackManager.DescribeInstalled() });
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleSpeedhackUninstall(bool force)
    {
        var result = _speedhackManager.Uninstall(RequireGameDirectory(), force);
        Post(new { type = "toast", message = result.Message });
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleSpeedhackSaveConfig(string rawJson, string? afterSave, bool overwriteDll)
    {
        var model = JsonSerializer.Deserialize<SpeedhackEditorModel>(rawJson, WebReadOptions);
        if (model is null) throw new InvalidOperationException("配置内容为空，无法保存。");

        var config = _speedhackManager.LoadProfileConfig();
        SpeedhackEditorMapper.ApplyToConfig(config, model);
        _speedhackManager.SaveProfileConfig(config);

        if (string.Equals(afterSave, "install", StringComparison.Ordinal))
        {
            _speedhackManager.Install(RequireGameDirectory(), overwriteDll);
            Post(new { type = "toast", message = $"变速器已安装，每次进入游戏将自动使用 {model.BaseSpeed:0.##} 倍速。" + (SpeedhackManager.IsGameRunning() ? "游戏正在运行：本次写入要重启游戏后才会加载。" : "") });
            _ = PushSpeedhackStatusAsync();
            return;
        }

        var currentStatus = _speedhackManager.GetStatus();
        var installedDirectory = currentStatus.Installed ? currentStatus.GameDirectory : null;
        if (!string.IsNullOrEmpty(installedDirectory)) _speedhackManager.PushConfigToGame(installedDirectory);

        Post(new
        {
            type = "toast",
            message = installedDirectory is { Length: > 0 }
                ? $"配置已保存并同步到游戏目录；下次进入游戏自动使用 {model.BaseSpeed:0.##} 倍速。"
                : "配置已保存到本地主配置，安装变速器时会一并复制到游戏目录。"
        });
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleSpeedhackResetConfig()
    {
        _speedhackManager.ResetProfileToTemplate();
        var installedDirectory = _speedhackManager.GetStatus().Installed
            ? _speedhackManager.GetStatus().GameDirectory : null;
        if (!string.IsNullOrEmpty(installedDirectory)) _speedhackManager.PushConfigToGame(installedDirectory);

        Post(new { type = "toast", message = "已恢复默认模板（Ctrl 切换 2 倍速）。" });
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleSpeedhackPushConfig()
    {
        var directory = RequireGameDirectory();
        if (!File.Exists(Path.Combine(directory, SpeedhackManager.DllName)))
            throw new InvalidOperationException("游戏目录尚未安装变速器，请先安装。");
        _speedhackManager.PushConfigToGame(directory);
        Post(new { type = "toast", message = "配置已同步到游戏目录，重启游戏后生效。" });
        _ = PushSpeedhackStatusAsync();
    }

    private void HandleSpeedhackEditConfig()
    {
        var profilePath = _speedhackManager.EnsureProfileConfig();
        SpeedhackManager.OpenWithDefaultEditor(profilePath);
    }

    private void HandleSpeedhackOpenGameDir()
    {
        var directory = RequireGameDirectory();
        SpeedhackManager.OpenInExplorer(directory);
    }


    // ============================== Mod 加载器（游戏工具） ==============================

    private int _modStatusRequest;

    private async Task PushModStatusAsync()
    {
        var request = ++_modStatusRequest;
        try
        {
            var status = await Task.Run(() => _modManager.GetStatus());
            if (request == _modStatusRequest)
                Post(new
                {
                    type = "modStatus",
                    payload = new
                    {
                        status,
                        builtInMods = ModManager.EmbeddedBuiltInModIdList,
                        mirrorType = _libraryService.Settings.GitHubMirrorType,
                        customMirrorUrl = _libraryService.Settings.CustomGitHubMirrorUrl
                    }
                });
            if (request == _modStatusRequest)
                PostAppVersion(status);   // 关于页的「已安装加载器」跟着安装 / 更新 / 停用一起刷新
        }
        catch (Exception ex)
        {
            if (request == _modStatusRequest)
                Post(new { type = "error", message = $"读取模组状态失败：{ex.Message}" });
        }
    }

    private string RequireModGameDirectory()
    {
        var directory = _modManager.GetStatus().GameDirectory;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new InvalidOperationException("尚未选择有效的游戏目录，请先自动检测或手动选择。");
        return directory;
    }

    private void HandleModDetect()
    {
        var directory = SpeedhackManager.DetectGameDirectory();
        if (directory is null)
        {
            Post(new { type = "toast", message = "自动检测未找到游戏目录，请点「选择…」手动指定。" });
        }
        else
        {
            _gameLibrary.AddDirectory(directory, activate: true);
            SyncActiveGameToManagers();
            Post(new { type = "toast", message = $"已自动定位游戏目录：{directory}" });
            _ = PushGameProfilesAsync();
        }
        _ = PushModStatusAsync();
    }

    private void HandleModBrowse()
    {
        string? current = null;
        try
        {
            current = _modManager.ResolveGameDirectory();
            var folder = PickFolderWithFallback(
                "选择吉星派对游戏目录（AstralParty exe 所在文件夹）",
                current);
            if (folder is not { Length: > 0 }) return;
            _gameLibrary.AddDirectory(folder, activate: true);
            SyncActiveGameToManagers();
            Post(new { type = "toast", message = $"已选择游戏目录：{folder}" });
            _ = PushGameProfilesAsync();
            _ = PushModStatusAsync();
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"打开文件夹选择框失败：{ex.Message}（起始目录：{current ?? "（无）"}）" });
        }
    }

    private void HandleModInstall(bool overwriteDll, bool includeSample, bool allowDowngrade)
    {
        if (_modInstallRunning)
        {
            Post(new { type = "toast", message = "正在安装加载器，请稍候。" });
            return;
        }
        _modInstallRunning = true;
        _ = InstallModLoaderAsync(overwriteDll, includeSample, allowDowngrade);
    }

    private bool _modInstallRunning;

    /// <summary>安装内置加载器：写文件放到后台线程，进度经 <see cref="LoaderProgressReporter"/> 实时回给界面。</summary>
    private async Task InstallModLoaderAsync(bool overwriteDll, bool includeSample, bool allowDowngrade)
    {
        try
        {
            var directory = RequireModGameDirectory();
            var reporter = LoaderProgressReporter();
            await Task.Run(() => _modManager.Install(directory, overwriteDll, includeSample, allowDowngrade, reporter))
                .ConfigureAwait(true);
            Post(new { type = "toast", message = ModManager.DescribeInstalled() });
            _ = PostSteamBypassSyncAsync();   // 刚装/更新完，首页的启动方式单选框跟上新配置
        }
        catch (Exception ex)
        {
            ToysLog.Error("modloader", "安装内置加载器失败", ex);
            Post(new { type = "error", message = $"安装加载器失败：{ex.Message}" });
            Post(new { type = "modLoaderProgress", payload = new { phase = "error", percent = -1, message = ex.Message, active = false } });
        }
        finally
        {
            _modInstallRunning = false;
            _ = PushModStatusAsync();
        }
    }

    // ============================== 版本回滚（安装历史） ==============================

    /// <summary>把安装历史里还能整包回滚的版本推给界面。</summary>
    private void HandleModRollbackList()
    {
        try
        {
            var directory = RequireModGameDirectory();
            var versions = _modManager.GetRetainedVersions(directory);
            Post(new { type = "modRollbackVersions", payload = new { versions } });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"读取可回滚版本失败：{ex.Message}" });
        }
    }

    private bool _modRollbackRunning;

    private void HandleModRollbackApply(string version)
    {
        if (_modRollbackRunning)
        {
            Post(new { type = "toast", message = "正在回滚加载器，请稍候。" });
            return;
        }
        _modRollbackRunning = true;
        _ = RollbackModLoaderAsync(version);
    }

    private async Task RollbackModLoaderAsync(string version)
    {
        var reporter = LoaderProgressReporter();
        try
        {
            var directory = RequireModGameDirectory();
            // 异常在后台委托里就地接住：不让它穿过 Task 边界，调试器就不会把它当成未处理异常弹出来
            var outcome = await Task.Run(() =>
            {
                try
                {
                    return (Message: _modManager.RollbackLoader(directory, version, reporter), Error: (Exception?)null);
                }
                catch (Exception ex)
                {
                    return (Message: (string?)null, Error: ex);
                }
            }).ConfigureAwait(true);

            if (outcome.Error is not null)
            {
                ToysLog.Error("modloader", $"回滚到 v{version} 失败", outcome.Error);
                Post(new { type = "modLoaderProgress", payload = new { phase = "error", percent = -1, message = outcome.Error.Message, active = false } });
                Post(new { type = "error", message = $"回滚失败：{outcome.Error.Message}" });
                return;
            }

            Post(new
            {
                type = "toast",
                message = outcome.Message + (SpeedhackManager.IsGameRunning() ? "。游戏正在运行，重启游戏后生效。" : "。")
            });
            _ = PostSteamBypassSyncAsync();
        }
        catch (Exception ex)
        {
            ToysLog.Error("modloader", $"回滚到 v{version} 失败", ex);
            Post(new { type = "modLoaderProgress", payload = new { phase = "error", percent = -1, message = ex.Message, active = false } });
            Post(new { type = "error", message = $"回滚失败：{ex.Message}" });
        }
        finally
        {
            _modRollbackRunning = false;
            _ = PushModStatusAsync();
        }
    }

    // ============================== 加载器进度 / 实时日志 ==============================
    /// <summary>把 ModManager 的进度回调转成界面消息（modLoaderProgress）：下载、校验、安装各阶段一条。
    /// 回调可能来自后台线程，Post 自己会切回 UI 线程。</summary>
    private ModManager.LoaderProgressCallback LoaderProgressReporter() => (phase, percent, message) =>
        Post(new { type = "modLoaderProgress", payload = new { phase, percent, message, active = phase != "done" } });

    /// <summary>日志落盘的同时转发给设置页的「运行日志」面板（只转发到配置级别及以上的日志）。</summary>
    private void OnToysLogWritten(DateTime time, ToysLogLevel level, string area, string message) =>
        Post(new
        {
            type = "toysLog",
            payload = LogPayload(time, level, area, message)
        });

    /// <summary>设置页打开「日志」时补一次历史：窗口订阅 Written 之前的启动日志只有内存里还留着。
    /// 不读日志文件——那里面还有上一次启动的记录，混在一起反而分不清哪条属于本次运行。</summary>
    private void HandleToysLogsRequest()
    {
        var lines = ToysLog.RecentEntries(200)
            .Select(entry => LogPayload(entry.Time, entry.Level, entry.Area, entry.Message))
            .ToArray();
        Post(new { type = "toysLogHistory", payload = new { lines } });
    }

    private static object LogPayload(DateTime time, ToysLogLevel level, string area, string message) => new
    {
        time = time.ToString("HH:mm:ss"),
        level = level.ToString().ToLowerInvariant(),
        area,
        message
    };

    // ============================== Mod 加载器：联网更新 ==============================

    private void HandleModUninstall(bool force)
    {
        var result = _modManager.Uninstall(RequireModGameDirectory(), force);
        Post(new { type = "toast", message = result.Message });
        _ = PushModStatusAsync();
    }

    private void HandleModOpenFolder(string folderName)
    {
        var directory = RequireModGameDirectory();
        var folder = Path.Combine(directory, ModManager.LoaderFolderName, folderName);
        Directory.CreateDirectory(folder);
        ModManager.OpenInExplorer(folder);
    }

    private bool _modImportRunning;

    /// <summary>等待界面回答「是否降级导入」的模组包路径；空 = 没有待确认的导入。</summary>
    private List<string> _pendingModImportPaths = new();

    private void HandleModImport(string path) => _ = PrepareModImportAsync(new[] { path });

    /// <summary>
    /// 导入模组：单个 DLL 直接复制进 mods\{ModId}\；ZIP 模组包按包内结构解压安装
    /// （&lt;ModId&gt;\&lt;ModId&gt;.dll，可一次带多个模组）。逐个文件处理，某个失败不影响其它文件。
    ///
    /// ZIP 先做一次只读预检：包内版本比已装的旧时先回界面确认（和加载器安装的降级闸门同一套口径），
    /// 用户点「仍然导入」才真正落盘。读包、解压与写盘都在后台线程。</summary>
    private async Task PrepareModImportAsync(IReadOnlyList<string> paths)
    {
        if (_modImportRunning)
        {
            Post(new { type = "toast", message = "正在导入模组，请稍候。" });
            return;
        }
        // 新的一次导入作废上一次没回答的确认（避免旧弹窗丢了答复后卡住后续操作）
        _pendingModImportPaths = new List<string>();

        string directory;
        try
        {
            directory = RequireModGameDirectory();
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = ex.Message });
            return;
        }

        var packages = paths
            .Where(path => Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (packages.Count > 0)
        {
            List<ModPackageInspection> inspections = new();
            try
            {
                inspections = await Task.Run(() => packages
                    .Select(path => _modManager.InspectModPackage(directory, path))
                    .ToList());
            }
            catch
            {
                // 预检失败不拦路：真正导入时还会逐文件校验并报错，同一个包不会写坏任何东西
            }

            var downgrades = inspections.Where(inspection => inspection.HasDowngrade).ToList();
            if (downgrades.Count > 0)
            {
                _pendingModImportPaths = paths.ToList();
                Post(new
                {
                    type = "modImportConfirm",
                    payload = new
                    {
                        packages = downgrades.Select(inspection => new
                        {
                            name = inspection.SourceName,
                            mods = inspection.Mods.Where(mod => mod.IsDowngrade).Select(mod => new
                            {
                                name = mod.Name,
                                from = mod.InstalledVersion,
                                to = mod.PackageVersion
                            })
                        })
                    }
                });
                return;
            }
        }

        await ImportModFilesAsync(paths, allowDowngrade: false);
    }

    /// <summary>降级确认弹窗的回答（界面按钮）。确认后才带着 allowDowngrade 真正导入。</summary>
    private void HandleModImportConfirmAnswer(bool allow)
    {
        var paths = _pendingModImportPaths;
        _pendingModImportPaths = new List<string>();
        if (!allow || paths.Count == 0) return;
        _ = ImportModFilesAsync(paths, allowDowngrade: true);
    }

    private async Task ImportModFilesAsync(IReadOnlyList<string> paths, bool allowDowngrade)
    {
        if (_modImportRunning)
        {
            Post(new { type = "toast", message = "正在导入模组，请稍候。" });
            return;
        }

        string directory;
        try
        {
            directory = RequireModGameDirectory();
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = ex.Message });
            return;
        }

        _modImportRunning = true;
        try
        {
            var outcome = await Task.Run(() => ImportModFiles(directory, paths, allowDowngrade));
            if (outcome.Imported.Count > 0)
            {
                var message = $"已导入 {outcome.Imported.Count} 个模组：{string.Join("、", outcome.Imported)}——重启游戏后生效";
                if (outcome.Notes.Count > 0) message += "；" + string.Join("；", outcome.Notes);
                Post(new { type = "toast", message });
            }
            foreach (var failure in outcome.Failures)
                Post(new { type = "toast", message = $"导入失败：{failure}" });
        }
        finally
        {
            _modImportRunning = false;
            _ = PushModStatusAsync();
        }
    }

    private readonly record struct ModImportOutcome(
        List<string> Imported, List<string> Notes, List<string> Failures);

    /// <summary>逐个文件导入（在后台线程执行，不碰界面）。</summary>
    private ModImportOutcome ImportModFiles(string gameDirectory, IReadOnlyList<string> paths, bool allowDowngrade)
    {
        var imported = new List<string>();
        var notes = new List<string>();
        var failures = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var result = _modManager.ImportModPackage(gameDirectory, path, allowDowngrade);
                    if (result.Changes.Count > 0)
                        imported.AddRange(result.Changes.Select(DescribeModChange));
                    else
                        imported.AddRange(result.Mods.Select(ModImportName));
                    // 升级已有模组时，玩家自己的配置与停用状态会被保留 —— 值得说一句，免得以为没生效
                    if (result.PreservedConfigs.Count > 0)
                        notes.Add($"保留了 {string.Join("、", result.PreservedConfigs)} 的已有配置");
                    if (result.PreservedDisabled.Count > 0)
                        notes.Add($"{string.Join("、", result.PreservedDisabled)} 仍为禁用状态");
                    if (result.RemovedFiles.Count > 0)
                        notes.Add($"把 {result.RemovedFiles.Count} 个旧版本文件移入了回收站");
                    // 回收站不可用时旧文件原样保留（不永久删除），如实说一声
                    if (result.KeptFiles.Count > 0)
                        notes.Add($"{result.KeptFiles.Count} 个旧版本文件没能移入回收站，已保留");
                    if (result.Skipped.Count > 0)
                        notes.Add($"忽略了 {result.Skipped.Count} 个非模组文件");
                }
                else
                {
                    imported.Add(ModImportName(_modManager.ImportMod(gameDirectory, path)));
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(path)}：{ex.Message}");
            }
        }
        return new ModImportOutcome(imported, notes, failures);
    }

    /// <summary>toast 里用模组自己的显示名（sidecar 的 name），没有就用程序集名。</summary>
    private static string ModImportName(ModEntryInfo entry)
        => entry.DisplayName.Length > 0 ? entry.DisplayName : entry.Name;

    /// <summary>「手牌查看（升级 v1.0.8 → v1.1.0）」这类一句话版本说明。</summary>
    private static string DescribeModChange(ModPackageChange change)
    {
        var name = change.Name.Length > 0 ? change.Name : change.ModId;
        if (!change.WasInstalled || change.FromVersion.Length == 0)
            return change.ToVersion.Length > 0 ? $"{name}（新装 v{change.ToVersion}）" : name;
        if (change.ToVersion.Length == 0) return $"{name}（版本未知）";
        if (change.IsDowngrade) return $"{name}（降级 v{change.FromVersion} → v{change.ToVersion}）";
        if (change.IsUpgrade) return $"{name}（升级 v{change.FromVersion} → v{change.ToVersion}）";
        return $"{name}（v{change.ToVersion}，版本未变）";
    }

    private void HandleModPickPackageFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 CesiumLoader 发布包",
            Filter = "CesiumLoader 发布包|*.zip|SDK DLL|CesiumLoader.SDK.dll",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
            Post(new { type = "modPackageFileSelected", payload = new { path = dialog.FileName } });
    }

    private bool _modPackageImportRunning;

    private async Task HandleModImportPackageAsync(string path, bool overwriteDll, bool allowDowngrade)
    {
        if (_modPackageImportRunning)
        {
            Post(new { type = "modPackageImportResult", payload = new { success = false, message = "已有发布包正在导入，请稍候。" } });
            return;
        }
        _modPackageImportRunning = true;
        try
        {
            var directory = RequireModGameDirectory();
            path = path.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(path) || !File.Exists(path))
                throw new InvalidOperationException("请填写存在的发布包完整路径。");
            var description = await Task.Run(() => _modManager.ImportReleaseFile(directory, path, overwriteDll, allowDowngrade, LoaderProgressReporter()));
            var message = $"已导入 {description}，重启游戏后生效。";
            Post(new { type = "modPackageImportResult", payload = new { success = true, message } });
            Post(new { type = "toast", message });
            _ = PostSteamBypassSyncAsync();
        }
        catch (Exception ex)
        {
            Post(new { type = "modPackageImportResult", payload = new { success = false, message = $"导入失败：{ex.Message}" } });
        }
        finally
        {
            _modPackageImportRunning = false;
            _ = PushModStatusAsync();
        }
    }

    private async void HandleModPickImport()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要导入的模组（DLL 或 ZIP 模组包）",
            Filter = "模组包 (*.zip)|*.zip|模组 DLL (*.dll)|*.dll|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true || dialog.FileNames.Length == 0) return;
        await PrepareModImportAsync(dialog.FileNames);
    }

    private void HandleModDelete(string fileName)
    {
        var entry = _modManager.DeleteMod(RequireModGameDirectory(), fileName);
        if (entry is null)
        {
            Post(new { type = "toast", message = $"未找到 mod：{fileName}" });
        }
        else
        {
            Post(new { type = "toast", message = $"已删除 mod：{entry.FileName}" });
        }
        _ = PushModStatusAsync();
    }

    private void HandleModToggle(string fileName, bool enabled)
    {
        try
        {
            _modManager.ToggleMod(RequireModGameDirectory(), fileName, enabled);
            Post(new { type = "toast", message = enabled
                ? $"已启用 mod：{fileName}（重启游戏后生效）"
                : $"已禁用 mod：{fileName}（重启游戏后生效）" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"切换 mod 状态失败：{ex.Message}" });
        }
        _ = PushModStatusAsync();
    }

    private void HandleModToggleBatch(IReadOnlyList<string> fileNames, bool enabled)
    {
        if (fileNames is null || fileNames.Count == 0)
        {
            Post(new { type = "toast", message = "没有选择模组" });
            return;
        }
        try
        {
            var (ok, failed) = _modManager.ToggleMods(RequireModGameDirectory(), fileNames, enabled);
            var verb = enabled ? "启用" : "禁用";
            Post(new { type = "toast", message = failed.Length == 0
                ? $"已{verb} {ok} 个模组（重启游戏后生效）"
                : $"已{verb} {ok} 个模组，{failed.Length} 个失败：{string.Join("、", failed)}" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"批量切换 mod 状态失败：{ex.Message}" });
        }
        _ = PushModStatusAsync();
    }

    private void HandleModDeleteBatch(IReadOnlyList<string> fileNames)
    {
        if (fileNames is null || fileNames.Count == 0)
        {
            Post(new { type = "toast", message = "没有选择模组" });
            return;
        }
        try
        {
            var (ok, failed) = _modManager.DeleteMods(RequireModGameDirectory(), fileNames);
            Post(new { type = "toast", message = failed.Length == 0
                ? $"已删除 {ok} 个模组"
                : $"已删除 {ok} 个模组，{failed.Length} 个失败：{string.Join("、", failed)}" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"批量删除 mod 失败：{ex.Message}" });
        }
        _ = PushModStatusAsync();
    }

    private void HandleModOpenConfig(string fileName)
    {
        try
        {
            var path = _modManager.OpenModConfig(RequireModGameDirectory(), fileName);
            Post(new { type = "toast", message = $"已用默认编辑器打开配置：{path}" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"打开配置失败：{ex.Message}" });
        }
    }

    private void HandleModOpenConfigRaw(string fileName)
    {
        try
        {
            var path = _modManager.OpenModConfig(RequireModGameDirectory(), fileName);
            Post(new { type = "toast", message = $"已用默认编辑器打开：{path}" });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"打开配置失败：{ex.Message}" });
        }
    }

    // ============================== Mod 加载器：联网更新 ==============================

    /// <summary>只把「最新版本号」回推给界面 —— 版本三栏的「GitHub 最新」栏与结论都由前端自己算，
    /// 这里不再拼一句 toast 文案（同一个结论说两遍，用户反而要自己对齐两个说法）。
    /// 轻量走 GitHub API（不下载整个发布包）；API 失败才下载发布包读清单兜底。</summary>
    private async Task HandleModCheckUpdateAsync(string? mirrorType = null, string? customMirrorUrl = null)
    {
        try
        {
            var settings = _libraryService.Settings;
            var mirror = !string.IsNullOrEmpty(mirrorType) ? mirrorType : settings.GitHubMirrorType;
            var custom = customMirrorUrl ?? settings.CustomGitHubMirrorUrl;
            var info = await _modManager.GetLatestVersionAsync(mirror, custom).ConfigureAwait(true);
            Post(new
            {
                type = "modUpdateCheck",
                payload = new
                {
                    latest = info.Version ?? "",
                    tag = info.Tag ?? "",
                    publishedAt = info.PublishedAt ?? "",
                    htmlUrl = info.HtmlUrl ?? ""
                }
            });
            _ = PushModStatusAsync();
        }
        catch (Exception ex)
        {
            Post(new { type = "modUpdateCheck", payload = new { error = ex.Message } });
        }
    }

    private bool _modUpdateRunning;
    private async Task HandleModDownloadUpdateAsync(bool overwriteDll, bool allowDowngrade, string? mirrorType = null, string? customMirrorUrl = null)
    {
        if (_modUpdateRunning || _loaderRecoveryBusy || _modPackageImportRunning)
            throw new InvalidOperationException("加载器操作正在进行，请稍候。");
        _modUpdateRunning = true;
        var reporter = LoaderProgressReporter();
        Post(new { type = "toast", message = "正在下载最新 CesiumLoader 并安装…" });
        try
        {
            var directory = RequireModGameDirectory();
            var settings = _libraryService.Settings;
            var mirror = !string.IsNullOrEmpty(mirrorType) ? mirrorType : settings.GitHubMirrorType;
            var custom = customMirrorUrl ?? settings.CustomGitHubMirrorUrl;
            reporter("download", -1, "正在连接 GitHub 下载 CesiumLoader…");
            var bytes = await _modManager.DownloadLatestPackageAsync(mirror, custom, cancellationToken: default, progress: reporter).ConfigureAwait(true);
            var manifest = ModManager.ParsePackageManifest(bytes);
            var installReporter = reporter;
            await Task.Run(() => _modManager.InstallPackage(directory, bytes, overwriteDll, allowDowngrade, installReporter))
                .ConfigureAwait(true);

            var versionText = string.IsNullOrEmpty(manifest?.Version) ? "" : $"（{manifest.Version}）";
            Post(new { type = "toast", message = $"加载器已更新{versionText}。" + (SpeedhackManager.IsGameRunning() ? "游戏正在运行，重启游戏后生效。" : "") });
            _ = PushModStatusAsync();
            _ = PostSteamBypassSyncAsync();   // 刚更新完，首页的启动方式单选框跟上新配置
        }
        catch (Exception ex)
        {
            ToysLog.Error("modloader", "从 GitHub 更新加载器失败", ex);
            Post(new { type = "modLoaderProgress", payload = new { phase = "error", percent = -1, message = ex.Message, active = false } });
            Post(new { type = "error", message = $"更新失败：{ex.Message}" });
        }
        finally
        {
            _modUpdateRunning = false;
            _ = PushModStatusAsync();
        }
    }


    private void PushSkinProfiles()
    {
        try
        {
            var directory = _modManager.GetStatus().GameDirectory;
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                Post(new { type = "skinsProfiles", payload = new { installed = false, activeSkin = "default", profiles = Array.Empty<SkinProfile>() } });
                return;
            }

            var profiles = _skinManager.GetProfiles(directory);
            var activeSkin = _skinManager.GetActiveSkin(directory);
            Post(new { type = "skinsProfiles", payload = new { installed = true, activeSkin, profiles } });
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"读取皮肤方案失败: {ex.Message}" });
        }
    }

    private void HandleSkinImportZip()
    {
        try
        {
            var directory = RequireModGameDirectory();
            var dialog = new OpenFileDialog
            {
                Title = "选择皮肤压缩包 (支持 cesium-default-skins 或自定义皮肤 ZIP)",
                Filter = "皮肤压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                var importedName = _skinManager.ImportSkinZip(directory, dialog.FileName);
                Post(new { type = "toast", message = $"成功导入皮肤方案: {importedName}" });
                PushSkinProfiles();
            }
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"导入皮肤失败: {ex.Message}" });
        }
    }

    private void HandleSkinDuplicate(JsonElement root)
    {
        try
        {
            var directory = RequireModGameDirectory();
            var source = root.TryGetProperty("source", out var srcElem) ? srcElem.GetString() : "default";
            var newName = root.TryGetProperty("newName", out var nameElem) ? nameElem.GetString() : null;
            var displayName = root.TryGetProperty("displayName", out var dispElem) ? dispElem.GetString() : newName;
            var author = root.TryGetProperty("author", out var authElem) ? authElem.GetString() : "";
            var desc = root.TryGetProperty("description", out var descElem) ? descElem.GetString() : "";

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(newName))
            {
                Post(new { type = "toast", message = "方案名称不能为空" });
                return;
            }

            var created = _skinManager.DuplicateProfile(directory, source, newName, displayName ?? newName, author ?? "", desc ?? "");
            Post(new { type = "toast", message = $"已创建新皮肤方案: {created}" });
            PushSkinProfiles();
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"新建方案失败: {ex.Message}" });
        }
    }

    private (string Token, string Directory, string Name, string Path)? _pendingSkinExport;
    private void HandleExportSkin(JsonElement root, bool confirmed)
    {
        try
        {
            string directory, name, path;
            if (confirmed)
            {
                if (_pendingSkinExport is not { } pending || root.GetProperty("token").GetString() != pending.Token) throw new InvalidOperationException("导出确认已过期，请重新导出。");
                _pendingSkinExport = null;
                directory = pending.Directory; name = pending.Name; path = pending.Path;
                if (!directory.Equals(RequireModGameDirectory(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("当前游戏已切换，请重新导出。");
            }
            else
            {
                _pendingSkinExport = null;
                name = root.GetProperty("name").GetString() ?? "";
                if (name == SkinManager.GameDefault) throw new InvalidOperationException("游戏默认方案没有外部图片可导出。");
                directory = RequireModGameDirectory();
                var save = new SaveFileDialog { Title = "导出皮肤方案 ZIP", Filter = "ZIP 压缩包 (*.zip)|*.zip", DefaultExt = ".zip", AddExtension = true, FileName = name + ".zip", OverwritePrompt = false };
                if (save.ShowDialog(this) != true) return;
                path = save.FileName;
                if (File.Exists(path)) {
                    var token = Guid.NewGuid().ToString("N");
                    _pendingSkinExport = (token, directory, name, path);
                    Post(new { type = "skinsExportConfirm", payload = new { token, fileName = Path.GetFileName(path) } });
                    return;
                }
            }
            _skinManager.ExportSkinZip(directory, name, path);
            Post(new { type = "toast", message = "皮肤方案已导出为 ZIP，可通过导入 ZIP 使用。" });
        }
        catch (Exception ex) { Post(new { type = "toast", message = "导出失败：" + ex.Message }); }
    }
    private bool _skinDownloadRunning;
    private async Task DownloadDefaultSkinsAsync(bool overwrite)
    {
        if (_skinDownloadRunning) return;
        string? temporary = null;
        try
        {
            var directory = RequireModGameDirectory();
            var defaultDirectory = Path.Combine(_skinManager.GetSkinsDirectory(directory), "default");
            if (Directory.Exists(defaultDirectory) && Directory.EnumerateFiles(defaultDirectory).Any()
                && !overwrite) { Post(new { type = "skinsDownloadConfirm" }); return; }
            _skinDownloadRunning = true;
            Post(new { type = "skinsDownloadState", payload = new { running = true } });
            temporary = Path.Combine(Path.GetTempPath(), "cesium-default-skins-" + Guid.NewGuid().ToString("N") + ".zip");
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            using var response = await client.GetAsync("https://github.com/higashitaniyume/CesiumLoader/releases/latest/download/cesium-default-skins.zip", System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new InvalidOperationException("最新 Release 尚未提供默认皮肤包，请先发布包含 cesium-default-skins.zip 的版本。");
            response.EnsureSuccessStatusCode();
            using (var input = await response.Content.ReadAsStreamAsync())
            using (var output = File.Create(temporary))
            {
                var buffer = new byte[81920]; long total = 0; int count;
                while ((count = await input.ReadAsync(buffer)) > 0)
                {
                    total += count;
                    if (total > 256L * 1024 * 1024) throw new InvalidOperationException("默认皮肤包超过 256 MB");
                    await output.WriteAsync(buffer.AsMemory(0, count));
                }
            }
            // 下载完整后再导入，只允许 default 下的图片和元数据。
            using (var archive = System.IO.Compression.ZipFile.OpenRead(temporary))
            {
                var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToArray();
                if (!entries.Any(e => Path.GetExtension(e.Name).Equals(".png", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("默认包中没有卡面图片");
                foreach (var entry in entries)
                {
                    var relative = entry.FullName.Replace('\\', '/');
                    if (!relative.StartsWith("AstralParty_ModLoader/skins/default/", StringComparison.Ordinal)
                        || entry.Length > 32L * 1024 * 1024) throw new InvalidOperationException("默认包内容不符合预期");
                }
                if (entries.Sum(e => e.Length) > 512L * 1024 * 1024) throw new InvalidOperationException("默认包解压体积过大");
            }
            _skinManager.ImportSkinZip(directory, temporary);
            PushSkinProfiles();
            Post(new { type = "toast", message = "默认皮肤包已下载并安装，可选择 default 方案使用。" });
        }
        catch (Exception ex) { Post(new { type = "toast", message = "下载默认包失败：" + ex.Message }); }
        finally
        {
            _skinDownloadRunning = false;
            Post(new { type = "skinsDownloadState", payload = new { running = false } });
            if (temporary != null && File.Exists(temporary)) File.Delete(temporary);
        }
    }
    private (string Token, string Directory, string Skin, string File, System.Windows.Media.Imaging.BitmapSource Image)? _pendingSkinCrop;
    private void HandleSaveSkinCrop(JsonElement root)
    {
        var token = root.TryGetProperty("token", out var value) ? value.GetString() : null;
        try
        {
            if (_pendingSkinCrop is not { } pending || pending.Token != token) throw new InvalidOperationException("裁剪已取消或过期，请重新选择图片。");
            if (!string.Equals(pending.Directory, RequireModGameDirectory(), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("当前游戏已切换，请重新选择图片。");
            var x = root.GetProperty("x").GetInt32(); var y = root.GetProperty("y").GetInt32(); var size = root.GetProperty("size").GetInt32();
            var image = SkinCropImage.RenderSquare(pending.Image, new System.Windows.Int32Rect(x, y, size, size));
            var updated = _skinManager.SaveCroppedCard(pending.Directory, pending.Skin, pending.File, image);
            _pendingSkinCrop = null;
            Post(new { type = "skinsCropSaved", payload = new { token, success = true } });
            Post(new { type = "skinsCardUpdated", payload = new { skinName = pending.Skin, card = updated } });
            PushSkinProfiles();
            Post(new { type = "toast", message = "裁剪卡面已保存，重启游戏后生效。" });
        }
        catch (Exception ex) { Post(new { type = "skinsCropSaved", payload = new { token, success = false, error = ex.Message } }); }
    }
    private void HandlePickAndReplaceCard(JsonElement root)
    {
        try
        {
            if (_cardAnimationConversion != null) throw new InvalidOperationException("请先等待或取消当前动态卡面转换。");
            var skinName = root.TryGetProperty("skinName", out var sElem) ? sElem.GetString() : null;
            var fileName = root.TryGetProperty("fileName", out var fElem) ? fElem.GetString() : null;
            if (string.IsNullOrWhiteSpace(skinName) || string.IsNullOrWhiteSpace(fileName))
            {
                Post(new { type = "toast", message = "参数缺失" });
                return;
            }

            var directory = RequireModGameDirectory();
            var dialog = new OpenFileDialog
            {
                Title = $"选择替换卡面图片 ({fileName})",
                Filter = "图片文件 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                var image = SkinCropImage.Load(dialog.FileName);
                var token = Guid.NewGuid().ToString("N");
                _pendingSkinCrop = (token, directory, skinName, fileName, image);
                Post(new { type = "skinsCropSource", payload = new { token, fileName, width = image.PixelWidth, height = image.PixelHeight, previewUrl = SkinCropImage.Preview(image) } });
            }
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"替换卡面失败：{ex.Message}" });
        }
    }

    private void HandleRevertCard(JsonElement root)
    {
        try
        {
            if (_cardAnimationConversion != null) throw new InvalidOperationException("请先等待或取消当前动态卡面转换。");
            var skinName = root.TryGetProperty("skinName", out var sElem) ? sElem.GetString() : null;
            var fileName = root.TryGetProperty("fileName", out var fElem) ? fElem.GetString() : null;
            if (string.IsNullOrWhiteSpace(skinName) || string.IsNullOrWhiteSpace(fileName))
            {
                Post(new { type = "toast", message = "参数缺失" });
                return;
            }

            var directory = RequireModGameDirectory();
            var updated = _skinManager.RevertCardImage(directory, skinName, fileName);
            Post(new { type = "toast", message = $"已还原卡面为默认：{updated.CardName} ({fileName})" });
            Post(new { type = "skinsCardUpdated", payload = new { skinName, card = updated } });
            PushSkinProfiles();
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"还原卡面失败：{ex.Message}" });
        }
    }

}
