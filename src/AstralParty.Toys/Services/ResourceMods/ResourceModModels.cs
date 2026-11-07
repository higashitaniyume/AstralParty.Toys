using System;
using System.Collections.Generic;

namespace AstralParty.Toys.Services.ResourceMods;

public sealed class ResourceModBundle
{
    public string FileName { get; set; } = "";
    public string BundleName { get; set; } = "";
    public long Size { get; set; }
}

public sealed class ResourceModItem
{
    public string Kind { get; set; } = "redirect";
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? Container { get; set; }
    public string? Reference { get; set; }
}

public sealed class ResourceModPackage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; }
    public bool Enabled { get; set; } = true;
    public int FileCount { get; set; }
    public long Size { get; set; }
    public bool HasModJson { get; set; }
    public bool HasPatch { get; set; }
    /// <summary>旧清单字段，保留读取兼容；新导入不再单独解析。</summary>
    public List<ResourceModBundle> Bundles { get; set; } = new();
    public List<ResourceModItem> Items { get; set; } = new();
    public List<string> SkippedEntries { get; set; } = new();
    public List<string> Unsupported { get; set; } = new();
}

public sealed class ResourceModAppliedState
{
    public string CatalogVersion { get; set; } = "";
    public DateTimeOffset AppliedAt { get; set; }
    public int Redirects { get; set; }
    public int Bundles { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();
    public string Signature { get; set; } = "";
}

public sealed class ResourceModManifest
{
    public int Schema { get; set; } = 1;
    public List<ResourceModPackage> Mods { get; set; } = new();
    public ResourceModAppliedState? Applied { get; set; }
}

public sealed class ResourceModPackageStatus
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SourceFile { get; set; } = "";
    public bool Enabled { get; set; }
    public int BundleCount { get; set; }
    public int MatchedBundles { get; set; }
    public int UnmatchedBundles { get; set; }
    public long Size { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public string[] UnmatchedNames { get; set; } = Array.Empty<string>();
    /// <summary>包内是否残留 U+FFFD 乱码文件名（旧版本按 UTF-8 解码 GBK 名字导致），需重新导入修正。</summary>
    public bool HasMojibakeNames { get; set; }
}

public sealed class ResourceModStatus
{
    public string GameDirectory { get; set; } = "";
    public string StoreDirectory { get; set; } = "";
    public bool StoreExists { get; set; }
    public string CatalogVersion { get; set; } = "";
    public bool CatalogAvailable { get; set; }
    public string CatalogError { get; set; } = "";
    public bool GameRunning { get; set; }
    public bool NeedsApply { get; set; }
    public string? AppliedVersion { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public int? AppliedRedirects { get; set; }
    public int AppliedErrors { get; set; }
    public int AppliedWarnings { get; set; }
    public string[] AppliedMessages { get; set; } = Array.Empty<string>();
    public long TotalSize { get; set; }
    public ResourceModPackageStatus[] Mods { get; set; } = Array.Empty<ResourceModPackageStatus>();
}

public sealed class ResourceModImportResult
{
    public ResourceModPackage Package { get; set; } = new();
    public bool Applied { get; set; }
    public int Redirects { get; set; }
    public int ApplyErrors { get; set; }
    public int ApplyWarnings { get; set; }
    public string[] ApplyMessages { get; set; } = Array.Empty<string>();
    public string? ApplyError { get; set; }
    public string? PendingReason { get; set; }
}

public sealed class ResourceModApplyResult
{
    public string CatalogVersion { get; set; } = "";
    public int Redirects { get; set; }
    public int Bundles { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
    public string[] Messages { get; set; } = Array.Empty<string>();
}

public sealed class ResourceModProgress
{
    public string Phase { get; set; } = "";
    public int Percent { get; set; }
    public string Task { get; set; } = "";
    public string Detail { get; set; } = "";
    public bool Paused { get; set; }
    public bool CanPause { get; set; } = true;
    public bool CanCancel { get; set; } = true;
}
