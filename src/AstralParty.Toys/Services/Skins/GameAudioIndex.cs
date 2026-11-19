using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AddressablesTools;
using AddressablesTools.Catalog;
using AddressablesTools.Classes;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AstralParty.Toys.Services.ResourceMods;
using Ww2Ogg.Core;

namespace AstralParty.Toys.Services.Skins;

/// <summary>一个声音库在游戏文件里的位置。Size/Modified 用来判断游戏更新后索引是否过期。</summary>
public sealed class GameAudioBankRef
{
    public string Name { get; set; } = "";
    public string Language { get; set; } = "";
    public string Bundle { get; set; } = "";
    public long Size { get; set; }
    public long Modified { get; set; }
}

/// <summary>目录条目对应的原版音频：哪个声音库里的哪一段。</summary>
public sealed class GameAudioEntry
{
    public string Bank { get; set; } = "";
    public uint Wem { get; set; }
    public int Offset { get; set; }
    public int Size { get; set; }
    /// <summary>这个事件一共能播出几条录音（随机台词等）。试听只放其中一条。</summary>
    public int Variants { get; set; } = 1;
}

public sealed class GameAudioIndexFile
{
    public const int CurrentFormat = 1;
    public int Format { get; set; } = CurrentFormat;
    public DateTime BuiltAt { get; set; }
    public List<string> Catalogs { get; set; } = new();
    public Dictionary<string, GameAudioBankRef> Banks { get; set; } = new();
    public Dictionary<int, GameAudioEntry> Entries { get; set; } = new();
}

public sealed record GameAudioPrepareResult(int Ready, int Total, int Banks, string? Problem);

