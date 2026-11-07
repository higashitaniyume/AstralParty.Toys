namespace AstralParty.Toys.Services.Modding;

/// <summary>市场公共接口的返回模型（与 ForgeHiynet 的 <c>/api/mods</c> 对应）。</summary>
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
    public string RepositoryUrl { get; set; } = "";
    public string License { get; set; } = "";
    public string LicenseText { get; set; } = "";
    public string Readme { get; set; } = "";
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

    /// <summary>作者托管的原始直链；市场自己托管包时为空。</summary>
    public string SourceUrl { get; set; } = "";

    /// <summary>包是否由市场保存（不是 302 到作者）。</summary>
    public bool Hosted { get; set; }

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

/// <summary>市场贴纸回应：一个模组上某张贴纸的计数与「我是否回应过」。</summary>
public sealed class StickerCount
{
    public string StickerId { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public long Count { get; set; }
    public bool ReactedByMe { get; set; }
}

/// <summary>单个模组的贴纸回应状态（批量查询与点选切换都返回这个形状）。</summary>
public sealed class ModStickerState
{
    public string ModId { get; set; } = "";
    public List<StickerCount> Stickers { get; set; } = new();
    public long ReactionTotal { get; set; }
}

/// <summary>批量贴纸回应接口的返回（与 Forge 的 <c>/api/mod-stickers</c> 对应）。</summary>
public sealed class ModStickersPayload
{
    public string GeneratedAt { get; set; } = "";
    public List<ModStickerState> Items { get; set; } = new();
}

/// <summary>市场贴纸目录：<c>GET /api/stickers</c> 的返回（与 Forge 的贴纸目录对应）。</summary>
public sealed class StickerCatalogResponse
{
    public string GeneratedAt { get; set; } = "";
    public List<StickerGroup> Groups { get; set; } = new();
    public List<StickerCatalogItem> Items { get; set; } = new();
    public int Total { get; set; }
}

/// <summary>贴纸目录中的一个分组（角色）。</summary>
public sealed class StickerGroup
{
    public string GroupId { get; set; } = "";
    public string GroupName { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>贴纸目录中的一张贴纸；<see cref="ImageUrl"/> 由宿主改写为本地缓存地址。</summary>
public sealed class StickerCatalogItem
{
    public string StickerId { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string File { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string Kind { get; set; } = "static";
    public int SortIndex { get; set; }
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

/// <summary>贴纸图片缓存进度（目录拉取完成后按张计数，已缓存的也计入完成）。</summary>
public sealed class WorkshopStickerDownloadProgress
{
    public WorkshopStickerDownloadProgress(int downloaded, int total)
    {
        Downloaded = downloaded;
        Total = total;
    }

    public int Downloaded { get; }
    public int Total { get; }

    public int Percent => Total > 0
        ? (int)Math.Clamp(Downloaded * 100.0 / Total, 0, 100)
        : 0;
}

/// <summary>市场请求失败时抛出的用户可读错误。</summary>
public sealed class WorkshopException(string message) : Exception(message);
