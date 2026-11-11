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

    /// <summary>把外部写进来的值收拢到合法区间；文件被手改坏时也能继续用。</summary>
    public ToysConfig Normalize()
    {
        App ??= new AppConfigSection();
        Logging ??= new LoggingConfigSection();
        Replay ??= new ReplayConfigSection();
        Workshop ??= new WorkshopConfigSection();
        Update ??= new UpdateConfigSection();
        Migration ??= new MigrationConfigSection();

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
}

public sealed class UpdateConfigSection
{
    public bool AutoCheck { get; set; } = true;
    public bool AutoDownload { get; set; }
    public bool AutoInstall { get; set; }

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

/// <summary>
/// 回放库的常量。放在这里而不是 <c>ReplayLibraryService</c> 上，是为了让配置模型不必依赖那个大服务的静态成员。
/// </summary>
public static class ReplayServiceLimits
{
    /// <summary>游戏客户端写死的本地回放席位上限（Global 配置 MAX_BATTLE_RECORDS）。</summary>
    public const int GameSlotCapacity = 10;
}
