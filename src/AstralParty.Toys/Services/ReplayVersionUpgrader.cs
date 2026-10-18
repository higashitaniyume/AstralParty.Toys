using System.Buffers.Binary;
using System.Text;

namespace AstralParty.Toys.Services;

/// <summary>
/// 旧回放「版本升级」：只改 1016 结算帧里的 <c>GameFinishS2C.Version</c> 字符串，帧流和其他字节原样保留。
///
/// 游戏判断回放能不能播用的是 <c>BattleShortRecord.IsValidVersion()</c>，严格比较
/// <c>APP_VERSION-RES_VERSION</c>。旧版本的帧格式和协议可能仍然完全兼容，只是这个字符串不相等，
/// 所以升级只需要改这一个 protobuf 字段，不需要重新序列化整条消息。
/// </summary>
internal static class ReplayVersionUpgrader
{
    private const int CmdGameFinish = 1016;
    private const int VersionFieldNumber = 12;
    private const int FrameHeaderLength = 6;

    /// <summary>读出最后一个非空 1016 的 Version；没有结算帧时返回 false。</summary>
    public static bool TryReadVersion(byte[] bytes, out string version, out string error)
    {
        version = "";
        error = "";
        var found = false;
        var offset = 0;
        while (offset < bytes.Length)
        {
            if (!TryReadFrame(bytes, ref offset, out var cmdId, out var payload, out error)) return false;
            if (cmdId != CmdGameFinish || payload.Length == 0) continue;
            if (!TryReadVersionField(payload, out version, out error)) return false;
            found = true;
        }

        if (!found)
        {
            error = "回放里没有可读取的 1016 结算帧。";
            return false;
        }
        return true;
    }

    /// <summary>把所有非空 1016 结算帧的 Version 改成 <paramref name="targetVersion"/>。</summary>
    public static bool TryUpgrade(byte[] bytes, string targetVersion, out byte[] upgraded, out string oldVersion, out string error)
    {
        upgraded = [];
        oldVersion = "";
        error = "";

        if (string.IsNullOrWhiteSpace(targetVersion))
        {
            error = "目标版本为空。";
            return false;
        }

        var targetBytes = Encoding.UTF8.GetBytes(targetVersion);
        using var output = new MemoryStream(bytes.Length + 16);
        var header = new byte[FrameHeaderLength];
        var offset = 0;
        var patched = 0;

        while (offset < bytes.Length)
        {
            if (!TryReadFrame(bytes, ref offset, out var cmdId, out var payload, out error)) return false;

            if (cmdId == CmdGameFinish && payload.Length > 0)
            {
                if (!TryPatchVersionField(payload, targetBytes, out var patchedPayload, out var frameVersion, out error))
                    return false;
                payload = patchedPayload;
                oldVersion = frameVersion;
                patched++;
            }

            Span<byte> headerSpan = header;
            BinaryPrimitives.WriteInt16BigEndian(headerSpan, (short)cmdId);
            BinaryPrimitives.WriteInt32BigEndian(headerSpan[2..], payload.Length);
            output.Write(headerSpan);
            output.Write(payload);
        }

        if (patched == 0)
        {
            error = "回放里没有可升级的 1016 结算帧。";
            return false;
        }

        upgraded = output.ToArray();
        return true;
    }

