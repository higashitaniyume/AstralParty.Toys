using System.Net.Http;
using System.Text.Json;

namespace AstralParty.Toys.Services.Modding;

public sealed partial class WorkshopService
{
    /// <summary>获取 Forge 启用的资源类型列表。</summary>
    public async Task<List<WorkshopResourceTypeItem>> GetResourceTypesAsync(
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var url = $"{NormalizeBaseUrl(baseUrl)}/api/resource-types";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response = await ApiHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            EnsureSuccess(response, "读取资源类型");
            using var doc = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);

            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<WorkshopResourceTypeItem>>(doc.RootElement.GetRawText(), Json)
                       ?? [];
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("items", out var itemsElement) &&
                itemsElement.ValueKind == JsonValueKind.Array)
            {
                return JsonSerializer.Deserialize<List<WorkshopResourceTypeItem>>(itemsElement.GetRawText(), Json)
                       ?? [];
            }

            return [];
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WorkshopException("连接市场超时，请检查市场地址或网络。");
        }
        catch (HttpRequestException ex)
        {
            throw new WorkshopException($"无法连接市场：{ex.Message}");
        }
    }

    /// <summary>查询公共资源模组列表。</summary>
    public async Task<WorkshopResourceListResponse> GetResourceListAsync(
        string baseUrl,
        string language,
        string? type,
        string? query,
        string? tag,
        string? rating,
        string sort = "updated",
        int page = 1,
        int pageSize = 24,
        CancellationToken cancellationToken = default)
    {
        var url = $"{NormalizeBaseUrl(baseUrl)}/api/resources?lang={Uri.EscapeDataString(language)}"
                  + $"&sort={Uri.EscapeDataString(string.IsNullOrWhiteSpace(sort) ? "updated" : sort.Trim())}"
                  + $"&page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 60)}";
        if (!string.IsNullOrWhiteSpace(type)) url += "&type=" + Uri.EscapeDataString(type.Trim());
        if (!string.IsNullOrWhiteSpace(query)) url += "&query=" + Uri.EscapeDataString(query.Trim());
        if (!string.IsNullOrWhiteSpace(tag)) url += "&tag=" + Uri.EscapeDataString(tag.Trim());
        if (!string.IsNullOrWhiteSpace(rating)) url += "&rating=" + Uri.EscapeDataString(rating.Trim());

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response = await ApiHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            EnsureSuccess(response, "读取资源市场目录");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            return await JsonSerializer.DeserializeAsync<WorkshopResourceListResponse>(stream, Json, timeout.Token)
                   ?? throw new WorkshopException("资源市场目录返回了空数据。");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WorkshopException("连接市场超时，请检查市场地址或网络。");
        }
        catch (HttpRequestException ex)
        {
            throw new WorkshopException($"无法连接市场：{ex.Message}");
        }
    }

    /// <summary>获取单个资源模组详情（含版本、依赖、画廊与说明书）。</summary>
    public async Task<WorkshopResourceDetail> GetResourceDetailAsync(
        string baseUrl,
        string id,
        string language,
        CancellationToken cancellationToken = default)
    {
        var url = $"{NormalizeBaseUrl(baseUrl)}/api/resources/{Uri.EscapeDataString(id)}?lang={Uri.EscapeDataString(language)}";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var response = await ApiHttp.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new WorkshopException("读取资源详情失败：市场里没有这个资源模组。");
            EnsureSuccess(response, "读取资源详情");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            return await JsonSerializer.DeserializeAsync<WorkshopResourceDetail>(stream, Json, timeout.Token)
                   ?? throw new WorkshopException("资源详情返回了空数据。");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new WorkshopException("连接市场超时，请检查市场地址或网络。");
        }
        catch (HttpRequestException ex)
        {
            throw new WorkshopException($"无法连接市场：{ex.Message}");
        }
    }

    /// <summary>缓存资源封面图到本地虚拟域名。</summary>
    public Task<string?> CacheResourceCoverAsync(
        string baseUrl,
        string resourceId,
        string? coverUrl,
        CancellationToken cancellationToken = default)
        => CacheCoverAsync(baseUrl, $"resource-{resourceId}", coverUrl, cancellationToken);

    /// <summary>缓存资源预览画廊图片到本地虚拟域名。</summary>
    public Task<string?> CacheResourcePreviewAsync(
        string baseUrl,
        string resourceId,
        string sha256,
        string? previewUrl,
        CancellationToken cancellationToken = default)
    {
        var shortSha = string.IsNullOrWhiteSpace(sha256)
            ? "0"
            : sha256.Trim().Length > 12 ? sha256.Trim()[..12] : sha256.Trim();
        return CacheCoverAsync(baseUrl, $"resource-{resourceId}-p-{shortSha}", previewUrl, cancellationToken);
    }
}
