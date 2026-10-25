using System.Buffers.Binary;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AssetsTools.NET.Extra;
using CesiumLoader.CharacterSkins;

namespace AstralParty.Toys.Services;

public sealed record LegacyCharacterSkinImportResult(string Id, int Images, int HeroId, int SkinItemId, string[] Normalizations, int Animations = 0, int Sprites = 0, int PreservedMedia = 0);
public sealed class LegacyCharacterSkinTargetException : InvalidOperationException
{
    public CharacterSkinTarget[] Candidates { get; }
    public LegacyCharacterSkinTargetException(CharacterSkinTarget[] candidates)
        : base("资源对应多个角色或皮肤，无法自动识别；请使用包含明确目标立绘的皮肤包。匹配结果：" + string.Join("、", candidates.Select(x => x.Name + " (" + x.HeroId + "/" + x.ItemId + ")"))) => Candidates = candidates;
}

/// <summary>Converts legacy character images and sprite animations to an inactive Toys package; preserves recognized skill video sources.
/// Never overwrites original game bundles or activates the imported package.
/// 不设人为的文件数量或总大小上限：只受 Unity 纹理尺寸、单个 bundle 可读入内存与磁盘剩余空间约束（见 <see cref="ImportLimits"/>）。</summary>
public static class LegacyCharacterSkinImporter
{
    internal sealed record Target(int HeroId, int SkinItemId, string Name, Dictionary<string, string> Slots, HashSet<string> Assets);
    internal sealed record Texture(string Name, int Width, int Height, byte[] Pixels);
    internal delegate IReadOnlyList<Texture> ReadBundle(string path, CancellationToken token);

