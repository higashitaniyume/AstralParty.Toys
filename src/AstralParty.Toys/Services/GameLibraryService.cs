using System.Text.Json;
using Microsoft.Win32;

namespace AstralParty.Toys.Services;

/// <summary>一个已保存的游戏位置（国服 / 国际服 / TapTap 端…）。</summary>
public sealed class GameProfile
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Directory { get; set; } = "";
    /// <summary>cn | global | taptap | custom。</summary>
    public string Edition { get; set; } = "custom";
}

/// <summary>持久化到 game-profiles.json 的整体数据。</summary>
public sealed class GameLibraryData
{
    public List<GameProfile> Profiles { get; set; } = new();
    public string? ActiveId { get; set; }
}

/// <summary>发给前端的档案视图（附带实时的存在性 / Unity 结构校验）。</summary>
public sealed class GameProfileView
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Directory { get; set; } = "";
    public string Edition { get; set; } = "custom";
    public string EditionLabel { get; set; } = "";
    public bool Active { get; set; }
    public bool DirectoryExists { get; set; }
    public bool Valid { get; set; }
    public string? ExeName { get; set; }
}

/// <summary>系统搜索命中的一个候选目录。</summary>
public sealed class GameScanCandidate
{
    public string Directory { get; set; } = "";
    public string ExeName { get; set; } = "";
    public string Edition { get; set; } = "custom";
    public string EditionLabel { get; set; } = "";
    public bool Valid { get; set; }
    public bool AlreadyAdded { get; set; }
    /// <summary>registry | steam | common | deep。</summary>
    public string Source { get; set; } = "";
}

/// <summary>
/// 多个游戏位置（国服 / 国际服 / TapTap 端…）的统一存放处，外加"当前正在操作的游戏"指针。
/// 模组页与变速器页都以这里的「当前游戏」为准（HybridWindow 负责把它同步进两个管理器各自的状态文件）。
/// </summary>
public sealed class GameLibraryService
{
    private readonly string _profileDirectory;
    private readonly object _gate = new();

    public GameLibraryService(string? profileDirectory = null)
    {
        _profileDirectory = profileDirectory ?? SpeedhackManager.ResolveProfileDirectory();
    }

    private string StorePath => Path.Combine(_profileDirectory, "game-profiles.json");

    // ============================== 读写 ==============================

