using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services.Modding;

/// <summary>
/// 一份文件的回滚快照：按**原始字节**保存（BOM、CRLF、注释都不丢），恢复后校验 SHA-256。
/// <see cref="Existed"/> = false 表示"操作前这份文件不存在" —— 回滚时要把后来生成的那份删掉，
/// 否则不叫无损。
/// </summary>
public sealed class RollbackFileSnapshot
{
    public bool Existed { get; set; }
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public string BytesBase64 { get; set; } = "";

    public static RollbackFileSnapshot Capture(string? path) =>
        Capture(path, ModRollbackJournal.MaxFileSnapshotBytes);

    public static RollbackFileSnapshot Capture(string? path, long maxBytes)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return new RollbackFileSnapshot { Existed = false };

        var info = new FileInfo(path);
        if (info.Length > maxBytes)
            throw new InvalidOperationException(
                $"文件过大，无法为这个操作建立回滚记录：{path}（{info.Length} 字节，上限 {maxBytes} 字节）。");

        var bytes = File.ReadAllBytes(path);
        return new RollbackFileSnapshot
        {
            Existed = true,
            Length = bytes.LongLength,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            BytesBase64 = Convert.ToBase64String(bytes)
        };
    }

    public byte[] GetBytes() => Existed ? Convert.FromBase64String(BytesBase64) : Array.Empty<byte>();

    /// <summary>磁盘上的文件是否仍与这份快照一致（快照不存在 + 文件也不存在 = 一致）。</summary>
    public bool MatchesCurrent(string? path)
    {
        var exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        if (!Existed) return !exists;
        if (!exists) return false;
        try { return string.Equals(ModRollbackJournal.Sha256For(path!), Sha256, StringComparison.OrdinalIgnoreCase); }
        catch { return false; }
    }
}

/// <summary>一个模组在操作前的状态：所在目录、sidecar 原文、config.json 原文。</summary>
public sealed class RollbackModSnapshot
{
    public string Name { get; set; } = "";

    /// <summary>true = 目录在 <c>mods</c> 里；false = 在 <c>disabled_mods</c> 里。</summary>
    public bool DirectoryEnabled { get; set; }

    public RollbackFileSnapshot? Sidecar { get; set; }
    public RollbackFileSnapshot? Config { get; set; }

    /// <summary>模组目录里的那份 <c>config.json</c>（当前配置双写，回滚要把它一起还原）。</summary>
    public RollbackFileSnapshot? PackagedConfig { get; set; }
}

/// <summary>操作之后的观测值，只用于回滚前的冲突检测（外部改过就不静默覆盖）。</summary>
public sealed class RollbackModAfter
{
    public bool DirectoryEnabled { get; set; }
    public string? SidecarSha { get; set; }
    public string? ConfigSha { get; set; }

    /// <summary>模组目录里那份 <c>config.json</c> 的观测值；空 = 旧记录没有这一项，不做冲突检测。</summary>
    public string? PackagedConfigSha { get; set; }
}

public sealed class RollbackAfterState
{
    public string? ProfilesSha { get; set; }
    public string? LoaderConfigSha { get; set; }
    public Dictionary<string, RollbackModAfter> Mods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// 方案定义类操作的"定向撤销"信息。
///
/// 定义类操作（保存 / 更新 / 改名 / 删除 / 备注 / 当前方案）回滚时**不能整文件覆盖**：
/// 那样会把操作之后新建的其它方案一起抹掉。所以这里记住"这一条改了什么"，
/// 回滚只撤销这一条；操作之后被改过就报冲突，确认后才强制覆盖。
/// </summary>
public sealed class RollbackProfileEdit
{
    /// <summary>capture | update | rename | delete | note | active</summary>
    public string Kind { get; set; } = "";

    public string Name { get; set; } = "";
    public string? NewName { get; set; }

    /// <summary>操作前的定义（update / delete 用来还原）。</summary>
    public string? PreviousJson { get; set; }

    /// <summary>操作后的定义（冲突检测用）。</summary>
    public string? ResultJson { get; set; }

    public string? PreviousNote { get; set; }
    public string? ResultNote { get; set; }

    public string? PreviousActive { get; set; }
    public string? ResultActive { get; set; }

    public int Index { get; set; }
}

/// <summary>一条回滚记录。写盘位置：<c>state\rollback\&lt;id&gt;.json</c>。</summary>
public sealed class RollbackEntry
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string GameDirectory { get; set; } = "";
    public string CreatedAtUtc { get; set; } = "";

    /// <summary>Begin 时先落盘，Commit 时置 true；进程中途崩溃会留下 false 的记录。</summary>
    public bool Completed { get; set; }

