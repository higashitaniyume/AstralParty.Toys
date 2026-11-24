using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace AstralParty.Toys.Mobile.Bundles;

/// <summary>
/// Decodes the Addressables catalog binary strings just enough to locate the bundle that carries
/// a named asset. Ported from iOS/cesium_ios/catalog.py (same layout on iOS and Android).
/// </summary>
public sealed class AddressablesCatalog
{
    public const string RuntimeAssetKey = "AstralParty.Runtime.dll";

    public string BuildResultHash { get; }

    private readonly List<object?> _keys = [];
    private readonly Dictionary<int, int[]> _buckets = new();
    private readonly List<int[]> _entries = [];

    private AddressablesCatalog(string buildResultHash)
    {
        BuildResultHash = buildResultHash;
    }

    public static AddressablesCatalog Parse(ReadOnlySpan<byte> utf8Json)
    {
        using var doc = JsonDocument.Parse(utf8Json.ToArray());
        var root = doc.RootElement;
        var result = new AddressablesCatalog(GetString(root, "m_BuildResultHash") ?? "");

        var keyData = GetBytes(root, "m_KeyDataString");
        var bucketData = GetBytes(root, "m_BucketDataString");
        var entryData = GetBytes(root, "m_EntryDataString");

        var bucketCount = I32(bucketData, 0);
        var cursor = 4;
        for (var i = 0; i < bucketCount; i++)
        {
            var offset = I32(bucketData, cursor);
            cursor += 4;
            var entryCount = I32(bucketData, cursor);
            cursor += 4;
            var indexes = new int[Math.Max(entryCount, 0)];
            for (var j = 0; j < indexes.Length; j++)
            {
                indexes[j] = I32(bucketData, cursor);
                cursor += 4;
            }

            result._buckets[i] = indexes;
            if (offset >= 0 && offset < keyData.Length)
                result._keys.Add(ReadObject(keyData, offset));
            else
                result._keys.Add(null);
        }

        var entryCountTotal = I32(entryData, 0);
        cursor = 4;
        for (var i = 0; i < entryCountTotal; i++)
        {
            var entry = new int[7];
            for (var j = 0; j < entry.Length; j++)
            {
                entry[j] = I32(entryData, cursor);
                cursor += 4;
            }

            result._entries.Add(entry);
        }

        return result;
    }

    /// <summary>Returns the bundle file hash (file name without .bundle) carrying <paramref name="assetKey"/>.</summary>
    public string? BundleHashFor(string assetKey)
    {
        for (var i = 0; i < _keys.Count; i++)
        {
            if (!string.Equals(_keys[i] as string, assetKey, StringComparison.Ordinal))
                continue;
            if (!_buckets.TryGetValue(i, out var entryIndexes))
                continue;
            foreach (var entryIndex in entryIndexes)
            {
                if (entryIndex < 0 || entryIndex >= _entries.Count)
                    continue;
                var dependency = _entries[entryIndex][2];
                if (dependency < 0 || dependency >= _keys.Count)
                    continue;
                if (_keys[dependency] is string name &&
                    name.EndsWith(".bundle", StringComparison.Ordinal))
                    return name[..^".bundle".Length];
            }
        }

        return null;
    }

    /// <summary>Prefer catalog_catalogVersion.json, otherwise let the caller pick the newest one.</summary>
    public static string? PickCatalogName(IEnumerable<string> names, string appVersion)
    {
        var list = names
            .Where(n => n.StartsWith("catalog_", StringComparison.Ordinal) &&
                        n.EndsWith(".json", StringComparison.Ordinal))
            .ToList();
        if (list.Count == 0)
            return null;
        var preferred = $"catalog_{appVersion}.json";
        return list.Contains(preferred, StringComparer.Ordinal) ? preferred : null;
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static byte[] GetBytes(JsonElement root, string name)
    {
        var text = GetString(root, name);
        return string.IsNullOrEmpty(text) ? [] : Convert.FromBase64String(text);
    }

    private static int I32(byte[] data, int offset) =>
        offset + 4 <= data.Length ? BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4)) : 0;

    private static object? ReadObject(byte[] data, int offset)
    {
        if (offset < 0 || offset >= data.Length)
            return null;
        var kind = data[offset++];
        switch (kind)
        {
            case 0:
            {
                var length = I32(data, offset);
                var start = offset + 4;
                if (length < 0 || start + length > data.Length) return null;
                return Encoding.ASCII.GetString(data, start, length);
            }
            case 1:
            {
                var length = I32(data, offset);
                var start = offset + 4;
                if (length < 0 || start + length * 2 > data.Length) return null;
                return Encoding.Unicode.GetString(data, start, length * 2);
            }
            case 2:
                return offset + 2 <= data.Length
                    ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2))
                    : null;
            case 3:
                return offset + 4 <= data.Length
                    ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4))
                    : null;
            case 4:
                return I32(data, offset);
            case 5:
                return offset + 16 <= data.Length ? "Hash128:" + Convert.ToHexString(data.AsSpan(offset, 16)).ToLowerInvariant() : null;
            default:
                return null;
        }
    }
}
