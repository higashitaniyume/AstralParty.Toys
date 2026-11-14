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
/// 游戏 Mod 加载器（CesiumLoader version.dll Doorstop 式代理）与 mod 的安装 / 卸载 / 列表管理。
/// 加载器本体 version.dll（Doorstop 式代理，UnityPlayer 导入 version.dll 时被优先加载）与
/// 内置 SDK（CesiumLoader.SDK.dll）、内置 mod（见 <see cref="BuiltInModIds"/>：行为日志 / 自由相机 / 变速）、
/// 各 mod 的 sidecar 元数据（*.json）以及 doorstop_config.json 以「内嵌资源」编译进程序集
/// （AstralParty.Toys.ModLoader.*）。
/// 安装 = 把 version.dll 写为游戏 exe 目录下的 version.dll，并创建
/// AstralParty_ModLoader\{sdk,mods,logs} 目录结构 + 写入 doorstop_config.json；
/// 卸载前校验 DLL 哈希，避免误删他人文件。
/// </summary>
public sealed partial class ModManager
{
    public const string LoaderDllName = "version.dll";
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

    /// <summary>
    /// 随程序集内置的 mod（ModId，顺序即安装顺序）。资源名 = {ModId}.dll / {ModId}.json。
    ///
    /// 从按 BuiltInMods.props 公开分发清单编入的资源中枚举，
    /// 内嵌安装、发布包安装与 CI 同步均遵循这份清单。
    /// </summary>
    public static readonly IReadOnlyList<string> BuiltInModIds = DiscoverBuiltInModIds();

    /// <summary>扫内嵌资源名, 认出所有 "<c>*Mod.dll</c>"（与公开分发清单中的资源一致）。</summary>
    private static string[] DiscoverBuiltInModIds()
    {
        try
        {
            var ids = new List<string>();
            foreach (var name in typeof(ModManager).Assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal)) continue;
                var tail = name.Substring(ResourcePrefix.Length);
                if (!tail.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
                var id = tail.Substring(0, tail.Length - 4);
                // 以 "Mod" 结尾 = 内置 mod; 这样自动排除 version.dll / CesiumLoader.SDK.dll 等非 mod 资源
                if (!id.EndsWith("Mod", StringComparison.Ordinal)) continue;
                if (!ids.Contains(id)) ids.Add(id);
            }
            ids.Sort(StringComparer.Ordinal);   // 顺序稳定, 便于对比与测试
            return ids.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private const string ResourcePrefix = "AstralParty.Toys.ModLoader.";

    /// <summary>发布包（cesium-loader.zip）内 mod 条目的前缀；zip 内路径统一用正斜杠。</summary>
    private const string PackageModsPrefix = "AstralParty_ModLoader/mods/";

    /// <summary>
    /// 是否是发布包里的模组条目。安装以发布包内容为准：包内 mods\{ModId}\ 下的
    /// 文件全部安装，不再按 Toys 的公开分发清单过滤（清单只决定随程序内置哪些 mod）。
    /// </summary>
    private static bool IsPackageModEntry(string path)
    {
        if (!path.StartsWith(PackageModsPrefix, StringComparison.OrdinalIgnoreCase)) return false;
        var relative = path.Substring(PackageModsPrefix.Length);
        var separator = relative.IndexOf('/');
        return separator > 0 && separator < relative.Length - 1;
    }

    /// <summary>内嵌加载器版本号；来源：构建时下载的 cesium-loader.json 里 version 字段（CI 自动更新），本地开发构建为 dev-local。</summary>
    private static readonly string EmbeddedVersion = ReadEmbeddedVersion();

    private static string ReadEmbeddedVersion()
    {
        try
        {
            using var stream = typeof(ModManager).Assembly.GetManifestResourceStream(ResourcePrefix + "loader-version.json");
            if (stream is null) return "";
            using var reader = new StreamReader(stream);
            var raw = reader.ReadToEnd().Trim().Trim('"');
            return string.IsNullOrWhiteSpace(raw) || raw == "unknown" ? "" : raw;
        }
        catch
        {
            return "";
        }
    }

    private static readonly Assembly Assembly = typeof(ModManager).Assembly;

    private static readonly byte[]? EmbeddedLoaderDll = ReadEmbeddedResource(LoaderDllName);
    private static readonly byte[]? EmbeddedConfig = ReadEmbeddedResource(ConfigFileName);
    private static readonly byte[]? EmbeddedSdkDll = ReadEmbeddedResource(SdkDllName);

    /// <summary>内置 mod 的内嵌资源（DLL + 可选 sidecar）。资源缺失的 mod 会被跳过，不影响其它 mod。</summary>
    private static readonly IReadOnlyList<BuiltInModResource> EmbeddedBuiltInMods = ReadBuiltInMods();

    /// <summary>内置 doorstop_config.json 模板里声明的 SDK 版本（加载器用它校验 mod 声明的 SdkVersion）。</summary>
    private static readonly string EmbeddedSdkVersion = ReadSdkVersionFromConfigBytes(EmbeddedConfig);

    private static readonly string? EmbeddedLoaderHash = EmbeddedLoaderDll is null
        ? null
        : Convert.ToHexString(SHA256.HashData(EmbeddedLoaderDll));

    /// <summary>一个内置 mod 的内嵌资源：DLL + 可选 sidecar + 可选数据文件（mods\{ModId}\data\*）。</summary>
    private readonly record struct BuiltInModResource(
        string Id,
        byte[] Dll,
        byte[]? Sidecar,
        IReadOnlyDictionary<string, byte[]> Data);

    private static BuiltInModResource[] ReadBuiltInMods()
    {
        var list = new List<BuiltInModResource>();
        string[] resources;
        try { resources = Assembly.GetManifestResourceNames(); }
        catch { resources = Array.Empty<string>(); }

        foreach (var id in BuiltInModIds)
        {
            var dll = ReadEmbeddedResource(id + ".dll");
            if (dll is null) continue;   // 该 mod 未随程序集分发 → 跳过
            list.Add(new BuiltInModResource(id, dll, ReadEmbeddedResource(id + ".json"), ReadEmbeddedData(id, resources)));
        }
        return list.ToArray();
    }

    /// <summary>读取某个 mod 的数据包资源（资源名 data.{ModId}.{文件名}）。</summary>
    private static IReadOnlyDictionary<string, byte[]> ReadEmbeddedData(string id, string[] resourceNames)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var prefix = ResourcePrefix + "data." + id + ".";
        foreach (var name in resourceNames)
        {
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var fileName = name.Substring(prefix.Length);
            if (fileName.Length == 0) continue;
            var bytes = ReadEmbeddedResource("data." + id + "." + fileName);
            if (bytes is not null) map[fileName] = bytes;
        }
        return map;
    }

