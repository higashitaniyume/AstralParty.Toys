using System.Text.Json;

namespace AstralParty.Toys.Services;

/// <summary>
/// 读取 mod <c>config.json</c> 里的保留键 <c>_comments</c>，按当前界面语言取一条说明。
///
/// 结构:
/// <code>
/// "_comments": {
///   "cooldownSeconds": { "zh-Hans": "…", "zh-Hant": "…", "en": "…", "ja": "…" },
///   "enabled": "所有语言共用的一句说明"
/// }
/// </code>
///
/// 兼容: 没有 <c>_comments</c>、字段没有说明、说明只有部分语言、值写成字符串，都按“没有说明/可用说明”处理，
/// 不会影响配置字段本身。
/// </summary>
internal static class ModConfigComments
{
    public const string Key = "_comments";
    private const int MaxLength = 2000;

    /// <summary>取某个字段在当前语言下的说明；没有可用说明时返回空串。</summary>
    public static string Resolve(JsonElement root, string fieldName, string? language)
    {
        if (string.IsNullOrEmpty(fieldName)) return "";
        if (root.ValueKind != JsonValueKind.Object) return "";
        if (!root.TryGetProperty(Key, out var comments) || comments.ValueKind != JsonValueKind.Object) return "";
        if (!comments.TryGetProperty(fieldName, out var entry)) return "";

        if (entry.ValueKind == JsonValueKind.String) return Clean(entry.GetString());
        if (entry.ValueKind != JsonValueKind.Object) return "";

        string target = AppLanguage.Normalize(language);
        foreach (string code in FallbackOrder(target))
        {
            string? value = Pick(entry, code);
            if (value != null) return Clean(value);
        }

        // 当前语言、常见回退都没有时，用文件里第一个非空语言，避免用户只写了一种语言却完全看不到。
        foreach (var prop in entry.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String) continue;
            string value = Clean(prop.Value.GetString());
            if (value.Length > 0) return value;
        }
        return "";
    }

    private static string[] FallbackOrder(string language)
    {
        if (language == AppLanguage.TraditionalChinese)
            return [AppLanguage.TraditionalChinese, AppLanguage.SimplifiedChinese, AppLanguage.English, AppLanguage.Japanese];
        if (language == AppLanguage.English)
            return [AppLanguage.English, AppLanguage.SimplifiedChinese, AppLanguage.Japanese];
        if (language == AppLanguage.Japanese)
            return [AppLanguage.Japanese, AppLanguage.SimplifiedChinese, AppLanguage.English];
        return [AppLanguage.SimplifiedChinese, AppLanguage.TraditionalChinese, AppLanguage.English, AppLanguage.Japanese];
    }

    /// <summary>按语言别名取词（zh-CN → zh-Hans、zh-TW → zh-Hant 等）。</summary>
    private static string? Pick(JsonElement entry, string target)
    {
        foreach (var prop in entry.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String) continue;
            if (NormalizeCode(prop.Name) != target) continue;
            string value = Clean(prop.Value.GetString());
            if (value.Length > 0) return value;
        }
        return null;
    }

    private static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        string lower = code.Trim().ToLowerInvariant();
        return lower switch
        {
            "zh-hant" or "zh-tw" or "zh-hk" or "zh-mo" or "zh-hant-tw" or "zh-hant-hk" or "traditional" or "zht" => AppLanguage.TraditionalChinese,
            "zh-hans" or "zh-cn" or "zh-sg" or "zh" or "chs" or "simplified" => AppLanguage.SimplifiedChinese,
            "en" or "en-us" or "en-gb" or "english" => AppLanguage.English,
            "ja" or "ja-jp" or "japanese" => AppLanguage.Japanese,
            _ => null
        };
    }

    private static string Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        string value = text.Trim();
        return value.Length <= MaxLength ? value : value[..MaxLength];
    }
}
