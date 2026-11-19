namespace AstralParty.Toys.Services.Game;

public sealed partial class GameLibraryService
{
    internal static GameProfileView DescribeProfile(GameProfile profile, bool active, bool exists, bool valid, string? exe) => new()
    {
        Id = profile.Id,
        Label = profile.Label,
        Directory = profile.Directory,
        Edition = profile.Edition,
        EditionLabel = EditionLabel(profile.Edition),
        Active = active,
        DirectoryExists = exists,
        Valid = valid,
        ExeName = exe
    };

    public static string EditionLabel(string edition) => edition switch
    {
        "cn" => "国服",
        "global" => "国际服",
        "taptap" => "TapTap",
        _ => "自定义"
    };

    internal static string DefaultLabel(string edition, string directory)
    {
        var name = EditionLabel(edition);
        if (edition == "custom")
        {
            var leaf = SafeLeafName(directory);
            return string.IsNullOrEmpty(leaf) ? "吉星派对" : leaf;
        }
        return $"吉星派对 · {name}";
    }
    private static string SafeLeafName(string path)
    {
        try { return new DirectoryInfo(path.TrimEnd('\\', '/')).Name; }
        catch { return ""; }
    }

}
