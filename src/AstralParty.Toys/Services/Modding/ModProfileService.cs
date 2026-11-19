using System.Text.Json;
using System.Text.RegularExpressions;
using AstralParty.Toys.Models;

namespace AstralParty.Toys.Services.Modding;

/// <summary>
/// 模组方案（全局共享）：捕获当前启用状态 + 每个模组的 config.json 原文 + 加载器设置子集，
/// 以后一键套用；每次写操作都经 <see cref="ModRollbackJournal"/> 记录，可无损回滚。
///
/// 纯文件逻辑（不含弹窗 / 消息），便于单元测试。方案文件位于
/// <c>&lt;数据根&gt;\state\mod-profiles.json</c>，不放进游戏的 <c>AstralParty_ModLoader</c>
/// （完整卸载会删掉整个目录，方案不该跟着丢）。
/// </summary>
public sealed partial class ModProfileService
{
    private static readonly object Gate = new();
    private static readonly Regex NameRegex = new(NamePattern, RegexOptions.CultureInvariant);

    private readonly ModManager _modManager;
    private readonly ModRollbackJournal _journal;

    public ModProfileService(ModManager modManager, string? gameDirectory = null)
    {
        _modManager = modManager;
        GameDirectory = (gameDirectory ?? "").Trim();
        _journal = new ModRollbackJournal(modManager);
    }

    /// <summary>当前游戏目录（可为空：只能看方案列表，不能捕获 / 套用）。</summary>
    public string GameDirectory { get; }

    public string ProfilesPath => GetStorePath(_modManager.ProfileDirectory);

    public ModRollbackJournal Journal => _journal;

    // ============================== 读取 ==============================

    public ModProfileStore Load()
    {
        lock (Gate) return LoadCore();
    }

    private ModProfileStore LoadCore()
    {
        var store = ReadStore(ProfilesPath);
        if (store is null)
        {
            if (File.Exists(ProfilesPath))
                ToysLog.Warn("mods", $"方案文件无法解析，已按空列表处理：{ProfilesPath}");
            return new ModProfileStore();
        }
        return store;
    }

    public List<ModProfileSummary> List()
    {
        lock (Gate)
        {
            var store = LoadCore();
            return Summarize(store, GameDirectory, _modManager.GetInstalledMods(GameDirectory), ReadSummaryLoaderConfig());
        }
    }

    public ModProfileSummary? Find(string name)
    {
        lock (Gate)
        {
            var store = LoadCore();
            var profile = FindCore(store, name);
            return profile is null ? null : Summary(profile, store.Active, CurrentMods());
        }
    }

    // ============================== 定义类操作（只改方案文件） ==============================

    public ModProfileSummary Capture(string name, string? note, bool includeConfigs, bool includeLoader)
    {
        var trimmed = (name ?? "").Trim();
        EnsureValidName(trimmed);
        if (string.IsNullOrWhiteSpace(GameDirectory) || !Directory.Exists(GameDirectory))
            throw new InvalidOperationException("还没有选择有效的游戏目录，无法保存方案。");

        lock (Gate)
        {
            var store = LoadCore();
            if (FindCore(store, trimmed) is not null)
                throw new InvalidOperationException($"已经有一个叫「{trimmed}」的方案了。");

            using var op = ProfilesOperation("modProfileCapture", $"保存方案「{trimmed}」");
            var previousActive = store.Active;
            var profile = BuildProfile(trimmed, includeConfigs, includeLoader);
            profile.Note = ClampNote(note);
            store.Profiles.Add(profile);
            store.Active = profile.Name;   // 刚捕获的就是当前状态，直接算当前方案
            Save(store);
            op.SetProfileEdit(new RollbackProfileEdit
            {
                Kind = "capture",
                Name = profile.Name,
                ResultJson = SerializeProfile(profile),
                PreviousActive = previousActive,
                ResultActive = profile.Name
            });
            op.Commit();
            ToysLog.Info("mods", $"保存模组方案「{trimmed}」（{profile.Mods.Count} 个模组）");
            return Summary(profile, store.Active, CurrentMods());
        }
    }

