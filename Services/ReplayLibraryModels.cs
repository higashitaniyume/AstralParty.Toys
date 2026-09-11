namespace AstralParty.Toys.Services;

/// <summary>回放库设置，持久化在 %AppData%\AstralParty.Toys\replay-library.json。</summary>
public sealed class ReplayLibrarySettings
{
    /// <summary>回放库根目录。为空时使用默认的「文档\AstralPartyReplays」。</summary>
    public string? LibraryRoot { get; set; }

    /// <summary>开启后每次刷新列表都会自动把游戏目录里超出保留名额的旧回放归档进库。</summary>
    public bool AutoMaintain { get; set; }

    /// <summary>游戏目录里保留的最新回放数量。游戏自身的上限是 10，这里不会超过它。</summary>
    public int KeepInGame { get; set; } = ReplayLibraryService.GameSlotCapacity;
}

/// <summary>正在被删除的回放位于哪一侧。</summary>
public enum ReplayDeleteTarget
{
    Game,
    Library,
    Both
}

public sealed class ReplayPlayerBrief
{
    public long Id { get; set; }
    public string Nick { get; set; } = "";
    public int HeroId { get; set; }
    public string HeroName { get; set; } = "未知角色";
}

/// <summary>单个回放文件的元数据，同时作为库内 <c>_index/&lt;id&gt;.json</c> 的存档格式。</summary>
public sealed class ReplayMeta
{
    public int SchemaVersion { get; set; } = 1;
    public string ReplayId { get; set; } = "";
    /// <summary>回放文件自己记录的 ReplayId（可能与所在目录名不同）。</summary>
    public string FileReplayId { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public long StartTime { get; set; }
    public long FinishTime { get; set; }
    public int DurationSeconds { get; set; }
    public int MapId { get; set; }
    public string MapName { get; set; } = "未知地图";
    public int MapType { get; set; }
    public int Difficulty { get; set; }
    public int RoundCount { get; set; }
    public int FrameCount { get; set; }
    public long WinnerId { get; set; }
    public string WinnerName { get; set; } = "";
    public List<ReplayPlayerBrief> Players { get; set; } = [];
    public long SizeBytes { get; set; }
    public long ModifiedUtcTicks { get; set; }
    public string Sha256 { get; set; } = "";
    /// <summary>能否被游戏自己的结算解析器读出来。false 时游戏列到它会把整个目录删掉。</summary>
    public bool Healthy { get; set; }
    public string? Error { get; set; }
    public DateTime CapturedUtc { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "scan";
}

/// <summary>游戏目录与回放库合并后的单条回放视图。</summary>
public sealed class ReplayEntry
{
    public string ReplayId { get; set; } = "";
    public bool InGame { get; set; }
    public bool InLibrary { get; set; }
    public string? GamePath { get; set; }
    public string? LibraryPath { get; set; }
    public long SizeBytes { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public ReplayMeta? Meta { get; set; }

    /// <summary>用于排序：优先对局结束时间，其次文件修改时间。</summary>
    public long SortTime => Meta is { FinishTime: > 0 } meta ? meta.FinishTime : new DateTimeOffset(ModifiedUtc).ToUnixTimeSeconds();
}

public sealed class LibrarySnapshot
{
    public string GameDirectory { get; set; } = "";
    public bool GameDirectoryAvailable { get; set; }
    public int GameSlotCapacity { get; set; } = ReplayLibraryService.GameSlotCapacity;
    public int GameCount { get; set; }
    public int KeepInGame { get; set; } = ReplayLibraryService.GameSlotCapacity;
    public bool AutoMaintain { get; set; }
    public bool GameFull => GameCount >= GameSlotCapacity;
    public bool GameOverKeep => GameCount > KeepInGame;
    public bool GameRunning { get; set; }

    public string LibraryRoot { get; set; } = "";
    public bool LibraryAvailable { get; set; }
    public int LibraryCount { get; set; }
    public long LibraryBytes { get; set; }

    /// <summary>库目录与游戏目录被配置成同一个路径（会互相破坏，必须拦住）。</summary>
    public bool SameFolder { get; set; }

    public List<ReplayEntry> Entries { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}

/// <summary>归档 / 放回 / 整理 / 删除的统一结果。</summary>
public sealed class ReplayOperationResult
{
    public bool Ok { get; set; } = true;
    public int Applied { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Messages { get; set; } = [];

    /// <summary>存在需要玩家二次确认才能继续的操作（例如放回解析不出结算帧的回放）。</summary>
    public bool NeedsConfirmation { get; set; }

    /// <summary>等待确认的 ReplayId。</summary>
    public List<string> PendingIds { get; set; } = [];

    public static ReplayOperationResult Fail(string message)
    {
        var result = new ReplayOperationResult { Ok = false, Failed = 1 };
        result.Messages.Add(message);
        return result;
    }
}
