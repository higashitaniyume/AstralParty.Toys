namespace AstralParty.Toys.Services;

/// <summary>创意工坊公共接口的返回模型（与 ForgeHiynet 的 <c>/api/mods</c> 对应）。</summary>
public sealed class WorkshopListResponse
{
    public int ApiVersion { get; set; }
    public string GeneratedAt { get; set; } = "";
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<string> Tags { get; set; } = new();
    public List<WorkshopModSummary> Items { get; set; } = new();
}

public sealed class WorkshopModSummary
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Author { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string? CoverUrl { get; set; }
    public string LatestVersion { get; set; } = "";
    public string SdkVersion { get; set; } = "";
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public long Downloads { get; set; }
    public long Downloads7Days { get; set; }
    public long Downloads30Days { get; set; }
    public bool Featured { get; set; }
}

public sealed class WorkshopModDetail
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public string? CoverUrl { get; set; }
    public bool Featured { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string LatestVersion { get; set; } = "";
    public long Downloads { get; set; }
    public List<WorkshopModVersion> Versions { get; set; } = new();
}

public sealed class WorkshopModVersion
{
    public string Version { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public string SdkVersion { get; set; } = "";
    public int Permissions { get; set; }
    public List<WorkshopDependency> Dependencies { get; set; } = new();
    public string Changelog { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public string VerifiedAt { get; set; } = "";
    public long Downloads { get; set; }
}

public sealed class WorkshopDependency
{
    public string Id { get; set; } = "";
    public string MinVersion { get; set; } = "";
}

/// <summary>下载进度（总量未知时 Percent 为 null）。</summary>
public sealed class WorkshopDownloadProgress
{
    public WorkshopDownloadProgress(long receivedBytes, long? totalBytes)
    {
        ReceivedBytes = receivedBytes;
        TotalBytes = totalBytes;
    }

    public long ReceivedBytes { get; }
    public long? TotalBytes { get; }

    public int? Percent => TotalBytes is > 0
        ? (int)Math.Clamp(ReceivedBytes * 100 / TotalBytes.Value, 0, 100)
        : null;
}

/// <summary>工坊请求失败时抛出的用户可读错误。</summary>
public sealed class WorkshopException(string message) : Exception(message);
