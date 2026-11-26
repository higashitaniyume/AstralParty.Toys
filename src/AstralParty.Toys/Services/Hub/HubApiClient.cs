using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AstralParty.Toys.Services.Hub;

public sealed class HubApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public string ErrorCode { get; }

    public HubApiException(HttpStatusCode statusCode, string errorCode, string message)
        : base(string.IsNullOrWhiteSpace(message) ? errorCode : message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// 组队大厅 REST + 长轮询 HTTP 客户端。所有认证端点自动附带 Bearer hubToken。
/// </summary>
public sealed class HubApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private string _baseUrl;
    private string _token = "";

    public HubApiClient(string? baseUrl = null, HttpMessageHandler? handler = null)
    {
        _baseUrl = HubConfigSection.NormalizeServerUrl(baseUrl);
        if (handler is null)
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
            _ownsHttp = true;
        }
        else
        {
            _http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(40) };
            _ownsHttp = true;
        }

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AstralParty.Toys/Hub");
    }

    public string BaseUrl => _baseUrl;
    public bool HasToken => !string.IsNullOrWhiteSpace(_token);

    public void SetBaseUrl(string? baseUrl) => _baseUrl = HubConfigSection.NormalizeServerUrl(baseUrl);

    public void SetToken(string? token) => _token = (token ?? "").Trim();

    public Task<HubServerConfig> GetConfigAsync(CancellationToken ct = default)
        => SendAsync<HubServerConfig>(HttpMethod.Get, "/api/hub/config", null, authenticated: false, ct);

    public Task<HubAuthResponse> ExchangeMicrosoftCodeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        CancellationToken ct = default)
        => SendAsync<HubAuthResponse>(HttpMethod.Post, "/api/hub/auth/microsoft",
            new { code, codeVerifier, redirectUri }, authenticated: false, ct);

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (!HasToken) return;
        try
        {
            await SendNoContentAsync(HttpMethod.Post, "/api/hub/auth/logout", null, authenticated: true, ct)
                .ConfigureAwait(false);
        }
        catch (HubApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            // 已过期或注销，无需报错。
        }
    }

    public Task<HubUserProfileDto> GetMeAsync(CancellationToken ct = default)
        => SendAsync<HubUserProfileDto>(HttpMethod.Get, "/api/hub/me", null, authenticated: true, ct);

    public Task<HubUserProfileDto> UpdateProfileAsync(
        string? displayName,
        string? primaryRegion,
        string? primaryUid,
        CancellationToken ct = default)
        => SendAsync<HubUserProfileDto>(HttpMethod.Put, "/api/hub/me/profile",
            new { displayName, primaryRegion, primaryUid }, authenticated: true, ct);

    public Task<HubUserProfileDto> UpdatePrivacyAsync(HubPrivacySettings privacy, CancellationToken ct = default)
        => SendAsync<HubUserProfileDto>(HttpMethod.Put, "/api/hub/me/privacy", privacy, authenticated: true, ct);

    public Task<HubUserProfileDto> BindGameAccountAsync(
        string region,
        string uid,
        bool setPrimary,
        HubGameSnapshot? snapshot,
        CancellationToken ct = default)
        => SendAsync<HubUserProfileDto>(HttpMethod.Post, "/api/hub/me/bindings",
            new { region, uid, setPrimary, snapshot }, authenticated: true, ct);

    public Task<HubUserProfileDto> UnbindGameAccountAsync(string region, string uid, CancellationToken ct = default)
        => SendAsync<HubUserProfileDto>(
            HttpMethod.Delete,
            $"/api/hub/me/bindings/{Uri.EscapeDataString(region)}/{Uri.EscapeDataString(uid)}",
            null,
            authenticated: true,
            ct);

    public Task<HubUserProfileDto> GetProfileAsync(string userId, CancellationToken ct = default)
        => SendAsync<HubUserProfileDto>(
            HttpMethod.Get,
            $"/api/hub/profiles/{Uri.EscapeDataString(userId)}",
            null,
            authenticated: HasToken,
            ct);

    public Task<HubUserProfileDto> GetProfileByUidAsync(string region, string uid, CancellationToken ct = default)
        => SendAsync<HubUserProfileDto>(
            HttpMethod.Get,
            $"/api/hub/profiles/by-uid/{Uri.EscapeDataString(region)}/{Uri.EscapeDataString(uid)}",
            null,
            authenticated: HasToken,
            ct);

    public Task<HubRoomListResponse> ListRoomsAsync(
        string region,
        string? query = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var qs = new List<string> { $"region={Uri.EscapeDataString(region)}" };
        if (!string.IsNullOrWhiteSpace(query)) qs.Add($"q={Uri.EscapeDataString(query.Trim())}");
        if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status.Trim())}");
        return SendAsync<HubRoomListResponse>(HttpMethod.Get, $"/api/hub/rooms?{string.Join('&', qs)}", null,
            authenticated: HasToken, ct);
    }

    public Task<HubRoomDetailDto> GetRoomAsync(string roomId, CancellationToken ct = default)
        => SendAsync<HubRoomDetailDto>(HttpMethod.Get, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}", null,
            authenticated: HasToken, ct);

    public Task<HubRoomDetailDto> CreateRoomAsync(
        string region,
        string title,
        string? note,
        string? password,
        int capacity,
        string? activeUid,
        CancellationToken ct = default)
        => SendAsync<HubRoomDetailDto>(HttpMethod.Post, "/api/hub/rooms",
            new { region, title, note, password, capacity, activeUid }, authenticated: true, ct);

    public Task<HubRoomDetailDto> JoinRoomAsync(
        string roomId,
        string? password,
        string? activeUid,
        CancellationToken ct = default)
        => SendAsync<HubRoomDetailDto>(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/join",
            new { password, activeUid }, authenticated: true, ct);

    public Task LeaveRoomAsync(string roomId, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/leave",
            null, authenticated: true, ct);

    public Task DisbandRoomAsync(string roomId, CancellationToken ct = default)
        => SendNoContentAsync(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/disband",
            null, authenticated: true, ct);

    public async Task<(byte[] Bytes, string ContentType)?> DownloadAvatarAsync(string avatarPath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(avatarPath) || !avatarPath.StartsWith("/api/hub/avatars/", StringComparison.Ordinal))
        {
            return null;
        }

        const int MaxAvatarBytes = 262_144;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, _baseUrl + avatarPath);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;

            var mediaType = (resp.Content.Headers.ContentType?.MediaType ?? "").Trim().ToLowerInvariant();
            if (mediaType is not ("image/jpeg" or "image/png" or "image/webp")) return null;
            if (resp.Content.Headers.ContentLength is > MaxAvatarBytes) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var ms = new MemoryStream();
            var buf = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buf, ct).ConfigureAwait(false)) > 0)
            {
                if (ms.Length + read > MaxAvatarBytes) return null;
                ms.Write(buf, 0, read);
            }

            return ms.Length == 0 ? null : (ms.ToArray(), mediaType);
        }
        catch
        {
            return null;
        }
    }

    public Task<HubRoomDetailDto> KickMemberAsync(string roomId, string targetUserId, CancellationToken ct = default)
        => SendAsync<HubRoomDetailDto>(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/kick",
            new { targetUserId }, authenticated: true, ct);

    public Task<HubRoomDetailDto> SwitchActiveUidAsync(string roomId, string activeUid, CancellationToken ct = default)
        => SendAsync<HubRoomDetailDto>(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/active-uid",
            new { activeUid }, authenticated: true, ct);

    public Task<HubRoomDetailDto> NotifyHostLaunchingAsync(string roomId, CancellationToken ct = default)
        => SendAsync<HubRoomDetailDto>(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/host-launching",
            new { }, authenticated: true, ct);

    public Task<HubRoomDetailDto> ReportGameRoomAsync(
        string roomId,
        long gameRoomId,
        string? gamePwd,
        int roomServerId,
        int mapId,
        int mapType,
        CancellationToken ct = default)
        => SendAsync<HubRoomDetailDto>(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/game-room",
            new { gameRoomId, gamePwd, roomServerId, mapId, mapType }, authenticated: true, ct);

    public Task<HubRoomDetailDto> ReportMemberStateAsync(
        string roomId,
        string gameState,
        string? error = null,
        CancellationToken ct = default)
        => SendAsync<HubRoomDetailDto>(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/member-state",
            new { gameState, error }, authenticated: true, ct);

    public Task<HubRoomEventDto> SendMessageAsync(
        string roomId,
        string clientMsgId,
        string text,
        CancellationToken ct = default)
        => SendAsync<HubRoomEventDto>(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/messages",
            new { clientMsgId, text }, authenticated: true, ct);

    public Task<HubFinishResponse> FinishMatchAsync(
        string roomId,
        HubInstantSummaryDto summary,
        CancellationToken ct = default)
        => SendAsync<HubFinishResponse>(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/finish",
            new
            {
                replayId = summary.ReplayId,
                gameRoomId = summary.GameRoomId,
                winnerId = summary.WinnerId,
                finishTime = summary.FinishTime,
                mapId = summary.MapId,
                mapType = summary.MapType,
                round = summary.Round,
                gameVersion = summary.GameVersion,
                players = summary.Players,
            }, authenticated: true, ct);

    public Task<HubRoomDetailDto> ReopenRoomAsync(string roomId, CancellationToken ct = default)
        => SendAsync<HubRoomDetailDto>(HttpMethod.Post, $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/reopen",
            new { }, authenticated: true, ct);

    public Task<HubStreamResponse> StreamRoomAsync(
        string roomId,
        long after,
        int waitSeconds = 25,
        CancellationToken ct = default)
        => SendAsync<HubStreamResponse>(
            HttpMethod.Get,
            $"/api/hub/rooms/{Uri.EscapeDataString(roomId)}/stream?after={after}&wait={waitSeconds}",
            null,
            authenticated: true,
            ct);

    public Task<HubMatchSummaryDto> GetMatchAsync(string region, string replayId, CancellationToken ct = default)
        => SendAsync<HubMatchSummaryDto>(
            HttpMethod.Get,
            $"/api/hub/matches/{Uri.EscapeDataString(region)}/{Uri.EscapeDataString(replayId)}",
            null,
            authenticated: HasToken,
            ct);

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string pathAndQuery,
        object? body,
        bool authenticated,
        CancellationToken ct)
    {
        using var req = BuildRequest(method, pathAndQuery, body, authenticated);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw ParseError(resp.StatusCode, raw);
        }

        return JsonSerializer.Deserialize<T>(raw, JsonOptions)
               ?? throw new InvalidDataException($"服务端返回了空响应：{pathAndQuery}");
    }

    private async Task SendNoContentAsync(
        HttpMethod method,
        string pathAndQuery,
        object? body,
        bool authenticated,
        CancellationToken ct)
    {
        using var req = BuildRequest(method, pathAndQuery, body, authenticated);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw ParseError(resp.StatusCode, raw);
        }
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string pathAndQuery, object? body, bool authenticated)
    {
        var req = new HttpRequestMessage(method, _baseUrl + pathAndQuery);
        if (authenticated && !string.IsNullOrWhiteSpace(_token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        return req;
    }

    private static HubApiException ParseError(HttpStatusCode statusCode, string raw)
    {
        var code = "http_" + (int)statusCode;
        var message = $"请求失败（HTTP {(int)statusCode}）";
        try
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.TryGetProperty("error", out var errEl))
                {
                    if (errEl.ValueKind == JsonValueKind.Object)
                    {
                        if (errEl.TryGetProperty("code", out var cEl) && cEl.GetString() is { Length: > 0 } c)
                            code = c;
                        if (errEl.TryGetProperty("message", out var mEl) && mEl.GetString() is { Length: > 0 } m)
                            message = m;
                    }
                    else if (errEl.ValueKind == JsonValueKind.String && errEl.GetString() is { Length: > 0 } err)
                    {
                        code = err;
                    }
                }
                if (root.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String && msgEl.GetString() is { Length: > 0 } msg)
                    message = msg;
            }
        }
        catch
        {
            // 非 JSON 错误体按 HTTP 状态码兜底。
        }

        return new HubApiException(statusCode, code, message);
    }

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
