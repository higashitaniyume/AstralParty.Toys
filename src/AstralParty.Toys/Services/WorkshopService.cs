using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace AstralParty.Toys.Services;

/// <summary>
/// 创意工坊客户端：拉取 Forge 的模组目录、缓存封面、下载作者托管的模组包。
///
/// 所有网络访问都留在 WPF 宿主里（页面只收到已经整理好的数据），封面通过
/// <c>https://workshop.astral.local/{name}.webp</c> 虚拟域名提供给页面。
/// </summary>
public sealed class WorkshopService
{
    /// <summary>默认工坊地址；正式域名由用户在设置里替换。</summary>
    public const string DefaultBaseUrl = "http://localhost:15555";

    /// <summary>封面的虚拟域名（页面用 &lt;img&gt; 直接引用）。</summary>
    public const string CoverOrigin = "https://workshop.astral.local/";

    /// <summary>单张封面上限，与 Forge 的管理端限制一致。</summary>
    public const int MaxCoverBytes = 8 * 1024 * 1024;

    /// <summary>单个模组包上限：与回放下载一样走本地临时文件，不占内存。</summary>
    public const long MaxPackageBytes = 256L * 1024 * 1024;

    /// <summary>
    /// 工坊请求的用户代理，Forge 用它统计客户端版本。
    /// 格式与 Forge 的解析规则一致：<c>AstralParty Toys/1.2.0</c>。
    /// </summary>
    public static string UserAgent { get; } = BuildUserAgent();

    private static string BuildUserAgent()
    {
        var version = AppInfo.Get().Version;
        if (string.IsNullOrWhiteSpace(version)) version = "0.0.0";
        return $"AstralParty Toys/{version}";
    }

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static string CoverDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AstralParty.Toys", "workshop-covers");

    /// <summary>目录用的 HTTP 管道。只有测试会替换它，生产路径永远是下面那份真实客户端。</summary>
    internal HttpMessageHandler? ApiHandlerOverride { get; set; }

    /// <summary>下载用的 HTTP 管道（测试替换点，见 <see cref="ApiHandlerOverride"/>）。</summary>
    internal HttpMessageHandler? DownloadHandlerOverride { get; set; }

    /// <summary>封面缓存目录（测试指向临时目录，避免污染用户数据）。</summary>
    internal string? CoverDirectoryOverride { get; set; }

    private HttpClient? _apiClient;
    private HttpClient? _downloadClient;

    private HttpClient ApiHttp => _apiClient ??= CreateApiClient(ApiHandlerOverride);

    /// <summary>下载专用客户端：不设整体超时，由调用方的取消源控制。</summary>
    private HttpClient DownloadHttp => _downloadClient ??= CreateDownloadClient(DownloadHandlerOverride);

    private string CoverRoot => CoverDirectoryOverride ?? CoverDirectory;

    /// <summary>规范化用户填写的工坊地址；非法输入回退到默认地址。</summary>
    public static string NormalizeBaseUrl(string? value)
    {
        var text = (value ?? "").Trim().TrimEnd('/');
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return DefaultBaseUrl;
        return text;
    }

    public static void EnsureCoverDirectory() => Directory.CreateDirectory(CoverDirectory);

    // ============================== 目录 ==============================

