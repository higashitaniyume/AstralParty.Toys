using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstralParty.Toys.Services.Skins;

/// <summary>
/// Wwise SoundBank（.bnk）的最小解析：只读试听原版音频需要的三段。
/// <list type="bullet">
/// <item><c>BKHD</c>：版本号（本游戏是 145）。</item>
/// <item><c>DIDX</c> + <c>DATA</c>：内嵌的 .wem，每条 12 字节（ID、相对 DATA 的偏移、长度）。</item>
/// <item><c>HIRC</c>：事件 → 动作 → 容器 → 声音的层级，用来从事件 ID 找到 wem。</item>
/// </list>
/// </summary>
internal sealed class WwiseBank
{
    public const byte SoundType = 2;
    public const byte ActionType = 3;
    public const byte EventType = 4;

    public uint Version { get; private set; }

    /// <summary>wem ID → 在 bnk 文件里的绝对偏移和长度。</summary>
    public Dictionary<uint, (int Offset, int Size)> Media { get; } = new();

    /// <summary>HIRC 对象：ID → (类型, 负载)。负载不含类型、长度和 ID 本身。</summary>
    public Dictionary<uint, (byte Type, byte[] Payload)> Objects { get; } = new();

    public static WwiseBank Parse(ReadOnlySpan<byte> data)
    {
        var bank = new WwiseBank();
        int didxStart = -1, didxSize = 0, dataStart = -1;
        int pos = 0;
        while (pos + 8 <= data.Length)
        {
            var tag = data.Slice(pos, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(pos + 4, 4));
            var body = pos + 8;
            if (size < 0 || body + size > data.Length) throw new InvalidDataException("声音库数据不完整");

            if (tag.SequenceEqual("BKHD"u8) && size >= 4) bank.Version = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(body, 4));
            else if (tag.SequenceEqual("DIDX"u8)) { didxStart = body; didxSize = size; }
            else if (tag.SequenceEqual("DATA"u8)) dataStart = body;
            else if (tag.SequenceEqual("HIRC"u8)) ReadHirc(data.Slice(body, size), bank.Objects);

            pos = body + size;
        }

        if (didxStart >= 0 && dataStart >= 0)
        {
            for (int p = didxStart; p + 12 <= didxStart + didxSize; p += 12)
            {
                var id = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(p, 4));
                var offset = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(p + 4, 4));
                var length = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(p + 8, 4));
                if (offset < 0 || length <= 0 || dataStart + (long)offset + length > data.Length) continue;
                bank.Media[id] = (dataStart + offset, length);
            }
        }
        return bank;
    }

    private static void ReadHirc(ReadOnlySpan<byte> hirc, Dictionary<uint, (byte, byte[])> objects)
    {
        if (hirc.Length < 4) return;
        var count = BinaryPrimitives.ReadUInt32LittleEndian(hirc);
        int p = 4;
        for (uint i = 0; i < count && p + 9 <= hirc.Length; i++)
        {
            var type = hirc[p];
            var size = BinaryPrimitives.ReadInt32LittleEndian(hirc.Slice(p + 1, 4));
            if (size < 4 || p + 5 + size > hirc.Length) break;
            var id = BinaryPrimitives.ReadUInt32LittleEndian(hirc.Slice(p + 5, 4));
            objects[id] = (type, hirc.Slice(p + 9, size - 4).ToArray());
            p += 5 + size;
        }
    }
}

/// <summary>
/// 从事件 ID 顺着 HIRC 找到它会播放的 wem。对象可以分散在不同的声音库里
/// （例如事件在通用库、录音在语言库），所以传入的是所有库合并后的对象表。
/// </summary>
internal static class WwiseEventResolver
{
    // 会出现在播放链路上的节点：声音、随机/顺序容器、切换容器、Actor-Mixer、混合容器、音乐类节点
    private static readonly HashSet<byte> NodeTypes = [2, 5, 6, 7, 9, 10, 11, 12, 13];
    private const byte MusicTrackType = 11;

    /// <summary>返回事件会播放到的、在 <paramref name="hasMedia"/> 里存在的 wem ID（按找到的先后顺序，不重复）。事件不存在时返回 null。</summary>
    public static List<uint>? Resolve(uint eventId, IReadOnlyDictionary<uint, (byte Type, byte[] Payload)> objects, Func<uint, bool> hasMedia)
    {
        if (!objects.TryGetValue(eventId, out var ev) || ev.Type != WwiseBank.EventType) return null;

        var result = new List<uint>();
        var seen = new HashSet<uint>();
        var stack = new Stack<uint>();

        foreach (var actionId in ReadEventActions(ev.Payload))
        {
            if (objects.TryGetValue(actionId, out var action) && action.Type == WwiseBank.ActionType && action.Payload.Length >= 6)
                stack.Push(BinaryPrimitives.ReadUInt32LittleEndian(action.Payload.AsSpan(2, 4)));
        }

        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!seen.Add(id) || !objects.TryGetValue(id, out var node)) continue;

            if (node.Type == WwiseBank.SoundType)
            {
                // AkBankSourceData：插件 ID(4) + 流类型(1) + 源 ID(4)
                if (node.Payload.Length >= 9)
                {
                    var source = BinaryPrimitives.ReadUInt32LittleEndian(node.Payload.AsSpan(5, 4));
                    if (hasMedia(source) && !result.Contains(source)) result.Add(source);
                }
            }
            else if (node.Type == MusicTrackType)
            {
                // 音乐轨把音源直接写在自己里面（数量 + 若干 AkBankSourceData），不是子节点；逐个位置找存在的 wem ID
                for (int i = 0; i + 4 <= node.Payload.Length; i++)
                {
                    var source = BinaryPrimitives.ReadUInt32LittleEndian(node.Payload.AsSpan(i, 4));
                    if (hasMedia(source) && !result.Contains(source)) result.Add(source);
                }
            }
            else if (NodeTypes.Contains(node.Type))
            {
                // 子节点列表是「数量 + 若干 ID」，不同版本前面的字段长短不一，这里取最靠后的那段
                var children = FindChildren(node.Payload, objects);
                for (int i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
            }
        }
        return result;
    }

    private static IEnumerable<uint> ReadEventActions(byte[] payload)
    {
        // 动作数量是变长整数（每字节 7 位，高位表示还有下一字节）
        int p = 0;
        uint count = 0;
        while (p < payload.Length)
        {
            var b = payload[p++];
            count = (count << 7) | (uint)(b & 0x7F);
            if ((b & 0x80) == 0) break;
        }
        for (uint i = 0; i < count && p + 4 <= payload.Length; i++, p += 4)
            yield return BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(p, 4));
    }

    private static List<uint> FindChildren(byte[] payload, IReadOnlyDictionary<uint, (byte Type, byte[] Payload)> objects)
    {
        List<uint> best = [];
        for (int i = 0; i + 4 <= payload.Length; i++)
        {
            var n = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(i, 4));
            if (n < 1 || n > 512 || i + 4 + n * 4L > payload.Length) continue;
            var ids = new List<uint>((int)n);
            for (int k = 0; k < n; k++)
            {
                var id = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(i + 4 + k * 4, 4));
                if (!objects.TryGetValue(id, out var child) || !NodeTypes.Contains(child.Type)) break;
                ids.Add(id);
            }
            if (ids.Count == n) best = ids;
        }
        return best;
    }
}
