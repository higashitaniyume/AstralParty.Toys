using System;
using System.Collections.Generic;
using System.Linq;

namespace AstralParty.Toys.Services.Replay;

public sealed partial class ReplayAnalyzer
{
    private void FillUiAssets(ReplayReport report)
    {
        var assets = new Dictionary<string, string>
        {
            ["starCoin"] = _assets.Named("UT_Item_Currency_1"),
            ["hp"] = _assets.Named("UT_Relic_Hp", "UT_Buff_HPUp"),
            ["attack"] = _assets.Named("UT_Relic_Attack", "UT_Buff_AttackUp"),
            ["defense"] = _assets.Named("UT_Relic_Defend", "UT_Buff_DefUp"),
            ["damage"] = _assets.Named("UT_Buff_DamagedUp"),
            ["healing"] = _assets.Named("UT_Relic_Heal", "UT_Buff_Heal"),
            ["move"] = _assets.Named("UT_Relic_Speed", "UT_Buff_MoveUp"),
            ["relicLand"] = _assets.Named("UT_Platform_Relic"),
            ["mission"] = _assets.Named("UT_Platform_Event"),
            ["card"] = _assets.Named("UT_Platform_Card"),
            ["monster"] = _assets.Named("UT_Platform_Monster")
        };
        foreach (var pair in assets.Where(pair => pair.Value.Length > 0)) report.UiAssets[pair.Key] = pair.Value;
    }

    private string EventIcon(string key)
    {
        var local = _assets.Event(key);
        if (local.Length > 0) return local;
        var names = key switch
        {
            "gold" => new[] { "UT_Item_Currency_1", "UT_Platform_Gold" },
            "relic" => ["UT_Platform_Relic"],
            "card" => ["UT_Platform_Card"],
            "heal" => ["UT_Platform_Heal", "UT_Relic_Heal"],
            "monster" or "battle" => ["UT_Platform_Monster"],
            "move" => ["UT_Platform_MoveAgain", "UT_Relic_Speed"],
            "shop" => ["UT_Platform_Shop"],
            "event" or "mission" => ["UT_Platform_Event"],
            "turn" => ["UT_Platform_Start"],
            "attribute" => ["UT_Buff_HPUp"],
            _ => Array.Empty<string>()
        };
        return names.Length > 0 ? _assets.Named(names) : "";
    }

    private string LocalizeWinner(long winner)
    {
        if (winner <= 0)
        {
            return _config.Language switch
            {
                AppLanguage.English => "No winner recorded",
                AppLanguage.Japanese => "勝者記録なし",
                AppLanguage.TraditionalChinese => "未記錄獲勝方",
                _ => "未记录获胜方"
            };
        }
        return _config.Language switch
        {
            AppLanguage.English => $"Winner {winner}",
            AppLanguage.Japanese => $"勝者 {winner}",
            AppLanguage.TraditionalChinese => $"獲勝方 {winner}",
            _ => $"获胜方 {winner}"
        };
    }

    private string LocalizeResult(long winner)
    {
        if (winner > 0)
        {
            return _config.Language switch
            {
                AppLanguage.English => "Victory",
                AppLanguage.Japanese => "勝利",
                AppLanguage.TraditionalChinese => "勝利",
                _ => "胜利"
            };
        }
        return _config.Language switch
        {
            AppLanguage.English => "Finished",
            AppLanguage.Japanese => "終了",
            AppLanguage.TraditionalChinese => "已結束",
            _ => "已结束"
        };
    }

    private string LocalizeAwards(string awards)
    {
        if (string.IsNullOrEmpty(awards))
        {
            return _config.Language switch
            {
                AppLanguage.English => "No awards recorded",
                AppLanguage.Japanese => "報酬記録なし",
                AppLanguage.TraditionalChinese => "未記錄獎勵",
                _ => "未记录奖励"
            };
        }
        return awards;
    }

    private string LocalizeFinalStatus(bool isHeroNull, bool isBot, int hp)
    {
        if (isHeroNull)
        {
            if (isBot)
            {
                return _config.Language switch
                {
                    AppLanguage.English => "Bot placeholder · No final data",
                    AppLanguage.Japanese => "BOTプレースホルダー · 最終データなし",
                    AppLanguage.TraditionalChinese => "機器人佔位 · 無末局英雄數據",
                    _ => "机器人占位 · 无末局英雄数据"
                };
            }
            return _config.Language switch
            {
                AppLanguage.English => "No final hero data",
                AppLanguage.Japanese => "最終ヒーローデータなし",
                AppLanguage.TraditionalChinese => "無末局英雄數據",
                _ => "无末局英雄数据"
            };
        }

        if (hp > 0)
        {
            return _config.Language switch
            {
                AppLanguage.English => "Survived until settlement",
                AppLanguage.Japanese => "決着まで生存",
                AppLanguage.TraditionalChinese => "存活至結算",
                _ => "存活至结算"
            };
        }

        return _config.Language switch
        {
            AppLanguage.English => "HP reached 0 at settlement",
            AppLanguage.Japanese => "決着時HPが0",
            AppLanguage.TraditionalChinese => "結算時生命值為 0",
            _ => "结算时生命值为 0"
        };
    }

    private string RelicSource(int level, int supLv, int purchaseCost)
    {
        if (purchaseCost > 0)
        {
            return _config.Language switch
            {
                AppLanguage.English => "Relic Tile Purchase",
                AppLanguage.Japanese => "レリックマス購入",
                AppLanguage.TraditionalChinese => "籌碼地塊購買",
                _ => "筹码地块购买"
            };
        }
        if (supLv > 0)
        {
            return _config.Language switch
            {
                AppLanguage.English => "Star Up",
                AppLanguage.Japanese => "昇格",
                AppLanguage.TraditionalChinese => "升星",
                _ => "升星"
            };
        }
        return _config.Language switch
        {
            AppLanguage.English => "Mission",
            AppLanguage.Japanese => "ミッション",
            AppLanguage.TraditionalChinese => "任務",
            _ => "任务"
        };
    }

    private string RelicSourceIcon(string source)
    {
        if (source.Contains("购买") || source.Contains("商店") || source.Contains("地块") ||
            source.Contains("Purchase") || source.Contains("購入"))
            return EventIcon("shop");
        if (source.Contains("升星") || source.Contains("Star") || source.Contains("昇格"))
            return EventIcon("attribute");
        if (source.Contains("任务") || source.Contains("Mission") || source.Contains("ミッション") || source.Contains("任務"))
            return EventIcon("mission");
        return EventIcon("relic");
    }

    private sealed class PendingRelicOffer
    {
        public int Level { get; init; }
        public string Source { get; init; } = "来源未记录";
        public string SourceIconPath { get; init; } = "";
        public int RefreshCount { get; set; }
        public int PurchaseCost { get; init; }
    }

    /// <summary>进行中的一次商店进店（等待购买回执 / 关闭回执后落盘）。</summary>
    private sealed class PendingShopVisit
    {
        public int FrameIndex { get; init; }
        public int Round { get; init; }
        public long PlayerId { get; init; }
        public string ShopType { get; init; } = "商店";
        public int[] Cards { get; init; } = [];
        public int Price { get; init; }
        public int Discount { get; init; }
        public int FreeCard { get; init; }
        public int FreeCardNum { get; init; }
        public bool[] Alreadys { get; set; } = [];
        public List<int> BuyIndices { get; } = [];
        public bool IsClosed { get; set; }
    }
}
