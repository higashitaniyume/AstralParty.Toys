namespace AstralParty.Toys.Services;

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
    public bool ForwardActivityLog { get; set; } = true;
    public double SpeedhackBaseSpeed { get; set; } = 1.0;
    /// <summary>变速控制文件通道(mod 热键变速用)。默认开; 关掉后 SpeedHackMod 的热键失效(基础倍率仍生效)。</summary>
    public bool SpeedControlEnabled { get; set; } = true;

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

    public string SdkVersion { get; set; } = "2.0.0";
}
