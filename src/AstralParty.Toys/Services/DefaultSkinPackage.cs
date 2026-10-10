using System.IO.Compression;

namespace AstralParty.Toys.Services;

/// <summary>只读应用内置默认皮肤，用户磁盘上的 default 不作为原始模板。</summary>
public static class DefaultSkinPackage
{
    private const string ResourceName = "AstralParty.Toys.DefaultSkins.zip";
    private const string Prefix = "AstralParty_ModLoader/skins/default/";
    private static ZipArchive Open()
    {
        var stream = typeof(DefaultSkinPackage).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("应用缺少内置默认皮肤包，请重新安装 Toys。");
        return new ZipArchive(stream, ZipArchiveMode.Read);
    }
    private static string RelativeName(ZipArchiveEntry entry)
    {
        var name = entry.FullName.Replace('\\', '/');
        if (!name.StartsWith(Prefix, StringComparison.Ordinal)) throw new InvalidOperationException("内置皮肤包路径不正确。");
        var relative = name[Prefix.Length..];
        if (relative.Contains('/') || relative.Contains(':') || relative is "" or "." or "..")
            throw new InvalidOperationException("内置皮肤包只能包含默认方案文件。");
        return relative;
    }
    public static int Install(string directory)
    {
        using var archive = Open();
        var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToArray();
        foreach (var entry in entries) _ = RelativeName(entry);
        if (!entries.Any(e => Path.GetExtension(e.Name).Equals(".png", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("内置皮肤包为空。");
        Directory.CreateDirectory(directory);
        foreach (var entry in entries) entry.ExtractToFile(Path.Combine(directory, RelativeName(entry)), true);
        return entries.Length;
    }
    public static void RestoreCard(string directory, string fileName)
    {
        if (Path.GetFileName(fileName) != fileName || fileName.Contains('\\')) throw new ArgumentException("卡面文件名不正确");
        using var archive = Open();
        var entry = archive.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.Name) && RelativeName(e).Equals(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("内置默认包没有这张卡面，无法恢复。");
        Directory.CreateDirectory(directory);
        entry.ExtractToFile(Path.Combine(directory, fileName), true);
    }
    private static readonly Lazy<HashSet<string>> Names = new(() => {
        using var archive = Open();
        return archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Select(RelativeName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    });
    public static bool Contains(string fileName) => Names.Value.Contains(fileName);
}