/// <summary>
/// 从用户自己安装的游戏里找出原版音频。
/// <para>
/// 游戏的声音放在 Addressables bundle 里：bundle → <c>WwiseSoundBankAsset</c>（字段 <c>m_Name</c> / <c>language</c> / <c>RawData</c>）
/// → RawData 就是整个 .bnk → .bnk 里的 .wem（Wwise Vorbis）。
/// </para>
/// <para>
/// 「准备」只建立索引（记下每个目录条目在哪个 bundle 的哪一段），不转换、不复制，约 1～2 秒；
/// 试听或导出时才把那一段切出来，用 Ww2Ogg 无损转成标准 ogg 存进 audio-cache。
/// wem ID 会随游戏版本变化，所以按事件 ID（目录的 wwiseId）现场查，查不到再退回目录里记录的 relOgg。
/// </para>
/// </summary>
public static class GameAudioIndex
{
    public const string IndexFileName = "game-audio-index.json";
    private const string SoundBankAssetType = "AK.Wwise.Unity.WwiseAddressables.WwiseSoundBankAsset";
    private const string SharedLanguage = "SFX";
    private const string DefaultLanguage = "Simplified Chinese";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false, PropertyNameCaseInsensitive = true };
    private static readonly object Gate = new();
    private static (string Path, DateTime Stamp, GameAudioIndexFile Index)? _loaded;

    public static string IndexPath(string gameDirectory) =>
        Path.Combine(AudioSkinSchemeManager.AudioCacheDirectory(gameDirectory), IndexFileName);

    // ---------------------------------------------------------------- 建立索引

    public static GameAudioPrepareResult Build(string gameDirectory, IReadOnlyList<AudioSkinCatalogItem> catalog, Action<string, double>? progress = null)
    {
        var layout = ResourceModCatalog.TryResolve(gameDirectory);
        if (layout == null) return new(0, catalog.Count, 0, "没有在游戏目录里找到游戏数据文件夹（*_Data）");

        progress?.Invoke("正在读取游戏资源目录…", 0.1);
        var addressablesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", layout.Company, layout.Product, "com.unity.addressables");
        var catalogs = LoadCatalogs(layout, addressablesDir);
        if (catalogs.Count == 0) return new(0, catalog.Count, 0, "没有找到游戏的资源目录（catalog），请先启动一次游戏");

        var localBundles = IndexLocalBundles(layout.DataDirectory);

        // 按目录从新到旧找声音库；同一个库只取最先找到的那份（新目录里的文件缺了，才用旧版本的）
        var banks = new Dictionary<string, (GameAudioBankRef Ref, WwiseBank Bank)>(StringComparer.Ordinal);
        var openedBundles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usedCatalogs = new List<string>();
        int done = 0;
        foreach (var (catalogPath, data) in catalogs)
        {
            done++;
            var before = banks.Count;
            foreach (var location in data.Resources.Values.SelectMany(x => x).Where(IsSoundBankAsset).Distinct())
            {
                foreach (var dependency in Dependencies(data, location))
                {
                    var file = ResolveBundleFile(dependency, addressablesDir, localBundles);
                    if (file == null || !openedBundles.Add(file)) continue;
                    ReadBanksFromBundle(file, banks);
                }
            }
            if (banks.Count > before) usedCatalogs.Add(catalogPath);
            progress?.Invoke($"已找到 {banks.Count} 个声音库", 0.1 + 0.6 * done / catalogs.Count);
        }
        if (banks.Count == 0) return new(0, catalog.Count, 0, "游戏文件里没有找到声音库（可能需要先启动游戏完成资源更新）");

        progress?.Invoke("正在对应音效条目…", 0.75);
        var index = new GameAudioIndexFile { BuiltAt = DateTime.UtcNow, Catalogs = usedCatalogs };
        foreach (var (key, value) in banks) index.Banks[key] = value.Ref;

        // 每种语言一张对象表：通用库打底，语言库盖在上面（中日两套语言库里的对象 ID 是同一批）
        var languages = banks.Values.Select(b => b.Ref.Language).Where(l => l != SharedLanguage).Distinct().ToList();
        var objectsByLanguage = new Dictionary<string, Dictionary<uint, (byte, byte[])>>();
        var mediaByLanguage = new Dictionary<string, Dictionary<uint, string>>();
        foreach (var language in languages.DefaultIfEmpty(SharedLanguage))
        {
            var objects = new Dictionary<uint, (byte, byte[])>();
            var media = new Dictionary<uint, string>();
            foreach (var (key, value) in banks.OrderBy(b => b.Value.Ref.Language == SharedLanguage ? 0 : 1))
            {
                if (value.Ref.Language != SharedLanguage && value.Ref.Language != language) continue;
                foreach (var (id, obj) in value.Bank.Objects) objects[id] = obj;
                foreach (var id in value.Bank.Media.Keys) media.TryAdd(id, key);
            }
            objectsByLanguage[language] = objects;
            mediaByLanguage[language] = media;
        }

        foreach (var item in catalog)
        {
            var (relBank, relWem) = ParseRelOgg(item.RelOgg);
            var language = relBank != null && banks.TryGetValue(relBank, out var rb) && rb.Ref.Language != SharedLanguage
                ? rb.Ref.Language
                : (languages.Contains(DefaultLanguage) ? DefaultLanguage : languages.FirstOrDefault() ?? SharedLanguage);
            var objects = objectsByLanguage[language];
            var media = mediaByLanguage[language];

            List<uint>? found = item.WwiseId != 0
                ? WwiseEventResolver.Resolve(unchecked((uint)item.WwiseId), objects, media.ContainsKey)
                : null;

            uint wem;
            string? bankKey;
            int variants;
            if (found is { Count: > 0 })
            {
                wem = relWem is uint r && found.Contains(r) ? r : found[0];
                bankKey = relBank != null && relWem == wem && banks.ContainsKey(relBank) && banks[relBank].Bank.Media.ContainsKey(wem) ? relBank : media[wem];
                variants = found.Count;
            }
            else if (relBank != null && relWem is uint rw && banks.TryGetValue(relBank, out var fallback) && fallback.Bank.Media.ContainsKey(rw))
            {
                wem = rw; bankKey = relBank; variants = 1;
            }
            else continue;

            var (offset, size) = banks[bankKey].Bank.Media[wem];
            index.Entries[item.Id] = new GameAudioEntry { Bank = bankKey, Wem = wem, Offset = offset, Size = size, Variants = variants };
        }

        progress?.Invoke("正在保存索引…", 0.95);
        Save(gameDirectory, index);
        return new(index.Entries.Count, catalog.Count, banks.Count, index.Entries.Count == 0 ? "找到了声音库，但没有和音效目录对上的条目" : null);
    }

    /// <summary>catalog 里依赖关系通常只记一个 DependencyKey，要再用它到 Resources 里查出依赖的 bundle 位置。</summary>
    private static IEnumerable<ResourceLocation> Dependencies(ContentCatalogData data, ResourceLocation location)
    {
        if (location.Dependencies is { Count: > 0 } direct) return direct;
        if (location.DependencyKey != null && data.Resources.TryGetValue(location.DependencyKey, out var list)) return list;
        return [];
    }

    private static bool IsSoundBankAsset(ResourceLocation location) =>
        string.Equals(location.Type?.ClassName, SoundBankAssetType, StringComparison.Ordinal);

    /// <summary>可用的 catalog，从新到旧：LocalLow 里热更新下载的 JSON → 安装目录预置的 → 安装包里的 catalog.bundle。</summary>
    private static List<(string Path, ContentCatalogData Data)> LoadCatalogs(ResourceModGameLayout layout, string addressablesDir)
    {
        var paths = new List<string>();
        void AddJsons(string dir)
        {
            if (!Directory.Exists(dir)) return;
            paths.AddRange(Directory.EnumerateFiles(dir, "catalog*.json", SearchOption.TopDirectoryOnly)
                .Where(p => p.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc));
        }
        AddJsons(addressablesDir);
        AddJsons(Path.Combine(layout.DataDirectory, "com.unity.addressables"));
        AddJsons(Path.Combine(layout.DataDirectory, "StreamingAssets", "aa"));

        var result = new List<(string, ContentCatalogData)>();
        foreach (var path in paths)
        {
            try { result.Add((path, ResourceModCatalog.LoadJson(path))); } catch { }
        }
        try
        {
            if (ResourceModCatalog.TryLoadBundle(layout.CatalogBundlePath) is { } bundled) result.Add((layout.CatalogBundlePath, bundled));
        }
        catch { }
        return result;
    }

    private static Dictionary<string, string> IndexLocalBundles(string dataDirectory)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in new[] { Path.Combine(dataDirectory, "StreamingAssets", "aa"), Path.Combine(dataDirectory, "com.unity.addressables") })
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.EnumerateFiles(root, "*.bundle", SearchOption.AllDirectories))
                map.TryAdd(Path.GetFileName(file), file);
        }
        return map;
    }

    /// <summary>
    /// 热更新下载的 bundle 在 <c>AssetBundles\&lt;BundleName&gt;\&lt;Hash&gt;\__data</c>（只认哈希完全对上的那份）；
    /// 否则按文件名找安装目录里的。InternalId 是绝对路径的（资源模组改写过）跳过，避免把模组文件当成原版。
    /// </summary>
    private static string? ResolveBundleFile(ResourceLocation location, string addressablesDir, Dictionary<string, string> localBundles)
    {
        if (location.ProviderId != ResourceModCatalog.AssetBundleProviderId) return null;
        if (location.Data is WrappedSerializedObject { Object: AssetBundleRequestOptions options }
            && !string.IsNullOrEmpty(options.BundleName) && !string.IsNullOrEmpty(options.Hash))
        {
            var cached = Path.Combine(addressablesDir, "AssetBundles", options.BundleName, options.Hash, "__data");
            if (File.Exists(cached)) return cached;
        }
        var internalId = location.InternalId ?? "";
        if (internalId.Length == 0 || Path.IsPathRooted(internalId)) return null;
        var name = internalId[(internalId.LastIndexOfAny(['\\', '/']) + 1)..];
        return localBundles.TryGetValue(name, out var local) ? local : null;
    }

    private static void ReadBanksFromBundle(string file, Dictionary<string, (GameAudioBankRef, WwiseBank)> banks)
    {
        var manager = new AssetsManager();
        try
        {
            var bundle = manager.LoadBundleFile(file, true);
            var info = new FileInfo(file);
            for (int i = 0; i < bundle.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
            {
                var name = bundle.file.GetFileName(i);
                if (name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
                var assets = manager.LoadAssetsFileFromBundle(bundle, i, false);
                if (assets == null) continue;
                foreach (var asset in assets.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
                {
                    var field = manager.GetBaseField(assets, asset);
                    var raw = field["RawData"];
                    if (raw.IsDummy) continue;
                    var bankName = field["m_Name"].AsString;
                    var language = field["language"].IsDummy ? SharedLanguage : field["language"].AsString;
                    var key = BankKey(bankName, language);
                    if (banks.ContainsKey(key)) continue;
                    var bytes = raw["Array"].AsByteArray;
                    banks[key] = (new GameAudioBankRef
                    {
                        Name = bankName,
                        Language = language,
                        Bundle = file,
                        Size = info.Length,
                        Modified = info.LastWriteTimeUtc.Ticks
                    }, WwiseBank.Parse(bytes));
                }
            }
        }
        catch
        {
            // 单个 bundle 读不了（损坏、格式不认识）不影响其它的
        }
        finally
        {
            manager.UnloadAll(true);
        }
    }

    /// <summary>与 AudioCatalog 的 relOgg 一致：通用库就是库名，语言库是「库名.语言」。</summary>
    public static string BankKey(string name, string language) =>
        string.IsNullOrEmpty(language) || language == SharedLanguage ? name : $"{name}.{language}";

    /// <summary>relOgg 形如 <c>ogg/Hero_101.Simplified Chinese/554811043.ogg</c>。</summary>
    internal static (string? Bank, uint? Wem) ParseRelOgg(string? relOgg)
    {
        if (string.IsNullOrEmpty(relOgg)) return (null, null);
        var parts = relOgg.Replace('\\', '/').Split('/');
        if (parts.Length < 3) return (null, null);
        var bank = parts[^2];
        return uint.TryParse(Path.GetFileNameWithoutExtension(parts[^1]), out var wem) ? (bank, wem) : (bank, null);
    }

    // ---------------------------------------------------------------- 读写索引

    private static void Save(string gameDirectory, GameAudioIndexFile index)
    {
        var path = IndexPath(gameDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        lock (Gate)
        {
            // 以游戏文件为准：缓存里不是从这份索引转换出来的文件（游戏更新前转换的、导入音频包时对错了编号的）都删掉，
            // 下次试听重新转换。索引里没有的条目不动（可能是用户导入的、游戏里找不到的）。
            var old = TryLoadUnlocked(path);
            var cacheDir = Path.GetDirectoryName(path)!;
            foreach (var (id, after) in index.Entries)
            {
                if (old != null && old.Entries.TryGetValue(id, out var before) && SameSource(old, before, index, after)) continue;
                TryDelete(Path.Combine(cacheDir, $"{id}.ogg"));
                TryDelete(Path.Combine(cacheDir, $"{id}.wav"));
            }
            if (old != null)
            {
                foreach (var id in old.Entries.Keys.Where(id => !index.Entries.ContainsKey(id)))
                    TryDelete(Path.Combine(cacheDir, $"{id}.ogg"));
            }
            var temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(index, JsonOptions));
            File.Move(temp, path, overwrite: true);
            _loaded = null;
        }
    }

    private static bool SameSource(GameAudioIndexFile a, GameAudioEntry x, GameAudioIndexFile b, GameAudioEntry y)
    {
        if (x.Wem != y.Wem || x.Bank != y.Bank || x.Size != y.Size) return false;
        return a.Banks.TryGetValue(x.Bank, out var ba) && b.Banks.TryGetValue(y.Bank, out var bb)
            && ba.Size == bb.Size && ba.Modified == bb.Modified;
    }

    public static GameAudioIndexFile? TryLoad(string gameDirectory)
    {
        lock (Gate) return TryLoadUnlocked(IndexPath(gameDirectory));
    }

    private static GameAudioIndexFile? TryLoadUnlocked(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var stamp = File.GetLastWriteTimeUtc(path);
            if (_loaded is { } l && l.Path == path && l.Stamp == stamp) return l.Index;
            var index = JsonSerializer.Deserialize<GameAudioIndexFile>(File.ReadAllText(path), JsonOptions);
            if (index == null || index.Format != GameAudioIndexFile.CurrentFormat) return null;
            _loaded = (path, stamp, index);
            return index;
        }
        catch { return null; }
    }

    // ---------------------------------------------------------------- 用到才转

    /// <summary>
    /// 返回条目 <paramref name="id"/> 的原版音频文件（audio-cache 里的 ogg / wav）。缓存里没有、索引里有时，现场转换一次。
    /// 找不到时返回 null，<paramref name="problem"/> 说明原因。
    /// </summary>
    public static string? EnsureOriginal(string gameDirectory, int id, out string? problem)
    {
        problem = null;
        var cacheDir = AudioSkinSchemeManager.AudioCacheDirectory(gameDirectory);
        var existing = CachedFile(cacheDir, id);
        if (existing != null) return existing;

        var index = TryLoad(gameDirectory);
        if (index == null || !index.Entries.TryGetValue(id, out var entry)) { problem = "这条还没有准备原版音频"; return null; }
        var converted = ConvertMany(gameDirectory, index, [id], null);
        if (converted.TryGetValue(id, out var path)) return path;
        problem = StaleProblem(index, entry) ?? "转换原版音频失败";
        return null;
    }

    /// <summary>批量准备（导出用）：按声音库分组，每个 bundle 只打开一次。返回成功的条目 → 文件。</summary>
    public static Dictionary<int, string> EnsureOriginals(string gameDirectory, IEnumerable<int> ids, Action<string, double>? progress = null)
    {
        var cacheDir = AudioSkinSchemeManager.AudioCacheDirectory(gameDirectory);
        var result = new Dictionary<int, string>();
        var missing = new List<int>();
        foreach (var id in ids)
        {
            var existing = CachedFile(cacheDir, id);
            if (existing != null) result[id] = existing; else missing.Add(id);
        }
        var index = TryLoad(gameDirectory);
        if (index != null && missing.Count > 0)
            foreach (var (id, path) in ConvertMany(gameDirectory, index, missing, progress)) result[id] = path;
        return result;
    }

    private static string? CachedFile(string cacheDir, int id)
    {
        var ogg = Path.Combine(cacheDir, $"{id}.ogg");
        if (File.Exists(ogg)) return ogg;
        var wav = Path.Combine(cacheDir, $"{id}.wav");
        return File.Exists(wav) ? wav : null;
    }

    private static string? StaleProblem(GameAudioIndexFile index, GameAudioEntry entry)
    {
        if (!index.Banks.TryGetValue(entry.Bank, out var bank)) return null;
        try
        {
            var info = new FileInfo(bank.Bundle);
            if (!info.Exists || info.Length != bank.Size || info.LastWriteTimeUtc.Ticks != bank.Modified)
                return "游戏资源已经更新，请重新「准备原版音频」";
        }
        catch { }
        return null;
    }

    private static Dictionary<int, string> ConvertMany(string gameDirectory, GameAudioIndexFile index, IReadOnlyCollection<int> ids, Action<string, double>? progress)
    {
        var result = new Dictionary<int, string>();
        var cacheDir = AudioSkinSchemeManager.AudioCacheDirectory(gameDirectory);
        Directory.CreateDirectory(cacheDir);

        var groups = ids.Where(index.Entries.ContainsKey)
            .GroupBy(id => index.Entries[id].Bank)
            .Where(g => index.Banks.ContainsKey(g.Key))
            .ToList();
        int done = 0, total = groups.Sum(g => g.Count());
        foreach (var group in groups)
        {
            var bank = index.Banks[group.Key];
            if (StaleProblem(index, index.Entries[group.First()]) != null) { done += group.Count(); continue; }
            var raw = ReadBankRaw(bank);
            if (raw == null) { done += group.Count(); continue; }

            foreach (var id in group)
            {
                done++;
                var entry = index.Entries[id];
                if (entry.Offset < 0 || entry.Size <= 0 || entry.Offset + (long)entry.Size > raw.Length) continue;
                try
                {
                    var ogg = WemToOgg(raw.AsSpan(entry.Offset, entry.Size).ToArray());
                    var target = Path.Combine(cacheDir, $"{id}.ogg");
                    lock (Gate)
                    {
                        var temp = target + ".tmp";
                        File.WriteAllBytes(temp, ogg);
                        File.Move(temp, target, overwrite: true);
                    }
                    result[id] = target;
                }
                catch { }
                if (progress != null && done % 25 == 0) progress($"正在转换原版音频 ({done}/{total})…", (double)done / Math.Max(total, 1));
            }
        }
        return result;
    }

    private static byte[]? ReadBankRaw(GameAudioBankRef bank)
    {
        var manager = new AssetsManager();
        try
        {
            var bundle = manager.LoadBundleFile(bank.Bundle, true);
            for (int i = 0; i < bundle.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
            {
                var name = bundle.file.GetFileName(i);
                if (name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
                var assets = manager.LoadAssetsFileFromBundle(bundle, i, false);
                if (assets == null) continue;
                foreach (var asset in assets.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
                {
                    var field = manager.GetBaseField(assets, asset);
                    if (field["RawData"].IsDummy) continue;
                    var language = field["language"].IsDummy ? SharedLanguage : field["language"].AsString;
                    if (field["m_Name"].AsString == bank.Name && language == bank.Language)
                        return field["RawData"]["Array"].AsByteArray;
                }
            }
        }
        catch { }
        finally { manager.UnloadAll(true); }
        return null;
    }

    /// <summary>Wwise Vorbis → 标准 Ogg Vorbis（只重新封装，不重新压缩）。这个游戏用的是 aoTuV 码本，失败再试默认码本。</summary>
    internal static byte[] WemToOgg(byte[] wem)
    {
        Exception? last = null;
        foreach (var codebooks in new[] { CodebookLibrary.AoTuV, CodebookLibrary.Default })
        {
            try
            {
                using var input = new MemoryStream(wem, writable: false);
                using var output = new MemoryStream();
                new WwiseRiffVorbis(input, codebooks).GenerateOgg(output);
                return output.ToArray();
            }
            catch (Exception ex) { last = ex; }
        }
        throw new InvalidDataException("无法转换这段音频：" + last?.Message, last);
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
