using System.Text.Json;

namespace AstralParty.Toys.Services;

public static class MaterialSource
{
    public static IReadOnlyList<string> DiscoverAll(string appDirectory)
    {
        var roots = new List<string>();
        var configured = Path.Combine(appDirectory, "asset-sources.json");
        if (File.Exists(configured))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(configured));
                if (document.RootElement.TryGetProperty("materialRoots", out var rootsElement) &&
                    rootsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in rootsElement.EnumerateArray()) AddRoot(roots, element.GetString());
                }
                if (document.RootElement.TryGetProperty("materialsRoot", out var rootElement))
                    AddRoot(roots, rootElement.GetString());
            }
            catch (JsonException)
            {
                // 配置无效时只禁用外部素材，不影响回放解析。
            }
        }

        return roots;
    }

    private static void AddRoot(List<string> roots, string? value)
    {
        var expanded = Environment.ExpandEnvironmentVariables(value ?? "");
        if (!Directory.Exists(expanded)) return;
        var fullPath = Path.GetFullPath(expanded);
        if (!roots.Contains(fullPath, StringComparer.OrdinalIgnoreCase)) roots.Add(fullPath);
    }
}

public sealed class AssetLocator
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp", ".bmp"];
    private readonly string _localRoot;
    private readonly bool _useEmbedded;
    private readonly List<Dictionary<string, List<string>>> _externalSources = [];

    public AssetLocator(string localRoot, IEnumerable<string> externalRoots, bool useEmbedded = true)
    {
        _localRoot = localRoot;
        _useEmbedded = useEmbedded;
        foreach (var externalRoot in externalRoots.Where(Directory.Exists))
        {
            var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in Directory.EnumerateFiles(externalRoot, "*", SearchOption.AllDirectories)
                         .Where(path => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (!index.TryGetValue(name, out var paths)) index[name] = paths = [];
                paths.Add(path);
            }
            _externalSources.Add(index);
        }
    }

    public string Map(int id, params string[] hints) => Find("Maps", "地图预览", [.. hints, id.ToString()]);
    public string Character(int id, params string[] hints) => Find("Characters", "小人物头像",
        [.. hints, $"UT_Hero_ProfilePhoto_{id}", $"UT_Item_Hero_{id}", id.ToString()]);
    public string Relic(int id, params string[] hints) => Find("Relics", "筹码", [.. hints, $"UT_Relic_{id}", id.ToString()]);
    public string Monster(int id, params string[] hints)
    {
        var card = Find("Monsters", "敌对立绘", [.. hints, $"UT_Monster_Card_{id}", id.ToString()]);
        return card.Length > 0 ? card : Find("Monsters", "敌对受击", [$"UT_Monster_Bust_{id}_0"]);
    }
    public string Item(int id) => Find("Items", "养成素材", [$"UT_Item_{id}", id.ToString()]);
    public string Card(int id) => Find("Cards", "卡面", [$"UT_HandCard_{id}", id.ToString()]);
    public string Skill(int id) => Find("Skills", "卡面", [$"UT_Skill_{id}", id.ToString()]);
    public string Buff(int id) => Find("Buffs", "buff", [$"UT_Buff_{id}", id.ToString()]);
    public string Land(int id) => Find("Lands", "地块", [$"UT_Land_{id}", id.ToString()]);
    public string Event(string key) => Find("Events", "事件", [key]);
    public string Named(params string[] names) => Find("UI", "", names);

    private string Find(string localFolder, string externalFolder, IEnumerable<string> names)
    {
        foreach (var rawName in names.Where(name => !string.IsNullOrWhiteSpace(name)))
        {
            var name = Path.GetFileNameWithoutExtension(rawName.Replace('\\', '/').Split('/').Last());
            if (_useEmbedded && EmbeddedAssetStore.TryFind(localFolder, name, out var embeddedPath))
                return embeddedPath;

            foreach (var extension in Extensions)
            {
                var localPath = Path.Combine(_localRoot, localFolder, name + extension);
                if (File.Exists(localPath)) return localPath;
            }

            foreach (var source in _externalSources)
            {
                if (!source.TryGetValue(name, out var matches)) continue;
                if (matches.Count == 1) return matches[0];

                var categorized = matches.Where(path => path.Split(Path.DirectorySeparatorChar)
                        .Any(part => part.Equals(externalFolder, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (categorized.Count == 1) return categorized[0];
                break;
            }
        }

        return "";
    }
}
