using System.Text;
using Netimobiledevice.Afc;
using Netimobiledevice.Lockdown;

namespace AstralParty.Toys.Mobile.Channels;

/// <summary>
/// Self-implemented raw AFC operations. Netimobiledevice 2.5.2 has neither MakeDirectory/RenamePath
/// nor a shallow directory listing (LsDirectory walks the whole tree and calls GetFileInfo on every
/// entry — 20 s on the game's cache vs 5 ms for a single READ_DIR). Protocol details and device
/// results: docs/iOS容器-AFC自补-实测记录-2026-11-24.md.
/// </summary>
internal sealed class RawAfcClient
{
    private const string Magic = "CFA6LPAA";
    private const int HeaderSize = 40;
    private static readonly byte[] MagicBytes = Encoding.UTF8.GetBytes(Magic);

    private readonly ServiceConnection _service;
    private ulong _packetNumber;

    public RawAfcClient(ServiceConnection service)
    {
        _service = service;
    }

    /// <summary>mkdir -p semantics: an existing directory returns Success and the whole path is created.</summary>
    public Task<ulong> MakeDirectoryAsync(string path, CancellationToken cancellationToken = default) =>
        RequestStatusAsync(0x09, CString(path), cancellationToken);

    /// <summary>RENAME_PATH moves an entire tree; payload is old\0new\0.</summary>
    public Task<ulong> RenameAsync(string from, string to, CancellationToken cancellationToken = default) =>
        RequestStatusAsync(0x18, [.. CString(from), .. CString(to)], cancellationToken);

    public Task<ulong> RemovePathAsync(string path, CancellationToken cancellationToken = default) =>
        RequestStatusAsync(0x08, CString(path), cancellationToken);

    public async Task<IReadOnlyList<string>> ReadDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        var (operation, payload) = await ExchangeAsync(0x03, CString(path), cancellationToken).ConfigureAwait(false);
        if (operation != 0x02)
            throw new IOException($"READ_DIR 期望 DATA，收到 op=0x{operation:X2}");
        var names = Encoding.UTF8.GetString(payload).Split('\0').ToList();
        if (names.Count > 0 && names[^1].Length == 0)
            names.RemoveAt(names.Count - 1);
        names.RemoveAll(n => n is "." or "..");
        return names;
    }

    private static byte[] CString(string value) => [.. Encoding.UTF8.GetBytes(value), 0];

    private async Task<ulong> RequestStatusAsync(ulong operation, byte[] payload, CancellationToken cancellationToken)
    {
        var (replyOperation, body) = await ExchangeAsync(operation, payload, cancellationToken).ConfigureAwait(false);
        if (replyOperation != 0x01)
            throw new IOException($"期望 STATUS，收到 op=0x{replyOperation:X2}（载荷 {body.Length} 字节）");
        if (body.Length < 8)
            throw new IOException($"STATUS 载荷只有 {body.Length} 字节，读不出 u64 错误码");
        return BitConverter.ToUInt64(body, 0);
    }

    private async Task<(ulong Operation, byte[] Payload)> ExchangeAsync(
        ulong operation, byte[] payload, CancellationToken cancellationToken)
    {
        var packet = new byte[HeaderSize + payload.Length];
        MagicBytes.CopyTo(packet, 0);
        BitConverter.TryWriteBytes(packet.AsSpan(8, 8), (ulong)packet.Length);
        BitConverter.TryWriteBytes(packet.AsSpan(16, 8), (ulong)packet.Length);
        BitConverter.TryWriteBytes(packet.AsSpan(24, 8), _packetNumber++);
        BitConverter.TryWriteBytes(packet.AsSpan(32, 8), operation);
        payload.CopyTo(packet, HeaderSize);

        await _service.SendAsync(packet, cancellationToken).ConfigureAwait(false);

        var head = await _service.ReceiveAsync(HeaderSize, cancellationToken).ConfigureAwait(false);
        if (head.Length != HeaderSize)
            throw new IOException($"AFC 响应头只收到 {head.Length} 字节（期望 {HeaderSize}）");
        if (!head.AsSpan(0, 8).SequenceEqual(MagicBytes))
            throw new IOException("AFC 响应 magic 不对");
        var entireLength = BitConverter.ToUInt64(head, 8);
        var replyOperation = BitConverter.ToUInt64(head, 32);
        if (entireLength < HeaderSize)
            throw new IOException($"AFC EntireLength={entireLength} 小于头长度");
        var bodyLength = checked((int)(entireLength - HeaderSize));
        var body = bodyLength > 0
            ? await _service.ReceiveAsync(bodyLength, cancellationToken).ConfigureAwait(false)
            : [];
        if (body.Length != bodyLength)
            throw new IOException($"AFC 响应载荷只收到 {body.Length} 字节（期望 {bodyLength}）");
        return (replyOperation, body);
    }
}
