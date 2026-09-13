using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using AstralParty.Toys.Services;

namespace AstralParty.Toys;

public partial class HybridWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _appDirectory = AppContext.BaseDirectory;

    /// <summary>
    /// 是否允许"开发回退"（exe 同目录放了 WebUI\ 时优先走磁盘页面）：只有 Debug 构建允许。
    /// Release 永远用内嵌资源——否则用户把新版 exe 覆盖到旧安装目录时，会静默地继续用那份旧页面。
    /// 改前端想免重编译，请用 Debug 构建（VS 默认/F5）。
    /// </summary>
    private static readonly bool AllowDiskWebRoot =
#if DEBUG
        true;
#else
        false;
#endif

    private readonly string _assetDirectory;
    private readonly IReadOnlyList<string> _materialDirectories;
    private readonly HomeDataService _homeDataService;
    private readonly SpeedhackManager _speedhackManager;
    private readonly ModManager _modManager;
    private readonly ReplayLibraryService _libraryService;
    private ReplayAnalyzer? _analyzer;
    private ReplayReport? _report;
    private bool _webReady;
    private bool _webRootIsEmbedded;
    private readonly Dictionary<string, string> _replayLibrary = new(StringComparer.Ordinal);

    public HybridWindow()
    {
        InitializeComponent();
        _assetDirectory = Path.Combine(_appDirectory, "Assets");
        _materialDirectories = MaterialSource.DiscoverAll(_appDirectory);
        _homeDataService = new HomeDataService(_appDirectory);
        _speedhackManager = new SpeedhackManager(_appDirectory);
        _modManager = new ModManager(_appDirectory);
        _libraryService = new ReplayLibraryService(_appDirectory);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var webRoot = Path.Combine(_appDirectory, EmbeddedWebStore.DirectoryName);
            // 开发回退：Debug 构建下 exe 同目录手动放一份 WebUI\ 就优先走磁盘（改完刷新即可见），
            // 否则用内嵌资源。Release 永远用内嵌资源，见 AllowDiskWebRoot 的说明。
            var useDiskWebRoot = AllowDiskWebRoot && File.Exists(Path.Combine(webRoot, "index.html"));
            if (!useDiskWebRoot && !EmbeddedWebStore.HasIndex)
                throw new FileNotFoundException(
                    "找不到 WebUI/index.html（内嵌资源里也没有）。", Path.Combine(webRoot, "index.html"));

            StartupDetail.Text = "初始化本地界面";
            var userDataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AstralParty.Toys", "WebView2");
            Directory.CreateDirectory(userDataDirectory);
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
            await WebView.EnsureCoreWebView2Async(environment);
            if (useDiskWebRoot)
            {
                // 注意：被 SetVirtualHostNameToFolderMapping 映射过的域名不会触发 WebResourceRequested，
                // 所以磁盘映射与内嵌拦截这两条路是互斥的，按上面判定二选一。
                WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.astral.local", webRoot, CoreWebView2HostResourceAccessKind.DenyCors);
            }
            else
            {
                _webRootIsEmbedded = true;
                WebView.CoreWebView2.AddWebResourceRequestedFilter(
                    "https://app.astral.local/*", CoreWebView2WebResourceContext.All);
            }

            // 内嵌素材（PackedAssets）始终走拦截。
            // 关键：WebResourceRequested 是**单一事件**，AddWebResourceRequestedFilter 只决定"哪些请求会触发"；
            // 请求一旦触发，所有订阅者都会被调用，且共用同一个 Response 槽。所以页面和素材只能由同一个
            // 处理器按 host 分派——分成两个处理器的话，后一个会把前一个的响应覆盖成 404（实测：主文档被覆盖成 404，
            // 导航直接失败并落到 Chromium 错误页）。
            WebView.CoreWebView2.AddWebResourceRequestedFilter(
                "https://assets.astral.local/*", CoreWebView2WebResourceContext.Image);
            WebView.CoreWebView2.WebResourceRequested += EmbeddedRequested;
            for (var index = 0; index < _materialDirectories.Count; index++)
                WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    $"materials{index}.astral.local", _materialDirectories[index], CoreWebView2HostResourceAccessKind.Allow);
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.WebMessageReceived += WebMessageReceived;
            WebView.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                TryOpenExternal(args.Uri);
            };
            WebView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (!args.Uri.StartsWith("https://app.astral.local/", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                    TryOpenExternal(args.Uri);
                }
            };
            WebView.Source = new Uri("https://app.astral.local/index.html");
        }
        catch (Exception ex)
        {
            StartupDetail.Text = $"主界面启动失败：{ex.Message}";
            ClassicButton.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// 内嵌资源的唯一入口。必须保持"只有一个处理器"——见 <see cref="InitializeWebViewAsync"/> 里的说明：
    /// 多个 filter 共用一个事件，处理器要自己按 host 判断该不该接管，否则会互相覆盖响应。
    /// </summary>
    private void EmbeddedRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = new Uri(e.Request.Uri);
        if (_webRootIsEmbedded && uri.Host.Equals("app.astral.local", StringComparison.OrdinalIgnoreCase))
            e.Response = CreatePageResponse(uri);
        else if (uri.Host.Equals("assets.astral.local", StringComparison.OrdinalIgnoreCase))
            e.Response = CreateAssetResponse(uri);
    }

    /// <summary>提供页面本体（Document、CSS、JS、图片、fetch 的 JSON）——全部来自程序集资源。</summary>
    private CoreWebView2WebResourceResponse CreatePageResponse(Uri uri)
    {
        var relativePath = uri.AbsolutePath.TrimStart('/');
        var stream = EmbeddedWebStore.Open(relativePath);
        return stream is null
            ? WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                null, 404, "Not Found", "Content-Type: text/plain")
            : WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                new WebResourceStream(stream), 200, "OK",
                $"Content-Type: {EmbeddedWebStore.ContentType(relativePath)}\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *");
    }

    /// <summary>提供内嵌素材（PackedAssets）。</summary>
    private CoreWebView2WebResourceResponse CreateAssetResponse(Uri uri)
    {
        var stream = EmbeddedAssetStore.OpenWebPath(uri.AbsolutePath);
        var contentType = Path.GetExtension(uri.AbsolutePath).Equals(".png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/webp";
        return stream is null
            ? WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                Stream.Null, 404, "Not Found", "Content-Type: text/plain")
            : WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                new WebResourceStream(stream), 200, "OK",
                $"Content-Type: {contentType}\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *");
    }

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
                    StartupOverlay.Visibility = Visibility.Collapsed;
                    Post(new { type = "homeData", payload = _homeDataService.GetHomeData() });
                    await SendReplayLibraryAsync(runMaintain: true);
                    Post(new { type = "librarySettings", payload = BuildLibrarySettingsPayload() });
                    var args = Environment.GetCommandLineArgs();
                    if (args.Length > 1 && File.Exists(args[1])) await LoadReplayAsync(args[1]);
                    break;
                case "getHomeData":
                    Post(new { type = "homeData", payload = _homeDataService.GetHomeData() });
                    break;
                case "openReplay":
                    await PickAndLoadReplayAsync();
                    break;
                case "openRecentReplay":
                    if (root.TryGetProperty("token", out var tokenElement) &&
                        tokenElement.GetString() is { Length: > 0 } token &&
                        _replayLibrary.TryGetValue(token, out var replayPath) && File.Exists(replayPath))
                        await LoadReplayAsync(replayPath);
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
                case "openBrowser":
                    if (root.TryGetProperty("url", out var urlElement) &&
                        urlElement.GetString() is { Length: > 0 } targetUrl)
                    {
                        TryOpenExternal(targetUrl);
                    }
                    break;
                case "launchGame":
                    HandleLaunchGame();
                    break;
                case "speedhackStatus":
                    PushSpeedhackStatus();
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
                    PushModStatus();
                    break;
                case "modInstall":
                    var modOverwrite = root.TryGetProperty("overwriteDll", out var modOverwriteElement) &&
                                       modOverwriteElement.GetBoolean();
                    var includeSample = root.TryGetProperty("includeSample", out var includeSampleElement) &&
                                        includeSampleElement.GetBoolean();
                    HandleModInstall(modOverwrite, includeSample);
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
                            var fields = modCfgSaveFields.Deserialize<List<ModConfigField>>() ?? new();
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
                case "modSaveLoaderConfig":
                    if (root.TryGetProperty("config", out var modLoaderCfgElement))
                    {
                        try
                        {
                            var config = modLoaderCfgElement.Deserialize<LoaderConfig>() ?? new LoaderConfig();
                            _modManager.SaveLoaderConfig(RequireModGameDirectory(), config);
                            Post(new { type = "toast", message = "已保存加载器设置（重启游戏生效）" });
                        }
                        catch (Exception ex)
                        {
                            Post(new { type = "toast", message = $"保存加载器设置失败：{ex.Message}" });
                        }
                    }
                    break;
                case "modReadPermissions":
                    if (root.TryGetProperty("fileName", out var modPermElement) &&
                        modPermElement.GetString() is { Length: > 0 } modPermName)
                    {
                        var dir = RequireModGameDirectory();
                        Post(new
                        {
                            type = "modPermissions",
                            payload = new
                            {
                                fileName = modPermName,
                                declared = _modManager.GetStatus().Mods.FirstOrDefault(m => m.FileName == modPermName)?.Permissions ?? 0,
                                overridden = _modManager.ReadPermissionsOverride(dir, modPermName)
                            }
                        });
                    }
                    break;
                case "modSavePermissions":
                    if (root.TryGetProperty("fileName", out var modPermSaveElement) &&
                        modPermSaveElement.GetString() is { Length: > 0 } modPermSaveName &&
                        root.TryGetProperty("granted", out var modPermGranted) &&
                        root.TryGetProperty("denied", out var modPermDenied))
                    {
                        try
                        {
                            _modManager.SavePermissionsOverride(
                                RequireModGameDirectory(), modPermSaveName,
                                modPermGranted.GetInt32(), modPermDenied.GetInt32());
                            Post(new { type = "toast", message = $"已保存权限设置：{modPermSaveName}（重启游戏生效）" });
                        }
                        catch (Exception ex)
                        {
                            Post(new { type = "toast", message = $"保存权限失败：{ex.Message}" });
                        }
                    }
                    break;
                case "modCheckUpdate":
                    _ = HandleModCheckUpdateAsync();
                    break;
                case "modDownloadUpdate":
                    var updateOverwrite = root.TryGetProperty("overwriteDll", out var updateOverwriteElement) &&
                                          updateOverwriteElement.GetBoolean();
                    _ = HandleModDownloadUpdateAsync(updateOverwrite);
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

    private static void TryOpenExternal(string? url)
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
        catch { }
    }

    // ============================== 回放库 ==============================

    /// <summary>重新扫描游戏回放目录与回放库；runMaintain 为真时先跑一次自动托管。</summary>
    private async Task SendReplayLibraryAsync(bool runMaintain)
    {
        LibrarySnapshot snapshot;
        string? maintainMessage = null;
        try
        {
            snapshot = await Task.Run(() =>
            {
                if (runMaintain && _libraryService.Settings.AutoMaintain && !_libraryService.IsSameFolder())
                {
                    var maintained = _libraryService.Maintain();
                    if (maintained.Applied > 0) maintainMessage = maintained.Messages[0];
                }
                return _libraryService.Snapshot();
            });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = $"回放列表读取失败：{ex.Message}" });
            return;
        }

        Post(new { type = "replayLibrary", payload = BuildLibraryPayload(snapshot) });
        if (maintainMessage is not null) Post(new { type = "toast", message = maintainMessage });
    }

    private object BuildLibraryPayload(LibrarySnapshot snapshot)
    {
        _replayLibrary.Clear();
        var entries = new List<object>(snapshot.Entries.Count);
        for (var index = 0; index < snapshot.Entries.Count; index++)
        {
            var entry = snapshot.Entries[index];
            var token = $"replay-{index}";
            _replayLibrary[token] = entry.LibraryPath ?? entry.GamePath ?? "";
            var meta = entry.Meta;
            entries.Add(new
            {
                token,
                replayId = entry.ReplayId,
                inGame = entry.InGame,
                inLibrary = entry.InLibrary,
                healthy = meta?.Healthy ?? false,
                error = meta?.Error ?? "",
                sizeText = FormatLibrarySize(entry.SizeBytes),
                sizeBytes = entry.SizeBytes,
                modifiedText = entry.ModifiedUtc == DateTime.UnixEpoch
                    ? "—" : entry.ModifiedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                sortTime = entry.SortTime,
                finishText = meta is { FinishTime: > 0 } ? FormatUnixTime(meta.FinishTime) : "—",
                durationText = meta is { DurationSeconds: > 0 } m2
                    ? TimeSpan.FromSeconds(m2.DurationSeconds).ToString(@"hh\:mm\:ss") : "—",
                mapName = meta?.MapName is { Length: > 0 } mapName ? mapName : "未知地图",
                mapId = meta?.MapId ?? 0,
                roundCount = meta?.RoundCount ?? 0,
                frameCount = meta?.FrameCount ?? 0,
                gameVersion = meta?.GameVersion is { Length: > 0 } version ? version : "—",
                winnerText = meta is { WinnerName.Length: > 0 } ? meta.WinnerName
                    : meta is { WinnerId: > 0 } ? $"玩家 {meta.WinnerId}" : "—",
                playersText = meta is null || meta.Players.Count == 0
                    ? "—"
                    : string.Join(" · ", meta.Players.Select(p => p.Nick.Length > 0 ? p.Nick : $"玩家 {p.Id}")),
                heroesText = meta is null || meta.Players.Count == 0
                    ? "—"
                    : string.Join(" · ", meta.Players.Select(p => p.HeroName))
            });
        }

        return new
        {
            directory = snapshot.GameDirectory,
            available = snapshot.GameDirectoryAvailable,
            libraryRoot = snapshot.LibraryRoot,
            libraryAvailable = snapshot.LibraryAvailable,
            libraryCount = snapshot.LibraryCount,
            librarySizeText = FormatLibrarySize(snapshot.LibraryBytes),
            gameCount = snapshot.GameCount,
            capacity = snapshot.GameSlotCapacity,
            keepInGame = snapshot.KeepInGame,
            autoMaintain = snapshot.AutoMaintain,
            gameFull = snapshot.GameFull,
            gameOverKeep = snapshot.GameOverKeep,
            gameRunning = snapshot.GameRunning,
            sameFolder = snapshot.SameFolder,
            warnings = snapshot.Warnings,
            entries
        };
    }

    private static List<string> ReadIds(JsonElement root)
    {
        var ids = new List<string>();
        if (root.TryGetProperty("ids", out var element) && element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } id) ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>归档游戏目录里最旧的 count 局（保留名额以内的部分不动）。</summary>
    private ReplayOperationResult ArchiveOldest(int count)
    {
        var snapshot = _libraryService.Snapshot();
        var gameEntries = snapshot.Entries.Where(x => x.InGame).ToList();
        var excess = gameEntries.Count - ReplayLibraryService.GameSlotCapacity;
        var take = Math.Max(excess, 0) > 0 ? Math.Max(excess, 0) : Math.Min(count, gameEntries.Count);
        var targets = gameEntries.Skip(Math.Max(gameEntries.Count - take, 0)).Select(x => x.ReplayId).ToList();
        if (targets.Count == 0) return ReplayOperationResult.Fail("游戏目录里没有可归档的回放。");

        var result = _libraryService.Archive(targets);
        result.Messages.Insert(0, $"已归档 {result.Applied} 局最旧的回放，游戏内席位腾出 {result.Applied} 个。");
        return result;
    }

    private async Task RunLibraryOperationAsync(Func<ReplayOperationResult> operation)
    {
        ReplayOperationResult result;
        try
        {
            result = await Task.Run(operation);
        }
        catch (Exception ex)
        {
            result = ReplayOperationResult.Fail($"操作失败：{ex.Message}");
        }

        Post(new
        {
            type = "libraryResult",
            payload = new
            {
                ok = result.Ok,
                applied = result.Applied,
                skipped = result.Skipped,
                failed = result.Failed,
                needsConfirmation = result.NeedsConfirmation,
                pendingIds = result.PendingIds,
                summary = result.Messages.Count > 0 ? result.Messages[0] : (result.Ok ? "操作完成。" : "操作失败。"),
                messages = result.Messages
            }
        });
        await SendReplayLibraryAsync(runMaintain: false);
    }

    private async Task HandleLibraryImportAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要导入回放库的回放文件",
            Filter = "回放文件|*|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        var path = dialog.FileName;
        await RunLibraryOperationAsync(() => _libraryService.Import(path));
    }

    private void HandleLibraryOpenFolder()
    {
        try
        {
            _libraryService.EnsureLibrary();
            SpeedhackManager.OpenInExplorer(_libraryService.LibraryRoot);
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"打开库目录失败：{ex.Message}" });
        }
    }

    private void HandleLibraryBrowseRoot()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择回放库目录（不要和游戏的 Temp\\Replay 选成同一个）",
            InitialDirectory = Directory.Exists(_libraryService.LibraryRoot) ? _libraryService.LibraryRoot : null
        };
        if (dialog.ShowDialog(this) != true || dialog.FolderName is not { Length: > 0 } folder) return;
        _libraryService.SaveSettings(new ReplayLibrarySettings
        {
            LibraryRoot = folder,
            AutoMaintain = _libraryService.Settings.AutoMaintain,
            KeepInGame = _libraryService.Settings.KeepInGame
        });
        Post(new { type = "toast", message = $"回放库已切换到：{folder}" });
        Post(new { type = "librarySettings", payload = BuildLibrarySettingsPayload() });
    }

    private void HandleLibrarySaveSettings(JsonElement root)
    {
        var root_ = root.TryGetProperty("libraryRoot", out var rootElement) ? rootElement.GetString() : null;
        var autoMaintain = root.TryGetProperty("autoMaintain", out var autoElement) && autoElement.GetBoolean();
        var keepInGame = root.TryGetProperty("keepInGame", out var keepElement) && keepElement.TryGetInt32(out var parsedKeep)
            ? parsedKeep
            : ReplayLibraryService.GameSlotCapacity;

        _libraryService.SaveSettings(new ReplayLibrarySettings
        {
            LibraryRoot = string.IsNullOrWhiteSpace(root_) ? null : root_,
            AutoMaintain = autoMaintain,
            KeepInGame = Math.Clamp(keepInGame, 1, ReplayLibraryService.GameSlotCapacity)
        });
        Post(new { type = "librarySettings", payload = BuildLibrarySettingsPayload() });
        Post(new { type = "toast", message = "回放库设置已保存。" });
    }

    private object BuildLibrarySettingsPayload() => new
    {
        libraryRoot = _libraryService.LibraryRoot,
        defaultLibraryRoot = ReplayLibraryService.DefaultLibraryRoot(),
        autoMaintain = _libraryService.Settings.AutoMaintain,
        keepInGame = _libraryService.Settings.KeepInGame,
        capacity = ReplayLibraryService.GameSlotCapacity
    };

    // ============================== 启动游戏 ==============================

    /// <summary>Steam AppID 2622000（Astral Party / 吉星派对）。</summary>
    private const string SteamLaunchUrl = "steam://rungameid/2622000";

    /// <summary>
    /// 优先交给 Steam 启动（能带上 Steam 的 DRM / 云存档 / 更新检查）。
    /// Steam 没装或 steam:// 协议没注册时，退回直接启动探测到的游戏主程序。
    /// </summary>
    private void HandleLaunchGame()
    {
        if (SpeedhackManager.IsGameRunning())
        {
            Post(new { type = "toast", message = "游戏已经在运行了。" });
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(SteamLaunchUrl) { UseShellExecute = true });
            Post(new { type = "toast", message = "已交给 Steam 启动 Astral Party，稍等一下。" });
            return;
        }
        catch (Exception steamError)
        {
            var gameDirectory = _speedhackManager.ResolveGameDirectory();
            var gameExe = string.IsNullOrEmpty(gameDirectory) ? null : SpeedhackManager.ContainsGameExe(gameDirectory);
            if (gameExe is null)
            {
                Post(new
                {
                    type = "toast",
                    message = $"没能启动游戏：{steamError.Message}。请确认已安装 Steam；也可以在「变速器」页手动选择游戏目录后再试。"
                });
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(gameExe)
                {
                    UseShellExecute = true,
                    WorkingDirectory = gameDirectory!
                });
                Post(new { type = "toast", message = "Steam 不可用，已直接启动游戏主程序。" });
            }
            catch (Exception ex)
            {
                Post(new { type = "toast", message = $"启动游戏失败：{ex.Message}" });
            }
        }
    }

    private static string FormatUnixTime(long seconds)
        => DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    private static string FormatLibrarySize(long length) => length switch
    {
        >= 1024 * 1024 => $"{length / 1024d / 1024d:0.00} MB",
        >= 1024 => $"{length / 1024d:0.0} KB",
        _ => $"{length} B"
    };

    private async Task PickAndLoadReplayAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择吉星派对回放文件",
            Filter = "回放文件|*|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true) await LoadReplayAsync(dialog.FileName);
    }

    private async Task LoadReplayAsync(string path)
    {
        Post(new { type = "loading", active = true, message = "正在准备协议和配置…" });
        try
        {
            var progress = new Progress<string>(message => Post(new { type = "loading", active = true, message }));
            _report = await Task.Run(() =>
            {
                _analyzer ??= new ReplayAnalyzer(_appDirectory, _assetDirectory, _materialDirectories);
                return _analyzer.Analyze(path, progress);
            });
            Post(new { type = "report", payload = BuildClientReport(_report) });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = ex.Message });
        }
        finally
        {
            Post(new { type = "loading", active = false, message = "" });
        }
    }

    private async Task SendFrameDetailAsync(int index)
    {
        if (_report is null || _analyzer is null || index < 0 || index >= _report.Frames.Count) return;
        var frame = _report.Frames[index];
        var detail = await Task.Run(() => _analyzer.FormatFrame(frame));
        Post(new
        {
            type = "frameDetail",
            payload = new { frame.Index, frame.OffsetText, frame.CmdId, frame.MessageName, frame.PayloadLength, detail }
        });
    }

    private async Task ExportRelicsAsync(string? format)
    {
        if (_report is null) return;

        var baseName = Path.GetFileNameWithoutExtension(_report.FileName);
        var isTxt = string.Equals(format, "txt", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(format, "text", StringComparison.OrdinalIgnoreCase);
        var isCsv = !isTxt && string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase);

        var dialog = new SaveFileDialog
        {
            Title = isTxt 
                ? "导出筹码时间线 (自然语言 AI 文本)" 
                : isCsv 
                    ? "导出筹码时间线表格 (CSV)" 
                    : "导出筹码时间线数据 (JSON)",
            Filter = isTxt
                ? "AI 分析文本 (*.txt)|*.txt|所有文件 (*.*)|*.*"
                : isCsv 
                    ? "CSV 逗号分隔表格 (*.csv)|*.csv|所有文件 (*.*)|*.*" 
                    : "JSON 数据文件 (*.json)|*.json|所有文件 (*.*)|*.*",
            FileName = isTxt
                ? $"{baseName}.relics.ai.txt"
                : isCsv 
                    ? $"{baseName}.relics.csv" 
                    : $"{baseName}.relics.json"
        };

        if (dialog.ShowDialog(this) != true) return;

        Post(new { type = "loading", active = true, message = "正在导出筹码时间线…" });
        try
        {
            var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            if (ext == ".txt" || isTxt)
            {
                var text = await Task.Run(() => BuildRelicsAiSummary(_report));
                await File.WriteAllTextAsync(dialog.FileName, text, Encoding.UTF8);
            }
            else if (ext == ".csv" || isCsv)
            {
                var sb = new StringBuilder();
                // 写入 UTF-8 BOM 避免 Excel 打开中文乱码
                sb.AppendLine("帧号,回合,玩家ID,玩家名称,玩家角色,操作类型,档位,筹码ID,筹码名称,品质,来源,刷新状态,候选选项");
                foreach (var r in _report.Relics)
                {
                    string EscapeCsv(string val) => $"\"{val.Replace("\"", "\"\"")}\"";
                    sb.AppendLine(string.Join(",",
                        r.FrameIndex,
                        r.Round,
                        r.PlayerId,
                        EscapeCsv(r.PlayerName),
                        EscapeCsv(r.HeroName),
                        EscapeCsv(r.Kind),
                        EscapeCsv(r.LevelText),
                        r.RelicId,
                        EscapeCsv(r.RelicName),
                        EscapeCsv(r.Quality),
                        EscapeCsv(r.Source),
                        EscapeCsv(r.RefreshText),
                        EscapeCsv(r.OptionsText)
                    ));
                }
                var utf8Bom = new UTF8Encoding(true);
                await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), utf8Bom);
            }
            else
            {
                var payload = new
                {
                    replayFile = _report.FileName,
                    totalRelicEvents = _report.Relics.Count,
                    relics = _report.Relics.Select(r => new
                    {
                        frameIndex = r.FrameIndex,
                        round = r.Round,
                        playerId = r.PlayerId,
                        playerName = r.PlayerName,
                        heroName = r.HeroName,
                        actionKind = r.Kind,
                        level = r.Level,
                        relicId = r.RelicId,
                        relicName = r.RelicName,
                        quality = r.Quality,
                        source = r.Source,
                        refreshCount = r.RefreshNumber,
                        isRefresh = r.IsRefresh,
                        refreshText = r.RefreshText,
                        optionsText = r.OptionsText
                    })
                };
                var json = await Task.Run(() => JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));
                await File.WriteAllTextAsync(dialog.FileName, json, Encoding.UTF8);
            }

            Post(new { type = "toast", message = $"已成功导出筹码记录到 {Path.GetFileName(dialog.FileName)}" });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = $"导出失败: {ex.Message}" });
        }
        finally
        {
            Post(new { type = "loading", active = false, message = "" });
        }
    }

    private async Task ExportShopsAsync(string format)
    {
        if (_report is null) return;

        var baseName = Path.GetFileNameWithoutExtension(_report.FileName);
        var dialog = new SaveFileDialog
        {
            Title = "导出商店时间线表格 (CSV)",
            Filter = "CSV 逗号分隔表格 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            FileName = $"{baseName}.shops.csv"
        };
        if (dialog.ShowDialog(this) != true) return;

        Post(new { type = "loading", active = true, message = "正在导出商店时间线…" });
        try
        {
            var sb = new StringBuilder();
            // UTF-8 BOM 避免 Excel 打开中文乱码
            sb.AppendLine("帧号,回合,玩家ID,玩家名称,玩家角色,商店类型,候选卡牌,售价,折扣,免费卡,购买结果,已售出槽位,是否关闭");
            foreach (var s in _report.Shops)
            {
                string EscapeCsv(string val) => $"\"{val.Replace("\"", "\"\"")}\"";
                sb.AppendLine(string.Join(",",
                    s.FrameIndex,
                    s.Round,
                    s.PlayerId,
                    EscapeCsv(s.PlayerName),
                    EscapeCsv(s.HeroName),
                    EscapeCsv(s.ShopType),
                    EscapeCsv(s.OptionsText),
                    s.Price,
                    s.Discount,
                    s.FreeCard > 0 ? $"{s.FreeCard} x{s.FreeCardNum}" : "",
                    EscapeCsv(s.PurchaseText),
                    EscapeCsv(s.SoldOutText),
                    s.IsClosed
                ));
            }
            var utf8Bom = new UTF8Encoding(true);
            await File.WriteAllTextAsync(dialog.FileName, sb.ToString(), utf8Bom);
            Post(new { type = "toast", message = $"已成功导出商店记录到 {Path.GetFileName(dialog.FileName)}" });
        }
        catch (Exception ex)
        {
            Post(new { type = "error", message = $"导出失败: {ex.Message}" });
        }
        finally
        {
            Post(new { type = "loading", active = false, message = "" });
        }
    }

    public static string BuildRelicsAiSummary(ReplayReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 《吉星派对》(Astral Party) 对局筹码流转与战术决策复盘数据");
        sb.AppendLine($"# 生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("# 说明: 本文档专为大语言模型 (AI) 对局复盘设计，包含对局基础背景、参战玩家配置、全局筹码统计以及按回合推进的筹码候选与决策全量时间线。");
        sb.AppendLine();
        sb.AppendLine("================================================================================");
        sb.AppendLine("一、对局背景基本信息");
        sb.AppendLine("================================================================================");
        sb.AppendLine($"- 回放文件: {report.FileName}");
        sb.AppendLine($"- 游戏版本: {report.GameVersion}");
        sb.AppendLine($"- 对局地图: {report.MapName} (地图ID: {report.MapId})");
        sb.AppendLine($"- 模式/难度: {report.MapType} / {report.Difficulty}");
        sb.AppendLine($"- 首领(BOSS): {report.BossName}");
        sb.AppendLine($"- 对局进程: {report.ProgressText}");
        sb.AppendLine($"- 对局结果: {report.ResultText} (最终决胜: {report.WinnerText})");
        sb.AppendLine($"- 回合统计: 共 {report.RoundCount} 回合 (包含 {report.TurnCount} 个行动轮次, 帧数 {report.FrameCount})");
        sb.AppendLine($"- 对局时间: {report.StartTimeText} 至 {report.FinishTimeText} (时长: {report.DurationText})");
        sb.AppendLine($"- 玩家阵亡次数: {report.PlayerDeaths} 次");
        sb.AppendLine();

        sb.AppendLine("================================================================================");
        sb.AppendLine("二、参战玩家与角色战绩概况");
        sb.AppendLine("================================================================================");
        foreach (var p in report.Players)
        {
            sb.AppendLine($"【玩家】{p.Nickname} (ID: {p.Id})");
            sb.AppendLine($"  - 参战角色: {p.HeroName} (英雄ID: {p.HeroId} · 账号等级: {p.AccountLevel})");
            sb.AppendLine($"  - 最终状态: {p.FinalStatus}" + (p.FinalBossKill ? " [★ 最终首领击杀者/MVP]" : ""));
            sb.AppendLine($"  - 生命值: {p.HpText} | 拥有星币: {p.Gold} | 攻防属性: 攻击 {p.Attack} / 防御 {p.Defense}");
            sb.AppendLine($"  - 输出伤害: {p.Damage:N0} | 承受伤害: {p.Injured:N0} | 击杀数: {p.Kills} | 治疗量: {p.Healing}");
            sb.AppendLine($"  - 棋盘移动: {p.MovePoints} 点 | 卡牌使用: {p.UsedCards} 张 | 技能释放: {p.UsedSkills} 次");
            sb.AppendLine($"  - 筹码地块购买次数: {p.BoughtRelics} 次 | 最终持有筹码数: {p.SelectedRelicCount} 个");
            sb.AppendLine($"  - 最终持有筹码清单: {(string.IsNullOrWhiteSpace(p.SelectedRelicsText) ? "无" : p.SelectedRelicsText)}");
            sb.AppendLine();
        }

        sb.AppendLine("================================================================================");
        sb.AppendLine("三、全局筹码统计摘要");
        sb.AppendLine("================================================================================");
        var picks = report.Relics.Where(r => r.Kind == "选择").ToList();
        var candidates = report.Relics.Where(r => r.Kind != "选择").ToList();
        var refreshes = report.Relics.Count(r => r.IsRefresh);
        sb.AppendLine($"- 筹码流转记录总数: {report.Relics.Count} 条 (最终选定 {picks.Count} 次，出现候选 {candidates.Count} 次)");
        sb.AppendLine($"- 玩家刷新候选次数: {refreshes} 次");

        var sourceGroups = picks.GroupBy(r => r.Source).OrderByDescending(g => g.Count());
        sb.AppendLine($"- 选定筹码来源分布: " + string.Join("，", sourceGroups.Select(g => $"{g.Key} {g.Count()} 次")));

        var qualityGroups = picks.Where(r => r.RelicId > 0).GroupBy(r => r.Quality).OrderByDescending(g => g.Count());
        sb.AppendLine($"- 选定筹码品质分布: " + string.Join("，", qualityGroups.Select(g => $"{g.Key} {g.Count()} 个")));
        sb.AppendLine();

        sb.AppendLine("================================================================================");
        sb.AppendLine("四、按回合详细筹码流转与决策时间线 (Chronological Relic Timeline)");
        sb.AppendLine("================================================================================");
        var roundGroups = report.Relics.GroupBy(r => r.Round > 0 ? r.Round : 0).OrderBy(g => g.Key);
        foreach (var group in roundGroups)
        {
            var roundName = group.Key > 0 ? $"第 {group.Key} 回合" : "全局 / 开局阶段";
            sb.AppendLine($"--------------------------------------------------------------------------------");
            sb.AppendLine($"【{roundName}】(共 {group.Count()} 条流转事件)");
            sb.AppendLine($"--------------------------------------------------------------------------------");

            var index = 1;
            foreach (var r in group)
            {
                if (r.Kind == "选择")
                {
                    sb.AppendLine($"  [事件 {index++} | 帧 {r.FrameIndex}] 玩家「{r.PlayerName}」（角色：{r.HeroName}）");
                    sb.AppendLine($"    - 动作类型: 【最终选定筹码】");
                    sb.AppendLine($"    - 获得筹码: 【{r.RelicName}】 (品质: {r.Quality} · ID: {r.RelicId} · {r.LevelText})");
                    sb.AppendLine($"    - 筹码来源: {r.Source}");
                    sb.AppendLine($"    - 刷新情况: {(r.RefreshNumber > 0 ? $"此前累计刷新了 {r.RefreshNumber} 次后才确认选择" : "未刷新，直接从初始候选选择")}");
                }
                else if (r.IsRefresh)
                {
                    sb.AppendLine($"  [事件 {index++} | 帧 {r.FrameIndex}] 玩家「{r.PlayerName}」（角色：{r.HeroName}）");
                    sb.AppendLine($"    - 动作类型: 【刷新候选】(第 {Math.Max(1, r.RefreshNumber)} 次刷新)");
                    sb.AppendLine($"    - 来源途径: {r.Source} ({r.LevelText})");
                    sb.AppendLine($"    - 刷新后呈现的新候选池: {r.OptionsText}");
                }
                else
                {
                    sb.AppendLine($"  [事件 {index++} | 帧 {r.FrameIndex}] 玩家「{r.PlayerName}」（角色：{r.HeroName}）");
                    sb.AppendLine($"    - 动作类型: 【首次呈现候选】");
                    sb.AppendLine($"    - 来源途径: {r.Source} ({r.LevelText})");
                    sb.AppendLine($"    - 初始候选池: {r.OptionsText}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("================================================================================");
        sb.AppendLine("五、供 AI 智能分析与复盘的建议 Prompt 方向");
        sb.AppendLine("================================================================================");
        sb.AppendLine("你可以直接复制以下 Prompt 向大语言模型提问：");
        sb.AppendLine("1. 【流派与契合度评估】请结合每位玩家的角色技能与定位，分析他们选取的筹码是否形成了有效联动（Synergy）？有没有错失更优质的候选筹码？");
        sb.AppendLine("2. 【刷新时机与策略分析】请评价各位玩家在各回合中的刷新决策：是否存在盲目刷新浪费机会，或是在关键轮次赌出了核心关键筹码？");
        sb.AppendLine($"3. 【关键胜负手复盘】根据获胜者（{report.WinnerText}）与其他玩家的数据，分析获胜者主要是依靠哪几个核心筹码建立优势并最终斩获胜利的？");
        sb.AppendLine("4. 【改进建议】如果给表现落后或伤害不足的玩家提供改进策略，在筹码获取和资源配置上应该做出哪些针对性调整？");
        sb.AppendLine();
        return sb.ToString();
    }

    private async Task ExportAsync()
    {
        if (_report is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "导出回放完整数据",
            Filter = "JSON 数据文件 (*.json)|*.json|CSV 筹码表格 (*.csv)|*.csv|所有文件 (*.*)|*.*",
            FileName = $"{Path.GetFileNameWithoutExtension(_report.FileName)}.report.json"
        };
        if (dialog.ShowDialog(this) != true) return;

        if (dialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            await ExportRelicsAsync("csv");
            return;
        }

        Post(new { type = "loading", active = true, message = "正在导出完整协议数据…" });
        try
        {
            var json = await Task.Run(() => JsonSerializer.Serialize(BuildExportReport(_report), new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));
            await File.WriteAllTextAsync(dialog.FileName, json, Encoding.UTF8);
            Post(new { type = "toast", message = $"已导出到 {dialog.FileName}" });
        }
        finally
        {
            Post(new { type = "loading", active = false, message = "" });
        }
    }

    private object BuildClientReport(ReplayReport report) => new
    {
        file = new { report.FileName, report.FilePath, report.FileSizeText, report.Sha256 },
        assets = report.UiAssets.ToDictionary(pair => pair.Key, pair => AssetUrl(pair.Value)),
        match = new
        {
            report.ReplayId, report.GameVersion, report.RoomId, report.RoomServerId,
            report.MapId, report.MapName, mapImage = AssetUrl(report.MapImagePath),
            report.MapType, report.Difficulty, report.RoundCount, report.TurnCount,
            report.FrameCount, report.CommandTypeCount, report.PlayerDeaths,
            report.GameProgress, report.GameMaxProgress, report.ProgressText,
            report.StartTimeText, report.FinishTimeText, report.DurationText,
            report.ResultText, report.WinnerText, report.BossName, report.AwardsText,
            report.WarningCount
        },
        players = report.Players.Select(x => new
        {
            x.Id, x.Nickname, x.Slot, x.AccountLevel, x.HeroId, x.HeroName,
            avatar = AssetUrl(x.AvatarPath), x.FinalStatus, x.Hp, x.MaxHp, x.HpText,
            x.Gold, x.Attack, x.Defense, x.Damage, x.Injured, x.Kills, x.Healing,
            x.MovePoints, x.UsedCards, x.UsedSkills, x.TransferGold, x.BoughtRelics,
            x.FinalBossKill, x.SelectedRelicCount, x.SelectedRelicsText
        }),
        events = report.Events.Select(x => new
        {
            x.FrameIndex, x.Round, x.RoundText, x.PlayerId, x.PlayerName, x.Type,
            x.Title, x.Description, icon = AssetUrl(x.IconPath)
        }),
        relics = report.Relics.Select(x => new
        {
            x.FrameIndex, x.Round, x.PlayerId, x.PlayerName, x.HeroName, x.Kind, x.Level, x.LevelText,
            x.RelicId, x.RelicName, x.Quality, x.OptionsText, x.Source, x.RefreshNumber, x.IsRefresh,
            x.RefreshText, image = AssetUrl(x.ImagePath), sourceIcon = AssetUrl(x.SourceIconPath)
        }),
        shops = report.Shops.Select(x => new
        {
            x.FrameIndex, x.Round, x.PlayerId, x.PlayerName, x.HeroName, x.ShopType,
            x.OptionsText, x.Price, x.Discount, x.PriceText, x.FreeCard, x.FreeCardNum,
            x.Alreadys, x.BuyIndices, x.BoughtText, x.PurchaseText,
            x.IsClosed, x.HasPurchase, x.SoldOutText
        }),
        frames = report.Frames.Select(x => new { x.Index, x.Offset, x.OffsetText, x.CmdId, x.MessageName, x.PayloadLength }),
        statistics = new
        {
            commands = report.CommandStats,
            actions = report.ActionStats,
            relicQualities = report.RelicQualityStats
        },
        warnings = report.Warnings
    };

    private static object BuildExportReport(ReplayReport report) => new
    {
        schemaVersion = 1,
        source = new { report.FileName, report.FileSizeText, report.Sha256 },
        match = new
        {
            report.ReplayId, report.GameVersion, report.RoomId, report.RoomServerId,
            report.MapId, report.MapName, report.MapType, report.Difficulty,
            report.StartTimeText, report.FinishTimeText, report.DurationText,
            report.ResultText, report.WinnerText, report.RoundCount, report.TurnCount,
            report.PlayerDeaths, report.GameProgress, report.GameMaxProgress,
            report.BossName, report.AwardsText
        },
        players = report.Players,
        timeline = report.Events,
        relics = report.Relics,
        shops = report.Shops,
        statistics = new { commands = report.CommandStats, actions = report.ActionStats, relicQualities = report.RelicQualityStats },
        frames = report.Frames.Select(frame => new
        {
            frame.Index, frame.Offset, frame.CmdId, frame.MessageName, frame.PayloadLength,
            payloadBase64 = Convert.ToBase64String(frame.Payload)
        }),
        warnings = report.Warnings
    };

    private string AssetUrl(string path)
    {
        if (EmbeddedAssetStore.TryGetRelativePath(path, out var embeddedPath))
            return "https://assets.astral.local/" + EncodePath(embeddedPath);

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return "";
        var relative = Path.GetRelativePath(_assetDirectory, path).Replace('\\', '/');
        if (!relative.StartsWith("../", StringComparison.Ordinal))
            return "https://assets.astral.local/" + EncodePath(relative);

        for (var index = 0; index < _materialDirectories.Count; index++)
        {
            relative = Path.GetRelativePath(_materialDirectories[index], path).Replace('\\', '/');
            if (!relative.StartsWith("../", StringComparison.Ordinal))
                return $"https://materials{index}.astral.local/" + EncodePath(relative);
        }
        return "";
    }

    private static string EncodePath(string relative) => string.Join('/', relative.Split('/').Select(Uri.EscapeDataString));

    // ============================== 变速器（游戏工具） ==============================

    private void PushSpeedhackStatus()
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

        Post(new { type = "speedhackStatus", payload = new { status, config = editor, configBroken } });
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
            _speedhackManager.SaveStoredGameDirectory(directory);
            Post(new { type = "toast", message = $"已自动定位游戏目录：{directory}" });
        }
        PushSpeedhackStatus();
    }

    private void HandleSpeedhackBrowse()
    {
        var current = _speedhackManager.ResolveGameDirectory();
        var dialog = new OpenFolderDialog
        {
            Title = "选择吉星派对游戏目录（AstralParty exe 所在文件夹）",
            InitialDirectory = current is { Length: > 0 } && Directory.Exists(current) ? current : null
        };
        if (dialog.ShowDialog(this) == true && dialog.FolderName is { Length: > 0 })
        {
            _speedhackManager.SaveStoredGameDirectory(dialog.FolderName);
            Post(new { type = "toast", message = $"已选择游戏目录：{dialog.FolderName}" });
            PushSpeedhackStatus();
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
        PushSpeedhackStatus();
    }

    private void HandleSpeedhackUninstall(bool force)
    {
        var result = _speedhackManager.Uninstall(RequireGameDirectory(), force);
        Post(new { type = "toast", message = result.Message });
        PushSpeedhackStatus();
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
            PushSpeedhackStatus();
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
        PushSpeedhackStatus();
    }

    private void HandleSpeedhackResetConfig()
    {
        _speedhackManager.ResetProfileToTemplate();
        var installedDirectory = _speedhackManager.GetStatus().Installed
            ? _speedhackManager.GetStatus().GameDirectory : null;
        if (!string.IsNullOrEmpty(installedDirectory)) _speedhackManager.PushConfigToGame(installedDirectory);

        Post(new { type = "toast", message = "已恢复默认模板（Ctrl 切换 2 倍速）。" });
        PushSpeedhackStatus();
    }

    private void HandleSpeedhackPushConfig()
    {
        var directory = RequireGameDirectory();
        if (!File.Exists(Path.Combine(directory, SpeedhackManager.DllName)))
            throw new InvalidOperationException("游戏目录尚未安装变速器，请先安装。");
        _speedhackManager.PushConfigToGame(directory);
        Post(new { type = "toast", message = "配置已同步到游戏目录，重启游戏后生效。" });
        PushSpeedhackStatus();
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

    private void PushModStatus()
    {
        var status = _modManager.GetStatus();
        Post(new { type = "modStatus", payload = new { status } });
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
            _modManager.SaveStoredGameDirectory(directory);
            Post(new { type = "toast", message = $"已自动定位游戏目录：{directory}" });
        }
        PushModStatus();
    }

    private void HandleModBrowse()
    {
        var current = _modManager.ResolveGameDirectory();
        var dialog = new OpenFolderDialog
        {
            Title = "选择吉星派对游戏目录（AstralParty exe 所在文件夹）",
            InitialDirectory = current is { Length: > 0 } && Directory.Exists(current) ? current : null
        };
        if (dialog.ShowDialog(this) == true && dialog.FolderName is { Length: > 0 })
        {
            _modManager.SaveStoredGameDirectory(dialog.FolderName);
            Post(new { type = "toast", message = $"已选择游戏目录：{dialog.FolderName}" });
            PushModStatus();
        }
    }

    private void HandleModInstall(bool overwriteDll, bool includeSample)
    {
        _modManager.Install(RequireModGameDirectory(), overwriteDll, includeSample);
        Post(new { type = "toast", message = ModManager.DescribeInstalled() });
        PushModStatus();
    }

    private void HandleModUninstall(bool force)
    {
        var result = _modManager.Uninstall(RequireModGameDirectory(), force);
        Post(new { type = "toast", message = result.Message });
        PushModStatus();
    }

    private void HandleModOpenFolder(string folderName)
    {
        var directory = RequireModGameDirectory();
        var folder = Path.Combine(directory, ModManager.LoaderFolderName, folderName);
        Directory.CreateDirectory(folder);
        ModManager.OpenInExplorer(folder);
    }

    private void HandleModImport(string path)
    {
        var entry = _modManager.ImportMod(RequireModGameDirectory(), path);
        Post(new { type = "toast", message = $"已导入 mod：{entry.FileName}（{FormatBytes(entry.SizeBytes)}）" });
        PushModStatus();
    }

    private void HandleModPickImport()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要导入的 mod DLL",
            Filter = "Mod DLL|*.dll|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = true
        };
        if (dialog.ShowDialog(this) != true || dialog.FileNames.Length == 0) return;
        var imported = 0;
        foreach (var file in dialog.FileNames)
        {
            try
            {
                _modManager.ImportMod(RequireModGameDirectory(), file);
                imported++;
            }
            catch (Exception ex)
            {
                Post(new { type = "toast", message = $"导入 {Path.GetFileName(file)} 失败：{ex.Message}" });
            }
        }
        if (imported > 0)
            Post(new { type = "toast", message = $"已导入 {imported} 个 mod，重启游戏后生效。" });
        PushModStatus();
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
        PushModStatus();
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
        PushModStatus();
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

    private async Task HandleModCheckUpdateAsync()
    {
        Post(new { type = "toast", message = "正在检查 CesiumLoader 最新版本…" });
        try
        {
            var bytes = await _modManager.DownloadLatestPackageAsync().ConfigureAwait(true);
            var manifest = ModManager.ParsePackageManifest(bytes);
            var latest = manifest?.Version ?? "";
            var installed = _modManager.GetStatus().InstalledVersion;

            var message = string.IsNullOrEmpty(latest)
                ? "已连接到 GitHub，但发布包没有版本信息。"
                : string.IsNullOrEmpty(installed)
                    ? $"最新版本：{latest}（游戏目录里未记录已装版本，可直接更新）。"
                    : string.Equals(installed, latest, StringComparison.OrdinalIgnoreCase)
                        ? $"已是最新版本：{latest}。"
                        : $"发现新版本：{installed} → {latest}。";

            Post(new { type = "toast", message });
            PushModStatus();
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"检查更新失败：{ex.Message}" });
        }
    }

    private async Task HandleModDownloadUpdateAsync(bool overwriteDll)
    {
        Post(new { type = "toast", message = "正在下载最新 CesiumLoader 并安装…" });
        try
        {
            var directory = RequireModGameDirectory();
            var bytes = await _modManager.DownloadLatestPackageAsync().ConfigureAwait(true);
            var manifest = ModManager.ParsePackageManifest(bytes);
            _modManager.InstallPackage(directory, bytes, overwriteDll);

            var versionText = string.IsNullOrEmpty(manifest?.Version) ? "" : $"（{manifest.Version}）";
            Post(new { type = "toast", message = $"加载器已更新{versionText}。" + (SpeedhackManager.IsGameRunning() ? "游戏正在运行，重启游戏后生效。" : "") });
            PushModStatus();
        }
        catch (Exception ex)
        {
            Post(new { type = "toast", message = $"更新失败：{ex.Message}" });
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.##} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes} B";
    }

    private static readonly JsonSerializerOptions WebReadOptions = new() { PropertyNameCaseInsensitive = true };

    private void Post(object message)
    {
        if (!_webReady || WebView.CoreWebView2 is null) return;
        WebView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(message, JsonOptions));
    }

    private void ClassicButton_Click(object sender, RoutedEventArgs e) => OpenClassicWindow();

    private void OpenClassicWindow()
    {
        var window = new MainWindow();
        Application.Current.MainWindow = window;
        window.Show();
        Close();
    }
}
