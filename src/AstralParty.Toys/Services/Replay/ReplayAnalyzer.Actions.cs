using System;
using System.Collections.Generic;
using System.Linq;

namespace AstralParty.Toys.Services.Replay;

public sealed partial class ReplayAnalyzer
{
    private void ParseActions(ProtocolFrame frame, int round, IReadOnlyDictionary<long, string> playerNames,
        IReadOnlyDictionary<long, string> playerHeroes,
        HashSet<long> seenActions, Dictionary<long, PendingRelicOffer> pendingRelicOffers,
        Dictionary<long, int> pendingRelicPurchaseCosts, HashSet<long> confirmedRelicPurchases,
        Dictionary<long, PendingShopVisit> pendingShopVisits,
        Dictionary<string, int> actionCounts, ReplayReport report)
    {
        var predict = _protocol.TryDecode(1002, frame.Payload);
        if (predict is null) return;
        foreach (var action in ReflectionValue.Items(ReflectionValue.Get(predict, "Actions")))
        {
            var actionId = ReflectionValue.Int(action, "Id");
            var sn = ReflectionValue.Long(action, "Sn");
            if (sn != 0 && !seenActions.Add(sn)) continue;
            var playerId = ReflectionValue.Long(action, "PlayerId");
            var meta = ActionNames.TryGetValue(actionId, out var knownAction)
                ? knownAction
                : (Name: $"动作 {actionId}", Key: "event");
            actionCounts[$"{actionId} · {meta.Name}"] = actionCounts.GetValueOrDefault($"{actionId} · {meta.Name}") + 1;

            if (actionId == 5249)
            {
                try
                {
                    var purchase = _protocol.Decode("party.protocol.BuyRelicC2S", ReflectionValue.Bytes(ReflectionValue.Get(action, "Data")));
                    pendingRelicPurchaseCosts[playerId] = ReflectionValue.Int(purchase, "RelicGold");
                }
                catch { }
                continue;
            }

            if (actionId == 5250)
            {
                confirmedRelicPurchases.Add(playerId);
                continue;
            }

            if (actionId is 5029 or 5215)
            {
                HandleShopAction(frame.Index, action, actionId, round, playerNames, playerHeroes, pendingShopVisits, report);
                continue;
            }

            if (actionId == 5323)
            {
                HandleVendorAction(frame.Index, action, round, playerNames, playerHeroes, report);
                continue;
            }

            if (actionId == 5211)
            {
                try
                {
                    var offer = _protocol.Decode("party.protocol.SelectRelicC2S", ReflectionValue.Bytes(ReflectionValue.Get(action, "Data")));
                    var level = ReflectionValue.Int(offer, "Lv");
                    var supLv = ReflectionValue.Int(offer, "SupLv");
                    var purchaseCost = pendingRelicPurchaseCosts.TryGetValue(playerId, out var cost) &&
                                       confirmedRelicPurchases.Remove(playerId) ? cost : 0;
                    pendingRelicPurchaseCosts.Remove(playerId);
                    var source = pendingRelicOffers.TryGetValue(playerId, out var existing) && existing.Level == level
                        ? existing.Source
                        : RelicSource(level, supLv, purchaseCost);
                    var sourceIcon = RelicSourceIcon(source);
                    var refreshCount = pendingRelicOffers.TryGetValue(playerId, out var previous) && previous.Level == level
                        ? previous.RefreshCount + 1
                        : 0;
                    pendingRelicOffers[playerId] = new PendingRelicOffer
                    {
                        Level = level,
                        Source = source,
                        SourceIconPath = sourceIcon,
                        RefreshCount = refreshCount,
                        PurchaseCost = purchaseCost
                    };
                    var ids = ReflectionValue.Items(ReflectionValue.Get(offer, "Relics")).Select(x => Convert.ToInt32(x)).ToList();
                    var options = string.Join("、", ids.Select(id => $"{_config.Relic(id)}（{_config.RelicQuality(id)}）"));
                    report.Relics.Add(new RelicRecord
                    {
                        FrameIndex = frame.Index,
                        Round = round,
                        PlayerId = playerId,
                        PlayerName = PlayerName(playerNames, playerId),
                        HeroName = playerHeroes.GetValueOrDefault(playerId, "未知角色"),
                        Kind = refreshCount > 0 ? "刷新候选" : "首次候选",
                        Level = level,
                        OptionsText = options,
                        Options = ids.Select(BuildRelicDisplay).ToArray(),
                        Source = source,
                        SourceIconPath = sourceIcon,
                        RefreshNumber = refreshCount,
                        IsRefresh = refreshCount > 0
                    });
                    report.Events.Add(new TimelineEvent
                    {
                        FrameIndex = frame.Index,
                        Round = round,
                        PlayerId = playerId,
                        PlayerName = PlayerName(playerNames, playerId),
                        Type = "筹码",
                        Title = refreshCount > 0 ? $"第 {refreshCount} 次刷新筹码" : $"{source} · 出现三个候选筹码",
                        Description = $"{PlayerName(playerNames, playerId)}{(refreshCount > 0 ? "刷新后看到了" : "可以从中选择")}：{options}" + (purchaseCost > 0 ? $"（价格 {purchaseCost} 星币）" : ""),
                        IconPath = sourceIcon.Length > 0 ? sourceIcon : EventIcon("relic")
                    });
                    continue;
                }
                catch { }
                continue;
            }

            report.Events.Add(new TimelineEvent
            {
                FrameIndex = frame.Index,
                Round = round,
                PlayerId = playerId,
                PlayerName = PlayerName(playerNames, playerId),
                Type = "行动",
                Title = meta.Name,
                Description = playerId == 0 ? meta.Name : $"{PlayerName(playerNames, playerId)}进行了{meta.Name}",
                IconPath = EventIcon(meta.Key)
            });
        }
    }