    private static bool TryReadFrame(byte[] bytes, ref int offset, out int cmdId, out byte[] payload, out string error)
    {
        cmdId = 0;
        payload = [];
        error = "";

        if (bytes.Length - offset < FrameHeaderLength)
        {
            error = $"回放帧头被截断（offset={offset}）。";
            return false;
        }

        cmdId = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(offset, 2));
        var payloadLength = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset + 2, 4));
        offset += FrameHeaderLength;
        if (payloadLength < 0 || payloadLength > bytes.Length - offset)
        {
            error = $"回放帧负载长度非法（cmdId={cmdId}, length={payloadLength}, remaining={bytes.Length - offset}）。";
            return false;
        }

        payload = bytes.AsSpan(offset, payloadLength).ToArray();
        offset += payloadLength;
        return true;
    }

    /// <summary>读取顶层 field 12（string）的值。</summary>
    private static bool TryReadVersionField(byte[] payload, out string version, out string error)
    {
        version = "";
        error = "";
        var offset = 0;
        var found = false;

        while (offset < payload.Length)
        {
            if (!TryReadKey(payload, ref offset, out var fieldNumber, out var wireType, out error)) return false;
            if (!TrySkipValue(payload, ref offset, wireType, out var valueStart, out var valueLength, out error)) return false;

            if (fieldNumber == VersionFieldNumber)
            {
                if (wireType != 2)
                {
                    error = $"1016.Version 的 wire type 不是 string（实际 {wireType}）。";
                    return false;
                }
                version = Encoding.UTF8.GetString(payload, valueStart, valueLength);
                found = true;
            }
        }

        if (!found)
        {
            error = "1016 结算帧里没有 Version 字段。";
            return false;
        }
        return true;
    }

    /// <summary>复制 payload，只替换 field 12 的字符串值，其它字段（包括未知字段）逐字节保留。</summary>
    private static bool TryPatchVersionField(byte[] payload, byte[] targetBytes, out byte[] patched, out string oldVersion, out string error)
    {
        patched = [];
        oldVersion = "";
        error = "";
        using var output = new MemoryStream(payload.Length + 8);
        var offset = 0;
        var found = false;

        while (offset < payload.Length)
        {
            var fieldStart = offset;
            if (!TryReadKey(payload, ref offset, out var fieldNumber, out var wireType, out error)) return false;

            if (wireType == 2)
            {
                var lengthStart = offset;
                if (!TryReadVarint(payload, ref offset, out var lengthValue, out error)) return false;
                if (lengthValue > (ulong)int.MaxValue || lengthValue > (ulong)(payload.Length - offset))
                {
                    error = "1016.Version 字段长度越界。";
                    return false;
                }

                var valueStart = offset;
                var valueLength = (int)lengthValue;
                if (fieldNumber == VersionFieldNumber)
                {
                    oldVersion = Encoding.UTF8.GetString(payload, valueStart, valueLength);
                    output.Write(payload, fieldStart, lengthStart - fieldStart);
                    WriteVarint(output, (ulong)targetBytes.Length);
                    output.Write(targetBytes);
                    found = true;
                }
                else
                {
                    output.Write(payload, fieldStart, valueStart - fieldStart + valueLength);
                }
                offset += valueLength;
                continue;
            }

            if (!TrySkipValue(payload, ref offset, wireType, out _, out _, out error)) return false;
            output.Write(payload, fieldStart, offset - fieldStart);
        }

        if (!found)
        {
            error = "1016 结算帧里没有 Version 字段。";
            return false;
        }

        patched = output.ToArray();
        return true;
    }

    private static bool TryReadKey(byte[] payload, ref int offset, out int fieldNumber, out int wireType, out string error)
    {
        fieldNumber = 0;
        wireType = 0;
        error = "";
        if (!TryReadVarint(payload, ref offset, out var key, out error)) return false;
        fieldNumber = (int)(key >> 3);
        wireType = (int)(key & 7);
        if (fieldNumber <= 0)
        {
            error = "protobuf 字段号非法。";
            return false;
        }
        return true;
    }

    /// <summary>跳过当前字段的值，并给出 wire type 2 的字符串/字节区间。</summary>
    private static bool TrySkipValue(
        byte[] payload,
        ref int offset,
        int wireType,
        out int valueStart,
        out int valueLength,
        out string error)
    {
        valueStart = 0;
        valueLength = 0;
        error = "";

        switch (wireType)
        {
            case 0:
                return TryReadVarint(payload, ref offset, out _, out error);
            case 1:
                if (payload.Length - offset < 8) { error = "protobuf fixed64 字段被截断。"; return false; }
                offset += 8;
                return true;
            case 2:
                if (!TryReadVarint(payload, ref offset, out var lengthValue, out error)) return false;
                if (lengthValue > (ulong)int.MaxValue || lengthValue > (ulong)(payload.Length - offset))
                {
                    error = "protobuf 长度字段越界。";
                    return false;
                }
                valueStart = offset;
                valueLength = (int)lengthValue;
                offset += valueLength;
                return true;
            case 5:
                if (payload.Length - offset < 4) { error = "protobuf fixed32 字段被截断。"; return false; }
                offset += 4;
                return true;
            default:
                error = $"不支持的 protobuf wire type {wireType}（不支持 group）。";
                return false;
        }
    }

    private static bool TryReadVarint(byte[] data, ref int offset, out ulong value, out string error)
    {
        value = 0;
        error = "";
        var shift = 0;
        while (true)
        {
            if (offset >= data.Length)
            {
                error = "protobuf varint 被截断。";
                return false;
            }
            var current = data[offset++];
            if (shift == 63 && current > 1)
            {
                error = "protobuf varint 溢出。";
                return false;
            }
            value |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0) return true;
            shift += 7;
            if (shift >= 64)
            {
                error = "protobuf varint 过长。";
                return false;
            }
        }
    }

    private static void WriteVarint(Stream output, ulong value)
    {
        while (value >= 0x80)
        {
            output.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }
        output.WriteByte((byte)(value & 0x7F));
    }
}
