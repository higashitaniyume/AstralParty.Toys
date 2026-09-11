using AstralParty.Toys.Services;

namespace AstralParty.Toys.Tests.Support;

/// <summary>把一局合成回放写进沙箱并解析出报告，供多个测试类共用。</summary>
internal static class ReportFactory
{
    public static (ReplayAnalyzer Analyzer, ReplayFixtureOptions Options, ReplayReport Report) Create(
        TestSandbox sandbox, ReplayFixtureOptions? options = null)
    {
        options = ReplayFixture.Resolve(options ?? new ReplayFixtureOptions());
        var directory = Path.Combine(sandbox.Root, options.ReplayId);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, options.ReplayId);
        File.WriteAllBytes(path, ReplayFixture.Build(options));

        var analyzer = TestPaths.CreateAnalyzer();
        return (analyzer, options, analyzer.Analyze(path));
    }

    /// <summary>把报告里引用到的所有素材路径摊平，便于逐个验证能否在内嵌素材里找到。</summary>
    public static IEnumerable<(string Label, string Path)> ReferencedAssets(ReplayReport report)
    {
        foreach (var (key, value) in report.UiAssets) yield return ($"界面素材 {key}", value);
        if (report.MapImagePath.Length > 0) yield return ("地图图", report.MapImagePath);
        foreach (var player in report.Players)
        {
            if (player.AvatarPath.Length > 0) yield return ($"头像 {player.Nickname}", player.AvatarPath);
        }
        foreach (var relic in report.Relics)
        {
            if (relic.ImagePath.Length > 0) yield return ($"筹码图标 {relic.RelicName}", relic.ImagePath);
            if (relic.SourceIconPath.Length > 0) yield return ($"来源图标 {relic.Source}", relic.SourceIconPath);
        }
        foreach (var timelineEvent in report.Events)
        {
            if (timelineEvent.IconPath.Length > 0) yield return ($"事件图标 {timelineEvent.Title}", timelineEvent.IconPath);
        }
    }
}