    public ModProfileSummary Update(string name, bool includeConfigs, bool includeLoader)
    {
        if (string.IsNullOrWhiteSpace(GameDirectory) || !Directory.Exists(GameDirectory))
            throw new InvalidOperationException("还没有选择有效的游戏目录，无法更新方案。");
        lock (Gate)
        {
            var store = LoadCore();
            var existing = FindCore(store, name) ?? throw new InvalidOperationException($"找不到方案「{name}」。");

            using var op = ProfilesOperation("modProfileUpdate", $"更新方案「{existing.Name}」");
            var previousActive = store.Active;
            var previousJson = SerializeProfile(existing);
            var rebuilt = BuildProfile(existing.Name, includeConfigs, includeLoader);
            rebuilt.Note = existing.Note;
            rebuilt.CreatedAtUtc = existing.CreatedAtUtc;
            rebuilt.CapturedFrom = GameDirectory;
            var index = store.Profiles.IndexOf(existing);
            store.Profiles[index] = rebuilt;
            if (string.Equals(store.Active, existing.Name, StringComparison.OrdinalIgnoreCase))
                store.Active = rebuilt.Name;
            Save(store);
            op.SetProfileEdit(new RollbackProfileEdit
            {
                Kind = "update",
                Name = rebuilt.Name,
                PreviousJson = previousJson,
                ResultJson = SerializeProfile(rebuilt),
                PreviousActive = previousActive,
                ResultActive = store.Active
            });
            op.Commit();
            ToysLog.Info("mods", $"更新模组方案「{rebuilt.Name}」");
            return Summary(rebuilt, store.Active, CurrentMods());
        }
    }

    public void Rename(string name, string newName)
    {
        var trimmed = (newName ?? "").Trim();
        EnsureValidName(trimmed);

        lock (Gate)
        {
            var store = LoadCore();
            var profile = FindCore(store, name) ?? throw new InvalidOperationException($"找不到方案「{name}」。");
            if (string.Equals(profile.Name, trimmed, StringComparison.Ordinal))
                return;
            if (FindCore(store, trimmed) is not null)
                throw new InvalidOperationException($"已经有一个叫「{trimmed}」的方案了。");

            using var op = ProfilesOperation("modProfileRename", $"重命名方案「{profile.Name}」→「{trimmed}」");
            var oldName = profile.Name;
            var previousActive = store.Active;
            var previousJson = SerializeProfile(profile);
            var wasActive = string.Equals(store.Active, profile.Name, StringComparison.OrdinalIgnoreCase);
            profile.Name = trimmed;
            profile.UpdatedAtUtc = Now();
            if (wasActive) store.Active = trimmed;
            Save(store);
            op.SetProfileEdit(new RollbackProfileEdit
            {
                Kind = "rename",
                Name = oldName,
                NewName = trimmed,
                PreviousJson = previousJson,
                ResultJson = SerializeProfile(profile),
                PreviousActive = previousActive,
                ResultActive = store.Active
            });
            op.Commit();
            ToysLog.Info("mods", $"重命名模组方案「{name}」→「{trimmed}」");
        }
    }

    public void Delete(string name)
    {
        lock (Gate)
        {
            var store = LoadCore();
            var profile = FindCore(store, name) ?? throw new InvalidOperationException($"找不到方案「{name}」。");

            using var op = ProfilesOperation("modProfileDelete", $"删除方案「{profile.Name}」");
            var previousActive = store.Active;
            var deletedJson = SerializeProfile(profile);
            var index = store.Profiles.IndexOf(profile);
            var deletedName = profile.Name;
            store.Profiles.Remove(profile);
            if (string.Equals(store.Active, deletedName, StringComparison.OrdinalIgnoreCase))
                store.Active = "";
            Save(store);
            op.SetProfileEdit(new RollbackProfileEdit
            {
                Kind = "delete",
                Name = deletedName,
                PreviousJson = deletedJson,
                Index = index,
                PreviousActive = previousActive,
                ResultActive = store.Active
            });
            op.Commit();
            ToysLog.Info("mods", $"删除模组方案「{profile.Name}」");
        }
    }

    public void SetNote(string name, string? note)
    {
        lock (Gate)
        {
            var store = LoadCore();
            var profile = FindCore(store, name) ?? throw new InvalidOperationException($"找不到方案「{name}」。");
            var text = ClampNote(note);
            if (string.Equals(profile.Note, text, StringComparison.Ordinal)) return;

            using var op = ProfilesOperation("modProfileNote", $"修改方案「{profile.Name}」的备注");
            var previousNote = profile.Note;
            profile.Note = text;
            profile.UpdatedAtUtc = Now();
            Save(store);
            op.SetProfileEdit(new RollbackProfileEdit
            {
                Kind = "note",
                Name = profile.Name,
                PreviousNote = previousNote,
                ResultNote = text
            });
            op.Commit();
        }
    }

