using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace AstralParty.Toys.Services.Speedhack;

/// <summary>
/// 游戏变速器（speedhack-rs version.dll 代理）的安装 / 卸载 / 配置管理。
/// version.dll 与 speedhack_config.json 模板以「内嵌资源」编译进程序集（AstralParty.Toys.SpeedhackTools.*），
/// 安装 = 把资源写为游戏 exe 目录下的 version.dll + speedhack_config.json，
/// 利用 Windows DLL 搜索顺序让游戏进程加载变速器；卸载前校验 DLL 哈希，避免误删他人文件。
/// </summary>
public sealed partial class SpeedhackManager
{
    public const string DllName = "version.dll";
    public const string ConfigName = "speedhack_config.json";
    private const string ResourcePrefix = "AstralParty.Toys.SpeedhackTools.";

    /// <summary>
    /// 游戏主程序的通配匹配：国服 <c>AstralParty_CN.exe</c>、国际服 <c>AstralParty.exe</c>、
    /// TapTap 端或其它渠道的 <c>AstralParty*.exe</c> 都认，不再写死那两个确切名字。
    /// （加载器 / version.dll 是按 exe 旁边的位置加载的，跟 exe 具体叫什么无关。）
    /// </summary>
    public const string GameExeSearchPattern = "AstralParty*.exe";

    private static readonly Assembly Assembly = typeof(SpeedhackManager).Assembly;

    private static readonly byte[]? EmbeddedDll = ReadEmbeddedResource(DllName);
    private static readonly byte[]? EmbeddedConfigTemplate = ReadEmbeddedResource(ConfigName);
    private static readonly string? EmbeddedDllHash = EmbeddedDll is null
        ? null
        : Convert.ToHexString(SHA256.HashData(EmbeddedDll));

    private static byte[]? ReadEmbeddedResource(string name)
    {
        using var stream = Assembly.GetManifestResourceStream(ResourcePrefix + name);
        if (stream is null) return null;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>数据文件夹名（用户「文档」下的默认位置）。</summary>
    public const string DataFolderName = ToysPaths.DefaultFolderName;

    private readonly string _documentsDirectory;
    private readonly string? _profileDirectoryOverride;
    private readonly string? _localDataRootOverride;
    private readonly string? _documentsDirectoryOverride;

    /// <param name="appDirectory">工具目录；只用于提示信息，配置不跟 exe 走。</param>
    /// <param name="profileDirectory">显式指定数据根目录（测试用）。</param>
    /// <param name="localDataRoot">引导指针所在的本地目录（测试用）。</param>
    /// <param name="documentsDirectory">「文档」数据目录覆盖（测试用）。</param>
    public SpeedhackManager(string appDirectory, string? profileDirectory = null,
        string? localDataRoot = null, string? documentsDirectory = null)
    {
        _ = appDirectory;
        _documentsDirectory = documentsDirectory ?? ToysPaths.DefaultRoot;
        _profileDirectoryOverride = profileDirectory is { Length: > 0 } ? Path.GetFullPath(profileDirectory) : null;
        _localDataRootOverride = localDataRoot;
        _documentsDirectoryOverride = documentsDirectory;
    }

    /// <summary>
    /// 数据根目录。没显式指定时跟随 <see cref="ToysPaths.Root"/>：用户在设置里换了数据文件夹，
    /// 本次运行就能跟着走，不必重启。
    /// </summary>
    public string ProfileDirectory =>
        _profileDirectoryOverride ?? (_localDataRootOverride is null && _documentsDirectoryOverride is null
            ? ToysPaths.Root
            : ResolveProfileDirectory(_localDataRootOverride, _documentsDirectoryOverride));

    /// <summary>数据文件夹位置的人话说明（回退到本地 AppData 等情况会写明原因）。</summary>
    private string ProfileLocationNote =>
        _profileDirectoryOverride is null ? ToysPaths.RootNote : "";

    /// <summary>默认数据根：用户「文档」下的 AstralParty.Toys。</summary>
    public static string ResolveDocumentsDataDirectory() => ToysPaths.DefaultRoot;

    /// <summary>旧配置位置 %AppData%\AstralParty.Toys（含旧名 AstralParty.ReplayTool 的一次性迁移）。</summary>
    public static string ResolveAppDataDirectory()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var current = Path.Combine(appData, "AstralParty.Toys");
        var legacy = Path.Combine(appData, "AstralParty.ReplayTool");
        if (!Directory.Exists(current) && Directory.Exists(legacy))
        {
            try
            {
                Directory.Move(legacy, current);
            }
            catch
            {
                return legacy;
            }
        }
        return current;
    }

