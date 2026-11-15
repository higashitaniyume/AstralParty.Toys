namespace AstralParty.Toys.Models;

/// <summary>加载器配置（doorstop_config.json）的可编辑字段。</summary>
public sealed class LoaderConfig
{
    public bool Enabled { get; set; } = true;
    public bool UseManagedBootstrap { get; set; }
    public string BootstrapAssembly { get; set; } = "CesiumLoader.Bootstrap.dll";
    public string BootstrapType { get; set; } = "CesiumLoader.Bootstrap.Bootstrap";
    public string BootstrapMethod { get; set; } = "Main";
    public int GameAssemblyTimeoutSec { get; set; } = 60;
    public int DomainTimeoutSec { get; set; } = 30;
    public int HybridclrTimeoutSec { get; set; } = 60;
    public bool ConsoleEnabled { get; set; } = true;
    public bool ConsoleTopmost { get; set; } = true;
    /// <summary>CesiumLoader Debug Shell: 控制台命令输入(输入 help 查看命令)。默认开; 关掉只影响命令, 日志照常。</summary>
    public bool DebugShell { get; set; } = true;
    public bool ForwardActivityLog { get; set; } = true;
    public double SpeedhackBaseSpeed { get; set; } = 1.0;
    /// <summary>变速控制文件通道(mod 热键变速用)。默认开; 关掉后 SpeedHackMod 的热键失效(基础倍率仍生效)。</summary>
    public bool SpeedControlEnabled { get; set; } = true;
    /// <summary>窗口置前文件通道(回合置前类 mod 用)。默认开; 关掉后只影响这类 mod。</summary>
    public bool WindowControlEnabled { get; set; } = true;
    /// <summary>原生音频控制通道(UDP + 加载器 audio 目录, 自定义音效/BGM 用)。默认开。</summary>
    public bool AudioControlEnabled { get; set; } = true;

    // ---- 排查: 崩溃转储 / 卡死看门狗 / 日志级别 / 等待调试器(加载器原生层功能) ----

    /// <summary>崩溃转储: 原生层遇到未处理异常时在加载器目录 crash\ 写 &lt;会话&gt;.dmp 与同名 .txt 摘要,
    /// 可直接用 Visual Studio 打开看线程栈。默认开; 只在真崩溃时产生文件。</summary>
    public bool CrashDumpEnabled { get; set; } = true;

    /// <summary>⚠ 转储包含完整内存(文件可达数 GB, 写出时游戏停顿更久)。只在排查托管崩溃 / 局部变量时才开。</summary>
    public bool CrashDumpFullMemory { get; set; }

    /// <summary>卡死看门狗: 主线程长时间不响应时自动写 crash\hang-&lt;会话&gt;.dmp。
    /// 平时有轻微开销, 排查「游戏卡死」时再打开。</summary>
    public bool NativeWatchdogEnabled { get; set; }

    /// <summary>卡死判定秒数(加载器只接受 [1,3600], 建议 20–60)。</summary>
    public int NativeWatchdogTimeoutSec { get; set; } = 30;

    /// <summary>日志级别: inherit(跟随环境变量) / Debug / Info / Warn / Error / Fatal。
    /// 加载器初始化 SDK 与 mod 之前把它注入 CESIUM_LOG_LEVEL; Debug 会写出大量每帧日志, 排查完记得改回。</summary>
    public string LogLevel { get; set; } = "inherit";

    /// <summary>启动时等待调试器接入(开发用): 等到有调试器挂上再继续, 并在该处断开点。
    /// 没接调试器时会一直等到 <see cref="DebugWaitTimeoutSec"/>; Steam 启动删不掉参数, 普通玩家保持关闭。</summary>
    public bool DebugWaitForDebugger { get; set; }

    /// <summary>等待调试器的超时秒数(加载器只接受 [1,3600])。</summary>
    public int DebugWaitTimeoutSec { get; set; } = 120;

    // ---- Steam 绕过(加载器原生功能, 非 mod; 见加载器 docs\steam-bypass.md) ----
    // 这些是**加载器**(version.dll) 的功能开关: 国服客户端在非 Steam 启动时, AOT 类
    // SteamManager.Awake() 会打印 "[ERROR] [SteamManager] 非Steam客户端启动, 退出游戏" 并退出。
    // 只能由加载器在托管层被加载**之前**用原生 inline hook 拦下, 所以它做不成 mod。

