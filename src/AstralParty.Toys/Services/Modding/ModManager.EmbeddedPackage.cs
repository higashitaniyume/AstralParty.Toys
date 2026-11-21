using System.IO.Compression;
using System.Reflection;
using AstralParty.Toys.Models;

namespace AstralParty.Toys.Services.Modding;

/// <summary>
/// 随程序集内置的 CesiumLoader **发布包**（<c>Resources\ModLoader\cesium-loader.zip</c>）。
///
/// 「安装 / 重新安装内置版本」= 解压这份发布包，与「导入发布包」走完全同一条代码路径
/// （见 <see cref="InstallPackage"/>），区别只是包的来源。
///
/// 因此包内的版本号、SDK 版本、内置 mod 清单与加载器更新日志都直接从包里读，
/// 程序集里不再有第二份需要手工同步的清单（旧实现是逐文件内嵌 + <c>BuiltInMods.props</c>，
/// 两者漂移过一次：加载器包里已有的内置 mod 在 Toys 侧漏装）。
/// </summary>
public sealed partial class ModManager
{
    /// <summary>内嵌发布包的文件名（也是内嵌资源名的后缀）。</summary>
    public const string EmbeddedPackageFileName = "cesium-loader.zip";

    /// <summary>内嵌加载器相关资源的统一前缀（资源名 = <c>AstralParty.Toys.ModLoader.*</c>）。</summary>
    private const string ResourcePrefix = "AstralParty.Toys.ModLoader.";

    /// <summary>发布包内 mod 条目的前缀；zip 内路径统一用正斜杠。</summary>
    private const string PackageModsPrefix = "AstralParty_ModLoader/mods/";

    /// <summary>发布包内加载器目录的前缀。</summary>
    private const string PackageLoaderPrefix = "AstralParty_ModLoader/";

    private const string PackageConfigEntry = PackageLoaderPrefix + ConfigFileName;
    private const string PackageSdkEntry = PackageLoaderPrefix + SdkFolderName + "/" + SdkDllName;

    /// <summary>发布包根目录的加载器更新日志（离线兜底用）。</summary>
    private const string PackageChangelogEntry = "CHANGELOG.md";

    private static readonly Assembly Assembly = typeof(ModManager).Assembly;

    private static readonly Lazy<byte[]?> EmbeddedPackageBytesLazy = new(ReadEmbeddedPackage);

    /// <summary>内嵌发布包的原始字节；没有内嵌包（裁剪构建 / 本地未同步）时为 null。</summary>
    public static byte[]? EmbeddedPackageBytes => EmbeddedPackageBytesLazy.Value;

    /// <summary>程序集里是否带了内置发布包。</summary>
    public static bool HasEmbeddedPackage => EmbeddedPackageBytes is { Length: > 0 };

    /// <summary>内嵌发布包的清单（版本 + 各文件 SHA256）；没有内嵌包或清单损坏时为 null。</summary>
    public static LoaderPackageInfo? EmbeddedPackageManifest
        => EmbeddedPackageBytes is { Length: > 0 } bytes ? ParsePackageManifest(bytes) : null;

    /// <summary>随程序集内置的加载器版本号（来自包内清单；空 = 没有内嵌包）。</summary>
    public static string BundleLoaderVersion => EmbeddedPackageManifest?.Version ?? "";

    /// <summary>内置加载器版本号（等同 <see cref="BundleLoaderVersion"/>，内部沿用旧名）。</summary>
    private static string EmbeddedVersion => BundleLoaderVersion;

    /// <summary>内嵌发布包里 doorstop_config.json 模板声明的 SDK 版本。</summary>
    public static string BundleSdkVersion
        => ReadPackageEntryBytes(PackageConfigEntry) is { Length: > 0 } config
            ? ReadSdkVersionFromConfigBytes(config)
            : "";

    /// <summary>内嵌加载器版本号（同 <see cref="BundleSdkVersion"/> 的旧名）。</summary>
    private static string EmbeddedSdkVersion => BundleSdkVersion;