    /// <summary>
    /// 数据根目录：默认「文档\AstralParty.Toys」，用户可在设置里改到任意位置（位置记在 %LOCALAPPDATA% 的引导指针里）。
    /// 两个 override 参数仅供测试注入。
    /// </summary>
    public static string ResolveProfileDirectory(string? localDataRootOverride = null,
        string? documentsDirectoryOverride = null)
    {
        // 生产路径直接用已经解析好的根（用户改了数据文件夹后这里立刻跟着变）；
        // 测试注入时走一遍完整解析。
        if (localDataRootOverride is null && documentsDirectoryOverride is null) return ToysPaths.Root;
        var documents = documentsDirectoryOverride ?? ToysPaths.DefaultRoot;
        return ToysPaths.ResolveRoot(localDataRootOverride, documents);
    }

    public static bool HasEmbeddedResources => EmbeddedDll is not null && EmbeddedConfigTemplate is not null;
    public static bool HasEmbeddedDll => EmbeddedDll is not null;
    public static bool HasEmbeddedConfigTemplate => EmbeddedConfigTemplate is not null;

    /// <summary>可编辑的主配置（数据根下的 speedhack\speedhack_config.json）。</summary>
    public string ProfileConfigPath => Path.Combine(ProfileDirectory, ToysPaths.SpeedhackFolderName, ConfigName);

