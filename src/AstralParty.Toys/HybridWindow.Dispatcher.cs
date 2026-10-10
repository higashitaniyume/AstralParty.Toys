using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

public partial class HybridWindow
{
    private async void WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!e.Source.StartsWith("https://app.astral.local/", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            switch (type)
            {
                case "ready":
                    _webReady = true;
                    // 版本来自程序集，先发送，不能排在游戏目录扫描之后。
                    PostAppVersion();
                    StartupDetail.Text = "读取游戏档案，首次使用时检测 Steam 游戏目录";
                    try
                    {
                        // 目录提供者必须延迟调用：已有档案时不扫描 Steam。
                        // 扫描和迁移放到后台；初始化完成前保留加载层，避免操作与迁移同时写状态。
                        await Task.Run(() => _gameLibrary.SeedFromLegacyIfEmpty(() => new[]
                            {
                                _modManager.GetStoredGameDirectory(),
                                _speedhackManager.GetStoredGameDirectory()
                            }.Concat(SpeedhackManager.EnumerateGameDirectories())));
                        SyncActiveGameToManagers();
                        Post(new { type = "homeData", payload = _homeDataService.GetHomeData() });
                        _ = PostSteamBypassSyncAsync();
                        _ = PushGameProfilesAsync();
                    }
                    finally
                    {
                        StartupOverlay.Visibility = Visibility.Collapsed;
                    }
                    await SendReplayLibraryAsync(runMaintain: true);
                    Post(new { type = "librarySettings", payload = BuildLibrarySettingsPayload() });
                    var args = Environment.GetCommandLineArgs();
                    if (args.Length > 1 && File.Exists(args[1])) await LoadReplayAsync(args[1]);
                    break;
                case "getHomeData":
                    Post(new { type = "homeData", payload = _homeDataService.GetHomeData() });
                    _ = PostSteamBypassSyncAsync();
                    break;
                case "getAppVersion":
                    PostAppVersion();
                    break;
                case "copyVersionInfo":
                    CopyVersionInfo();
                    break;
                case "changelogGet":
                    PostChangelog();
                    break;
                case "loaderChangelogGet":
                    _ = PostLoaderChangelogAsync(root.TryGetProperty("fresh", out var freshEl) && freshEl.ValueKind == JsonValueKind.True);
                    break;
                case "openReplay":
                    await PickAndLoadReplayAsync();
                    break;
                case "openRecentReplay":
                    if (root.TryGetProperty("token", out var tokenElement) &&
                        tokenElement.GetString() is { Length: > 0 } token &&
                        _replayLibrary.TryGetValue(token, out var replayPath) && File.Exists(replayPath))
                        await LoadReplayAsync(replayPath);
                    else
                        Post(new { type = "toast", message = "这个回放找不到了（可能已被移动或删除），点「刷新列表」重新扫描。" });
                    break;
                case "refreshReplays":
                    await SendReplayLibraryAsync(runMaintain: true);
                    break;
                case "libraryArchive":
                    await RunLibraryOperationAsync(() => _libraryService.Archive(ReadIds(root)));
                    break;
                case "libraryArchiveOldest":
                    var archiveCount = root.TryGetProperty("count", out var countElement) && countElement.TryGetInt32(out var parsedCount)
                        ? Math.Clamp(parsedCount, 1, ReplayLibraryService.GameSlotCapacity)
                        : 3;
                    await RunLibraryOperationAsync(() => ArchiveOldest(archiveCount));
                    break;
                case "libraryRestore":
                    var allowUnparseable = root.TryGetProperty("allowUnparseable", out var allowElement) && allowElement.GetBoolean();
                    await RunLibraryOperationAsync(() => _libraryService.Restore(ReadIds(root), allowUnparseable));
                    break;
                case "libraryDelete":
                    var deleteTarget = (root.TryGetProperty("target", out var targetElement) ? targetElement.GetString() : null) switch
                    {
                        "game" => ReplayDeleteTarget.Game,
                        "both" => ReplayDeleteTarget.Both,
                        _ => ReplayDeleteTarget.Library
                    };
                    await RunLibraryOperationAsync(() => _libraryService.Delete(ReadIds(root), deleteTarget));
                    break;
                case "libraryMaintain":
                    await RunLibraryOperationAsync(() => _libraryService.Maintain());
                    break;
                case "libraryImport":
                    await HandleLibraryImportAsync();
                    break;
                case "replayDownload":
                    await HandleReplayDownloadAsync(root);
                    break;
                case "replayDownloadCancel":
                    _replayDownloadCts?.Cancel();
                    break;
                case "libraryOpenFolder":
                    HandleLibraryOpenFolder();
                    break;
                case "libraryBrowseRoot":
                    HandleLibraryBrowseRoot();
                    break;
                case "libraryGetSettings":
                    Post(new { type = "librarySettings", payload = BuildLibrarySettingsPayload() });
                    break;
                case "librarySaveSettings":
                    HandleLibrarySaveSettings(root);
                    break;
                case "gameProfilesGet":
                    _ = PushGameProfilesAsync();
                    break;
                case "gameProfileBrowse":
                    HandleGameProfileBrowse();
                    break;
                case "gameProfileAddPath":
                    if (root.TryGetProperty("directory", out var addDirElement) &&
                        addDirElement.GetString() is { Length: > 0 } addDir)
                        HandleGameProfileAddPath(addDir,
                            root.TryGetProperty("activate", out var actEl) && actEl.GetBoolean());
                    break;
                case "gameProfileSetActive":
                    if (root.TryGetProperty("id", out var setActiveIdEl) &&
                        setActiveIdEl.GetString() is { Length: > 0 } setActiveId)
                        HandleGameProfileSetActive(setActiveId);
                    break;
                case "gameProfileUpdate":
                    if (root.TryGetProperty("id", out var updIdEl) &&
                        updIdEl.GetString() is { Length: > 0 } updId)
                    {
                        var newLabel = root.TryGetProperty("label", out var lblEl) ? lblEl.GetString() : null;
                        var newEdition = root.TryGetProperty("edition", out var edEl) ? edEl.GetString() : null;
                        _gameLibrary.UpdateProfile(updId, newLabel, newEdition);
                        _ = PushGameProfilesAsync();
                    }
                    break;
                case "gameProfileRemove":
                    if (root.TryGetProperty("id", out var rmIdEl) &&
                        rmIdEl.GetString() is { Length: > 0 } rmId)
                    {
                        _gameLibrary.Remove(rmId);
                        SyncActiveGameToManagers();
                        _ = PushGameProfilesAsync();
                        _ = PushModStatusAsync();
                        _ = PushSpeedhackStatusAsync();
                    }
                    break;
                case "gameProfileOpen":
                    if (root.TryGetProperty("id", out var openIdEl) &&
                        openIdEl.GetString() is { Length: > 0 } openId)
                        HandleGameProfileOpen(openId);
                    break;
                case "gameScanQuick":
                    HandleGameScanQuick();
                    break;
                case "gameScanDeep":
                    HandleGameScanDeep(root.TryGetProperty("drive", out var driveEl) ? driveEl.GetString() : null);
                    break;
                case "gameScanCancel":
                    _deepScanCts?.Cancel();
                    break;
                case "getFrame":
                    if (root.TryGetProperty("index", out var indexElement)) await SendFrameDetailAsync(indexElement.GetInt32());
                    break;
                case "export":
                    await ExportAsync();
                    break;
                case "exportRelics":
                    var relicFormat = root.TryGetProperty("format", out var formatElement) ? formatElement.GetString() : "csv";
                    await ExportRelicsAsync(relicFormat);
                    break;
                case "exportShops":
                    await ExportShopsAsync("csv");
                    break;
                case "openClassic":
                    OpenClassicWindow();
                    break;
                case "copySpectatorLink":
                    try
                    {
                        Clipboard.SetText(SpectatorUri.AbsoluteUri);
                        Post(new { type = "spectatorLinkCopied", success = true });
                    }
                    catch (Exception)
                    {
                        Post(new { type = "spectatorLinkCopied", success = false });
                    }
                    break;
                case "openSpectator":
                    OpenSpectator();
                    break;
                case "openBrowser":
                    if (root.TryGetProperty("url", out var urlElement) &&
                        urlElement.GetString() is { Length: > 0 } targetUrl)
                    {
                        TryOpenExternal(targetUrl);
                    }
                    break;
                case "launchGame":
                    // 首页「启动游戏」下面那两个单选框决定走哪条路（默认：从 Steam 启动）
                    HandleLaunchGame(root.TryGetProperty("bypassSteam", out var bypassSteamElement) &&
                                     bypassSteamElement.GetBoolean());
                    break;
                case "setSteamBypass":
                    // 首页「绕过 Steam 启动」单选框 —— 与「加载器设置」里的同名开关是同一个配置
                    if (root.TryGetProperty("enabled", out var steamBypassElement))
                    {
                        HandleSetSteamBypass(steamBypassElement.GetBoolean());
                    }
                    break;
                case "speedhackStatus":
                    _ = PushSpeedhackStatusAsync();
                    break;
                case "speedhackDetect":
                    HandleSpeedhackDetect();
                    break;
                case "speedhackBrowse":
                    HandleSpeedhackBrowse();
                    break;
                case "speedhackInstall":
                    var overwriteDll = root.TryGetProperty("overwriteDll", out var overwriteElement) &&
                                       overwriteElement.GetBoolean();
                    HandleSpeedhackInstall(overwriteDll);
                    break;
                case "speedhackUninstall":
                    var force = root.TryGetProperty("force", out var forceElement) && forceElement.GetBoolean();
                    HandleSpeedhackUninstall(force);
                    break;
                case "speedhackSaveConfig":
                    if (root.TryGetProperty("config", out var configElement))
                    {
                        var afterSave = root.TryGetProperty("afterSave", out var afterSaveElement)
                            ? afterSaveElement.GetString() : null;
                        var saveOverwrite = root.TryGetProperty("overwriteDll", out var saveOverwriteElement) &&
                                            saveOverwriteElement.GetBoolean();
                        HandleSpeedhackSaveConfig(configElement.GetRawText(), afterSave, saveOverwrite);
                    }
                    break;
                case "speedhackResetConfig":
                    HandleSpeedhackResetConfig();
                    break;
                case "speedhackPushConfig":
                    HandleSpeedhackPushConfig();
                    break;
                case "speedhackEditConfig":
                    HandleSpeedhackEditConfig();
                    break;
                case "speedhackOpenGameDir":
                    HandleSpeedhackOpenGameDir();
                    break;
                case "modStatus":
                    _ = PushModStatusAsync();
                    break;
                case "modInstall":
                    var modOverwrite = root.TryGetProperty("overwriteDll", out var modOverwriteElement) &&
                                       modOverwriteElement.GetBoolean();
                    var modAllowDowngrade = root.TryGetProperty("allowDowngrade", out var modDowngradeElement) &&
                                            modDowngradeElement.GetBoolean();
                    var includeSample = root.TryGetProperty("includeSample", out var includeSampleElement) &&
                                        includeSampleElement.GetBoolean();
                    HandleModInstall(modOverwrite, includeSample, modAllowDowngrade);
                    break;
                case "modUninstall":
                    var modForce = root.TryGetProperty("force", out var modForceElement) && modForceElement.GetBoolean();
                    HandleModUninstall(modForce);
                    break;
                case "modDetect":
                    HandleModDetect();
                    break;
                case "modBrowse":
                    HandleModBrowse();
                    break;
                case "modOpenModsFolder":
                    HandleModOpenFolder(ModManager.ModsFolderName);
                    break;
                case "modOpenSdkFolder":
                    HandleModOpenFolder(ModManager.SdkFolderName);
                    break;
                case "modOpenLogsFolder":
                    HandleModOpenFolder(ModManager.LogsFolderName);
                    break;
                case "modImport":
                    if (root.TryGetProperty("path", out var modPathElement) &&
                        modPathElement.GetString() is { Length: > 0 } modPath)
                    {
                        HandleModImport(modPath);
                    }
                    break;
                case "modPickPackageFile":
                    HandleModPickPackageFile();
                    break;
                case "modImportPackage":
                    var importOverwrite = root.TryGetProperty("overwriteDll", out var importOverwriteElement) && importOverwriteElement.GetBoolean();
                    var importDowngrade = root.TryGetProperty("allowDowngrade", out var importDowngradeElement) && importDowngradeElement.GetBoolean();
                    var importPath = root.TryGetProperty("path", out var importPathElement) ? importPathElement.GetString() ?? "" : "";
                    _ = HandleModImportPackageAsync(importPath, importOverwrite, importDowngrade);
                    break;
                case "modPickImport":
                    HandleModPickImport();
                    break;
                case "modDelete":
                    if (root.TryGetProperty("fileName", out var modDeleteElement) &&
                        modDeleteElement.GetString() is { Length: > 0 } modFileName)
                    {
                        HandleModDelete(modFileName);
                    }
                    break;
                case "modToggle":
                    if (root.TryGetProperty("fileName", out var modToggleElement) &&
                        modToggleElement.GetString() is { Length: > 0 } modToggleName &&
                        root.TryGetProperty("enabled", out var modToggleEnabled))
                    {
                        HandleModToggle(modToggleName, modToggleEnabled.GetBoolean());
                    }
                    break;
                case "modOpenConfig":
                    if (root.TryGetProperty("fileName", out var modConfigElement) &&
                        modConfigElement.GetString() is { Length: > 0 } modConfigName)
                    {
                        HandleModOpenConfig(modConfigName);
                    }
                    break;
                case "modOpenConfigRaw":
                    if (root.TryGetProperty("fileName", out var modConfigRawElement) &&
                        modConfigRawElement.GetString() is { Length: > 0 } modConfigRawName)
                    {
                        HandleModOpenConfigRaw(modConfigRawName);
                    }
                    break;
                case "modReadConfigFields":
                    if (root.TryGetProperty("fileName", out var modCfgFieldsElement) &&
                        modCfgFieldsElement.GetString() is { Length: > 0 } modCfgFieldsName)
                    {
                        Post(new { type = "modConfigFields", payload = new { fileName = modCfgFieldsName, fields = _modManager.ReadModConfigFields(RequireModGameDirectory(), modCfgFieldsName) } });
                    }
                    break;
                case "modSaveConfigFields":
                    if (root.TryGetProperty("fileName", out var modCfgSaveElement) &&
                        modCfgSaveElement.GetString() is { Length: > 0 } modCfgSaveName &&
                        root.TryGetProperty("fields", out var modCfgSaveFields) &&
                        modCfgSaveFields.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        try
                        {
                            var fields = modCfgSaveFields.Deserialize<List<ModConfigField>>(WebReadOptions) ?? new();
                            _modManager.SaveModConfigFields(RequireModGameDirectory(), modCfgSaveName, fields);
                            Post(new { type = "toast", message = $"已保存配置：{modCfgSaveName}" });
                        }
                        catch (Exception ex)
                        {
                            Post(new { type = "toast", message = $"保存配置失败：{ex.Message}" });
                        }
                    }
                    break;
                case "modReadLoaderConfig":
                    Post(new { type = "modLoaderConfig", payload = new { config = _modManager.ReadLoaderConfig(RequireModGameDirectory()) } });
                    break;
                case "modSyncSpeedhack":
                    // 进入 mod 页时同步变速栏状态(不弹窗口)
                    try
                    {
                        Post(new { type = "modSpeedhackSync", payload = new { config = _modManager.ReadLoaderConfig(RequireModGameDirectory()) } });
                    }
                    catch
                    {
                        // 未选游戏目录等: 忽略, 变速栏保持默认
                    }
                    break;
                case "modSaveSpeedhack":
                    if (root.TryGetProperty("speedhackBaseSpeed", out var modSpeedhackElement))
                    {
                        try
                        {
                            _modManager.SetSpeedhack(RequireModGameDirectory(), modSpeedhackElement.GetDouble());
                            Post(new { type = "toast", message = modSpeedhackElement.GetDouble() > 1.0
                                ? $"已启用变速：{modSpeedhackElement.GetDouble():F1}x（重启游戏生效）"
                                : "已禁用变速（恢复 1.0 正常速度，重启游戏生效）" });
                        }
                        catch (Exception ex)
                        {
                            Post(new { type = "toast", message = $"设置变速失败：{ex.Message}" });
                        }
                    }
                    break;
                case "modSaveLoaderConfig":
                    if (root.TryGetProperty("config", out var modLoaderCfgElement))
                    {
                        try
                        {
                            var config = modLoaderCfgElement.Deserialize<LoaderConfig>(WebReadOptions) ?? new LoaderConfig();
                            _modManager.SaveLoaderConfig(RequireModGameDirectory(), config);
                            Post(new { type = "toast", message = "已保存加载器设置（重启游戏生效）" });
                            // 加载器设置里的「Steam 绕过」与首页的「绕过 Steam 启动」是同一个配置：改完回推给首页
                            Post(new { type = "steamBypassSync", payload = new { enabled = ModManager.IsSteamBypassActive(config) } });
                        }
                        catch (Exception ex)
                        {
                            Post(new { type = "toast", message = $"保存加载器设置失败：{ex.Message}" });
                        }
                    }
                    break;
                case "modCheckUpdate":
                    _ = HandleModCheckUpdateAsync();
                    break;
                case "modDownloadUpdate":
                    var updateOverwrite = root.TryGetProperty("overwriteDll", out var updateOverwriteElement) &&
                                          updateOverwriteElement.GetBoolean();
                    var updateAllowDowngrade = root.TryGetProperty("allowDowngrade", out var updateDowngradeElement) &&
                                               updateDowngradeElement.GetBoolean();
                    _ = HandleModDownloadUpdateAsync(updateOverwrite, updateAllowDowngrade);
                    break;
                case "skinsGetProfiles":
                    PushSkinProfiles();
                    break;
                case "skinsSetActive":
                    if (root.TryGetProperty("name", out var sActiveElem) && sActiveElem.GetString() is { Length: > 0 } targetSkin)
                    {
                        try
                        {
                            var gDir = RequireModGameDirectory();
                            _skinManager.SetActiveSkin(gDir, targetSkin);
                            Post(new { type = "toast", message = $"已选择卡面方案：{(targetSkin == SkinManager.GameDefault ? "游戏默认" : targetSkin)}，完全退出并重启游戏后生效。" });
                            PushSkinProfiles();
                        }
                        catch (Exception ex)
                        {
                            Post(new { type = "toast", message = $"切换方案失败：{ex.Message}" });
                        }
                    }
                    break;
                case "skinsDownloadDefault":
                    await DownloadDefaultSkinsAsync(root.TryGetProperty("overwrite", out var skinOverwrite) && skinOverwrite.GetBoolean());
                    break;
                case "skinsExportZip":
                    HandleExportSkin(root, false);
                    break;
                case "skinsExportConfirmed":
                    HandleExportSkin(root, true);
                    break;                case "skinsImportZip":
                    HandleSkinImportZip();
                    break;
                case "skinsRestoreDefault":
                    try {
                        _skinManager.RestoreDefaultProfile(RequireModGameDirectory());
                        PushSkinProfiles();
                        Post(new { type = "toast", message = "已从应用内置默认包恢复 default，重启游戏后生效。" });
                    } catch (Exception ex) { Post(new { type = "toast", message = "恢复默认包失败：" + ex.Message }); }
                    break;
                case "skinsDuplicate":
                    HandleSkinDuplicate(root);
                    break;
                case "skinsDelete":
                    if (root.TryGetProperty("name", out var sDelElem) && sDelElem.GetString() is { Length: > 0 } delSkin)
                    {
                        try
                        {
                            var gDir = RequireModGameDirectory();
                            _skinManager.DeleteProfile(gDir, delSkin);
                            Post(new { type = "toast", message = $"已删除方案: {delSkin}" });
                            PushSkinProfiles();
                        }
                        catch (Exception ex)
                        {
                            Post(new { type = "toast", message = $"删除方案失败：{ex.Message}" });
                        }
                    }
                    break;
                case "skinsOpenFolder":
                    try
                    {
                        var sOpenName = root.TryGetProperty("name", out var sOpenElem) ? sOpenElem.GetString() : null;
                        var gDir = RequireModGameDirectory();
                        _skinManager.OpenFolder(gDir, sOpenName);
                    }
                    catch (Exception ex)
                    {
                        Post(new { type = "toast", message = $"打开目录失败：{ex.Message}" });
                    }
                    break;
                case "skinsGetCards":
                    if (root.TryGetProperty("name", out var sQueryElem) && sQueryElem.GetString() is { Length: > 0 } querySkin)
                    {
                        try
                        {
                            var gDir = RequireModGameDirectory();
                            var cards = _skinManager.GetCards(gDir, querySkin);
                            Post(new { type = "skinsCards", payload = new { skinName = querySkin, cards } });
                        }
                        catch (Exception ex)
                        {
                            Post(new { type = "toast", message = $"获取卡面失败：{ex.Message}" });
                        }
                    }
                    break;
                case "skinsCropSave":
                    HandleSaveSkinCrop(root);
                    break;
                case "skinsCropCancel":
                    if (_pendingSkinCrop is { } pending && root.TryGetProperty("token", out var cropToken) && cropToken.GetString() == pending.Token) _pendingSkinCrop = null;
                    break;
                case "skinsPickAndReplaceCard":
                    HandlePickAndReplaceCard(root);
                    break;
                case "skinsRevertCard":
                    HandleRevertCard(root);
                    break;
                case "closeWindow":
                    Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = ex.Message });
        }
    }

    private void TryOpenExternal(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            // 以前这里静默吞掉：点了「用浏览器打开」毫无反应，用户不知道是没点上还是打不开
            Post(new { type = "toast", message = $"打不开系统浏览器：{ex.Message}" });
        }
    }

}
