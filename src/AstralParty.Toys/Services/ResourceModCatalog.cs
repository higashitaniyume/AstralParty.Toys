using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using AddressablesTools;
using AddressablesTools.Catalog;
using AddressablesTools.Classes;

namespace AstralParty.Toys.Services;

/// <summary>
/// 游戏安装目录与 Addressables catalog 的定位、读取与回写。
/// 资源模组只改 catalog 里 AssetBundleProvider 的 InternalId，把它指向模组自己的 bundle；
/// 备份语义与 ResourceModLoader 一致：首次应用前保留原始 catalog，之后都从原始文件重算。
/// </summary>
internal sealed record ResourceModGameLayout(
    string GameDirectory,
    string DataDirectory,
    string Company,
    string Product,
    string CatalogVersion,
    string CatalogJsonPath,
    string CatalogBundlePath)
{
    public bool CatalogJsonExists => CatalogJsonPath.Length > 0 && File.Exists(CatalogJsonPath);
    public bool CatalogBundleExists => CatalogBundlePath.Length > 0 && File.Exists(CatalogBundlePath);
}

internal static partial class ResourceModCatalog
{
    public const string AssetBundleProviderId = "UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider";

    private static readonly Regex CatalogVersionPattern = new(@"catalog_(?<v>[0-9A-Za-z._\-]+)\.hash", RegexOptions.Compiled);

    /// <summary>按游戏目录解析 app.info / settings.json；解析不到版本时返回 CatalogVersion = ""。</summary>
    public static ResourceModGameLayout? TryResolve(string? gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory)) return null;
        string root;
        try { root = Path.GetFullPath(gameDirectory); } catch { return null; }
        if (!Directory.Exists(root)) return null;

        string? dataDir = null;
        try
        {
            dataDir = Directory.EnumerateDirectories(root, "*_Data", SearchOption.TopDirectoryOnly)
                .FirstOrDefault(dir => File.Exists(Path.Combine(dir, "app.info")))
                ?? Directory.EnumerateDirectories(root, "*_Data", SearchOption.TopDirectoryOnly).FirstOrDefault();
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        if (dataDir is null) return null;

        string company = "feimo";
        string product = "AstralParty_CN";
        var appInfo = Path.Combine(dataDir, "app.info");
        if (File.Exists(appInfo))
        {
            try
            {
                var lines = File.ReadAllLines(appInfo).Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
                if (lines.Length >= 2)
                {
                    company = lines[0];
                    product = lines[1];
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        var addressablesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", company, product, "com.unity.addressables");

        string version = "";
        var settings = Path.Combine(dataDir, "StreamingAssets", "aa", "settings.json");
        if (File.Exists(settings))
        {
            try
            {
                var match = CatalogVersionPattern.Match(File.ReadAllText(settings));
                if (match.Success) version = match.Groups["v"].Value;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        if (version.Length == 0 && Directory.Exists(addressablesDir))
        {
            // settings.json 缺失时退化为「最新的 catalog_<版本>.json」。
            version = Directory.EnumerateFiles(addressablesDir, "catalog_*.json", SearchOption.TopDirectoryOnly)
                .Select(path => Path.GetFileName(path))
                .Select(name => name["catalog_".Length..^".json".Length])
                .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault() ?? "";
        }

        var jsonPath = version.Length > 0 ? Path.Combine(addressablesDir, "catalog_" + version + ".json") : "";
        var bundlePath = Path.Combine(dataDir, "StreamingAssets", "aa", "catalog.bundle");
        return new ResourceModGameLayout(root, dataDir, company, product, version, jsonPath, bundlePath);
    }

    public static ContentCatalogData LoadJson(string path) =>
        AddressablesCatalogFileParser.FromJsonString(File.ReadAllText(path));

    public static ContentCatalogData? TryLoadBundle(string path)
    {
        if (!File.Exists(path)) return null;
        return AddressablesCatalogFileParser.FromBundle(path);
    }

    /// <summary>catalog 中所有 AssetBundleProvider 的 bundle 名（去掉扩展名），用于判断模组是否能匹配。</summary>
    public static HashSet<string> ReadBundleStems(ContentCatalogData catalog)
    {
        var stems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var locations in catalog.Resources.Values)
        {
            foreach (var location in locations)
            {
                if (location.ProviderId != AssetBundleProviderId) continue;
                if (location.Data is WrappedSerializedObject { Object: AssetBundleRequestOptions options })
                {
                    var stem = Stem(options.BundleName);
                    if (stem.Length > 0) stems.Add(stem);
                }
            }
        }
        return stems;
    }

    /// <summary>把 catalog 里匹配 stem 的 bundle 位置改到模组文件；返回改写的 location 数量。</summary>
    public static int ApplyRedirects(ContentCatalogData catalog, IReadOnlyDictionary<string, string> stemToFile)
    {
        if (stemToFile.Count == 0) return 0;
        var count = 0;
        foreach (var locations in catalog.Resources.Values)
        {
            foreach (var location in locations)
            {
                if (location.ProviderId != AssetBundleProviderId) continue;
                if (location.Data is not WrappedSerializedObject { Object: AssetBundleRequestOptions options }) continue;
                var stem = Stem(options.BundleName);
                if (stem.Length == 0 || !stemToFile.TryGetValue(stem, out var file)) continue;
                if (!File.Exists(file)) continue;
                if (string.Equals(location.InternalId, file, StringComparison.OrdinalIgnoreCase)) continue;
                location.InternalId = file;
                count++;
            }
        }
        return count;
    }

    public static void WriteJson(ContentCatalogData catalog, string targetPath)
    {
        var text = AddressablesCatalogFileParser.ToJsonString(catalog);
        var temp = targetPath + ".toys-tmp";
        File.WriteAllText(temp, text);
        // 先自检，再落盘；自检失败时保留原文件。
        _ = AddressablesCatalogFileParser.FromJsonString(File.ReadAllText(temp));
        File.Move(temp, targetPath, overwrite: true);
    }

    public static void WriteBundle(ContentCatalogData catalog, string templatePath, string targetPath)
    {
        var temp = targetPath + ".toys-tmp";
        AddressablesCatalogFileParser.ToBundle(catalog, templatePath, temp);
        _ = AddressablesCatalogFileParser.FromBundle(temp);
        File.Move(temp, targetPath, overwrite: true);
    }

    public static string Stem(string? bundleName)
    {
        if (string.IsNullOrWhiteSpace(bundleName)) return "";
        var name = Path.GetFileName(bundleName.Trim());
        var withoutExtension = Path.GetFileNameWithoutExtension(name);
        return withoutExtension.Length > 0 ? withoutExtension : name;
    }
}
