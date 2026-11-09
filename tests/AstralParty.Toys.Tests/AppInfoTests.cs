using AstralParty.Toys.Models;

namespace AstralParty.Toys.Tests;

/// <summary>「关于 / 版本详情」里展示的版本与当前加载器安装信息。</summary>
public sealed class AppInfoTests
{
    [Fact]
    public void Get_WithoutLoaderStatus_OnlyReportsProgramAndEmbeddedVersions()
    {
        var info = AppInfo.Get();

        Assert.False(info.LoaderStatusKnown);
        Assert.Equal(ModManager.BundleLoaderVersion, info.EmbeddedLoaderVersion);
        Assert.Equal(ModManager.BundleSdkVersion, info.EmbeddedSdkVersion);
        Assert.DoesNotContain("已安装加载器", info.DetailText);
    }

    [Fact]
    public void Get_WithLoaderStatus_ReportsInstalledLoaderSdkAndMods()
    {
        var status = new ModStatus
        {
            GameDirectory = @"C:\Games\AstralParty",
            GameRunning = true,
            LoaderPresent = true,
            LoaderManaged = true,
            LoaderOrigin = "installed",
            InstalledVersion = "2.5.1",
            InstalledSdkVersion = "2.5.1",
            Mods =
            {
                new ModEntryInfo { Name = "ActivityLogMod" },
                new ModEntryInfo { Name = "SpeedhackMod" }
            }
        };

        var info = AppInfo.Get(status);

        Assert.True(info.LoaderStatusKnown);
        Assert.True(info.GameRunning);
        Assert.True(info.LoaderPresent);
        Assert.Equal("installed", info.LoaderOrigin);
        Assert.Equal("2.5.1", info.InstalledLoaderVersion);
        Assert.Equal("2.5.1", info.InstalledSdkVersion);
        Assert.Equal(2, info.InstalledModCount);
        Assert.Contains("已安装加载器: v2.5.1（通过本程序更新）", info.DetailText);
        Assert.Contains(@"游戏目录: C:\Games\AstralParty", info.DetailText);
        Assert.Contains("已装模组: 2 个", info.DetailText);
        Assert.Contains("游戏进程: 运行中", info.DetailText);
    }

    [Fact]
    public void Get_WithDisabledLoader_ReportsDisabledState()
    {
        var status = new ModStatus
        {
            GameDirectory = @"C:\Games\AstralParty",
            LoaderDisabled = true,
            LoaderManaged = true,
            InstalledVersion = "2.5.1"
        };

        var info = AppInfo.Get(status);

        Assert.Contains("已安装加载器: v2.5.1（已停用）", info.DetailText);
    }

    [Fact]
    public void Get_WithoutGameDirectory_ReportsMissingDirectory()
    {
        var info = AppInfo.Get(new ModStatus());

        Assert.True(info.LoaderStatusKnown);
        Assert.Contains("已安装加载器: 未找到游戏目录", info.DetailText);
    }
}
