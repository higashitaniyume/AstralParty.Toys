using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace AstralParty.Toys.Mobile.Bundles;

/// <summary>
/// The only TextAsset write-back path that was verified byte-for-byte against the iOS (UnityPy)
/// output in the M0 spike: splice the raw object data inside the SerializedFile, write the bundle
/// uncompressed, then re-pack with the original compression. Replacer/SetNewData paths do not work
/// with AssetsTools.NET 3.0.5 and must not be "simplified" back in.
/// </summary>
public static class BundleSplicer
{
    public static SpliceResult Splice(
        byte[] originalBundle, string assetName, byte[] replacement, string outputPath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempInput = outputPath + ".orig";
        File.WriteAllBytes(tempInput, originalBundle);
        var manager = new AssetsManager();
        try
        {
            var bundle = manager.LoadBundleFile(tempInput);
            if (bundle?.file?.BlockAndDirInfo is null)
                throw new InvalidDataException("代码包不是有效的 UnityFS 包");

            var replaced = false;
            for (var i = 0; i < bundle.file.BlockAndDirInfo.DirectoryInfos.Count && !replaced; i++)
            {
                if (!bundle.file.IsAssetsFile(i))
                    continue;
                var assets = manager.LoadAssetsFileFromBundle(bundle, i);
                if (assets is null)
                    continue;

                var backing = ReadSerializedFileBytes(bundle, i);
                foreach (var info in assets.file.AssetInfos)
                {
                    var field = manager.GetBaseField(assets, info);
                    if (field is null || field.IsDummy || field["m_Name"].IsDummy || field["m_Script"].IsDummy)
                        continue;
                    if (field["m_Script"].AsByteArray is null)
                        continue;

                    var raw = ReadRawData(assets, info);
                    var (name, _) = RuntimeAssemblyReader.ParseTextAsset(raw);
                    if (!string.Equals(name, assetName, StringComparison.Ordinal))
                        continue;

                    var patchRaw = ReplaceScript(raw, replacement);
                    var newSerialized = (byte[])backing.Clone();
                    var absolute = checked((int)info.GetAbsoluteByteOffset(assets.file));
                    var delta = patchRaw.Length - raw.Length;
                    if (delta == 0)
                    {
                        Buffer.BlockCopy(patchRaw, 0, newSerialized, absolute, patchRaw.Length);
                    }
                    else
                    {
                        newSerialized = new byte[backing.Length + delta];
                        Buffer.BlockCopy(backing, 0, newSerialized, 0, absolute);
                        Buffer.BlockCopy(patchRaw, 0, newSerialized, absolute, patchRaw.Length);
                        Buffer.BlockCopy(
                            backing, absolute + raw.Length,
                            newSerialized, absolute + patchRaw.Length,
                            backing.Length - absolute - raw.Length);
                        RewriteAssetTable(newSerialized, backing, assets.file, info.PathId, delta);
                    }

                    var (verifyName, verifyScript) = RuntimeAssemblyReader.ParseTextAsset(
                        newSerialized.AsSpan(absolute, patchRaw.Length).ToArray());
                    if (!string.Equals(verifyName, assetName, StringComparison.Ordinal) ||
                        !verifyScript.AsSpan().SequenceEqual(replacement))
                        throw new InvalidDataException("换回自检失败：目标资产内容与替换文件不一致");

                    var dir = bundle.file.BlockAndDirInfo.DirectoryInfos[i];
                    dir.Replacer = new ContentReplacerFromBuffer(newSerialized);
                    dir.DecompressedSize = newSerialized.Length;
                    replaced = true;
                    break;
                }
            }

            if (!replaced)
                throw new InvalidDataException($"代码包里找不到资产 {assetName}");

            var written = Write(bundle, outputPath);
            return new SpliceResult(outputPath, written);
        }
        finally
        {
            manager.UnloadAll();
            if (File.Exists(tempInput))
                File.Delete(tempInput);
        }
    }

    private static byte[] ReplaceScript(byte[] originalRaw, byte[] script)
    {
        var offset = 0;
        var nameLength = RuntimeAssemblyReader.ReadInt32(originalRaw, ref offset);
        if (nameLength < 0 || offset + nameLength > originalRaw.Length)
            throw new InvalidDataException($"m_Name 长度非法：{nameLength}");
        var prefixLength = RuntimeAssemblyReader.Align4(offset + nameLength);
        var buffer = new byte[RuntimeAssemblyReader.Align4(prefixLength + 4 + script.Length)];
        Buffer.BlockCopy(originalRaw, 0, buffer, 0, prefixLength);
        var write = prefixLength;
        BitConverter.TryWriteBytes(buffer.AsSpan(write, 4), script.Length);
        write += 4;
        Buffer.BlockCopy(script, 0, buffer, write, script.Length);
        return buffer;
    }

    private static byte[] ReadRawData(AssetsFileInstance assets, AssetFileInfo info)
    {
        assets.file.Reader.Position = info.GetAbsoluteByteOffset(assets.file);
        return assets.file.Reader.ReadBytes(checked((int)info.ByteSize));
    }

