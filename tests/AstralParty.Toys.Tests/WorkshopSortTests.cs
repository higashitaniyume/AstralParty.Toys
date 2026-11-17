using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>市场排序：默认「最近更新」，并记住用户选过的方式。</summary>
public sealed class WorkshopSortTests
{
    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(TestPaths.WebUiSourceDirectory, relativePath));

    [Fact]
    public void Market_DefaultsToRecentlyUpdated()
    {
        var js = Read("js/workshop/module.js");
        Assert.Contains("const DEFAULT_SORT = 'updated';", js);
        Assert.Contains("sort: savedSort(),", js);
        // 进市场不再把排序重置回下载量
        Assert.DoesNotContain("state.sort = 'downloads';", js);
        Assert.Contains("state.sort = savedSort();", js);
    }

    [Fact]
    public void Market_RemembersUserChoice()
    {
        var js = Read("js/workshop/module.js");
        Assert.Contains("const SORT_KEY = 'workshop_sort';", js);
        Assert.Contains("rememberSort(state.sort);", js);
        Assert.Contains("savedSort()", js);
        // 只接受页面提供的三个排序值，手改 localStorage 不会把请求写坏
        Assert.Contains("SORT_VALUES.includes(value)", js);
    }

    [Fact]
    public void Market_SortSelectShowsRecentlyUpdatedFirst()
    {
        var html = Read("view-workshop.html");
        var updated = html.IndexOf("value=\"updated\"", StringComparison.Ordinal);
        var downloads = html.IndexOf("value=\"downloads\"", StringComparison.Ordinal);
        var name = html.IndexOf("value=\"name\"", StringComparison.Ordinal);
        Assert.True(updated >= 0 && downloads >= 0 && name >= 0);
        Assert.True(updated < downloads && downloads < name, "排序下拉应按「最近更新 / 下载量 / 名称」排列");
    }

    [Fact]
    public void Market_HostFallsBackToRecentlyUpdated()
    {
        var source = File.ReadAllText(Path.Combine(
            TestPaths.AppSourceDirectory, "HybridWindow.Workshop.cs"));
        Assert.Contains("\"updated\" : sort", source);
        Assert.DoesNotContain("\"downloads\" : sort", source);
    }
}
