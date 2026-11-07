using System;

namespace AstralParty.Toys.Services.Skins;

public sealed class SkinProfile
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string DirectoryPath { get; set; } = "";
    public int CardCount { get; set; }
    public int TotalPoolCount { get; set; }
    public double CoverageRate { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime LastModified { get; set; }
}

public sealed class SkinCardInfo
{
    public string FileName { get; set; } = "";
    public string AssetKey { get; set; } = "";
    public int CardId { get; set; }
    public string CardName { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsSfw { get; set; }
    public bool IsCustomized { get; set; }
    public bool CanRestore { get; set; }
    public long FileSizeBytes { get; set; }
    public string PreviewUrl { get; set; } = "";
    public string FallbackUrl { get; set; } = "";
    public bool IsAnimated { get; set; }
    public string AnimationBinding { get; set; } = "";
    public int AnimationFps { get; set; }
    public int AnimationFrameCount { get; set; }
    public string AnimationFrameUrlPrefix { get; set; } = "";
    public DateTime LastModified { get; set; }
}
