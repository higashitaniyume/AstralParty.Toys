using System.Collections.ObjectModel;

namespace AstralParty.Toys;

public sealed class ReplayReport
{
    public string FilePath { get; init; } = "";
    public string FileName { get; init; } = "";
    public string FileSizeText { get; init; } = "";
    public string Sha256 { get; init; } = "";
    public string ReplayId { get; set; } = "—";
    public string GameVersion { get; set; } = "—";
    public long RoomId { get; set; }
    public int RoomServerId { get; set; }
    public int MapId { get; set; }
    public string MapName { get; set; } = "未知地图";
    public string MapImagePath { get; set; } = "";
    public int MapType { get; set; }
    public int Difficulty { get; set; }
    public int RoundCount { get; set; }
    public int TurnCount { get; set; }
    public int FrameCount { get; set; }
    public int CommandTypeCount { get; set; }
    public int PlayerDeaths { get; set; }
    public int GameProgress { get; set; }
    public int GameMaxProgress { get; set; }
    public string StartTimeText { get; set; } = "—";
    public string FinishTimeText { get; set; } = "—";
    public string DurationText { get; set; } = "—";
    public string ResultText { get; set; } = "未知";
    public string WinnerText { get; set; } = "—";
    public string BossName { get; set; } = "—";
    public string AwardsText { get; set; } = "—";
    public int WarningCount { get; set; }
    public ObservableCollection<PlayerSummary> Players { get; } = [];
    public ObservableCollection<TimelineEvent> Events { get; } = [];
    public ObservableCollection<RelicRecord> Relics { get; } = [];
    public ObservableCollection<ShopRecord> Shops { get; } = [];
    /// <summary>按玩家、按回合边界快照归集的表现曲线；旧回放可能为空。</summary>
    public ObservableCollection<PlayerPerformance> Performance { get; } = [];
    /// <summary>本局在快照中观测到的怪物；按实体 ID 合并，死亡由 ReplayDieS2C 回填。</summary>
    public ObservableCollection<MonsterRecord> Monsters { get; } = [];
    /// <summary>玩家从其他节点踩中“刷怪地块”（LandType 19）的快照记录。</summary>
    public ObservableCollection<MonsterTileStep> MonsterTileSteps { get; } = [];
    public ObservableCollection<ProtocolFrame> Frames { get; } = [];
    public ObservableCollection<StatItem> CommandStats { get; } = [];
    public ObservableCollection<StatItem> ActionStats { get; } = [];
    public ObservableCollection<StatItem> RelicQualityStats { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];
    public Dictionary<string, string> UiAssets { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string ProgressText => GameMaxProgress > 0 ? $"{GameProgress} / {GameMaxProgress}" : "—";
    public string RoomText => RoomId == 0 ? "—" : $"{RoomId} · 服务器 {RoomServerId}";
    public string FormatText => $"{FrameCount:N0} 帧 · {CommandTypeCount} 种命令";
}

public sealed class PlayerSummary
{
    public long Id { get; init; }
    public string Nickname { get; init; } = "";
    public int Slot { get; init; }
    public int AccountLevel { get; init; }
    public int HeroId { get; init; }
    public string HeroName { get; init; } = "未知角色";
    public string AvatarPath { get; init; } = "";
    public string FinalStatus { get; set; } = "未知";
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Gold { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Damage { get; set; }
    public int Injured { get; set; }
    public int Kills { get; set; }
    public int Healing { get; set; }
    public int MovePoints { get; set; }
    public int UsedCards { get; set; }
    public int UsedSkills { get; set; }
    public int TransferGold { get; set; }
    public int BoughtRelics { get; set; }
    public bool FinalBossKill { get; set; }
    public int SelectedRelicCount { get; set; }
    public string SelectedRelicsText { get; set; } = "—";
    public RelicDisplay[] SelectedRelics { get; set; } = [];
    public string HpText => MaxHp > 0 ? $"{Hp} / {MaxHp}" : "—";
    public string CombatText => MaxHp > 0 ? $"{Attack} / {Defense}" : "—";
    public string IdentityText => $"ID {Id} · 槽位 {Slot + 1} · 账号等级 {AccountLevel}";
}

public sealed class PlayerPerformance
{
    public long PlayerId { get; init; }
    /// <summary>按回合升序；数值为快照时的累计值，Delta 为相对上一回合的增量。</summary>
    public List<PerformancePoint> Points { get; init; } = [];
    public List<DiceRoll> Dice { get; init; } = [];
}

public sealed class PerformancePoint
{
    public int Round { get; init; }
    public int Damage { get; init; }
    public int Transfer { get; init; }
    public int Cards { get; init; }
    public int EarnedGold { get; init; }
    public int CurrentGold { get; init; }
    public int Deaths { get; init; }
    public int Kills { get; init; }
    public int DamageDelta { get; init; }
    public int TransferDelta { get; init; }
    public int CardsDelta { get; init; }
    public int EarnedGoldDelta { get; init; }
    public int CurrentGoldDelta { get; init; }
    public int DeathsDelta { get; init; }
    public int KillsDelta { get; init; }
}

public sealed class DiceRoll
{
    public int Round { get; init; }
    public int[] Values { get; init; } = [];
    public int MovePoint { get; init; }
    public bool Controlled { get; init; }
}

/// <summary>一只在回放中出现过的怪物。出生信息来自首次出现的回合快照，死亡信息来自 ReplayDieS2C。</summary>
public sealed class MonsterRecord
{
    public long Id { get; init; }
    public int HeroId { get; init; }
    public string MonsterName { get; init; } = "未知怪物";
    public string ImagePath { get; init; } = "";
    public int MonsterIndex { get; set; }
    public int SpawnFrameIndex { get; init; }
    public int SpawnRound { get; init; }
    public int SpawnNodeId { get; init; }
    public int SpawnLandType { get; init; }
    public bool SpawnedOnMonsterTile => SpawnLandType == 19;
    /// <summary>是否可归因到一次“踩中刷怪地块”的快照（同帧出现的新怪物）。</summary>
    public bool SpawnedByMonsterTile { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int LastFrameIndex { get; set; }
    public int LastRound { get; set; }
    public int LastNodeId { get; set; }
    public int? DeathFrameIndex { get; set; }
    public int? DeathRound { get; set; }
    public long KillerId { get; set; }
    public string KillerName { get; set; } = "";
    public string Status { get; set; } = "存活至结算";
    /// <summary>稳定状态键，供界面选择样式；不受语言影响。</summary>
    public string StatusKey { get; set; } = "alive";
}

/// <summary>一次踩中刷怪地块的快照。连续停留在同一格不会重复计数，离开后再次踩中才计下一次。</summary>
public sealed class MonsterTileStep
{
    public int FrameIndex { get; init; }
    public int Round { get; init; }
    public long PlayerId { get; init; }
    public string PlayerName { get; init; } = "";
    public int NodeId { get; init; }
    /// <summary>同一快照里新出现、可归因到这次踩格的怪物实体。</summary>
    public List<long> MonsterIds { get; } = [];
    public int MonsterCount => MonsterIds.Count;
}

public sealed class TimelineEvent
{
    public int FrameIndex { get; init; }
    public int Round { get; init; }
    public long PlayerId { get; init; }
    public string PlayerName { get; init; } = "系统";
    public string Type { get; init; } = "协议";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string IconPath { get; init; } = "";
    public string RoundText => Round > 0 ? $"第 {Round} 回合" : "对局";
}

public sealed class RelicDisplay
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Quality { get; init; } = "";
    public string ImagePath { get; init; } = "";
}

public sealed class RelicRecord
{
    public int FrameIndex { get; init; }
    public int Round { get; init; }
    public long PlayerId { get; init; }
    public string PlayerName { get; init; } = "未知玩家";
    public string HeroName { get; init; } = "未知角色";
    public string Kind { get; init; } = "";
    public int Level { get; init; }
    public int RelicId { get; init; }
    public string RelicName { get; init; } = "";
    public string Quality { get; init; } = "";
    public string OptionsText { get; init; } = "";
    public RelicDisplay[] Options { get; init; } = [];
    public string ImagePath { get; init; } = "";
    public string Source { get; init; } = "来源未记录";
    public string SourceIconPath { get; init; } = "";
    public int RefreshNumber { get; init; }
    public bool IsRefresh { get; init; }
    public string LevelText => Level > 0 ? $"第 {Level} 档" : "—";
    public string RefreshText => IsRefresh ? $"第 {Math.Max(1, RefreshNumber)} 次刷新" : RefreshNumber > 0 ? $"刷新 {RefreshNumber} 次后选择" : "未刷新";
}

/// <summary>
/// 一次商店进店记录：候选卡牌（<see cref="Cards"/>）+ 该次购买结果（<see cref="BoughtText"/>）。
/// 同一候选组会被服务器重复广播，按 Action.Sn 与候选内容去重后合并为一条。
/// </summary>
public sealed class ShopRecord
{
    public int FrameIndex { get; init; }
    public int Round { get; init; }
    public long PlayerId { get; init; }
    public string PlayerName { get; init; } = "未知玩家";
    public string HeroName { get; init; } = "未知角色";
    /// <summary>PVE商店 / PVP商店</summary>
    public string ShopType { get; init; } = "商店";
    public int[] Cards { get; init; } = [];
    public string OptionsText { get; init; } = "";
    public int Price { get; init; }
    public int Discount { get; init; }
    public int FreeCard { get; init; }
    public int FreeCardNum { get; init; }
    /// <summary>逐槽位售出标记：某张卡被买走后对应位置为 true（服务器 Alreadys）。</summary>
    public bool[] Alreadys { get; init; } = [];
    public int[] BuyIndices { get; init; } = [];
    public string BoughtText { get; init; } = "";
    public bool IsClosed { get; init; }
    public bool HasPurchase => BuyIndices.Length > 0;
    public string PriceText => Discount > 0 ? $"{Price}（折后 {Math.Max(0, Price - Discount)}）" : Price > 0 ? $"{Price}" : "—";
    public string SoldOutText => Alreadys.Length > 0 && Alreadys.Any(x => x)
        ? string.Join("、", Alreadys.Select((sold, index) => sold ? $"第{index + 1}格已售出" : "").Where(s => s.Length > 0))
        : "";
    public string PurchaseText => HasPurchase ? BoughtText : IsClosed ? "未购买，关闭商店" : "—";
}

public sealed class ProtocolFrame
{
    public int Index { get; init; }
    public int Offset { get; init; }
    public int CmdId { get; init; }
    public int PayloadLength => Payload.Length;
    public byte[] Payload { get; init; } = [];
    public string MessageName { get; init; } = "未知消息";
    public string OffsetText => $"0x{Offset:X8}";
}

public sealed class StatItem
{
    public string Name { get; init; } = "";
    public int Count { get; init; }
    public double Percent { get; init; }
    public double BarWidth { get; init; }
    public string Detail => $"{Count:N0} · {Percent:P1}";
}



