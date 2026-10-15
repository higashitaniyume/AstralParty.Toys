using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

/// <summary>3.3.0 协议与配表必须成套更新，尤其是 Map 新插入的 protobuf 字段。</summary>
public sealed class Game330CompatibilityTests
{
    [Theory]
    [InlineData(21026, "疾行菇")]
    [InlineData(21027, "仙品杨桃")]
    [InlineData(21028, "芳香蜜桃")]
    [InlineData(21029, "招财树")]
    public void BuildingCards_HaveNamesAndBuildType(int id, string name)
    {
        var config = TestPaths.CreateAnalyzer().Config;
        Assert.Equal(name, config.Card(id));

        var protocol = CreateProtocol();
        var cards = protocol.Decode("CardConfigure", File.ReadAllBytes(Path.Combine(TestPaths.GameDataDirectory, "Card.bin")));
        var card = Assert.Single(ReflectionValue.Items(ReflectionValue.Get(cards, "Infos")), value => ReflectionValue.Int(value, "Id") == id);
        Assert.Equal("Build", ReflectionValue.Text(card, "CardType"));
    }

    [Fact]
    public void UpdatedCatalog_LoadsNewMapCharactersAndMonster()
    {
        var config = TestPaths.CreateAnalyzer().Config;
        Assert.Equal("森秋幻境", config.Map(82017));
        Assert.NotEmpty(config.MapAssets(82017));
        Assert.Contains(1001, config.MapIds);
        Assert.Contains(1002, config.MapIds);
        Assert.Contains(307, config.CharacterIds);
        Assert.Contains(308, config.CharacterIds);
        Assert.Equal("海盗王嘎呜", config.Monster(100401));
        Assert.Equal(90, config.RelicIds.Count());
    }

    [Theory]
    [InlineData(1118, "BuildingChangeS2C", "BuildId", 21026)]
    [InlineData(1119, "LandTypeChangeS2C", "NewLandType", 42)]
    public void BuildingNotifications_DecodeRealGeneratedPayload(int cmdId, string messageName, string field, int value)
    {
        var game = new GameProtocolTypes();
        var message = game.New("party.protocol." + messageName);
        game.SetNumber(message, field, value);
        game.SetNumber(message, "PlayerId", 12345678901L);
        var protocol = CreateProtocol();

        var decoded = protocol.TryDecode(cmdId, game.Serialize(message));

        Assert.NotNull(decoded);
        Assert.Equal(messageName, protocol.MessageName(cmdId));
        Assert.Equal(value, ReflectionValue.Int(decoded, field));
        Assert.Equal(12345678901L, ReflectionValue.Long(decoded, "PlayerId"));
    }

    private static GameProtocolContext CreateProtocol() => new(Path.GetDirectoryName(TestPaths.ProtocolAssembly)!);
}
