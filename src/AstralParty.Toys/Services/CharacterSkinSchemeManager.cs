using System.IO;
using System.IO.Compression;
using System.Text.Json;
using CesiumLoader.CharacterSkins;

namespace AstralParty.Toys.Services;

public sealed record CharacterSkinSchemeProfile(string Id, CharacterSkinScheme Scheme, bool Active, string? Error);

public sealed class CharacterSkinSchemeManager
{
    private readonly CharacterSkinManager packages = new();
    private readonly Lazy<List<CharacterSkinTarget>> targets;
    public string? MigrationError { get; private set; }
    public CharacterSkinSchemeManager(IEnumerable<CharacterSkinTarget>? catalog = null)
    {
        targets = new(() => catalog?.ToList() ?? CharacterSkinCatalog.Load(
            new GameProtocolContext(Path.Combine(AppContext.BaseDirectory, "Protocol")), Path.Combine(AppContext.BaseDirectory, "GameData")));
    }
    public string Root(string game) => Path.Combine(game, "AstralParty_ModLoader", "character-skin-schemes");
    public string SchemeDirectory(string game, string id) { CharacterSkinSchemeRules.ValidateId(id); return CharacterSkinRules.SafePath(Root(game), id); }
    private string ManifestPath(string game, string id) => Path.Combine(SchemeDirectory(game, id), "character-scheme.json");
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), CharacterSkinManager.JsonOptions) ?? throw new InvalidOperationException("清单为空");
    private static void Atomic<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(value, CharacterSkinManager.JsonOptions)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private void Validate(CharacterSkinScheme scheme, Func<string, CharacterSkinManifest> loadPackage)
    {
        CharacterSkinSchemeRules.Validate(scheme);
        var manifests = new Dictionary<string, CharacterSkinManifest>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in scheme.Rules)
        {
            if (!targets.Value.Any(x => x.HeroId == rule.HeroId)) throw new InvalidOperationException("角色不存在: " + rule.HeroId);
            foreach (int id in new[] { rule.TargetSkinItemId, rule.OfficialSourceSkinItemId })
                if (id != 0 && !targets.Value.Any(x => x.HeroId == rule.HeroId && x.ItemId == id)) throw new InvalidOperationException("皮肤不属于当前角色: " + id);
            if (rule.CustomPackageId.Length > 0)
            {
                if (!manifests.TryGetValue(rule.CustomPackageId, out var manifest)) manifests[rule.CustomPackageId] = manifest = loadPackage(rule.CustomPackageId);
                if (manifest.HeroId != rule.HeroId) throw new InvalidOperationException("自定义包角色与规则不匹配: " + rule.CustomPackageId);
            }
        }
        CharacterSkinSchemeRules.ValidateSharedOverrides(scheme, manifests);
    }
    public CharacterSkinScheme Load(string game, string id)
    {
        var scheme = Read<CharacterSkinScheme>(ManifestPath(game, id));
        if (scheme.Id != id) throw new InvalidOperationException("方案目录与标识不一致");
        Validate(scheme, packageId => packages.Load(game, packageId));
        return scheme;
    }
    public List<CharacterSkinSchemeProfile> Profiles(string game)
    {
        MigrationError = null;
        try { EnsureMigration(game); }
        catch (Exception ex) { MigrationError = ex.Message; }
        var result = new List<CharacterSkinSchemeProfile>();
        if (!Directory.Exists(Root(game))) return result;
        string active = packages.Settings(game).ActiveSchemeId;
        foreach (string dir in Directory.GetDirectories(Root(game)).OrderBy(x => x))
        {
            string id = Path.GetFileName(dir);
            if (id.StartsWith('.')) continue;
            try { result.Add(new(id, Load(game, id), id == active, null)); }
            catch (Exception ex) { result.Add(new(id, new() { Id = id, Name = id }, id == active, ex.Message)); }
        }
        return result;
    }
    public void Save(string game, string id, CharacterSkinScheme scheme)
    {
        CharacterSkinSchemeRules.ValidateId(id);
        if (scheme.Id != id) throw new InvalidOperationException("方案标识与保存目标不一致");
        Validate(scheme, packageId => packages.Load(game, packageId));
        Atomic(ManifestPath(game, id), scheme);
    }
    public CharacterSkinScheme Create(string game, string id, string name)
    {
        if (Directory.Exists(SchemeDirectory(game, id))) throw new InvalidOperationException("方案已存在");
        var scheme = new CharacterSkinScheme { Id = id, Name = name };
        Save(game, id, scheme);
        return scheme;
    }
    public CharacterSkinBinding CreateBindingFromPackage(string game, string packageId, string ruleId)
    {
        CharacterSkinSchemeRules.ValidateId(ruleId);
        var manifest = packages.Load(game, packageId);
        return new() { Id = ruleId, HeroId = manifest.HeroId, TargetSkinItemId = manifest.SkinItemId, CustomPackageId = packageId };
    }
    public void AttachPackage(string game, string schemeId, CharacterSkinBinding rule)
    {
        var scheme = Load(game, schemeId);
        if (rule == null || string.IsNullOrEmpty(rule.CustomPackageId)) throw new InvalidOperationException("绑定规则必须指定自定义包");
        CharacterSkinSchemeRules.ValidateId(rule.CustomPackageId);
        var binding = CopyBinding(rule);
        int index = scheme.Rules.FindIndex(x => string.Equals(x.Id, binding.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0) scheme.Rules.Add(binding);
        else scheme.Rules[index] = binding;
        Save(game, schemeId, scheme);
    }
    public string ImportPackageIntoScheme(string game, string schemeId, string zipPath, CharacterSkinBinding? draft = null)
    {
        Load(game, schemeId);
        string id = packages.Import(game, zipPath);
        try
        {
            var binding = draft == null ? CreateBindingFromPackage(game, id, "rule-" + Guid.NewGuid().ToString("N")) : CopyBinding(draft);
            binding.CustomPackageId = id;
            AttachPackage(game, schemeId, binding);
            return id;
        }
        catch (Exception attachError)
        {
            try { packages.Delete(game, id); }
            catch (Exception rollbackError)
            {
                throw new AggregateException("自定义包已导入但绑定失败，回滚未完成；包可能被其他方案引用，已保留包。", attachError, rollbackError);
            }
            throw;
        }
    }
    private static CharacterSkinBinding CopyBinding(CharacterSkinBinding rule) => new()
    {
        Id = rule.Id, HeroId = rule.HeroId, TargetSkinItemId = rule.TargetSkinItemId,
        OfficialSourceSkinItemId = rule.OfficialSourceSkinItemId, CustomPackageId = rule.CustomPackageId
    };
    public void SetActive(string game, string id, bool enable = true)
    {
        if (id.Length > 0) Load(game, id);
        var settings = packages.Settings(game);
        settings.ActiveSchemeId = id;
        if (enable && id.Length > 0) settings.Enabled = true;
        settings.ActivePackages.Clear();
        Atomic(packages.Config(game), settings);
    }
    public void Delete(string game, string id)
    {
        string directory = SchemeDirectory(game, id);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);
        if (packages.Settings(game).ActiveSchemeId == id) SetActive(game, "");
        string trash = Path.Combine(Root(game), ".deleted");
        Directory.CreateDirectory(trash);
        Directory.Move(directory, Path.Combine(trash, id + "-" + Guid.NewGuid().ToString("N")));
    }
    public void EnsureMigration(string game)
    {
        var settings = packages.Settings(game);
        if (!string.IsNullOrEmpty(settings.ActiveSchemeId) || settings.ActivePackages == null || settings.ActivePackages.Count == 0) return;
        var scheme = new CharacterSkinScheme { Id = "legacy", Name = "旧版启用皮肤" };
        foreach (string id in settings.ActivePackages.Distinct(StringComparer.OrdinalIgnoreCase))
            scheme.Rules.Add(CreateBindingFromPackage(game, id, "rule-" + scheme.Rules.Count));
        Validate(scheme, id => packages.Load(game, id));
        string backup = packages.Config(game) + ".legacy-backup";
        if (File.Exists(packages.Config(game)) && !File.Exists(backup)) File.Copy(packages.Config(game), backup);
        // A user-created legacy scheme is never overwritten.
        string stem = scheme.Id;
        for (int i = 1; Directory.Exists(SchemeDirectory(game, scheme.Id)); i++) scheme.Id = stem + "-" + i;
        Save(game, scheme.Id, scheme);
        SetActive(game, scheme.Id, enable: false);
    }
    private static string[] Referenced(CharacterSkinScheme scheme) => scheme.Rules.Select(x => x.CustomPackageId).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    public void Export(string game, string id, string destination)
    {
        var scheme = Load(game, id);
        var files = new List<(string Path, string Entry)>();
        foreach (string packageId in Referenced(scheme))
            foreach (string path in Directory.GetFiles(packages.Package(game, packageId), "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(packages.Package(game, packageId), path).Replace('\\', '/');
                CharacterSkinRules.SafePath(packages.Package(game, packageId), relative);
                files.Add((path, "packages/" + packageId + "/" + relative));
            }
        string json = JsonSerializer.Serialize(scheme, CharacterSkinManager.JsonOptions);
        // 不设文件数量或总大小上限：导出的是玩家自己的方案与素材包，只确认磁盘放得下
        long requiredBytes = System.Text.Encoding.UTF8.GetByteCount(json);
        foreach (var file in files) requiredBytes += new FileInfo(file.Path).Length;
        ImportLimits.EnsureFreeSpace(Path.GetFullPath(destination), requiredBytes);
        using var archive = ZipFile.Open(destination, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("character-scheme.json");
        using (var writer = new StreamWriter(entry.Open())) writer.Write(json);
        foreach (var file in files) archive.CreateEntryFromFile(file.Path, file.Entry);
    }
    public string Import(string game, string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        // 不设文件数量或总大小上限：解压前只确认目标盘放得下（见 ImportLimits）
        long declaredBytes = 0;
        foreach (var declaredEntry in archive.Entries) declaredBytes += declaredEntry.Length;
        ImportLimits.EnsureFreeSpace(Root(game), declaredBytes);
        string staging = Path.Combine(Root(game), ".import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var moved = new List<string>();
        bool committed = false;
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in archive.Entries)
            {
                string relative = entry.FullName.Replace('\\', '/');
                if (relative.EndsWith('/')) { CharacterSkinRules.SafePath(staging, relative.TrimEnd('/')); continue; }
                string path = CharacterSkinRules.SafePath(staging, relative);
                if (!seen.Add(path)) throw new InvalidOperationException("ZIP 存在重复路径");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                entry.ExtractToFile(path);
            }
            var scheme = Read<CharacterSkinScheme>(Path.Combine(staging, "character-scheme.json"));
            string target = SchemeDirectory(game, scheme.Id);
            if (Directory.Exists(target)) throw new InvalidOperationException("同名方案已存在");
            CharacterSkinSchemeRules.Validate(scheme);
            var referenced = Referenced(scheme);
            // Only the scheme and its referenced packages are permitted; no official assets are bundled.
            foreach (string file in Directory.GetFiles(staging, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(staging, file).Replace('\\', '/');
                if (relative != "character-scheme.json" && !referenced.Any(x => relative.StartsWith("packages/" + x + "/", StringComparison.Ordinal))) throw new InvalidOperationException("ZIP 包含未引用的文件");
            }
            var imported = new Dictionary<string, CharacterSkinManifest>();
            foreach (string packageId in referenced)
            {
                string source = CharacterSkinRules.SafePath(staging, "packages/" + packageId);
                var manifest = Read<CharacterSkinManifest>(Path.Combine(source, "character-skin.json"));
                if (manifest.Id != packageId) throw new InvalidOperationException("包目录与标识不一致");
                CharacterSkinRules.Validate(manifest, source, true);
                if (Directory.Exists(packages.Package(game, packageId))) throw new InvalidOperationException("同名自定义包已存在: " + packageId);
                imported.Add(packageId, manifest);
                Atomic(Path.Combine(source, "character-skin.json"), manifest);
            }
            Validate(scheme, packageId => imported[packageId]);
            foreach (string packageId in referenced)
            {
                string packageTarget = packages.Package(game, packageId);
                Directory.CreateDirectory(packages.Root(game));
                Directory.Move(Path.Combine(staging, "packages", packageId), packageTarget);
                moved.Add(packageTarget);
            }
            Directory.CreateDirectory(target);
            moved.Add(target);
            Atomic(Path.Combine(target, "character-scheme.json"), scheme);
            committed = true;
            return scheme.Id;
        }
        finally
        {
            if (!committed) foreach (string path in moved) if (Directory.Exists(path)) Directory.Delete(path, true);
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }
}