    /// <summary>主开关: 把 AOT 的 SteamManager.Awake() 换成 no-op, 让游戏在无 Steam 时继续启动。关闭 = 恢复原版行为(非 Steam 启动会退出游戏)。</summary>
    public bool SteamBypassEnabled { get; set; } = true;

    /// <summary>附加保险: 让 steam_api64.dll 的 SteamAPI_RestartAppIfNecessary 恒返回 0。仅在主开关为 true 时有意义。</summary>
    public bool SteamBypassRestartCheck { get; set; } = true;

    /// <summary>大厅匹配绕过: CreateLobbyAsync / JoinLobbyAsync 改为返回"已完成的空 Task", 修"Steam 全关时点创建/加入房间毫无反应"。</summary>
    public bool SteamBypassMatchmaking { get; set; } = true;

    /// <summary>方案C 的调用方式: auto(默认, 直调失败自动退回) / direct / invoke。一般保持 auto。</summary>
    public string SteamBypassTaskCtorMode { get; set; } = "auto";

    /// <summary>退房兜底: LobbyQuery.RequestAsync 改为返回"结果为长度 0 的 Lobby[] 的已完成 Task", 修退房/解散/被踢时 await 之后的 6 条 NRE。</summary>
    public bool SteamBypassLobbyQuery { get; set; } = true;

    /// <summary>阶段3(实测作废, hook 保留但从未被命中): Nullable&lt;Lobby&gt;.get_HasValue 恒返回 false。仅在"大厅匹配绕过"开启时有意义。</summary>
    public bool SteamBypassLobbyHasValue { get; set; } = true;

    /// <summary>方案B 备用安全网(挂 no-op Lobby.SetPublic / SetJoinable / get_Id)。
    /// <para>★ 默认值必须是 false: 实机验证置 true 会导致游戏启动早期崩溃(0xC0000005, 模块 GameAssembly.dll)。
    /// 主解法(方案C)已把 Task 结果构造成真正的空 Nullable, 这块代码根本不会被调用, 所以**不需要**它。</para></summary>
    public bool SteamBypassLobbyMethods { get; set; }

    // ---- TapTap 绕过(加载器原生功能, 见加载器 taptaphack.h) ----
    /// <summary>TapTap 绕过主开关: IsLaunchedFromTapTapPC 恒返回 true。</summary>
    public bool TapTapBypassEnabled { get; set; }

    /// <summary>TapTap 附加保险: 让 taptap_api.dll 的 TapSDK_RestartAppIfNecessary 恒返回 0。</summary>
    public bool TapTapBypassRestartCheck { get; set; }

    /// <summary>TapTap 登录 stub: 预留开关。</summary>
    public bool TapTapBypassLogin { get; set; }

    /// <summary>加载器自身版本(SemVer)。与发布 tag (modloader-&lt;版本&gt;) 对应, 启动横幅会打印。</summary>
    public string SdkVersion { get; set; } = "2.0.0";

    /// <summary>多 profile: 把游戏的账号 / 存档 / 回放目录重定向到
    /// <c>%USERPROFILE%\AppData\LocalLow\&lt;公司&gt;\&lt;产品&gt;_profiles\&lt;名&gt;</c>(加载器原生功能)。
    /// 空串 = 不重定向(游戏写自己的目录); 绝对路径 = 直接把 profile 放到指定目录。
    /// 见加载器 <c>docs\多开-profile.md</c>。</summary>
    public string Profile { get; set; } = "";

    /// <summary>多 profile: 在游戏窗口标题后追加 profile 名(如 "吉星派对 · P2"),
    /// 方便同时开多个实例时从任务栏 / Alt+Tab 分辨。默认 true; 只在配置了 profile 时生效,
    /// 关掉只影响标题显示, 目录隔离照常。见加载器 <c>docs\多开-profile.md</c>。</summary>
    public bool ProfileWindowTitle { get; set; } = true;
}
