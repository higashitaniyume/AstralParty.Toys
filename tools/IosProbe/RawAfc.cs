// 自补的 AFC 原始操作（M0 剩余项 ② / iOS 篇 §8 风险 2）。
//
// 为什么需要：Netimobiledevice 3.x 的 AfcService 只实现了 读/写/删/列目录/取信息，
// **没有 MakeDir、没有 RenamePath**；而 iOS 载荷投放需要 `CesiumLoader/mods/<Id>/data` 这种多级目录，
// 改名那一版（M8）还要能改引导文件名。写嵌套路径现在直接 `ObjectNotFound`。
//
// 为什么是「自己发包」而不是复用库：库里的 `AfcHeader`、`AfcPacket`、`AfcService.RunOperation`
// 分别是 internal / internal abstract / private，外部既用不了也继承不了；
// 但 `ServiceConnection` 是公开的，所以按协议自己发 AFC 包。
//
// 协议（逐字节核对过 Netimobiledevice 的 AfcHeader / DispatchPacket / ReceiveData 反编译结果）：
//   请求 = 40 字节头 + 载荷
//     头 = "CFA6LPAA"          (8 B, ASCII)
//        | EntireLength  (u64 LE)  整个包长度 = 40 + 载荷长度
//        | Length        (u64 LE)  同理（分块写时才会小于 EntireLength）
//        | PacketNumber  (u64 LE)  自增序号
//        | Operation     (u64 LE)  操作码
//   响应 = 同构头 + 载荷；Operation = Status(0x01) 时，载荷是 8 字节 u64 LE 错误码
//   单路径操作载荷 = UTF-8 + NUL；RENAME_PATH = 旧路径 NUL + 新路径 NUL
namespace IosProbe;

/// <summary>AFC 操作码。值取自 Netimobiledevice 的 <c>AfcOpCode</c>，并标注与 libimobiledevice <c>afc.h</c> 的差异。</summary>
/// <remarks>本表只列本次用到的项。<c>MakeDir</c> / <c>RenamePath</c> / <c>GetFileInfo</c> / <c>ReadDir</c>
/// 两个来源一致；<c>RemovePath</c> / <c>Truncate</c> 两边编号不同（库这边 `<c>Rm</c>` 在真机上可用，
/// 因此本文件沿用库的编号，并把差异留在注释里备查）。</remarks>
internal enum AfcOp : ulong
{
    Status = 0x01,
    Data = 0x02,
    ReadDir = 0x03,
    Truncate = 0x07,     // libimobiledevice afc.h: 0x06
    RemovePath = 0x08,   // libimobiledevice afc.h: 0x0E
    MakeDir = 0x09,      // 两边一致
    GetFileInfo = 0x0A,  // 两边一致
    RenamePath = 0x18,   // 两边一致
}

/// <summary>一个 AFC 响应包。</summary>
internal sealed record AfcReply(AfcOp Operation, ulong Status, byte[] Payload)
{
    public bool Ok => Status == 0;

    /// <summary>把状态码翻成 Netimobiledevice 的 <see cref="Netimobiledevice.Afc.AfcError"/> 名字。</summary>
    /// <remarks><c>AfcError</c> 的底层类型是 <c>ulong</c>，所以这里必须传 <c>ulong</c> 给 <c>Enum.IsDefined</c>。</remarks>
    public string StatusName =>
        Enum.IsDefined(typeof(Netimobiledevice.Afc.AfcError), Status)
            ? ((Netimobiledevice.Afc.AfcError)Status).ToString()
            : $"Unknown(0x{Status:X2})";
}

/// <summary>在一条 house_arrest 服务连接上直接收发 AFC 包。</summary>
internal sealed class RawAfcClient(Netimobiledevice.Lockdown.ServiceConnection service)
{
    private const string Magic = "CFA6LPAA";
    private const int HeaderSize = 40;

    private static readonly byte[] MagicBytes = System.Text.Encoding.UTF8.GetBytes(Magic);

    private ulong _packetNumber;

    /// <summary>可覆盖的 opcode，用来在真机上实测编号有争议的操作。</summary>
    public ulong MakeDirOp { get; init; } = (ulong)AfcOp.MakeDir;

    public ulong RenameOp { get; init; } = (ulong)AfcOp.RenamePath;

    public Task<AfcReply> MakeDirectoryAsync(string path, CancellationToken ct = default) =>
        RequestStatusAsync(MakeDirOp, CString(path), ct);

    /// <summary>RENAME_PATH：`old\0new\0`。改名是整棵目录树搬移，不发中间状态。</summary>
    public Task<AfcReply> RenameAsync(string from, string to, CancellationToken ct = default) =>
        RequestStatusAsync(RenameOp, [.. CString(from), .. CString(to)], ct);

    public Task<AfcReply> RemovePathAsync(string path, CancellationToken ct = default) =>
        RequestStatusAsync((ulong)AfcOp.RemovePath, CString(path), ct);

