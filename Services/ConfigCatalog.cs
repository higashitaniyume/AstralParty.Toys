namespace AstralParty.ReplayTool.Services;

public sealed class ConfigCatalog
{
    private readonly GameProtocolContext _protocol;
    private readonly string _dataDirectory;
    private readonly Dictionary<int, string> _characters = [];
    private readonly Dictionary<int, string> _characterNicks = [];
    private readonly Dictionary<int, string> _maps = [];
    private readonly Dictionary<int, string> _monsters = [];
    private readonly Dictionary<int, string> _items = [];
    private readonly Dictionary<int, (string Name, string Quality)> _relics = [];
    private readonly Dictionary<int, string[]> _characterAssets = [];
    private readonly Dictionary<int, string[]> _mapAssets = [];
    private readonly Dictionary<int, string[]> _monsterAssets = [];
    private readonly Dictionary<int, string[]> _relicAssets = [];

    public ConfigCatalog(GameProtocolContext protocol, string dataDirectory)
    {
        _protocol = protocol;
        _dataDirectory = dataDirectory;
        Load();
    }

    public string Character(int id) => _characters.GetValueOrDefault(id, id == 0 ? "未知角色" : $"角色 {id}");
    public string CharacterNick(int id) => _characterNicks.GetValueOrDefault(id, "");
    public string Map(int id) => _maps.GetValueOrDefault(id, id == 0 ? "未知地图" : $"地图 {id}");
    public string Monster(int id) => _monsters.GetValueOrDefault(id, id == 0 ? "未知怪物" : $"怪物 {id}");
    public string Item(int id) => _items.GetValueOrDefault(id, $"物品 {id}");
    public string Relic(int id) => _relics.TryGetValue(id, out var value) ? value.Name : $"筹码 {id}";
    public string RelicQuality(int id) => _relics.TryGetValue(id, out var value) ? TranslateQuality(value.Quality) : "未知";
    public string[] CharacterAssets(int id) => _characterAssets.GetValueOrDefault(id) ?? [];
    public string[] MapAssets(int id) => _mapAssets.GetValueOrDefault(id) ?? [];
    public string[] MonsterAssets(int id) => _monsterAssets.GetValueOrDefault(id) ?? [];
    public string[] RelicAssets(int id) => _relicAssets.GetValueOrDefault(id) ?? [];
    public IEnumerable<int> CharacterIds => _characters.Keys.OrderBy(id => id);
    public IEnumerable<int> MapIds => _maps.Keys.OrderBy(id => id);
    public IEnumerable<int> MonsterIds => _monsters.Keys.OrderBy(id => id);
    public IEnumerable<int> RelicIds => _relics.Keys.OrderBy(id => id);

    private void Load()
    {
        LoadNamed("CharacterConfigure", "Character.bin", "STRCharacterConfigure", "STRCharacter.bin", "NameID", _characters);
        LoadNamed("CharacterConfigure", "Character.bin", "STRCharacterConfigure", "STRCharacter.bin", "NickID", _characterNicks);
        LoadNamed("MapConfigure", "Map.bin", "STRMapConfigure", "STRMap.bin", "MapName", _maps);
        LoadNamed("MonsterConfigure", "Monster.bin", "STRMonsterConfigure", "STRMonster.bin", "NameID", _monsters);
        LoadNamed("ItemConfigure", "Item.bin", "STRItemConfigure", "STRItem.bin", "NameID", _items);

        LoadAssets("CharacterConfigure", "Character.bin", _characterAssets, "CharacterMap");
        LoadAssets("MapConfigure", "Map.bin", _mapAssets, "MapImage", "MapSceneImage");
        LoadAssets("MonsterConfigure", "Monster.bin", _monsterAssets, "CharacterMap");

        var local = LoadLocalization("STRRelicConfigure", "STRRelic.bin");
        var config = DecodeFile("RelicConfigure", "Relic.bin");
        foreach (var info in ReflectionValue.Items(ReflectionValue.Get(config, "Infos")))
        {
            var id = ReflectionValue.Int(info, "Id");
            var nameId = ReflectionValue.Int(info, "NameID");
            _relics[id] = (local.GetValueOrDefault(nameId, $"筹码 {id}"), ReflectionValue.Text(info, "RelicQualityType"));
            _relicAssets[id] = [ReflectionValue.Text(info, "Icon")];
        }
    }

    private void LoadAssets(string configType, string fileName, Dictionary<int, string[]> target, params string[] properties)
    {
        var config = DecodeFile(configType, fileName);
        foreach (var info in ReflectionValue.Items(ReflectionValue.Get(config, "Infos")))
        {
            var id = ReflectionValue.Int(info, "Id");
            target[id] = properties.Select(property => ReflectionValue.Text(info, property))
                .Where(value => value.Length > 0)
                .ToArray();
        }
    }

    private void LoadNamed(string configType, string fileName, string stringType, string stringFile, string nameProperty, Dictionary<int, string> target)
    {
        var local = LoadLocalization(stringType, stringFile);
        var config = DecodeFile(configType, fileName);
        foreach (var info in ReflectionValue.Items(ReflectionValue.Get(config, "Infos")))
        {
            var id = ReflectionValue.Int(info, "Id");
            var nameId = ReflectionValue.Int(info, nameProperty);
            target[id] = local.GetValueOrDefault(nameId, $"{id}");
        }
    }

    private Dictionary<int, string> LoadLocalization(string typeName, string fileName)
    {
        var config = DecodeFile(typeName, fileName);
        return ReflectionValue.Items(ReflectionValue.Get(config, "Locals"))
            .ToDictionary(x => ReflectionValue.Int(x, "Id"), x => ReflectionValue.Text(x, "Simplified"));
    }

    private object DecodeFile(string typeName, string fileName)
    {
        var path = Path.Combine(_dataDirectory, fileName);
        if (!File.Exists(path)) throw new FileNotFoundException($"缺少配置文件 {fileName}。", path);
        return _protocol.Decode(typeName, File.ReadAllBytes(path));
    }

    private static string TranslateQuality(string quality) => quality switch
    {
        "Blue" => "普通",
        "Purple" => "稀有",
        "Orange" => "传说",
        _ => "未知"
    };
}
