using AstralParty.Toys.Services;

namespace AstralParty.Toys.Tests.Support;

/// <summary>
/// 真实回放 CDN 的取样：只有显式设置环境变量 <c>ASTRAL_TEST_REPLAY_ID</c> 才会真联网。
///
/// 「按 ID 下载」本身就是联网功能，合成响应只能证明落库逻辑，证明不了**官方 CDN 的地址拼法**
/// （<c>_cdnBaseUrl + replayId</c>）现在依然可用；所以留一条默认跳过、按需开启的真机用例，
/// 让测试套件在离线机器上仍然全绿。
/// </summary>
internal static class RealDownload
{
    public const string ReplayIdVariable = "ASTRAL_TEST_REPLAY_ID";
    public const string EndpointVariable = "ASTRAL_TEST_REPLAY_CDN";

    public static string? ReplayId
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(ReplayIdVariable)?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }
    }

    /// <summary>区服端点（<c>CnProd</c> / <c>JpProd</c> / 编号均可），默认国服正式服。</summary>
    public static ReplayCdnEndpoint Endpoint
        => ReplayCdn.TryParse(Environment.GetEnvironmentVariable(EndpointVariable)) ?? ReplayCdnEndpoint.CnProd;

    public static bool IsAvailable => ReplayId is not null;

    public static string SkipReason =>
        $"没有指定真实回放 ID：设置环境变量 {ReplayIdVariable}=<回放ID>（可选 {EndpointVariable}=CnProd）才会真联网下载。";
}

/// <summary>需要真实回放 CDN 的测试：没有指定 ID 时报告为「已跳过」，而不是失败。</summary>
public sealed class RealDownloadFactAttribute : FactAttribute
{
    public RealDownloadFactAttribute()
    {
        if (!RealDownload.IsAvailable) Skip = RealDownload.SkipReason;
    }
}
