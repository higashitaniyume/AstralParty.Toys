using System.Text.Json;
using Microsoft.Win32;
using AstralParty.Toys.Services.Game;

namespace AstralParty.Toys.Services.Speedhack;

public sealed partial class SpeedhackManager
{
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
    public static string? DetectGameDirectory() =>
        EnumerateGameDirectories().FirstOrDefault();

    /// <summary>顺着 Steam 库把所有含游戏 exe 的目录都列出来（供多位置管理 / 系统搜索复用）。</summary>
    public static IReadOnlyList<string> EnumerateGameDirectories()
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var steamBase in SteamBases())
        {
            var common = Path.Combine(steamBase, "steamapps", "common");
            if (!Directory.Exists(common)) continue;
            foreach (var appDir in SafeEnumerateDirectories(common))
            {
                // 自动查找只认 AstralParty*.exe：ContainsGameExe 的 Unity 兜底是给手动选目录用的，
                // 用在这里会把 Steam 库里别的 Unity 游戏也当成吉星派对（首次启动还可能设成当前游戏）。
                if (HasKnownGameExe(appDir)) found.Add(appDir);
                foreach (var sub in SafeEnumerateDirectories(appDir))
                {
                    if (HasKnownGameExe(sub)) found.Add(sub);
                }
            }
        }
        return found.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
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

    /// <summary>目录里有名字以 AstralParty 开头的 exe（不含手动选目录时的 Unity 兜底）。</summary>
    public static bool HasKnownGameExe(string directory)
    {
        try { return Directory.EnumerateFiles(directory, GameExeSearchPattern).Any(); }
        catch { return false; }
    }

    /// <summary>返回已识别游戏主程序的完整路径，包含 Unity 结构内的非标准渠道 exe 名。</summary>
    public static string? ContainsGameExe(string directory)
    {
        var exe = GameLibraryService.DetectExe(directory);
        return string.IsNullOrEmpty(exe) ? null : Path.Combine(directory, exe);
    }
}