    private static byte[]? ReadEmbeddedResource(string name)
    {
        using var stream = Assembly.GetManifestResourceStream(ResourcePrefix + name);
        if (stream is null) return null;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
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

    public static bool HasEmbeddedLoader => EmbeddedLoaderDll is not null;
    public static bool HasEmbeddedConfig => EmbeddedConfig is not null;
    public static bool HasEmbeddedSdk => EmbeddedSdkDll is not null;
    /// <summary>程序集里是否带了内置 mod（至少一个）。</summary>
    public static bool HasEmbeddedBuiltInMods => EmbeddedBuiltInMods.Count > 0;
    /// <summary>内置 mod 里是否包含行为日志（ActivityLogMod）——兼容旧判定。</summary>
    public static bool HasEmbeddedSampleMod => EmbeddedBuiltInMods.Any(m => m.Id == SampleModDllName.Replace(".dll", ""));
    /// <summary>程序集里实际带上的内置 mod id（界面/诊断显示用）。</summary>
    public static IReadOnlyList<string> EmbeddedBuiltInModIdList => EmbeddedBuiltInMods.Select(m => m.Id).ToArray();

    /// <summary>内置配置模板声明的 SDK 版本（界面/测试核对用；空 = 模板缺失或没写该字段）。</summary>
    public static string BundleSdkVersion => EmbeddedSdkVersion;

    /// <summary>随程序集内置的加载器版本号（界面显示"内置版本"用；空 = 本地开发构建没内嵌发布包）。</summary>
    public static string BundleLoaderVersion => EmbeddedVersion;

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
            return string.IsNullOrWhiteSpace(value) ? null : value;
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
        File.WriteAllText(path, JsonSerializer.Serialize(new { gameDirectory = directory }));
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

    /// <summary>
    /// 这个 mod 的内容该写回哪个根目录：已经在 <c>disabled_mods</c> 里的 mod 保持原位，
    /// 更新加载器 / 重装内置模组 / 重新导入都不能把它悄悄搬回 <c>mods</c>（否则停用就白点了）。
    /// </summary>
    private static string ResolveModRoot(string loaderRoot, string folderName)
    {
        if (!string.IsNullOrEmpty(folderName) && Directory.Exists(Path.Combine(loaderRoot, DisabledModsFolderName, folderName)))
            return Path.Combine(loaderRoot, DisabledModsFolderName);
        return Path.Combine(loaderRoot, ModsFolderName);
    }
}

