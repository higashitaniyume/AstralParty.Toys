using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AstralParty.Toys.Services;

public sealed class ReplayAnalyzer
{
    private const int MaxFileSize = 512 * 1024 * 1024;
    private const int MaxPayloadSize = 64 * 1024 * 1024;

    private readonly GameProtocolContext _protocol;
    private readonly ConfigCatalog _config;
    private readonly AssetLocator _assets;

    public ConfigCatalog Config => _config;

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

    public ReplayAnalyzer(
        string appDirectory,
        string? assetDirectory = null,
        IEnumerable<string>? materialDirectories = null,
        bool useEmbeddedAssets = true)
    {
        _protocol = new GameProtocolContext(Path.Combine(appDirectory, "Protocol"));
        _config = new ConfigCatalog(_protocol, Path.Combine(appDirectory, "GameData"));
        _assets = new AssetLocator(
            assetDirectory ?? Path.Combine(appDirectory, "Assets"),
            materialDirectories ?? MaterialSource.DiscoverAll(appDirectory),
            useEmbeddedAssets);
    }

    public ReplayReport Analyze(string filePath, IProgress<string>? progress = null)
    {
        progress?.Report("读取回放文件…");
        var file = new FileInfo(filePath);
        if (!file.Exists) throw new FileNotFoundException("回放文件不存在。", filePath);
        if (file.Length == 0) throw new InvalidDataException("回放文件为空。");
        if (file.Length > MaxFileSize) throw new InvalidDataException("回放文件超过 512 MB 安全限制。");

        var bytes = File.ReadAllBytes(filePath);
        var frames = ReadFrames(bytes);
        progress?.Report($"已拆分 {frames.Count:N0} 帧，正在解析协议…");

        var report = new ReplayReport
        {
            FilePath = file.FullName,
            FileName = file.Name,
            FileSizeText = FormatSize(file.Length),
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            FrameCount = frames.Count
        };
        foreach (var frame in frames) report.Frames.Add(frame);
        FillUiAssets(report);

        var runningFrame = frames.FirstOrDefault(x => x.CmdId == 1003)
            ?? throw new InvalidDataException("找不到开局锚点 RunningGameS2C（cmd 1003）。");
        var running = _protocol.TryDecode(1003, runningFrame.Payload)
            ?? throw new InvalidDataException("RunningGameS2C 解码失败，可能与当前游戏协议版本不兼容。");
        var initialRoom = ReflectionValue.Get(running, "Room")
            ?? throw new InvalidDataException("开局消息中没有 Room 数据。");

        var finishFrame = frames.LastOrDefault(x => x.CmdId == 1016);
        var finish = finishFrame is null ? null : _protocol.TryDecode(1016, finishFrame.Payload);
        var snapshots = frames.Where(x => x.CmdId == 1113)
            .Select(x => (Frame: x, Value: _protocol.TryDecode(1113, x.Payload)))
            .Where(x => x.Value is not null)
            .Select(x => (x.Frame, Value: x.Value!))
            .ToList();
        var finalRoom = snapshots.Count > 0 ? ReflectionValue.Get(snapshots[^1].Value, "Room") : initialRoom;

        FillMatchSummary(report, initialRoom, finalRoom, finish, snapshots);
        var players = FillPlayers(report, initialRoom, finalRoom);
        var playerNames = players.ToDictionary(x => x.Id, x => x.Nickname);
        var playerHeroes = players.ToDictionary(x => x.Id, x => x.HeroName);
        report.BossName = FindBossName(finalRoom);

        var commandCounts = frames.GroupBy(x => x.CmdId).ToDictionary(x => x.Key, x => x.Count());
        report.CommandTypeCount = commandCounts.Count;
        FillStats(commandCounts.ToDictionary(x => $"{x.Key} · {_protocol.MessageName(x.Key)}", x => x.Value), frames.Count, report.CommandStats);

        var actionCounts = new Dictionary<string, int>();
        var qualityCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var selectedByPlayer = new Dictionary<long, List<int>>();
        var seenActions = new HashSet<long>();
        var pendingRelicOffers = new Dictionary<long, PendingRelicOffer>();
        var pendingRelicPurchaseCosts = new Dictionary<long, int>();
        var confirmedRelicPurchases = new HashSet<long>();
        var pendingShopVisits = new Dictionary<long, PendingShopVisit>();
        var currentRound = 0;
        var currentPlayer = 0L;
        var lastSnapshotFrame = snapshots.Count > 0 ? snapshots[^1].Frame.Index : -1;

        foreach (var frame in frames)
        {
            if (frame.CmdId == 1113)
            {
                var snapshot = _protocol.TryDecode(1113, frame.Payload);
                if (snapshot is null) continue;
                currentPlayer = ReflectionValue.Long(snapshot, "PlayerId");
                var room = ReflectionValue.Get(snapshot, "Room");
                currentRound = ReflectionValue.Int(room, "Round");
                if (frame.Index != lastSnapshotFrame)
                {
                    report.Events.Add(new TimelineEvent
                    {
                        FrameIndex = frame.Index,
                        Round = currentRound,
                        PlayerId = currentPlayer,
                        PlayerName = PlayerName(playerNames, currentPlayer),
                        Type = "回合",
                        Title = "行动回合开始",
                        Description = $"{PlayerName(playerNames, currentPlayer)} 的行动快照",
                        IconPath = EventIcon("turn")
                    });
                }
                continue;
            }

            if (frame.CmdId == 1002)
            {
                ParseActions(frame, currentRound, playerNames, playerHeroes, seenActions, pendingRelicOffers,
                    pendingRelicPurchaseCosts, confirmedRelicPurchases, pendingShopVisits, actionCounts, report);
                continue;
            }

            if (frame.CmdId == 5212)
            {
                var message = _protocol.TryDecode(5212, frame.Payload);
                if (message is null) continue;
                var playerId = ReflectionValue.Long(message, "PlayerId");
                var relicId = ReflectionValue.Int(message, "RelicId");
                var reroll = ReflectionValue.Bool(message, "IsReroll");
                var relicName = relicId > 0 ? _config.Relic(relicId) : "—";
                var quality = relicId > 0 ? _config.RelicQuality(relicId) : "—";
                pendingRelicOffers.TryGetValue(playerId, out var pendingOffer);
                var refreshNumber = pendingOffer?.RefreshCount ?? 0;
                if (reroll)
                {
                    report.Events.Add(new TimelineEvent
                    {
                        FrameIndex = frame.Index,
                        Round = currentRound,
                        PlayerId = playerId,
                        PlayerName = PlayerName(playerNames, playerId),
                        Type = "筹码",
                        Title = "请求刷新筹码",
                        Description = $"{PlayerName(playerNames, playerId)}请求更换这一组三个候选筹码",
                        IconPath = pendingOffer?.SourceIconPath ?? EventIcon("relic")
                    });
                    continue;
                }
                pendingRelicOffers.Remove(playerId);

                report.Relics.Add(new RelicRecord
                {
                    FrameIndex = frame.Index,
                    Round = currentRound,
                    PlayerId = playerId,
                    PlayerName = PlayerName(playerNames, playerId),
                    HeroName = playerHeroes.GetValueOrDefault(playerId, "未知角色"),
                    Kind = "选择",
                    RelicId = relicId,
                    RelicName = relicName,
                    Quality = quality,
                    ImagePath = relicId > 0 ? _assets.Relic(relicId, _config.RelicAssets(relicId)) : "",
                    Level = pendingOffer?.Level ?? 0,
                    Source = pendingOffer?.Source ?? "来源未记录",
                    SourceIconPath = pendingOffer?.SourceIconPath ?? "",
                    RefreshNumber = refreshNumber,
                    IsRefresh = false
                });
                report.Events.Add(new TimelineEvent
                {
                    FrameIndex = frame.Index,
                    Round = currentRound,
                    PlayerId = playerId,
                    PlayerName = PlayerName(playerNames, playerId),
                    Type = "筹码",
                    Title = $"获得筹码 · {relicName}",
                    Description = $"{PlayerName(playerNames, playerId)}通过{pendingOffer?.Source ?? "未知途径"}获得了{relicName}，此前刷新 {refreshNumber} 次",
                    IconPath = EventIcon("relic")
                });

                if (!reroll && relicId > 0)
                {
                    if (!selectedByPlayer.TryGetValue(playerId, out var selected)) selectedByPlayer[playerId] = selected = [];
                    selected.Add(relicId);
                    qualityCounts[quality] = qualityCounts.GetValueOrDefault(quality) + 1;
                }
                continue;
            }

            if (frame.CmdId == 5250)
            {
                var purchase = _protocol.TryDecode(5250, frame.Payload);
                if (purchase is null) continue;
                var playerId = ReflectionValue.Long(purchase, "PlayerId");
                if (ReflectionValue.Int(purchase, "Select") > 0)
                {
                    confirmedRelicPurchases.Add(playerId);
                }
                else if (ReflectionValue.Bool(purchase, "Exit"))
                {
                    pendingRelicPurchaseCosts.Remove(playerId);
                    report.Events.Add(new TimelineEvent
                    {
                        FrameIndex = frame.Index,
                        Round = currentRound,
                        PlayerId = playerId,
                        PlayerName = PlayerName(playerNames, playerId),
                        Type = "筹码",
                        Title = "离开筹码地块",
                        Description = $"{PlayerName(playerNames, playerId)}没有购买筹码",
                        IconPath = EventIcon("relic")
                    });
                }
                continue;
            }

            if (frame.CmdId == 1115)
            {
                var message = _protocol.TryDecode(1115, frame.Payload);
                if (message is null) continue;
                var heroId = ReflectionValue.Int(message, "HeroId");
                var killerId = ReflectionValue.Long(message, "KillerId");
                report.Events.Add(new TimelineEvent
                {
                    FrameIndex = frame.Index,
                    Round = currentRound,
                    PlayerId = killerId,
                    PlayerName = PlayerName(playerNames, killerId),
                    Type = "击败",
                    Title = $"击败 {_config.Monster(heroId)}",
                    Description = $"目标 ID {heroId} · 击杀者 {PlayerName(playerNames, killerId)}",
                    IconPath = _assets.Monster(heroId, _config.MonsterAssets(heroId)) is { Length: > 0 } monsterImage ? monsterImage : EventIcon("monster")
                });
                continue;
            }

            if (frame.CmdId is 5030 or 5216)
            {
                var message = _protocol.TryDecode(frame.CmdId, frame.Payload);
                if (message is null) continue;
                var playerId = ReflectionValue.Long(message, "PlayerId");
                var buyIndices = ReflectionValue.Items(ReflectionValue.Get(message, "BuyCards"))
                    .Select(x => Convert.ToInt32(x)).ToArray();
                // PVE 回执带 IsClose；PVP 回执（5030）没有该字段，无购买即视为本次进店结束
                var isClose = frame.CmdId == 5216
                    ? ReflectionValue.Bool(message, "IsClose")
                    : buyIndices.Length == 0;

                if (pendingShopVisits.TryGetValue(playerId, out var visit))
                {
                    foreach (var index in buyIndices)
                    {
                        if (index >= 0 && !visit.BuyIndices.Contains(index)) visit.BuyIndices.Add(index);
                    }
                    if (isClose) visit.IsClosed = true;
                }
                else
                {
                    // 没有对应进店记录（如回执先于 Action 或数据不全）：仍记一条购买事件
                    if (buyIndices.Length > 0)
                    {
                        report.Events.Add(new TimelineEvent
                        {
                            FrameIndex = frame.Index,
                            Round = currentRound,
                            PlayerId = playerId,
                            PlayerName = PlayerName(playerNames, playerId),
                            Type = "商店",
                            Title = frame.CmdId == 5216 ? "PVE 商店购买" : "PVP 商店购买",
                            Description = $"购买了 {buyIndices.Length} 张卡牌（缺少候选上下文）",
                            IconPath = EventIcon("shop")
                        });
                    }
                }
                continue;
            }

            if (frame.CmdId == 5324)
            {
                var message = _protocol.TryDecode(5324, frame.Payload);
                if (message is null) continue;
                var playerId = ReflectionValue.Long(message, "PlayerId");
                var isBuy = ReflectionValue.Bool(message, "IsBuy");
                if (pendingShopVisits.TryGetValue(playerId, out var visit))
                {
                    visit.IsClosed = true;
                    if (isBuy && visit.BuyIndices.Count == 0 && visit.Cards.Length > 0) visit.BuyIndices.Add(0);
                }
                report.Events.Add(new TimelineEvent
                {
                    FrameIndex = frame.Index,
                    Round = currentRound,
                    PlayerId = playerId,
                    PlayerName = PlayerName(playerNames, playerId),
                    Type = "商店",
                    Title = isBuy ? "商人买卡" : "商人未买卡",
                    Description = $"{PlayerName(playerNames, playerId)}{(isBuy ? "从商人处购买了一张卡牌" : "没有从商人处买卡")}",
                    IconPath = EventIcon("shop")
                });
                continue;
            }

            if (frame.CmdId == 5214)
            {
                report.Events.Add(new TimelineEvent
                {
                    FrameIndex = frame.Index,
                    Round = currentRound,
                    PlayerId = currentPlayer,
                    PlayerName = PlayerName(playerNames, currentPlayer),
                    Type = "系统",
                    Title = "怪物追击结算",
                    Description = _protocol.MessageName(frame.CmdId),
                    IconPath = EventIcon("monster")
                });
            }
        }

        if (finishFrame is not null)
        {
            report.Events.Add(new TimelineEvent
            {
                FrameIndex = finishFrame.Index,
                Round = report.RoundCount,
                Type = "结算",
                Title = $"对局结束 · {report.ResultText}",
                Description = $"{report.WinnerText} · {report.AwardsText}",
                IconPath = EventIcon("finish")
            });
        }

        foreach (var player in players)
        {
            if (!selectedByPlayer.TryGetValue(player.Id, out var ids)) continue;
            player.SelectedRelicCount = ids.Count;
            player.SelectedRelicsText = string.Join("、", ids.Select(_config.Relic));
        }

        // 收尾：把未关闭的商店进店记录也落进报告（重复广播已合并）
        foreach (var visit in pendingShopVisits.Values)
        {
            report.Shops.Add(BuildShopRecord(visit, playerNames, playerHeroes));
        }

        FillStats(actionCounts, Math.Max(1, actionCounts.Values.Sum()), report.ActionStats);
        FillStats(qualityCounts, Math.Max(1, qualityCounts.Values.Sum()), report.RelicQualityStats);
        progress?.Report("回放解析完成");
        return report;
    }

