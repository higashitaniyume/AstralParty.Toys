using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AstralParty.Toys.Services.Common;
using Microsoft.Win32.SafeHandles;

namespace AstralParty.Toys.Services.Modding;

/// <summary>
/// 市场包下载引擎：支持 HTTP Range 多线程并发分段下载、本地 <c>.part</c>/<c>.part.json</c> 断点续传、
/// 指数退避分片重试与非 Range 服务器的单流无缝回退。
/// </summary>
internal static class WorkshopDownloadEngine
{
    public const int DefaultThreads = 8;
    public const int MinThreads = 1;
    public const int MaxThreads = 16;

    /// <summary>首次试探分片上限（512 KiB）：既可充当无头端点的 Range 探针，小包也可单次请求直接下完。</summary>
    private const long InitialProbeChunkBytes = 512L * 1024;
    private const long SmallChunkBytes = 1024L * 1024;       // 1 MiB（512 KiB ~ 32 MiB 包）
    private const long LargeChunkBytes = 4L * 1024 * 1024;   // 4 MiB（> 32 MiB 大包）
    private const long LargePackageThreshold = 32L * 1024 * 1024;
    private const int StreamBufferBytes = 128 * 1024;
    private const int MaxSegmentRetries = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static async Task<(string Sha256, long SizeBytes)> DownloadAsync(
        HttpClient http,
        string url,
        string targetPath,
        string resumeCacheDirectory,
        IProgress<WorkshopDownloadProgress>? progress,
        CancellationToken cancellationToken,
        long maxPackageBytes,
        int maxThreads,
        string? expectedSha256)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var threads = Math.Clamp(maxThreads <= 0 ? DefaultThreads : maxThreads, MinThreads, MaxThreads);
        var normalizedExpectedSha = string.IsNullOrWhiteSpace(expectedSha256)
            ? null
            : expectedSha256.Trim().ToLowerInvariant();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(targetPath))!);
        Directory.CreateDirectory(resumeCacheDirectory);
        CleanupStaleCache(resumeCacheDirectory);

        var cacheKey = BuildCacheKey(url, normalizedExpectedSha);
        var partPath = Path.Combine(resumeCacheDirectory, cacheKey + ".part");
        var metaPath = Path.Combine(resumeCacheDirectory, cacheKey + ".part.json");

        // 1. 尝试复用已有断点续传状态
        var existing = TryLoadValidResumeState(partPath, metaPath, url, normalizedExpectedSha, maxPackageBytes);
        if (existing is not null)
        {
            ImportLimits.EnsureFreeSpace(targetPath, existing.TotalBytes);
            try
            {
                return await RunMultiSegmentAsync(
                    http, url, existing.EffectiveUrl ?? url, targetPath, partPath, metaPath,
                    existing, threads, resumed: true, progress, cancellationToken);
            }
            catch (RangeResetException)
            {
                // 服务端资源变化或不再接受断点 Range，清理旧断点后从头下。
                TryDeleteFile(metaPath);
                TryDeleteFile(partPath);
            }
        }

        // 2. 发起首包请求（带 Range: bytes=0-524287）：
        //    - 若服务端返回 200 OK：说明不支持 Range，直接复用此响应流单线程写完；
        //    - 若服务端返回 206 Partial Content：若已涵盖整文件直接写完，否则进入多线程续传。
        using var initialRequest = new HttpRequestMessage(HttpMethod.Get, url);
        initialRequest.Headers.Range = new RangeHeaderValue(0, InitialProbeChunkBytes - 1);

        using var initialResponse = await http.SendAsync(
            initialRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        EnsureDownloadStatus(initialResponse);

        if (initialResponse.StatusCode == HttpStatusCode.PartialContent
            && TryGetTotalLength(initialResponse, out var totalBytes))
        {
            if (totalBytes <= 0)
            {
                return await DownloadSingleStreamAsync(
                    initialResponse, targetPath, maxPackageBytes, progress, cancellationToken);
            }

            if (totalBytes > maxPackageBytes)
            {
                throw new WorkshopException(
                    $"模组包有 {totalBytes / 1024d / 1024d:0.0} MB，超过 {maxPackageBytes / 1024 / 1024} MB 上限。");
            }

            ImportLimits.EnsureFreeSpace(targetPath, totalBytes);

            var effectiveUrl = initialResponse.RequestMessage?.RequestUri?.AbsoluteUri ?? url;
            var etag = initialResponse.Headers.ETag?.Tag;
            var lastModified = initialResponse.Content.Headers.LastModified?.ToString("R");

            var state = CreateInitialState(url, effectiveUrl, totalBytes, normalizedExpectedSha, etag, lastModified, threads);
            TryDeleteFile(partPath);
            PreallocatePartFile(partPath, totalBytes);
            SaveStateAtomic(metaPath, state);

            // 首包响应本身可能包含多于/等于第一片的数据（Range 响应范围 [from..to]）
            var (rangeFrom, rangeTo) = GetResponseRangeBounds(initialResponse, totalBytes);
            return await RunMultiSegmentWithInitialStreamAsync(
                http, url, effectiveUrl, targetPath, partPath, metaPath,
                state, initialResponse, rangeFrom, rangeTo, threads, progress, cancellationToken);
        }

        // 非 206（常见于 200 OK）：单线程流式写入，保证仅发出这一次 HTTP 请求。
        return await DownloadSingleStreamAsync(
            initialResponse, targetPath, maxPackageBytes, progress, cancellationToken);
    }

    private static async Task<(string Sha256, long SizeBytes)> RunMultiSegmentWithInitialStreamAsync(
        HttpClient http,
        string originalUrl,
        string effectiveUrl,
        string targetPath,
        string partPath,
        string metaPath,
        ResumeState state,
        HttpResponseMessage initialResponse,
        long initialFrom,
        long initialTo,
        int threads,
        IProgress<WorkshopDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tracker = new SpeedTracker(state.TotalBytes, state.TotalDownloaded(), threads, resumable: true, resumed: false);
        using var fileHandle = File.OpenHandle(
            partPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);

        var stateGate = new object();
        var lastSaveTicks = Environment.TickCount64;
        var lastReportTicks = 0L;

        void MaybeReport(bool force = false)
        {
            var now = Environment.TickCount64;
            if (!force && Interlocked.Read(ref lastReportTicks) + 180 > now) return;
            Interlocked.Exchange(ref lastReportTicks, now);
            progress?.Report(tracker.Snapshot());
        }

        void MaybeSaveState(bool force = false)
        {
            var now = Environment.TickCount64;
            if (!force && Interlocked.Read(ref lastSaveTicks) + 1000 > now) return;
            lock (stateGate)
            {
                if (!force && lastSaveTicks + 1000 > now) return;
                lastSaveTicks = now;
                try { SaveStateAtomic(metaPath, state); } catch { /* 持久化失败不阻断下载 */ }
            }
        }

        MaybeReport(force: true);

        try
        {
            // 先消费首包 0..initialTo 的响应流，按实际字节更新对应分片进度（不重下首片）。
            await using (var initialStream = await initialResponse.Content.ReadAsStreamAsync(cancellationToken))
            {
                var buffer = new byte[StreamBufferBytes];
                long writeOffset = initialFrom;
                long expectedEnd = initialTo + 1;
                while (writeOffset < expectedEnd)
                {
                    var read = await initialStream.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;
                    var toWrite = (int)Math.Min(read, expectedEnd - writeOffset);
                    await RandomAccess.WriteAsync(fileHandle, buffer.AsMemory(0, toWrite), writeOffset, cancellationToken);
                    MarkBytesDownloaded(state, writeOffset, toWrite, stateGate);
                    writeOffset += toWrite;
                    tracker.AddBytes(toWrite);
                    MaybeReport();
                    MaybeSaveState();
                }
            }

            MaybeSaveState(force: true);

            if (!state.IsCompleted())
            {
                await ExecuteWorkersAsync(
                    http, originalUrl, effectiveUrl, fileHandle, state, stateGate,
                    threads, tracker, MaybeReport, MaybeSaveState, cancellationToken);
            }
        }
        catch
        {
            MaybeSaveState(force: true);
            throw;
        }

        return await FinalizeCompletedPartAsync(fileHandle, partPath, metaPath, targetPath, state.TotalBytes, tracker, progress, cancellationToken);
    }

    private static async Task<(string Sha256, long SizeBytes)> RunMultiSegmentAsync(
        HttpClient http,
        string originalUrl,
        string effectiveUrl,
        string targetPath,
        string partPath,
        string metaPath,
        ResumeState state,
        int threads,
        bool resumed,
        IProgress<WorkshopDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var tracker = new SpeedTracker(state.TotalBytes, state.TotalDownloaded(), threads, resumable: true, resumed);
        using var fileHandle = File.OpenHandle(
            partPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.Asynchronous);

        var stateGate = new object();
        var lastSaveTicks = Environment.TickCount64;
        var lastReportTicks = 0L;

        void MaybeReport(bool force = false)
        {
            var now = Environment.TickCount64;
            if (!force && Interlocked.Read(ref lastReportTicks) + 180 > now) return;
            Interlocked.Exchange(ref lastReportTicks, now);
            progress?.Report(tracker.Snapshot());
        }

        void MaybeSaveState(bool force = false)
        {
            var now = Environment.TickCount64;
            if (!force && Interlocked.Read(ref lastSaveTicks) + 1000 > now) return;
            lock (stateGate)
            {
                if (!force && lastSaveTicks + 1000 > now) return;
                lastSaveTicks = now;
                try { SaveStateAtomic(metaPath, state); } catch { }
            }
        }

        MaybeReport(force: true);

        try
        {
            if (!state.IsCompleted())
            {
                await ExecuteWorkersAsync(
                    http, originalUrl, effectiveUrl, fileHandle, state, stateGate,
                    threads, tracker, MaybeReport, MaybeSaveState, cancellationToken);
            }
        }
        catch
        {
            MaybeSaveState(force: true);
            throw;
        }

        return await FinalizeCompletedPartAsync(fileHandle, partPath, metaPath, targetPath, state.TotalBytes, tracker, progress, cancellationToken);
    }

    private static async Task ExecuteWorkersAsync(
        HttpClient http,
        string originalUrl,
        string effectiveUrl,
        SafeFileHandle fileHandle,
        ResumeState state,
        object stateGate,
        int threads,
        SpeedTracker tracker,
        Action<bool> maybeReport,
        Action<bool> maybeSaveState,
        CancellationToken cancellationToken)
    {
        var pending = new ConcurrentQueue<ResumeSegment>(
            state.Segments.Where(seg => seg.Downloaded < seg.Length).OrderBy(seg => seg.Index));
        if (pending.IsEmpty) return;

        var workerCount = Math.Clamp(Math.Min(threads, pending.Count), MinThreads, MaxThreads);
        tracker.SetActiveThreads(workerCount);
        maybeReport(true);

        var workers = new Task[workerCount];
        for (var i = 0; i < workerCount; i++)
        {
            workers[i] = Task.Run(async () =>
            {
                var buffer = new byte[StreamBufferBytes];
                while (pending.TryDequeue(out var segment))
                {
                    await DownloadSegmentWithRetryAsync(
                        http, originalUrl, effectiveUrl, fileHandle, state, segment, buffer,
                        stateGate, tracker, maybeReport, maybeSaveState, cancellationToken);
                }
            }, cancellationToken);
        }

        await Task.WhenAll(workers);
    }

    private static async Task DownloadSegmentWithRetryAsync(
        HttpClient http,
        string originalUrl,
        string effectiveUrl,
        SafeFileHandle fileHandle,
        ResumeState state,
        ResumeSegment segment,
        byte[] buffer,
        object stateGate,
        SpeedTracker tracker,
        Action<bool> maybeReport,
        Action<bool> maybeSaveState,
        CancellationToken cancellationToken)
    {
        var targetUrl = effectiveUrl;
        for (var attempt = 0; attempt <= MaxSegmentRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            long offset;
            long remaining;
            lock (stateGate)
            {
                offset = segment.Start + segment.Downloaded;
                remaining = segment.Length - segment.Downloaded;
            }
            if (remaining <= 0) return;

            var rangeEnd = segment.End;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                request.Headers.Range = new RangeHeaderValue(offset, rangeEnd);
                if (!string.IsNullOrWhiteSpace(state.ETag))
                    request.Headers.TryAddWithoutValidation("If-Range", state.ETag);

                using var response = await http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

                // 如果重定向后的直链签名失效，回退到原始下载地址重走跳转
                if (targetUrl != originalUrl &&
                    response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.Gone)
                {
                    targetUrl = originalUrl;
                    continue;
                }

                if (response.StatusCode == HttpStatusCode.PreconditionFailed
                    || response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable
                    || response.StatusCode == HttpStatusCode.OK)
                {
                    throw new RangeResetException();
                }

                if ((int)response.StatusCode is 429 or >= 500 && attempt < MaxSegmentRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(350 * (1 << attempt)), cancellationToken);
                    continue;
                }

                EnsureDownloadStatus(response);

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                while (remaining > 0)
                {
                    var read = await stream.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;

                    var toWrite = (int)Math.Min(read, remaining);
                    await RandomAccess.WriteAsync(fileHandle, buffer.AsMemory(0, toWrite), offset, cancellationToken);
                    lock (stateGate)
                    {
                        segment.Downloaded += toWrite;
                    }
                    offset += toWrite;
                    remaining -= toWrite;
                    tracker.AddBytes(toWrite);
                    maybeReport(false);
                    maybeSaveState(false);
                }

                if (remaining == 0)
                {
                    maybeSaveState(false);
                    return;
                }

                if (attempt < MaxSegmentRetries)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(300 * (1 << attempt)), cancellationToken);
                    continue;
                }

                throw new WorkshopException("分片下载未完成：连接意外中断。");
            }
            catch (HttpRequestException) when (attempt < MaxSegmentRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(400 * (1 << attempt)), cancellationToken);
            }
            catch (IOException) when (attempt < MaxSegmentRetries)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(400 * (1 << attempt)), cancellationToken);
            }
        }
    }

    private static async Task<(string Sha256, long SizeBytes)> FinalizeCompletedPartAsync(
        SafeFileHandle fileHandle,
        string partPath,
        string metaPath,
        string targetPath,
        long totalBytes,
        SpeedTracker tracker,
        IProgress<WorkshopDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        RandomAccess.FlushToDisk(fileHandle);
        fileHandle.Dispose();

        string sha;
        await using (var readStream = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read, StreamBufferBytes, useAsync: true))
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buf = new byte[StreamBufferBytes];
            while (true)
            {
                var read = await readStream.ReadAsync(buf, cancellationToken);
                if (read == 0) break;
                hash.AppendData(buf, 0, read);
            }
            sha = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        File.Move(partPath, targetPath, overwrite: true);
        TryDeleteFile(metaPath);

        progress?.Report(new WorkshopDownloadProgress(
            totalBytes, totalBytes, tracker.CurrentSpeedBytesPerSecond, tracker.ActiveThreads, resumable: true, resumed: tracker.Resumed));
        return (sha, totalBytes);
    }

    private static async Task<(string Sha256, long SizeBytes)> DownloadSingleStreamAsync(
        HttpResponseMessage response,
        string targetPath,
        long maxPackageBytes,
        IProgress<WorkshopDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var total = response.Content.Headers.ContentLength;
        if (total is > 0 && total.Value > maxPackageBytes)
            throw new WorkshopException($"模组包有 {total.Value / 1024d / 1024d:0.0} MB，超过 {maxPackageBytes / 1024 / 1024} MB 上限。");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            targetPath, FileMode.Create, FileAccess.Write, FileShare.None, StreamBufferBytes, useAsync: true);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[StreamBufferBytes];
        long received = 0;
        var tracker = new SpeedTracker(total ?? 0, 0, activeThreads: 1, resumable: false, resumed: false);
        var lastReportTicks = 0L;

        progress?.Report(new WorkshopDownloadProgress(0, total, 0, 1, resumable: false, resumed: false));

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;

            received += read;
            if (received > maxPackageBytes)
                throw new WorkshopException($"下载超过 {maxPackageBytes / 1024 / 1024} MB 上限，已中止。");

            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            tracker.AddBytes(read);

            var now = Environment.TickCount64;
            if (now - lastReportTicks >= 180)
            {
                lastReportTicks = now;
                progress?.Report(new WorkshopDownloadProgress(
                    received, total, tracker.CurrentSpeedBytesPerSecond, 1, resumable: false, resumed: false));
            }
        }

        await output.FlushAsync(cancellationToken);
        var sha = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        progress?.Report(new WorkshopDownloadProgress(
            received, total, tracker.CurrentSpeedBytesPerSecond, 1, resumable: false, resumed: false));
        return (sha, received);
    }

    // ============================== 状态与分片规划 ==============================

    private static ResumeState CreateInitialState(
        string originalUrl,
        string effectiveUrl,
        long totalBytes,
        string? expectedSha256,
        string? etag,
        string? lastModified,
        int threads)
    {
        var chunkSize = totalBytes <= InitialProbeChunkBytes || threads <= 1
            ? totalBytes
            : totalBytes <= LargePackageThreshold
                ? Math.Max(InitialProbeChunkBytes, (long)Math.Ceiling(totalBytes / (double)Math.Max(1, threads * 2)))
                : LargeChunkBytes;
        chunkSize = Math.Clamp(chunkSize, InitialProbeChunkBytes, LargeChunkBytes);

        var segments = new List<ResumeSegment>();
        long cursor = 0;
        var idx = 0;
        while (cursor < totalBytes)
        {
            var size = idx == 0 && totalBytes > InitialProbeChunkBytes
                ? InitialProbeChunkBytes
                : Math.Min(chunkSize, totalBytes - cursor);
            var end = cursor + size - 1;
            segments.Add(new ResumeSegment
            {
                Index = idx++,
                Start = cursor,
                End = end,
                Downloaded = 0
            });
            cursor = end + 1;
        }

        return new ResumeState
        {
            Url = originalUrl,
            EffectiveUrl = effectiveUrl,
            TotalBytes = totalBytes,
            ExpectedSha256 = expectedSha256 ?? "",
            ETag = etag ?? "",
            LastModified = lastModified ?? "",
            UpdatedAt = DateTimeOffset.UtcNow,
            Segments = segments
        };
    }

    private static void MarkBytesDownloaded(ResumeState state, long offset, int count, object gate)
    {
        lock (gate)
        {
            var endOffset = offset + count;
            foreach (var seg in state.Segments)
            {
                if (seg.Start > endOffset || seg.End < offset) continue;
                var segExpectedEnd = Math.Min(seg.End + 1, endOffset);
                var covered = segExpectedEnd - seg.Start;
                if (covered > seg.Downloaded && covered <= seg.Length)
                    seg.Downloaded = covered;
            }
        }
    }

    private static ResumeState? TryLoadValidResumeState(
        string partPath,
        string metaPath,
        string url,
        string? expectedSha256,
        long maxPackageBytes)
    {
        if (!File.Exists(partPath) || !File.Exists(metaPath)) return null;
        try
        {
            var state = JsonSerializer.Deserialize<ResumeState>(File.ReadAllText(metaPath), JsonOptions);
            if (state is null || state.TotalBytes <= 0 || state.TotalBytes > maxPackageBytes || state.Segments.Count == 0)
                return null;
            if (!string.IsNullOrEmpty(expectedSha256)
                && !string.Equals(state.ExpectedSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                return null;
            if (string.IsNullOrEmpty(expectedSha256)
                && !string.Equals(state.Url, url, StringComparison.Ordinal))
                return null;

            var fi = new FileInfo(partPath);
            if (fi.Length != state.TotalBytes) return null;

            return state;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetTotalLength(HttpResponseMessage response, out long totalLength)
    {
        if (response.Content.Headers.ContentRange is { Length: > 0 } range)
        {
            totalLength = range.Length.Value;
            return true;
        }
        totalLength = 0;
        return false;
    }

    private static (long From, long To) GetResponseRangeBounds(HttpResponseMessage response, long totalBytes)
    {
        if (response.Content.Headers.ContentRange is { From: not null, To: not null } range)
            return (range.From.Value, range.To.Value);
        return (0, Math.Min(InitialProbeChunkBytes, totalBytes) - 1);
    }

    private static void EnsureDownloadStatus(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new WorkshopException("下载地址返回 404：作者可能已删除这个版本。");
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new WorkshopException("市场暂时关闭了接口，请稍后再试。");
        if (!response.IsSuccessStatusCode)
            throw new WorkshopException($"下载失败：HTTP {(int)response.StatusCode}");
    }

    private static string BuildCacheKey(string url, string? normalizedSha256)
    {
        if (!string.IsNullOrEmpty(normalizedSha256))
            return "sha-" + new string(normalizedSha256.Where(char.IsLetterOrDigit).Take(48).ToArray());
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return "url-" + Convert.ToHexString(hash)[..32].ToLowerInvariant();
    }

    private static void PreallocatePartFile(string partPath, long totalBytes)
    {
        using var fs = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None);
        fs.SetLength(totalBytes);
    }

    private static void SaveStateAtomic(string metaPath, ResumeState state)
    {
        state.UpdatedAt = DateTimeOffset.UtcNow;
        var temp = metaPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, JsonOptions));
        File.Move(temp, metaPath, overwrite: true);
    }

    private static void CleanupStaleCache(string cacheDir)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            foreach (var file in Directory.EnumerateFiles(cacheDir, "*.part*"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    TryDeleteFile(file);
            }
        }
        catch
        {
            // 清理失败不影响正常下载
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private sealed class RangeResetException : Exception;

    private sealed class SpeedTracker
    {
        private readonly long _totalBytes;
        private readonly bool _resumable;
        private readonly bool _resumed;
        private readonly object _windowLock = new();
        private readonly Queue<(long Ticks, long Bytes)> _window = new();
        private long _received;
        private long _windowBytes;
        private int _activeThreads;

        public SpeedTracker(long totalBytes, long initialBytes, int activeThreads, bool resumable, bool resumed)
        {
            _totalBytes = totalBytes;
            _received = initialBytes;
            _activeThreads = activeThreads;
            _resumable = resumable;
            _resumed = resumed;
        }

        public bool Resumed => _resumed;
        public int ActiveThreads => Volatile.Read(ref _activeThreads);
        public double CurrentSpeedBytesPerSecond => ComputeSpeed();

        public void SetActiveThreads(int threads) => Volatile.Write(ref _activeThreads, Math.Max(1, threads));

        public void AddBytes(long count)
        {
            Interlocked.Add(ref _received, count);
            var now = Stopwatch.GetTimestamp();
            lock (_windowLock)
            {
                _window.Enqueue((now, count));
                _windowBytes += count;
                TrimLocked(now);
            }
        }

        public WorkshopDownloadProgress Snapshot()
            => new(
                Interlocked.Read(ref _received),
                _totalBytes > 0 ? _totalBytes : null,
                ComputeSpeed(),
                Volatile.Read(ref _activeThreads),
                _resumable,
                _resumed);

        private double ComputeSpeed()
        {
            var now = Stopwatch.GetTimestamp();
            lock (_windowLock)
            {
                TrimLocked(now);
                if (_window.Count == 0 || _windowBytes <= 0) return 0;
                var oldest = _window.Peek().Ticks;
                var elapsedSec = Math.Max(0.15, (now - oldest) / (double)Stopwatch.Frequency);
                return _windowBytes / elapsedSec;
            }
        }

        private void TrimLocked(long now)
        {
            var maxAgeTicks = (long)(Stopwatch.Frequency * 1.5);
            while (_window.Count > 0 && now - _window.Peek().Ticks > maxAgeTicks)
            {
                var dropped = _window.Dequeue();
                _windowBytes -= dropped.Bytes;
            }
        }
    }

    private sealed class ResumeState
    {
        public string Url { get; set; } = "";
        public string? EffectiveUrl { get; set; }
        public long TotalBytes { get; set; }
        public string ExpectedSha256 { get; set; } = "";
        public string ETag { get; set; } = "";
        public string LastModified { get; set; } = "";
        public DateTimeOffset UpdatedAt { get; set; }
        public List<ResumeSegment> Segments { get; set; } = [];

        public long TotalDownloaded() => Segments.Sum(s => s.Downloaded);
        public bool IsCompleted() => Segments.All(s => s.Downloaded >= s.Length);
    }

    private sealed class ResumeSegment
    {
        public int Index { get; set; }
        public long Start { get; set; }
        public long End { get; set; }
        public long Downloaded { get; set; }
        public long Length => End - Start + 1;
    }
}
