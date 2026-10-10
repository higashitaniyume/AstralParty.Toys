namespace AstralParty.Toys.Services;

/// <summary>CesiumLoader 发布包清单（cesium-loader.json）。</summary>
public sealed class LoaderPackageInfo
{
    public string Version { get; set; } = "";
    public Dictionary<string, string> Files { get; set; } = new();
}