    /// <summary>内嵌发布包里 version.dll 的 SHA256；用来判「游戏目录里的 version.dll 是不是本工具装的」。</summary>
    private static readonly Lazy<string?> EmbeddedLoaderHashLazy = new(() =>
        ReadPackageEntryBytes(LoaderDllName) is { Length: > 0 } dll ? HashBytes(dll) : null);

    private static string? EmbeddedLoaderHash => EmbeddedLoaderHashLazy.Value;

    public static bool HasEmbeddedLoader => ReadPackageEntryBytes(LoaderDllName) is not null;
    public static bool HasEmbeddedConfig => ReadPackageEntryBytes(PackageConfigEntry) is not null;
    public static bool HasEmbeddedSdk => ReadPackageEntryBytes(PackageSdkEntry) is not null;

    /// <summary>内嵌包里是否带内置 mod（至少一个）。</summary>
    public static bool HasEmbeddedBuiltInMods => BuiltInModIds.Count > 0;

    /// <summary>内嵌发布包里是否包含行为日志（ActivityLogMod）——兼容旧判定。</summary>
    public static bool HasEmbeddedSampleMod => BuiltInModIds.Contains(SampleModDllName.Replace(".dll", ""));

    /// <summary>内嵌的加载器更新日志（包内 <c>CHANGELOG.md</c>）；读不到返回空串。</summary>
    public static string BundledLoaderChangelog
    {
        get
        {
            var text = ReadPackageEntryText(PackageChangelogEntry).Trim();
            // 包内没有 / 内容是占位符时，不当作有效日志（调用方会回退到联网取仓库根 CHANGELOG.md）
            return text.StartsWith("<!-- placeholder", StringComparison.OrdinalIgnoreCase) ? "" : text;
        }
    }

    /// <summary>
    /// 随程序集内置的 mod（ModId，序数排序 = 安装顺序）：**直接来自内嵌发布包的 <c>mods\</c> 目录**。
    /// 清单只有一份，就是加载器发布包本身，不可能再和 Toys 侧的另一份清单对不上。
    /// </summary>
    public static readonly IReadOnlyList<string> BuiltInModIds = ReadBuiltInModIds();

    private static string[] ReadBuiltInModIds()
    {
        try
        {
            var ids = new List<string>();
            foreach (var entry in ReadPackageEntries())
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;   // 目录条目
                if (!entry.FullName.StartsWith(PackageModsPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                var relative = entry.FullName.Substring(PackageModsPrefix.Length);
                var separator = relative.IndexOf('/');
                if (separator <= 0) continue;
                var id = relative.Substring(0, separator);
                if (!ids.Contains(id, StringComparer.Ordinal)) ids.Add(id);
            }
            ids.Sort(StringComparer.Ordinal);
            return ids.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static byte[]? ReadEmbeddedPackage()
    {
        try
        {
            using var stream = Assembly.GetManifestResourceStream(ResourcePrefix + EmbeddedPackageFileName);
            if (stream is null) return null;
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>内嵌发布包里的文件条目（没有内嵌包时为空表）。每次调用都重新打开一次中央目录，调用方按需缓存。</summary>
    private static IReadOnlyList<ZipArchiveEntry> ReadPackageEntries()
    {
        if (EmbeddedPackageBytes is not { Length: > 0 } bytes) return Array.Empty<ZipArchiveEntry>();
        using var stream = new MemoryStream(bytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        return archive.Entries.ToArray();
    }

    /// <summary>读内嵌发布包里的一个文件条目（缺失返回 null）。</summary>
    private static byte[]? ReadPackageEntryBytes(string entryName)
    {
        if (EmbeddedPackageBytes is not { Length: > 0 } bytes) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var entry = archive.GetEntry(entryName);
            if (entry is null || entry.Length <= 0) return null;
            using var entryStream = entry.Open();
            using var memory = new MemoryStream();
            entryStream.CopyTo(memory);
            return memory.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>读内嵌发布包里的一个文本条目（缺失返回空串）。</summary>
    private static string ReadPackageEntryText(string entryName)
    {
        var bytes = ReadPackageEntryBytes(entryName);
        return bytes is null ? "" : System.Text.Encoding.UTF8.GetString(bytes);
    }
}
