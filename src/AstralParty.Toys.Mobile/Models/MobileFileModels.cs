using System.Text.Json.Serialization;

namespace AstralParty.Toys.Mobile.Models;

public sealed class MobileFileEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("isDir")]
    public bool IsDir { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("modified")]
    public string Modified { get; set; } = "";
}

public sealed class MobileFileListResult
{
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";

    [JsonPropertyName("rootLabel")]
    public string RootLabel { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("entries")]
    public List<MobileFileEntry> Entries { get; set; } = [];

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public sealed class MobileFilePreviewResult
{
    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = "";

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("binary")]
    public bool Binary { get; set; }

    [JsonPropertyName("truncated")]
    public bool Truncated { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
