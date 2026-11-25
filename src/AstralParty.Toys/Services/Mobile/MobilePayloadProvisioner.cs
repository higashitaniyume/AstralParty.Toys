using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using AstralParty.Toys.Mobile.Payload;
using AstralParty.Toys.Services.Common;

namespace AstralParty.Toys.Services.Mobile;

public sealed record MobilePayloadPullResult(
    string Tag,
    string Version,
    string SdkVersion,
    string ArchivePath,
    string Sha256);

/// <summary>
/// 把随包内嵌的移动端载荷包解到数据根，供「投放载荷」直接用。
///
/// 内嵌的是**一个 zip**（<c>Resources\ModLoader\cesium-loader-android-managed.zip</c>，安卓仓库发版产出），
/// 与桌面那包同一条路：不在代码里写死版本或哈希，版本从包内 <c>loader.json</c> 读，
/// CI 打 tag 时按安卓仓库最新 Release 的 <c>version.json → packages.loaderManaged</c> 覆盖这个文件。
/// </summary>
public static class MobilePayloadProvisioner
{
    public const string AndroidRepo = "aspatoys/AstralParty.Toys.Android";
    private const string ReleasesApiUrl = $"https://api.github.com/repos/{AndroidRepo}/releases?per_page=1";
    private const string LatestManifestFallbackUrl =
        $"https://github.com/{AndroidRepo}/releases/latest/download/version.json";
    private const long MaxDownloadBytes = 64L * 1024 * 1024;

    private static readonly HttpClient Http = CreateHttpClient();

    /// <summary>内嵌资源名，由 csproj 的 <c>LogicalName</c> 固定。</summary>
    public const string ResourceName = "AstralParty.Toys.ModLoader.cesium-loader-mobile-managed.zip";

    /// <summary>落到数据根后的文件名。</summary>
    public const string FileName = "cesium-loader-mobile-managed.zip";

    /// <summary>版本标记：应用版本 + 内嵌包内容哈希，任一变化都重解。</summary>
    private const string MarkerName = "cesium-loader-mobile-managed.sha256";

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AstralParty.Toys", "1.0"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    /// <summary>载荷所在目录（数据根下）。</summary>
    public static string PayloadDirectory(string dataRoot) => Path.Combine(dataRoot, "payload");

    /// <summary>载荷包路径（不存在时按需落盘）。</summary>
    public static string PayloadArchive(string dataRoot) =>
        Path.Combine(PayloadDirectory(dataRoot), FileName);

    /// <summary>读取当前已落盘载荷的版本与 SDK 信息（不抛异常）。</summary>
    public static (bool Present, string Path, string Version, string SdkVersion, int FileCount) ReadSummary(string dataRoot)
    {
        var path = PayloadArchive(dataRoot);
        if (!File.Exists(path) && !Directory.Exists(path))
            return (false, path, "", "", 0);
        try
        {
            var payload = MobilePayload.Read(path);
            return (true, path, payload.Version, payload.SdkVersion, payload.Files.Count);
        }
        catch
        {
            return (File.Exists(path), path, "", "", 0);
        }
    }