    public static Task<LegacyCharacterSkinImportResult> ImportAsync(string game, string zipPath, string dataDirectory,
        string protocolDirectory, Action<CharacterExportProgress> progress, CancellationToken cancellationToken = default,
        int selectedHeroId = 0, int selectedSkinItemId = 0)
        => Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress(new(1, "读取角色与皮肤配置"));
            var (targets, assets) = LoadTargets(dataDirectory, protocolDirectory);
            return Import(game, zipPath, targets, assets, ReadTextures, progress, cancellationToken, selectedHeroId, selectedSkinItemId, true);
        }, cancellationToken);

    internal static Task<LegacyCharacterSkinImportResult> ImportForTestingAsync(string game, string zipPath, Target[] targets,
        HashSet<string> assets, ReadBundle reader, Action<CharacterExportProgress> progress, CancellationToken cancellationToken = default,
        int selectedHeroId = 0, int selectedSkinItemId = 0)
        => Task.Run(() => Import(game, zipPath, targets, assets, reader, progress, cancellationToken, selectedHeroId, selectedSkinItemId), cancellationToken);

    private static (Target[], HashSet<string>) LoadTargets(string dataDirectory, string protocolDirectory)
    {
        var protocol = new GameProtocolContext(protocolDirectory);
        object Decode(string name) => protocol.Decode(name + "Configure", File.ReadAllBytes(Path.Combine(dataDirectory, name + ".bin")));
        var characters = Decode("Character");
        var skins = Decode("Skin");
        object? items = File.Exists(Path.Combine(dataDirectory, "Item.bin")) ? Decode("Item") : null;
        var allAssets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var config in new[] { characters, skins, items })
            if (config != null) foreach (string value in Strings(config)) allAssets.Add(value);
        var groups = ReflectionValue.Items(ReflectionValue.Get(skins, "StandingPaintings"))
            .ToDictionary(x => ReflectionValue.Int(x, "Id"));
        var itemInfos = ReflectionValue.Items(ReflectionValue.Get(items, "Infos"))
            .ToDictionary(x => ReflectionValue.Int(x, "Id"));
        var names = CharacterSkinCatalog.Load(protocol, dataDirectory).ToDictionary(x => (x.HeroId, x.ItemId), x => x.Name);
        var targets = new List<Target>();
        foreach (var hero in ReflectionValue.Items(ReflectionValue.Get(characters, "Infos")))
        {
            if (!groups.TryGetValue(ReflectionValue.Int(hero, "StandingPainting"), out var group)) continue;
            int heroId = ReflectionValue.Int(hero, "Id");
            foreach (var skin in ReflectionValue.Items(ReflectionValue.Get(group, "SkinStandingPaintingConfigureItems")))
            {
                int itemId = ReflectionValue.Int(skin, "ItemID");
                if (itemId <= 0) continue;
                var slots = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (string slot in CharacterSkinRules.ImageSlots)
                {
                    string asset;
                    if (slot == "CharacterMap") asset = ReflectionValue.Text(hero, slot);
                    else if (slot.StartsWith("Bust", StringComparison.Ordinal) || slot.StartsWith("SfwBust", StringComparison.Ordinal))
                    {
                        var bust = ReflectionValue.Items(ReflectionValue.Get(skin, slot.StartsWith("Sfw", StringComparison.Ordinal) ? "SfwBust" : "Bust"))
                            .Select(x => x.ToString() ?? "").ToArray();
                        int index = slot.EndsWith('1') ? 1 : 0;
                        asset = index < bust.Length ? bust[index] : "";
                    }
                    else asset = ReflectionValue.Text(skin, slot);
                    if (asset.Length > 0) slots[slot] = asset;
                }
                var linked = new HashSet<string>(slots.Values, StringComparer.Ordinal);
                foreach (string value in Strings(skin)) linked.Add(value);
                if (itemInfos.TryGetValue(itemId, out var item)) foreach (string value in Strings(item)) linked.Add(value);
                targets.Add(new(heroId, itemId, names.GetValueOrDefault((heroId, itemId), itemId.ToString()), slots, linked));
            }
        }
        if (targets.Count == 0) throw new InvalidDataException("游戏配置没有可匹配的角色皮肤。");
        return (targets.ToArray(), allAssets);
    }

    // Only protobuf data properties are traversed; Descriptor/Parser trigger incompatible reflection in game DLLs.
    private static IEnumerable<string> Strings(object? value, int depth = 0)
    {
        if (value is string text) { if (text.Length > 0) yield return text; yield break; }
        if (value == null || depth > 10 || value.GetType().IsPrimitive || value.GetType().IsEnum) yield break;
        if (value is IEnumerable sequence)
        {
            foreach (object? child in sequence) foreach (string s in Strings(child, depth + 1)) yield return s;
            yield break;
        }
        foreach (var property in value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (property.GetIndexParameters().Length == 0 && (property.CanWrite || typeof(IEnumerable).IsAssignableFrom(property.PropertyType)))
                foreach (string s in Strings(property.GetValue(value), depth + 1)) yield return s;
    }

    private static LegacyCharacterSkinImportResult Import(string game, string zipPath, Target[] targets, HashSet<string> assets,
        ReadBundle readBundle, Action<CharacterExportProgress> progress, CancellationToken token, int selectedHeroId, int selectedSkinItemId, bool animationResources = false)
    {
        void Report(int percent, string text) { token.ThrowIfCancellationRequested(); progress(new(percent, text)); token.ThrowIfCancellationRequested(); }
        Report(2, "检查旧皮肤 ZIP 的路径与可用空间");
        var manager = new CharacterSkinManager();
        string id = "legacy-" + Guid.NewGuid().ToString("N");
        string targetPath = manager.Package(game, id);
        string staging = Path.Combine(manager.Root(game), ".legacy-import-" + Guid.NewGuid().ToString("N"));
        using var archive = OpenArchive(zipPath);
        var entries = ValidateArchive(archive);
        if (!entries.Any(x => x.Entry.Name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("ZIP 中没有 Unity .bundle 资源。");
        Directory.CreateDirectory(staging);
        try
        {
            // 不按固定体积挡包：只确认目标盘放得下这些解压后的内容
            long declaredBytes = 0;
            foreach (var (declaredEntry, _) in entries) declaredBytes += declaredEntry.Length;
            ImportLimits.EnsureFreeSpace(staging, declaredBytes);

            var bundles = new List<(string Original, string Path)>();
            foreach (var (entry, relative) in entries)
            {
                token.ThrowIfCancellationRequested();
                string destination = CharacterSkinRules.SafePath(staging, "source/" + relative);
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { Directory.CreateDirectory(destination); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                using (var input = entry.Open())
                using (var output = File.Create(destination))
                {
                    byte[] buffer = new byte[81920]; int count; long written = 0;
                    while ((count = input.Read(buffer)) > 0)
                    {
                        token.ThrowIfCancellationRequested(); written += count;
                        if (written > entry.Length) throw new InvalidDataException("ZIP 解压大小超过声明。");
                        output.Write(buffer, 0, count);
                    }
                    if (written != entry.Length) throw new InvalidDataException("ZIP 文件长度不完整：" + relative);
                }
                if (relative.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase)) bundles.Add((relative, destination));
            }
            var textureNames = new HashSet<string>(StringComparer.Ordinal);
            var converted = new Dictionary<string, string>(StringComparer.Ordinal);
            var normalizations = new List<string>();
            var animation = animationResources ? new LegacyCharacterAnimationImport(staging, token) : null;
            for (int i = 0; i < bundles.Count; i++)
            {
                var bundle = bundles[i];
                Report(10 + 65 * i / bundles.Count, $"转换资源 {i + 1} / {bundles.Count}：{bundle.Original}");
                string workingPath = Path.Combine(staging, ".work", i.ToString("D5") + ".bundle");
                Directory.CreateDirectory(Path.GetDirectoryName(workingPath)!);
                byte[] bytes = File.ReadAllBytes(bundle.Path);
                byte[] normalized = NormalizeBundle(bytes, bundle.Original, out string? normalization);
                if (normalization != null) normalizations.Add(normalization);
                File.WriteAllBytes(workingPath, normalized);
                IReadOnlyList<Texture> textures;
                try
                {
                    if (animation != null) { animation.ExportBundle(workingPath, converted, textureNames, (done,total) => Report(10+65*i/bundles.Count+(65*done/total/bundles.Count), $"已转换动画帧 {done} / {total}")); continue; }
                    textures = readBundle(workingPath, token);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { throw new InvalidDataException("无法完整读取旧资源包 " + bundle.Original + "：" + ex.Message, ex); }
                if (textures.Count == 0) throw new InvalidDataException("资源包没有可转换的 Texture2D：" + bundle.Original);
                foreach (var texture in textures)
                {
                    token.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(texture.Name) || !textureNames.Add(texture.Name))
                        throw new InvalidDataException("纹理资源名为空或重复：" + texture.Name);
                    // Some account/background configs are not shipped in Toys' GameData subset.
                    // Auxiliary textures retain their exact Unity resource name; target detection below must still have an exact config anchor.
                    // 唯一保留的尺寸限制是 Unity 纹理边长上限；解码字节数由它自然限住（16384×16384×4 = 1 GiB）。
                    if (texture.Width <= 0 || texture.Height <= 0
                        || texture.Width > ImportLimits.MaxTextureDimension || texture.Height > ImportLimits.MaxTextureDimension
                        || (long)texture.Width * texture.Height * 4 != texture.Pixels.LongLength)
                        throw new InvalidDataException("纹理尺寸超出 Unity 上限（最长边 16384）或解码数据不完整：" + texture.Name);
                    string relative = "images/" + converted.Count.ToString("D5") + ".png";
                    CharacterUnityResources.SavePng(CharacterSkinRules.SafePath(staging, relative), texture.Width, texture.Height, texture.Pixels);
                    converted.Add(texture.Name, relative);
                }
            }
            Report(80, "按配置中的完整资源名匹配目标皮肤");
            var target = SelectTarget(targets, textureNames, selectedHeroId, selectedSkinItemId);
            var manifest = new CharacterSkinManifest
            {
                Id = id, Name = Path.GetFileNameWithoutExtension(zipPath), HeroId = target.HeroId, SkinItemId = target.SkinItemId,
                Description = "从旧式 Unity bundle 皮肤包转换。原作者说明和原文件保存在 source/。", UseOriginalSpriteLayout = true
            };
            foreach (var (asset, relative) in converted)
            {
                var slots = target.Slots.Where(x => x.Value == asset).Select(x => x.Key).ToArray();
                if (slots.Length == 0) manifest.Images.Add("asset:" + asset, relative);
                else foreach (string slot in slots) manifest.Images.Add(slot, relative);
            }
            animation?.Apply(manifest, target);
            // Original archive documents remain unmodified; no guessed author is assigned to someone else's work.
            Report(95, "校验 PNG 和皮肤清单");
            CharacterSkinRules.Validate(manifest, staging, true);
            File.WriteAllText(Path.Combine(staging, "character-skin.json"), JsonSerializer.Serialize(manifest, CharacterSkinManager.JsonOptions));
            File.WriteAllText(Path.Combine(staging, "legacy-source.json"), JsonSerializer.Serialize(new
            {
                Archive = Path.GetFileName(zipPath), target.HeroId, target.SkinItemId, Textures = converted,
                AuxiliaryResourcesAbsentFromBundledConfig = converted.Keys.Where(x => !assets.Contains(x)).ToArray(), Normalizations = normalizations
            }, CharacterSkinManager.JsonOptions));
            Directory.Delete(Path.Combine(staging, ".work"), true);
            Report(99, "保存旧包转换结果");
            Directory.Move(staging, targetPath);
            // No cancellation/throwing observer callback after atomic commit.
            return new(id, converted.Count, target.HeroId, target.SkinItemId, normalizations.ToArray(), animation?.Animations.Count ?? 0, animation?.Sprites.Count ?? 0, animation?.Media.Count ?? 0);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    internal static Target SelectTarget(Target[] targets, IEnumerable<string> textureNames, int heroId = 0, int skinItemId = 0)
    {
        var candidates = targets.ToList(); bool linked = false;
        foreach (string asset in textureNames)
        {
            var owners = targets.Where(x => x.Assets.Contains(asset)).ToArray();
            if (owners.Length == 0) continue; // Background/item resources are validated against the full configs separately.
            linked = true;
            candidates.RemoveAll(x => !owners.Contains(x));
        }
        if (!linked || candidates.Count == 0) throw new InvalidDataException("资源无法对应一个角色皮肤，或包中混有多个目标的资源。");
        if (heroId != 0 || skinItemId != 0)
        {
            if (heroId <= 0 || skinItemId <= 0) throw new InvalidDataException("选择目标必须同时指定角色与皮肤 ItemID。");
            return candidates.SingleOrDefault(x => x.HeroId == heroId && x.SkinItemId == skinItemId)
                ?? throw new InvalidDataException("选择的角色皮肤与包内资源的准确配置匹配结果不符。");
        }
        if (candidates.Count != 1)
            throw new LegacyCharacterSkinTargetException(candidates.Select(x => new CharacterSkinTarget(x.HeroId, x.SkinItemId, x.Name, "", [])).ToArray());
        return candidates[0];
    }

    internal static ZipArchive OpenArchive(string path) => CompatZipArchive.OpenRead(path);

    internal static List<(ZipArchiveEntry Entry, string Relative)> ValidateArchive(ZipArchive archive)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(ZipArchiveEntry, string)>();
        foreach (var entry in archive.Entries)
        {
            // 文件数量与总大小不设人为上限；只有「单个 bundle 必须整块读进内存」这一条硬边界。
            if (entry.Name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase) && entry.Length > ImportLimits.MaxBundleBytes)
                throw new InvalidDataException("单个资源包超过 2 GiB，无法读入内存：" + entry.FullName);
            string name = entry.FullName.Replace('\\', '/').TrimEnd('/');
            if (name.Length == 0 || name.Length > 2000 || name.StartsWith('/') || name.IndexOf(':') >= 0)
                throw new InvalidDataException("ZIP 路径无效：" + entry.FullName);
            foreach (string component in name.Split('/'))
                if (component.Length == 0 || component is "." or ".." || component.EndsWith(' ') || component.EndsWith('.') || component.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    IsReservedName(component)) throw new InvalidDataException("ZIP 路径无效：" + entry.FullName);
            if (!seen.Add(name)) throw new InvalidDataException("ZIP 存在重复路径：" + entry.FullName);
            bool directory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            if (!directory) files.Add(name);
            if (entry.Name.Equals("character-skin.json", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("这是 Toys 皮肤包，请使用标准皮肤导入。");
            if ((entry.ExternalAttributes >> 16 & 0xf000) == 0xa000) throw new InvalidDataException("ZIP 不允许符号链接。");
            result.Add((entry, name));
        }
        foreach (string name in seen)
            for (int index = name.IndexOf('/'); index >= 0; index = name.IndexOf('/', index + 1))
                if (files.Contains(name[..index])) throw new InvalidDataException("ZIP 文件与目录路径冲突：" + name);
        return result;
    }

    private static bool IsReservedName(string component)
    {
        string name = component.Split('.')[0].ToUpperInvariant();
        return name is "CON" or "PRN" or "AUX" or "NUL" ||
            (name.Length == 4 && (name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal)) && name[3] is >= '0' and <= '9');
    }

    internal static byte[] NormalizeBundle(byte[] bytes, string name, out string? normalization)
    {
        normalization = null;
        if (bytes.Length < 32 || !bytes.AsSpan(0, 8).SequenceEqual("UnityFS\0"u8)) throw new InvalidDataException("不是 UnityFS bundle：" + name);
        int offset = 12;
        for (int i = 0; i < 2; i++)
        {
            int end = Array.IndexOf(bytes, (byte)0, offset);
            if (end < 0 || end - offset > 100) throw new InvalidDataException("UnityFS 版本头无效：" + name);
            offset = end + 1;
        }
        if (offset + 20 > bytes.Length) throw new InvalidDataException("UnityFS 头不完整：" + name);
        long declared = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(offset, 8));
        uint compressed = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8, 4));
        uint uncompressed = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 12, 4));
        uint flags = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 16, 4));
        if (declared < offset + 20 || declared > bytes.Length || declared > ImportLimits.MaxBundleBytes
            || compressed > ImportLimits.MaxBundleBytes || uncompressed > ImportLimits.MaxBundleBytes)
            throw new InvalidDataException("UnityFS 大小声明无效，或超过 2 GiB 无法读入内存：" + name);
        if (declared == bytes.Length) return bytes;
        // A known old package accidentally repeats the tail of its end-of-file directory block.
        // Accept ONLY a byte-identical duplicate suffix wholly contained in that compressed directory.
        int extra = checked(bytes.Length - (int)declared);
        if ((flags & 0x80) == 0 || extra <= 0 || extra > compressed || declared - extra < offset + 20 ||
            !bytes.AsSpan((int)declared, extra).SequenceEqual(bytes.AsSpan((int)declared - extra, extra)))
            throw new InvalidDataException("UnityFS 存在无法解释的尾部数据，拒绝修复：" + name);
        normalization = name + "：移除与目录块尾部完全相同的 " + extra + " 个追加字节；原文件保存在 source/。";
        return bytes.AsSpan(0, (int)declared).ToArray();
    }

    internal static IReadOnlyList<Texture> ReadTextures(string path, CancellationToken token)
    {
        // 不再按固定体积限制解压总量：bundle 本身已受「能整块读入内存」约束，真正的上限是机器内存。
        var manager = new AssetsManager();
        try
        {
            var bundle = manager.LoadBundleFile(path, true);
            for (int i = 0; i < bundle.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                string entryName = bundle.file.GetFileName(i);
                if (entryName.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) || entryName.EndsWith(".resource", StringComparison.OrdinalIgnoreCase)) continue;
                var file = manager.LoadAssetsFileFromBundle(bundle, i, false)
                    ?? throw new InvalidDataException("无法读取 bundle 内的资源文件：" + entryName);
                if (file.file.AssetInfos.Any(x => x.TypeId is not (28 or 142)))
                    throw new InvalidDataException("仅支持静态 Texture2D 替换包；包内含动画、精灵或其他资源，不能部分导入。");
            }
        }
        finally { manager.UnloadAll(); }
        using var resources = new CharacterUnityResources();
        resources.Load([path], token);
        var textures = resources.Objects.Where(x => x.Info.TypeId == 28).ToArray();
        foreach (var file in textures.Select(x => x.File).Distinct())
            if (file.file.AssetInfos.Any(x => x.TypeId is not (28 or 142)))
                throw new InvalidDataException("仅支持静态 Texture2D 替换包；包内含动画、精灵或其他资源，不能部分导入。");
        var result = new List<Texture>();
        foreach (var texture in textures)
        {
            token.ThrowIfCancellationRequested();
            int width = texture.Value["m_Width"].AsInt, height = texture.Value["m_Height"].AsInt;
            if (width <= 0 || height <= 0
                || width > ImportLimits.MaxTextureDimension || height > ImportLimits.MaxTextureDimension)
                throw new InvalidDataException("Texture2D 尺寸超出 Unity 上限（最长边 16384）。");
            var decoded = resources.Texture(texture);
            result.Add(new(texture.Value["m_Name"].AsString, decoded.Width, decoded.Height, decoded.Pixels));
        }
        return result;
    }
}