    /// <summary>
    /// true = 回滚加载器配置时**保留当时的多开档案选择**（<c>profile</c> / <c>profileWindowTitle</c>）。
    /// 模组方案 / 加载器设置这些操作本来就不改这两个字段，回滚不该把用户之后切换的账号一起退回去。
    /// </summary>
    public bool PreserveAccountFields { get; set; }

    public bool RolledBack { get; set; }
    public string? RolledBackAtUtc { get; set; }

    public RollbackFileSnapshot? Profiles { get; set; }
    public RollbackFileSnapshot? LoaderConfig { get; set; }
    public List<RollbackModSnapshot> Mods { get; set; } = new();
    public RollbackAfterState After { get; set; } = new();

    /// <summary>非空 = 这条操作按"定向撤销"回滚（方案定义类）。</summary>
    public RollbackProfileEdit? ProfileEdit { get; set; }
}

/// <summary>给界面用的列表行。</summary>
public sealed class RollbackEntryInfo
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Title { get; set; } = "";
    public string CreatedAtUtc { get; set; } = "";
    public bool Completed { get; set; }
    public bool RolledBack { get; set; }
    public bool HasMods { get; set; }
    public bool HasLoaderConfig { get; set; }
    public bool HasProfiles { get; set; }
    public int ModCount { get; set; }
}

public sealed class RollbackResult
{
    public bool Ok { get; set; }

    /// <summary>true = 当前状态与记录不一致，需要用户确认（force）后才能覆盖。</summary>
    public bool NeedsForce { get; set; }

    public string Message { get; set; } = "";
    public List<string> Restored { get; set; } = new();
    public List<string> Conflicts { get; set; } = new();
    public List<string> Failed { get; set; } = new();
}

/// <summary>
/// 无损回滚日志。
///
/// 本功能的每个写操作（保存 / 更新 / 重命名 / 删除方案、套用方案、多开档案切换、
/// 加载器设置与变速保存）都包一层：<c>Begin</c> 先把"操作前"按原始字节落盘，改完 <c>Commit</c>
/// 再补"操作后"的观测值；<c>Rollback</c> 用 Begin 记下的字节逐项还原并校验 SHA-256。
///
/// 三条硬规则：
///   · 只记录真正会被改到的东西（方案文件 / 加载器配置 / 模组目录位置 + sidecar + config.json），
///     不复制模组 DLL；
///   · 回滚前先比对"操作后"的观测值，外部改过的项默认不覆盖（返回 NeedsForce）；
///   · 回滚逐项执行、逐项校验，部分失败如实报告，不把失败写成成功。
/// </summary>
public sealed class ModRollbackJournal
{
    public const int MaxEntries = 20;
    public const int MaxAgeDays = 30;
    public const long MaxTotalBytes = 64L * 1024 * 1024;
    public const long MaxFileSnapshotBytes = 2L * 1024 * 1024;
    public const long MaxEntryBytes = 8L * 1024 * 1024;

    private static readonly object Gate = new();

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ModManager _modManager;

    public ModRollbackJournal(ModManager modManager)
    {
        _modManager = modManager;
        Root = Path.Combine(modManager.ProfileDirectory, ToysPaths.StateFolderName, "rollback");
    }

    /// <summary><c>&lt;数据根&gt;\state\rollback</c>。</summary>
    public string Root { get; }

    private string ProfilesPath =>
        Path.Combine(_modManager.ProfileDirectory, ToysPaths.StateFolderName, ModProfileService.ProfilesFileName);

    private string EntryPath(string id) => Path.Combine(Root, id + ".json");

