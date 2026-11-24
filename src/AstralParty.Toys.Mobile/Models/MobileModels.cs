using System.Text.Json.Serialization;

namespace AstralParty.Toys.Mobile.Models;

/// <summary>A device found on a channel. Kept JSON-friendly so it can be sent to the WebUI as-is.</summary>
public sealed class DeviceInfo
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "ios";

    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("osVersion")]
    public string OsVersion { get; set; } = "";

    [JsonPropertyName("paired")]
    public bool Paired { get; set; }

    [JsonPropertyName("state")]
    public string State { get; set; } = "online";

    [JsonPropertyName("lastSeen")]
    public string LastSeen { get; set; } = "";
}

/// <summary>A remembered device target. Stored in state/mobile-targets.json.</summary>
public sealed class MobileTarget
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "ios";

    [JsonPropertyName("serial")]
    public string Serial { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("osVersion")]
    public string OsVersion { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "online";

    [JsonPropertyName("paired")]
    public bool Paired { get; set; }

    [JsonPropertyName("appId")]
    public string AppId { get; set; } = "";

    [JsonPropertyName("online")]
    public bool Online { get; set; }

    [JsonPropertyName("installed")]
    public bool Installed { get; set; }

    [JsonPropertyName("bundleOuter")]
    public string BundleOuter { get; set; } = "";

    [JsonPropertyName("installedInner")]
    public string InstalledInner { get; set; } = "";

    [JsonPropertyName("installedPatchedSha256")]
    public string InstalledPatchedSha256 { get; set; } = "";

    [JsonPropertyName("originalSha256")]
    public string OriginalSha256 { get; set; } = "";

    [JsonPropertyName("lastSeen")]
    public string LastSeen { get; set; } = "";
}

/// <summary>Which target the UI is currently acting on. state/active-target.json.</summary>
public sealed class ActiveTargetState
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "local";

    [JsonPropertyName("profileId")]
    public string ProfileId { get; set; } = "";

    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = "";
}

public static class PatchStates
{
    public const string Unknown = "unknown";
    public const string Clean = "clean";
    public const string Patched = "patched";
    public const string PatchedExternal = "patched-external";
    public const string Stale = "stale";
}

/// <summary>Read-only result of a device detection pass.</summary>
public sealed class MobileDetectReport
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "ios";

    [JsonPropertyName("targetId")]
    public string TargetId { get; set; } = "";

    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("bundleId")]
    public string BundleId { get; set; } = "";

    [JsonPropertyName("appVersion")]
    public string AppVersion { get; set; } = "";

    [JsonPropertyName("catalog")]
    public string Catalog { get; set; } = "";

    [JsonPropertyName("catalogSha256")]
    public string CatalogSha256 { get; set; } = "";

    [JsonPropertyName("catalogBuildHash")]
    public string CatalogBuildHash { get; set; } = "";

    [JsonPropertyName("codeBundleHash")]
    public string CodeBundleHash { get; set; } = "";

    [JsonPropertyName("bundleDir")]
    public string BundleDir { get; set; } = "";

    [JsonPropertyName("bundleBytes")]
    public long BundleBytes { get; set; }

    [JsonPropertyName("bundleSha256")]
    public string BundleSha256 { get; set; } = "";

    [JsonPropertyName("assetCount")]
    public int AssetCount { get; set; }

    [JsonPropertyName("injected")]
    public bool Injected { get; set; }

    [JsonPropertyName("legacyMarker")]
    public bool LegacyMarker { get; set; }

    [JsonPropertyName("currentMarker")]
    public bool CurrentMarker { get; set; }

    [JsonPropertyName("patchState")]
    public string PatchState { get; set; } = PatchStates.Unknown;

    [JsonPropertyName("loaderPresent")]
    public bool LoaderPresent { get; set; }

    [JsonPropertyName("loaderRoot")]
    public string LoaderRoot { get; set; } = "";

    [JsonPropertyName("loaderVersion")]
    public string LoaderVersion { get; set; } = "";

    [JsonPropertyName("sdkVersion")]
    public string SdkVersion { get; set; } = "";

    [JsonPropertyName("bootstrapAssembly")]
    public string BootstrapAssembly { get; set; } = "";

    [JsonPropertyName("modCount")]
    public int ModCount { get; set; }

    [JsonPropertyName("modsOk")]
    public int ModsOk { get; set; }

    [JsonPropertyName("modsFailed")]
    public int ModsFailed { get; set; }

    [JsonPropertyName("bootstrapLog")]
    public string BootstrapLog { get; set; } = "";

    [JsonPropertyName("bootstrapTail")]
    public List<string> BootstrapTail { get; set; } = [];

    [JsonPropertyName("markers")]
    public List<string> Markers { get; set; } = [];

    [JsonPropertyName("issues")]
    public List<string> Issues { get; set; } = [];

    [JsonPropertyName("detectedAt")]
    public string DetectedAt { get; set; } = "";
}
