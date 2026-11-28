namespace AstralParty.Toys.Services;

/// <summary>
/// 工具箱统一配置的落盘模型，对应 <c>AstralParty.Toys.Config.json</c>。
///
/// 只放"设置"：语言、主题、回放库、市场、更新、日志。高频写入的运行状态（游戏目录、加载器安装状态、
/// 变速器状态）仍然单独放在 <c>state\</c>，避免一次模组安装就重写整份配置、把损坏的波及面放大。
/// </summary>
public sealed class ToysConfig
{
    public int SchemaVersion { get; set; } = ToysConfigStore.CurrentSchemaVersion;

    public AppConfigSection App { get; set; } = new();

    public LoggingConfigSection Logging { get; set; } = new();

    public ReplayConfigSection Replay { get; set; } = new();

    public WorkshopConfigSection Workshop { get; set; } = new();

    public UpdateConfigSection Update { get; set; } = new();

    public MigrationConfigSection Migration { get; set; } = new();

    public MobileConfigSection Mobile { get; set; } = new();

    public HubConfigSection Hub { get; set; } = new();

    /// <summary>把外部写进来的值收拢到合法区间；文件被手改坏时也能继续用。</summary>
    public ToysConfig Normalize()
    {
        App ??= new AppConfigSection();
        Logging ??= new LoggingConfigSection();
        Replay ??= new ReplayConfigSection();
        Workshop ??= new WorkshopConfigSection();
        Update ??= new UpdateConfigSection();
        Migration ??= new MigrationConfigSection();
        Mobile ??= new MobileConfigSection();
        Mobile.AdbPath = (Mobile.AdbPath ?? "").Trim();
        Hub ??= new HubConfigSection();
        Hub.Normalize();

        App.Language = AppLanguage.Normalize(App.Language);
        App.Theme = string.Equals(App.Theme, "light", StringComparison.OrdinalIgnoreCase) ? "light" : "dark";

        Logging.Level = ToysLog.NormalizeLevel(Logging.Level);
        Logging.RetentionDays = Math.Clamp(Logging.RetentionDays, 1, 365);
        Logging.MaxFileBytes = Math.Clamp(Logging.MaxFileBytes, 256 * 1024, 256L * 1024 * 1024);

        Replay.KeepInGame = Math.Clamp(Replay.KeepInGame, 1, ReplayServiceLimits.GameSlotCapacity);

        Workshop.Url = WorkshopService.NormalizeBaseUrl(Workshop.Url);
        Workshop.ConsentVersion = Math.Max(0, Workshop.ConsentVersion);

        Update.GitHubMirrorType = (Update.GitHubMirrorType ?? "").Trim().ToLowerInvariant();
        if (Update.GitHubMirrorType is not (GitHubMirrorHelper.TypeDirect or GitHubMirrorHelper.TypeGhProxy
            or GitHubMirrorHelper.TypeGhFast or GitHubMirrorHelper.TypeCustom))
        {
            Update.GitHubMirrorType = GitHubMirrorHelper.DefaultMirrorType;
        }
        Update.CustomGitHubMirrorUrl = (Update.CustomGitHubMirrorUrl ?? "").Trim();
        Update.LastSeenAppVersion = (Update.LastSeenAppVersion ?? "").Trim();
        Update.LastSeenEmbeddedLoaderVersion = (Update.LastSeenEmbeddedLoaderVersion ?? "").Trim();

        SchemaVersion = ToysConfigStore.CurrentSchemaVersion;
        return this;
    }
}

public sealed class AppConfigSection
{
    /// <summary>界面与配表语言（zh-Hans, zh-Hant, en, ja）。</summary>
    public string Language { get; set; } = AppLanguage.SimplifiedChinese;

    /// <summary>启动画面用的深浅色（dark / light）。</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>「我的账号」免责说明是否已同意。</summary>
    public bool MyAccountConsent { get; set; }
}

public sealed class LoggingConfigSection
{
    /// <summary>debug / info / warn / error。</summary>
    public string Level { get; set; } = "info";

    /// <summary>日志保留天数。</summary>
    public int RetentionDays { get; set; } = ToysLog.DefaultRetentionDays;

    /// <summary>单个日志文件的上限字节数，超了继续写 -1、-2。</summary>
    public long MaxFileBytes { get; set; } = ToysLog.DefaultMaxFileBytes;
}

public sealed class ReplayConfigSection
{
    /// <summary>开启后每次刷新列表都会自动把游戏目录里超出保留名额的旧回放归档进库。</summary>
    public bool AutoMaintain { get; set; }

