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

    /// <summary>配置与回放库共用的数据文件夹名（位于用户「文档」下）。</summary>
    public const string DataFolderName = "AstralPartyReplays";

    private readonly string _documentsDirectory;
    private readonly string _profileDirectory;
    private readonly string _profileLocationNote;

    /// <param name="appDirectory">工具目录；现在只用于提示信息，配置不再放在 exe 旁边。</param>
    /// <param name="profileDirectory">显式指定配置根目录（测试用）。</param>
    /// <param name="appDataDirectory">%AppData% 位置覆盖（测试用）。</param>
    /// <param name="documentsDirectory">「文档」数据目录覆盖（测试用）。</param>
    public SpeedhackManager(string appDirectory, string? profileDirectory = null,
        string? appDataDirectory = null, string? documentsDirectory = null)
    {
        _ = appDirectory; // 配置统一放文档目录，不跟 exe 走
        _documentsDirectory = documentsDirectory ?? ResolveDocumentsDataDirectory();

        if (profileDirectory is { Length: > 0 })
        {
            _profileDirectory = profileDirectory;
            _profileLocationNote = "";
        }
        else
        {
            var (directory, note) = ChooseProfileDirectory(
                _documentsDirectory,
                appDataDirectory ?? ResolveAppDataDirectory());
            _profileDirectory = directory;
            _profileLocationNote = note;
        }
    }

    /// <summary>用户「文档」下的 AstralPartyReplays（回放库默认也用这个文件夹）。</summary>
    public static string ResolveDocumentsDataDirectory()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
            documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(documents, DataFolderName);
    }

    /// <summary>%AppData%\AstralParty.Toys（含旧名 AstralParty.ReplayTool 的一次性迁移）。</summary>
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
    /// 配置根目录：统一放在用户「文档」下的 AstralPartyReplays（主配置、游戏目录记忆、回放库设置都在这一个文件夹里）。
    /// 首次运行会把 %AppData% 里的旧配置复制过去，玩家的设置不会丢；文档目录实在不可写时才回退 %AppData%。
    /// 两个 override 参数仅供测试注入。
    /// </summary>
    public static string ResolveProfileDirectory(string? appDataDirectoryOverride = null, string? documentsDirectoryOverride = null)
    {
        var documents = documentsDirectoryOverride ?? ResolveDocumentsDataDirectory();
        var appData = appDataDirectoryOverride ?? ResolveAppDataDirectory();
        return ChooseProfileDirectory(documents, appData).Directory;
    }

    private static (string Directory, string Note) ChooseProfileDirectory(string documentsDirectory, string appDataDirectory)
    {
        if (!TryEnsureWritable(documentsDirectory))
        {
            return (appDataDirectory,
                $"文档目录不可写（{documentsDirectory}），配置暂时保存在 {appDataDirectory}。");
        }

        var migrated = MigrateLegacyConfig(appDataDirectory, documentsDirectory);
        var note = $"配置保存在「文档\\{DataFolderName}」（和回放库同一个文件夹）：主配置、游戏目录记忆、回放库设置都在这里。";
        if (migrated.Count > 0)
            note += $"\n已把 %AppData% 里的旧配置复制过来：{string.Join("、", migrated)}。";
        return (documentsDirectory, note);
    }

    /// <summary>把 %AppData% 里已有的配置复制到文档目录（只在目标不存在时复制，不覆盖、不删除源文件）。</summary>
    private static List<string> MigrateLegacyConfig(string appDataDirectory, string documentsDirectory)
    {
        var migrated = new List<string>();
        if (SamePath(appDataDirectory, documentsDirectory) || !Directory.Exists(appDataDirectory)) return migrated;

        foreach (var relative in new[]
                 {
                     Path.Combine("speedhack", ConfigName),
                     "speedhack-state.json",
                     "replay-library.json"
                 })
        {
            try
            {
                var source = Path.Combine(appDataDirectory, relative);
                var target = Path.Combine(documentsDirectory, relative);
                if (!File.Exists(source) || File.Exists(target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target);
                migrated.Add(relative);
            }
            catch
            {
                // 复制不动就当没有旧配置，工具会生成默认值
            }
        }
        return migrated;
    }

    /// <summary>真的往目录里写一个临时文件来探测可写性（目录不存在会先创建）。</summary>
    private static bool TryEnsureWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".astral-write-probe-{Guid.NewGuid():N}.tmp");
            using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1,
                FileOptions.DeleteOnClose);
            stream.WriteByte(0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool HasEmbeddedResources => EmbeddedDll is not null && EmbeddedConfigTemplate is not null;
    public static bool HasEmbeddedDll => EmbeddedDll is not null;
    public static bool HasEmbeddedConfigTemplate => EmbeddedConfigTemplate is not null;

    /// <summary>可编辑的主配置；默认在工具目录 portable 形态下，或回退到 %AppData%\AstralParty.Toys。</summary>
    public string ProfileConfigPath => Path.Combine(_profileDirectory, "speedhack", ConfigName);

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
            status.ProfileDirectory = _profileDirectory;
            status.ProfileLocationDocuments = SamePath(_profileDirectory, _documentsDirectory);
            status.ProfileLocationNote = _profileLocationNote;

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
            throw new InvalidOperationException("所选目录里没有找到 AstralParty.exe / AstralParty_CN.exe，确认这是游戏 exe 所在的目录？");

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

    public static string? ContainsGameExe(string directory)
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

    /// <summary>配置根目录（主配置、speedhack-state、回放库设置都在这下面）。</summary>
    public string ProfileDirectory { get; set; } = "";

    /// <summary>配置是否保存在用户「文档」目录（正常情况都是 true）。</summary>
    public bool ProfileLocationDocuments { get; set; }

    /// <summary>配置位置的人话说明（便携 / 回退 AppData / 沿用旧位置）。</summary>
    public string ProfileLocationNote { get; set; } = "";

    /// <summary>游戏正在运行时，安装会发生什么（两个界面共用同一套文案）。</summary>
    public string RunningInstallHint => SpeedhackManager.DescribeRunningInstall();

    /// <summary>游戏正在运行时，卸载会发生什么。</summary>
    public string RunningUninstallHint => SpeedhackManager.DescribeRunningUninstall();

    /// <summary>游戏正在运行时，同步配置会发生什么。</summary>
    public string RunningPushConfigHint => SpeedhackManager.DescribeRunningPushConfig();
}

public sealed class UninstallResult
{
    public bool RemovedDll { get; set; }
    public bool RemovedConfig { get; set; }
    public string Message { get; set; } = "";
}
