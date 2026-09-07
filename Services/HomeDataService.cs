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

    private static readonly string[] UniversalGreetings =
    [
        "欢迎来到吉星派对！今天也要一起投掷出大吉的命运骰子吗？",
        "吉星高照！听说在对战前向看板娘打个招呼，摇出6点的概率会大幅提升哦！",
        "好久不见！今天有什么精彩的对局想要复盘或者研究吗？",
        "准备好开始新的棋盘冒险了吗？攻略、工具和回放我都替你整理好啦～",
        "今天在棋盘上，也要保持优雅又犀利的走位哦！",
        "叮～发现一名充满斗志的指挥官！今天想先看回放还是查阅攻略呢？",
        "听说合理的筹码组合是逆风翻盘的秘诀，快去看看攻略吧！",
        "悄悄告诉你，今天幸运星的坐标就在我们这边！"
    ];

    private static readonly Dictionary<int, string[]> HeroSpecificGreetings = new()
    {
        [101] =
        [
            "商业之主帕露南在此！只要精打细算，就能在吉星派对中积累出无可匹敌的星币优势！",
            "名义上我是商业之主，但在棋盘上，智慧和运筹帷幄才是真正的财富！",
            "只要仔细观察对手的筹码与走位，就能提前推算出他们的全部策略哦！"
        ],
        [102] =
        [
            "只要报酬丰厚……啊不是，古怪神探芬妮随时准备出击！",
            "想要获胜的话，可别只顾着往前冲，合理的星币理财才是硬道理！",
            "嘘……我刚才发现了一张超稀有的筹码卡，要不要借你看看？"
        ],
        [103] =
        [
            "魔法的奇迹可不是巧合，而是阿兰娜无数次精准计算的结果哦～",
            "水晶球显示，你今天在对局中会遇到意想不到的大惊喜！",
            "呼呼，今天想看我用哪一种华丽的魔法把对手送回起点呢？"
        ],
        [104] =
        [
            "保持专注！棋盘如同战场，绝不容许丝毫松懈！",
            "战术、策略、果断的决断，这才是小町克敌制胜的关键所在。",
            "指挥官，列阵完毕！随时可以进入下一场对战！"
        ],
        [105] =
        [
            "呜哇！派德曼差点把红茶洒出来了……别突然戳我啦！",
            "刚刚烤好了松饼，指挥官要先吃一块再去看回放分析吗？",
            "派对开始前，一定要把体力补充得满满的才行～"
        ],
        [106] =
        [
            "巡逻任务完成！帕帕拉报告：棋盘区域一切正常，无异常骚乱！",
            "遵守派对规则可是每个人的义务哦！若是作弊，手铐可不答应！",
            "今天也要堂堂正正地拿下胜利！"
        ],
        [107] =
        [
            "喵呜～恋觉得阳光正好，要在棋盘的草地上打个滚吗？",
            "特调一杯‘幸运猫薄荷苏打’送给你，喝了之后点数全都是大点哦！",
            "别走那么快嘛，留下来多陪恋玩一会儿嘛～"
        ]
    };

    private static readonly string[] ClickReactions =
    [
        "哎呀！戳到痒痒肉啦～",
        "再戳……再戳我就要把你的骰子点数变成1了哦！",
        "你在看哪里呀，指挥官？",
        "嘻嘻，感觉跟指挥官的羁绊值又悄悄增加了呢！",
        "怎么啦？是遇到难对付的对手了吗，快来让我看看回放！",
        "星光闪烁，幸运降临！"
    ];

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

        // 仅保留一张人物立绘 (芬妮 UT_Hero_Card_102_01.webp)
        var packedDir = Path.Combine(_appDirectory, "PackedAssets", "Portraits");
        var singlePortrait = Path.Combine(packedDir, "UT_Hero_Card_102_01.webp");
        if (File.Exists(singlePortrait))
        {
            results.Add(CreatePortraitItem("UT_Hero_Card_102_01", "https://assets.astral.local/Portraits/UT_Hero_Card_102_01.webp", true));
        }
        else
        {
            results.Add(CreatePortraitItem("UT_Hero_Card_102_01", "https://assets.astral.local/Portraits/UT_Hero_Card_102_01.webp", true));
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
        var portraits = GetPortraits();
        var announcements = GetAnnouncements();

        return new
        {
            portraitsCount = portraits.Count,
            portraits,
            announcements,
            greetings = UniversalGreetings,
            heroGreetings = HeroSpecificGreetings,
            clickReactions = ClickReactions,
            wikiUrl = "https://wiki.biligame.com/starengine/%E9%A6%96%E9%A1%B5"
        };
    }

    public static IReadOnlyList<object> GetAnnouncements() =>
    [
        new
        {
            id = "notice-01",
            category = "活动",
            tagClass = "badge-event",
            title = "【活动】「星辉狂欢盛典」限时开启！累计对局赢取限定看板立绘与专属棋盘",
            date = "2026-09-06",
            isNew = true,
            summary = "全新派对庆典盛大揭幕！完成每日对局与筹码挑战即可兑换限定纪念角色立绘与动态表情。",
            content = "亲爱的派对指挥官：\n\n「星辉狂欢盛典」现已全面开启！\n\n【活动时间】\n2026年9月6日维护后 - 2026年9月26日 23:59\n\n【核心玩法】\n1. 每日参与匹配对战，赢取「星辉代币」；\n2. 达成指定筹码三级强化，解锁专属荣誉成就；\n3. 庆典商店开放「帕露南」与「芬妮」珍藏立绘换领。\n\n祝各位指挥官掷骰顺遂，派对尽兴！"
        },
        new
        {
            id = "notice-02",
            category = "更新",
            tagClass = "badge-update",
            title = "【更新】v2.4.0 版本发布：全新回放分析引擎与离线数据解析系统上线",
            date = "2026-09-04",
            isNew = true,
            summary = "全面重构本地回放查看器，支持全量 Protocol 协议帧解析、筹码流转追踪以及毫秒级事件时间线。",
            content = "亲爱的派对指挥官：\n\n为了给广大玩家与攻略创作者提供更深入、更顺畅的复盘体验，v2.4.0 现已更新上线！\n\n【主要更新内容】\n1. 【全新主界面】：引入看板娘立绘互动欢迎页与三大核心功能导航；\n2. 【工具中心整合】：对局回放分析工具深度优化，支持自动扫描 LocalLow 本地回放；\n3. 【协议帧检视】：内置 Protobuf 数据结构解码器，支持每一帧指令与载荷查看；\n4. 【维基图鉴】：集成全英雄图鉴与 SSR 筹码速查库。\n\n感谢大家一直以来的支持与反馈！"
        },
        new
        {
            id = "notice-03",
            category = "维护",
            tagClass = "badge-maintenance",
            title = "【维护】全服网络节点扩容与房间对战帧同步优化完成",
            date = "2026-09-02",
            isNew = false,
            summary = "针对跨区联机延迟及断线重连逻辑进行了全量修复，大幅提升多人派对对战的流畅度。",
            content = "各位玩家朋友：\n\n服务器已于 9月2日 清晨完成无缝热更新，本次维护针对多人联机环境进行了多项底层优化：\n\n- 修复了极端网络波动下可能出现的卡帧与动作脱节；\n- 优化断线重连同步机制，重连速度提升 60%；\n- 补偿礼包已发放到全体注册邮箱，请及时查收。"
        },
        new
        {
            id = "notice-04",
            category = "公告",
            tagClass = "badge-notice",
            title = "【平衡】关于「筹码地块」购买机制与部分英雄技能数值微调说明",
            date = "2026-08-30",
            isNew = false,
            summary = "为了提升战术博弈的多样性，对部分高费筹码的效果持续回合及基础移动点数进行了适度平衡。",
            content = "各位指挥官好：\n\n根据近期天梯排位与玩家对局数据，我们对以下内容进行了微调：\n\n1. 【筹码调整】：微调了部分攻击型筹码的增伤梯度，避免开局秒杀带来的挫败感；\n2. 【角色微调】：提升了部分辅助型角色的基础防御与自保能力；\n3. 详细调整数值已在游戏内维基图鉴同步更新。"
        }
    ];
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
