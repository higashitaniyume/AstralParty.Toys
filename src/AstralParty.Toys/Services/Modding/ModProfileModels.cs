using System.Text.Json;
using System.Text.Json.Nodes;
using AstralParty.Toys.Models;

namespace AstralParty.Toys.Services.Modding;

/// <summary>模组方案的落盘根对象（<c>state\mod-profiles.json</c>，全局共享）。</summary>
public sealed class ModProfileStore
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>当前使用的方案名（全局唯一，空串 = 没有选方案）。</summary>
    public string Active { get; set; } = "";

    public List<ModProfile> Profiles { get; set; } = new();
}

/// <summary>一个模组方案：捕获时刻的启用状态 + 每个模组的配置原文 + 加载器设置子集。</summary>
public sealed class ModProfile
{
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";

    /// <summary>捕获时所在游戏目录，仅作展示（方案是全局共享的，可以套到别的目录）。</summary>
    public string CapturedFrom { get; set; } = "";

    public string CreatedAtUtc { get; set; } = "";
    public string UpdatedAtUtc { get; set; } = "";

    /// <summary>捕获时是否带上了模组配置（不带 = 套用时不动任何 config.json）。</summary>
    public bool IncludesConfigs { get; set; } = true;

    /// <summary>捕获时是否带上了加载器设置；加载器没装时为 false，套用时跳过这一部分。</summary>
    public bool IncludesLoader { get; set; } = true;

    public List<ModProfileMod> Mods { get; set; } = new();

    public ModProfileLoaderSettings Loader { get; set; } = new();
}

/// <summary>方案里的一个模组。匹配键是 DLL 文件名去掉扩展名。</summary>
public sealed class ModProfileMod
{
    public string Name { get; set; } = "";
    public string Id { get; set; } = "";
    public string Version { get; set; } = "";

    /// <summary>捕获时的有效启用状态（目录位置 + sidecar 都算）。</summary>
    public bool Enabled { get; set; }

    /// <summary>config.json 原文（null = 当时没有配置文件）。</summary>
    public string? Config { get; set; }

    /// <summary>原文是否带 UTF-8 BOM（写回时按原样还原）。</summary>
    public bool ConfigHasBom { get; set; }
}

/// <summary>
/// 方案里保存的加载器设置子集。
///
/// 刻意**不含** <c>enabled</c>（加载器停用开关）、<c>profile</c> / <c>profileWindowTitle</c>
/// （游戏多开的账号档案）：那些是其它功能的状态，套用方案时保留当前值。
/// </summary>
public sealed class ModProfileLoaderSettings
{
    public bool UseManagedBootstrap { get; set; }
    public string BootstrapAssembly { get; set; } = "CesiumLoader.Bootstrap.dll";
    public string BootstrapType { get; set; } = "CesiumLoader.Bootstrap.Bootstrap";
    public string BootstrapMethod { get; set; } = "Main";
    public int GameAssemblyTimeoutSec { get; set; } = 60;
    public int DomainTimeoutSec { get; set; } = 30;
    public int HybridclrTimeoutSec { get; set; } = 60;
    public bool ConsoleEnabled { get; set; } = true;
    public bool ConsoleTopmost { get; set; } = true;
    public bool DebugShell { get; set; } = true;
    public bool ForwardActivityLog { get; set; } = true;
    public double SpeedhackBaseSpeed { get; set; } = 1.0;
    public bool SpeedControlEnabled { get; set; } = true;
    public bool WindowControlEnabled { get; set; } = true;
    public bool AudioControlEnabled { get; set; } = true;
    public bool CrashDumpEnabled { get; set; } = true;
    public bool CrashDumpFullMemory { get; set; }
    public bool NativeWatchdogEnabled { get; set; }
    public int NativeWatchdogTimeoutSec { get; set; } = 30;
    public string LogLevel { get; set; } = "inherit";
    public bool DebugWaitForDebugger { get; set; }
    public int DebugWaitTimeoutSec { get; set; } = 120;
    public bool SteamBypassEnabled { get; set; } = true;
    public bool SteamBypassRestartCheck { get; set; } = true;
    public bool SteamBypassMatchmaking { get; set; } = true;
    public bool SteamBypassLobbyQuery { get; set; } = true;
    public bool SteamBypassLobbyHasValue { get; set; } = true;
    public bool SteamBypassLobbyMethods { get; set; }
    public string SteamBypassTaskCtorMode { get; set; } = "auto";
    public bool TapTapBypassEnabled { get; set; }
    public bool TapTapBypassRestartCheck { get; set; }
    public bool TapTapBypassLogin { get; set; }

