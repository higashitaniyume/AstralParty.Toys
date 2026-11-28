using System.Text.Json;

namespace AstralParty.Toys.Services.Modding;

public sealed class WorkshopResourceTypeItem
{
    public string Type { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public long MaxPackageBytes { get; set; }
    public int MaxFiles { get; set; }
    public string ToysAction { get; set; } = "";
    public List<string> AcceptedExtensions { get; set; } = [];
    public List<string> ManifestHints { get; set; } = [];
}

public sealed class WorkshopResourcePreview
{
    public string Sha256 { get; set; } = "";
    public string Url { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public string Caption { get; set; } = "";
    public string SourcePath { get; set; } = "";
}

public sealed class WorkshopResourceCredit
{
    public string Role { get; set; } = "";
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string Note { get; set; } = "";
}

public sealed class WorkshopResourceDependency
{
    public string Id { get; set; } = "";
    public string MinVersion { get; set; } = "";
}

public sealed class WorkshopResourceSummary
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Author { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string ContentRating { get; set; } = "general";
    public Dictionary<string, JsonElement> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? CoverUrl { get; set; }
    public int PreviewCount { get; set; }
    public string LatestVersion { get; set; } = "";
    public string GameVersion { get; set; } = "";
    public List<string> Platforms { get; set; } = [];
    public long SizeBytes { get; set; }
    public int FileCount { get; set; }
    public bool Hosted { get; set; }
    public string Sha256 { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public long Downloads { get; set; }
    public long Downloads7Days { get; set; }
    public long Downloads30Days { get; set; }
    public bool Featured { get; set; }
    public string Origin { get; set; } = "community";
}

public sealed class WorkshopResourceListResponse
{
    public int ApiVersion { get; set; }
    public string GeneratedAt { get; set; } = "";
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<WorkshopResourceTypeItem> Types { get; set; } = [];
    public List<WorkshopResourceSummary> Items { get; set; } = [];
}

public sealed class WorkshopResourceVersion
{
    public string Version { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ManifestUrl { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public List<string> MirrorUrls { get; set; } = [];
    public bool Hosted { get; set; }
    public string StorageProvider { get; set; } = "local";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public long UnpackedBytes { get; set; }
    public int FileCount { get; set; }
    public string GameVersion { get; set; } = "";
    public List<string> Platforms { get; set; } = [];
    public List<WorkshopResourceDependency> ModDependencies { get; set; } = [];
    public List<WorkshopResourceDependency> ResourceDependencies { get; set; } = [];
    public Dictionary<string, JsonElement> Meta { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string Changelog { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public string VerifiedAt { get; set; } = "";
    public long Downloads { get; set; }
}

public sealed class WorkshopResourceDetail
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string TypeName { get; set; } = "";
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string ContentRating { get; set; } = "general";
    public Dictionary<string, JsonElement> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string RepositoryUrl { get; set; } = "";
    public string License { get; set; } = "";
    public string LicenseText { get; set; } = "";
    public string Readme { get; set; } = "";
    public List<WorkshopResourceCredit> Credits { get; set; } = [];
    public string? CoverUrl { get; set; }
    public List<WorkshopResourcePreview> Previews { get; set; } = [];
    public bool Featured { get; set; }
    public string Origin { get; set; } = "community";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public long Downloads { get; set; }
    public List<WorkshopResourceVersion> Versions { get; set; } = [];
}
