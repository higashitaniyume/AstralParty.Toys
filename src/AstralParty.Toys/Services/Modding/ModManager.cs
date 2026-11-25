using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace AstralParty.Toys.Services.Modding;

/// <summary>
/// 游戏 Mod 加载器（CesiumLoader：version.dll 薄代理 + CesiumLoader.dll 核心）与 mod 的安装 / 卸载 / 列表管理。
/// 加载器本体是两个原生 DLL：<c>version.dll</c>（Doorstop 式代理，UnityPlayer 导入它时被优先加载，
/// 只负责转发系统 version.dll 的导出）与 <c>CesiumLoader.dll</c>（配置 / 变速 / 日志 / 模组编排；
/// 由代理从**自身所在目录**加载）—— 两者必须在游戏 exe 目录里成对存在。
/// 加上内置 SDK（CesiumLoader.SDK.dll）、内置 mod（见 <see cref="BuiltInModIds"/>）、
/// 各 mod 的 sidecar 元数据（*.json）以及 doorstop_config.json，全部以「内嵌资源」编译进程序集
/// （AstralParty.Toys.ModLoader.*）。
/// 安装 = 按发布包布局把两个 DLL 落到游戏 exe 目录，并创建
/// AstralParty_ModLoader\{sdk,mods,logs} 目录结构 + 写入 doorstop_config.json；
/// 卸载前校验 DLL 哈希，避免误删他人文件。
/// </summary>
public sealed partial class ModManager
{
    public const string LoaderDllName = "version.dll";

    /// <summary>加载器核心（与 <see cref="LoaderDllName"/> 同目录，由代理在后台线程加载）。
    /// 两个文件必须成对安装/更新：只有代理时游戏照常启动但不加载任何模组，代理会在
    /// <c>AstralParty_ModLoader\logs\proxy-*.log</c> 里写清楚原因。</summary>
    public const string CoreDllName = "CesiumLoader.dll";

    public const string LoaderFolderName = "AstralParty_ModLoader";
    public const string ModsFolderName = "mods";
    /// <summary>停用的 mod 搬到这里：加载器只扫 mods，等于彻底不进游戏，DLL 也不会被游戏进程占用。</summary>
    public const string DisabledModsFolderName = "disabled_mods";
    public const string SdkFolderName = "sdk";
    public const string LogsFolderName = "logs";
    public const string ConfigFileName = "doorstop_config.json";
    public const string SdkDllName = "CesiumLoader.SDK.dll";
    public const string SampleModDllName = "ActivityLogMod.dll";

    /// <summary>加载器内置变速的倍率下限 = 1.0：加载器不支持减速，低于 1.0 的请求会被忽略。
    /// 与加载器 speedhack.h 的 kSpeedMin / SDK SpeedHack.MinSpeed 保持一致。</summary>
    public const double MinBaseSpeed = 1.0;

    /// <summary>加载器内置变速的倍率上限，与 speedhack.h 的 kSpeedMax 一致。</summary>
    public const double MaxBaseSpeed = 100.0;

    /// <summary>加载器各类超时（等待调试器 / 看门狗判定）的合法范围，与加载器 config.cpp 的
    /// <c>read_timeout_sec</c> 一致：越界会被加载器静默回退成默认值。</summary>
    public const int MinTimeoutSec = 1;
    public const int MaxTimeoutSec = 3600;

    /// <summary>
    /// 是否是发布包里的模组条目。安装以发布包内容为准：包内 mods\{ModId}\ 下的
    /// 文件全部安装，Toys 侧不再有另一份过滤清单（内置 mod 清单就是从这份包派生出来的）。
    /// </summary>
    private static bool IsPackageModEntry(string path)
    {
        if (!path.StartsWith(PackageModsPrefix, StringComparison.OrdinalIgnoreCase)) return false;
        var relative = path.Substring(PackageModsPrefix.Length);
        var separator = relative.IndexOf('/');
        return separator > 0 && separator < relative.Length - 1;
    }

    private readonly string? _profileDirectoryOverride;

    /// <param name="appDirectory">工具目录；只用于提示信息。</param>
    /// <param name="profileDirectory">配置根目录（游戏目录记忆等），显式指定供测试。</param>
    public ModManager(string appDirectory, string? profileDirectory = null)
    {
        _ = appDirectory;
        _profileDirectoryOverride = profileDirectory is { Length: > 0 } ? Path.GetFullPath(profileDirectory) : null;
    }

    /// <summary>
    /// 数据根目录。没显式指定时跟随 <see cref="ToysPaths.Root"/>：换了数据文件夹本次运行就跟着走。
    /// </summary>
    public string ProfileDirectory =>
        _profileDirectoryOverride ?? SpeedhackManager.ResolveProfileDirectory();

    private string StateFilePath => Path.Combine(ProfileDirectory, ToysPaths.StateFolderName, "modloader-state.json");

    // ============================== 游戏目录 ==============================

    public string? ResolveGameDirectory() => GetStoredGameDirectory() ?? SpeedhackManager.DetectGameDirectory();

    public string? GetStoredGameDirectory()
    {
        try
        {
            if (!File.Exists(StateFilePath)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(StateFilePath));
            var value = document.RootElement.TryGetProperty("gameDirectory", out var element)
                ? element.GetString() : null;
            var normalized = SpeedhackManager.NormalizeDirectoryPath(value);
            return normalized.Length == 0 ? null : normalized;
        }
        catch
        {
            return null;
        }
    }

