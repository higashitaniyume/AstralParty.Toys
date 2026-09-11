using AstralParty.Toys.Services;

namespace AstralParty.Toys.Tests;

/// <summary>工具链自检：测试输出目录里必须能拿到协议程序集与配表，否则后面的解析类测试全都没意义。</summary>
public sealed class EnvironmentSanityTests
{
    [Fact]
    public void AppDirectory_ContainsProtocolAssembly()
    {
        Assert.True(File.Exists(Support.TestPaths.ProtocolAssembly),
            $"缺少游戏协议程序集：{Support.TestPaths.ProtocolAssembly}");
    }

    [Fact]
    public void AppDirectory_ContainsGameData()
    {
        Assert.True(Directory.Exists(Support.TestPaths.GameDataDirectory),
            $"缺少配表目录：{Support.TestPaths.GameDataDirectory}");
        Assert.Contains("Character.bin", Directory.GetFiles(Support.TestPaths.GameDataDirectory).Select(Path.GetFileName));
    }

    [Fact]
    public void Analyzer_CanBeConstructed_AndReadsCatalog()
    {
        var analyzer = Support.TestPaths.CreateAnalyzer();
        Assert.True(analyzer.Config.CharacterIds.Count() >= 35, "官方角色数量异常");
        Assert.Equal("帕露南", analyzer.Config.Character(101));
    }
}
