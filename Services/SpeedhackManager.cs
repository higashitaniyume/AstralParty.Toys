using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace AstralParty.Toys.Services;

/// <summary>
/// 游戏变速器（speedhack-rs version.dll 代理）的安装 / 卸载 / 配置管理。
/// version.dll 与 speedhack_config.json 模板以「内嵌资源」编译进程序集（AstralParty.Toys.SpeedhackTools.*），
/// 安装 = 把资源写为游戏 exe 目录下的 version.dll + speedhack_config.json，
/// 利用 Windows DLL 搜索顺序让游戏进程加载变速器；卸载前校验 DLL 哈希，避免误删他人文件。
/// </summary>
public sealed class SpeedhackManager
{
    public const string DllName = "version.dll";
    public const string ConfigName = "speedhack_config.json";
    private const string ResourcePrefix = "AstralParty.Toys.SpeedhackTools.";

    private static readonly string[] GameExeNames = ["astralparty.exe", "astralparty_cn.exe"];

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

    private readonly string _profileDirectory;

    public SpeedhackManager(string appDirectory, string? profileDirectory = null)
    {
        _ = appDirectory; // 保留兼容调用签名；配置不应依赖应用或单文件解包目录
        _profileDirectory = profileDirectory ?? ResolveProfileDirectory();
    }

    private static string ResolveProfileDirectory()
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

    public static bool HasEmbeddedResources => EmbeddedDll is not null && EmbeddedConfigTemplate is not null;
    public static bool HasEmbeddedDll => EmbeddedDll is not null;
    public static bool HasEmbeddedConfigTemplate => EmbeddedConfigTemplate is not null;

    /// <summary>可编辑的主配置，位于 %AppData%\AstralParty.Toys。</summary>
    public string ProfileConfigPath => Path.Combine(_profileDirectory, "speedhack", ConfigName);

    private string StateFilePath => Path.Combine(_profileDirectory, "speedhack-state.json");

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

    /// <summary>把内嵌文件写入游戏目录。version.dll 目标已存在且不是内置文件时，必须 overwriteDll 才会覆盖。</summary>
    public void Install(string gameDirectory, bool overwriteDll)
    {
        if (!HasEmbeddedResources)
            throw new InvalidOperationException("程序集缺少内置变速器资源（version.dll / speedhack_config.json），无法安装。");
        if (string.IsNullOrWhiteSpace(gameDirectory))
            throw new ArgumentException("请先选择游戏目录。");
        if (!Directory.Exists(gameDirectory))
            throw new DirectoryNotFoundException($"游戏目录不存在：{gameDirectory}");
        if (IsGameRunning())
            throw new InvalidOperationException("游戏正在运行，请先退出游戏再安装。");
        if (string.IsNullOrEmpty(ContainsGameExe(gameDirectory)))
            throw new InvalidOperationException("所选目录里没有找到 AstralParty.exe / AstralParty_CN.exe，确认这是游戏 exe 所在的目录？");

        var targetDll = Path.Combine(gameDirectory, DllName);
        if (File.Exists(targetDll) && !MatchesEmbeddedDll(targetDll) && !overwriteDll)
            throw new InvalidOperationException(
                "游戏目录已存在一个与内置不同的 version.dll（可能是其它工具的）——如确定要覆盖，请勾选「允许覆盖其它 version.dll」。");

        WriteAllBytesProtected(targetDll, EmbeddedDll!);
        WriteAllBytesProtected(Path.Combine(gameDirectory, ConfigName), EnsureProfileConfigBytes());
        SaveStoredGameDirectory(gameDirectory);
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
            File.Delete(dllPath);
            result.RemovedDll = true;
        }
        if (configExists)
        {
            File.Delete(configPath);
            result.RemovedConfig = true;
        }
        result.Message = "变速器已卸载（游戏恢复正常速度）。";
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

    // ============================== 游戏目录定位 ==============================

