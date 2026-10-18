using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>字节级版本升级：只改 1016.Version，帧头长度随字符串自动调整。</summary>
public sealed class ReplayVersionUpgraderTests
{
    [Fact]
    public void Upgrade_HandlesLongerVersionString()
    {
        var original = ReplayFixture.Create(new ReplayFixtureOptions
        {
            ReplayId = "1800000000000100",
            GameVersion = "3.2.0-2.0.0"
        });

        Assert.True(ReplayVersionUpgrader.TryUpgrade(original, "3.10.0-0.0.0", out var upgraded, out var oldVersion, out var error), error);
        Assert.Equal("3.2.0-2.0.0", oldVersion);
        Assert.True(upgraded.Length > original.Length);

        Assert.True(ReplayVersionUpgrader.TryReadVersion(upgraded, out var version, out error), error);
        Assert.Equal("3.10.0-0.0.0", version);

        var protocol = new GameProtocolContext(Path.Combine(TestPaths.AppDirectory, "Protocol"));
        Assert.True(protocol.TryParseSettlement(upgraded, out var finish, out _));
        Assert.Equal("3.10.0-0.0.0", ReflectionValue.Text(finish, "Version"));
    }

    [Theory]
    [InlineData("3.2.0-2.0.0", "3.3.0-0.0.0", -1)]
    [InlineData("3.3.0-0.0.0", "3.3.0-0.0.0", 0)]
    [InlineData("3.3.0-0.0.1", "3.3.0-0.0.0", 1)]
    [InlineData("3.10.0-0.0.0", "3.9.0-9.9.9", 1)]
    public void CompareGameVersion_UsesNumericParts(string left, string right, int expectedSign)
    {
        var actual = ReplayLibraryService.CompareGameVersion(left, right);
        Assert.Equal(expectedSign, Math.Sign(actual));
    }

    [RealReplayFact]
    public void RealReplay_UpgradesToCurrentVersion()
    {
        var path = RealReplay.FindFile()!;
        var bytes = File.ReadAllBytes(path);
        var target = SupportedVersion();
        Assert.True(ReplayVersionUpgrader.TryReadVersion(bytes, out var oldVersion, out var error), error);
        if (oldVersion == target) return;

        Assert.True(ReplayVersionUpgrader.TryUpgrade(bytes, target, out var upgraded, out _, out error), error);
        Assert.True(ReplayVersionUpgrader.TryReadVersion(upgraded, out var upgradedVersion, out error), error);
        Assert.Equal(target, upgradedVersion);

        var protocol = new GameProtocolContext(Path.Combine(TestPaths.AppDirectory, "Protocol"));
        Assert.True(protocol.TryParseSettlement(upgraded, out var finish, out _));
        Assert.Equal(target, ReflectionValue.Text(finish, "Version"));
    }

    private static string SupportedVersion()
    {
        var protocol = new GameProtocolContext(Path.Combine(TestPaths.AppDirectory, "Protocol"));
        var config = protocol.Decode("VersionConfigure", File.ReadAllBytes(Path.Combine(TestPaths.GameDataDirectory, "Version.bin")));
        var data = ReflectionValue.Items(ReflectionValue.Get(config, "DataDict"))
            .Select(item => ReflectionValue.Get(item, "Value"))
            .First(value => ReflectionValue.Text(value, "AppVersion").Length > 0);
        return $"{ReflectionValue.Text(data, "AppVersion")}-{ReflectionValue.Text(data, "ResVersion")}";
    }
}