    public static ModProfileLoaderSettings From(LoaderConfig config) => new()
    {
        UseManagedBootstrap = config.UseManagedBootstrap,
        BootstrapAssembly = config.BootstrapAssembly,
        BootstrapType = config.BootstrapType,
        BootstrapMethod = config.BootstrapMethod,
        GameAssemblyTimeoutSec = config.GameAssemblyTimeoutSec,
        DomainTimeoutSec = config.DomainTimeoutSec,
        HybridclrTimeoutSec = config.HybridclrTimeoutSec,
        ConsoleEnabled = config.ConsoleEnabled,
        ConsoleTopmost = config.ConsoleTopmost,
        DebugShell = config.DebugShell,
        ForwardActivityLog = config.ForwardActivityLog,
        SpeedhackBaseSpeed = config.SpeedhackBaseSpeed,
        SpeedControlEnabled = config.SpeedControlEnabled,
        WindowControlEnabled = config.WindowControlEnabled,
        AudioControlEnabled = config.AudioControlEnabled,
        CrashDumpEnabled = config.CrashDumpEnabled,
        CrashDumpFullMemory = config.CrashDumpFullMemory,
        NativeWatchdogEnabled = config.NativeWatchdogEnabled,
        NativeWatchdogTimeoutSec = config.NativeWatchdogTimeoutSec,
        LogLevel = config.LogLevel,
        DebugWaitForDebugger = config.DebugWaitForDebugger,
        DebugWaitTimeoutSec = config.DebugWaitTimeoutSec,
        SteamBypassEnabled = config.SteamBypassEnabled,
        SteamBypassRestartCheck = config.SteamBypassRestartCheck,
        SteamBypassMatchmaking = config.SteamBypassMatchmaking,
        SteamBypassLobbyQuery = config.SteamBypassLobbyQuery,
        SteamBypassLobbyHasValue = config.SteamBypassLobbyHasValue,
        SteamBypassLobbyMethods = config.SteamBypassLobbyMethods,
        SteamBypassTaskCtorMode = config.SteamBypassTaskCtorMode,
        TapTapBypassEnabled = config.TapTapBypassEnabled,
        TapTapBypassRestartCheck = config.TapTapBypassRestartCheck,
        TapTapBypassLogin = config.TapTapBypassLogin
    };

    /// <summary>
    /// 把方案里的值覆盖到当前配置上（<see cref="LoaderConfig.Enabled"/> / <see cref="LoaderConfig.Profile"/> /
    /// <see cref="LoaderConfig.ProfileWindowTitle"/> 保持当前值）。数值按加载器接受的范围夹紧，认不出的枚举回退默认值 ——
    /// 旧版本方案不该让保存直接失败。
    /// </summary>
    public void ApplyTo(LoaderConfig target)
    {
        target.UseManagedBootstrap = UseManagedBootstrap;
        if (!string.IsNullOrWhiteSpace(BootstrapAssembly)) target.BootstrapAssembly = BootstrapAssembly;
        if (!string.IsNullOrWhiteSpace(BootstrapType)) target.BootstrapType = BootstrapType;
        if (!string.IsNullOrWhiteSpace(BootstrapMethod)) target.BootstrapMethod = BootstrapMethod;
        target.GameAssemblyTimeoutSec = ClampTimeout(GameAssemblyTimeoutSec);
        target.DomainTimeoutSec = ClampTimeout(DomainTimeoutSec);
        target.HybridclrTimeoutSec = ClampTimeout(HybridclrTimeoutSec);
        target.ConsoleEnabled = ConsoleEnabled;
        target.ConsoleTopmost = ConsoleTopmost;
        target.DebugShell = DebugShell;
        target.ForwardActivityLog = ForwardActivityLog;
        target.SpeedhackBaseSpeed = Math.Clamp(
            double.IsFinite(SpeedhackBaseSpeed) ? SpeedhackBaseSpeed : 1.0,
            ModManager.MinBaseSpeed, ModManager.MaxBaseSpeed);
        target.SpeedControlEnabled = SpeedControlEnabled;
        target.WindowControlEnabled = WindowControlEnabled;
        target.AudioControlEnabled = AudioControlEnabled;
        target.CrashDumpEnabled = CrashDumpEnabled;
        target.CrashDumpFullMemory = CrashDumpFullMemory;
        target.NativeWatchdogEnabled = NativeWatchdogEnabled;
        target.NativeWatchdogTimeoutSec = ClampTimeout(NativeWatchdogTimeoutSec);
        target.LogLevel = ModManager.NormalizeLogLevel(LogLevel);
        target.DebugWaitForDebugger = DebugWaitForDebugger;
        target.DebugWaitTimeoutSec = ClampTimeout(DebugWaitTimeoutSec);
        target.SteamBypassEnabled = SteamBypassEnabled;
        target.SteamBypassRestartCheck = SteamBypassRestartCheck;
        target.SteamBypassMatchmaking = SteamBypassMatchmaking;
        target.SteamBypassLobbyQuery = SteamBypassLobbyQuery;
        target.SteamBypassLobbyHasValue = SteamBypassLobbyHasValue;
        target.SteamBypassLobbyMethods = SteamBypassLobbyMethods;
        target.SteamBypassTaskCtorMode = NormalizeTaskCtorMode(SteamBypassTaskCtorMode);
        target.TapTapBypassEnabled = TapTapBypassEnabled;
        target.TapTapBypassRestartCheck = TapTapBypassRestartCheck;
        target.TapTapBypassLogin = TapTapBypassLogin;
        // sdkVersion 属于加载器安装元数据（随加载器版本走），不纳入方案：
        // 否则套用旧方案会把 SDK 版本写回旧值，影响模组的版本协商。
    }

