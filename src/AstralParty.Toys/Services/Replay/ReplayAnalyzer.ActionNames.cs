using System.Collections.Generic;

namespace AstralParty.Toys.Services.Replay;

public sealed partial class ReplayAnalyzer
{
    private static readonly IReadOnlyDictionary<int, (string Name, string Key)> ActionNames =
        new Dictionary<int, (string, string)>
        {
            [5021] = ("掷骰", "dice"),
            [5027] = ("移动", "move"),
            [5029] = ("购买", "purchase"),
            [5033] = ("战斗", "battle"),
            [5035] = ("回合流转", "turn"),
            [5037] = ("行动开始", "turn"),
            [5039] = ("行动结束", "turn"),
            [5041] = ("使用卡牌", "card"),
            [5047] = ("地块事件", "land"),
            [5049] = ("战斗结算", "battle"),
            [5053] = ("技能行动", "skill"),
            [5055] = ("属性结算", "attribute"),
            [5059] = ("商店事件", "shop"),
            [5063] = ("随机事件", "event"),
            [5067] = ("特殊移动", "move"),
            [5069] = ("恢复事件", "heal"),
            [5071] = ("星币事件", "gold"),
            [5073] = ("角色事件", "character"),
            [5075] = ("地图事件", "map"),
            [5077] = ("特殊事件", "event"),
            [5081] = ("Buff 事件", "buff"),
            [5083] = ("效果事件", "effect"),
            [5093] = ("任务事件", "mission"),
            [5211] = ("筹码候选", "relic"),
            [5213] = ("怪物追击", "monster"),
            [5215] = ("PVE 商店", "shop"),
            [5249] = ("到达筹码地块", "relic"),
            [5259] = ("筹码事件", "relic"),
            [5309] = ("PVE 事件", "event"),
            [5313] = ("PVE 事件", "event"),
            [5317] = ("特殊事件", "event"),
            [5323] = ("PVE 事件", "event"),
            [5377] = ("特殊事件", "event")
        };
}
