using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>配表（角色 / 地图 / 怪物 / 筹码 / 物品）与内嵌素材的完整性，以及报告引用素材的可解析性。</summary>
public sealed class CatalogAndAssetTests
{
    [Fact]
    public void CharacterCatalog_HasOfficialNames()
    {
        var config = TestPaths.CreateAnalyzer().Config;

        Assert.True(config.CharacterIds.Count() >= 35, $"官方角色数量异常：{config.CharacterIds.Count()}");
        Assert.Equal("帕露南", config.Character(101));
        Assert.Equal("商业之主", config.CharacterNick(101));
        Assert.Equal("芬妮", config.Character(102));
        Assert.Equal("古怪神探", config.CharacterNick(102));
    }

    [Fact]
    public void Catalog_LoadsEveryTable()
    {
        var config = TestPaths.CreateAnalyzer().Config;

        Assert.NotEmpty(config.MapIds);
        Assert.NotEmpty(config.MonsterIds);
        Assert.NotEmpty(config.RelicIds);
        Assert.NotEmpty(config.ItemIds);

        // 配表与本地化对上了号：条目应当有真实名字，而不是退化成 ID 字符串
        Assert.True(Named(config.CharacterIds, config.Character) >= 35, "有名字的角色太少");
        Assert.True(Named(config.MapIds, config.Map) > 0, "地图没有名字");
        Assert.True(Named(config.MonsterIds, config.Monster) > 0, "怪物没有名字");
        Assert.True(Named(config.ItemIds, config.Item) > 0, "物品没有名字");
        Assert.All(config.RelicIds, id => Assert.DoesNotContain("筹码 ", config.Relic(id)));
    }

    /// <summary>统计有多少条目的名字不是「ID 字符串」这种兜底值。</summary>
    private static int Named(IEnumerable<int> ids, Func<int, string> name) =>
        ids.Count(id => name(id) != id.ToString());

    [Fact]
    public void Catalog_FallsBackForUnknownIds()
    {
        var config = TestPaths.CreateAnalyzer().Config;

        Assert.Equal("未知角色", config.Character(0));
        Assert.Equal("角色 999999", config.Character(999999));
        Assert.Equal("未知地图", config.Map(0));
        Assert.Equal("地图 999999", config.Map(999999));
        Assert.Equal("未知怪物", config.Monster(0));
        Assert.Equal("怪物 999999", config.Monster(999999));
        Assert.Equal("筹码 999999", config.Relic(999999));
        Assert.Equal("物品 999999", config.Item(999999));
    }

    [Fact]
    public void EmbeddedAssets_ArePresent()
    {
        // PackedAssets 里的 webp 全部以内嵌资源形式随程序发布
        Assert.True(EmbeddedAssetStore.Count >= 245, $"内嵌素材数量异常：{EmbeddedAssetStore.Count}");
    }

    [Fact]
    public void ReportAssets_ResolveToEmbeddedStreams()
    {
        using var sandbox = new TestSandbox("ap-assets-resolve");
        var (_, _, report) = ReportFactory.Create(sandbox);

        var referenced = ReportFactory.ReferencedAssets(report).ToList();
        Assert.NotEmpty(referenced);

        var unresolved = new List<string>();
        foreach (var (label, path) in referenced)
        {
            if (!EmbeddedAssetStore.TryGetRelativePath(path, out var relative))
            {
                unresolved.Add($"{label}：{path}");
                continue;
            }
            using var stream = EmbeddedAssetStore.OpenWebPath(relative);
            if (stream is null || stream.Length == 0) unresolved.Add($"{label}：{path} → {relative}");
        }

        Assert.Empty(unresolved);
    }

    [Fact]
    public void AiSummary_ContainsAllFiveSections()
    {
        using var sandbox = new TestSandbox("ap-ai-summary");
        var (_, options, report) = ReportFactory.Create(sandbox);

        var summary = HybridWindow.BuildRelicsAiSummary(report);

        Assert.True(summary.Length > 200, $"AI 摘要太短：{summary.Length} 字");
        Assert.Contains("一、对局背景基本信息", summary);
        Assert.Contains("二、参战玩家与角色战绩概况", summary);
        Assert.Contains("三、全局筹码统计摘要", summary);
        Assert.Contains("四、按回合详细筹码流转与决策时间线", summary);
        Assert.Contains("五、供 AI 智能分析与复盘的建议 Prompt 方向", summary);
        Assert.Contains(report.FileName, summary);
        Assert.Contains(report.MapName, summary);
        Assert.Contains(options.Nicks[0], summary);
    }
}