    /// <summary>
    /// 确保载荷包已落盘且是最新内容，返回包路径；内容没变时不做任何写入。
    /// </summary>
    public static string Ensure(string dataRoot)
    {
        var directory = PayloadDirectory(dataRoot);
        var target = Path.Combine(directory, FileName);
        var marker = Path.Combine(directory, MarkerName);

        // 直接读资源流算哈希：标记同时覆盖「应用换了包」和「同一版本里换了包」两种情况
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"应用里没有内嵌移动端载荷包（{ResourceName}）");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        var stamp = EmbeddedStamp(hash);
        if (File.Exists(target) && File.Exists(marker) &&
            string.Equals(File.ReadAllText(marker).Trim(), stamp, StringComparison.Ordinal))
            return target;

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(target, bytes);
        File.WriteAllText(marker, stamp);
        TryDeleteLegacyArchive(directory);
        return target;
    }

    private static void TryDeleteLegacyArchive(string directory)
    {
        foreach (var oldName in new[] { "cesium-loader-android-managed.zip", "cesium-loader-android-managed.sha256" })
        {
            try
            {
                var path = Path.Combine(directory, oldName);
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
                // Best effort cleanup of pre-rename archive.
            }
        }
    }

    /// <summary>内嵌载荷包的 SHA-256（十六进制小写），用于日志与自检。</summary>
    public static string EmbeddedHash()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException(
                               $"应用里没有内嵌移动端载荷包（{ResourceName}）");
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    /// <summary>
    /// 从安卓仓库的最新 Release（含 pre-release）读取 <c>version.json → packages.loaderManaged</c>，
    /// 下载并校验 SHA-256 与载荷结构后原子替换 <see cref="PayloadArchive"/>。
    /// </summary>
    public static async Task<MobilePayloadPullResult> PullLatestAsync(
        string dataRoot,
        string? mirrorType,
        string? customMirrorUrl,
        Action<string, int>? progress = null,
        HttpClient? client = null,
        CancellationToken cancellationToken = default)
    {
        var http = client ?? Http;
        progress?.Invoke("正在查询最新移动端载荷…", 10);

        var (tag, manifestJson) = await FetchLatestManifestAsync(http, mirrorType, customMirrorUrl, cancellationToken)
            .ConfigureAwait(false);
        var (downloadUrl, expectedSha256) = ParseLoaderManagedFromManifest(manifestJson);

        Directory.CreateDirectory(PayloadDirectory(dataRoot));
        var target = PayloadArchive(dataRoot);
        var temp = target + ".download.tmp";

        try
        {
            progress?.Invoke($"正在下载载荷（{tag}）…", 25);
            await DownloadVerifiedAsync(http, downloadUrl, expectedSha256, temp, mirrorType, customMirrorUrl,
                percent => progress?.Invoke($"正在下载载荷（{tag}）…", 25 + percent * 60 / 100),
                cancellationToken).ConfigureAwait(false);

            progress?.Invoke("正在校验载荷包内容…", 90);
            var payload = MobilePayload.Read(temp);
            payload.EnsureSdkUsable(allowDesktopSdk: !string.IsNullOrEmpty(
                Environment.GetEnvironmentVariable(MobilePayload.AllowDesktopSdkEnv)));

            File.Move(temp, target, overwrite: true);
            // 保持与当前应用内嵌包一致的 marker，避免重启或下次 Ensure 把刚拉取的最新包覆盖回旧包；
            // 若未来更新 Toys 且内嵌哈希变新，Ensure 会再按新哈希覆盖。
            var embeddedStamp = EmbeddedStamp(EmbeddedHash());
            File.WriteAllText(Path.Combine(PayloadDirectory(dataRoot), MarkerName), embeddedStamp);

            var finalSha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(target))).ToLowerInvariant();
            progress?.Invoke("载荷拉取完成", 100);
            return new MobilePayloadPullResult(tag, payload.Version, payload.SdkVersion, target, finalSha);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try { File.Delete(temp); } catch { }
            }
        }
    }

    private static string EmbeddedStamp(string hash) =>
        (Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0") + " " + hash;

    private static IEnumerable<string> UrlAttempts(string originalUrl, string? mirrorType, string? customMirrorUrl)
    {
        if (string.IsNullOrEmpty(mirrorType) ||
            string.Equals(mirrorType, GitHubMirrorHelper.TypeDirect, StringComparison.OrdinalIgnoreCase))
            return [originalUrl];

        var mirrored = GitHubMirrorHelper.ApplyMirror(originalUrl, mirrorType, customMirrorUrl);
        return string.Equals(mirrored, originalUrl, StringComparison.OrdinalIgnoreCase)
            ? [originalUrl]
            : [mirrored, originalUrl];
    }

    private static async Task<(string Tag, string ManifestJson)> FetchLatestManifestAsync(
        HttpClient http, string? mirrorType, string? customMirrorUrl, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        string? resolvedTag = null;
        string? manifestUrl = null;

        // 优先经 Releases API 查最新一条（安卓常为 pre-release，releases/latest 会跳过）。
        foreach (var apiUrl in UrlAttempts(ReleasesApiUrl, mirrorType, customMirrorUrl))
        {
            try
            {
                using var response = await http.GetAsync(apiUrl, cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"releases API HTTP {(int)response.StatusCode}");
                    continue;
                }
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var latest = doc.RootElement[0];
                    if (latest.TryGetProperty("tag_name", out var tagEl) && tagEl.GetString() is { Length: > 0 } tag)
                    {
                        resolvedTag = tag;
                        if (latest.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var asset in assets.EnumerateArray())
                            {
                                var name = asset.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
                                var url = asset.TryGetProperty("browser_download_url", out var uEl) ? uEl.GetString() : null;
                                if (string.Equals(name, "version.json", StringComparison.OrdinalIgnoreCase) &&
                                    !string.IsNullOrWhiteSpace(url))
                                {
                                    manifestUrl = url;
                                    break;
                                }
                            }
                        }
                        manifestUrl ??= $"https://github.com/{AndroidRepo}/releases/download/{Uri.EscapeDataString(tag)}/version.json";
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(manifestUrl))
            candidates.Add(manifestUrl);
        candidates.Add(LatestManifestFallbackUrl);

        foreach (var rawManifestUrl in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var url in UrlAttempts(rawManifestUrl, mirrorType, customMirrorUrl))
            {
                try
                {
                    using var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        errors.Add($"version.json HTTP {(int)response.StatusCode}");
                        continue;
                    }
                    var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    return (resolvedTag ?? TryReadTag(json) ?? "latest", json);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                }
            }
        }

        throw new InvalidOperationException("拉取载荷版本清单失败：" + string.Join("；", errors));
    }

    private static string? TryReadTag(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("tag", out var t) && t.GetString() is { Length: > 0 } tv)
                return tv;
            if (doc.RootElement.TryGetProperty("version", out var v) && v.GetString() is { Length: > 0 } vv)
                return vv;
        }
        catch { }
        return null;
    }

    private static (string Url, string Sha256) ParseLoaderManagedFromManifest(string manifestJson)
    {
        using var doc = JsonDocument.Parse(manifestJson);
        if (!doc.RootElement.TryGetProperty("packages", out var packages) ||
            !packages.TryGetProperty("loaderManaged", out var loaderManaged))
        {
            throw new InvalidDataException("version.json 缺少 packages.loaderManaged");
        }

        var url = loaderManaged.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
        var sha256 = loaderManaged.TryGetProperty("sha256", out var shaEl) ? shaEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidDataException("packages.loaderManaged 缺少下载地址");

        return (url, sha256 ?? "");
    }

    private static async Task DownloadVerifiedAsync(
        HttpClient http,
        string originalUrl,
        string expectedSha256,
        string targetPath,
        string? mirrorType,
        string? customMirrorUrl,
        Action<int>? onPercent,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        foreach (var url in UrlAttempts(originalUrl, mirrorType, customMirrorUrl))
        {
            try
            {
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"HTTP {(int)response.StatusCode}");
                    continue;
                }

                var total = response.Content.Headers.ContentLength;
                if (total > MaxDownloadBytes)
                    throw new InvalidDataException("移动端载荷包超过 64 MB 限制");

                using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                await using (var destination = File.Create(targetPath))
                {
                    var buffer = new byte[81920];
                    long received = 0;
                    var lastPercent = -1;
                    int read;
                    while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                               .ConfigureAwait(false)) > 0)
                    {
                        received += read;
                        if (received > MaxDownloadBytes)
                            throw new InvalidDataException("移动端载荷包超过 64 MB 限制");
                        hasher.AppendData(buffer, 0, read);
                        await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        if (total > 0)
                        {
                            var percent = (int)(received * 100 / total.Value);
                            if (percent != lastPercent)
                            {
                                lastPercent = percent;
                                onPercent?.Invoke(percent);
                            }
                        }
                    }
                }

                var actualSha = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
                if (!string.IsNullOrWhiteSpace(expectedSha256) &&
                    !string.Equals(actualSha, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"SHA-256 校验失败（{actualSha[..12]} != {expectedSha256.Trim()[..12]}）");
                    continue;
                }

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }
        }

        throw new InvalidOperationException("下载移动端载荷包失败：" + string.Join("；", errors));
    }
}
