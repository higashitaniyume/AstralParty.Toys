using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using CesiumLoader.CharacterSkins;

namespace AstralParty.Toys.Services;

public sealed record CharacterSkinProfile(string Id, CharacterSkinManifest Manifest, bool Active, string? Error);
public sealed class CharacterSkinManager
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public string Root(string game) => Path.Combine(game, "AstralParty_ModLoader", "character-skins");
    public string Config(string game) => Path.Combine(game, "AstralParty_ModLoader", "mods", "CharacterSkinMod", "config.json");
    public string Package(string game, string id)
    {
        if (id is null || !Regex.IsMatch(id, "^[A-Za-z0-9_-]{1,80}$")) throw new InvalidOperationException("方案标识仅支持字母、数字、下划线和短横线，最长 80 字符。");
        return CharacterSkinRules.SafePath(Root(game), id);
    }
    public CharacterSkinSettings Settings(string game) => File.Exists(Config(game)) ? JsonSerializer.Deserialize<CharacterSkinSettings>(File.ReadAllText(Config(game)), JsonOptions) ?? new() : new();
    public CharacterSkinManifest Load(string game, string id)
    {
        var directory = Package(game, id);
        var m = JsonSerializer.Deserialize<CharacterSkinManifest>(File.ReadAllText(Path.Combine(directory, "character-skin.json")), JsonOptions) ?? throw new InvalidOperationException("清单为空");
        CharacterSkinRules.Validate(m, directory, true);
        return m;
    }
    public List<CharacterSkinProfile> Profiles(string game)
    {
        var result = new List<CharacterSkinProfile>();
        if (!Directory.Exists(Root(game))) return result;
        var active = Settings(game).ActivePackages ?? [];
        foreach (var directory in Directory.GetDirectories(Root(game)).OrderBy(x => x))
        {
            var id = Path.GetFileName(directory);
            if (id.StartsWith('.')) continue;
            try { result.Add(new(id, Load(game, id), active.Contains(id), null)); }
            catch (Exception ex) { result.Add(new(id, new() { Id = id, Name = id }, false, ex.Message)); }
        }
        return result;
    }
    public void Save(string game, string id, CharacterSkinManifest m)
    {
        var dir = Package(game, id);
        m.Id = id;
        CharacterSkinRules.Validate(m, dir, true);
        Directory.CreateDirectory(dir);
        Atomic(Path.Combine(dir, "character-skin.json"), JsonSerializer.Serialize(m, JsonOptions));
    }
    public void Create(string game, string id, int hero, int skin, string name)
    {
        var dir = Package(game, id);
        if (Directory.Exists(dir)) throw new InvalidOperationException("方案已存在");
        var m = new CharacterSkinManifest { Id = id, Name = name, HeroId = hero, SkinItemId = skin };
        CharacterSkinRules.Validate(m, dir, false);
        Directory.CreateDirectory(dir);
        Save(game, id, m);
    }
    public void SetActive(string game, string id, bool enabled)
    {
        var settings = Settings(game);
        settings.ActivePackages ??= [];
        settings.ActivePackages.Remove(id);
        if (enabled)
        {
            var m = Load(game, id);
            // Explicit legacy activation remains available; editing a package never activates it.
            settings.ActiveSchemeId = "";
            // Wildcard targets overlap every skin of that hero.
            foreach (var other in settings.ActivePackages.ToArray())
            {
                var existing = Load(game, other);
                if (existing.HeroId == m.HeroId && (existing.SkinItemId == m.SkinItemId || existing.SkinItemId == 0 || m.SkinItemId == 0)) settings.ActivePackages.Remove(other);
            }
            settings.ActivePackages.Add(id);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Config(game))!);
        Atomic(Config(game), JsonSerializer.Serialize(settings, JsonOptions));
    }
    public void Delete(string game, string id)
    {
        var dir = Package(game, id);
        string schemesRoot = Path.Combine(game, "AstralParty_ModLoader", "character-skin-schemes");
        if (Directory.Exists(schemesRoot))
            foreach (string schemeDirectory in Directory.GetDirectories(schemesRoot))
            {
                if (Path.GetFileName(schemeDirectory).StartsWith('.')) continue;
                string path = Path.Combine(schemeDirectory, "character-scheme.json");
                if (!File.Exists(path)) continue;
                var scheme = JsonSerializer.Deserialize<CharacterSkinScheme>(File.ReadAllText(path), JsonOptions);
                if (scheme?.Rules?.Any(rule => rule != null && string.Equals(rule.CustomPackageId, id, StringComparison.OrdinalIgnoreCase)) == true)
                    throw new InvalidOperationException("自定义包被皮肤方案引用，请先移除方案规则: " + scheme.Id);
            }
        SetActive(game, id, false);
        var trash = Path.Combine(Root(game), ".deleted");
        Directory.CreateDirectory(trash);
        Directory.Move(dir, Path.Combine(trash, id + "-" + Guid.NewGuid().ToString("N")));
    }
    public string Import(string game, string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count > 12000 || archive.Entries.Sum(x => x.Length) > 512L * 1024 * 1024) throw new InvalidOperationException("皮肤包文件数量或大小超限");
        var manifests = archive.Entries.Where(x => x.Name == "character-skin.json").ToArray();
        if (manifests.Length != 1) throw new InvalidOperationException("ZIP 必须包含且仅包含一份 character-skin.json");
        var manifestEntry = manifests[0];
        using var reader = new StreamReader(manifestEntry.Open());
        var manifest = JsonSerializer.Deserialize<CharacterSkinManifest>(reader.ReadToEnd(), JsonOptions) ?? throw new InvalidOperationException("清单为空");
        string target = Package(game, manifest.Id);
        if (Directory.Exists(target)) throw new InvalidOperationException("同名方案已存在，请修改包的 Id 或先删除原方案");
        string prefix = manifestEntry.FullName[..^manifestEntry.Name.Length];
        string staging = Path.Combine(Root(game), ".import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal) || entry.FullName.EndsWith('/')) continue;
                string relative = entry.FullName[prefix.Length..];
                string path = CharacterSkinRules.SafePath(staging, relative);
                if (!seen.Add(path)) throw new InvalidOperationException("ZIP 存在重复路径");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                entry.ExtractToFile(path);
            }
            CharacterSkinRules.Validate(manifest, staging, true);
            // Normalize property casing for the runtime SDK's case-sensitive JSON reader.
            File.WriteAllText(Path.Combine(staging, "character-skin.json"), JsonSerializer.Serialize(manifest, JsonOptions));
            Directory.Move(staging, target);
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
        return manifest.Id;
    }
    public void Export(string game, string id, string destination)
    {
        Load(game, id);
        ZipFile.CreateFromDirectory(Package(game, id), destination);
    }
    public void ReplaceImage(string game, string id, string slot, string source)
    {
        if (Array.IndexOf(CharacterSkinRules.ImageSlots, slot) < 0) throw new InvalidOperationException("图片槽位无效");
        string ext = Path.GetExtension(source).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg")) throw new InvalidOperationException("请选择 PNG/JPG 图片");
        var m = Load(game, id);
        string relative = "images/" + slot + "-" + Guid.NewGuid().ToString("N") + ext;
        string path = CharacterSkinRules.SafePath(Package(game, id), relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(source, path);
        m.Images[slot] = relative;
        Save(game, id, m);
    }
    public void SaveImage(string game,string id,string slot,System.Windows.Media.Imaging.BitmapSource image)
    {
        if(Array.IndexOf(CharacterSkinRules.ImageSlots,slot)<0)throw new InvalidOperationException("图片槽位无效");
        var m=Load(game,id);
        string relative="images/"+slot+"-"+Guid.NewGuid().ToString("N")+".png";
        string path=CharacterSkinRules.SafePath(Package(game,id),relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using(var stream=File.Create(path))encoder.Save(stream);
        m.Images[slot]=relative;Save(game,id,m);
    }
    public void SaveFrame(string game, string id, string action, int index, System.Windows.Media.Imaging.BitmapSource image, string expectedPath)
    {
        var m = Load(game, id);
        if (!m.Animations.TryGetValue(action, out var animation) || index < 0 || index >= animation.Frames.Count)
            throw new InvalidOperationException("选中的动画帧不存在");
        string original = animation.Frames[index];
        if (original != expectedPath) throw new InvalidOperationException("该帧已发生变化，请重新选择替换");
        string relative = "animations/replacements/" + Guid.NewGuid().ToString("N") + ".png";
        string path = CharacterSkinRules.SafePath(Package(game, id), relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using (var stream = File.Create(path)) encoder.Save(stream);
        bool mapped = m.SpriteMappings.Values.Contains(original);
        // A mapped sprite may appear more than once in a clip. Keep its preview and runtime references consistent.
        if (mapped)
        {
            foreach (var key in m.SpriteMappings.Where(x => x.Value == original).Select(x => x.Key).ToArray()) m.SpriteMappings[key] = relative;
            foreach (var a in m.Animations.Values)
                for (int i = 0; i < a.Frames.Count; i++) if (a.Frames[i] == original) a.Frames[i] = relative;
        }
        else animation.Frames[index] = relative;
        Save(game, id, m);
    }
    private static void RemoveActionMappings(CharacterSkinManifest manifest, string action)
    {
        foreach (var key in manifest.SpriteMappings.Keys.Where(x => x.StartsWith(action + "_", StringComparison.Ordinal)).ToArray())
            manifest.SpriteMappings.Remove(key);
    }
    public void ClearAction(string game, string id, string action)
    {
        var m = Load(game, id);
        m.Animations.Remove(action);
        RemoveActionMappings(m, action);
        Save(game, id, m);
    }
    public void ImportFrames(string game, string id, string action, string[] sources, float fps, bool loop)
    {
        if (!Regex.IsMatch(action, "^[A-Za-z0-9_-]{1,80}$")) throw new InvalidOperationException("动作名无效");
        if (sources.Length == 0 || sources.Length > 2000) throw new InvalidOperationException("请选择 1～2000 张图片");
        var m = Load(game, id);
        var a = new CharacterAnimation { Fps = fps, Loop = loop };
        string folder = "animations/" + action + "-" + Guid.NewGuid().ToString("N");
        int i = 0;
        foreach (var source in sources.OrderBy(x => Path.GetFileName(x), StringComparer.Ordinal))
        {
            var ext = Path.GetExtension(source).ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg")) throw new InvalidOperationException("帧序列必须是 PNG/JPG");
            string relative = folder + "/" + (i++).ToString("D4") + ext;
            string path = CharacterSkinRules.SafePath(Package(game, id), relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.Copy(source, path);
            a.Frames.Add(relative);
        }
        m.Animations[action] = a;
        RemoveActionMappings(m, action);
        Save(game, id, m);
    }
    public static string Preview(string id, string path) => "https://characters.astral.local/" + Uri.EscapeDataString(id) + "/" + string.Join('/', path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
    private static void Atomic(string path, string text)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, true);
    }
}
