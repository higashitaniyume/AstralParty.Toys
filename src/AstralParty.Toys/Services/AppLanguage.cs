namespace AstralParty.Toys.Services;

/// <summary>应用支持的语言常量与归一化辅助类。</summary>
public static class AppLanguage
{
    public const string SimplifiedChinese = "zh-Hans";
    public const string English = "en";
    public const string Japanese = "ja";
    public const string TraditionalChinese = "zh-Hant";

    public static readonly string[] Supported =
    [
        SimplifiedChinese,
        English,
        Japanese,
        TraditionalChinese
    ];

    /// <summary>
    /// 将输入的语言代码归一化为支持的 4 种标准代码之一。
    /// 未知或空值统一回退为简体中文（zh-Hans）。
    /// </summary>
    public static string Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return SimplifiedChinese;
        var lower = language.Trim().ToLowerInvariant();
        if (lower is "en" or "en-us" or "en-gb" or "english") return English;
        if (lower is "ja" or "ja-jp" or "japanese") return Japanese;
        if (lower is "zh-hant" or "zh-tw" or "zh-hk" or "zh-mo" or "traditional" or "zht") return TraditionalChinese;
        return SimplifiedChinese;
    }

    /// <summary>对应游戏配置表 STR*.bin 中 LocalConfigure 的属性名。</summary>
    public static string ToProtoProperty(string? language) => Normalize(language) switch
    {
        English => "English",
        Japanese => "Japanese",
        TraditionalChinese => "Traditional",
        _ => "Simplified"
    };
}
