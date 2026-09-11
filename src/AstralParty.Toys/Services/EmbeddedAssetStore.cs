using System.Reflection;

namespace AstralParty.Toys.Services;

public static class EmbeddedAssetStore
{
    private const string ResourcePrefix = "AstralParty.Toys.PackedAssets.";
    private const string AssetPrefix = "embedded://asset/";
    private static readonly Assembly Assembly = typeof(EmbeddedAssetStore).Assembly;
    private static readonly IReadOnlyDictionary<string, string> Resources = BuildResourceIndex();

    public static int Count => Resources.Count;

    public static bool TryFind(string folder, string name, out string assetPath)
    {
        var relativePath = Normalize($"{folder}/{Path.GetFileNameWithoutExtension(name)}.webp");
        if (Resources.ContainsKey(relativePath))
        {
            assetPath = AssetPrefix + relativePath;
            return true;
        }

        assetPath = "";
        return false;
    }

    public static bool TryGetRelativePath(string assetPath, out string relativePath)
    {
        if (assetPath.StartsWith(AssetPrefix, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = Normalize(assetPath[AssetPrefix.Length..]);
            return Resources.ContainsKey(relativePath);
        }

        relativePath = "";
        return false;
    }

    public static Stream? OpenWebPath(string relativePath)
    {
        var key = Normalize(Uri.UnescapeDataString(relativePath).TrimStart('/'));
        return Resources.TryGetValue(key, out var resourceName)
            ? Assembly.GetManifestResourceStream(resourceName)
            : null;
    }

    private static IReadOnlyDictionary<string, string> BuildResourceIndex()
    {
        var resources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var resourceName in Assembly.GetManifestResourceNames()
                     .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) &&
                                    (name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                                     name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))))
        {
            var remainder = resourceName[ResourcePrefix.Length..];
            var folderEnd = remainder.IndexOf('.');
            if (folderEnd <= 0) continue;
            var folder = remainder[..folderEnd];
            var fileName = remainder[(folderEnd + 1)..];
            resources[Normalize($"{folder}/{fileName}")] = resourceName;
        }

        return resources;
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
