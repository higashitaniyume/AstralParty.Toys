using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace AstralParty.Toys.Services.Hub;

/// <summary>
/// 负责 Microsoft OAuth PKCE 桌面回环登录、状态缓存以及 DPAPI 令牌保护。
/// 公共客户端模式不包含也不保存任何 ClientSecret。
/// </summary>
public sealed class HubAuthService
{
    private readonly ToysConfigStore _configStore;

    public HubAuthService(ToysConfigStore configStore)
    {
        _configStore = configStore;
    }

    public static string GenerateCodeVerifier()
        => Base64UrlEncode(RandomNumberGenerator.GetBytes(48));

    public static string ComputeCodeChallenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(hash);
    }

    public static string BuildAuthorizeUrl(
        string clientId,
        string tenant,
        string redirectUri,
        string state,
        string codeChallenge)
    {
        var safeTenant = Uri.EscapeDataString(string.IsNullOrWhiteSpace(tenant) ? "common" : tenant.Trim());
        return $"https://login.microsoftonline.com/{safeTenant}/oauth2/v2.0/authorize"
               + $"?client_id={Uri.EscapeDataString(clientId.Trim())}"
               + "&response_type=code"
               + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
               + "&response_mode=query"
               + $"&scope={Uri.EscapeDataString("openid profile")}"
               + $"&state={Uri.EscapeDataString(state)}"
               + $"&code_challenge={Uri.EscapeDataString(codeChallenge)}"
               + "&code_challenge_method=S256"
               + "&prompt=select_account";
    }

    public (string Token, long ExpiresAt, string UserId, string DisplayName)? LoadStoredSession()
    {
        var cfg = _configStore.Load().Hub;
        if (string.IsNullOrWhiteSpace(cfg.ProtectedTokenBase64)) return null;

        try
        {
            var cipher = Convert.FromBase64String(cfg.ProtectedTokenBase64);
            var plain = ProtectedData.Unprotect(cipher, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var token = Encoding.UTF8.GetString(plain).Trim();
            if (string.IsNullOrEmpty(token)) return null;
            return (token, cfg.TokenExpiresAt, cfg.UserId, cfg.DisplayName);
        }
        catch
        {
            return null;
        }
    }

    public void SaveSession(string hubToken, long expiresAt, HubUserProfileDto profile)
    {
        var plain = Encoding.UTF8.GetBytes(hubToken.Trim());
        var cipher = ProtectedData.Protect(plain, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var encoded = Convert.ToBase64String(cipher);

        _configStore.Update(cfg =>
        {
            cfg.Hub.ProtectedTokenBase64 = encoded;
            cfg.Hub.TokenExpiresAt = expiresAt;
            cfg.Hub.UserId = profile.UserId;
            cfg.Hub.DisplayName = profile.DisplayName;
        });
    }

    public void ClearSession()
    {
        _configStore.Update(cfg =>
        {
            cfg.Hub.ProtectedTokenBase64 = "";
            cfg.Hub.TokenExpiresAt = 0;
            cfg.Hub.UserId = "";
            cfg.Hub.DisplayName = "";
        });
    }

    public async Task<HubAuthResponse> SignInWithBrowserAsync(
        HubApiClient api,
        HubServerConfig serverConfig,
        CancellationToken ct)
    {
        if (!serverConfig.MicrosoftAuthEnabled || string.IsNullOrWhiteSpace(serverConfig.MicrosoftClientId))
        {
            throw new InvalidOperationException("当前组队大厅服务端尚未配置 Microsoft 登录（HUB_MS_CLIENT_ID）。");
        }

        var (listener, port) = StartLoopbackListener();
        using (listener)
        {
            var redirectUri = $"http://127.0.0.1:{port}/callback";
            var verifier = GenerateCodeVerifier();
            var challenge = ComputeCodeChallenge(verifier);
            var state = Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
            var authorizeUrl = BuildAuthorizeUrl(
                serverConfig.MicrosoftClientId,
                serverConfig.MicrosoftTenant,
                redirectUri,
                state,
                challenge);

            Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });

            var code = await WaitForCallbackCodeAsync(listener, state, ct).ConfigureAwait(false);
            var auth = await api.ExchangeMicrosoftCodeAsync(code, verifier, redirectUri, ct).ConfigureAwait(false);
            SaveSession(auth.HubToken, auth.ExpiresAt, auth.Me);
            api.SetToken(auth.HubToken);
            return auth;
        }
    }

    private static async Task<string> WaitForCallbackCodeAsync(
        HttpListener listener,
        string expectedState,
        CancellationToken ct)
    {
        using var registration = ct.Register(() =>
        {
            try { listener.Stop(); }
            catch { }
        });

        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync().ConfigureAwait(false);
            }
            catch when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }

            var path = ctx.Request.Url?.AbsolutePath ?? "";
            if (!string.Equals(path, "/callback", StringComparison.OrdinalIgnoreCase))
            {
                await WriteCallbackHtmlAsync(ctx.Response, HttpStatusCode.NotFound,
                    "无效路径", "请关闭此页面，回到 AstralParty.Toys 重试。").ConfigureAwait(false);
                continue;
            }

            var error = ctx.Request.QueryString["error"];
            if (!string.IsNullOrWhiteSpace(error))
            {
                var desc = ctx.Request.QueryString["error_description"] ?? error;
                await WriteCallbackHtmlAsync(ctx.Response, HttpStatusCode.BadRequest,
                    "Microsoft 登录未完成", desc).ConfigureAwait(false);
                throw new InvalidOperationException($"Microsoft 登录已取消或失败：{desc}");
            }

            var receivedState = ctx.Request.QueryString["state"] ?? "";
            var code = ctx.Request.QueryString["code"] ?? "";
            if (!string.Equals(receivedState, expectedState, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(code))
            {
                await WriteCallbackHtmlAsync(ctx.Response, HttpStatusCode.BadRequest,
                    "登录校验失败", "回调参数无效或状态不匹配，请回到 AstralParty.Toys 重新登录。").ConfigureAwait(false);
                throw new InvalidOperationException("Microsoft OAuth 回调状态不匹配或缺少授权码。");
            }

            await WriteCallbackHtmlAsync(ctx.Response, HttpStatusCode.OK,
                "登录成功 · AstralParty.Toys",
                "已收到 Microsoft 登录授权，可以关闭此标签页并返回工具箱。").ConfigureAwait(false);
            return code;
        }

        throw new OperationCanceledException(ct);
    }

    private static async Task WriteCallbackHtmlAsync(
        HttpListenerResponse response,
        HttpStatusCode status,
        string heading,
        string body)
    {
        var html =
            "<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\">"
            + $"<title>{WebUtility.HtmlEncode(heading)}</title>"
            + "<style>body{margin:0;min-height:100vh;display:grid;place-items:center;background:#10121e;color:#f1f5f9;font-family:system-ui,-apple-system,sans-serif;}"
            + ".card{max-width:440px;padding:28px 30px;border-radius:14px;background:#191c2e;border:1px solid rgba(255,255,255,.12);box-shadow:0 18px 42px rgba(0,0,0,.45);}"
            + "h1{margin:0 0 10px;font-size:20px;color:#ff4d97;}p{margin:0;line-height:1.65;color:#cbd5e1;font-size:14px;}</style>"
            + $"</head><body><div class=\"card\"><h1>{WebUtility.HtmlEncode(heading)}</h1><p>{WebUtility.HtmlEncode(body)}</p></div></body></html>";

        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = (int)status;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.Close();
    }

    private static (HttpListener Listener, int Port) StartLoopbackListener()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            int port;
            using (var tcp = new TcpListener(IPAddress.Loopback, 0))
            {
                tcp.Start();
                port = ((IPEndPoint)tcp.LocalEndpoint).Port;
            }

            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            try
            {
                listener.Start();
                return (listener, port);
            }
            catch
            {
                try { listener.Close(); }
                catch { }
            }
        }

        throw new InvalidOperationException("无法在本机回环端口启动 OAuth 回调监听。");
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