    public string FormatFrame(ProtocolFrame frame) => _protocol.FormatFrame(frame);

    private static List<ProtocolFrame> ReadFrames(byte[] bytes)
    {
        var frames = new List<ProtocolFrame>();
        var offset = 0;
        while (offset < bytes.Length)
        {
            var frameOffset = offset;
            if (bytes.Length - offset < 6)
                throw new InvalidDataException($"帧头被截断：offset=0x{offset:X}，剩余 {bytes.Length - offset} 字节。");

            var cmdId = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(offset, 2));
            var payloadLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset + 2, 4));
            offset += 6;
            if (cmdId <= 0) throw new InvalidDataException($"无效命令号 {cmdId}：offset=0x{frameOffset:X}。");
            if (payloadLength < 0 || payloadLength > MaxPayloadSize)
                throw new InvalidDataException($"无效载荷长度 {payloadLength}：帧 {frames.Count}，offset=0x{frameOffset:X}。");
            if (payloadLength > bytes.Length - offset)
                throw new InvalidDataException($"帧载荷被截断：帧 {frames.Count}，需要 {payloadLength} 字节，只剩 {bytes.Length - offset} 字节。");

            var payload = bytes.AsSpan(offset, payloadLength).ToArray();
            frames.Add(new ProtocolFrame
            {
                Index = frames.Count,
                Offset = frameOffset,
                CmdId = cmdId,
                Payload = payload,
                MessageName = GameProtocolContext.MessageTypes.TryGetValue(cmdId, out var typeName)
                    ? typeName.Split('.').Last()
                    : $"CMD {cmdId}"
            });
            offset += payloadLength;
        }
        return frames;
    }

    private void FillMatchSummary(ReplayReport report, object initialRoom, object? finalRoom, object? finish,
        List<(ProtocolFrame Frame, object Value)> snapshots)
    {
        report.RoomId = ReflectionValue.Long(initialRoom, "Id");
        report.RoomServerId = ReflectionValue.Int(initialRoom, "RoomServerId");
        report.MapId = ReflectionValue.Int(initialRoom, "MapId");
        report.MapName = _config.Map(report.MapId);
        report.MapImagePath = _assets.Map(report.MapId, _config.MapAssets(report.MapId));
        report.MapType = ReflectionValue.Int(initialRoom, "MapType");
        report.Difficulty = ReflectionValue.Int(initialRoom, "Difficulty");
        report.PlayerDeaths = ReflectionValue.Int(finalRoom, "PlayerTotalDie");
        report.GameProgress = ReflectionValue.Int(finalRoom, "GameProgress");
        report.GameMaxProgress = ReflectionValue.Int(finalRoom, "GameMaxProgress");
        report.RoundCount = snapshots.Select(x => ReflectionValue.Int(ReflectionValue.Get(x.Value, "Room"), "Round")).DefaultIfEmpty(0).Max();
        report.TurnCount = Math.Max(0, snapshots.Count - 1);

        var start = ReflectionValue.Long(initialRoom, "StartTime");
        var end = ReflectionValue.Long(finish, "FinishTime");
        report.StartTimeText = FormatUnixTime(start);
        report.FinishTimeText = FormatUnixTime(end);
        if (start > 0 && end >= start) report.DurationText = TimeSpan.FromSeconds(end - start).ToString(@"hh\:mm\:ss");

        report.ReplayId = ReflectionValue.Text(finish, "ReplayId") is { Length: > 0 } replayId ? replayId : report.FileName;
        report.GameVersion = ReflectionValue.Text(finish, "Version") is { Length: > 0 } version ? version : "—";
        var winner = ReflectionValue.Long(finish, "Winer");
        report.WinnerText = winner > 0 ? $"获胜方 {winner}" : "未记录获胜方";
        report.ResultText = winner > 0 ? "胜利" : "已结束";

        var awards = ReflectionValue.Items(ReflectionValue.Get(finish, "Awards"))
            .Select(x => (Id: ReflectionValue.Int(x, "Key"), Count: ReflectionValue.Int(x, "Value")))
            .Where(x => x.Count != 0)
            .Select(x => $"{_config.Item(x.Id)} ×{x.Count}")
            .ToList();
        report.AwardsText = awards.Count > 0 ? string.Join(" · ", awards) : "未记录奖励";
    }

    private List<PlayerSummary> FillPlayers(ReplayReport report, object initialRoom, object? finalRoom)
    {
        var finalPlayers = ReflectionValue.Items(ReflectionValue.Get(finalRoom, "Players"))
            .ToDictionary(x => ReflectionValue.Long(x, "Id"));
        var players = new List<PlayerSummary>();
        foreach (var player in ReflectionValue.Items(ReflectionValue.Get(initialRoom, "Players")))
        {
            var id = ReflectionValue.Long(player, "Id");
            var initialHero = ReflectionValue.Get(player, "Hero");
            var heroId = ReflectionValue.Int(initialHero, "HeroId");
            finalPlayers.TryGetValue(id, out var finalPlayer);
            var finalHero = ReflectionValue.Get(finalPlayer, "Hero");
            var cond = ReflectionValue.Get(finalHero, "Cond");
            var finalIsBot = ReflectionValue.Bool(finalPlayer, "IsBot");
            var hp = ReflectionValue.Int(finalHero, "Hp");
            var maxHp = ReflectionValue.Int(finalHero, "MaxHp");

            var summary = new PlayerSummary
            {
                Id = id,
                Nickname = ReflectionValue.Text(player, "Nick") is { Length: > 0 } nick ? nick : id.ToString(),
                Slot = ReflectionValue.Int(player, "Slot"),
                AccountLevel = ReflectionValue.Int(player, "Level"),
                HeroId = heroId,
                HeroName = _config.Character(heroId),
                AvatarPath = _assets.Character(heroId, _config.CharacterAssets(heroId)),
                FinalStatus = finalHero is null
                    ? finalIsBot ? "机器人占位 · 无末局英雄数据" : "无末局英雄数据"
                    : hp > 0 ? "存活至结算" : "结算时生命值为 0",
                Hp = hp,
                MaxHp = maxHp,
                Gold = ReflectionValue.Int(finalHero, "Gold"),
                Attack = ReflectionValue.Int(finalHero, "Attack"),
                Defense = ReflectionValue.Int(finalHero, "Defense"),
                Damage = ReflectionValue.Int(cond, "TotalDamage"),
                Injured = ReflectionValue.Int(cond, "TotalInjured"),
                Kills = ReflectionValue.Int(cond, "KillPveMonster"),
                Healing = ReflectionValue.Int(cond, "TreatmentScore"),
                MovePoints = ReflectionValue.Int(cond, "MovePoint"),
                UsedCards = ReflectionValue.Int(cond, "UseCard"),
                UsedSkills = ReflectionValue.Int(cond, "UseSkill"),
                TransferGold = ReflectionValue.Int(cond, "TransferGold"),
                BoughtRelics = ReflectionValue.Int(finalHero, "BuyRelicNum"),
                FinalBossKill = ReflectionValue.Bool(cond, "FinalKillBoss")
            };
            report.Players.Add(summary);
            players.Add(summary);
        }
        return players;
    }

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

    private static string RelicSource(int level, int supLv, int purchaseCost)
    {
        if (purchaseCost > 0) return "筹码地块购买";
        if (supLv > 0) return "升星";
        return "任务";
    }

    private string RelicSourceIcon(string source)
    {
        if (source.Contains("购买") || source.Contains("商店") || source.Contains("地块"))
            return EventIcon("shop");
        if (source.Contains("升星"))
            return EventIcon("attribute");
        if (source.Contains("任务"))
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

    private string FindBossName(object? room)
    {
        foreach (var monster in ReflectionValue.Items(ReflectionValue.Get(room, "Monsters")))
        {
            var hero = ReflectionValue.Get(monster, "Hero");
            if (ReflectionValue.Text(hero, "MonsterType").Contains("BOSS", StringComparison.OrdinalIgnoreCase))
                return _config.Monster(ReflectionValue.Int(hero, "HeroId"));
        }
        return "—";
    }

    private static void FillStats(Dictionary<string, int> counts, int total, ICollection<StatItem> target)
    {
        var max = Math.Max(1, counts.Values.DefaultIfEmpty(0).Max());
        foreach (var pair in counts.OrderByDescending(x => x.Value).ThenBy(x => x.Key))
        {
            target.Add(new StatItem
            {
                Name = pair.Key,
                Count = pair.Value,
                Percent = (double)pair.Value / Math.Max(1, total),
                BarWidth = 130d * pair.Value / max
            });
        }
    }

    private static string PlayerName(IReadOnlyDictionary<long, string> players, long id)
        => id == 0 ? "系统" : players.GetValueOrDefault(id, $"实体 {id}");

    private static string FormatUnixTime(long seconds)
    {
        if (seconds <= 0) return "—";
        try { return DateTimeOffset.FromUnixTimeSeconds(seconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"); }
        catch { return seconds.ToString(); }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value:0.##} {units[index]}";
    }
}
