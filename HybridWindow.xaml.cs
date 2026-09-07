using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using AstralParty.ReplayTool.Services;

namespace AstralParty.ReplayTool;

public partial class HybridWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _appDirectory = AppContext.BaseDirectory;
    private readonly string _assetDirectory;
    private readonly IReadOnlyList<string> _materialDirectories;
    private readonly HomeDataService _homeDataService;
    private ReplayAnalyzer? _analyzer;
    private ReplayReport? _report;
    private bool _webReady;
    private readonly Dictionary<string, string> _replayLibrary = new(StringComparer.Ordinal);

    public HybridWindow()
    {
        InitializeComponent();
        _assetDirectory = Path.Combine(_appDirectory, "Assets");
        _materialDirectories = MaterialSource.DiscoverAll(_appDirectory);
        _homeDataService = new HomeDataService(_appDirectory);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var webRoot = Path.Combine(_appDirectory, "WebUI");
            if (!File.Exists(Path.Combine(webRoot, "index.html")))
                throw new FileNotFoundException("找不到 WebUI/index.html。", Path.Combine(webRoot, "index.html"));

            StartupDetail.Text = "初始化 Microsoft Edge WebView2";
            await WebView.EnsureCoreWebView2Async();
            WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "app.astral.local", webRoot, CoreWebView2HostResourceAccessKind.DenyCors);
            WebView.CoreWebView2.AddWebResourceRequestedFilter(
                "https://assets.astral.local/*", CoreWebView2WebResourceContext.Image);
            WebView.CoreWebView2.WebResourceRequested += EmbeddedAssetRequested;
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
            StartupDetail.Text = $"WebView2 启动失败：{ex.Message}";
            ClassicButton.Visibility = Visibility.Visible;
        }
    }

    private void EmbeddedAssetRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = new Uri(e.Request.Uri);
        var stream = EmbeddedAssetStore.OpenWebPath(uri.AbsolutePath);
        e.Response = stream is null
            ? WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                Stream.Null, 404, "Not Found", "Content-Type: text/plain")
            : WebView.CoreWebView2.Environment.CreateWebResourceResponse(
                stream, 200, "OK",
                "Content-Type: image/webp\r\nCache-Control: no-store\r\nAccess-Control-Allow-Origin: *");
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
                    Post(new { type = "hostReady", payload = new { runtime = "WPF · .NET 8 · WebView2", offline = true } });
                    Post(new { type = "homeData", payload = _homeDataService.GetHomeData() });
                    await SendReplayLibraryAsync();
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
                    await SendReplayLibraryAsync();
                    break;
                case "getFrame":
                    if (root.TryGetProperty("index", out var indexElement)) await SendFrameDetailAsync(indexElement.GetInt32());
                    break;
                case "export":
                    await ExportAsync();
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

    private async Task SendReplayLibraryAsync()
    {
        var directory = FindReplayDirectory();
        var files = await Task.Run(() =>
        {
            if (!Directory.Exists(directory)) return new List<FileInfo>();
            try
            {
                return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .Where(file => file.Length > 0)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(100)
                    .ToList();
            }
            catch (IOException) { return []; }
            catch (UnauthorizedAccessException) { return []; }
        });

        _replayLibrary.Clear();
        var items = files.Select((file, index) =>
        {
            var token = $"replay-{index}-{file.LastWriteTimeUtc.Ticks}";
            _replayLibrary[token] = file.FullName;
            var parentName = file.Directory?.Name;
            return new
            {
                token,
                name = string.IsNullOrWhiteSpace(parentName) ? file.Name : $"回放 {parentName}",
                fileName = file.Name,
                sizeText = FormatLibrarySize(file.Length),
                modifiedText = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm")
            };
        }).ToList();

        Post(new { type = "replayLibrary", payload = new { directory, available = Directory.Exists(directory), items } });
    }

    private static string FindReplayDirectory()
    {
        var localLow = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "feimo");
        var candidates = new[]
        {
            Path.Combine(localLow, "AstralParty_CN", "Temp", "Replay"),
            Path.Combine(localLow, "AstralParty", "_CN", "Temp", "Replay")
        };
        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

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

    private async Task ExportAsync()
    {
        if (_report is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "导出回放数据",
            Filter = "JSON 文件|*.json",
            FileName = $"{Path.GetFileNameWithoutExtension(_report.FileName)}.report.json"
        };
        if (dialog.ShowDialog(this) != true) return;

        Post(new { type = "loading", active = true, message = "正在导出完整协议数据…" });
        try
        {
            var json = await Task.Run(() => JsonSerializer.Serialize(BuildExportReport(_report), new JsonSerializerOptions(JsonOptions) { WriteIndented = true }));
            await File.WriteAllTextAsync(dialog.FileName, json);
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
            x.FrameIndex, x.Round, x.PlayerId, x.PlayerName, x.Kind, x.Level, x.LevelText,
            x.RelicId, x.RelicName, x.Quality, x.OptionsText, x.Source, x.RefreshNumber, x.IsRefresh,
            x.RefreshText, image = AssetUrl(x.ImagePath), sourceIcon = AssetUrl(x.SourceIconPath)
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
