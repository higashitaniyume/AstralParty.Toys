namespace AstralParty.Toys.Services;

public sealed class ConfigCatalog
{
    private readonly GameProtocolContext _protocol;
    private readonly string _dataDirectory;
    private readonly Dictionary<int, string> _characters = [];
    private readonly Dictionary<int, string> _characterNicks = [];
    private readonly Dictionary<int, string> _maps = [];
    private readonly Dictionary<int, string> _monsters = [];
    private readonly Dictionary<int, string> _items = [];
    private readonly Dictionary<int, string> _cards = [];
    private readonly Dictionary<int, (string Name, string Quality)> _relics = [];
    private readonly Dictionary<int, string[]> _characterAssets = [];
    private readonly Dictionary<int, string[]> _mapAssets = [];
    private readonly Dictionary<int, string[]> _monsterAssets = [];
    private readonly Dictionary<int, string[]> _relicAssets = [];

    private string _language;

    public string Language => _language;

    public ConfigCatalog(GameProtocolContext protocol, string dataDirectory, string language = AppLanguage.SimplifiedChinese)
    {
        _protocol = protocol;
        _dataDirectory = dataDirectory;
        _language = AppLanguage.Normalize(language);
        Load();
    }

    public void Reload(string? language = null)
    {
        if (!string.IsNullOrWhiteSpace(language)) _language = AppLanguage.Normalize(language);
        _characters.Clear();
        _characterNicks.Clear();
        _maps.Clear();
        _monsters.Clear();
        _items.Clear();
        _cards.Clear();
        _relics.Clear();
        _characterAssets.Clear();
        _mapAssets.Clear();
        _monsterAssets.Clear();
        _relicAssets.Clear();
        Load();
    }

    public string UnknownCharacter => _language switch
    {
        AppLanguage.English => "Unknown Character",
        AppLanguage.Japanese => "未知のキャラクター",
        _ => "未知角色"
    };

    public string Character(int id) => _characters.GetValueOrDefault(id, id == 0 ? UnknownCharacter : _language switch
    {
        AppLanguage.English => $"Character {id}",
        AppLanguage.Japanese => $"キャラクター {id}",
        _ => $"角色 {id}"
    });

    public string CharacterNick(int id) => _characterNicks.GetValueOrDefault(id, "");

    public string UnknownMap => _language switch
    {
        AppLanguage.English => "Unknown Map",
        AppLanguage.Japanese => "未知のマップ",
        AppLanguage.TraditionalChinese => "未知地圖",
        _ => "未知地图"
    };

    public string Map(int id) => _maps.GetValueOrDefault(id, id == 0 ? UnknownMap : _language switch
    {
        AppLanguage.English => $"Map {id}",
        AppLanguage.Japanese => $"マップ {id}",
        AppLanguage.TraditionalChinese => $"地圖 {id}",
        _ => $"地图 {id}"
    });

    public string UnknownMonster => _language switch
    {
        AppLanguage.English => "Unknown Monster",
        AppLanguage.Japanese => "未知のモンスター",
        _ => "未知怪物"
    };

    public string Monster(int id) => _monsters.GetValueOrDefault(id, id == 0 ? UnknownMonster : _language switch
    {
        AppLanguage.English => $"Monster {id}",
        AppLanguage.Japanese => $"モンスター {id}",
        _ => $"怪物 {id}"
    });

    public string Item(int id) => _items.GetValueOrDefault(id, _language switch
    {
        AppLanguage.English => $"Item {id}",
        AppLanguage.Japanese => $"アイテム {id}",
        _ => $"物品 {id}"
    });

    public string Card(int id) => _cards.TryGetValue(id, out var name) && name.Length > 0 ? name : _language switch
    {
        AppLanguage.English => $"Card {id}",
        AppLanguage.Japanese => $"カード {id}",
        _ => $"卡牌 {id}"
    };

    public string Relic(int id) => _relics.TryGetValue(id, out var value) ? value.Name : _language switch
    {
        AppLanguage.English => $"Relic {id}",
        AppLanguage.Japanese => $"レリック {id}",
        AppLanguage.TraditionalChinese => $"籌碼 {id}",
        _ => $"筹码 {id}"
    };

