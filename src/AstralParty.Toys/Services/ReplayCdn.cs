namespace AstralParty.Toys.Services;

/// <summary>
/// 回放 CDN 的区服端点。编号刻意与游戏 <c>GameLogic.ReplayLogic.ChangeReplayCDNUrl(index)</c>
/// 的入参一致（0 = 国际服测试、1 = 国际服正式、2 = 国服测试、3 = 国服正式），
/// 这样界面选项、操作日志与反编译代码三者能直接对上。
/// </summary>
public enum ReplayCdnEndpoint
{
    JpDev = 0,
    JpProd = 1,
    CnDev = 2,
    CnProd = 3
}

/// <summary>
/// 回放 CDN 的地址与回放 ID 校验。
///
/// 游戏侧（已确认）就一句话：<c>url = _cdnBaseUrl + replayId</c>，没有扩展名、没有查询串，
/// 4 个 base 分别来自 <c>ReplayLogic</c> 的 <c>_cdnBaseUrl_INT_DEV / _INT / _CN_DEV / _CN</c> 常量，
/// 默认值是国服正式 <c>https://sereplaycn.feimogames.com/prod/</c>。
/// 客户端只负责下载，回放文件本身是服务端录制的消息流。
/// </summary>
public static class ReplayCdn
{
    /// <summary>单局回放 ID 的最大长度（真实 ID 是 <c>GameFinishS2C.ReplayId</c> 的十进制字符串，实测 16 位）。</summary>
    public const int MaxReplayIdLength = 32;

    public static string BaseUrl(ReplayCdnEndpoint endpoint) => endpoint switch
    {
        ReplayCdnEndpoint.JpDev => "https://sereplayjp.feimogames.com/dev/",
        ReplayCdnEndpoint.JpProd => "https://sereplayjp.feimogames.com/prod/",
        ReplayCdnEndpoint.CnDev => "https://sereplaycn.feimogames.com/dev/",
        _ => "https://sereplaycn.feimogames.com/prod/"
    };

    /// <summary>界面上显示的名字（人话版，不出现域名）。</summary>
    public static string Label(ReplayCdnEndpoint endpoint) => endpoint switch
    {
        ReplayCdnEndpoint.JpDev => "国际服（测试服）",
        ReplayCdnEndpoint.JpProd => "国际服（正式服）",
        ReplayCdnEndpoint.CnDev => "国服（测试服）",
        _ => "国服（正式服）"
    };

    /// <summary>下载地址：游戏用的就是这个拼法。</summary>
    public static string Url(ReplayCdnEndpoint endpoint, string replayId) => BaseUrl(endpoint) + replayId;

    /// <summary>
    /// 按游戏档案的区服推断默认端点：国际服走国际服正式，其余（国服 / TapTap / 自定义）走国服正式。
    /// </summary>
    public static ReplayCdnEndpoint FromEdition(string? edition)
        => string.Equals(edition, "global", StringComparison.OrdinalIgnoreCase)
            ? ReplayCdnEndpoint.JpProd
            : ReplayCdnEndpoint.CnProd;

    /// <summary>解析界面传来的端点标识：接受枚举名（<c>CnProd</c>，大小写不敏感）或编号（<c>3</c>）。</summary>
    public static ReplayCdnEndpoint? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        // Enum.TryParse 对 "9" 这种越界数字也会返回 true，所以必须再用 IsDefined 收口
        return Enum.TryParse<ReplayCdnEndpoint>(value.Trim(), ignoreCase: true, out var endpoint)
               && Enum.IsDefined(endpoint)
            ? endpoint
            : null;
    }

    /// <summary>
    /// 回放 ID 会被直接拼进下载 URL，也会当成库目录名，所以只认纯数字——
    /// 挡掉 <c>../</c> 这类路径穿越、以及能改写 URL 的 <c>/ ? #</c> 等字符。
    /// </summary>
    public static bool IsValidReplayId(string? replayId, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(replayId))
        {
            error = "请填写回放 ID。";
            return false;
        }
        var text = replayId.Trim();
        if (text.Length > MaxReplayIdLength)
        {
            error = $"回放 ID 不该超过 {MaxReplayIdLength} 位（收到 {text.Length} 位）。";
            return false;
        }
        foreach (var character in text)
        {
            if (character is < '0' or > '9')
            {
                error = "回放 ID 只能是数字——就是游戏战绩里那一长串数字。";
                return false;
            }
        }
        return true;
    }
}
