using System.Text;
using System.Text.Json;
using AstralParty.Toys.Mobile.Models;

namespace AstralParty.Toys.Mobile.Payload;

/// <summary>
/// 模组 <c>config.json</c> 的读写：**只做文本定点替换，绝不重新序列化**。
/// 重新序列化会顺手改掉注释、空行、以及 <c>1</c> 与 <c>1.0</c> 这种写法 —— 玩家手写的配置会被我们改花。
/// 表单只处理标量（bool / number / string）；对象、数组这类结构原样保留，改动走「整段文本编辑」。
/// </summary>
public static class MobileModConfig
{
    /// <summary>读出所有顶层标量字段，并按界面语言取 <c>_comments</c> 说明。</summary>
    public static IReadOnlyList<MobileConfigField> Parse(string text, string language)
    {
        var fields = new List<MobileConfigField>();
        if (string.IsNullOrWhiteSpace(text))
            return fields;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(StripComments(text));
        }
        catch (JsonException)
        {
            return fields;   // 解析不了就当没有可编辑字段（原始文本仍可整段编辑）
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return fields;

            var comments = ReadComments(document.RootElement, language);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.StartsWith('_'))
                    continue;   // _comments 之类的元数据不是配置项

                var field = new MobileConfigField
                {
                    Name = property.Name,
                    Comment = comments.GetValueOrDefault(property.Name, ""),
                };

                switch (property.Value.ValueKind)
                {
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        field.Kind = "bool";
                        field.BoolValue = property.Value.GetBoolean();
                        break;
                    case JsonValueKind.Number:
                        field.Kind = "number";
                        field.NumberValue = property.Value.GetDouble();
                        field.StringValue = property.Value.GetRawText();
                        break;
                    case JsonValueKind.String:
                        field.Kind = "string";
                        field.StringValue = property.Value.GetString() ?? "";
                        break;
                    default:
                        field.Kind = "other";
                        break;
                }

                fields.Add(field);
            }
        }

        return fields;
    }

    /// <summary>
    /// 把某个顶层标量字段的值换成新值，返回新文本。只替换那个值 token 的字节区间，
    /// 其余字符（注释、缩进、换行、其他字段的写法）一个都不动。
    /// </summary>
    public static string SetScalar(string text, string key, string kind, string value)
    {
        var span = FindValueSpan(text, key);
        var literal = kind switch
        {
            "bool" => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false",
            "number" => NormalizeNumber(value),
            "string" => Quote(value),
            _ => throw new InvalidOperationException($"字段 {key} 不是标量（{kind}），只能整段文本编辑"),
        };

        return string.Concat(text.AsSpan(0, span.Start), literal, text.AsSpan(span.End));
    }

    /// <summary>去掉 <c>//</c> 与 <c>/* */</c> 注释（保留字符串里的内容），只用于解析，不用来写回。</summary>
    public static string StripComments(string text)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '"')
            {
                var end = SkipString(text, i);
                sb.Append(text, i, end - i);
                i = end;
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i = SkipLineComment(text, i);
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i = SkipBlockComment(text, i);
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static Dictionary<string, string> ReadComments(JsonElement root, string language)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!root.TryGetProperty("_comments", out var comments) || comments.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var entry in comments.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object)
                continue;
            foreach (var candidate in new[] { language, "zh-Hans", "en" })
            {
                if (entry.Value.TryGetProperty(candidate, out var text) && text.ValueKind == JsonValueKind.String)
                {
                    result[entry.Name] = text.GetString() ?? "";
                    break;
                }
            }
        }

        return result;
    }

    private static (int Start, int End) FindValueSpan(string text, string key)
    {
        var depth = 0;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i = SkipLineComment(text, i);
                continue;
            }

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i = SkipBlockComment(text, i);
                continue;
            }

            if (c == '"')
            {
                var after = SkipString(text, i);
                var decoded = DecodeString(text, i, after);

                if (depth == 1 && string.Equals(decoded, key, StringComparison.Ordinal))
                {
                    var j = after;
                    SkipWhitespaceAndComments(text, ref j);
                    if (j < text.Length && text[j] == ':')
                    {
                        j++;
                        SkipWhitespaceAndComments(text, ref j);
                        return (j, FindValueEnd(text, j));
                    }
                }

                i = after;
                continue;
            }

            if (c is '{' or '[')
                depth++;
            else if (c is '}' or ']')
            {
                depth--;
                if (depth <= 0)
                    break;
            }

            i++;
        }

        throw new InvalidOperationException($"config.json 里找不到顶层字段 {key}");
    }

    private static int FindValueEnd(string text, int start)
    {
        if (start >= text.Length)
            throw new InvalidOperationException("config.json 结构不完整：字段值缺失");

        if (text[start] == '"')
            return SkipString(text, start);

        var depth = 0;
        var i = start;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '/'
                && i + 1 < text.Length
                && (text[i + 1] == '/' || text[i + 1] == '*'))
                break;

            if (c == '"')
            {
                i = SkipString(text, i);
                continue;
            }

            if (c is '{' or '[')
                depth++;
            else if (c is '}' or ']')
            {
                if (depth == 0)
                    return i;
                depth--;
            }
            else if (c == ',' && depth == 0)
                return i;

            i++;
        }

        return i;
    }

    private static void SkipWhitespaceAndComments(string text, ref int i)
    {
        while (i < text.Length)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
                continue;
            }

            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                i = SkipLineComment(text, i);
                continue;
            }

            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i = SkipBlockComment(text, i);
                continue;
            }

            break;
        }
    }

    private static int SkipLineComment(string text, int i)
    {
        while (i < text.Length && text[i] != '\n')
            i++;
        return i;
    }

    private static int SkipBlockComment(string text, int i)
    {
        i += 2;
        while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
            i++;
        return Math.Min(text.Length, i + 2);
    }

    private static int SkipString(string text, int start)
    {
        var i = start + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (text[i] == '"')
                return i + 1;
            i++;
        }

        return text.Length;
    }

    private static string DecodeString(string text, int start, int end)
    {
        var raw = text[(start)..Math.Max(start, end)];
        try
        {
            using var document = JsonDocument.Parse(raw.StartsWith('"') ? raw : "\"" + raw + "\"");
            return document.RootElement.GetString() ?? "";
        }
        catch (JsonException)
        {
            return raw.Trim('"');
        }
    }

    private static string NormalizeNumber(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0 || !double.TryParse(trimmed, out _))
            throw new InvalidOperationException($"不是合法数字：{value}");
        return trimmed;   // 原样写回，不把 1 改成 1.0
    }

    // 默认序列化会把中文转成 \uXXXX —— config.json 是 UTF-8 明文，必须原样写中文
    private static readonly JsonSerializerOptions QuoteOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string Quote(string value) => JsonSerializer.Serialize(value, QuoteOptions);
}
