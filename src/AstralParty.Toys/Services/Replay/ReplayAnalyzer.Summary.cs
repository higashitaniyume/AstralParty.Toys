using System;
using System.Collections.Generic;
using System.Linq;

namespace AstralParty.Toys.Services.Replay;

public sealed partial class ReplayAnalyzer
{
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
        report.WinnerText = LocalizeWinner(winner);
        report.ResultText = LocalizeResult(winner);

        var awards = ReflectionValue.Items(ReflectionValue.Get(finish, "Awards"))
            .Select(x => (Id: ReflectionValue.Int(x, "Key"), Count: ReflectionValue.Int(x, "Value")))
            .Where(x => x.Count != 0)
            .Select(x => $"{_config.Item(x.Id)} ×{x.Count}")
            .ToList();
        report.AwardsText = awards.Count > 0 ? string.Join(" · ", awards) : LocalizeAwards("");
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
                FinalStatus = LocalizeFinalStatus(finalHero is null, finalIsBot, hp),
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

    private List<PlayerPerformance> FillPerformance(List<PlayerSummary> players, object initialRoom,
        List<(ProtocolFrame Frame, object Value)> snapshots)
    {
        var initialPlayers = ReflectionValue.Items(ReflectionValue.Get(initialRoom, "Players"))
            .ToDictionary(x => ReflectionValue.Long(x, "Id"));
        var lastSnapshotFrame = snapshots.Count > 0 ? snapshots[^1].Frame.Index : -1;
        var result = new List<PlayerPerformance>();
        foreach (var player in players)
        {
            initialPlayers.TryGetValue(player.Id, out var baseline);
            var initialHero = ReflectionValue.Get(baseline, "Hero");
            var initialCond = ReflectionValue.Get(initialHero, "Cond");

            // 快照记录的是回合边界上的累计值：回合结束后的快照归到刚结束的回合，
            // 最后一个快照保留当前回合，避免把决胜回合的数字算到下一回合。
            var rounds = new SortedDictionary<int, PerformanceTotals>();
            foreach (var (frame, value) in snapshots)
            {
                var room = ReflectionValue.Get(value, "Room");
                var snapshotPlayer = ReflectionValue.Items(ReflectionValue.Get(room, "Players"))
                    .FirstOrDefault(x => ReflectionValue.Long(x, "Id") == player.Id);
                var hero = ReflectionValue.Get(snapshotPlayer, "Hero");
                if (hero is null) continue;
                var cond = ReflectionValue.Get(hero, "Cond");
                int round = Math.Max(0, ReflectionValue.Int(room, "Round") - (frame.Index == lastSnapshotFrame ? 0 : 1));
                if (!rounds.ContainsKey(round) || frame.Index == lastSnapshotFrame)
                {
                    rounds[round] = new PerformanceTotals(
                        ReflectionValue.Int(cond, "TotalDamage"),
                        ReflectionValue.Int(cond, "TransferGold"),
                        ReflectionValue.Int(cond, "UseCard"),
                        ReflectionValue.Int(cond, "Gold"),
                        ReflectionValue.Int(hero, "Gold"),
                        ReflectionValue.Int(cond, "SelfDie"),
                        ReflectionValue.Int(cond, "KillPveMonster"));
                }
            }

            int damage = ReflectionValue.Int(initialCond, "TotalDamage");
            int transfer = ReflectionValue.Int(initialCond, "TransferGold");
            int cards = ReflectionValue.Int(initialCond, "UseCard");
            int earnedGold = ReflectionValue.Int(initialCond, "Gold");
            int currentGold = ReflectionValue.Int(initialHero, "Gold");
            int deaths = ReflectionValue.Int(initialCond, "SelfDie");
            int kills = ReflectionValue.Int(initialCond, "KillPveMonster");
            var points = new List<PerformancePoint>(rounds.Count);
            foreach (var (round, totals) in rounds)
            {
                points.Add(new PerformancePoint
                {
                    Round = round,
                    Damage = totals.Damage,
                    Transfer = totals.Transfer,
                    Cards = totals.Cards,
                    EarnedGold = totals.EarnedGold,
                    CurrentGold = totals.CurrentGold,
                    Deaths = totals.Deaths,
                    Kills = totals.Kills,
                    DamageDelta = totals.Damage - damage,
                    TransferDelta = totals.Transfer - transfer,
                    CardsDelta = totals.Cards - cards,
                    EarnedGoldDelta = totals.EarnedGold - earnedGold,
                    CurrentGoldDelta = totals.CurrentGold - currentGold,
                    DeathsDelta = totals.Deaths - deaths,
                    KillsDelta = totals.Kills - kills
                });
                damage = totals.Damage;
                transfer = totals.Transfer;
                cards = totals.Cards;
                earnedGold = totals.EarnedGold;
                currentGold = totals.CurrentGold;
                deaths = totals.Deaths;
                kills = totals.Kills;
            }
            result.Add(new PlayerPerformance { PlayerId = player.Id, Points = points });
        }
        return result;
    }

    private readonly record struct PerformanceTotals(int Damage, int Transfer, int Cards, int EarnedGold,
        int CurrentGold, int Deaths, int Kills);
}
