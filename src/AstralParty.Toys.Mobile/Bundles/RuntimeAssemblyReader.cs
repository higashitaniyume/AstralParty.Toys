using System.Security.Cryptography;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace AstralParty.Toys.Mobile.Bundles;

/// <summary>
/// Reads TextAssets from a UnityFS code bundle. Only the raw object-data path is used
/// (m_Name + 4-byte alignment + m_Script + alignment) — never the UTF-8 string path.
/// </summary>
public static class RuntimeAssemblyReader
{
    public static RuntimeAssemblyInfo Read(byte[] bundleBytes, string assetName)
    {
        var assets = ReadAllTextAssets(bundleBytes, out _);
        // 这里必须是 TextAsset 的条数，不是包里 SerializedFile（CAB）的个数：调用方把它当「资产数」
        // 显示到设备页上，早先误传 fileCount 时一个 18 资产的包会显示成 1。
        return new RuntimeAssemblyInfo(assets.Count, assets.GetValueOrDefault(assetName));
    }

    public static Dictionary<string, byte[]> ReadAllTextAssets(byte[] bundleBytes, out int fileCount)
    {
        var manager = new AssetsManager();
        try
        {
            using var stream = new MemoryStream(bundleBytes, writable: false);
            var bundle = manager.LoadBundleFile(stream, "code.bundle");
            if (bundle?.file?.BlockAndDirInfo is null)
                throw new InvalidDataException("代码包不是有效的 UnityFS 包");

            fileCount = 0;
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            for (var i = 0; i < bundle.file.BlockAndDirInfo.DirectoryInfos.Count; i++)
            {
                if (!bundle.file.IsAssetsFile(i))
                    continue;
                var assets = manager.LoadAssetsFileFromBundle(bundle, i);
                if (assets is null)
                    continue;
                fileCount++;

                foreach (var info in assets.file.AssetInfos)
                {
                    var field = manager.GetBaseField(assets, info);
                    if (field is null || field.IsDummy || field["m_Name"].IsDummy || field["m_Script"].IsDummy)
                        continue;
                    if (field["m_Script"].AsByteArray is null)
                        continue;

                    var (name, script) = ParseTextAsset(ReadRawData(assets, info));
                    result[name] = script;
                }
            }

            return result;
        }
        finally
        {
            manager.UnloadAll();
        }
    }

    /// <summary>SHA-256 of every TextAsset, for the "all other assets unchanged" check.</summary>
    public static Dictionary<string, string> Fingerprints(byte[] bundleBytes, out int fileCount)
    {
        var all = ReadAllTextAssets(bundleBytes, out fileCount);
        return all.ToDictionary(
            pair => pair.Key,
            pair => Convert.ToHexString(SHA256.HashData(pair.Value)).ToLowerInvariant(),
            StringComparer.Ordinal);
    }

    private static byte[] ReadRawData(AssetsFileInstance assets, AssetFileInfo info)
    {
        var start = info.GetAbsoluteByteOffset(assets.file);
        var size = checked((int)info.ByteSize);
        assets.file.Reader.Position = start;
        return assets.file.Reader.ReadBytes(size);
    }

    internal static (string Name, byte[] Script) ParseTextAsset(byte[] raw)
    {
        var offset = 0;
        var nameLength = ReadInt32(raw, ref offset);
        if (nameLength < 0 || offset + nameLength > raw.Length)
            throw new InvalidDataException($"m_Name 长度非法：{nameLength}");
        var name = Encoding.UTF8.GetString(raw, offset, nameLength);
        offset = Align4(offset + nameLength);

        var scriptLength = ReadInt32(raw, ref offset);
        if (scriptLength < 0 || offset + scriptLength > raw.Length)
            throw new InvalidDataException($"m_Script 长度非法：{scriptLength}（name={name}）");
        var script = new byte[scriptLength];
        Buffer.BlockCopy(raw, offset, script, 0, scriptLength);
        return (name, script);
    }

    internal static int ReadInt32(byte[] data, ref int offset)
    {
        var value = BitConverter.ToInt32(data, offset);
        offset += 4;
        return value;
    }

    internal static int Align4(int value) => (value + 3) & ~3;
}

public sealed record RuntimeAssemblyInfo(int AssetCount, byte[]? Assembly);
