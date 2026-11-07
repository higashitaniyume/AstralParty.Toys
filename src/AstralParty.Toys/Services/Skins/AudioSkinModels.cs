using System;
using System.Collections.Generic;

namespace AstralParty.Toys.Services.Skins;

public sealed class AudioSkinProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public string DirectoryPath { get; set; } = "";
    public int RuleCount { get; set; }
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public DateTime LastModified { get; set; }
}

public sealed class AudioSkinCatalogItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Label { get; set; } = "";
    public long WwiseId { get; set; }
    public string Action { get; set; } = "none"; // none | replace | mute | redirect
    public string CustomFile { get; set; } = "";
    public float Volume { get; set; } = 1.0f;
    public int RedirectId { get; set; }
    public bool HasOriginal { get; set; }
    public string PreviewUrl { get; set; } = "";
    public string OriginalPreviewUrl { get; set; } = "";
    public string? RelOgg { get; set; }
    public int? CharacterId { get; set; }
    public string? CharacterName { get; set; }
    public string? SkinId { get; set; }
    public string? SkinName { get; set; }
    public string? Slot { get; set; }
    public string? SlotName { get; set; }
}

public sealed class AudioSkinRule
{
    public int ConfigId { get; set; }
    public string EventName { get; set; } = "";
    public string Action { get; set; } = "replace";
    public string File { get; set; } = "";
    public float Volume { get; set; } = 1.0f;
    public int RedirectId { get; set; }
}

public sealed class AudioSkinScheme
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public List<AudioSkinRule> Rules { get; set; } = new();
}
