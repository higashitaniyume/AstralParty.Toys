using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AstralParty.Toys.Services.Replay;

public sealed partial class ReplayLibraryService
{
    /// <summary>
    /// 从官方 CDN 抓回放并放进回放库（不碰游戏目录）：
    /// 1. ID 必须是纯数字，先下到 <c>.part</c> 临时文件，校验成功再改名；
    /// 2. 库里已有同 ID：内容相同 → 跳过，内容不同 → 拒绝覆盖（与归档/导入同一条规则）；
    /// 3. 解析不出结算帧也照样保留 —— 回放库不是游戏目录，游戏不会扫描它，
    ///    工具读不出只说明自带协议比这局旧，文件本身仍可能是好的，所以只给出明确提示。
    /// </summary>
    public async Task<ReplayOperationResult> DownloadAsync(
        string replayId,
        ReplayCdnEndpoint endpoint,
        IProgress<ReplayDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ReplayOperationResult();
        if (Guard(result) is { } guard) return guard;
        if (!ReplayCdn.IsValidReplayId(replayId, out var idError)) return ReplayOperationResult.Fail(idError);

        replayId = replayId.Trim();
        var url = ReplayCdn.Url(endpoint, replayId);
        var target = LibraryFile(replayId);
        var temp = target + ".part";
        var directory = Path.GetDirectoryName(target)!;
        var directoryExisted = Directory.Exists(directory);
        var alreadyInLibrary = File.Exists(target);

        try
        {
            EnsureLibrary();
            Directory.CreateDirectory(directory);

            var received = await DownloadToFileAsync(url, temp, endpoint, progress, cancellationToken).ConfigureAwait(false);
            if (received == 0)
                return ReplayOperationResult.Fail($"{replayId}：从{ReplayCdn.Label(endpoint)}下载到的是空文件，已放弃。");

            var bytes = await File.ReadAllBytesAsync(temp, cancellationToken).ConfigureAwait(false);
            if (CountFrames(bytes) == 0)
            {
                return ReplayOperationResult.Fail(
                    $"{replayId}：下载到的 {received:N0} 字节不是一个回放帧流（CDN 可能返回了错误页）。" +
                    "请确认回放 ID 与区服是否对应。");
            }

            if (alreadyInLibrary)
            {
                if (FilesIdentical(temp, target))
                {
                    result.Skipped++;
                    result.Messages.Add($"{replayId}：回放库里已经有同一份回放（{ReplayCdn.Label(endpoint)}），本次下载已丢弃。");
                    return result;
                }
                return ReplayOperationResult.Fail(
                    $"{replayId}：回放库里已有同 ID 但内容不同的回放，为避免覆盖已中止。" +
                    "确认要换成 CDN 上这一份，请先删掉库里的旧副本（或改用别的 ID）。");
            }

            File.Move(temp, target, overwrite: true);
            var meta = BuildMeta(replayId, bytes, "cdn-download");
            meta.SizeBytes = new FileInfo(target).Length;
            meta.ModifiedUtcTicks = File.GetLastWriteTimeUtc(target).Ticks;
            WriteSidecar(meta);
            _cache[target] = new CacheEntry(meta.SizeBytes, meta.ModifiedUtcTicks, meta);

            result.Applied++;
            result.Messages.Add($"{replayId}：已从{ReplayCdn.Label(endpoint)}下载并存入回放库（{FormatBytes(meta.SizeBytes)}）。");
            if (meta.FileReplayId.Length > 0 && !string.Equals(meta.FileReplayId, replayId, StringComparison.Ordinal))
            {
                result.Messages.Add($"{replayId}：注意文件内记录的 ID 是 {meta.FileReplayId}，与请求的 ID 不同；" +
                                    "已按请求的 ID 入库，需要的话可以改名后再放回游戏。");
            }
            if (!meta.Healthy)
            {
                result.Messages.Add($"{replayId}：本工具解析不出它的结算帧（{meta.Error}），文件仍已原样保留。" +
                                    "如果游戏也读不出来，别放回游戏目录——游戏列到它会删掉整个目录。");
            }
            Log("download", replayId, $"{url} -> {target}");
        }
        catch (OperationCanceledException)
        {
            result.Ok = false;
            result.Canceled = true;
            result.Failed++;
            result.Messages.Add($"{replayId}：下载已取消，没有写入回放库。");
        }
        catch (HttpRequestException ex)
        {
            result.Ok = false;
            result.Failed++;
            result.Messages.Add($"{replayId}：{DescribeHttpFailure(ex, endpoint)}");
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Failed++;
            result.Messages.Add($"{replayId}：下载失败（{ex.Message}）。");
        }
        finally
        {
            TryDeleteFile(temp);
            // 失败时别把空目录留在库里（游戏不认，工具列表也不会显示，但看着像"有个坏回放"）
            if (!directoryExisted) TryDeleteEmptyDirectory(directory);
        }

        return result;
    }

    /// <summary>边下边写 <paramref name="savePath"/>，返回收到的字节数；HTTP 404 直接给"CDN 上没有这个 ID"的提示。</summary>
    private async Task<long> DownloadToFileAsync(
        string url,
        string savePath,
        ReplayCdnEndpoint endpoint,
        IProgress<ReplayDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await DownloadClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                response.StatusCode == HttpStatusCode.NotFound
                    ? $"CDN 上没有这个回放（HTTP 404）——ID 可能写错了，或者这局不在{ReplayCdn.Label(endpoint)}。"
                    : $"回放 CDN 返回 HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                inner: null,
                statusCode: response.StatusCode);
        }

        var total = response.Content.Headers.ContentLength;
        if (total is > MaxDownloadBytes)
            throw new InvalidDataException($"这个文件有 {total.Value / 1024d / 1024d:0.0} MB，超过 {MaxDownloadBytes / 1024 / 1024} MB 上限，已中止。");

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = File.Create(savePath);

        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            long received = 0;
            progress?.Report(new ReplayDownloadProgress(0, total));
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                received += read;
                if (received > MaxDownloadBytes)
                    throw new InvalidDataException($"下载超过 {MaxDownloadBytes / 1024 / 1024} MB 上限，已中止。");
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                progress?.Report(new ReplayDownloadProgress(received, total));
            }
            return received;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static string DescribeHttpFailure(HttpRequestException exception, ReplayCdnEndpoint endpoint)
        => exception.StatusCode == HttpStatusCode.NotFound
            ? exception.Message
            : $"下载失败（{exception.Message}）请检查网络，或换一个区服试试（当前选的是{ReplayCdn.Label(endpoint)}）。";

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* 残留的 .part 不会被当成回放列出 */ }
    }

    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
                Directory.Delete(path);
        }
        catch { /* 清不掉就算了，空目录无害 */ }
    }

    private static string FormatBytes(long length) => length switch
    {
        >= 1024 * 1024 => $"{length / 1024d / 1024d:0.00} MB",
        >= 1024 => $"{length / 1024d:0.0} KB",
        _ => $"{length} B"
    };
}