    public string RelicQuality(int id) => _relics.TryGetValue(id, out var value) ? TranslateQuality(value.Quality) : _language switch
    {
        AppLanguage.English => "Unknown",
        AppLanguage.Japanese => "不明",
        _ => "未知"
    };
    public string[] CharacterAssets(int id) => _characterAssets.GetValueOrDefault(id) ?? [];
    public string[] MapAssets(int id) => _mapAssets.GetValueOrDefault(id) ?? [];
    public string[] MonsterAssets(int id) => _monsterAssets.GetValueOrDefault(id) ?? [];
    public string[] RelicAssets(int id) => _relicAssets.GetValueOrDefault(id) ?? [];
    public IEnumerable<int> CharacterIds => _characters.Keys.OrderBy(id => id);
    public IEnumerable<int> MapIds => _maps.Keys.OrderBy(id => id);
    public IEnumerable<int> MonsterIds => _monsters.Keys.OrderBy(id => id);
    public IEnumerable<int> RelicIds => _relics.Keys.OrderBy(id => id);
    public IEnumerable<int> ItemIds => _items.Keys.OrderBy(id => id);
    public IEnumerable<int> CardIds => _cards.Keys.OrderBy(id => id);

    private void Load()
    {
        LoadNamed("CharacterConfigure", "Character.bin", "STRCharacterConfigure", "STRCharacter.bin", "NameID", _characters);
        LoadNamed("CharacterConfigure", "Character.bin", "STRCharacterConfigure", "STRCharacter.bin", "NickID", _characterNicks);
        LoadNamed("MapConfigure", "Map.bin", "STRMapConfigure", "STRMap.bin", "MapName", _maps);
        LoadNamed("MonsterConfigure", "Monster.bin", "STRMonsterConfigure", "STRMonster.bin", "NameID", _monsters);
        LoadNamed("ItemConfigure", "Item.bin", "STRItemConfigure", "STRItem.bin", "NameID", _items);
        LoadNamed("CardConfigure", "Card.bin", "STRCardConfigure", "STRCard.bin", "NameID", _cards);

        LoadAssets("CharacterConfigure", "Character.bin", _characterAssets, "CharacterMap");
        LoadAssets("MapConfigure", "Map.bin", _mapAssets, "MapImage", "MapSceneImage");
        LoadAssets("MonsterConfigure", "Monster.bin", _monsterAssets, "CharacterMap");

        var local = LoadLocalization("STRRelicConfigure", "STRRelic.bin");
        var config = DecodeFile("RelicConfigure", "Relic.bin");
        var defaultRelicPrefix = _language switch
        {
            AppLanguage.English => "Relic",
            AppLanguage.Japanese => "レリック",
            AppLanguage.TraditionalChinese => "籌碼",
            _ => "筹码"
        };
        foreach (var info in ReflectionValue.Items(ReflectionValue.Get(config, "Infos")))
        {
            var id = ReflectionValue.Int(info, "Id");
            var nameId = ReflectionValue.Int(info, "NameID");
            _relics[id] = (local.GetValueOrDefault(nameId, $"{defaultRelicPrefix} {id}"), ReflectionValue.Text(info, "RelicQualityType"));
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
        var prop = AppLanguage.ToProtoProperty(_language);
        return ReflectionValue.Items(ReflectionValue.Get(config, "Locals"))
            .ToDictionary(
                x => ReflectionValue.Int(x, "Id"),
                x =>
                {
                    var val = ReflectionValue.Text(x, prop);
                    return !string.IsNullOrWhiteSpace(val) ? val : ReflectionValue.Text(x, "Simplified");
                });
    }

    private object DecodeFile(string typeName, string fileName)
    {
        var path = Path.Combine(_dataDirectory, fileName);
        if (!File.Exists(path)) throw new FileNotFoundException($"缺少配置文件 {fileName}。", path);
        return _protocol.Decode(typeName, File.ReadAllBytes(path));
    }

    private string TranslateQuality(string quality) => (quality, _language) switch
    {
        ("Blue", AppLanguage.English) => "Common",
        ("Blue", AppLanguage.Japanese) => "通常",
        ("Blue", _) => "普通",
        ("Purple", AppLanguage.English) => "Rare",
        ("Purple", AppLanguage.Japanese) => "レア",
        ("Purple", _) => "稀有",
        ("Orange", AppLanguage.English) => "Legendary",
        ("Orange", AppLanguage.Japanese) => "レジェンド",
        ("Orange", AppLanguage.TraditionalChinese) => "傳說",
        ("Orange", _) => "传说",
        (_, AppLanguage.English) => "Unknown",
        (_, AppLanguage.Japanese) => "不明",
        _ => "未知"
    };
}