    private RelicDisplay BuildRelicDisplay(int id) => new()
    {
        Id = id,
        Name = _config.Relic(id),
        Quality = _config.RelicQuality(id),
        ImagePath = _assets.Relic(id, _config.RelicAssets(id))
    };

    /// <summary>处理卡牌商店 Action（5029 PVP / 5215 PVE）：进店候选。</summary>
    private void HandleShopAction(int frameIndex, object action, int actionId, int round,
        IReadOnlyDictionary<long, string> playerNames,
        IReadOnlyDictionary<long, string> playerHeroes, Dictionary<long, PendingShopVisit> pendingShopVisits,
        ReplayReport report)
    {
        var playerId = ReflectionValue.Long(action, "PlayerId");
        var typeName = actionId == 5029 ? "party.protocol.ShopBuyC2S" : "party.protocol.PVEShopBuyC2S";
        object message;
        try { message = _protocol.Decode(typeName, ReflectionValue.Bytes(ReflectionValue.Get(action, "Data"))); }
        catch { return; }

        var cards = ReflectionValue.Items(ReflectionValue.Get(message, "Cards")).Select(x => Convert.ToInt32(x)).ToArray();
        var alreadys = ReflectionValue.Items(ReflectionValue.Get(message, "Alreadys")).Select(x => Convert.ToBoolean(x)).ToArray();
        var gold = ReflectionValue.Int(message, "Gold");
        var discount = ReflectionValue.Int(message, "DisCountGold");
        var freeCard = ReflectionValue.Int(message, "FreeCard");
        var freeCardNum = ReflectionValue.Int(message, "FreeCardNum");
        var shopType = actionId == 5029 ? "PVP商店" : "PVE商店";

        // 同一候选组重复广播（同 Sn 已去重；这里再按候选内容去重合并为一次进店）
        if (pendingShopVisits.TryGetValue(playerId, out var existing))
        {
            if (existing.Cards.SequenceEqual(cards))
            {
                // 同一次进店的后续广播：只更新售罄标记，不新增记录
                if (alreadys.Length > 0) existing.Alreadys = alreadys;
                return;
            }

            // 上一次进店结束（候选变了）：把旧访问落进报告（含购买结果）
            report.Shops.Add(BuildShopRecord(existing, playerNames, playerHeroes));
        }

        var visit = new PendingShopVisit
        {
            FrameIndex = frameIndex,
            Round = round,
            PlayerId = playerId,
            ShopType = shopType,
            Cards = cards,
            Price = gold,
            Discount = discount,
            FreeCard = freeCard,
            FreeCardNum = freeCardNum,
            Alreadys = alreadys
        };
        pendingShopVisits[playerId] = visit;

        report.Events.Add(new TimelineEvent
        {
            FrameIndex = frameIndex,
            Round = round,
            PlayerId = playerId,
            PlayerName = PlayerName(playerNames, playerId),
            Type = "商店",
            Title = $"进入{shopType}",
            Description = $"{PlayerName(playerNames, playerId)}走进商店，货架上有：{string.Join("、", cards.Select(_config.Card))}" +
                          (gold > 0 ? $"（每张 {gold} 星币{(discount > 0 ? $"，折后 {Math.Max(0, gold - discount)}" : "")}）" : ""),
            IconPath = EventIcon("shop")
        });
    }