    public async Task<WorkshopListResponse> GetListAsync(
        string baseUrl,
        string language,
        string? query,
        string? tag,
        string sort,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var url = $"{NormalizeBaseUrl(baseUrl)}/api/mods?lang={Uri.EscapeDataString(language)}"
                  + $"&sort={Uri.EscapeDataString(sort)}&page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 60)}";
        if (!string.IsNullOrWhiteSpace(query)) url += "&query=" + Uri.EscapeDataString(query.Trim());
        if (!string.IsNullOrWhiteSpace(tag)) url += "&tag=" + Uri.EscapeDataString(tag.Trim());

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response = await ApiHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            EnsureSuccess(response, "读取模组目录");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            return await JsonSerializer.DeserializeAsync<WorkshopListResponse>(stream, Json, timeout.Token)
                   ?? throw new WorkshopException("模组目录返回了空数据。");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WorkshopException("连接创意工坊超时，请检查工坊地址或网络。");
        }
        catch (HttpRequestException ex)
        {
            throw new WorkshopException($"无法连接创意工坊：{ex.Message}");
        }
    }

    public async Task<WorkshopModDetail> GetDetailAsync(
        string baseUrl,
        string id,
        string language,
        CancellationToken cancellationToken = default)
    {
        var url = $"{NormalizeBaseUrl(baseUrl)}/api/mods/{Uri.EscapeDataString(id)}?lang={Uri.EscapeDataString(language)}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response = await ApiHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            EnsureSuccess(response, "读取模组详情");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            return await JsonSerializer.DeserializeAsync<WorkshopModDetail>(stream, Json, timeout.Token)
                   ?? throw new WorkshopException("模组详情返回了空数据。");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WorkshopException("连接创意工坊超时，请检查工坊地址或网络。");
        }
        catch (HttpRequestException ex)
        {
            throw new WorkshopException($"无法连接创意工坊：{ex.Message}");
        }
    }

    // ============================== 封面 ==============================

    /// <summary>
    /// 确保封面已缓存在本地，返回页面可用的虚拟地址。失败返回 null（页面显示占位符）。
    /// 文件名里带版本标识，换封面后不会读到旧图。
    /// </summary>
    public async Task<string?> CacheCoverAsync(
        string baseUrl,
        string modId,
        string? coverUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(coverUrl)) return null;

        try
        {
            Directory.CreateDirectory(CoverRoot);
            var token = ExtractCoverToken(coverUrl);
            var fileName = Sanitize(modId) + "-" + token + ".webp";
            var path = Path.Combine(CoverRoot, fileName);
            if (File.Exists(path) && new FileInfo(path).Length > 0)
                return CoverOrigin + fileName;

            var absolute = ResolveUrl(baseUrl, coverUrl);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            using var response = await ApiHttp.GetAsync(absolute, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) return null;

            if (response.Content.Headers.ContentLength is > MaxCoverBytes) return null;
            var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            if (bytes.Length == 0 || bytes.Length > MaxCoverBytes) return null;
            // 只缓存真正的 WebP：反向代理的错误页也可能带 200。
            if (!IsWebp(bytes)) return null;

            File.WriteAllBytes(path, bytes);
            return CoverOrigin + fileName;
        }
        catch (Exception)
        {
            // 封面只是装饰：下载失败就让页面显示占位符，不打断列表。
            return null;
        }
    }

    // ============================== 下载 ==============================

    /// <summary>下载模组包到临时文件，边下边算 SHA-256；调用方负责删除文件。</summary>
    public async Task<(string Sha256, long SizeBytes)> DownloadPackageAsync(
        string url,
        string targetPath,
        IProgress<WorkshopDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await DownloadHttp.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new WorkshopException("下载地址返回 404：作者可能已删除这个版本。");
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            throw new WorkshopException("创意工坊暂时关闭了接口，请稍后再试。");
        if (!response.IsSuccessStatusCode)
            throw new WorkshopException($"下载失败：HTTP {(int)response.StatusCode}");

        var total = response.Content.Headers.ContentLength;
        if (total is > MaxPackageBytes)
            throw new WorkshopException($"模组包有 {total.Value / 1024d / 1024d:0.0} MB，超过 {MaxPackageBytes / 1024 / 1024} MB 上限。");

        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = new FileStream(
            targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[128 * 1024];
        long received = 0;
        progress?.Report(new WorkshopDownloadProgress(0, total));

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;

            received += read;
            if (received > MaxPackageBytes)
                throw new WorkshopException($"下载超过 {MaxPackageBytes / 1024 / 1024} MB 上限，已中止。");

            hash.AppendData(buffer, 0, read);
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            progress?.Report(new WorkshopDownloadProgress(received, total));
        }

        await output.FlushAsync(cancellationToken);
        var sha = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        return (sha, received);
    }

    // ============================== 辅助 ==============================

    /// <summary>把目录里的相对下载/封面地址拼成绝对地址；已经是绝对地址就原样返回。</summary>
    public static string ResolveUrl(string baseUrl, string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? url
            : NormalizeBaseUrl(baseUrl) + "/" + url.TrimStart('/');
    }

    /// <summary>SemVer 风格比较（最多三段数字）；无法解析的段按 0 处理。</summary>
    public static int CompareVersions(string? left, string? right)
    {
        static int[] Parts(string? value)
        {
            var parts = new int[3];
            var segments = (value ?? "").Split('.');
            for (var index = 0; index < 3 && index < segments.Length; index++)
                int.TryParse(segments[index].Trim(), out parts[index]);
            return parts;
        }

        var a = Parts(left);
        var b = Parts(right);
        for (var index = 0; index < 3; index++)
        {
            if (a[index] != b[index]) return a[index] > b[index] ? 1 : -1;
        }

        return 0;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string action)
    {
        if (response.IsSuccessStatusCode) return;
        throw response.StatusCode switch
        {
            HttpStatusCode.NotFound => new WorkshopException($"{action}失败：创意工坊没有这个模组。"),
            HttpStatusCode.ServiceUnavailable => new WorkshopException(
                $"{action}失败：创意工坊暂时关闭了接口，请稍后再试。"),
            _ => new WorkshopException($"{action}失败：HTTP {(int)response.StatusCode}")
        };
    }

    private static bool IsWebp(byte[] bytes)
        => bytes.Length >= 12
           && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
           && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';

    /// <summary>从 <c>/api/mods/{id}/cover?v={sha前8位}</c> 里取出缓存标识。</summary>
    private static string ExtractCoverToken(string coverUrl)
    {
        var queryIndex = coverUrl.IndexOf('?');
        if (queryIndex >= 0)
        {
            foreach (var pair in coverUrl[(queryIndex + 1)..].Split('&'))
            {
                var equals = pair.IndexOf('=');
                if (equals <= 0) continue;
                if (!pair[..equals].Equals("v", StringComparison.OrdinalIgnoreCase)) continue;
                var token = Sanitize(pair[(equals + 1)..]);
                if (token.Length > 0) return token;
            }
        }

        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(coverUrl));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    private static string Sanitize(string value)
    {
        var chars = value.Trim().Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray();
        var text = new string(chars);
        return text.Length == 0 ? "cover" : text.Length > 48 ? text[..48] : text;
    }

    private static HttpClient CreateApiClient(HttpMessageHandler? handler = null)
    {
        var client = handler is null
            ? new HttpClient { Timeout = Timeout.InfiniteTimeSpan }
            : new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        ApplyUserAgent(client);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    private static HttpClient CreateDownloadClient(HttpMessageHandler? handler = null)
    {
        HttpClient client;
        if (handler is null)
        {
            var sockets = new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 8,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                ConnectTimeout = TimeSpan.FromSeconds(20),
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            };
            client = new HttpClient(sockets) { Timeout = Timeout.InfiniteTimeSpan };
        }
        else
        {
            client = new HttpClient(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
        }

        ApplyUserAgent(client);
        return client;
    }

    /// <summary><c>User-Agent</c> 里带空格，不能用 ParseAdd（会被当成产品/版本对拒绝）。</summary>
    private static void ApplyUserAgent(HttpClient client)
        => client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
}