    /// <summary>
    /// 开始记录一次操作。<paramref name="includeMods"/> 用于会动模组目录 / 配置的操作（套用方案），
    /// 只改配置文件的操作用 <paramref name="includeLoader"/>/<paramref name="includeProfiles"/> 即可。
    /// 记录会立刻落盘（Completed=false），所以中途崩溃也能回滚。
    /// </summary>
    public RollbackOperation Begin(
        string kind,
        string title,
        string? gameDirectory,
        bool includeMods,
        bool includeLoader,
        bool includeProfiles,
        bool preserveAccountFields = false,
        string? modFileName = null)
    {
        lock (Gate)
        {
            var directory = (gameDirectory ?? "").Trim();
            var entry = new RollbackEntry
            {
                Id = NewId(kind),
                Kind = kind,
                Title = title,
                GameDirectory = directory,
                CreatedAtUtc = DateTime.UtcNow.ToString("o"),
                PreserveAccountFields = preserveAccountFields
            };

            if (includeLoader && directory.Length > 0)
                entry.LoaderConfig = RollbackFileSnapshot.Capture(LoaderConfigPath(directory));

            if (includeProfiles)
                entry.Profiles = RollbackFileSnapshot.Capture(ProfilesPath);

            if (includeMods && directory.Length > 0 && Directory.Exists(directory))
            {
                foreach (var mod in _modManager.GetInstalledMods(directory))
                {
                    if (modFileName is not null && !string.Equals(mod.FileName, modFileName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    entry.Mods.Add(new RollbackModSnapshot
                    {
                        Name = mod.Name,
                        DirectoryEnabled = !mod.DisabledFolder,
                        Sidecar = RollbackFileSnapshot.Capture(_modManager.ModSidecarPath(directory, mod.FileName)),
                        Config = RollbackFileSnapshot.Capture(_modManager.ModConfigWritePathFor(directory, mod.FileName)),
                        PackagedConfig = RollbackFileSnapshot.Capture(_modManager.ModPackagedConfigPathFor(directory, mod.FileName))
                    });
                }
            }

            var json = JsonSerializer.Serialize(entry, JsonOptions);
            if (json.Length > MaxEntryBytes)
                throw new InvalidOperationException(
                    $"这次操作涉及的文件太多（约 {json.Length / 1024} KB），超过回滚记录上限 " +
                    $"（{MaxEntryBytes / 1024 / 1024} MB），已取消，未改动任何文件。");

            Directory.CreateDirectory(Root);
            ToysConfigStore.WriteAtomic(EntryPath(entry.Id), json);
            return new RollbackOperation(this, entry);
        }
    }

    /// <summary>最近的操作，新的在前。</summary>
    public List<RollbackEntryInfo> ListEntries()
    {
        lock (Gate)
        {
            var list = new List<RollbackEntryInfo>();
            foreach (var path in SafeEnumerate())
            {
                var entry = TryRead(path);
                if (entry is null) continue;
                list.Add(new RollbackEntryInfo
                {
                    Id = entry.Id,
                    Kind = entry.Kind,
                    Title = entry.Title,
                    CreatedAtUtc = entry.CreatedAtUtc,
                    Completed = entry.Completed,
                    RolledBack = entry.RolledBack,
                    HasMods = entry.Mods.Count > 0,
                    HasLoaderConfig = entry.LoaderConfig?.Existed == true,
                    HasProfiles = entry.Profiles is not null,
                    ModCount = entry.Mods.Count
                });
            }
            return list.OrderByDescending(e => e.CreatedAtUtc, StringComparer.Ordinal).ToList();
        }
    }

    /// <summary>按记录还原。冲突（操作后文件被外部改过）默认拒绝，<paramref name="force"/> 才覆盖。</summary>
    public RollbackResult Rollback(string id, bool force)
    {
        lock (Gate)
        {
            var result = new RollbackResult();
            if (string.IsNullOrWhiteSpace(id) || id.Length > 128 ||
                id.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
            {
                result.Message = "回滚记录编号无效。";
                return result;
            }

            var path = EntryPath(id ?? "");
            var entry = TryRead(path);
            if (entry is null)
            {
                result.Message = "找不到这条回滚记录（可能已被自动清理）。";
                return result;
            }
            if (entry.RolledBack)
            {
                result.Message = "这条操作已经回滚过了。";
                return result;
            }

            var directory = entry.GameDirectory;
            var hasMods = entry.Mods.Count > 0;
            if (hasMods && (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)))
            {
                result.Message = "游戏目录已经不存在，无法还原模组文件。";
                return result;
            }

            // ---- 1. 冲突检测 ----
            if (entry.Completed)
            {
                if (entry.Profiles is not null && !ProfilesMatchesAfter(entry))
                    result.Conflicts.Add("方案文件在操作之后被改过");
                if (entry.LoaderConfig is not null)
                {
                    var loaderPath = LoaderConfigPath(directory);
                    if (!SnapshotMatches(loaderPath, entry.After.LoaderConfigSha, entry.PreserveAccountFields))
                        result.Conflicts.Add(entry.PreserveAccountFields
                            ? "加载器配置（多开档案以外的部分）在操作之后被改过"
                            : "加载器配置在操作之后被改过");
                }
                foreach (var mod in entry.Mods)
                {
                    if (!entry.After.Mods.TryGetValue(mod.Name, out var after)) continue;
                    var current = CurrentModState(directory, mod.Name);
                    if (current is null)
                    {
                        result.Conflicts.Add($"模组「{mod.Name}」已被移走或删除");
                        continue;
                    }
                    if (current.DirectoryEnabled != after.DirectoryEnabled ||
                        !SnapshotMatches(_modManager.ModSidecarPath(directory, current.FileName), after.SidecarSha) ||
                        !SnapshotMatches(_modManager.ModConfigWritePathFor(directory, current.FileName), after.ConfigSha) ||
                        (mod.PackagedConfig is not null &&
                         !SnapshotMatches(_modManager.ModPackagedConfigPathFor(directory, current.FileName), after.PackagedConfigSha)))
                    {
                        result.Conflicts.Add($"模组「{mod.Name}」的状态或配置在操作之后被改过");
                    }
                }
            }
            else
            {
                result.Conflicts.Add("这次操作没有正常完成（程序可能中途退出），记录里只有操作前的状态");
            }

            if (entry.ProfileEdit is not null)
                result.Conflicts.AddRange(ProfileEditConflicts(entry.ProfileEdit));

            if (result.Conflicts.Count > 0 && !force)
            {
                result.NeedsForce = true;
                result.Message = "有文件在操作之后被改过，回滚会覆盖这些改动。确认后可以强制回滚。";
                return result;
            }

            // ---- 2. 还原 ----
            if (entry.Profiles is not null)
            {
                if (RestoreFile(entry.Profiles, ProfilesPath, "模组方案文件", result))
                    result.Restored.Add("模组方案文件");
            }

            if (entry.LoaderConfig is not null && directory.Length > 0)
            {
                // 模组方案 / 加载器设置本来就不改账号档案：还原时把当前选择保住，
                // 否则"回滚一个方案"会顺手把用户后来切换的多开档案退回去。
                var preserveAccount = entry.PreserveAccountFields;
                string? currentProfile = null;
                var currentWindowTitle = true;
                if (preserveAccount)
                {
                    var now = _modManager.ReadLoaderConfig(directory);
                    currentProfile = now.Profile;
                    currentWindowTitle = now.ProfileWindowTitle;
                }

                var loaderPath = LoaderConfigPath(directory);
                var restored = RestoreFile(
                    entry.LoaderConfig, loaderPath, "加载器配置", result, verify: !preserveAccount);

                if (restored && preserveAccount)
                {
                    var wroteAccountField = false;
                    var cfg = _modManager.ReadLoaderConfig(directory);
                    if (!string.Equals((cfg.Profile ?? "").Trim(), (currentProfile ?? "").Trim(), StringComparison.Ordinal))
                    {
                        wroteAccountField = true;
                        if (!_modManager.SetLoaderProfile(directory, currentProfile ?? ""))
                            result.Failed.Add("回滚后没能保留多开档案选择");
                    }
                    cfg = _modManager.ReadLoaderConfig(directory);
                    if (cfg.ProfileWindowTitle != currentWindowTitle)
                    {
                        wroteAccountField = true;
                        if (!_modManager.SetProfileWindowTitle(directory, currentWindowTitle))
                            result.Failed.Add("回滚后没能保留多开窗口标题开关");
                    }

                    if (result.Failed.Count == 0)
                    {
                        var verify = _modManager.ReadLoaderConfig(directory);
                        if (!string.Equals((verify.Profile ?? "").Trim(), (currentProfile ?? "").Trim(), StringComparison.Ordinal) ||
                            verify.ProfileWindowTitle != currentWindowTitle)
                        {
                            result.Failed.Add("回滚后多开档案选择没有保住");
                        }
                        else if (!wroteAccountField && !entry.LoaderConfig.MatchesCurrent(loaderPath))
                        {
                            result.Failed.Add("加载器配置还原后内容校验不一致");
                        }
                        else
                        {
                            result.Restored.Add("加载器配置（保留多开档案）");
                        }
                    }
                }
                else if (restored)
                {
                    result.Restored.Add("加载器配置");
                }
            }

            if (hasMods)
            {
                foreach (var mod in entry.Mods)
                {
                    try
                    {
                        RestoreMod(directory, mod, result);
                    }
                    catch (Exception ex)
                    {
                        result.Failed.Add($"模组「{mod.Name}」：{ex.Message}");
                    }
                }
            }

            // 方案定义类：只撤销这一条，不覆盖整份 profiles.json（否则会删掉之后新建的方案）
            if (entry.ProfileEdit is not null)
                UndoProfileEdit(entry.ProfileEdit, force, result);

            // ---- 3. 校验 + 标记 ----
            if (entry.Profiles is not null && !entry.Profiles.MatchesCurrent(ProfilesPath))
                result.Failed.Add("模组方案文件校验不一致");

            result.Ok = result.Failed.Count == 0;
            if (result.Ok)
            {
                entry.RolledBack = true;
                entry.RolledBackAtUtc = DateTime.UtcNow.ToString("o");
                TryWrite(entry);
                result.Message = "已回滚到操作前的状态。";
            }
            else
            {
                result.Message = "回滚没有完全成功，失败项已列出；已成功的部分保持原样。";
            }
            return result;
        }
    }

    // ============================== 内部 ==============================

    private sealed class CurrentMod
    {
        public string FileName { get; set; } = "";
        public bool DirectoryEnabled { get; set; }
    }

    private CurrentMod? CurrentModState(string directory, string name)
    {
        foreach (var mod in _modManager.GetInstalledMods(directory))
        {
            if (string.Equals(mod.Name, name, StringComparison.OrdinalIgnoreCase))
                return new CurrentMod { FileName = mod.FileName, DirectoryEnabled = !mod.DisabledFolder };
        }
        return null;
    }

    private bool ProfilesMatchesAfter(RollbackEntry entry)
        => SnapshotMatches(ProfilesPath, entry.After.ProfilesSha);

    // ============================== 方案定义类：定向撤销 ==============================

    private ModProfileStore LoadProfilesStore()
    {
        try
        {
            if (File.Exists(ProfilesPath))
            {
                var store = JsonSerializer.Deserialize<ModProfileStore>(
                    File.ReadAllText(ProfilesPath), ModProfileJson.Options);
                if (store is not null) return store;
            }
        }
        catch
        {
            // 解析失败按空处理，撤销会报告失败而不是写坏文件
        }
        return new ModProfileStore();
    }

    private void SaveProfilesStore(ModProfileStore store)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilesPath)!);
        ToysConfigStore.WriteAtomic(ProfilesPath, JsonSerializer.Serialize(store, ModProfileJson.Options));
    }

    private static ModProfile? FindProfile(ModProfileStore store, string? name)
    {
        var value = (name ?? "").Trim();
        if (value.Length == 0) return null;
        return (store.Profiles ?? new List<ModProfile>())
            .FirstOrDefault(p => string.Equals(p?.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    private static ModProfile? ParseProfile(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ModProfile>(json, ModProfileJson.Options); }
        catch { return null; }
    }

    private static string SerializeProfile(ModProfile profile)
        => JsonSerializer.Serialize(profile, ModProfileJson.Options);

    private static bool SameText(string? a, string? b)
        => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static void SetActive(ModProfileStore store, string? name)
    {
        var value = (name ?? "").Trim();
        store.Active = value.Length == 0 ? "" : (FindProfile(store, value)?.Name ?? "");
    }

    /// <summary>只有"当前方案"仍是这条操作设定的值时才还原它；用户后来手动换了方案就不动。</summary>
    private static void RestoreActiveIf(ModProfileStore store, string? expected, string? target)
    {
        if (!SameText(store.Active, expected)) return;
        SetActive(store, target);
    }

    /// <summary>只比较"这一条操作碰过的东西"：别的方案后来怎么改都不影响撤销这一条。</summary>
    private List<string> ProfileEditConflicts(RollbackProfileEdit edit)
    {
        var conflicts = new List<string>();
        var store = LoadProfilesStore();
        switch ((edit.Kind ?? "").ToLowerInvariant())
        {
            case "capture":
            {
                // 方案已经不在了 = 这条已经被撤过，不算冲突
                var profile = FindProfile(store, edit.Name);
                if (profile is not null && !SameText(SerializeProfile(profile), edit.ResultJson))
                    conflicts.Add($"方案「{edit.Name}」在操作之后被改过");
                break;
            }
            case "update":
            {
                var profile = FindProfile(store, edit.Name);
                if (profile is null) conflicts.Add($"方案「{edit.Name}」已经不存在");
                else if (!SameText(SerializeProfile(profile), edit.ResultJson))
                    conflicts.Add($"方案「{edit.Name}」在操作之后被改过");
                break;
            }
            case "rename":
            {
                var profile = FindProfile(store, edit.NewName);
                if (profile is null) conflicts.Add($"方案「{edit.NewName}」已经不存在");
                else if (!SameText(SerializeProfile(profile), edit.ResultJson))
                    conflicts.Add($"方案「{edit.NewName}」在操作之后被改过");
                if (FindProfile(store, edit.Name) is not null) conflicts.Add($"旧名字「{edit.Name}」已经被别的方案占用");
                break;
            }
            case "delete":
            {
                if (FindProfile(store, edit.Name) is not null) conflicts.Add($"已经又有一个叫「{edit.Name}」的方案了");
                break;
            }
            case "note":
            {
                var profile = FindProfile(store, edit.Name);
                if (profile is null) conflicts.Add($"方案「{edit.Name}」已经不存在");
                else if (!string.Equals(profile.Note ?? "", edit.ResultNote ?? "", StringComparison.Ordinal))
                    conflicts.Add($"方案「{edit.Name}」的备注在操作之后被改过");
                break;
            }
            case "active":
                if (!SameText(store.Active, edit.ResultActive)) conflicts.Add("当前方案标记在操作之后被改过");
                break;
        }
        return conflicts;
    }

    private void UndoProfileEdit(RollbackProfileEdit edit, bool force, RollbackResult result)
    {
        var store = LoadProfilesStore();
        switch ((edit.Kind ?? "").ToLowerInvariant())
        {
            case "capture":
            {
                var profile = FindProfile(store, edit.Name);
                if (profile is not null)
                {
                    store.Profiles.Remove(profile);
                    RestoreActiveIf(store, edit.ResultActive, edit.PreviousActive);
                }
                break;
            }
            case "update":
            {
                var profile = FindProfile(store, edit.Name);
                var previous = ParseProfile(edit.PreviousJson);
                if (profile is null || previous is null)
                {
                    result.Failed.Add($"方案「{edit.Name}」的定义无法恢复");
                    return;
                }
                store.Profiles[store.Profiles.IndexOf(profile)] = previous;
                RestoreActiveIf(store, edit.ResultActive, edit.PreviousActive);
                break;
            }
            case "rename":
            {
                var profile = FindProfile(store, edit.NewName);
                if (profile is null)
                {
                    result.Failed.Add($"方案「{edit.NewName}」已经不存在");
                    return;
                }
                profile.Name = edit.Name;
                if (SameText(store.Active, edit.NewName)) store.Active = edit.Name;
                break;
            }
            case "delete":
            {
                if (FindProfile(store, edit.Name) is not null)
                {
                    result.Failed.Add($"同名方案「{edit.Name}」已经存在，无法恢复");
                    return;
                }
                var previous = ParseProfile(edit.PreviousJson);
                if (previous is null)
                {
                    result.Failed.Add($"方案「{edit.Name}」的备份无法解析");
                    return;
                }
                var index = Math.Clamp(edit.Index, 0, store.Profiles.Count);
                store.Profiles.Insert(index, previous);
                RestoreActiveIf(store, edit.ResultActive, edit.PreviousActive);
                break;
            }
            case "note":
            {
                var profile = FindProfile(store, edit.Name);
                if (profile is null)
                {
                    result.Failed.Add($"方案「{edit.Name}」已经不存在");
                    return;
                }
                profile.Note = edit.PreviousNote ?? "";
                break;
            }
            case "active":
                SetActive(store, edit.PreviousActive);
                break;
            default:
                result.Failed.Add($"未知的方案操作类型：{edit.Kind}");
                return;
        }

        try
        {
            SaveProfilesStore(store);
        }
        catch (Exception ex)
        {
            result.Failed.Add($"写回方案文件失败：{ex.Message}");
            return;
        }

        // 回读校验：撤销后的目标状态必须真的成立
        var verify = LoadProfilesStore();
        switch ((edit.Kind ?? "").ToLowerInvariant())
        {
            case "capture":
                if (FindProfile(verify, edit.Name) is not null)
                    result.Failed.Add($"方案「{edit.Name}」没有被删掉");
                break;
            case "update":
                if (!SameText(SerializeProfile(FindProfile(verify, edit.Name) ?? new ModProfile()), edit.PreviousJson))
                    result.Failed.Add($"方案「{edit.Name}」没有恢复成操作前的定义");
                break;
            case "rename":
                if (FindProfile(verify, edit.Name) is null || FindProfile(verify, edit.NewName) is not null)
                    result.Failed.Add($"方案「{edit.Name}」没有恢复原名");
                break;
            case "delete":
                if (FindProfile(verify, edit.Name) is null)
                    result.Failed.Add($"方案「{edit.Name}」没有恢复");
                break;
            case "note":
                if (!string.Equals(FindProfile(verify, edit.Name)?.Note ?? "", edit.PreviousNote ?? "", StringComparison.Ordinal))
                    result.Failed.Add($"方案「{edit.Name}」的备注没有恢复");
                break;
            case "active":
                if (!SameText(verify.Active, edit.PreviousActive))
                    result.Failed.Add("当前方案标记没有恢复");
                break;
        }

        if (result.Failed.Count == 0) result.Restored.Add("模组方案");
    }

    private static bool SnapshotMatches(string? path, string? expectedSha, bool ignoreAccountFields = false)
    {
        var exists = !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        if (string.IsNullOrEmpty(expectedSha)) return !exists;
        if (!exists) return false;
        try
        {
            return string.Equals(
                ignoreAccountFields ? Sha256IgnoringAccountFields(path!) : Sha256For(path!),
                expectedSha,
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private bool RestoreFile(
        RollbackFileSnapshot snapshot,
        string path,
        string label,
        RollbackResult result,
        bool verify = true)
    {
        try
        {
            if (snapshot.Existed)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                var bytes = snapshot.GetBytes();
                if (File.Exists(path)) File.SetAttributes(path, FileAttributes.Normal);
                File.WriteAllBytes(path, bytes);
            }
            else if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }

            if (verify && !snapshot.MatchesCurrent(path))
            {
                result.Failed.Add($"{label}还原后校验不一致");
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            result.Failed.Add($"{label}：{ex.Message}");
            return false;
        }
    }

    private void RestoreMod(string directory, RollbackModSnapshot snapshot, RollbackResult result)
    {
        var current = CurrentModState(directory, snapshot.Name);
        if (current is null)
        {
            result.Failed.Add($"模组「{snapshot.Name}」已经不存在，无法还原");
            return;
        }

        // 1) 目录位置：只搬目录，不碰 sidecar（下面按原文还原）
        if (current.DirectoryEnabled != snapshot.DirectoryEnabled)
        {
            if (!_modManager.SetModDirectoryEnabled(directory, current.FileName, snapshot.DirectoryEnabled))
            {
                result.Failed.Add($"模组「{snapshot.Name}」目录搬移失败");
                return;
            }
        }

        // 搬完之后路径可能变了（mods <-> disabled_mods），重新解析
        current = CurrentModState(directory, snapshot.Name);
        if (current is null)
        {
            result.Failed.Add($"模组「{snapshot.Name}」搬移后找不到");
            return;
        }

        if (snapshot.Sidecar is not null &&
            !RestoreFile(snapshot.Sidecar, _modManager.ModSidecarPath(directory, current.FileName) ?? "", "sidecar", result))
            return;

        if (snapshot.Config is not null &&
            !RestoreFile(snapshot.Config, _modManager.ModConfigWritePathFor(directory, current.FileName), "配置", result))
            return;

        // 模组目录里那份 config.json 也要按原文还原（当前配置双写，两边内容一致）
        if (snapshot.PackagedConfig is not null)
        {
            var packagedPath = _modManager.ModPackagedConfigPathFor(directory, current.FileName);
            if (packagedPath is not null &&
                !RestoreFile(snapshot.PackagedConfig, packagedPath, "模组目录配置", result))
                return;
        }

        result.Restored.Add($"模组「{snapshot.Name}」");
    }

    internal string LoaderConfigPath(string gameDirectory)
        => Path.Combine(gameDirectory, ModManager.LoaderFolderName, ModManager.ConfigFileName);

    internal static string Sha256For(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    // ---- 账号档案字段（profile / profileWindowTitle）在"方案类"回滚里要保留当前值 ----

    private static readonly Regex ProfileValueRegex = new(
        """("profile"\s*:\s*)("(?:[^"\\]|\\.)*")""", RegexOptions.Compiled);

    private static readonly Regex ProfileWindowTitleRegex = new(
        """("profileWindowTitle"\s*:\s*)(true|false)""", RegexOptions.Compiled);

    private static string TextOf(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static string Sha256OfText(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    /// <summary>把多开档案的两个字段统一成固定值，用于"忽略账号选择"的配置比较。</summary>
    internal static string NormalizeAccountFields(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var normalized = ProfileValueRegex.Replace(text, "$1\"\"");
        return ProfileWindowTitleRegex.Replace(normalized, "$1true");
    }

    private static string Sha256IgnoringAccountFields(string path)
        => Sha256OfText(NormalizeAccountFields(TextOf(File.ReadAllBytes(path))));

    internal static string NewId(string kind)
    {
        var now = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var safeKind = new string((kind ?? "").Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        if (safeKind.Length > 32) safeKind = safeKind[..32];
        return $"{now}-{safeKind}-{suffix}";
    }

    private IEnumerable<string> SafeEnumerate()
    {
        try
        {
            if (!Directory.Exists(Root)) return Array.Empty<string>();
            return Directory.EnumerateFiles(Root, "*.json").OrderBy(p => p, StringComparer.Ordinal).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private RollbackEntry? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var entry = JsonSerializer.Deserialize<RollbackEntry>(File.ReadAllText(path), JsonOptions);
            return string.IsNullOrWhiteSpace(entry?.Id) ? null : entry;
        }
        catch
        {
            return null;
        }
    }

    internal void TryWrite(RollbackEntry entry)
    {
        try
        {
            Directory.CreateDirectory(Root);
            ToysConfigStore.WriteAtomic(EntryPath(entry.Id), JsonSerializer.Serialize(entry, JsonOptions));
        }
        catch (Exception ex)
        {
            ToysLog.Error("mods", $"写入回滚记录失败：{entry.Id}", ex);
        }
    }

    /// <summary>保留最近 <see cref="MaxEntries"/> 条 / <see cref="MaxAgeDays"/> 天 / 合计 <see cref="MaxTotalBytes"/>。</summary>
    internal void Prune()
    {
        try
        {
            var entries = new List<(string Path, RollbackEntry? Entry, DateTime When, long Size)>();
            foreach (var path in SafeEnumerate())
            {
                long size = 0;
                try { size = new FileInfo(path).Length; } catch { }
                var entry = TryRead(path);
                var when = DateTime.TryParse(entry?.CreatedAtUtc, out var parsed) ? parsed : DateTime.MinValue;
                entries.Add((path, entry, when, size));
            }

            var keep = entries
                .OrderByDescending(e => e.When)
                .ThenBy(e => e.Path, StringComparer.Ordinal)
                .ToList();

            var cutoff = DateTime.UtcNow.AddDays(-MaxAgeDays);
            long total = 0;
            for (var i = 0; i < keep.Count; i++)
            {
                var item = keep[i];
                total += item.Size;
                var expired = i >= MaxEntries || item.When < cutoff || total > MaxTotalBytes;
                if (!expired) continue;
                try { File.Delete(item.Path); } catch { /* 删不掉就留着，下次再试 */ }
            }
        }
        catch (Exception ex)
        {
            ToysLog.Warn("mods", $"清理回滚记录失败：{ex.Message}");
        }
    }

    /// <summary>一次操作的句柄：Commit 才补"操作后"观测值，没有 Commit 就 Dispose 会删掉半截记录。</summary>
    public sealed class RollbackOperation : IDisposable
    {
        private readonly ModRollbackJournal _owner;
        private readonly RollbackEntry _entry;
        private bool _committed;

        internal RollbackOperation(ModRollbackJournal owner, RollbackEntry entry)
        {
            _owner = owner;
            _entry = entry;
        }

        public string Id => _entry.Id;

        /// <summary>方案定义类操作：登记"这一条改了什么"，回滚时只撤销这一条。</summary>
        public void SetProfileEdit(RollbackProfileEdit edit) => _entry.ProfileEdit = edit;

        /// <summary>操作成功：记录"操作后"的观测值，供回滚前的冲突检测使用。</summary>
        public void Commit()
        {
            if (_committed) return;
            lock (Gate)
            {
                try
                {
                    _entry.After = _owner.CaptureAfter(_entry);
                    _entry.Completed = true;
                    _owner.TryWrite(_entry);
                    _owner.Prune();
                }
                catch (Exception ex)
                {
                    ToysLog.Error("mods", $"写入回滚记录失败：{_entry.Id}", ex);
                }
                _committed = true;
            }
        }

        /// <summary>操作失败 / 没有改动任何文件：删掉记录，别让历史里多一条空操作。</summary>
        public void Discard()
        {
            if (_committed) return;
            _committed = true;
            try
            {
                var path = _owner.EntryPath(_entry.Id);
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 删不掉只影响历史列表，不影响正确性
            }
        }

        public void Dispose()
        {
            if (!_committed) Discard();
        }
    }

    private RollbackAfterState CaptureAfter(RollbackEntry entry)
    {
        var after = new RollbackAfterState();
        if (entry.Profiles is not null)
            after.ProfilesSha = File.Exists(ProfilesPath) ? Sha256For(ProfilesPath) : null;
        if (entry.LoaderConfig is not null && entry.GameDirectory.Length > 0)
        {
            var loaderPath = LoaderConfigPath(entry.GameDirectory);
            after.LoaderConfigSha = !File.Exists(loaderPath)
                ? null
                : entry.PreserveAccountFields
                    ? Sha256IgnoringAccountFields(loaderPath)
                    : Sha256For(loaderPath);
        }
        if (entry.Mods.Count > 0 && Directory.Exists(entry.GameDirectory))
        {
            foreach (var name in entry.Mods.Select(m => m.Name))
            {
                var current = CurrentModState(entry.GameDirectory, name);
                if (current is null) continue;
                var sidecar = _modManager.ModSidecarPath(entry.GameDirectory, current.FileName);
                var config = _modManager.ModConfigWritePathFor(entry.GameDirectory, current.FileName);
                var packagedConfig = _modManager.ModPackagedConfigPathFor(entry.GameDirectory, current.FileName);
                after.Mods[name] = new RollbackModAfter
                {
                    DirectoryEnabled = current.DirectoryEnabled,
                    SidecarSha = !string.IsNullOrWhiteSpace(sidecar) && File.Exists(sidecar) ? Sha256For(sidecar!) : null,
                    ConfigSha = File.Exists(config) ? Sha256For(config) : null,
                    PackagedConfigSha = packagedConfig is not null && File.Exists(packagedConfig)
                        ? Sha256For(packagedConfig)
                        : null
                };
            }
        }
        return after;
    }
}