    /// <summary>清掉"当前方案"标记（不改磁盘上的模组）。</summary>
    public void ClearActive()
    {
        lock (Gate)
        {
            var store = LoadCore();
            if (string.IsNullOrEmpty(store.Active)) return;

            using var op = ProfilesOperation("modProfileClearActive", "取消当前模组方案");
            var previousActive = store.Active;
            store.Active = "";
            Save(store);
            op.SetProfileEdit(new RollbackProfileEdit
            {
                Kind = "active",
                PreviousActive = previousActive,
                ResultActive = ""
            });
            op.Commit();
        }
    }

    // ============================== 套用 ==============================

    public ModProfilePlan Plan(string name)
    {
        lock (Gate)
        {
            var store = LoadCore();
            var profile = FindCore(store, name) ?? throw new InvalidOperationException($"找不到方案「{name}」。");
            return BuildPlan(profile, CurrentMods());
        }
    }

    public ModProfilePlan PlanFor(ModProfile profile)
    {
        lock (Gate) return BuildPlan(profile, CurrentMods());
    }

    public ModProfileApplyResult Apply(string name)
    {
        lock (Gate)
        {
            var store = LoadCore();
            var profile = FindCore(store, name) ?? throw new InvalidOperationException($"找不到方案「{name}」。");
            var plan = BuildPlan(profile, CurrentMods());
            var result = new ModProfileApplyResult();

            if (plan.Blocked)
            {
                result.Message = plan.BlockReason;
                return result;
            }

            var previousActive = store.Active;
            using var op = _journal.Begin(
                "modProfileApply",
                $"套用方案「{profile.Name}」",
                GameDirectory,
                includeMods: true,
                includeLoader: profile.IncludesLoader,
                includeProfiles: false,   // 方案定义用定向撤销，不整文件覆盖
                preserveAccountFields: true);   // 方案本来就不改多开档案，回滚时保留当前选择

            try
            {
                // 1) 启用 / 停用：走既有 ToggleMods（会同时改目录与 sidecar）
                if (plan.Enable.Count > 0)
                {
                    var (ok, failed) = _modManager.ToggleMods(GameDirectory, plan.Enable, enabled: true);
                    if (ok > 0) result.Applied.Add($"启用 {ok} 个模组");
                    foreach (var f in failed) result.Failed.Add($"启用失败：{f}");
                }
                if (plan.Disable.Count > 0)
                {
                    var (ok, failed) = _modManager.ToggleMods(GameDirectory, plan.Disable, enabled: false);
                    if (ok > 0) result.Applied.Add($"停用 {ok} 个模组");
                    foreach (var f in failed) result.Failed.Add($"停用失败：{f}");
                }

                // 2) 配置原文（必须在搬完目录之后写，路径以新位置为准）
                if (profile.IncludesConfigs)
                {
                    var current = CurrentMods();
                    var written = 0;
                    foreach (var mod in profile.Mods)
                    {
                        if (mod.Config is null) continue;
                        if (!current.TryGetValue(mod.Name, out var entry))
                        {
                            result.Skipped.Add($"缺少模组 {mod.Name}（配置未写入）");
                            continue;
                        }
                        try
                        {
                            _modManager.WriteModConfigText(GameDirectory, entry.FileName, mod.Config, mod.ConfigHasBom);
                            written++;
                        }
                        catch (Exception ex)
                        {
                            result.Failed.Add($"写入 {mod.Name} 配置失败：{ex.Message}");
                        }
                    }
                    if (written > 0) result.Applied.Add($"写入 {written} 份模组配置");
                }

                // 3) 加载器设置（保留 enabled / profile / profileWindowTitle）
                if (profile.IncludesLoader)
                {
                    var loaderPath = _modManager.LoaderConfigPath(GameDirectory);
                    if (File.Exists(loaderPath))
                    {
                        var config = _modManager.ReadLoaderConfig(GameDirectory);
                        profile.Loader.ApplyTo(config);
                        _modManager.SaveLoaderConfig(GameDirectory, config);
                        result.Applied.Add("加载器设置");
                    }
                    else
                    {
                        result.Skipped.Add("加载器配置不存在（跳过加载器设置）");
                    }
                }

                foreach (var missing in plan.Missing.Distinct(StringComparer.OrdinalIgnoreCase))
                    result.Skipped.Add($"缺少模组 {missing}（未安装，已跳过）");

                // 4) 全部成功才把方案记为"当前"
                if (result.Failed.Count == 0)
                {
                    store.Active = profile.Name;
                    Save(store);
                    if (!string.Equals(previousActive, store.Active, StringComparison.OrdinalIgnoreCase))
                    {
                        op.SetProfileEdit(new RollbackProfileEdit
                        {
                            Kind = "active",
                            Name = profile.Name,
                            PreviousActive = previousActive,
                            ResultActive = store.Active
                        });
                    }
                    result.Ok = true;
                    result.Message = plan.HasChanges
                        ? $"已套用方案「{profile.Name}」，重启游戏后生效。"
                        : $"当前已经是方案「{profile.Name}」的状态。";
                    ToysLog.Info("mods", $"套用模组方案「{profile.Name}」完成");
                }
                else
                {
                    result.Message = "方案没有完全套用，失败项见下；可以用「历史与回滚」还原。";
                    ToysLog.Warn("mods", $"套用模组方案「{profile.Name}」有 {result.Failed.Count} 项失败");
                }
            }
            finally
            {
                // 已经动过文件：无论成败都保留回滚记录
                op.Commit();
            }
            return result;
        }
    }