    private static bool SamePath(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left).TrimEnd('\\'),
                Path.GetFullPath(right).TrimEnd('\\'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string StateFilePath => Path.Combine(ProfileDirectory, ToysPaths.StateFolderName, "speedhack-state.json");

    // ============================== 状态 ==============================

    public SpeedhackStatus GetStatus()
    {
        var status = new SpeedhackStatus();
        try
        {
            status.BundleDllPresent = HasEmbeddedDll;
            status.TemplateConfigPresent = HasEmbeddedConfigTemplate;
            status.ProfileConfigPath = ProfileConfigPath;
            status.ProfileConfigPresent = File.Exists(ProfileConfigPath);
            status.ProfileDirectory = ProfileDirectory;
            status.ProfileLocationDocuments = SamePath(ProfileDirectory, _documentsDirectory);
            status.ProfileLocationNote = ProfileLocationNote.Length > 0
                ? ProfileLocationNote
                : $"数据文件夹：{ProfileDirectory}";

            var directory = GetStoredGameDirectory();
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                directory = DetectGameDirectory() ?? "";
                if (!string.IsNullOrWhiteSpace(directory)) status.AutoDetectedDirectory = directory;
            }
            status.GameDirectory = directory;
            status.GameExeFound = !string.IsNullOrEmpty(directory) && ContainsGameExe(directory) is not null;

            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                var dllPath = Path.Combine(directory, DllName);
                var configPath = Path.Combine(directory, ConfigName);
                status.DllPath = dllPath;
                status.ConfigPath = configPath;
                status.DllPresent = File.Exists(dllPath);
                status.ConfigPresent = File.Exists(configPath);
                if (status.DllPresent)
                {
                    status.DllMatchesBundle = MatchesEmbeddedDll(dllPath);
                    status.Installed = true;
                }
            }

            status.GameRunning = IsGameRunning();
            status.BundleHash = EmbeddedDllHash ?? "";
            status.Message = BuildStatusMessage(status);
        }
        catch (Exception ex)
        {
            status.Message = $"读取状态失败：{ex.Message}";
        }
        return status;
    }

    private string BuildStatusMessage(SpeedhackStatus status)
    {
        if (!HasEmbeddedResources)
            return "程序集缺少内置变速器资源（SpeedhackTools.version.dll / speedhack_config.json），请重新编译发布版本。";
        if (string.IsNullOrEmpty(status.GameDirectory))
            return "尚未找到游戏目录：可点击「选择游戏目录」手动指定安装位置。";
        if (!Directory.Exists(status.GameDirectory))
            return $"游戏目录不存在：{status.GameDirectory}";
        if (status.GameRunning)
            return "检测到游戏正在运行——安装或卸载前请先退出游戏。";
        if (status.Installed)
            return status.DllMatchesBundle
                ? $"已安装（{status.GameDirectory}），配置修改后重启游戏生效。"
                : "游戏目录存在其它 version.dll（与内置文件不同）——覆盖或卸载前请先确认来源。";
        return "尚未安装：点击「安装变速器」把文件复制到游戏目录。";
    }

    public static bool IsGameRunning()
    {
        foreach (var name in new[] { "AstralParty_CN", "AstralParty" })
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0) return true;
            }
            catch
            {
                // 进程枚举失败按未运行处理
            }
        }
        return false;
    }

    // ============================== 安装 / 卸载 ==============================

    /// <summary>
    /// 游戏正在运行时安装会发生什么。安装本身不拦，玩家自己决定：
    /// version.dll 由游戏启动时加载，所以本次写入不会立即生效；而正在运行的游戏通常占用着同名文件，覆盖可能直接失败。
    /// </summary>
    public static string DescribeRunningInstall() =>
        "游戏正在运行时也能装：version.dll 是游戏启动时加载的，本次写进去要重启游戏才会生效；" +
        "而且正在运行的游戏往往占用着 version.dll，覆盖会失败并提示文件被占用——那样就先完全退出游戏再装。";

    /// <summary>游戏正在运行时卸载会发生什么。</summary>
    public static string DescribeRunningUninstall() =>
        "游戏正在运行时也能卸：version.dll 已被游戏加载占用，删除通常会失败并提示文件被占用；" +
        "即使删除成功，当前这局也已经加载了变速器，要重启游戏才会恢复正常速度。";

    /// <summary>游戏正在运行时同步配置会发生什么。</summary>
    public static string DescribeRunningPushConfig() =>
        "游戏正在运行时也能同步：配置会写进游戏目录，已加载的变速器需要按重载热键（默认 Ctrl+Shift+R）重新读取，否则重启游戏后生效。";

    /// <summary>安装完成后的提示语（游戏仍在运行时说明本次不会立刻生效）。</summary>
    public static string DescribeInstalled() => IsGameRunning()
        ? "变速器文件已写入游戏目录。游戏正在运行，本次不会立刻生效——重启游戏后才会加载变速器。"
        : "变速器已安装到游戏目录。进入游戏后按配置的快捷键即可变速（需关闭垂直同步）。";

    /// <summary>把内嵌文件写入游戏目录。version.dll 目标已存在且不是内置文件时，必须 overwriteDll 才会覆盖。</summary>
    public void Install(string gameDirectory, bool overwriteDll)
    {
        if (!HasEmbeddedResources)
            throw new InvalidOperationException("程序集缺少内置变速器资源（version.dll / speedhack_config.json），无法安装。");
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("请先选择游戏目录。");
        if (!Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"游戏目录不存在：{gameDirectory}");
        if (string.IsNullOrEmpty(ContainsGameExe(gameDirectory)))
            throw new InvalidOperationException("所选目录里没有找到游戏主程序（AstralParty*.exe），确认这是游戏 exe 所在的目录？");

        var targetDll = Path.Combine(gameDirectory, DllName);
        if (File.Exists(targetDll) && !MatchesEmbeddedDll(targetDll) && !overwriteDll)
            throw new InvalidOperationException(
                "游戏目录已存在一个与内置不同的 version.dll（可能是其它工具的，如 CesiumLoader Mod 加载器）——如确定要覆盖，请勾选「允许覆盖其它 version.dll」。");

        // 互斥检测: 已安装 CesiumLoader Mod 加载器? 两者的 version.dll 同名, 会互相覆盖。
        if (HasModLoaderInstalled(gameDirectory))
            throw new InvalidOperationException(
                "检测到已安装 CesiumLoader Mod 加载器（AstralParty_ModLoader\\doorstop_config.json 存在）。" +
                "加载器与变速器共用 version.dll，不能同时安装：装变速器会覆盖加载器，所有 mod 将失效。" +
                "提示：CesiumLoader 已内置变速引擎——请勿安装本变速器，直接改加载器的 " +
                "AstralParty_ModLoader\\doorstop_config.json 里的 speedhackBaseSpeed 即可变速。");

        try
        {
            WriteAllBytesProtected(targetDll, EmbeddedDll!);
            WriteAllBytesProtected(Path.Combine(gameDirectory, ConfigName), EnsureProfileConfigBytes());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"写入游戏目录失败：{ex.Message}。" +
                (IsGameRunning() ? "游戏正在运行时 version.dll 会被占用，无法覆盖——请完全退出游戏后再安装。" : ""), ex);
        }

        SaveStoredGameDirectory(gameDirectory);
    }

    /// <summary>互斥检测: 游戏目录是否已安装 CesiumLoader Mod 加载器(以 AstralParty_ModLoader\doorstop_config.json 为标志)。</summary>
    public static bool HasModLoaderInstalled(string gameDirectory)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory)) return false;
            return File.Exists(Path.Combine(gameDirectory, ModManager.LoaderFolderName, ModManager.ConfigFileName));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>从游戏目录删除 version.dll 与 speedhack_config.json。DLL 与内置不一致时默认拒绝，force 才删除。</summary>
    public UninstallResult Uninstall(string gameDirectory, bool force)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"游戏目录不存在：{gameDirectory ?? "(未选择)"}");

        var dllPath = Path.Combine(gameDirectory, DllName);
        var configPath = Path.Combine(gameDirectory, ConfigName);
        var dllExists = File.Exists(dllPath);
        var configExists = File.Exists(configPath);

        if (!dllExists && !configExists)
            return new UninstallResult { Message = "游戏目录中没有变速器文件，无需卸载。" };

        if (dllExists && !MatchesEmbeddedDll(dllPath) && !force)
            throw new InvalidOperationException(
                "该 version.dll 与内置文件不同，可能不是本工具安装的——未删除任何文件。如确认要删除请勾选「强制卸载」。");

        var result = new UninstallResult();
        if (dllExists)
        {
            try
            {
                File.Delete(dllPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException(
                    $"删除 version.dll 失败：{ex.Message}。" +
                    (IsGameRunning()
                        ? "游戏正在运行时该文件已被加载占用，请完全退出游戏后再卸载（本次没有改动任何文件）。"
                        : "请检查文件权限后重试（本次没有改动任何文件）。"), ex);
            }
            result.RemovedDll = true;
        }
        if (configExists)
        {
            try
            {
                File.Delete(configPath);
                result.RemovedConfig = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Message = $"version.dll 已删除，但 speedhack_config.json 删除失败：{ex.Message}";
                return result;
            }
        }

        result.Message = IsGameRunning()
            ? "变速器已卸载；当前正在运行的游戏进程仍加载着变速器，重启游戏后恢复正常速度。"
            : "变速器已卸载（游戏恢复正常速度）。";
        return result;
    }

    // ============================== 配置读写 ==============================

    /// <summary>主配置不存在时，从内嵌模板（或默认 schema）生成一份。</summary>
    public string EnsureProfileConfig()
    {
        var directory = Path.GetDirectoryName(ProfileConfigPath)!;
        Directory.CreateDirectory(directory);
        if (!File.Exists(ProfileConfigPath))
        {
            var template = EmbeddedConfigTemplate ?? WriteConfigToBytes(new SpeedhackConfig());
            File.WriteAllBytes(ProfileConfigPath, template);
        }
        return ProfileConfigPath;
    }

    /// <summary>把主配置恢复为内嵌模板内容（已安装时需再调用 PushConfigToGame 同步）。</summary>
    public void ResetProfileToTemplate()
    {
        if (EmbeddedConfigTemplate is null)
            throw new InvalidOperationException("程序集缺少内置配置模板资源，无法恢复默认。");
        var directory = Path.GetDirectoryName(ProfileConfigPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(ProfileConfigPath, EmbeddedConfigTemplate);
    }

    public SpeedhackConfig LoadProfileConfig()
    {
        var path = EnsureProfileConfig();
        return ReadConfig(path);
    }

    public void SaveProfileConfig(SpeedhackConfig config)
    {
        var path = EnsureProfileConfig();
        WriteConfig(path, config);
    }

    /// <summary>把主配置同步到游戏目录（仅当已安装）。</summary>
    public void PushConfigToGame(string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory)) return;
        if (!File.Exists(Path.Combine(gameDirectory, DllName))) return;
        WriteAllBytesProtected(Path.Combine(gameDirectory, ConfigName), EnsureProfileConfigBytes());
    }

    public SpeedhackConfig ReadConfig(string path)
    {
        if (!File.Exists(path)) return new SpeedhackConfig();
        try
        {
            return JsonSerializer.Deserialize<SpeedhackConfig>(File.ReadAllText(path), JsonOptions) ?? new SpeedhackConfig();
        }
        catch (JsonException)
        {
            throw new InvalidDataException("配置文件不是有效的 JSON，请先用「用编辑器打开」修复，或点「恢复默认」。");
        }
    }

    public void WriteConfig(string path, SpeedhackConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, WriteConfigToBytes(config));
    }

    private static byte[] WriteConfigToBytes(SpeedhackConfig config)
    {
        var json = JsonSerializer.Serialize(config, JsonOptions);
        return System.Text.Encoding.UTF8.GetBytes(json + Environment.NewLine);
    }

    private byte[] EnsureProfileConfigBytes()
    {
        EnsureProfileConfig();
        return File.ReadAllBytes(ProfileConfigPath);
    }

    private static void WriteAllBytesProtected(string path, byte[] bytes)
    {
        if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
        File.WriteAllBytes(path, bytes);
    }

    // ============================== 工具 ==============================

    private static bool MatchesEmbeddedDll(string path)
    {
        if (EmbeddedDllHash is null) return false;
        try
        {
            using var stream = File.OpenRead(path);
            return CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(stream), Convert.FromHexString(EmbeddedDllHash));
        }
        catch
        {
            return false;
        }
    }

    public static void OpenInExplorer(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            var argument = File.Exists(path)
                ? $"/select,\"{path}\""
                : $"\"{path}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", argument) { UseShellExecute = true });
        }
        catch
        {
            // 打开失败静默，不影响主流程
        }
    }

    public static void OpenWithDefaultEditor(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch
        {
            // 打不开编辑器时静默
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };
}

// ============================== 配置模型（speedhack_config.json schema） ==============================