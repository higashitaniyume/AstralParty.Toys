using System.Text.RegularExpressions;

namespace AstralParty.Toys.Services;

public sealed class HomeDataService
{
    private static readonly Dictionary<int, string> HeroNames = new()
    {
        [101] = "帕露南",
        [102] = "芬妮",
        [103] = "阿兰娜",
        [104] = "小町",
        [105] = "派德曼",
        [106] = "帕帕拉",
        [1061] = "机械帕帕拉",
        [107] = "恋",
        [108] = "米米",
        [109] = "Z3000",
        [110] = "潘大猛",
        [111] = "墨影",
        [112] = "璐璐",
        [113] = "姬梦枫",
        [114] = "蓝海晴",
        [115] = "美咲",
        [116] = "娜蒂斯",
        [117] = "茉莉",
        [118] = "阿尔",
        [119] = "星魅琉华",
        [120] = "南希露",
        [121] = "凛",
        [122] = "梅加斯",
        [123] = "姬梦朝",
        [124] = "照",
        [125] = "摩西",
        [126] = "真梦梓",
        [127] = "邦妮",
        [128] = "玲玲",
        [129] = "赛克斯",
        [301] = "超绝最可爱天使酱",
        [302] = "主播女孩",
        [303] = "吉尔·斯汀雷",
        [304] = "多萝西·海兹",
        [305] = "远野汉娜",
        [306] = "橘雪莉"
    };

    private static readonly Dictionary<int, string> HeroTitles = new()
    {
        [101] = "商业之主",
        [102] = "古怪神探",
        [103] = "社恐修女",
        [104] = "暗影忍者",
        [105] = "社员叔叔",
        [106] = "猩红辣妹",
        [1061] = "猩红辣妹",
        [107] = "游戏大师",
        [108] = "看板娘",
        [109] = "垃圾箱",
        [110] = "肉弹战车",
        [111] = "小猎手",
        [112] = "史莱姆",
        [113] = "旗袍娘",
        [114] = "命运少女",
        [115] = "太刀使",
        [116] = "绿洲女王",
        [117] = "家政机器人",
        [118] = "暗区少主",
        [119] = "午夜闪光",
        [120] = "网络魅影",
        [121] = "新人调查员",
        [122] = "机械超人",
        [123] = "风水师",
        [124] = "三神御主",
        [125] = "枪匠",
        [126] = "沼之蛟龙",
        [127] = "毒苹果",
        [128] = "怪力乱神",
        [129] = "魔渊幼体",
        [301] = "超天酱",
        [302] = "糖糖",
        [303] = "吉尔",
        [304] = "多萝西",
        [305] = "汉娜",
        [306] = "雪莉"
    };

    private readonly string _appDirectory;
    private readonly ConfigCatalog? _catalog;
    private List<PortraitItem>? _cachedPortraits;

    public HomeDataService(string appDirectory, ConfigCatalog? catalog = null)
    {
        _appDirectory = appDirectory;
        _catalog = catalog;
        if (_catalog is null)
        {
            try
            {
                var protocolDir = Path.Combine(_appDirectory, "Protocol");
                var dataDir = Path.Combine(_appDirectory, "GameData");
                if (Directory.Exists(protocolDir) && Directory.Exists(dataDir))
                {
                    var protocol = new GameProtocolContext(protocolDir);
                    _catalog = new ConfigCatalog(protocol, dataDir);
                }
            }
            catch
            {
                // Fallback to internal dictionary
            }
        }
    }

    public IReadOnlyList<PortraitItem> GetPortraits()
    {
        if (_cachedPortraits is not null) return _cachedPortraits;

        var results = new List<PortraitItem>();

        foreach (var portraitName in new[] { "UT_Hero_Card_306", "UT_Hero_Card_305" })
        {
            results.Add(CreatePortraitItem(
                portraitName,
                $"https://assets.astral.local/Portraits/{portraitName}.png",
                true));
        }

        _cachedPortraits = results;
        return _cachedPortraits;
    }

    private PortraitItem CreatePortraitItem(string baseName, string url, bool isEmbedded)
    {
        var match = Regex.Match(baseName, @"^UT_Hero_Card_(\d+)(?:_(\w+))?$", RegexOptions.IgnoreCase);
        var heroId = 101;
        var variant = "默认";
        var isMax = baseName.Contains("Max", StringComparison.OrdinalIgnoreCase);

        if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedId))
        {
            heroId = parsedId;
            if (match.Groups[2].Success)
            {
                var suffix = match.Groups[2].Value;
                variant = suffix.Equals("Max", StringComparison.OrdinalIgnoreCase) ? "觉醒 Max" : $"皮肤 {suffix}";
            }
        }

        var heroName = _catalog?.Character(heroId) ?? HeroNames.GetValueOrDefault(heroId, $"角色 {heroId}");
        if (heroName.StartsWith("角色 ") && HeroNames.TryGetValue(heroId, out var mappedName))
        {
            heroName = mappedName;
        }

        var heroNick = _catalog?.CharacterNick(heroId);
        var heroTitle = !string.IsNullOrWhiteSpace(heroNick) ? heroNick : HeroTitles.GetValueOrDefault(heroId, "吉星派对冒险家");

        return new PortraitItem
        {
            Id = baseName,
            HeroId = heroId,
            HeroName = heroName,
            Title = heroTitle,
            Variant = variant,
            IsMax = isMax,
            Url = url,
            IsEmbedded = isEmbedded
        };
    }

    public object GetHomeData()
    {
        return new
        {
            portraits = GetPortraits()
        };
    }
}

public sealed class PortraitItem
{
    public string Id { get; init; } = "";
    public int HeroId { get; init; }
    public string HeroName { get; init; } = "";
    public string Title { get; init; } = "";
    public string Variant { get; init; } = "";
    public bool IsMax { get; init; }
    public string Url { get; init; } = "";
    public bool IsEmbedded { get; init; }
}
