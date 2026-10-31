using System.Text.RegularExpressions;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;
using Xunit;

namespace AstralParty.Toys.Tests;

/// <summary>
/// 市场页面的接线与文案：页面确实挂进了导航/路由、宿主消息都有人接、
/// 用到的翻译键在四种语言里都有。真正的交互仍在 WebView2 里验证，这里只守住容易漏的接线。
/// </summary>
public sealed class WorkshopWebUiTests
{
    private const string WorkshopTranslations = "workshop.";
    private const string WorkshopSettingsTranslations = "settings.workshop";

    private static readonly Regex IndexKey = new(
        """data-i18n(?:-placeholder|-title)?\s*=\s*["']([^"']+)["']""", RegexOptions.Compiled);

    private static readonly Regex JsKey = new(
        """['"](workshop\.[A-Za-z0-9_]+)['"]""", RegexOptions.Compiled);

    [Fact]
    public void WorkshopPage_IsWiredIntoNavigationAndRouter()
    {
        var html = Read("index.html");

        Assert.Contains("data-page=\"workshop\"", html);
        Assert.Contains("id=\"workshopView\"", html);
        Assert.Contains("id=\"workshopGrid\"", html);
        Assert.Contains("css/workshop.css", html);
        Assert.Contains("js/workshop.js", html);
        // 设置页要能改市场地址
        Assert.Contains("id=\"workshopUrlInput\"", html);
        // 右下角安装进度浮窗
        Assert.Contains("id=\"workshopInstallToast\"", html);
        Assert.Contains("id=\"workshopInstallBar\"", html);
        Assert.Contains("id=\"workshopInstallCancel\"", html);

        var core = Read("js/core.js");
        Assert.Contains("pageName === 'workshop'", core);
        Assert.Contains("WorkshopModule?.onShow", core);

        // 宿主回推的每条消息都要有分支，否则页面永远停在加载中
        foreach (var message in new[]
                 {
                     "workshopList", "workshopDetail", "workshopProgress",
                     "workshopResult", "workshopInstallConfirm",
                     "workshopConsentRequired", "workshopConsent"
                 })
        {
            Assert.Contains($"case '{message}':", core);
        }
    }

    [Fact]
    public void WorkshopModule_UsesTheSameOutgoingMessageNamesAsTheHost()
    {
        var js = Read("js/workshop.js");

        foreach (var message in new[]
                 {
                     "workshopList", "workshopDetail", "workshopInstall",
                     "workshopCancelDownload", "workshopInstallConfirmAnswer",
                     "workshopConsent"
                 })
        {
            Assert.Contains($"type: '{message}'", js);
        }
    }

    [Fact]
    public void EveryWorkshopTranslationKey_ExistsInAllFourLanguages()
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var match in JsKey.Matches(Read("js/workshop.js")).Cast<Match>())
            keys.Add(match.Groups[1].Value);
        foreach (var match in IndexKey.Matches(Read("index.html")).Cast<Match>())
        {
            var key = match.Groups[1].Value;
            if (key.StartsWith(WorkshopTranslations, StringComparison.Ordinal)
                || key.StartsWith(WorkshopSettingsTranslations, StringComparison.Ordinal))
                keys.Add(key);
        }

        keys.Add("nav.workshop");

        Assert.Contains("workshop.install_version", keys);
        Assert.Contains("workshop.downgrade_message", keys);
        Assert.Contains("workshop.perm_files", keys);
        Assert.Contains("settings.workshop_url", keys);

        var translations = Read("js/i18n.js");
        foreach (var key in keys)
        {
            var occurrences = Regex.Matches(translations, Regex.Escape($"'{key}'")).Count;
            Assert.True(occurrences >= 4, $"翻译键 {key} 只在 {occurrences} 种语言里出现（需要 4 种）");
        }
    }

    [Fact]
    public void EveryIndexTranslationKey_ExistsInAllFourLanguages()
    {
        var keys = IndexKey.Matches(Read("index.html"))
            .Cast<Match>()
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(keys);

        var translations = Read("js/i18n.js");
        foreach (var key in keys)
        {
            var occurrences = Regex.Matches(translations, Regex.Escape($"'{key}'")).Count;
            Assert.True(occurrences >= 4, $"页面用到的 {key} 只在 {occurrences} 种语言里出现（需要 4 种）");
        }
    }

    [Fact]
    public void WorkshopScriptsAndStyles_AreEmbedded()
    {
        Assert.Contains("js/workshop.js", EmbeddedWebStore.Paths);
        Assert.Contains("css/workshop.css", EmbeddedWebStore.Paths);
    }

    private static string Read(string relativePath)
    {
        using var stream = EmbeddedWebStore.Open(relativePath);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        return reader.ReadToEnd();
    }
}