    public void SaveStoredGameDirectory(string? directory)
    {
        var path = StateFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var normalized = SpeedhackManager.NormalizeDirectoryPath(directory);
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            gameDirectory = normalized.Length == 0 ? null : normalized
        }));
    }

    // ============================== 工具 ==============================

    /// <summary>磁盘上那个 <c>version.dll</c> 到底是不是「本工具装的那一份」——决定界面该不该报冲突。
    ///
    ///   <c>"bundle"</c>    与随程序集内置的加载器逐字节一致（点「安装加载器」装的）
    ///   <c>"installed"</c> 与安装清单 <c>cesium-loader.json</c> 里记录的哈希一致（点「从 GitHub 更新」装的）
    ///   <c>""</c>          两者都不是：来源不明（旧版独立变速器 / 第三方文件 / 玩家手换的），
    ///                       覆盖或卸载前必须让用户确认
    ///
    /// ★ <c>"installed"</c> 这一条是**必需**的，也是"更新过加载器之后被一直误报成其它 version.dll"的根因：
    ///   点「从 GitHub 更新」装的是比本程序内置更新的发布包，磁盘上的文件当然和内置的不同 ——
    ///   只比内置哈希，就会把本工具自己刚装上去的加载器说成"别人的文件"，小白用户看不懂，
    ///   老用户还会因此以为更新失败了。判据换成「内置哈希 **或** 清单哈希」之后，两者都算自己人。</summary>
    private string ClassifyLoaderOrigin(string gameDirectory, string dllPath)
    {
        if (MatchesEmbeddedLoader(dllPath)) return "bundle";
        var manifestHash = CurrentLoaderHash(gameDirectory, ReadRetainedInstallation(gameDirectory));
        return manifestHash.Length > 0 && MatchesHash(dllPath, manifestHash) ? "installed" : "";
    }

    private static bool MatchesEmbeddedLoader(string path)
        => EmbeddedLoaderHash is not null && MatchesHash(path, EmbeddedLoaderHash);

    /// <summary>拆分后的薄代理里一定带这个契约导出名（旧版单 DLL 加载器完全没有这个字符串）。
    /// 用它判断「盘上这个 version.dll 是否需要同目录的加载器核心」。
    ///
    /// 只**静态读文件字节**，绝不用 LoadLibrary + GetProcAddress 去探测：那会把加载器的 DllMain
    /// 跑进 Toys 自己的进程（装 inline hook、起引导线程、开控制台），后果严重。</summary>
    private const string SplitProxyMarker = "CesiumLoaderAbiVersion";

    private static readonly byte[] SplitProxyMarkerBytes = System.Text.Encoding.ASCII.GetBytes(SplitProxyMarker);

    /// <summary>盘上的 version.dll 是否"必须有核心"（= 拆分后的薄代理）。
    /// 读不到文件时按 false 处理：读不到不等于核心缺失，不能误报。</summary>
    private static bool RequiresCoreDll(string dllPath)
    {
        try
        {
            var bytes = File.ReadAllBytes(dllPath);
            // 代理只有 ~40 KB，旧版单 DLL 也就 1 MB；再大就不可能是它，别把整个文件读进内存。
            if (bytes.Length == 0 || bytes.Length > 8 * 1024 * 1024) return false;
            return bytes.AsSpan().IndexOf(SplitProxyMarkerBytes) >= 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 游戏目录里是否还留着加载器（<c>version.dll</c> 或 <c>AstralParty_ModLoader\</c> 任一存在）。
    ///
    /// 用来判断「安装历史里的版本号还算不算已装」：加载器历史记在数据根
    /// （<c>install-history\games\&lt;游戏目录哈希&gt;\current.json</c>），玩家手工把游戏目录里的
    /// 加载器删干净**不会**清掉它。若只看历史，删干净之后再装回内置版本就会被当成
    /// 「比已装的更旧」拦住 —— 那不是降级，是往空目录里重新安装。
    /// </summary>
    private static bool HasLoaderOnDisk(string gameDirectory)
        => File.Exists(Path.Combine(gameDirectory, LoaderDllName))
           || Directory.Exists(Path.Combine(gameDirectory, LoaderFolderName));

    /// <summary>文件 SHA256 是否等于给定的十六进制哈希（读不到文件 / 哈希串非法 → false）。</summary>
    private static bool MatchesHash(string path, string expectedHex)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(stream), Convert.FromHexString(expectedHex));
        }
        catch
        {
            return false;
        }
    }

    private static void WriteAllBytesProtected(string path, byte[] bytes)
    {
        if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
        File.WriteAllBytes(path, bytes);
    }

    public static void OpenInExplorer(string path)
        => SpeedhackManager.OpenInExplorer(path);

    // ============================== mod 目录（启用 / 停用） ==============================

    /// <summary>启用的 mod 目录：<c>AstralParty_ModLoader\mods</c>。</summary>
    public static string ModsRoot(string gameDirectory) => Path.Combine(gameDirectory, LoaderFolderName, ModsFolderName);

    /// <summary>停用的 mod 目录：<c>AstralParty_ModLoader\disabled_mods</c>。</summary>
    public static string DisabledModsRoot(string gameDirectory) => Path.Combine(gameDirectory, LoaderFolderName, DisabledModsFolderName);
}