    /// <summary>先取上次手动选择的目录，没有再自动探测。</summary>
    public string? ResolveGameDirectory() => GetStoredGameDirectory() ?? DetectGameDirectory();

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
        Directory.CreateDirectory(_profileDirectory);
        File.WriteAllText(StateFilePath, JsonSerializer.Serialize(new { gameDirectory = directory }));
    }

    /// <summary>通过 Steam 注册表 + libraryfolders.vdf 找到游戏 exe 所在目录（含启动器子目录）。</summary>
    public static string? DetectGameDirectory()
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var steamBase in SteamBases())
        {
            var common = Path.Combine(steamBase, "steamapps", "common");
            if (!Directory.Exists(common)) continue;
            foreach (var appDir in SafeEnumerateDirectories(common))
            {
                var exe = ContainsGameExe(appDir);
                if (exe is not null) found.Add(Path.GetDirectoryName(exe)!);
                foreach (var sub in SafeEnumerateDirectories(appDir))
                {
                    exe = ContainsGameExe(sub);
                    if (exe is not null) found.Add(Path.GetDirectoryName(exe)!);
                }
            }
        }
        return found.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
    }

    private static IEnumerable<string> SteamBases()
    {
        var bases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TryAddRegistryBase(bases, Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath");
        TryAddRegistryBase(bases, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
        TryAddRegistryBase(bases, Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath");
        foreach (var fallback in new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" })
        {
            if (Directory.Exists(fallback)) bases.Add(fallback);
        }

        var extras = new List<string>();
        foreach (var steamBase in bases)
        {
            var vdf = Path.Combine(steamBase, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            try
            {
                foreach (var line in File.ReadLines(vdf))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(line, "\"path\"\\s*\"([^\"]+)\"");
                    if (match.Success) extras.Add(match.Groups[1].Value.TrimEnd('\\', '/'));
                }
            }
            catch
            {
                // 读不到 VDF 时忽略，仅用注册表路径
            }
        }
        foreach (var extra in extras) bases.Add(extra);
        return bases;
    }

    private static void TryAddRegistryBase(HashSet<string> bases, RegistryKey hive, string subKey, string valueName)
    {
        try
        {
            using var key = hive.OpenSubKey(subKey);
            var path = key?.GetValue(valueName) as string;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path)) bases.Add(path.TrimEnd('\\', '/'));
        }
        catch
        {
            // 无注册表访问权限时忽略
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch
        {
            return [];
        }
    }

    private static string? ContainsGameExe(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.exe")
                .FirstOrDefault(file => GameExeNames.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
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

public sealed class SpeedhackConfig
{
    [JsonPropertyName("console")] public bool Console { get; set; } = false;

    [JsonPropertyName("base_speed")] public double BaseSpeed { get; set; } = 1.0;

    [JsonPropertyName("reload_config_keys")] public List<string> ReloadConfigKeys { get; set; } = ["VK_CONTROL", "VK_SHIFT", "VK_R"];

    [JsonPropertyName("wait_with_hook")] public SpeedhackDuration WaitWithHook { get; set; } = new() { Nanos = 250_000_000 };

    [JsonPropertyName("startup_state")] public SpeedhackStartupState? StartupState { get; set; }

    [JsonPropertyName("speed_states")] public List<SpeedhackSpeedState> SpeedStates { get; set; } = [];
}

public sealed class SpeedhackDuration
{
    [JsonPropertyName("secs")] public int Secs { get; set; }
    [JsonPropertyName("nanos")] public int Nanos { get; set; }
}

public sealed class SpeedhackStartupState
{
    [JsonPropertyName("speed")] public double Speed { get; set; } = 10.0;
    [JsonPropertyName("duration")] public SpeedhackDuration Duration { get; set; } = new() { Secs = 5 };
}

public sealed class SpeedhackSpeedState
{
    [JsonPropertyName("keys")] public List<string> Keys { get; set; } = [];
    [JsonPropertyName("speed")] public double Speed { get; set; } = 2.0;
    [JsonPropertyName("is_toggle")] public bool IsToggle { get; set; } = true;
}

public sealed class SpeedhackStatus
{
    public bool BundleDllPresent { get; set; }
    public bool TemplateConfigPresent { get; set; }
    public string ProfileConfigPath { get; set; } = "";
    public bool ProfileConfigPresent { get; set; }
    public string GameDirectory { get; set; } = "";
    public string AutoDetectedDirectory { get; set; } = "";
    public bool GameExeFound { get; set; }
    public bool GameRunning { get; set; }
    public bool Installed { get; set; }
    public bool DllPresent { get; set; }
    public bool DllMatchesBundle { get; set; }
    public bool ConfigPresent { get; set; }
    public string DllPath { get; set; } = "";
    public string ConfigPath { get; set; } = "";
    public string BundleHash { get; set; } = "";
    public string Message { get; set; } = "";
}

public sealed class UninstallResult
{
    public bool RemovedDll { get; set; }
    public bool RemovedConfig { get; set; }
    public string Message { get; set; } = "";
}
