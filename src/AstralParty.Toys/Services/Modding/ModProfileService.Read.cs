using System.Text;
using System.Text.Json;
using AstralParty.Toys.Models;
using AstralParty.Toys.Services.Common;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class ModProfileService
{
    public const string ProfilesFileName = "mod-profiles.json";

    /// <summary>方案名规则（唯一来源：宿主把同一个正则下发给页面做实时校验）。
    /// 允许中文、字母、数字、空格、下划线与连字符，1–32 个字符。</summary>
    public const string NamePattern = @"^[\p{L}\p{N} _-]{1,32}$";

    public const int MaxNameLength = 32;
    public const int MaxNoteLength = 256;

    internal static ModProfileStore? ReadStore(string path)
    {
        var store = TryRead(path) ?? TryRead(path + ".bak");
        if (store is not null) Normalize(store);
        return store;
    }

    internal static string GetStorePath(string dataDirectory) =>
        Path.Combine(dataDirectory, ToysPaths.StateFolderName, ProfilesFileName);

    private static ModProfileStore? TryRead(string path)
    {
        try
        {
            var text = InspectionFiles.ReadText(path, maxBytes: 16 * 1024 * 1024);
            return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<ModProfileStore>(text, ModProfileJson.Options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    internal static List<ModProfileSummary> Summarize(ModProfileStore store, string directory,
        IEnumerable<ModEntryInfo> mods, LoaderConfig? loaderConfig)
    {
        var map = IndexMods(mods);
        var gameExists = InspectionFiles.DirectoryExists(directory);
        var loaderExists = gameExists && InspectionFiles.DirectoryExists(Path.Combine(directory, ModManager.LoaderFolderName));
        var summaries = new List<ModProfileSummary>(store.Profiles.Count);
        foreach (var profile in store.Profiles)
            summaries.Add(SummarizeProfile(profile, store.Active, map, directory, gameExists, loaderExists, loaderConfig));
        return summaries;
    }

    private static Dictionary<string, ModEntryInfo> IndexMods(IEnumerable<ModEntryInfo> mods)
    {
        var map = new Dictionary<string, ModEntryInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods)
            if (!string.IsNullOrWhiteSpace(mod.Name)) map[mod.Name] = mod;
        return map;
    }

    private static ModProfileSummary SummarizeProfile(ModProfile profile, string active, Dictionary<string, ModEntryInfo> mods,
        string directory, bool gameExists, bool loaderExists, LoaderConfig? loaderConfig)
    {
        var missing = 0;
        foreach (var mod in profile.Mods)
            if (!mods.ContainsKey(mod.Name)) missing++;

        var problem = !gameExists ? "还没有选择游戏目录。"
            : !loaderExists ? "这个游戏目录里没有加载器。"
            : profile.Mods.Count == 0 && !profile.IncludesLoader ? "方案里没有记录任何内容。" : "";
        return new ModProfileSummary
        {
            Name = profile.Name,
            Note = profile.Note,
            UpdatedAtUtc = profile.UpdatedAtUtc,
            Active = string.Equals(active, profile.Name, StringComparison.OrdinalIgnoreCase),
            ModCount = profile.Mods.Count,
            EnabledCount = profile.Mods.Count(m => m.Enabled),
            MissingCount = missing,
            Dirty = problem.Length == 0 && IsDirty(profile, mods, directory, loaderConfig),
            IncludesLoader = profile.IncludesLoader,
            IncludesConfigs = profile.IncludesConfigs,
            Problem = problem
        };
    }

    private static bool IsDirty(ModProfile profile, Dictionary<string, ModEntryInfo> mods, string directory, LoaderConfig? loaderConfig)
    {
        foreach (var mod in profile.Mods)
        {
            if (!mods.TryGetValue(mod.Name, out var current)) continue;
            if (EffectiveEnabled(current) != mod.Enabled) return true;
            if (profile.IncludesConfigs && mod.Config is not null)
            {
                var (text, hasBom) = ReadConfigState(directory, current.FileName);
                if (!string.Equals(text, mod.Config, StringComparison.Ordinal) || hasBom != mod.ConfigHasBom)
                    return true;
            }
        }
        return profile.IncludesLoader && loaderConfig is not null && profile.Loader.DescribeDifferences(loaderConfig).Count > 0;
    }

    private static (string? Text, bool HasBom) ReadConfigState(string directory, string fileName)
    {
        var path = ModManager.LocateModConfig(directory, fileName);
        if (path is null) return (null, false);
        try
        {
            var bytes = InspectionFiles.ReadBytes(path);
            if (bytes is null) return (null, false);
            var bom = bytes.AsSpan().StartsWith("\uFEFF"u8);
            var offset = bom ? 3 : 0;
            return (Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset), bom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, false);
        }
    }

    private static void Normalize(ModProfileStore store)
    {
        store.Profiles ??= new List<ModProfile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<ModProfile>();
        foreach (var profile in store.Profiles)
        {
            if (profile is null) continue;
            var name = (profile.Name ?? "").Trim();
            if (name.Length == 0 || name.Length > MaxNameLength) continue;
            if (!seen.Add(name)) continue;
            profile.Name = name;
            profile.Note = ClampNote(profile.Note);
            profile.Mods ??= new List<ModProfileMod>();
            var modSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            profile.Mods = profile.Mods
                .Where(m => m is not null && !string.IsNullOrWhiteSpace(m.Name))
                .Where(m => modSeen.Add(m.Name.Trim()))
                .ToList();
            foreach (var mod in profile.Mods) mod.Name = mod.Name.Trim();
            profile.Loader ??= new ModProfileLoaderSettings();
            kept.Add(profile);
        }
        store.Profiles = kept;

        if (!string.IsNullOrEmpty(store.Active))
        {
            var active = kept.FirstOrDefault(p => string.Equals(p.Name, store.Active.Trim(), StringComparison.OrdinalIgnoreCase));
            store.Active = active?.Name ?? "";
        }
    }

    /// <summary>有效启用状态：目录在 <c>mods</c> 里 **且** sidecar 没有写 enabled=false（加载器两者都看）。</summary>
    private static bool EffectiveEnabled(ModEntryInfo mod) => !mod.DisabledFolder && mod.Enabled;

    private static string ClampNote(string? note)
    {
        var text = (note ?? "").Trim();
        return text.Length <= MaxNoteLength ? text : text[..MaxNoteLength];
    }
}