    public GameLibraryData Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(StorePath)) return new GameLibraryData();
                var data = JsonSerializer.Deserialize<GameLibraryData>(File.ReadAllText(StorePath));
                return Sanitize(data ?? new GameLibraryData());
            }
            catch
            {
                return new GameLibraryData();
            }
        }
    }

    private void Save(GameLibraryData data)
    {
        lock (_gate)
        {
            Directory.CreateDirectory(_profileDirectory);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(Sanitize(data),
                new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    /// <summary>去重（按规范化路径）、剔空、修正 activeId 指向。</summary>
    private static GameLibraryData Sanitize(GameLibraryData data)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cleaned = new List<GameProfile>();
        foreach (var profile in data.Profiles ?? new List<GameProfile>())
        {
            if (string.IsNullOrWhiteSpace(profile.Directory)) continue;
            var key = NormalizePath(profile.Directory);
            if (!seen.Add(key)) continue;
            if (string.IsNullOrWhiteSpace(profile.Id)) profile.Id = NewId();
            if (string.IsNullOrWhiteSpace(profile.Edition)) profile.Edition = "custom";
            if (string.IsNullOrWhiteSpace(profile.Label))
                profile.Label = DefaultLabel(profile.Edition, profile.Directory);
            cleaned.Add(profile);
        }

        var activeId = data.ActiveId;
        if (activeId is null || cleaned.All(p => p.Id != activeId))
            activeId = cleaned.FirstOrDefault()?.Id;

        return new GameLibraryData { Profiles = cleaned, ActiveId = activeId };
    }

    // ============================== 迁移 ==============================

    /// <summary>档案为空时，用给定的目录（旧的单目录记忆 + Steam 自动检测结果）各播一颗种子；首个有效目录设为当前。</summary>
    public void SeedFromLegacyIfEmpty(IEnumerable<string?> candidateDirectories)
    {
        var data = Load();
        if (data.Profiles.Count > 0) return;

        var seeds = candidateDirectories
            .Where(dir => !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            .Select(dir => dir!)
            .ToList();
        if (seeds.Count == 0) return;

        foreach (var dir in seeds) AddDirectory(dir, activate: false);

        // 首个种子设为当前
        var refreshed = Load();
        if (refreshed.ActiveId is null && refreshed.Profiles.Count > 0)
        {
            refreshed.ActiveId = refreshed.Profiles[0].Id;
            Save(refreshed);
        }
    }

    // ============================== 档案 CRUD ==============================

    public IReadOnlyList<GameProfileView> GetProfiles()
    {
        var data = Load();
        return data.Profiles.Select(p => ToView(p, p.Id == data.ActiveId)).ToArray();
    }

    public GameProfile? GetActive()
    {
        var data = Load();
        return data.Profiles.FirstOrDefault(p => p.Id == data.ActiveId);
    }

    /// <summary>当前游戏目录（校验存在）；没有有效档案时返回 null。</summary>
    public string? GetActiveDirectory()
    {
        var active = GetActive();
        return active is not null && Directory.Exists(active.Directory) ? active.Directory : null;
    }

    /// <summary>添加一个目录（已存在则返回既有档案）；activate=true 时顺便设为当前。</summary>
    public GameProfileView AddDirectory(string directory, bool activate = true, string? label = null, string? edition = null)
    {
        var data = Load();
        var key = NormalizePath(directory);
        var existing = data.Profiles.FirstOrDefault(p => NormalizePath(p.Directory) == key);
        if (existing is null)
        {
            var exe = DetectExe(directory);
            var resolvedEdition = edition ?? DetectEdition(directory, exe);
            existing = new GameProfile
            {
                Id = NewId(),
                Directory = directory,
                Edition = resolvedEdition,
                Label = string.IsNullOrWhiteSpace(label) ? DefaultLabel(resolvedEdition, directory) : label!
            };
            data.Profiles.Add(existing);
        }
        else if (!string.IsNullOrWhiteSpace(label))
        {
            existing.Label = label!;
        }

        if (activate) data.ActiveId = existing.Id;
        Save(data);

        var reloaded = Load();
        var view = reloaded.Profiles.FirstOrDefault(p => NormalizePath(p.Directory) == key);
        return view is not null ? ToView(view, view.Id == reloaded.ActiveId) : ToView(existing, activate);
    }

    public void SetActive(string id)
    {
        var data = Load();
        if (data.Profiles.Any(p => p.Id == id))
        {
            data.ActiveId = id;
            Save(data);
        }
    }

    public void UpdateProfile(string id, string? label, string? edition)
    {
        var data = Load();
        var profile = data.Profiles.FirstOrDefault(p => p.Id == id);
        if (profile is null) return;
        if (!string.IsNullOrWhiteSpace(label)) profile.Label = label!.Trim();
        if (!string.IsNullOrWhiteSpace(edition)) profile.Edition = edition!;
        Save(data);
    }

    public void Remove(string id)
    {
        var data = Load();
        data.Profiles.RemoveAll(p => p.Id == id);
        if (data.ActiveId == id) data.ActiveId = data.Profiles.FirstOrDefault()?.Id;
        Save(data);
    }

    public string? GetDirectory(string id) => Load().Profiles.FirstOrDefault(p => p.Id == id)?.Directory;

    // ============================== 校验 / 识别 ==============================

    /// <summary>目录里第一个匹配 AstralParty*.exe 的文件名（找不到返回 null）。</summary>
    public static string? DetectExe(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, SpeedhackManager.GameExeSearchPattern)
                .Select(Path.GetFileName)
                .FirstOrDefault(name => !string.IsNullOrEmpty(name));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 是否是标准 Unity 游戏目录：存在 <c>&lt;exe名&gt;_Data\</c>（或任意 <c>*_Data\</c>）且同目录有 UnityPlayer.dll。
    /// </summary>
    public static bool ValidateUnityStructure(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return false;
            if (!File.Exists(Path.Combine(directory, "UnityPlayer.dll"))) return false;

            var exe = DetectExe(directory);
            if (exe is not null)
            {
                var dataDir = Path.Combine(directory, Path.GetFileNameWithoutExtension(exe) + "_Data");
                if (Directory.Exists(dataDir)) return true;
            }
            // 回退：任意 *_Data 文件夹
            return Directory.EnumerateDirectories(directory, "*_Data").Any();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>按 exe 名 / 路径猜测区服（用户可在界面改）。</summary>
    public static string DetectEdition(string directory, string? exeName = null)
    {
        var dirLower = (directory ?? "").ToLowerInvariant();
        if (dirLower.Contains("taptap")) return "taptap";

        var exe = (exeName ?? DetectExe(directory ?? "") ?? "").ToLowerInvariant();
        if (exe.Contains("_cn")) return "cn";
        if (exe == "astralparty.exe") return "global";
        return "custom";
    }

    public static string EditionLabel(string edition) => edition switch
    {
        "cn" => "国服",
        "global" => "国际服",
        "taptap" => "TapTap",
        _ => "自定义"
    };

    private static string DefaultLabel(string edition, string directory)
    {
        var name = EditionLabel(edition);
        if (edition == "custom")
        {
            var leaf = SafeLeafName(directory);
            return string.IsNullOrEmpty(leaf) ? "吉星派对" : leaf;
        }
        return $"吉星派对 · {name}";
    }

    // ============================== 系统搜索 ==============================

    /// <summary>快速扫描：Steam 库 + 注册表卸载项 / App Paths + 常见安装位置（秒级返回）。</summary>
    public IReadOnlyList<GameScanCandidate> QuickScan()
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Consider(string? dir, string source)
        {
            if (string.IsNullOrWhiteSpace(dir)) return;
            var full = TryFullPath(dir);
            if (full is null || !Directory.Exists(full)) return;
            if (DetectExe(full) is null) return;
            if (dirs.Add(NormalizePath(full))) sources[NormalizePath(full)] = source;
        }

        foreach (var dir in SpeedhackManager.EnumerateGameDirectories()) Consider(dir, "steam");
        foreach (var dir in ScanRegistry()) Consider(dir, "registry");
        foreach (var dir in ScanCommonLocations()) Consider(dir, "common");

        return BuildCandidates(dirs, sources);
    }

    /// <summary>深度扫描：遍历指定盘符的所有目录找 AstralParty*.exe（慢、可取消，带进度回调）。</summary>
    public IReadOnlyList<GameScanCandidate> DeepScan(string driveRoot, CancellationToken token,
        Action<int, string>? onProgress = null)
    {
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var scanned = 0;

        var stack = new Stack<string>();
        stack.Push(driveRoot);
        while (stack.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var current = stack.Pop();
            scanned++;
            if (scanned % 200 == 0) onProgress?.Invoke(scanned, current);

            // exe 命中
            try
            {
                foreach (var exe in Directory.EnumerateFiles(current, SpeedhackManager.GameExeSearchPattern))
                {
                    var dir = Path.GetDirectoryName(exe);
                    if (dir is not null && dirs.Add(NormalizePath(dir))) sources[NormalizePath(dir)] = "deep";
                }
            }
            catch { /* 无权限 / 特殊目录，跳过 */ }

            try
            {
                foreach (var sub in Directory.EnumerateDirectories(current))
                {
                    var name = SafeLeafName(sub);
                    // 跳过明显不含游戏的系统目录，省时间
                    if (name is "Windows" or "$Recycle.Bin" or "System Volume Information") continue;
                    stack.Push(sub);
                }
            }
            catch { /* 无权限，跳过 */ }
        }

        onProgress?.Invoke(scanned, driveRoot);
        return BuildCandidates(dirs, sources);
    }

    public static IReadOnlyList<string> FixedDriveRoots()
    {
        try
        {
            return DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Select(d => d.RootDirectory.FullName)
                .ToArray();
        }
        catch
        {
            return new[] { @"C:\" };
        }
    }

    private IReadOnlyList<GameScanCandidate> BuildCandidates(HashSet<string> dirs, Dictionary<string, string> sources)
    {
        var added = Load().Profiles.Select(p => NormalizePath(p.Directory)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = new List<GameScanCandidate>();
        foreach (var norm in dirs)
        {
            // norm 是规范化后的 key；直接用它作为展示路径即可（已 FullPath 过）
            var directory = norm;
            var exe = DetectExe(directory) ?? "";
            var edition = DetectEdition(directory, exe);
            list.Add(new GameScanCandidate
            {
                Directory = directory,
                ExeName = exe,
                Edition = edition,
                EditionLabel = EditionLabel(edition),
                Valid = ValidateUnityStructure(directory),
                AlreadyAdded = added.Contains(norm),
                Source = sources.TryGetValue(norm, out var src) ? src : ""
            });
        }
        return list.OrderByDescending(c => c.Valid).ThenBy(c => c.Directory, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> ScanRegistry()
    {
        var results = new List<string>();

        // 卸载项：DisplayName 含 Astral Party 或 InstallLocation 指向游戏
        foreach (var (hive, subKey) in new (RegistryKey, string)[]
                 {
                     (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                     (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
                     (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")
                 })
        {
            try
            {
                using var root = hive.OpenSubKey(subKey);
                if (root is null) continue;
                foreach (var name in root.GetSubKeyNames())
                {
                    try
                    {
                        using var entry = root.OpenSubKey(name);
                        if (entry is null) continue;
                        var display = entry.GetValue("DisplayName") as string ?? "";
                        var location = entry.GetValue("InstallLocation") as string ?? "";
                        var isAstral = display.Contains("Astral", StringComparison.OrdinalIgnoreCase)
                                       && display.Contains("Party", StringComparison.OrdinalIgnoreCase);
                        if (!string.IsNullOrWhiteSpace(location) && (isAstral || DirectoryHasGameExe(location)))
                            results.Add(location);
                    }
                    catch { /* 单个项读不动就跳过 */ }
                }
            }
            catch { /* 该 hive 读不动就跳过 */ }
        }

        // App Paths：AstralParty*.exe
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            try
            {
                using var appPaths = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                if (appPaths is null) continue;
                foreach (var name in appPaths.GetSubKeyNames())
                {
                    if (!name.StartsWith("AstralParty", StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        using var entry = appPaths.OpenSubKey(name);
                        var path = entry?.GetValue("Path") as string
                                   ?? Path.GetDirectoryName(entry?.GetValue(null) as string ?? "");
                        if (!string.IsNullOrWhiteSpace(path)) results.Add(path!);
                    }
                    catch { /* 跳过 */ }
                }
            }
            catch { /* 跳过 */ }
        }

        return results;
    }

    private static IEnumerable<string> ScanCommonLocations()
    {
        var roots = new List<string>();
        foreach (var special in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                 })
        {
            if (!string.IsNullOrWhiteSpace(special)) roots.Add(special);
        }
        // 各固定盘根下的 TapTap / Games 常见目录
        foreach (var drive in FixedDriveRoots())
        {
            roots.Add(Path.Combine(drive, "TapTap"));
            roots.Add(Path.Combine(drive, "Program Files", "TapTap"));
            roots.Add(Path.Combine(drive, "Games"));
        }

        var results = new List<string>();
        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (DirectoryHasGameExe(root)) results.Add(root);
            foreach (var sub in SafeDirs(root))
            {
                if (DirectoryHasGameExe(sub)) results.Add(sub);
                foreach (var sub2 in SafeDirs(sub)) // TapTap 常见嵌套：TapTap\Games\<游戏>\
                    if (DirectoryHasGameExe(sub2)) results.Add(sub2);
            }
        }
        return results;
    }

    // ============================== 小工具 ==============================

    private static bool DirectoryHasGameExe(string directory)
    {
        try
        {
            return Directory.Exists(directory) && DetectExe(directory) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> SafeDirs(string path)
    {
        try { return Directory.EnumerateDirectories(path); }
        catch { return Array.Empty<string>(); }
    }

    private GameProfileView ToView(GameProfile profile, bool active)
    {
        var exists = Directory.Exists(profile.Directory);
        var exe = exists ? DetectExe(profile.Directory) : null;
        return new GameProfileView
        {
            Id = profile.Id,
            Label = profile.Label,
            Directory = profile.Directory,
            Edition = profile.Edition,
            EditionLabel = EditionLabel(profile.Edition),
            Active = active,
            DirectoryExists = exists,
            Valid = exists && ValidateUnityStructure(profile.Directory),
            ExeName = exe
        };
    }

    private static string NormalizePath(string path)
    {
        var full = TryFullPath(path) ?? path;
        return full.TrimEnd('\\', '/').ToLowerInvariant();
    }

    private static string? TryFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return null; }
    }

    private static string SafeLeafName(string path)
    {
        try { return new DirectoryInfo(path.TrimEnd('\\', '/')).Name; }
        catch { return ""; }
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
