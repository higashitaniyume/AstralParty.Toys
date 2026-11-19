using System.Text.Json;
using System.Text.Json.Nodes;

namespace AstralParty.Toys.LinuxHost;

internal static partial class LinuxLoader
{
    private static readonly JsonDocumentOptions JsonWithComments = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };
    private static readonly JsonSerializerOptions SidecarFormat = new() { WriteIndented = true };

    private static Version ValidateDefaultConfig(byte[] bytes)
    {
        using var config = ReadJsonObject(bytes, LoaderConfig);
        var root = config.RootElement;
        if (!TryReleaseVersion(ReadString(root, "sdkVersion"), out var sdk) || ReadString(root, "profile") != "" ||
            !root.TryGetProperty("speedhackBaseSpeed", out var speed) || speed.ValueKind != JsonValueKind.Number ||
            speed.GetDouble() != 1 || !IsOff("steamBypassEnabled") || !IsOff("steamBypassRestartCheck") ||
            !IsOff("steamBypassMatchmaking") || !IsOff("steamBypassLobbyHasValue") || !IsOff("steamBypassLobbyMethods") ||
            !IsOff("steamBypassLobbyQuery") || !IsOff("tapTapBypassEnabled") || !IsOff("tapTapBypassRestartCheck") ||
            !IsOff("tapTapBypassLogin"))
            throw new InvalidDataException("发布包的默认配置不适用于正常 Steam 启动，未安装加载器。");
        return sdk!;

        bool IsOff(string property) => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.False;
    }

    private static byte[]? RaiseSdkVersion(byte[] bytes, Version target)
    {
        using var config = ReadJsonObject(bytes, LoaderConfig);
        if (!config.RootElement.TryGetProperty("sdkVersion", out var field)) return null;
        if (field.ValueKind != JsonValueKind.String || !Version.TryParse(field.GetString(), out var installed))
            throw new InvalidDataException("加载器配置中的 sdkVersion 无效，未改动文件。");
        if (installed >= target) return null;

        // Replace only the top-level SDK value in the original UTF-8. This keeps
        // comments, BOM, line endings, whitespace, and unrelated user settings intact.
        var bom = BomLength(bytes);
        var reader = new Utf8JsonReader(bytes.AsSpan(bom), new JsonReaderOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip,
        });
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1 ||
                !reader.ValueTextEquals("sdkVersion"u8)) continue;
            reader.Read(); // The parsed document above established that this value is a string.
            var start = bom + (int)reader.TokenStartIndex;
            var end = bom + (int)reader.BytesConsumed;
            var replacement = JsonSerializer.SerializeToUtf8Bytes(target.ToString());
            var updated = new byte[bytes.Length - (end - start) + replacement.Length];
            bytes.AsSpan(0, start).CopyTo(updated);
            replacement.CopyTo(updated.AsSpan(start));
            bytes.AsSpan(end).CopyTo(updated.AsSpan(start + replacement.Length));
            return updated;
        }
        throw new InvalidDataException("无法在加载器配置中定位 sdkVersion，未改动文件。");
    }

    private static byte[]? MergeSidecar(byte[] existingBytes, byte[] releaseBytes, string mod)
    {
        using var existing = ReadJsonObject(existingBytes, mod + ".json");
        using var released = ReadJsonObject(releaseBytes, mod + ".json");
        if (ReadString(existing.RootElement, "id") != mod || ReadString(released.RootElement, "id") != mod)
            throw new InvalidDataException($"模组描述文件与目录不符，未覆盖：{mod}");
        if (existing.RootElement.TryGetProperty("enabled", out var enabled) &&
            enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException($"模组描述文件中的 enabled 无效，未覆盖：{mod}");

        var installed = JsonObject.Create(existing.RootElement)!;
        var updated = JsonObject.Create(released.RootElement)!;
        var hasUserChanges = false;
        foreach (var (key, value) in installed)
        {
            if (key != "enabled" && updated.ContainsKey(key)) continue;
            if (updated.TryGetPropertyValue(key, out var defaultValue) && JsonNode.DeepEquals(value, defaultValue)) continue;
            updated[key] = value?.DeepClone();
            hasUserChanges = true;
        }
        // Keep a hand-edited sidecar byte-for-byte if only the enabled/settings values differ.
        if (JsonNode.DeepEquals(installed, updated)) return null;
        return hasUserChanges ? JsonSerializer.SerializeToUtf8Bytes(updated, SidecarFormat) : releaseBytes;
    }

    private static JsonDocument ReadJsonObject(byte[] bytes, string label)
    {
        JsonDocument result;
        try
        {
            result = JsonDocument.Parse(bytes.AsMemory(BomLength(bytes)), JsonWithComments);
        }
        catch (JsonException error)
        {
            throw new InvalidDataException($"无法读取 JSON 文件，未改动：{label}", error);
        }
        if (result.RootElement.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (result.RootElement.EnumerateObject().All(property => keys.Add(property.Name))) return result;
        }
        result.Dispose();
        throw new InvalidDataException($"JSON 文件含有重复字段或不是对象，未改动：{label}");
    }

    private static int BomLength(ReadOnlySpan<byte> bytes) => bytes.StartsWith("\uFEFF"u8) ? 3 : 0;
}
