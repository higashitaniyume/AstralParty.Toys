using System.Diagnostics;
using System.Security.Cryptography;
using AstralParty.Toys.Mobile.Logging;

namespace AstralParty.Toys.Mobile.Channels;

/// <summary>
/// 给任意设备文件系统套一层日志：每一次存在性判断、列目录、读取、写入、改名、删除都留痕，
/// 读取会记下字节数与 SHA-256 前 16 位。设备页要求「详细到读取了什么文件」，实现就落在这里，
/// 这样两个平台（AFC / adb）自动都有，不用各写一遍。
/// </summary>
public sealed class LoggingFileSystem(IDeviceFileSystem inner, MobileOperationLog log) : IDeviceFileSystem
{
    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var exists = await inner.ExistsAsync(path, cancellationToken);
        log.File($"存在? {(exists ? "是" : "否")}  {path}");
        return exists;
    }

    public async Task<DeviceFileInfo?> StatAsync(string path, CancellationToken cancellationToken = default)
    {
        var info = await inner.StatAsync(path, cancellationToken);
        log.File(info is null
            ? $"取属性（无）  {path}"
            : $"取属性  {path}  {info.Size:N0} B  修改于 {info.Modified:yyyy-MM-dd HH:mm:ss}");
        return info;
    }

    public async Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken cancellationToken = default)
    {
        var names = await inner.ListAsync(path, cancellationToken);
        log.File($"列目录  {path}  → {names.Count} 项{(names.Count > 0 ? "：" + string.Join(", ", names.Take(8)) + (names.Count > 8 ? " …" : "") : "")}");
        return names;
    }

    public async Task<IReadOnlyList<DeviceDirectoryEntry>> ListDetailedAsync(
        string path, bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        var entries = await inner.ListDetailedAsync(path, includeHidden, cancellationToken);
        log.File($"详列目录  {path}  → {entries.Count} 项");
        return entries;
    }

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var bytes = await inner.ReadAllBytesAsync(path, cancellationToken);
        watch.Stop();
        log.File($"读取  {path}  {bytes.Length:N0} B  {watch.ElapsedMilliseconds} ms  sha256={Short(bytes)}");
        return bytes;
    }

    public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        var text = await inner.ReadAllTextAsync(path, cancellationToken);
        log.File($"读取(文本)  {path}  {text.Length:N0} 字符");
        return text;
    }

    public async Task WriteAllBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        await inner.WriteAllBytesAsync(path, data, cancellationToken);
        watch.Stop();
        log.File($"写入  {path}  {data.Length:N0} B  {watch.ElapsedMilliseconds} ms  sha256={Short(data)}");
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        await inner.DeleteAsync(path, cancellationToken);
        log.File($"删除  {path}");
    }

    public async Task MakeDirectoriesAsync(string path, CancellationToken cancellationToken = default)
    {
        await inner.MakeDirectoriesAsync(path, cancellationToken);
        log.File($"建目录  {path}");
    }

    public async Task RenameAsync(string from, string to, CancellationToken cancellationToken = default)
    {
        await inner.RenameAsync(from, to, cancellationToken);
        log.File($"改名  {from}  →  {to}");
    }

    public async Task<string?> FindBundleOuterAsync(string bundlesRoot, string innerHash,
        CancellationToken cancellationToken = default)
    {
        log.File($"定位代码包外层目录  {bundlesRoot}  内容哈希={innerHash}");
        var outer = await inner.FindBundleOuterAsync(bundlesRoot, innerHash, cancellationToken);
        log.File($"定位结果  {(string.IsNullOrEmpty(outer) ? "未找到" : outer)}");
        return outer;
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private static string Short(byte[] data) => Convert.ToHexString(SHA256.HashData(data))[..16].ToLowerInvariant();
}