    /// <summary>
    /// READ_DIR 的**浅层**列目录：只列这一层，不递归、不逐条 GetFileInfo。
    /// 库里的 <c>LsDirectory</c> 每次都把整棵树走一遍并逐条 <c>GetFileInfo</c>，
    /// <c>depth</c> 只过滤输出、不限制遍历，而且它的深度过滤在 Windows 上恒不生效
    /// （它数的是 <c>Path.DirectorySeparatorChar</c> = '\'，路径里用的却是 '/'）。
    /// M1 的设备文件页要懒加载，得用这个。
    /// </summary>
    /// <remarks>READ_DIR 的响应是一条 <c>DATA</c> 包，后面不跟 STATUS —— 库的 <c>Walk</c> 连续跑几千次
    /// 都不串位，反过来正说明这一点。载荷是 UTF-8，名字之间以 NUL 分隔、末尾多一个 NUL，
    /// 且以 "." / ".." 开头（库那边是 <c>Skip(2)</c>）。</remarks>
    public async Task<IReadOnlyList<string>> ReadDirectoryAsync(string path, CancellationToken ct = default)
    {
        var (operation, payload) = await ExchangeAsync((ulong)AfcOp.ReadDir, CString(path), ct).ConfigureAwait(false);
        if (operation != AfcOp.Data)
            throw new IOException($"READ_DIR 期望 DATA，收到 op=0x{(ulong)operation:X2}");
        var names = System.Text.Encoding.UTF8.GetString(payload).Split('\0').ToList();
        if (names.Count > 0 && names[^1].Length == 0) names.RemoveAt(names.Count - 1);
        names.RemoveAll(n => n is "." or "..");
        return names;
    }

    private static byte[] CString(string value) => [.. System.Text.Encoding.UTF8.GetBytes(value), 0];

    private async Task<AfcReply> RequestStatusAsync(ulong operation, byte[] payload, CancellationToken ct)
    {
        var (op, body) = await ExchangeAsync(operation, payload, ct).ConfigureAwait(false);
        if (op != AfcOp.Status)
            throw new IOException($"期望 STATUS，收到 op=0x{(ulong)op:X2}（载荷 {body.Length} 字节）");
        if (body.Length < 8)
            throw new IOException($"STATUS 载荷只有 {body.Length} 字节，读不出 u64 错误码");
        return new AfcReply(op, BitConverter.ToUInt64(body, 0), body);
    }

    private async Task<(AfcOp Operation, byte[] Payload)> ExchangeAsync(ulong operation, byte[] payload,
        CancellationToken ct)
    {
        var packet = new byte[HeaderSize + payload.Length];
        WriteHeader(packet, operation, (ulong)packet.Length, _packetNumber++);
        payload.CopyTo(packet, HeaderSize);
        await service.SendAsync(packet, ct).ConfigureAwait(false);
        return await ReadReplyAsync(ct).ConfigureAwait(false);
    }

    private static void WriteHeader(byte[] buffer, ulong operation, ulong packetSize, ulong packetNumber)
    {
        MagicBytes.CopyTo(buffer, 0);
        BitConverter.TryWriteBytes(buffer.AsSpan(8, 8), packetSize);   // EntireLength
        BitConverter.TryWriteBytes(buffer.AsSpan(16, 8), packetSize);  // Length
        BitConverter.TryWriteBytes(buffer.AsSpan(24, 8), packetNumber);
        BitConverter.TryWriteBytes(buffer.AsSpan(32, 8), operation);
    }

    /// <remarks>只读一个包，与库的 <c>ReceiveData</c> 一致：STATUS 与 DATA 各算一次响应，
    /// 不存在「DATA 之后还跟一条 STATUS」。</remarks>
    private async Task<(AfcOp Operation, byte[] Payload)> ReadReplyAsync(CancellationToken ct)
    {
        var head = await service.ReceiveAsync(HeaderSize, ct).ConfigureAwait(false);
        if (head.Length != HeaderSize)
            throw new IOException($"AFC 响应头只收到 {head.Length} 字节（期望 {HeaderSize}）");
        if (!head.AsSpan(0, 8).SequenceEqual(MagicBytes))
            throw new IOException($"AFC 响应 magic 不对：{Convert.ToHexString(head.AsSpan(0, 8))}");

        var entireLength = BitConverter.ToUInt64(head, 8);
        var operation = (AfcOp)BitConverter.ToUInt64(head, 32);
        if (entireLength < HeaderSize)
            throw new IOException($"AFC EntireLength={entireLength} 小于头长度 {HeaderSize}");

        var bodyLength = (int)(entireLength - HeaderSize);
        var body = bodyLength > 0
            ? await service.ReceiveAsync(bodyLength, ct).ConfigureAwait(false)
            : [];
        if (body.Length != bodyLength)
            throw new IOException($"AFC 响应载荷只收到 {body.Length} 字节（期望 {bodyLength}）");
        return (operation, body);
    }
}