    /// <summary>处理商人买卡 Action（5323）。</summary>
    private void HandleVendorAction(int frameIndex, object action, int round, IReadOnlyDictionary<long, string> playerNames,
        IReadOnlyDictionary<long, string> playerHeroes, ReplayReport report)
    {
        var playerId = ReflectionValue.Long(action, "PlayerId");
        object message;
        try { message = _protocol.Decode("party.protocol.VendorBuyCardC2S", ReflectionValue.Bytes(ReflectionValue.Get(action, "Data"))); }
        catch { return; }

        var cardId = ReflectionValue.Int(message, "CardId");
        var gold = ReflectionValue.Int(message, "Gold");
        var isBuy = ReflectionValue.Bool(message, "IsBuy");
        var visit = new PendingShopVisit
        {
            FrameIndex = frameIndex,
            Round = round,
            PlayerId = playerId,
            ShopType = "商人买卡",
            Cards = cardId > 0 ? [cardId] : [],
            Price = gold,
            IsClosed = true
        };
        if (isBuy && cardId > 0) visit.BuyIndices.Add(0);
        report.Shops.Add(BuildShopRecord(visit, playerNames, playerHeroes));
        report.Events.Add(new TimelineEvent
        {
            FrameIndex = frameIndex,
            Round = round,
            PlayerId = playerId,
            PlayerName = PlayerName(playerNames, playerId),
            Type = "商店",
            Title = isBuy ? "商人买卡" : "商人未买卡",
            Description = $"{PlayerName(playerNames, playerId)}在商人处{(isBuy ? $"买下了「{_config.Card(cardId)}」" : "没有买卡")}",
            IconPath = EventIcon("shop")
        });
    }

    private ShopRecord BuildShopRecord(PendingShopVisit visit, IReadOnlyDictionary<long, string> playerNames,
        IReadOnlyDictionary<long, string> playerHeroes)
    {
        var bought = visit.BuyIndices
            .Where(index => index >= 0 && index < visit.Cards.Length)
            .Select(index => $"第{index + 1}个 {_config.Card(visit.Cards[index])}")
            .ToList();
        var boughtText = bought.Count > 0 ? string.Join("、", bought) : "";
        return new ShopRecord
        {
            FrameIndex = visit.FrameIndex,
            Round = visit.Round,
            PlayerId = visit.PlayerId,
            PlayerName = PlayerName(playerNames, visit.PlayerId),
            HeroName = playerHeroes.GetValueOrDefault(visit.PlayerId, "未知角色"),
            ShopType = visit.ShopType,
            Cards = visit.Cards,
            OptionsText = string.Join("、", visit.Cards.Select(_config.Card)),
            Price = visit.Price,
            Discount = visit.Discount,
            FreeCard = visit.FreeCard,
            FreeCardNum = visit.FreeCardNum,
            Alreadys = visit.Alreadys,
            BuyIndices = visit.BuyIndices.ToArray(),
            BoughtText = boughtText,
            IsClosed = visit.IsClosed
        };
    }
}
