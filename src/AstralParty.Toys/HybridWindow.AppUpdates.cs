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
            _targetUpdatePackage = result.TargetPackage;

            if (result.HasUpdate && _targetUpdatePackage != null)
            {
                PostUpdateState(AppUpdateStatus.UpdateAvailable);
                if (userInitiated)
                {
                    Post(new { type = "toast", message = $"发现新版本 v{result.Manifest?.Version}" });
                }

                // 若开启了自动下载，后台静默拉取安装包
                if (settings.AutoDownloadUpdates)
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

    /// <summary>
    /// 开始下载目标安装包。
    /// </summary>
    private async Task StartAppUpdateDownloadAsync()
    {
        if (_targetUpdatePackage == null)
        {
            Post(new { type = "toast", message = "没有可下载的更新安装包。" });
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

            _downloadedUpdateInstallerPath = downloadedFile;
            PostUpdateState(AppUpdateStatus.ReadyToInstall, percent: 100);
            Post(new { type = "toast", message = $"新版本 v{_latestUpdateManifest?.Version} 安装包已下载就绪。" });

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

        try
        {
            AppUpdateService.LaunchInstaller(_downloadedUpdateInstallerPath, silent);
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"启动安装器失败：{ex.Message}" });
        }
    }

    /// <summary>
    /// 应用启动就绪后延迟触发自动检查。
    /// </summary>
    private void ScheduleStartupUpdateCheck()
    {
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
