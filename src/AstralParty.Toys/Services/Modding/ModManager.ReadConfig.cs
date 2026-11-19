using System.Diagnostics;
using System.Text.Json;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Common;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class ModManager
{
    /// <summary>加载器内置变速的倍率下限 = 1.0：加载器不支持减速，低于 1.0 的请求会被忽略。
    /// 与加载器 speedhack.h 的 kSpeedMin / SDK SpeedHack.MinSpeed 保持一致。</summary>
    public const double MinBaseSpeed = 1.0;

    /// <summary>加载器内置变速的倍率上限，与 speedhack.h 的 kSpeedMax 一致。</summary>
    public const double MaxBaseSpeed = 100.0;

    /// <summary>加载器各类超时（等待调试器 / 看门狗判定）的合法范围，与加载器 config.cpp 的
    /// <c>read_timeout_sec</c> 一致：越界会被加载器静默回退成默认值。</summary>
    public const int MinTimeoutSec = 1;
    public const int MaxTimeoutSec = 3600;

    internal static string LoaderConfigFilePath(string gameDirectory) =>
        Path.Combine(gameDirectory, LoaderFolderName, ConfigFileName);

    /// <summary>日志级别规范化：与加载器 <c>normalize_log_level</c> 同口径（忽略大小写，
    /// <c>warning</c> 归到 <c>Warn</c>，认不出的回退 <c>inherit</c>）。</summary>
    public static string NormalizeLogLevel(string? level)
    {
        switch ((level ?? "").Trim().ToLowerInvariant())
        {
            case "inherit": return "inherit";
            case "debug": return "Debug";
            case "info": return "Info";
            case "warn":
            case "warning": return "Warn";
            case "error": return "Error";
            case "fatal": return "Fatal";
            default: return "inherit";
        }
    }

    internal static string? LocateModConfig(string gameDirectory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || string.IsNullOrWhiteSpace(fileName)) return null;
        var path = ModConfigWritePath(gameDirectory, fileName);
        if (InspectionFiles.FileExists(path)) return path;
        var packaged = PackagedModConfigPath(gameDirectory, fileName);
        return InspectionFiles.FileExists(packaged) ? packaged : null;
    }

    internal static bool TryReadLoaderConfig(string gameDirectory, out LoaderConfig defaults, out bool? steamBypass)
    {
        defaults = new LoaderConfig();
        steamBypass = null;
        if (string.IsNullOrEmpty(gameDirectory)) return false;
        try
        {
            var text = InspectionFiles.ReadText(LoaderConfigFilePath(gameDirectory));
            if (text is null) return false;
            using var doc = JsonDocument.Parse(StripJsonComments(text));
            var root = doc.RootElement;
            if (root.TryGetProperty("enabled", out var e) && e.ValueKind == JsonValueKind.True) defaults.Enabled = true;
            if (root.TryGetProperty("enabled", out e) && e.ValueKind == JsonValueKind.False) defaults.Enabled = false;
            if (root.TryGetProperty("useManagedBootstrap", out var m) && m.ValueKind == JsonValueKind.True) defaults.UseManagedBootstrap = true;
            if (root.TryGetProperty("useManagedBootstrap", out m) && m.ValueKind == JsonValueKind.False) defaults.UseManagedBootstrap = false;
            if (root.TryGetProperty("bootstrapAssembly", out var ba) && ba.ValueKind == JsonValueKind.String) defaults.BootstrapAssembly = ba.GetString()!;
            if (root.TryGetProperty("bootstrapType", out var bt) && bt.ValueKind == JsonValueKind.String) defaults.BootstrapType = bt.GetString()!;
            if (root.TryGetProperty("bootstrapMethod", out var bm) && bm.ValueKind == JsonValueKind.String) defaults.BootstrapMethod = bm.GetString()!;
            if (root.TryGetProperty("gameAssemblyTimeoutSec", out var gt) && gt.ValueKind == JsonValueKind.Number) defaults.GameAssemblyTimeoutSec = gt.GetInt32();
            if (root.TryGetProperty("domainTimeoutSec", out var dt) && dt.ValueKind == JsonValueKind.Number) defaults.DomainTimeoutSec = dt.GetInt32();
            if (root.TryGetProperty("hybridclrTimeoutSec", out var ht) && ht.ValueKind == JsonValueKind.Number) defaults.HybridclrTimeoutSec = ht.GetInt32();
            if (root.TryGetProperty("consoleEnabled", out var ce) && ce.ValueKind == JsonValueKind.True) defaults.ConsoleEnabled = true;
            if (root.TryGetProperty("consoleEnabled", out ce) && ce.ValueKind == JsonValueKind.False) defaults.ConsoleEnabled = false;
            if (root.TryGetProperty("consoleTopmost", out var ct) && ct.ValueKind == JsonValueKind.True) defaults.ConsoleTopmost = true;
            if (root.TryGetProperty("consoleTopmost", out ct) && ct.ValueKind == JsonValueKind.False) defaults.ConsoleTopmost = false;
            if (root.TryGetProperty("debugShell", out var ds) && ds.ValueKind == JsonValueKind.True) defaults.DebugShell = true;
            if (root.TryGetProperty("debugShell", out ds) && ds.ValueKind == JsonValueKind.False) defaults.DebugShell = false;
            if (root.TryGetProperty("forwardActivityLog", out var fa) && fa.ValueKind == JsonValueKind.True) defaults.ForwardActivityLog = true;
            if (root.TryGetProperty("forwardActivityLog", out fa) && fa.ValueKind == JsonValueKind.False) defaults.ForwardActivityLog = false;
            if (root.TryGetProperty("speedhackBaseSpeed", out var sp) && sp.ValueKind == JsonValueKind.Number) defaults.SpeedhackBaseSpeed = sp.GetDouble();
            if (root.TryGetProperty("speedControlEnabled", out var sc) && sc.ValueKind == JsonValueKind.True) defaults.SpeedControlEnabled = true;
            if (root.TryGetProperty("speedControlEnabled", out sc) && sc.ValueKind == JsonValueKind.False) defaults.SpeedControlEnabled = false;
            if (root.TryGetProperty("windowControlEnabled", out var wc) && wc.ValueKind == JsonValueKind.True) defaults.WindowControlEnabled = true;
            if (root.TryGetProperty("windowControlEnabled", out wc) && wc.ValueKind == JsonValueKind.False) defaults.WindowControlEnabled = false;
            if (root.TryGetProperty("audioControlEnabled", out var ac) && ac.ValueKind == JsonValueKind.True) defaults.AudioControlEnabled = true;
            if (root.TryGetProperty("audioControlEnabled", out ac) && ac.ValueKind == JsonValueKind.False) defaults.AudioControlEnabled = false;
            // 排查类开关(加载器原生层): 崩溃转储 / 卡死看门狗 / 日志级别 / 等待调试器
            if (root.TryGetProperty("crashDumpEnabled", out var cde) && cde.ValueKind == JsonValueKind.True) defaults.CrashDumpEnabled = true;
            if (root.TryGetProperty("crashDumpEnabled", out cde) && cde.ValueKind == JsonValueKind.False) defaults.CrashDumpEnabled = false;
            if (root.TryGetProperty("crashDumpFullMemory", out var cdf) && cdf.ValueKind == JsonValueKind.True) defaults.CrashDumpFullMemory = true;
            if (root.TryGetProperty("crashDumpFullMemory", out cdf) && cdf.ValueKind == JsonValueKind.False) defaults.CrashDumpFullMemory = false;
            if (root.TryGetProperty("nativeWatchdogEnabled", out var nwe) && nwe.ValueKind == JsonValueKind.True) defaults.NativeWatchdogEnabled = true;
            if (root.TryGetProperty("nativeWatchdogEnabled", out nwe) && nwe.ValueKind == JsonValueKind.False) defaults.NativeWatchdogEnabled = false;
            if (root.TryGetProperty("nativeWatchdogTimeoutSec", out var nwt) && nwt.ValueKind == JsonValueKind.Number) defaults.NativeWatchdogTimeoutSec = nwt.GetInt32();
            if (root.TryGetProperty("logLevel", out var ll) && ll.ValueKind == JsonValueKind.String) defaults.LogLevel = ll.GetString()!;
            if (root.TryGetProperty("debugWaitForDebugger", out var dwd) && dwd.ValueKind == JsonValueKind.True) defaults.DebugWaitForDebugger = true;
            if (root.TryGetProperty("debugWaitForDebugger", out dwd) && dwd.ValueKind == JsonValueKind.False) defaults.DebugWaitForDebugger = false;
            if (root.TryGetProperty("debugWaitTimeoutSec", out var dwt) && dwt.ValueKind == JsonValueKind.Number) defaults.DebugWaitTimeoutSec = dwt.GetInt32();
            if (root.TryGetProperty("steamBypassEnabled", out var sb) && sb.ValueKind == JsonValueKind.True) defaults.SteamBypassEnabled = true;
            if (root.TryGetProperty("steamBypassEnabled", out sb) && sb.ValueKind == JsonValueKind.False) defaults.SteamBypassEnabled = false;
            if (root.TryGetProperty("steamBypassRestartCheck", out var sr) && sr.ValueKind == JsonValueKind.True) defaults.SteamBypassRestartCheck = true;
            if (root.TryGetProperty("steamBypassRestartCheck", out sr) && sr.ValueKind == JsonValueKind.False) defaults.SteamBypassRestartCheck = false;
            if (root.TryGetProperty("steamBypassMatchmaking", out var sm) && sm.ValueKind == JsonValueKind.True) defaults.SteamBypassMatchmaking = true;
            if (root.TryGetProperty("steamBypassMatchmaking", out sm) && sm.ValueKind == JsonValueKind.False) defaults.SteamBypassMatchmaking = false;
            if (root.TryGetProperty("steamBypassLobbyQuery", out var sq) && sq.ValueKind == JsonValueKind.True) defaults.SteamBypassLobbyQuery = true;
            if (root.TryGetProperty("steamBypassLobbyQuery", out sq) && sq.ValueKind == JsonValueKind.False) defaults.SteamBypassLobbyQuery = false;
            if (root.TryGetProperty("steamBypassLobbyHasValue", out var sh) && sh.ValueKind == JsonValueKind.True) defaults.SteamBypassLobbyHasValue = true;
            if (root.TryGetProperty("steamBypassLobbyHasValue", out sh) && sh.ValueKind == JsonValueKind.False) defaults.SteamBypassLobbyHasValue = false;
            if (root.TryGetProperty("steamBypassLobbyMethods", out var sl) && sl.ValueKind == JsonValueKind.True) defaults.SteamBypassLobbyMethods = true;
            if (root.TryGetProperty("steamBypassLobbyMethods", out sl) && sl.ValueKind == JsonValueKind.False) defaults.SteamBypassLobbyMethods = false;
            if (root.TryGetProperty("steamBypassTaskCtorMode", out var st) && st.ValueKind == JsonValueKind.String) defaults.SteamBypassTaskCtorMode = st.GetString()!;
            if (root.TryGetProperty("tapTapBypassEnabled", out var tbe) && tbe.ValueKind == JsonValueKind.True) defaults.TapTapBypassEnabled = true;
            if (root.TryGetProperty("tapTapBypassEnabled", out tbe) && tbe.ValueKind == JsonValueKind.False) defaults.TapTapBypassEnabled = false;
            if (root.TryGetProperty("tapTapBypassRestartCheck", out var tbr) && tbr.ValueKind == JsonValueKind.True) defaults.TapTapBypassRestartCheck = true;
            if (root.TryGetProperty("tapTapBypassRestartCheck", out tbr) && tbr.ValueKind == JsonValueKind.False) defaults.TapTapBypassRestartCheck = false;
            if (root.TryGetProperty("tapTapBypassLogin", out var tbl) && tbl.ValueKind == JsonValueKind.True) defaults.TapTapBypassLogin = true;
            if (root.TryGetProperty("tapTapBypassLogin", out tbl) && tbl.ValueKind == JsonValueKind.False) defaults.TapTapBypassLogin = false;
            if (root.TryGetProperty("sdkVersion", out var sv) && sv.ValueKind == JsonValueKind.String) defaults.SdkVersion = sv.GetString()!;
            if (root.TryGetProperty("profile", out var pf) && pf.ValueKind == JsonValueKind.String) defaults.Profile = pf.GetString()!;
            if (root.TryGetProperty("profileWindowTitle", out var pwt) && pwt.ValueKind == JsonValueKind.True) defaults.ProfileWindowTitle = true;
            if (root.TryGetProperty("profileWindowTitle", out pwt) && pwt.ValueKind == JsonValueKind.False) defaults.ProfileWindowTitle = false;
            if (sb.ValueKind == JsonValueKind.True || sm.ValueKind == JsonValueKind.True || sq.ValueKind == JsonValueKind.True)
                steamBypass = true;
            else if (sb.ValueKind == JsonValueKind.False && sm.ValueKind == JsonValueKind.False && sq.ValueKind == JsonValueKind.False)
                steamBypass = false;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>这套绕过当前是否**有任何一个**是开着的（首页单选框据此显示"绕过 Steam 启动"）。
    ///
    /// 用"任一为真"而不是只看主开关：只要还有一部分 hook 在起作用，Steam 那边的行为就已经不是原版了
    /// （比如主开关关了、匹配绕过还开着 → 照样不建真大厅），此时首页绝不能显示成「从 Steam 启动」。</summary>
    public static bool IsSteamBypassActive(LoaderConfig config)
        => config.SteamBypassEnabled || config.SteamBypassMatchmaking || config.SteamBypassLobbyQuery;

    /// <summary>这套 TapTap 绕过当前是否有任何一个是开着的。</summary>
    public static bool IsTapTapBypassActive(LoaderConfig config)
        => config.TapTapBypassEnabled || config.TapTapBypassRestartCheck;

    /// <summary>从配置文本里取 sdkVersion（容错：解析失败 / 没有该字段返回空串）。
    ///
    /// ★ 必须自行剥 UTF-8 BOM：本方法有两类调用方 —— File.ReadAllText（会剥 BOM）与
    /// ReadSdkVersionFromConfigBytes（Encoding.UTF8.GetString，**不剥** BOM）。带 BOM 的文本直接交给
    /// JsonDocument.Parse 会抛异常，于是内嵌模板的 sdkVersion 被读成空串，"把旧配置抬到随包 SDK 版本"
    /// 这条升级逻辑就静默失效（新版内置 mod 会被加载器判 SdkVersionTooNew 拒绝加载）。
    /// C++ 加载器侧的 jsonc.h 同样显式剥 BOM（记事本保存 UTF-8 会加 BOM），两边行为保持一致。</summary>
    private static string ParseSdkVersionFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        try
        {
            using var doc = JsonDocument.Parse(StripJsonComments(text.TrimStart('\uFEFF')));
            return doc.RootElement.TryGetProperty("sdkVersion", out var sv) && sv.ValueKind == JsonValueKind.String
                ? sv.GetString() ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>读取当前加载器实际用于 mod 版本校验的 SDK 版本。
    ///
    /// 优先取 <c>doorstop_config.json</c> 的 <c>sdkVersion</c>（加载器真正读取并用于拒绝 mod 的值）；
    /// 配置缺失或没有该字段时回退到 <c>sdk/CesiumLoader.SDK.dll</c> 的文件版本。
    /// 两者都读不到（例如还没安装加载器）返回 false，调用方应跳过检查。</summary>
    public static bool TryReadInstalledSdkVersion(string gameDirectory, out string version)
    {
        version = "";
        try
        {
            if (string.IsNullOrWhiteSpace(gameDirectory)) return false;

            var text = InspectionFiles.ReadText(LoaderConfigFilePath(gameDirectory));
            if (text is not null)
            {
                var fromConfig = ParseSdkVersionFromText(text);
                if (!string.IsNullOrEmpty(fromConfig))
                {
                    version = fromConfig;
                    return true;
                }
            }

            var sdkDll = Path.Combine(gameDirectory, LoaderFolderName, SdkFolderName, SdkDllName);
            if (InspectionFiles.FileExists(sdkDll) &&
                (!OperatingSystem.IsLinux() || new FileInfo(sdkDll).Length is > 0 and <= 64 * 1024 * 1024))
            {
                var normalized = NormalizeSdkVersion(FileVersionInfo.GetVersionInfo(sdkDll).FileVersion);
                if (!string.IsNullOrEmpty(normalized))
                {
                    version = normalized;
                    return true;
                }
            }
        }
        catch
        {
            // 读不到就当未知：不能因为配置损坏阻止所有模组安装。
        }
        return false;
    }

    /// <summary>把 <c>3.2.0.0</c> 这类文件版本规范化成三段；无法解析时保留原值。</summary>
    private static string NormalizeSdkVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var trimmed = value.Trim();
        var parts = trimmed.Split('.');
        if (parts.Length < 3) return trimmed;
        for (var i = 0; i < 3; i++)
            if (!int.TryParse(parts[i], out _)) return trimmed;
        return parts[0] + "." + parts[1] + "." + parts[2];
    }

    /// <summary>剥掉 JSON 里的 // 与 /* */ 注释(加载器配置文件带注释, System.Text.Json 不认)。</summary>
    private static string StripJsonComments(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        var sb = new System.Text.StringBuilder(json.Length);
        var inString = false;
        var i = 0;
        while (i < json.Length)
        {
            var c = json[i];
            if (inString)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < json.Length) { sb.Append(json[i + 1]); i += 2; continue; }
                if (c == '"') inString = false;
                i++;
                continue;
            }
            if (c == '"') { inString = true; sb.Append(c); i++; continue; }
            if (c == '/' && i + 1 < json.Length && json[i + 1] == '/')
            {
                while (i < json.Length && json[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < json.Length && json[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/')) i++;
                i += 2;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>
    /// mod 配置文件名(与 mod 的 DLL 同目录)。SDK 的 <c>ModConfig</c> 就是按
    /// <c>modDirectory\config.json</c> 落盘的, 所以这里必须同名同目录 —— 否则 mod 读的
    /// 和 Toys 写的是两个文件, 界面上改了配置游戏里根本不生效。
    /// </summary>
    private const string ModConfigFileName = "config.json";

    /// <summary>mod 玩家配置的落盘位置(<c>configs\{ModId}.json</c>; 不要求已存在)。
    /// 与加载器 SDK 的 <c>ModConfig.ResolvePath</c> 一致: configs 下同名文件存在时 mod 只读它。</summary>
    private static string ModConfigWritePath(string gameDirectory, string fileName)
    {
        var modsDir = ModsRoot(gameDirectory);
        var (_, directoryName, _) = ResolveModPath(modsDir, fileName);
        var modId = directoryName ?? Path.GetFileNameWithoutExtension(fileName);
        return Path.Combine(gameDirectory, LoaderFolderName, "configs", modId + ".json");
    }

    /// <summary>模组目录里那份 config.json(随包默认值; 旧安装的玩家配置也可能还停在这里), 仅用于读取兼容。</summary>
    private static string PackagedModConfigPath(string gameDirectory, string fileName)
    {
        var modsDir = ModsRoot(gameDirectory);
        var (_, directoryName, root) = ResolveModPath(modsDir, fileName);
        var folder = directoryName ?? Path.GetFileNameWithoutExtension(fileName);
        return Path.Combine(root, folder, ModConfigFileName);
    }

    /// <summary>按文件名定位 mod（DLL 路径 + 文件夹名 + 所在根目录）。
    /// 新布局: {root}\{FolderName}\{FolderName}.dll; 旧布局: {root}\{FileName}.dll。
    /// 先找 <c>mods</c>（启用的），再找同级的 <c>disabled_mods</c>（停用的）。</summary>
    private static (string? Dll, string? DirectoryName, string Root) ResolveModPath(string modsDir, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return (null, null, modsDir);
        var loaderRoot = Path.GetDirectoryName(modsDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                         ?? modsDir;
        var stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (var root in new[] { modsDir, Path.Combine(loaderRoot, DisabledModsFolderName) })
        {
            // 新布局优先
            var inFolder = Path.Combine(root, stem, fileName);
            if (InspectionFiles.FileExists(inFolder)) return (inFolder, stem, root);
            // 旧布局平铺
            var flat = Path.Combine(root, fileName);
            if (InspectionFiles.FileExists(flat)) return (flat, null, root);
        }
        return (null, null, modsDir);
    }

}
