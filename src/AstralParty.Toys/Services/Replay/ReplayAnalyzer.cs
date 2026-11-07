using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AstralParty.Toys.Services.Replay;

public sealed partial class ReplayAnalyzer
{
    private const int MaxFileSize = 512 * 1024 * 1024;
    private const int MaxPayloadSize = 64 * 1024 * 1024;

    private readonly GameProtocolContext _protocol;
    private readonly ConfigCatalog _config;
    private readonly AssetLocator _assets;

    public ConfigCatalog Config => _config;


    public ReplayAnalyzer(
        string appDirectory,
        string? assetDirectory = null,
        IEnumerable<string>? materialDirectories = null,
        bool useEmbeddedAssets = true,
        string language = AppLanguage.SimplifiedChinese)
    {
        _protocol = new GameProtocolContext(Path.Combine(appDirectory, "Protocol"));
        _config = new ConfigCatalog(_protocol, Path.Combine(appDirectory, "GameData"), language);
        _assets = new AssetLocator(
            assetDirectory ?? Path.Combine(appDirectory, "Assets"),
            materialDirectories ?? MaterialSource.DiscoverAll(appDirectory),
            useEmbeddedAssets);
    }

    public void Reload(string language)
    {
        _config.Reload(language);
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
        foreach (var performance in FillPerformance(players, initialRoom, snapshots)) report.Performance.Add(performance);
        var performanceByPlayer = report.Performance.ToDictionary(x => x.PlayerId);
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
        var initialRound = Math.Max(1, ReflectionValue.Int(initialRoom, "Round"));
        var landTypes = new Dictionary<int, int>();
        var lastPlayerNodes = new Dictionary<long, int>();
        var monsters = new Dictionary<long, MonsterRecord>();
        ReadLandTypes(initialRoom, landTypes);
        SeedPlayerNodes(initialRoom, lastPlayerNodes);
        TrackMonsters(initialRoom, runningFrame.Index, initialRound, landTypes, monsters, report);

        foreach (var frame in frames)
        {
            if (frame.CmdId == 5022)
            {
                var roll = _protocol.TryDecode(5022, frame.Payload);
                if (roll is null) continue;
                var playerId = ReflectionValue.Long(roll, "PlayerId");
                if (performanceByPlayer.TryGetValue(playerId, out var performance))
                {
                    performance.Dice.Add(new DiceRoll
                    {
                        Round = currentRound,
                        Values = ReflectionValue.Items(ReflectionValue.Get(roll, "Vals"))
                            .Select(x => Convert.ToInt32(x)).ToArray(),
                        MovePoint = ReflectionValue.Int(roll, "MovePoint"),
                        Controlled = ReflectionValue.Bool(roll, "IsControlMovePoint")
                    });
                }
                continue;
            }

            if (frame.CmdId == 1113)
            {
                var snapshot = _protocol.TryDecode(1113, frame.Payload);
                if (snapshot is null) continue;
                currentPlayer = ReflectionValue.Long(snapshot, "PlayerId");
                var room = ReflectionValue.Get(snapshot, "Room");
                currentRound = ReflectionValue.Int(room, "Round");
                ReadLandTypes(room, landTypes);
                var steps = TrackMonsterTileSteps(room, frame.Index, currentRound, landTypes, lastPlayerNodes, playerNames, report);
                var spawnedMonsters = TrackMonsters(room, frame.Index, currentRound, landTypes, monsters, report);
                AttributeTileSpawns(steps, spawnedMonsters, currentPlayer, report);
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
                var dyingId = ReflectionValue.Long(message, "PlayerId");
                if (!playerNames.ContainsKey(dyingId))
                {
                    if (monsters.TryGetValue(dyingId, out var defeated))
                    {
                        defeated.DeathFrameIndex = frame.Index;
                        defeated.DeathRound = currentRound;
                        defeated.KillerId = killerId;
                        defeated.KillerName = PlayerName(playerNames, killerId);
                    }
                    else
                    {
                        var deathOnly = new MonsterRecord
                        {
                            Id = dyingId,
                            HeroId = heroId,
                            MonsterName = _config.Monster(heroId),
                            ImagePath = _assets.Monster(heroId, _config.MonsterAssets(heroId)),
                            SpawnFrameIndex = -1,
                            SpawnRound = 0,
                            SpawnNodeId = 0,
                            SpawnLandType = 0,
                            DeathFrameIndex = frame.Index,
                            DeathRound = currentRound,
                            KillerId = killerId,
                            KillerName = PlayerName(playerNames, killerId)
                        };
                        monsters[dyingId] = deathOnly;
                        report.Monsters.Add(deathOnly);
                    }
                }
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

        FinalizeMonsters(report, snapshots.Count > 0 ? snapshots[^1].Frame.Index : -1);

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
            player.SelectedRelics = ids.Select(BuildRelicDisplay).ToArray();
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

}
