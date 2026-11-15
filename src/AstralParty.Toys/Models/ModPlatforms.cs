namespace AstralParty.Toys.Models;

/// <summary>
/// mod 支持的平台标记（sidecar 的 <c>platforms</c> 数组，与 SDK 的 <c>ModPlatform</c> 一致）。
///
/// 取值只有 <c>windows</c> 与 <c>android</c>；两个都写就是双平台。没有声明（或只写了认不出的取值）
/// 按双平台处理，旧 mod 与第三方 mod 不受影响。平台只用于界面标注：加载器照常加载，
/// 不会因为平台不匹配拒绝某个 mod。
/// </summary>
public static class ModPlatforms
{
    public const string Windows = "windows";
    public const string Android = "android";

    /// <summary>规范化平台列表：只保留已知取值、去重、Windows 在前。</summary>
    public static List<string> Normalize(IEnumerable<string>? platforms)
    {
        if (platforms is null) return new List<string>();
        bool windows = false;
        bool android = false;
        foreach (var raw in platforms)
        {
            if (raw is null) continue;
            var value = raw.Trim().ToLowerInvariant();
            if (value == Windows) windows = true;
            else if (value == Android) android = true;
        }
        var list = new List<string>(2);
        if (windows) list.Add(Windows);
        if (android) list.Add(Android);
        return list;
    }

    /// <summary>声明里是否包含某个平台；没有声明按「两个平台都支持」处理。</summary>
    public static bool Supports(IEnumerable<string>? platforms, string platform)
    {
        var declared = Normalize(platforms);
        if (declared.Count == 0) return true;
        return declared.Contains(platform.Trim().ToLowerInvariant());
    }

    /// <summary>界面标注：只在单平台时给标签，双平台/未声明返回 null。</summary>
    public static string? Badge(IEnumerable<string>? platforms)
    {
        var declared = Normalize(platforms);
        if (declared.Count != 1) return null;
        return declared[0] == Windows ? "仅 Windows" : "仅 Android";
    }
}
