using System.Collections.ObjectModel;

namespace AstralParty.ReplayTool;

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
    public string HpText => MaxHp > 0 ? $"{Hp} / {MaxHp}" : "—";
    public string CombatText => MaxHp > 0 ? $"{Attack} / {Defense}" : "—";
    public string IdentityText => $"ID {Id} · 槽位 {Slot + 1} · 账号等级 {AccountLevel}";
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

public sealed class RelicRecord
{
    public int FrameIndex { get; init; }
    public int Round { get; init; }
    public long PlayerId { get; init; }
    public string PlayerName { get; init; } = "未知玩家";
    public string Kind { get; init; } = "";
    public int Level { get; init; }
    public int RelicId { get; init; }
    public string RelicName { get; init; } = "";
    public string Quality { get; init; } = "";
    public string OptionsText { get; init; } = "";
    public string ImagePath { get; init; } = "";
    public string Source { get; init; } = "来源未记录";
    public string SourceIconPath { get; init; } = "";
    public int RefreshNumber { get; init; }
    public bool IsRefresh { get; init; }
    public string LevelText => Level > 0 ? $"第 {Level} 档" : "—";
    public string RefreshText => IsRefresh ? $"第 {Math.Max(1, RefreshNumber)} 次刷新" : RefreshNumber > 0 ? $"刷新 {RefreshNumber} 次后选择" : "未刷新";
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