    private static int ClampTimeout(int value)
        => Math.Clamp(value, ModManager.MinTimeoutSec, ModManager.MaxTimeoutSec);

    private static string NormalizeTaskCtorMode(string? mode) => (mode ?? "").Trim().ToLowerInvariant() switch
    {
        "direct" => "direct",
        "invoke" => "invoke",
        _ => "auto"
    };

    /// <summary>和当前配置的差异（用于"已修改"徽标与套用预览），返回中文说明。</summary>
    public List<string> DescribeDifferences(LoaderConfig current)
    {
        var labels = LoaderFieldLabels;
        var mine = JsonSerializer.SerializeToNode(this, ModProfileJson.Options) as JsonObject;
        var now = JsonSerializer.SerializeToNode(From(current), ModProfileJson.Options) as JsonObject;
        var changes = new List<string>();
        if (mine is null || now is null) return changes;
        foreach (var (key, value) in mine)
        {
            now.TryGetPropertyValue(key, out var currentValue);
            if (JsonNode.DeepEquals(value, currentValue)) continue;
            changes.Add(labels.TryGetValue(key, out var label) ? label : key);
        }
        return changes;
    }

    private static readonly Dictionary<string, string> LoaderFieldLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["useManagedBootstrap"] = "托管引导",
        ["bootstrapAssembly"] = "引导程序集",
        ["bootstrapType"] = "引导类型",
        ["bootstrapMethod"] = "引导方法",
        ["gameAssemblyTimeoutSec"] = "程序集加载超时",
        ["domainTimeoutSec"] = "域加载超时",
        ["hybridclrTimeoutSec"] = "HybridCLR 超时",
        ["consoleEnabled"] = "控制台窗口",
        ["consoleTopmost"] = "控制台置顶",
        ["debugShell"] = "控制台命令",
        ["forwardActivityLog"] = "日志转发",
        ["speedhackBaseSpeed"] = "启动倍速",
        ["speedControlEnabled"] = "变速通道",
        ["windowControlEnabled"] = "窗口置前通道",
        ["audioControlEnabled"] = "音频通道",
        ["crashDumpEnabled"] = "崩溃转储",
        ["crashDumpFullMemory"] = "完整内存转储",
        ["nativeWatchdogEnabled"] = "卡死看门狗",
        ["nativeWatchdogTimeoutSec"] = "看门狗判定时间",
        ["logLevel"] = "日志级别",
        ["debugWaitForDebugger"] = "等待调试器",
        ["debugWaitTimeoutSec"] = "等待调试器超时",
        ["steamBypassEnabled"] = "Steam 绕过",
        ["steamBypassRestartCheck"] = "Steam 重启检查",
        ["steamBypassMatchmaking"] = "Steam 大厅匹配",
        ["steamBypassLobbyQuery"] = "Steam 房间列表",
        ["steamBypassLobbyHasValue"] = "Steam 大厅空值",
        ["steamBypassLobbyMethods"] = "Steam 大厅方法",
        ["steamBypassTaskCtorMode"] = "Steam Task 构造方式",
        ["tapTapBypassEnabled"] = "TapTap 绕过",
        ["tapTapBypassRestartCheck"] = "TapTap 重启检查",
        ["tapTapBypassLogin"] = "TapTap 登录"
    };
}

/// <summary>会话与存档共用的 JSON 选项。中文不转义，方便用户直接看文件。</summary>
public static class ModProfileJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };
}

/// <summary>界面列表用的一行。</summary>
public sealed class ModProfileSummary
{
    public string Name { get; set; } = "";
    public string Note { get; set; } = "";
    public string UpdatedAtUtc { get; set; } = "";
    public bool Active { get; set; }
    public int ModCount { get; set; }
    public int EnabledCount { get; set; }
    public int MissingCount { get; set; }

    /// <summary>和当前磁盘状态是否一致（启用集合 / 配置 / 加载器设置）。</summary>
    public bool Dirty { get; set; }

    public bool IncludesLoader { get; set; }
    public bool IncludesConfigs { get; set; }

    /// <summary>不能套用的原因（空 = 可以套用）。</summary>
    public string Problem { get; set; } = "";
}

/// <summary>套用前的差异预览。</summary>
public sealed class ModProfilePlan
{
    public string Name { get; set; } = "";
    public bool Blocked { get; set; }
    public string BlockReason { get; set; } = "";
    public bool HasChanges { get; set; }
    public int EnableCount { get; set; }
    public int DisableCount { get; set; }
    public int ConfigWriteCount { get; set; }
    public int LoaderChangeCount { get; set; }
    public List<string> Missing { get; set; } = new();
    public List<string> Enable { get; set; } = new();
    public List<string> Disable { get; set; } = new();
    public List<string> Configs { get; set; } = new();
    public List<string> LoaderChanges { get; set; } = new();
    public List<string> Details { get; set; } = new();
}

/// <summary>套用结果。</summary>
public sealed class ModProfileApplyResult
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public List<string> Applied { get; set; } = new();
    public List<string> Failed { get; set; } = new();
    public List<string> Skipped { get; set; } = new();
}
