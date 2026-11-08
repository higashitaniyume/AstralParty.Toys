namespace AstralParty.Toys.Services.App;

/// <summary>
/// 网页唤起工具箱的 <c>aspatoys://</c> 协议，以及工具箱对外打开网页时带上的来源标识。
///
/// 协议格式（网页端生成，工具箱端解析）：
/// <c>aspatoys://install-mod?id=&lt;模组ID&gt;&amp;version=&lt;版本&gt;</c>，版本可省略（打开详情后默认装最新版）。
/// 工具箱收到后只打开工坊详情页，由用户确认才安装，不会在外部网页点一下就静默安装。
///
/// 对外打开的 http(s) 链接统一带上 <c>?from=app.aspatoys.hiynet.localhost</c>，
/// 让目标站点能认出来源是工具箱（内部域名本身只在工具箱内有效，不会真的请求到它）。
///
/// 协议注册只在安装器里做（见 installer/Setup.iss，受安装选项控制）；
/// 单文件版和便携版不注册，程序启动时也不自注册。
/// </summary>
public static class AspatoysProtocol
{
    /// <summary>自定义协议名（安装器注册到 <c>HKCU\Software\Classes\aspatoys</c>）。</summary>
    public const string Scheme = "aspatoys";

    /// <summary>工具箱内部域名（主页面来源）。</summary>
    public const string InternalHost = "app.aspatoys.hiynet.localhost";

    /// <summary>工具箱内部来源完整地址。</summary>
    public const string InternalOrigin = "https://app.aspatoys.hiynet.localhost";

    /// <summary>对外链接上标识来源的查询参数名。</summary>
    public const string FromParameter = "from";

    private const string InstallAction = "install-mod";

    /// <summary>是否是 <c>aspatoys://</c> 链接（不保证动作合法，只判断协议）。</summary>
    public static bool IsProtocolUrl(string? input)
        => !string.IsNullOrWhiteSpace(input)
           && Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)
           && string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 解析安装模组链接。成功时返回模组 ID 与可选版本；链接格式不对、ID 或版本不合法时返回 false。
    /// 除了标准格式 <c>aspatoys://install-mod?id=…</c>，也认路径式 <c>aspatoys://install/&lt;id&gt;</c>，
    /// 方便网页端以后换写法；生成链接时请用 <see cref="BuildInstallModUrl"/> 的标准格式。
    /// </summary>
    public static bool TryParseInstallMod(string? input, out string modId, out string? version)
    {
        modId = "";
        version = null;
        if (string.IsNullOrWhiteSpace(input)) return false;
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)) return false;
        if (!string.Equals(uri.Scheme, Scheme, StringComparison.OrdinalIgnoreCase)) return false;

        var host = uri.Host;
        var path = uri.AbsolutePath.Trim('/');
        var segments = path.Length == 0
            ? Array.Empty<string>()
            : path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                  .Select(Uri.UnescapeDataString)
                  .ToArray();

        var query = ParseQuery(uri.Query);

        string? idFromQuery = query.TryGetValue("id", out var queryId) ? queryId : null;
        if (string.Equals(host, InstallAction, StringComparison.OrdinalIgnoreCase))
        {
            // aspatoys://install-mod?id=…（标准）或 aspatoys://install-mod/<id>
            var id = !string.IsNullOrWhiteSpace(idFromQuery) ? idFromQuery
                : segments.Length > 0 ? segments[0] : null;
            if (!IsValidModId(id)) return false;
            modId = id!.Trim();
        }
        else if (string.Equals(host, "install", StringComparison.OrdinalIgnoreCase))
        {
            // aspatoys://install/<id>
            var id = segments.Length > 0 ? segments[0] : idFromQuery;
            if (!IsValidModId(id)) return false;
            modId = id!.Trim();
        }
        else if (string.IsNullOrEmpty(host) && segments.Length > 0
                 && string.Equals(segments[0], InstallAction, StringComparison.OrdinalIgnoreCase))
        {
            // aspatoys:///install-mod?id=…
            var id = !string.IsNullOrWhiteSpace(idFromQuery) ? idFromQuery
                : segments.Length > 1 ? segments[1] : null;
            if (!IsValidModId(id)) return false;
            modId = id!.Trim();
        }
        else
        {
            return false;
        }

        if (query.TryGetValue("version", out var requestedVersion))
        {
            if (string.IsNullOrWhiteSpace(requestedVersion)) return false;
            if (!IsValidVersion(requestedVersion)) return false;
            version = requestedVersion.Trim();
        }

        return true;
    }

    /// <summary>生成标准安装链接：<c>aspatoys://install-mod?id=…&amp;version=…</c>。</summary>
    public static string BuildInstallModUrl(string modId, string? version = null)
    {
        var url = $"{Scheme}://{InstallAction}?id=" + Uri.EscapeDataString(modId.Trim());
        if (!string.IsNullOrWhiteSpace(version))
            url += "&version=" + Uri.EscapeDataString(version.Trim());
        return url;
    }

    /// <summary>
    /// 给对外打开的 http(s) 链接带上工具箱内部域名来源（<c>?from=app.aspatoys.hiynet.localhost</c>）。
    /// 已有同名参数、本身就是内部域名、或不是 http(s) 时原样返回。
    /// </summary>
    public static string? WithInternalOrigin(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return url;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return url;
        if (string.Equals(uri.Host, InternalHost, StringComparison.OrdinalIgnoreCase)) return url;

        var query = ParseQuery(uri.Query);
        if (query.ContainsKey(FromParameter)) return url;

        var builder = new UriBuilder(uri);
        var existing = uri.Query.StartsWith('?') ? uri.Query[1..] : uri.Query;
        var addition = $"{FromParameter}={InternalHost}";
        builder.Query = string.IsNullOrEmpty(existing) ? addition : existing + "&" + addition;
        return builder.Uri.AbsoluteUri;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var text = query.StartsWith('?') ? query[1..] : query;
        if (string.IsNullOrEmpty(text)) return result;
        foreach (var pair in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            var key = equals >= 0 ? pair[..equals] : pair;
            var value = equals >= 0 ? pair[(equals + 1)..] : "";
            result[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return result;
    }

    /// <summary>模组 ID 只认字母、数字和 <c>_ - .</c>，挡掉路径分隔、空格和控制字符。</summary>
    private static bool IsValidModId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var id = value.Trim();
        if (id.Length > 128 || id is "." or "..") return false;
        return id.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.');
    }

    /// <summary>版本只认字母、数字和 <c>. - _ +</c>（如 <c>1.2.0</c>、<c>1.0.0-beta.1</c>）。</summary>
    private static bool IsValidVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var version = value.Trim();
        if (version.Length > 64) return false;
        return version.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' or '+');
    }
}