    private static byte[] ReadSerializedFileBytes(BundleFileInstance bundle, int fileIndex)
    {
        bundle.file.GetFileRange(fileIndex, out var start, out var length);
        bundle.file.DataReader.Position = start;
        return bundle.file.DataReader.ReadBytes(checked((int)length));
    }

    private static (int Start, int Stride) LocateAssetTable(byte[] serialized, AssetsFile file)
    {
        var reader = new AssetsFileReader(new MemoryStream(serialized));
        var header = new AssetsFileHeader();
        header.Read(reader);
        var metadata = new AssetsFileMetadata();
        metadata.Read(reader, header);
        var ids = metadata.AssetInfos.Select(a => a.PathId).ToArray();
        if (ids.Length == 0)
            throw new InvalidDataException("SerializedFile 里没有资产表");

        for (var stride = 12; stride <= 32; stride++)
        {
            var start = IndexOfPathId(serialized, ids[0]);
            while (start >= 0)
            {
                if (start + stride * ids.Length <= serialized.Length &&
                    Enumerable.Range(0, ids.Length).All(k =>
                        BitConverter.ToInt64(serialized, start + k * stride) == ids[k]))
                    return (start, stride);
                start = IndexOfPathId(serialized, ids[0], start + 1);
            }
        }

        throw new InvalidDataException("无法定位资产表（没有步长能让整张表对上 metadata）");
    }

    private static int IndexOfPathId(byte[] data, long pathId, int from = 0)
    {
        for (var i = from; i + 8 <= data.Length; i++)
            if (BitConverter.ToInt64(data, i) == pathId)
                return i;
        return -1;
    }

    private static void RewriteAssetTable(
        byte[] updated, byte[] original, AssetsFile file, long pathId, int delta)
    {
        const int fileSizeOffset = 24;
        const int dataOffsetOffset = 32;
        var (tableStart, stride) = LocateAssetTable(original, file);
        if (stride < 20)
            throw new InvalidDataException($"资产表步长 {stride} 不可信");

        var entries = new List<(long PathId, int OffsetField, int SizeField, uint Size, long RelOffset)>();
        for (var k = 0; k < file.AssetInfos.Count; k++)
        {
            var info = file.AssetInfos[k];
            var cursor = tableStart + k * stride;
            if (BitConverter.ToInt64(original, cursor) != info.PathId)
                throw new InvalidDataException($"资产表对账失败 @{cursor}（第 {k} 条 PathID）");
            var byteOffset = BitConverter.ToInt64(original, cursor + 8);
            var byteSize = BitConverter.ToUInt32(original, cursor + 16);
            if (byteOffset != info.ByteOffset || byteSize != info.ByteSize)
                throw new InvalidDataException($"资产表字段对账失败 @{cursor}（第 {k} 条）");
            entries.Add((info.PathId, cursor + 8, cursor + 16, byteSize, byteOffset));
        }

        var target = entries.FindIndex(e => e.PathId == pathId);
        if (target < 0)
            throw new InvalidDataException($"资产表里没有 PathID {pathId}");

        var dataSize = 0L;
        for (var k = 0; k < entries.Count; k++)
        {
            var (_, offsetField, sizeField, size, relOffset) = entries[k];
            var newRel = k > target ? relOffset + delta : relOffset;
            var newSize = k == target ? (uint)(size + delta) : size;
            BitConverter.TryWriteBytes(updated.AsSpan(offsetField, 8), newRel);
            BitConverter.TryWriteBytes(updated.AsSpan(sizeField, 4), newSize);
            dataSize = Math.Max(dataSize, newRel + newSize);
        }

        var dataOffset = ReadBigEndianInt64(original, dataOffsetOffset);
        var oldFileSize = ReadBigEndianInt64(original, fileSizeOffset);
        if (oldFileSize != dataOffset + dataSize - delta)
            throw new InvalidDataException("SerializedFile 头 FileSize 对不上，布局假设不成立");
        WriteBigEndianInt64(updated, fileSizeOffset, dataOffset + dataSize);
    }

    private static long ReadBigEndianInt64(byte[] data, int offset)
    {
        long value = 0;
        for (var i = 0; i < 8; i++)
            value = (value << 8) | data[offset + i];
        return value;
    }

    private static void WriteBigEndianInt64(byte[] data, int offset, long value)
    {
        for (var i = 7; i >= 0; i--)
        {
            data[offset + i] = (byte)(value & 0xFF);
            value >>= 8;
        }
    }

    private static long Write(BundleFileInstance bundle, string outputPath)
    {
        var originalCompression = bundle.file.GetCompressionType();
        var tempPath = outputPath + ".prepack";
        using (var temp = File.Create(tempPath))
            bundle.file.Write(new AssetsFileWriter(temp));
        try
        {
            var reloadManager = new AssetsManager();
            var reloaded = reloadManager.LoadBundleFile(tempPath);
            using var stream = File.Create(outputPath);
            reloaded.file.Pack(new AssetsFileWriter(stream), originalCompression, false, null);
            var length = stream.Length;
            reloadManager.UnloadAll();
            return length;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}

public sealed record SpliceResult(string Path, long Bytes);
