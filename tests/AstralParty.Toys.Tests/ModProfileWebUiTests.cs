using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>模组方案 / 多开当前档案选择器的前端接线（页面引用与消息名）不能被改断。</summary>
public sealed class ModProfileWebUiTests
{
    private static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(TestPaths.WebUiSourceDirectory, relativePath));

    [Fact]
    public void Index_LoadsModProfileModule()
    {
        var html = Read("index.html");
        Assert.Contains("js/v2/mod-profiles.js", html);
        Assert.True(
            html.IndexOf("js/v2/mod-profiles.js", StringComparison.Ordinal) <
            html.IndexOf("js/v2/mods.js", StringComparison.Ordinal),
            "模组方案模块要排在模组页之前加载");
    }

    [Fact]
    public void ModsPage_RendersProfileCard()
    {
        Assert.Contains("V2ModProfiles?.card", Read("js/v2/mods.js"));
    }

    [Fact]
    public void ModProfileModule_UsesHostProtocol()
    {
        var js = Read("js/v2/mod-profiles.js");
        foreach (var type in new[]
                 {
                     "modProfilesGet", "modProfileCapture", "modProfileUpdate", "modProfilePlan",
                     "modProfileApply", "modProfileRename", "modProfileDelete", "modProfileSetNote",
                     "modProfileClearActive", "modHistoryList", "modHistoryRollback"
                 })
        {
            Assert.Contains(type, js);
        }
        Assert.Contains("window.V2ModProfiles", js);
    }

    [Fact]
    public void Home_ShowsProfilePicker()
    {
        var js = Read("js/v2/shell.js");
        Assert.Contains("v2HomeProfile", js);
        Assert.Contains("V2ModProfiles?.choose", js);
        Assert.Contains("window.V2Home", js);
    }

    [Fact]
    public void MultiProfilePage_HasCurrentPicker()
    {
        var html = Read("view-wiki-profiles.html");
        Assert.Contains("accountProfileCurrentSelect", html);
        Assert.Contains("profile.current_hint", html);

        // 模组方案只能在模组页 / 概览里切换：多开页不提供修改入口，避免两套 profile 互相干扰
        Assert.DoesNotContain("accountModProfileSelect", html);

        var js = Read("js/profiles.js");
        Assert.Contains("accountProfileCurrentSelect", js);
        Assert.Contains("renderCurrentSelect", js);
        Assert.Contains("accountProfileActivate", js);
        Assert.DoesNotContain("accountModProfileSelect", js);
        Assert.DoesNotContain("renderModProfilePicker", js);
    }

    [Fact]
    public void ManagerMenu_IsAboveModal()
    {
        var menuZ = ZIndex(Read("css/v2-layout.css"), "v2-menu");
        var modalZ = ZIndex(Read("css/v2-components.css"), "v2-modal");
        Assert.True(menuZ > modalZ, $"方案管理里的「更多」菜单要压在弹窗之上（menu={menuZ}, modal={modalZ}）");
    }

    [Fact]
    public void Selects_FollowLightTheme()
    {
        var css = Read("css/v2-components.css");
        Assert.Contains(":root[data-theme=\"light\"] .v2-select", css);
        Assert.Contains("color-scheme: light", css);
        Assert.Contains(".v2-select option", css);
    }

    [Theory]
    [InlineData("dict-zh-Hans.js")]
    [InlineData("dict-zh-Hant.js")]
    [InlineData("dict-en.js")]
    [InlineData("dict-ja.js")]
    public void Dictionaries_HaveCurrentProfileHint(string file)
    {
        var dict = Read(Path.Combine("js/i18n", file));
        Assert.Contains("'profile.current_hint'", dict);
    }

    private static int ZIndex(string css, string className)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            css, "\\." + className + "\\s*\\{[^}]*z-index:\\s*(\\d+)");
        Assert.True(match.Success, $"没有找到 .{className} 的 z-index");
        return int.Parse(match.Groups[1].Value);
    }
}
