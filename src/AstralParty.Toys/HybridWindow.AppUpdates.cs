using System.Diagnostics;
using System.IO;
using System.Windows;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private readonly AppUpdateService _appUpdateService = new();
    private CancellationTokenSource? _updateDownloadCts;
    private AppVersionManifest? _latestUpdateManifest;
    private AppAssetPackageInfo? _targetUpdatePackage;
    private string? _downloadedUpdateInstallerPath;
    private AppUpdateStatus _currentUpdateStatus = AppUpdateStatus.Idle;
    private AppUpdatePlan? _updatePlan;
    /// <summary>便携版：新版本解压出来的文件夹。</summary>
    private string? _updateStagingDirectory;
    /// <summary>单文件版 / 便携版的用户选了「改用安装版」。</summary>
    private bool _updateSwitchToInstaller;
    /// <summary>上次检查的结果里有没有比当前新的版本（切换更新方式后要回到同一个状态）。</summary>
    private bool _latestHasUpdate;

    /// <summary>
    /// 构建当前应用程序更新状态载荷。
    /// </summary>
    private AppUpdateState BuildUpdateStatePayload(
        AppUpdateStatus status,
        string? error = null,
        int? percent = null,
        long receivedBytes = 0,
        long totalBytes = 0)
    {
        _currentUpdateStatus = status;
        var env = _appUpdateService.GetEnvironment();
        return new AppUpdateState
        {
            Status = status.ToString().ToLowerInvariant(),
            CurrentVersion = AppInfo.Get().Version,
            LatestVersion = _latestUpdateManifest?.Version ?? "",
            ReleaseNotes = _latestUpdateManifest?.ReleaseNotes ?? "",
            ReleaseDate = _latestUpdateManifest?.ReleaseDate ?? "",
            RuntimeKind = env.RuntimeKind switch
            {
                AppRuntimeKind.SelfContained => "self-contained",
                _ => "framework-dependent"
            },
            IsInstaller = env.IsInstaller,
            PackageForm = env.PackageForm switch
            {
                AppPackageForm.Installer => "installer",
                AppPackageForm.SingleFile => "single-file",
                AppPackageForm.Portable => "portable",
                _ => "development"
            },
            UpdateMode = (_updatePlan?.Mode ?? AppUpdateMode.Manual) switch
            {
                AppUpdateMode.Installer => "installer",
                AppUpdateMode.ReplaceExe => "replace-exe",
                AppUpdateMode.ReplaceFolder => "replace-folder",
                _ => "manual"
            },
            ManualReason = _updatePlan?.ManualReason ?? "",
            SwitchToInstaller = _updateSwitchToInstaller,
            TargetLocation = env.PackageForm == AppPackageForm.SingleFile ? env.ExePath : Path.GetDirectoryName(env.ExePath) ?? "",
            ReleaseUrl = AppUpdateService.ReleasePageUrl(_latestUpdateManifest),
            TargetFileName = _targetUpdatePackage?.FileName ?? "",
            DownloadedFilePath = _downloadedUpdateInstallerPath,
            Percent = percent,
            ReceivedBytes = receivedBytes,
            TotalBytes = totalBytes > 0 ? totalBytes : (_targetUpdatePackage?.Size ?? 0),
            Error = error
        };
    }

    private void PostUpdateState(
        AppUpdateStatus status,
        string? error = null,
        int? percent = null,
        long receivedBytes = 0,
        long totalBytes = 0)
    {
        try
        {
            Post(new
            {
                type = "appUpdateState",
                payload = BuildUpdateStatePayload(status, error, percent, receivedBytes, totalBytes)
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PostUpdateState failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 检查更新。
    /// </summary>
    private async Task CheckAppUpdateAsync(bool userInitiated)
    {
        if (_currentUpdateStatus is AppUpdateStatus.Checking or AppUpdateStatus.Downloading)
        {
            if (userInitiated) Post(new { type = "toast", message = "正在执行更新操作中，请稍候…" });
            return;
        }

        PostUpdateState(AppUpdateStatus.Checking);

        var settings = _libraryService.Settings;
        var mirrorType = settings.GitHubMirrorType;
        var customUrl = settings.CustomGitHubMirrorUrl;

        try
        {
            var result = await _appUpdateService.CheckUpdateAsync(
                currentVersion: AppInfo.Get().Version,
                mirrorType: mirrorType,
                customMirrorUrl: customUrl).ConfigureAwait(true);

            if (result.Error != null)
            {
                PostUpdateState(AppUpdateStatus.Error, error: result.Error);
                if (userInitiated)
                {
                    Post(new { type = "toast", message = $"检查更新失败：{result.Error}" });
                }
                return;
            }

            _latestUpdateManifest = result.Manifest;
            _latestHasUpdate = result.HasUpdate && result.Manifest != null;
            RefreshUpdatePlan();

            if (result.HasUpdate && result.Manifest != null)
            {
                // 不能自动装（Manual）也要报告有新版本：界面会让用户选「改用安装版」或自己下载
                PostUpdateState(AppUpdateStatus.UpdateAvailable);
                if (userInitiated)
                {
                    Post(new { type = "toast", message = $"发现新版本 v{result.Manifest?.Version}" });
                }

                // 若开启了自动下载，后台静默拉取安装包（不会替用户「改用安装版」）
                if (settings.AutoDownloadUpdates && _targetUpdatePackage != null)
                {
                    _ = StartAppUpdateDownloadAsync();
                }
            }
            else
            {
                PostUpdateState(AppUpdateStatus.UpToDate);
                if (userInitiated)
                {
                    Post(new { type = "toast", message = "当前已是最新版本。" });
                }
            }
        }
        catch (Exception ex)
        {
            PostUpdateState(AppUpdateStatus.Error, error: ex.Message);
            if (userInitiated)
            {
                Post(new { type = "toast", message = $"检查更新出错：{ex.Message}" });
            }
        }
    }

    /// <summary>按当前形态和用户的选择重新决定更新方式；换了方式，之前下好的包就不能用了。</summary>
    private void RefreshUpdatePlan()
    {
        var previous = _targetUpdatePackage;
        _updatePlan = _latestUpdateManifest == null
            ? null
            : AppUpdateService.PlanUpdate(_latestUpdateManifest, _appUpdateService.GetEnvironment(), _updateSwitchToInstaller);
        _targetUpdatePackage = _updatePlan?.Package;
        if (!ReferenceEquals(previous, _targetUpdatePackage))
        {
            _downloadedUpdateInstallerPath = null;
            _updateStagingDirectory = null;
        }
    }

    /// <summary>
    /// 单文件版 / 便携版：用户选择改用安装版（或改回原地替换）。
    /// </summary>
    private void SetAppUpdateSwitchToInstaller(bool on)
    {
        if (_currentUpdateStatus is AppUpdateStatus.Checking or AppUpdateStatus.Downloading or AppUpdateStatus.Installing) return;
        if (_appUpdateService.GetEnvironment().PackageForm == AppPackageForm.Installer) return;
        _updateSwitchToInstaller = on;
        RefreshUpdatePlan();
        PostUpdateState(_latestUpdateManifest == null ? AppUpdateStatus.Idle
            : !_latestHasUpdate ? AppUpdateStatus.UpToDate
            : _downloadedUpdateInstallerPath != null ? AppUpdateStatus.ReadyToInstall
            : AppUpdateStatus.UpdateAvailable);
    }

    /// <summary>
    /// 开始下载目标安装包。
    /// </summary>
    private async Task StartAppUpdateDownloadAsync()
    {
        if (_targetUpdatePackage == null)
        {
            var reason = _updatePlan?.ManualReason;
            Post(new { type = "toast", message = string.IsNullOrEmpty(reason) ? "没有可下载的更新安装包。" : $"不能自动更新：{reason}" });
            return;
        }

        if (_currentUpdateStatus == AppUpdateStatus.Downloading) return;

        _updateDownloadCts?.Cancel();
        _updateDownloadCts = new CancellationTokenSource();
        var ct = _updateDownloadCts.Token;

        var settings = _libraryService.Settings;
        var mirrorType = settings.GitHubMirrorType;
        var customUrl = settings.CustomGitHubMirrorUrl;

        PostUpdateState(AppUpdateStatus.Downloading, percent: 0);

        var lastReport = DateTime.UtcNow;
        var progress = new Progress<(long received, long total, int? percent)>(p =>
        {
            var now = DateTime.UtcNow;
            if ((now - lastReport).TotalMilliseconds >= 250 || p.percent == 100)
            {
                lastReport = now;
                PostUpdateState(AppUpdateStatus.Downloading, percent: p.percent, receivedBytes: p.received, totalBytes: p.total);
            }
        });

        try
        {
            var downloadedFile = await _appUpdateService.DownloadPackageAsync(
                _targetUpdatePackage,
                mirrorType,
                customUrl,
                progress,
                ct).ConfigureAwait(true);

            if (_updatePlan?.Mode == AppUpdateMode.ReplaceFolder)
            {
                // 便携版：先解压、确认是完整的便携版，再算「已就绪」
                _updateStagingDirectory = await Task.Run(() => AppUpdateService.ExtractPortablePackage(downloadedFile), ct).ConfigureAwait(true);
            }
            _downloadedUpdateInstallerPath = downloadedFile;
            PostUpdateState(AppUpdateStatus.ReadyToInstall, percent: 100);
            Post(new { type = "toast", message = $"新版本 v{_latestUpdateManifest?.Version} 已下载就绪。" });

            // 若开启了自动安装：拉起安装器并退出当前 Toys
            if (settings.AutoInstallUpdates)
            {
                ApplyAppUpdate(silent: true);
            }
        }
        catch (OperationCanceledException)
        {
            PostUpdateState(AppUpdateStatus.UpdateAvailable);
            Post(new { type = "toast", message = "已取消下载更新。" });
        }
        catch (Exception ex)
        {
            PostUpdateState(AppUpdateStatus.Error, error: ex.Message);
            Post(new { type = "toast", message = $"更新包下载失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// 取消正在进行的更新下载。
    /// </summary>
    private void CancelAppUpdateDownload()
    {
        _updateDownloadCts?.Cancel();
    }

    /// <summary>
    /// 应用已下载的安装包，退出当前进程完成升级。
    /// </summary>
    private void ApplyAppUpdate(bool silent = true)
    {
        if (string.IsNullOrWhiteSpace(_downloadedUpdateInstallerPath) || !File.Exists(_downloadedUpdateInstallerPath))
        {
            Post(new { type = "toast", message = "未找到已就绪的安装包，请重新下载。" });
            return;
        }

        var mode = _updatePlan?.Mode ?? AppUpdateMode.Installer;
        try
        {
            switch (mode)
            {
                case AppUpdateMode.ReplaceExe:
                case AppUpdateMode.ReplaceFolder:
                    LaunchSelfReplace(mode);
                    break;
                default:
                    AppUpdateService.LaunchInstaller(_downloadedUpdateInstallerPath, silent, addDesktopShortcut: _updateSwitchToInstaller);
                    break;
            }
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            if (_currentUpdateStatus == AppUpdateStatus.Installing) PostUpdateState(AppUpdateStatus.ReadyToInstall);
            Post(new { type = "toast", message = mode == AppUpdateMode.Installer ? $"启动安装器失败：{ex.Message}" : $"没能开始替换，程序没有改动：{ex.Message}" });
        }
    }

    /// <summary>单文件版换 exe、便携版覆盖文件夹：都交给退出后运行的脚本。抛异常时什么都还没改。</summary>
    private void LaunchSelfReplace(AppUpdateMode mode)
    {
        var env = _appUpdateService.GetEnvironment();
        var exe = env.ExePath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) throw new InvalidOperationException("找不到正在运行的程序文件。");
        var plan = new AppReplacePlan
        {
            ProcessId = Environment.ProcessId,
            Mode = mode,
            CurrentExe = exe,
            NewExe = mode == AppUpdateMode.ReplaceExe ? _downloadedUpdateInstallerPath! : "",
            StagingDirectory = mode == AppUpdateMode.ReplaceFolder ? _updateStagingDirectory ?? throw new InvalidOperationException("新版本还没解压好，请重新下载。") : "",
            AppDirectory = mode == AppUpdateMode.ReplaceFolder ? Path.GetDirectoryName(exe)! : "",
            BackupDirectory = mode == AppUpdateMode.ReplaceFolder ? Path.Combine(AppSelfUpdater.UpdateTempDirectory, $"backup-{Guid.NewGuid():N}") : "",
            DownloadedFile = mode == AppUpdateMode.ReplaceFolder ? _downloadedUpdateInstallerPath! : "",
            ResultFile = AppSelfUpdater.DefaultResultFile,
            NewVersion = _latestUpdateManifest?.Version ?? ""
        };
        if (mode == AppUpdateMode.ReplaceFolder)
            AppSelfUpdater.BackupOverwrittenFiles(plan.StagingDirectory, plan.AppDirectory, plan.BackupDirectory);
        PostUpdateState(AppUpdateStatus.Installing);
        AppSelfUpdater.Launch(plan);
    }

    /// <summary>上一次原地更新的结果（脚本在重新打开程序前写下），启动后告诉用户一次。</summary>
    private void ReportLastSelfUpdate()
    {
        var result = AppSelfUpdater.TakeLastResult();
        if (result == null) return;
        var (ok, version) = result.Value;
        var v = string.IsNullOrEmpty(version) ? "新版本" : $"v{version}";
        Post(new { type = "toast", message = ok ? $"已更新到 {v}。" : $"更新到 {v} 没有完成，已还原成原来的版本。可以稍后再试，或者到发布页手动下载。" });
    }

    /// <summary>
    /// 应用启动就绪后延迟触发自动检查。
    /// </summary>
    private void ScheduleStartupUpdateCheck()
    {
        ReportLastSelfUpdate();
        if (!_libraryService.Settings.AutoCheckUpdates) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(2500).ConfigureAwait(false);
                await Dispatcher.InvokeAsync(() => CheckAppUpdateAsync(userInitiated: false));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ScheduleStartupUpdateCheck failed: {ex.Message}");
            }
        });
    }
}
