namespace AstralParty.Toys.Services.Game;

/// <summary>一个已保存的游戏位置（国服 / 国际服 / TapTap 端…）。</summary>
public sealed class GameProfile
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Directory { get; set; } = "";
    /// <summary>cn | global | taptap | custom。</summary>
    public string Edition { get; set; } = "custom";
}

/// <summary>持久化到 game-profiles.json 的整体数据。</summary>
public sealed class GameLibraryData
{
    public List<GameProfile> Profiles { get; set; } = new();
    public string? ActiveId { get; set; }
}

/// <summary>发给前端的档案视图（附带实时的存在性 / Unity 结构校验）。</summary>
public sealed class GameProfileView
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Directory { get; set; } = "";
    public string Edition { get; set; } = "custom";
    public string EditionLabel { get; set; } = "";
    public bool Active { get; set; }
    public bool DirectoryExists { get; set; }
    public bool Valid { get; set; }
    public string? ExeName { get; set; }
}

/// <summary>系统搜索命中的一个候选目录。</summary>
public sealed class GameScanCandidate
{
    public string Directory { get; set; } = "";
    public string ExeName { get; set; } = "";
    public string Edition { get; set; } = "custom";
    public string EditionLabel { get; set; } = "";
    public bool Valid { get; set; }
    public bool AlreadyAdded { get; set; }
    /// <summary>registry | steam | common | deep。</summary>
    public string Source { get; set; } = "";
}
