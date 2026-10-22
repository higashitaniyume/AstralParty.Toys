using System.IO;
using System.Security.Cryptography;
using System.Text;
using AddressablesTools.Catalog;
using AddressablesTools.Classes;

namespace AstralParty.Toys.Services;

internal static partial class ResourceModCatalog
{
    public const string BundledAssetProviderId = "UnityEngine.ResourceManagement.ResourceProviders.BundledAssetProvider";

    /// <summary>mod.json 的 Add：用 Reference 的依赖结构新建一个 Addressable，指向模组 bundle。</summary>
    public static bool AddAddressable(ContentCatalogData catalog, string name, string file, string? container, string? reference)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(reference) || !File.Exists(file)) return false;
        if (catalog.Resources.ContainsKey(name)) return false;

        var referenceLocation = FindBundledAsset(catalog, reference);
        if (referenceLocation is null) return false;
        var referenceBundle = GetPrimaryBundleDependency(catalog, referenceLocation);
        if (referenceBundle is null) return false;

        var bundle = CreateSyntheticBundle(catalog, file, referenceBundle);
        var location = new ResourceLocation
        {
            ProviderId = BundledAssetProviderId,
            InternalId = string.IsNullOrWhiteSpace(container) ? referenceLocation.InternalId : container,
            PrimaryKey = name,
            Type = referenceLocation.Type,
            DependencyKey = bundle.PrimaryKey,
            DependencyHashCode = bundle.HashCode,
            HashCode = Random.Shared.Next(),
            Data = referenceLocation.Data
        };
        catalog.Resources[name] = new List<ResourceLocation> { location };
        return true;
    }

    /// <summary>mod.json Redirect / replace.txt：把已有 Addressable 指到模组 bundle。</summary>
    public static bool RedirectAddressable(ContentCatalogData catalog, string name, string file, string? container)
    {
        if (string.IsNullOrWhiteSpace(name) || !File.Exists(file)) return false;
        if (!catalog.Resources.TryGetValue(name, out var locations) || locations.Count == 0) return false;

        var changed = false;
        foreach (var location in locations.ToList())
        {
            if (location.ProviderId == AssetBundleProviderId)
            {
                location.InternalId = file;
                changed = true;
                continue;
            }

            if (location.ProviderId != BundledAssetProviderId) continue;
            var referenceBundle = GetPrimaryBundleDependency(catalog, location)
                ?? locations.FirstOrDefault(item => item.ProviderId == AssetBundleProviderId);
            var bundle = CreateSyntheticBundle(catalog, file, referenceBundle);
            if (location.Dependencies is { Count: > 0 })
            {
                location.Dependencies.Clear();
                location.Dependencies.Add(bundle);
            }
            else
            {
                location.DependencyKey = bundle.PrimaryKey;
                location.DependencyHashCode = bundle.HashCode;
            }

            if (!string.IsNullOrWhiteSpace(container)) location.InternalId = container;
            changed = true;
        }
        return changed;
    }

    public static bool HasAddressable(ContentCatalogData catalog, string name) => catalog.Resources.ContainsKey(name);

    private static ResourceLocation? FindBundledAsset(ContentCatalogData catalog, string name)
    {
        if (!catalog.Resources.TryGetValue(name, out var locations)) return null;
        return locations.FirstOrDefault(location => location.ProviderId == BundledAssetProviderId);
    }

    private static ResourceLocation? GetPrimaryBundleDependency(ContentCatalogData catalog, ResourceLocation reference)
    {
        if (reference.DependencyKey is { } key)
        {
            if (catalog.Resources.TryGetValue(key, out var dependencies) && dependencies.Count > 0)
                return dependencies.FirstOrDefault(item => item.ProviderId == AssetBundleProviderId);

            // 数值键在 JSON / bundle 两套 catalog 里可能是 int 或 long，按文本再找一次。
            var keyText = key.ToString();
            foreach (var pair in catalog.Resources)
            {
                if (!string.Equals(pair.Key.ToString(), keyText, StringComparison.Ordinal)) continue;
                var fallback = pair.Value.FirstOrDefault(item => item.ProviderId == AssetBundleProviderId);
                if (fallback is not null) return fallback;
            }
        }

        return reference.Dependencies?.FirstOrDefault(item => item.ProviderId == AssetBundleProviderId);
    }

    private static ResourceLocation CreateSyntheticBundle(ContentCatalogData catalog, string file, ResourceLocation? template)
    {
        var stem = Path.GetFileNameWithoutExtension(file);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(file + File.GetLastWriteTimeUtc(file).Ticks)))[..8].ToLowerInvariant();
        var primaryKey = "toys." + (stem.Length > 40 ? stem[..40] : stem) + "." + hash + ".bundle";
        if (catalog.Resources.TryGetValue(primaryKey, out var existing) && existing.Count > 0)
            return existing[0];

        var options = new AssetBundleRequestOptions
        {
            Hash = "",
            Crc = 0,
            BundleName = primaryKey
        };

        WrappedSerializedObject? data = null;
        if (template?.Data is WrappedSerializedObject { Object: AssetBundleRequestOptions templateOptions, Type: SerializedType templateType })
        {
            options.ComInfo = templateOptions.ComInfo;
            data = new WrappedSerializedObject(templateType, options);
        }

        var location = new ResourceLocation
        {
            ProviderId = AssetBundleProviderId,
            InternalId = file,
            PrimaryKey = primaryKey,
            Type = template?.Type,
            HashCode = Random.Shared.Next(),
            Data = data
        };
        catalog.Resources[primaryKey] = new List<ResourceLocation> { location };
        return location;
    }
}
