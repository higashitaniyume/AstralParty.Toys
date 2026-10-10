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
    private readonly SkinManager _skinManager;
    private readonly ReplayLibraryService _libraryService;
    private readonly GameLibraryService _gameLibrary;
    private ReplayAnalyzer? _analyzer;
    private ReplayReport? _report;
    private static readonly Uri SpectatorUri = new("https://astralpartycards.hiynet.com/");
    private CoreWebView2Environment? _webEnvironment;
    private bool _webReady;
    private bool _webRootIsEmbedded;
    private readonly Dictionary<string, string> _replayLibrary = new(StringComparer.Ordinal);
    private CancellationTokenSource? _deepScanCts;
    /// <summary>回放下载的取消源：同一时刻只允许一个下载，见 <see cref="HandleReplayDownloadAsync"/>。</summary>
    private CancellationTokenSource? _replayDownloadCts;

    public HybridWindow()
    {
        InitializeComponent();
        _assetDirectory = Path.Combine(_appDirectory, "Assets");
        _materialDirectories = MaterialSource.DiscoverAll(_appDirectory);
        _homeDataService = new HomeDataService(_appDirectory);
        _speedhackManager = new SpeedhackManager(_appDirectory);
        _modManager = new ModManager(_appDirectory);
        _skinManager = new SkinManager(_appDirectory);
        _libraryService = new ReplayLibraryService(_appDirectory);
        _gameLibrary = new GameLibraryService();
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
            _webEnvironment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataDirectory);
            await WebView.EnsureCoreWebView2Async(_webEnvironment);
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
            WebView.CoreWebView2.AddWebResourceRequestedFilter(
                "https://skins.astral.local/*", CoreWebView2WebResourceContext.Image);
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


    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024.0:0.##} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes} B";
    }

    private static readonly JsonSerializerOptions WebReadOptions = new() { PropertyNameCaseInsensitive = true };

    private void Post(object message)
    {
        // 可能从后台线程调用（如深度扫描的进度回调）：WebView2 必须在 UI 线程上访问
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => Post(message));
            return;
        }
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

    /// <summary>
    /// 立刻在调用线程上执行回调的 <see cref="IProgress{T}"/>。
    /// 用于「进度必须按顺序到达」的场合：<see cref="Progress{T}"/> 会把回调异步投递到 UI 上下文，
    /// 排在收尾消息之后到达的最后一个进度会让界面回退一格。
    /// </summary>
    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _callback;

        public InlineProgress(Action<T> callback) => _callback = callback;

        public void Report(T value) => _callback(value);
    }
}