    /// <summary>游戏目录里保留的最新回放数量。</summary>
    public int KeepInGame { get; set; } = ReplayServiceLimits.GameSlotCapacity;
}

public sealed class WorkshopConfigSection
{
    /// <summary>市场地址（Forge 公共站点根地址）。</summary>
    public string Url { get; set; } = WorkshopService.DefaultBaseUrl;

    /// <summary>已同意的市场免责说明版本；小于当前版本时进入市场会先询问。</summary>
    public int ConsentVersion { get; set; }

    /// <summary>同意时间（UTC）。</summary>
    public DateTimeOffset? ConsentAtUtc { get; set; }

    /// <summary>市场下载并发线程数（1–16，默认 8；支持断点续传）。</summary>
    public int DownloadThreads { get; set; } = 8;
}

public sealed class UpdateConfigSection
{
    public bool AutoCheck { get; set; } = true;
    public bool AutoDownload { get; set; }
    public bool AutoInstall { get; set; }

    /// <summary>发现新版本、更新完毕或首次安装时自动弹窗展示更新日志。</summary>
    public bool AutoChangelogPopup { get; set; } = true;

    /// <summary>上次已展示/确认更新日志的工具箱版本号（空串代表尚未记录，即全新安装或老版本升级）。</summary>
    public string LastSeenAppVersion { get; set; } = "";

    /// <summary>上次已知晓的随包内置加载器版本号。</summary>
    public string LastSeenEmbeddedLoaderVersion { get; set; } = "";

    /// <summary>gh-proxy / ghfast / custom / direct。</summary>
    public string GitHubMirrorType { get; set; } = GitHubMirrorHelper.DefaultMirrorType;

    public string CustomGitHubMirrorUrl { get; set; } = "";
}

public sealed class MigrationConfigSection
{
    /// <summary>旧布局迁移完成时间（UTC）。为空表示还没迁移过。</summary>
    public DateTimeOffset? CompletedUtc { get; set; }

    /// <summary>迁移来源目录，便于排查。</summary>
    public string FromRoot { get; set; } = "";
}

public sealed class MobileConfigSection
{
    /// <summary>玩家指定的 adb.exe 绝对路径；为空时自动按 SDK / PATH 查找。</summary>
    public string AdbPath { get; set; } = "";
}

public sealed class HubConfigSection
{
    public const string DefaultServerUrl = "https://connect.hiynet.com";

    /// <summary>组队大厅后端服务根地址（默认 connect.hiynet.com）。</summary>
    public string ServerUrl { get; set; } = DefaultServerUrl;

    /// <summary>当前选择的区服（cn / int）。</summary>
    public string ActiveRegion { get; set; } = "cn";

    /// <summary>上次登录的大厅用户 ID。</summary>
    public string UserId { get; set; } = "";

    /// <summary>上次登录的昵称缓存。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>使用当前 Windows 用户 DPAPI 加密后的 hubToken（Base64）。</summary>
    public string ProtectedTokenBase64 { get; set; } = "";

    /// <summary>令牌过期时间（Unix 毫秒）。</summary>
    public long TokenExpiresAt { get; set; }

    public void Normalize()
    {
        ServerUrl = NormalizeServerUrl(ServerUrl);
        ActiveRegion = string.Equals(ActiveRegion?.Trim(), "int", StringComparison.OrdinalIgnoreCase) ? "int" : "cn";
        UserId = (UserId ?? "").Trim();
        DisplayName = (DisplayName ?? "").Trim();
        ProtectedTokenBase64 = (ProtectedTokenBase64 ?? "").Trim();
        if (TokenExpiresAt < 0) TokenExpiresAt = 0;
    }

    public static string NormalizeServerUrl(string? raw)
    {
        var text = (raw ?? "").Trim().TrimEnd('/');
        if (text.Length == 0) return DefaultServerUrl;
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = text.StartsWith("127.0.0.1", StringComparison.Ordinal) ||
                   text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
                ? "http://" + text
                : "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return DefaultServerUrl;
        if (string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            return text.TrimEnd('/');
        if (string.Equals(uri.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
            (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uri.Host, "127.0.0.1", StringComparison.Ordinal)))
            return text.TrimEnd('/');

        return DefaultServerUrl;
    }
}

/// <summary>
/// 回放库的常量。放在这里而不是 <c>ReplayLibraryService</c> 上，是为了让配置模型不必依赖那个大服务的静态成员。
/// </summary>
public static class ReplayServiceLimits
{
    /// <summary>游戏客户端写死的本地回放席位上限（Global 配置 MAX_BATTLE_RECORDS）。</summary>
    public const int GameSlotCapacity = 10;
}
