using System.Diagnostics;
using AstralParty.Toys.Services;
using AstralParty.Toys.Services.App;
using AstralParty.Toys.Services.Modding;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private string _announcedAppUpdateChangelogVersion = "";
    private string _announcedLoaderUpdateChangelogVersion = "";

    /// <summary>
    /// 启动就绪后检查是否为全新安装或工具箱版本刚更新完成；若满足条件且未关闭自动弹窗，则弹出完整更新日志弹窗。
    /// </summary>
    private async Task CheckStartupUpdatedChangelogAsync(SelfUpdateOutcome? selfUpdateOutcome)
    {
        try
        {
            var store = ToysConfigStore.ForRoot(ToysPaths.Root);
            var config = store.Load();
            var currentAppVersion = AppInfo.Get().Version;
            var currentEmbeddedLoader = ModManager.BundleLoaderVersion;

            if (!config.Update.AutoChangelogPopup)
            {
                // 用户已关闭自动弹窗时，仍静默同步当前版本号，避免日后重新开启开关时对当前版误弹。
                SyncSeenChangelogVersions(currentAppVersion, currentEmbeddedLoader);
                return;
            }

            var decision = UpdateChangelogService.EvaluateStartup(
                currentAppVersion,
                currentEmbeddedLoader,
                selfUpdateOutcome,
                config.Update);

            if (decision is null) return;

            await PushChangelogModalAsync(
                trigger: decision.Trigger,
                defaultTab: "app",
                isAutoPopup: true,
                appPrevVersion: decision.PrevAppVersion,
                appTargetVersion: decision.CurrentAppVersion,
                loaderPrevVersion: decision.PrevLoaderVersion,
                loaderTargetVersion: decision.CurrentLoaderVersion,
                preferRemote: false).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"CheckStartupUpdatedChangelogAsync failed: {ex.Message}");
        }
    }

    private static void SyncSeenChangelogVersions(string? appVersion = null, string? embeddedLoaderVersion = null)
    {
        var currentApp = string.IsNullOrWhiteSpace(appVersion) ? AppInfo.Get().Version : appVersion.Trim();
        var currentLoader = embeddedLoaderVersion ?? ModManager.BundleLoaderVersion;
        if (string.IsNullOrWhiteSpace(currentApp)) return;

        ToysConfigStore.ForRoot(ToysPaths.Root).Update(cfg =>
        {
            cfg.Update.LastSeenAppVersion = currentApp;
            if (!string.IsNullOrWhiteSpace(currentLoader))
                cfg.Update.LastSeenEmbeddedLoaderVersion = currentLoader.Trim();
        });
    }

    private void HandleMarkChangelogSeen()
    {
        try
        {
            SyncSeenChangelogVersions();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HandleMarkChangelogSeen failed: {ex.Message}");
        }
    }

    private void HandleSetAutoChangelogPopup(bool enabled)
    {
        var cur = _libraryService.Settings;
        _libraryService.SaveSettings(new Services.Replay.ReplayLibrarySettings
        {
            AutoMaintain = cur.AutoMaintain,
            KeepInGame = cur.KeepInGame,
            Language = cur.Language,
            WorkshopUrl = cur.WorkshopUrl,
            AutoCheckUpdates = cur.AutoCheckUpdates,
            AutoDownloadUpdates = cur.AutoDownloadUpdates,
            AutoInstallUpdates = cur.AutoInstallUpdates,
            AutoChangelogPopup = enabled,
            GitHubMirrorType = cur.GitHubMirrorType,
            CustomGitHubMirrorUrl = cur.CustomGitHubMirrorUrl
        });
        SyncSeenChangelogVersions();
        Post(new { type = "librarySettings", payload = BuildLibrarySettingsPayload() });
        Post(new
        {
            type = "toast",
            message = enabled
                ? "已开启更新时自动弹出更新日志。"
                : "已永久关闭更新自动弹窗，可随时在「设置 → 软件更新」重新开启，或点击左上角图标查看。"
        });
    }

    /// <summary>
    /// 当发现新版本准备更新时触发（工具箱或加载器）。
    /// </summary>
    private async Task AnnounceUpdateAvailableChangelogAsync(
        string trigger,
        string defaultTab,
        bool userInitiated,
        string? appPrevVersion = null,
        string? appTargetVersion = null,
        string? loaderPrevVersion = null,
        string? loaderTargetVersion = null)
    {
        if (!_libraryService.Settings.AutoChangelogPopup) return;

        if (trigger == "app-update-available" && !string.IsNullOrWhiteSpace(appTargetVersion))
        {
            if (!userInitiated && string.Equals(_announcedAppUpdateChangelogVersion, appTargetVersion, StringComparison.OrdinalIgnoreCase))
                return;
            _announcedAppUpdateChangelogVersion = appTargetVersion;
        }
        else if (trigger == "loader-update-available" && !string.IsNullOrWhiteSpace(loaderTargetVersion))
        {
            if (!userInitiated && string.Equals(_announcedLoaderUpdateChangelogVersion, loaderTargetVersion, StringComparison.OrdinalIgnoreCase))
                return;
            _announcedLoaderUpdateChangelogVersion = loaderTargetVersion;
        }

        await PushChangelogModalAsync(
            trigger: trigger,
            defaultTab: defaultTab,
            isAutoPopup: true,
            appPrevVersion: appPrevVersion,
            appTargetVersion: appTargetVersion,
            loaderPrevVersion: loaderPrevVersion,
            loaderTargetVersion: loaderTargetVersion,
            preferRemote: true).ConfigureAwait(true);
    }

    /// <summary>
    /// 加载器更新完成时弹出完整更新日志（优先用刚下载包里的 CHANGELOG.md，同时异步刷新远端）。
    /// </summary>
    private Task AnnounceLoaderUpdatedChangelogAsync(
        string? loaderPrevVersion,
        string? loaderTargetVersion,
        string? extractedLoaderChangelog = null)
    {
        if (!_libraryService.Settings.AutoChangelogPopup) return Task.CompletedTask;
        return PushChangelogModalAsync(
            trigger: "loader-updated",
            defaultTab: "loader",
            isAutoPopup: true,
            loaderPrevVersion: loaderPrevVersion,
            loaderTargetVersion: loaderTargetVersion,
            extractedLoaderChangelog: extractedLoaderChangelog,
            preferRemote: false);
    }

    /// <summary>
    /// 统一构建并推送更新日志弹窗载荷（始终完整渲染，不裁剪内容）。
    /// </summary>
    private async Task PushChangelogModalAsync(
        string trigger,
        string defaultTab,
        bool isAutoPopup,
        string? appPrevVersion = null,
        string? appTargetVersion = null,
        string? loaderPrevVersion = null,
        string? loaderTargetVersion = null,
        string? extractedLoaderChangelog = null,
        bool preferRemote = true)
    {
        if (isAutoPopup && !_libraryService.Settings.AutoChangelogPopup) return;

        string? installedLoaderVersion = null;
        try
        {
            installedLoaderVersion = await Task.Run(() => _modManager.GetStatus().InstalledVersion).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PushChangelogModalAsync GetStatus failed: {ex.Message}");
        }

        var localApp = UpdateChangelogService.NormalizeHeader(BundledAppChangelog(), isLoader: false);
        var localLoaderRaw = !string.IsNullOrWhiteSpace(extractedLoaderChangelog)
            ? extractedLoaderChangelog
            : ModManager.BundledLoaderChangelog;
        var localLoader = UpdateChangelogService.NormalizeHeader(localLoaderRaw, isLoader: true);
        var localLoaderSource = !string.IsNullOrWhiteSpace(extractedLoaderChangelog)
            ? "package"
            : (!string.IsNullOrWhiteSpace(localLoader) ? "bundled" : "none");

        if (!preferRemote && (!string.IsNullOrWhiteSpace(localApp) || !string.IsNullOrWhiteSpace(localLoader)))
        {
            // 更新完毕 / 首次安装 / 手动点击：立即使用本地日志秒开弹窗，同时后台拉取远端更新内容。
            PostChangelogModalPayload(
                "changelogModal",
                trigger,
                defaultTab,
                isAutoPopup,
                appPrevVersion,
                appTargetVersion,
                localApp,
                "bundled",
                loaderPrevVersion,
                installedLoaderVersion,
                loaderTargetVersion,
                localLoader,
                localLoaderSource);

            _ = RefreshChangelogModalFromRemoteAsync(localApp, localLoader);
            return;
        }

        // 发现新版本准备更新（或本地为空）：并行从 GitHub 镜像拉取两份完整 CHANGELOG.md，失败回退本地。
        var (remoteApp, remoteLoader, remoteLoaderSource) = await FetchRemoteChangelogsAsync(localApp, localLoader, localLoaderSource).ConfigureAwait(true);
        PostChangelogModalPayload(
            "changelogModal",
            trigger,
            defaultTab,
            isAutoPopup,
            appPrevVersion,
            appTargetVersion,
            remoteApp,
            string.Equals(remoteApp, localApp, StringComparison.Ordinal) ? "bundled" : "github",
            loaderPrevVersion,
            installedLoaderVersion,
            loaderTargetVersion,
            remoteLoader,
            remoteLoaderSource);
    }

    private async Task RefreshChangelogModalFromRemoteAsync(string previousApp, string previousLoader)
    {
        try
        {
            var (remoteApp, remoteLoader, remoteLoaderSource) = await FetchRemoteChangelogsAsync(previousApp, previousLoader, "bundled").ConfigureAwait(true);
            var appChanged = !string.IsNullOrWhiteSpace(remoteApp) && !string.Equals(remoteApp, previousApp, StringComparison.Ordinal);
            var loaderChanged = !string.IsNullOrWhiteSpace(remoteLoader) && !string.Equals(remoteLoader, previousLoader, StringComparison.Ordinal);
            if (!appChanged && !loaderChanged) return;

            Post(new
            {
                type = "changelogModalRefresh",
                payload = new
                {
                    appMarkdown = remoteApp,
                    appSource = appChanged ? "github" : "bundled",
                    loaderMarkdown = remoteLoader,
                    loaderSource = remoteLoaderSource
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RefreshChangelogModalFromRemoteAsync failed: {ex.Message}");
        }
    }

    private async Task<(string AppMarkdown, string LoaderMarkdown, string LoaderSource)> FetchRemoteChangelogsAsync(
        string fallbackApp,
        string fallbackLoader,
        string fallbackLoaderSource)
    {
        var settings = _libraryService.Settings;
        var mirror = settings.GitHubMirrorType;
        var custom = settings.CustomGitHubMirrorUrl;

        var appTask = TryDownloadNormalizedChangelogAsync(ModManager.AppRepo, isLoader: false, mirror, custom);
        var loaderTask = TryDownloadNormalizedChangelogAsync(ModManager.LoaderRepo, isLoader: true, mirror, custom);
        await Task.WhenAll(appTask, loaderTask).ConfigureAwait(true);

        var remoteApp = await appTask;
        var remoteLoader = await loaderTask;

        var appMarkdown = !string.IsNullOrWhiteSpace(remoteApp) ? remoteApp : fallbackApp;
        var loaderMarkdown = !string.IsNullOrWhiteSpace(remoteLoader) ? remoteLoader : fallbackLoader;
        var loaderSource = !string.IsNullOrWhiteSpace(remoteLoader) ? "github" : fallbackLoaderSource;
        return (appMarkdown, loaderMarkdown, loaderSource);
    }

    private static async Task<string?> TryDownloadNormalizedChangelogAsync(
        string repo,
        bool isLoader,
        string? mirror,
        string? custom)
    {
        try
        {
            var raw = await ModManager.DownloadRepoChangelogAsync(repo, mirror, custom).ConfigureAwait(false);
            return UpdateChangelogService.NormalizeHeader(raw, isLoader);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DownloadRepoChangelogAsync({repo}) failed: {ex.Message}");
            return null;
        }
    }

    private void PostChangelogModalPayload(
        string messageType,
        string trigger,
        string defaultTab,
        bool isAutoPopup,
        string? appPrevVersion,
        string? appTargetVersion,
        string appMarkdown,
        string appSource,
        string? loaderPrevVersion,
        string? loaderInstalledVersion,
        string? loaderTargetVersion,
        string loaderMarkdown,
        string loaderSource)
    {
        var info = AppInfo.Get();
        Post(new
        {
            type = messageType,
            payload = new
            {
                trigger,
                defaultTab = defaultTab == "loader" ? "loader" : "app",
                isAutoPopup,
                autoChangelogPopup = _libraryService.Settings.AutoChangelogPopup,
                appPrevVersion = appPrevVersion ?? "",
                appCurrentVersion = info.Version,
                appTargetVersion = appTargetVersion ?? "",
                appSource,
                appMarkdown,
                loaderPrevVersion = loaderPrevVersion ?? "",
                loaderCurrentVersion = loaderInstalledVersion ?? "",
                loaderEmbeddedVersion = ModManager.BundleLoaderVersion,
                loaderTargetVersion = loaderTargetVersion ?? "",
                loaderSource,
                loaderMarkdown
            }
        });
    }
}
