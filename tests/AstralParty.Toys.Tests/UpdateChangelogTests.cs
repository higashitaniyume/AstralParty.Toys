using System.IO.Compression;
using System.Text;
using AstralParty.Toys.Services;
using AstralParty.Toys.Services.App;
using AstralParty.Toys.Services.Modding;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public sealed class UpdateChangelogTests
{
    [Fact]
    public void NormalizeHeader_ReplacesLegacyIntroWithoutTrimmingVersions()
    {
        var legacy = """
            # 更新日志 / Changelog

            本文件按版本记录 `AstralParty.Toys` 的功能变更。版本号与 GitHub 发布 tag 一一对应（如 `v0.2.0`），发布产物由 [release.yml](.github/workflows/release.yml) 在打 tag 时自动构建。

            ## [v2.4.6] - 2026-11-24

            - 适配双文件布局

            ## [v2.4.5] - 2026-11-23

            - 统一配置目录
            """;

        var normalized = UpdateChangelogService.NormalizeHeader(legacy, isLoader: false);

        Assert.DoesNotContain("在打 tag 时自动构建", normalized);
        Assert.Contains("1078464597", normalized);
        Assert.Contains("https://discord.gg/SZaYSwGX94", normalized);
        Assert.Contains("https://github.com/higashitaniyume/AstralParty.Toys/issues", normalized);
        Assert.Contains("## [v2.4.6] - 2026-11-24", normalized);
        Assert.Contains("## [v2.4.5] - 2026-11-23", normalized);
    }

    [Fact]
    public void NormalizeHeader_LoaderReplacesLegacyIntroWithLoaderIssues()
    {
        var legacy = """
            # 更新日志 / Changelog

            本文件按版本记录 `CesiumLoader` 的功能变更。版本号与 GitHub 发布 tag 一一对应（如 `modloader-3.3.2`，版本号为 `3.3.2`），发布产物由 [release-modloader.yml](.github/workflows/release-modloader.yml) 在打 tag 时自动构建。

            ## [4.1.4] - 2026-11-25

            - 静态链接运行库
            """;

        var normalized = UpdateChangelogService.NormalizeHeader(legacy, isLoader: true);

        Assert.DoesNotContain("在打 tag 时自动构建", normalized);
        Assert.Contains("https://github.com/higashitaniyume/CesiumLoader/issues", normalized);
        Assert.Contains("## [4.1.4] - 2026-11-25", normalized);
    }

    [Fact]
    public void EvaluateStartup_FreshInstall_TriggersFreshInstallPopup()
    {
        var cfg = new UpdateConfigSection { AutoChangelogPopup = true, LastSeenAppVersion = "" };

        var decision = UpdateChangelogService.EvaluateStartup("2.4.6", "4.1.1", null, cfg);

        Assert.NotNull(decision);
        Assert.Equal("fresh-install", decision!.Trigger);
        Assert.Equal("2.4.6", decision.CurrentAppVersion);
        Assert.Equal("4.1.1", decision.CurrentLoaderVersion);
        Assert.True(decision.LoaderChanged);
    }

    [Fact]
    public void EvaluateStartup_UpgradedVersion_TriggersAppUpdatedWithLoaderDiffFlag()
    {
        var cfg = new UpdateConfigSection
        {
            AutoChangelogPopup = true,
            LastSeenAppVersion = "2.4.5",
            LastSeenEmbeddedLoaderVersion = "4.0.0"
        };

        var decision = UpdateChangelogService.EvaluateStartup("2.4.6", "4.1.1", null, cfg);

        Assert.NotNull(decision);
        Assert.Equal("app-updated", decision!.Trigger);
        Assert.Equal("2.4.5", decision.PrevAppVersion);
        Assert.Equal("2.4.6", decision.CurrentAppVersion);
        Assert.Equal("4.0.0", decision.PrevLoaderVersion);
        Assert.Equal("4.1.1", decision.CurrentLoaderVersion);
        Assert.True(decision.LoaderChanged);
    }

    [Fact]
    public void EvaluateStartup_SameVersionWithoutOutcome_DoesNotPopup()
    {
        var cfg = new UpdateConfigSection
        {
            AutoChangelogPopup = true,
            LastSeenAppVersion = "2.4.6",
            LastSeenEmbeddedLoaderVersion = "4.1.1"
        };

        var decision = UpdateChangelogService.EvaluateStartup("2.4.6", "4.1.1", null, cfg);

        Assert.Null(decision);
    }

    [Fact]
    public void EvaluateStartup_FailedOrBusySelfUpdate_DoesNotPopup()
    {
        var cfg = new UpdateConfigSection { AutoChangelogPopup = true, LastSeenAppVersion = "2.4.5" };

        Assert.Null(UpdateChangelogService.EvaluateStartup("2.4.5", "4.0.0", new SelfUpdateOutcome(AppSelfUpdater.ResultBusy, "2.4.6"), cfg));
        Assert.Null(UpdateChangelogService.EvaluateStartup("2.4.5", "4.0.0", new SelfUpdateOutcome(AppSelfUpdater.ResultRestored, "2.4.6"), cfg));
    }

    [Fact]
    public void EvaluateStartup_WhenDisabledPermanently_ReturnsNull()
    {
        var cfg = new UpdateConfigSection { AutoChangelogPopup = false, LastSeenAppVersion = "" };

        Assert.Null(UpdateChangelogService.EvaluateStartup("2.4.6", "4.1.1", null, cfg));
    }

    [Fact]
    public void ConfigStore_PersistsAutoChangelogPopupAndSeenVersions()
    {
        using var sandbox = new TestSandbox(nameof(ConfigStore_PersistsAutoChangelogPopupAndSeenVersions));
        var store = ToysConfigStore.ForRoot(sandbox.Root);

        var initial = store.Load();
        Assert.True(initial.Update.AutoChangelogPopup);
        Assert.Equal("", initial.Update.LastSeenAppVersion);

        store.Update(cfg =>
        {
            cfg.Update.AutoChangelogPopup = false;
            cfg.Update.LastSeenAppVersion = " 2.4.6 ";
            cfg.Update.LastSeenEmbeddedLoaderVersion = " 4.1.1 ";
        });

        var reloaded = store.Load();
        Assert.False(reloaded.Update.AutoChangelogPopup);
        Assert.Equal("2.4.6", reloaded.Update.LastSeenAppVersion);
        Assert.Equal("4.1.1", reloaded.Update.LastSeenEmbeddedLoaderVersion);
    }

    [Fact]
    public void ExtractChangelogFromPackage_ReadsChangelogAndSkipsPlaceholder()
    {
        byte[] BuildZip(string content)
        {
            using var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry("CHANGELOG.md");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
            return ms.ToArray();
        }

        var realBytes = BuildZip("# 更新日志\n\n## [4.1.4]\n- test");
        Assert.Contains("## [4.1.4]", ModManager.ExtractChangelogFromPackage(realBytes));

        var placeholderBytes = BuildZip("<!-- placeholder -->");
        Assert.Equal("", ModManager.ExtractChangelogFromPackage(placeholderBytes));
        Assert.Equal("", ModManager.ExtractChangelogFromPackage(null));
    }

    [Fact]
    public void BundledAppChangelog_UsesWelcomingHeaderAndRetainsFullHistory()
    {
        using var stream = typeof(AppInfo).Assembly.GetManifestResourceStream("AstralParty.Toys.CHANGELOG.md");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!, Encoding.UTF8);
        var text = reader.ReadToEnd();

        Assert.DoesNotContain("在打 tag 时自动构建", text);
        Assert.Contains("1078464597", text);
        Assert.Contains("## [v2.4.6]", text);
        Assert.Contains("## [v0.2.0]", text);
    }
}
