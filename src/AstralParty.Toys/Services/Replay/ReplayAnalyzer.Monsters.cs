using System;
using System.Collections.Generic;
using System.Linq;

namespace AstralParty.Toys.Services.Replay;

public sealed partial class ReplayAnalyzer
{
    /// <summary>刷怪地块的 LandType；游戏枚举 LandType.Monster = 19。</summary>
    private const int MonsterTileLandType = 19;

    private static void ReadLandTypes(object? room, IDictionary<int, int> target)
    {
        foreach (var land in ReflectionValue.Items(ReflectionValue.Get(room, "Lands")))
        {
            var nodeId = ReflectionValue.Int(land, "Key");
            target[nodeId] = ReflectionValue.Int(ReflectionValue.Get(land, "Value"), "LandType");
        }
    }

    private static void SeedPlayerNodes(object? room, IDictionary<long, int> target)
    {
        foreach (var player in ReflectionValue.Items(ReflectionValue.Get(room, "Players")))
        {
            var id = ReflectionValue.Long(player, "Id");
            var nodeId = ReflectionValue.Int(ReflectionValue.Get(player, "Hero"), "NodeId");
            if (id != 0) target[id] = nodeId;
        }
    }

    private List<MonsterTileStep> TrackMonsterTileSteps(object? room, int frameIndex, int round, IReadOnlyDictionary<int, int> landTypes,
        IDictionary<long, int> lastPlayerNodes, IReadOnlyDictionary<long, string> playerNames, ReplayReport report)
    {
        var steps = new List<MonsterTileStep>();
        foreach (var player in ReflectionValue.Items(ReflectionValue.Get(room, "Players")))
        {
            var id = ReflectionValue.Long(player, "Id");
            var nodeId = ReflectionValue.Int(ReflectionValue.Get(player, "Hero"), "NodeId");
            if (id == 0) continue;

            // 只看回合快照落点：停在刷怪地块才计一次；经过但没停下的路径不在快照里，不会被计入；连续停在同一格也不重复计数。
            if (lastPlayerNodes.TryGetValue(id, out var previousNode) && previousNode != nodeId &&
                landTypes.GetValueOrDefault(nodeId) == MonsterTileLandType)
            {
                var step = new MonsterTileStep
                {
                    FrameIndex = frameIndex,
                    Round = round,
                    PlayerId = id,
                    PlayerName = PlayerName(playerNames, id),
                    NodeId = nodeId
                };
                steps.Add(step);
            }
            lastPlayerNodes[id] = nodeId;
        }
        return steps;
    }

    private List<MonsterRecord> TrackMonsters(object? room, int frameIndex, int round, IReadOnlyDictionary<int, int> landTypes,
        IDictionary<long, MonsterRecord> monsters, ReplayReport report)
    {
        var spawned = new List<MonsterRecord>();
        foreach (var monster in ReflectionValue.Items(ReflectionValue.Get(room, "Monsters")))
        {
            var id = ReflectionValue.Long(monster, "Id");
            if (id == 0) continue;
            var hero = ReflectionValue.Get(monster, "Hero");
            var heroId = ReflectionValue.Int(hero, "HeroId");
            var nodeId = ReflectionValue.Int(hero, "NodeId");
            var hp = ReflectionValue.Int(hero, "Hp");
            var maxHp = ReflectionValue.Int(hero, "MaxHp");
            var attack = ReflectionValue.Int(hero, "Attack");
            var defense = ReflectionValue.Int(hero, "Defense");
            var index = ReflectionValue.Int(hero, "MonsterIndex");

            if (!monsters.TryGetValue(id, out var record))
            {
                record = new MonsterRecord
                {
                    Id = id,
                    HeroId = heroId,
                    MonsterName = _config.Monster(heroId),
                    ImagePath = _assets.Monster(heroId, _config.MonsterAssets(heroId)),
                    SpawnFrameIndex = frameIndex,
                    SpawnRound = round,
                    SpawnNodeId = nodeId,
                    SpawnLandType = landTypes.GetValueOrDefault(nodeId)
                };
                monsters[id] = record;
                report.Monsters.Add(record);
                spawned.Add(record);
            }

            record.MonsterIndex = index;
            record.Hp = hp;
            record.MaxHp = maxHp;
            record.Attack = attack;
            record.Defense = defense;
            record.LastFrameIndex = frameIndex;
            record.LastRound = round;
            record.LastNodeId = nodeId;
        }
        return spawned;
    }

    private static void AttributeTileSpawns(IReadOnlyList<MonsterTileStep> steps, IReadOnlyList<MonsterRecord> spawned,
        long currentPlayer, ReplayReport report)
    {
        if (steps.Count == 0 || spawned.Count == 0) return;
        var target = steps.FirstOrDefault(step => step.PlayerId == currentPlayer) ?? steps[0];
        foreach (var monster in spawned)
        {
            target.MonsterIds.Add(monster.Id);
            monster.SpawnedByMonsterTile = true;
        }
        // 只有真的刷出怪物的踩中才进入报告；踩中但没刷出的落点直接丢弃。
        report.MonsterTileSteps.Add(target);
    }

    private void FinalizeMonsters(ReplayReport report, int finalFrameIndex)
    {
        foreach (var record in report.Monsters)
        {
            if (record.DeathFrameIndex is not null)
            {
                record.StatusKey = "defeated";
                record.Status = MonsterStatus("defeated");
            }
            else if (record.SpawnFrameIndex < 0)
            {
                record.StatusKey = "defeated";
                record.Status = MonsterStatus("deathOnly");
                record.DeathFrameIndex = finalFrameIndex >= 0 ? finalFrameIndex : null;
            }
            else if (finalFrameIndex < 0)
            {
                record.StatusKey = "left";
                record.Status = MonsterStatus("left");
            }
            else if (record.LastFrameIndex == finalFrameIndex)
            {
                record.StatusKey = "alive";
                record.Status = MonsterStatus("alive");
            }
            else
            {
                record.StatusKey = "left";
                record.Status = MonsterStatus("left");
            }
        }

        // 死亡记录可能先于怪物快照出现；按首次观测/死亡帧排序，保持时间线稳定。
        var ordered = report.Monsters
            .OrderBy(record => record.SpawnFrameIndex < 0 ? record.DeathFrameIndex ?? int.MaxValue : record.SpawnFrameIndex)
            .ThenBy(record => record.Id)
            .ToArray();
        report.Monsters.Clear();
        foreach (var record in ordered) report.Monsters.Add(record);
    }

    private string MonsterStatus(string key) => key switch
    {
        "defeated" => _config.Language switch
        {
            AppLanguage.English => "Defeated",
            AppLanguage.Japanese => "撃破",
            AppLanguage.TraditionalChinese => "被擊敗",
            _ => "被击败"
        },
        "deathOnly" => _config.Language switch
        {
            AppLanguage.English => "Defeated (spawn not captured)",
            AppLanguage.Japanese => "撃破（出現未記録）",
            AppLanguage.TraditionalChinese => "被擊敗（未捕獲出生）",
            _ => "被击败（未捕获出生）"
        },
        "left" => _config.Language switch
        {
            AppLanguage.English => "Left (defeat not recorded)",
            AppLanguage.Japanese => "離脱（撃破未記録）",
            AppLanguage.TraditionalChinese => "離場（未記錄擊敗）",
            _ => "离场（未记录击败）"
        },
        _ => _config.Language switch
        {
            AppLanguage.English => "Survived to settlement",
            AppLanguage.Japanese => "決着まで生存",
            AppLanguage.TraditionalChinese => "存活至結算",
            _ => "存活至结算"
        }
    };

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