    // ============================== 内部 ==============================

    private ModRollbackJournal.RollbackOperation ProfilesOperation(string kind, string title)
        => _journal.Begin(kind, title, GameDirectory, includeMods: false, includeLoader: false, includeProfiles: false);

    private static string SerializeProfile(ModProfile profile)
        => JsonSerializer.Serialize(profile, ModProfileJson.Options);

    private static string Now() => DateTime.UtcNow.ToString("o");

    private static ModProfile? FindCore(ModProfileStore store, string? name)
    {
        var value = (name ?? "").Trim();
        if (value.Length == 0 || store.Profiles is null) return null;
        return store.Profiles.FirstOrDefault(p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase));
    }

    private void Save(ModProfileStore store)
    {
        Normalize(store);
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilesPath)!);
        ToysConfigStore.WriteAtomic(ProfilesPath, JsonSerializer.Serialize(store, ModProfileJson.Options));
    }

    private Dictionary<string, ModEntryInfo> CurrentMods() => IndexMods(_modManager.GetInstalledMods(GameDirectory));


    private (string? Text, bool HasBom) ReadConfigState(string fileName) => ReadConfigState(GameDirectory, fileName);

    private ModProfile BuildProfile(string name, bool includeConfigs, bool includeLoader)
    {
        var now = Now();
        var profile = new ModProfile
        {
            Name = name,
            CapturedFrom = GameDirectory,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IncludesConfigs = includeConfigs,
            IncludesLoader = false
        };

        foreach (var mod in CurrentMods().Values.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
        {
            var (text, hasBom) = includeConfigs ? ReadConfigState(mod.FileName) : (null, false);
            profile.Mods.Add(new ModProfileMod
            {
                Name = mod.Name,
                Id = mod.Id,
                Version = mod.Version,
                Enabled = EffectiveEnabled(mod),
                Config = text,
                ConfigHasBom = hasBom
            });
        }

        if (includeLoader && !string.IsNullOrWhiteSpace(GameDirectory))
        {
            var loaderPath = _modManager.LoaderConfigPath(GameDirectory);
            if (File.Exists(loaderPath))
            {
                profile.Loader = ModProfileLoaderSettings.From(_modManager.ReadLoaderConfig(GameDirectory));
                profile.IncludesLoader = true;
            }
        }
        return profile;
    }

    private ModProfileSummary Summary(ModProfile profile, string active, Dictionary<string, ModEntryInfo> mods)
    {
        var gameExists = Directory.Exists(GameDirectory);
        var loaderExists = gameExists && Directory.Exists(Path.Combine(GameDirectory, ModManager.LoaderFolderName));
        var loader = loaderExists && profile.IncludesLoader ? ReadSummaryLoaderConfig() : null;
        return SummarizeProfile(profile, active, mods, GameDirectory, gameExists, loaderExists, loader);
    }

    private LoaderConfig? ReadSummaryLoaderConfig()
        => File.Exists(_modManager.LoaderConfigPath(GameDirectory)) ? _modManager.ReadLoaderConfig(GameDirectory) : null;

    private ModProfilePlan BuildPlan(ModProfile profile, Dictionary<string, ModEntryInfo> mods)
    {
        var plan = new ModProfilePlan { Name = profile.Name };

        if (string.IsNullOrWhiteSpace(GameDirectory) || !Directory.Exists(GameDirectory))
        {
            plan.Blocked = true;
            plan.BlockReason = "还没有选择游戏目录，先到「设置 → 游戏目录」添加。";
            plan.Details.Add(plan.BlockReason);
            return plan;
        }
        if (!Directory.Exists(Path.Combine(GameDirectory, ModManager.LoaderFolderName)))
        {
            plan.Blocked = true;
            plan.BlockReason = "这个游戏目录里没有加载器（AstralParty_ModLoader），先安装加载器再套用方案。";
            plan.Details.Add(plan.BlockReason);
            return plan;
        }
        if (SpeedhackManager.IsGameRunning())
        {
            plan.Blocked = true;
            plan.BlockReason = "游戏正在运行：先完全退出游戏再套用方案（模组目录会被占用）。";
            plan.Details.Add(plan.BlockReason);
            return plan;
        }

        foreach (var mod in profile.Mods)
        {
            if (!mods.TryGetValue(mod.Name, out var current))
            {
                plan.Missing.Add(mod.Name);
                continue;
            }
            if (EffectiveEnabled(current) != mod.Enabled)
            {
                // 启停接口按 DLL 文件名定位（mods\<Id>\<Id>.dll），这里存文件名，展示时再去掉扩展名
                if (mod.Enabled) plan.Enable.Add(current.FileName);
                else plan.Disable.Add(current.FileName);
            }
            if (profile.IncludesConfigs && mod.Config is not null)
            {
                var (text, hasBom) = ReadConfigState(current.FileName);
                if (!string.Equals(text, mod.Config, StringComparison.Ordinal) || hasBom != mod.ConfigHasBom)
                    plan.Configs.Add(mod.Name);
            }
        }

        if (profile.IncludesLoader)
        {
            var loaderPath = _modManager.LoaderConfigPath(GameDirectory);
            if (File.Exists(loaderPath))
                plan.LoaderChanges = profile.Loader.DescribeDifferences(_modManager.ReadLoaderConfig(GameDirectory));
        }

        plan.EnableCount = plan.Enable.Count;
        plan.DisableCount = plan.Disable.Count;
        plan.ConfigWriteCount = plan.Configs.Count;
        plan.LoaderChangeCount = plan.LoaderChanges.Count;
        plan.HasChanges = plan.EnableCount + plan.DisableCount + plan.ConfigWriteCount + plan.LoaderChangeCount > 0;

        if (plan.EnableCount > 0) plan.Details.Add($"启用 {plan.EnableCount} 个模组：{JoinNames(plan.Enable.Select(PrettyName))}");
        if (plan.DisableCount > 0) plan.Details.Add($"停用 {plan.DisableCount} 个模组：{JoinNames(plan.Disable.Select(PrettyName))}");
        if (plan.ConfigWriteCount > 0) plan.Details.Add($"改写 {plan.ConfigWriteCount} 个模组的配置");
        if (plan.LoaderChangeCount > 0) plan.Details.Add($"加载器设置有 {plan.LoaderChangeCount} 项会变：{JoinNames(plan.LoaderChanges)}");
        if (plan.Missing.Count > 0) plan.Details.Add($"有 {plan.Missing.Count} 个模组没装，会被跳过：{JoinNames(plan.Missing)}");
        if (!plan.HasChanges && plan.Missing.Count == 0) plan.Details.Add("当前已经是这个方案的状态，没有需要改动的项。");

        return plan;
    }

    private static string JoinNames(IEnumerable<string> names, int max = 8)
    {
        var list = names.ToList();
        var text = string.Join("、", list.Take(max));
        return list.Count > max ? text + $" 等 {list.Count} 个" : text;
    }

    private static string PrettyName(string fileName)
        => string.IsNullOrEmpty(fileName) ? fileName : Path.GetFileNameWithoutExtension(fileName);

    public static bool IsValidName(string? name, out string error)
    {
        var value = (name ?? "").Trim();
        if (value.Length == 0)
        {
            error = "请填写方案名称。";
            return false;
        }
        if (value.Length > MaxNameLength)
        {
            error = $"方案名称最多 {MaxNameLength} 个字符。";
            return false;
        }
        if (!NameRegex.IsMatch(value))
        {
            error = "方案名称只能用文字、数字、空格、下划线和连字符。";
            return false;
        }
        error = "";
        return true;
    }

    private static void EnsureValidName(string name)
    {
        if (!IsValidName(name, out var error)) throw new InvalidOperationException(error);
    }

}
