using System.Windows.Media.Imaging;
using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;

namespace AstralParty.Toys.Tests;

public sealed class DefaultSkinCoverageTests
{
    [Fact]
    public void EmbeddedDefault_ContainsEveryConfiguredCardImage()
    {
        var config = TestPaths.CreateAnalyzer().Config;
        Assert.True(config.CardAssetKeys.Count() >= 192);
        Assert.All(config.CardAssetKeys, key => Assert.True(DefaultSkinPackage.Contains(key + ".png"), $"默认包缺少官方卡面 {key}"));
    }

    [Theory]
    [InlineData(40001, "命运卡", "UT_Destiny_40001")]
    [InlineData(30001, "事件卡", "UT_Event_30001")]
    [InlineData(32131, "地图事件", "UT_MapEvent_32131")]
    [InlineData(32132, "地图事件", "UT_MapEvent_32132")]
    [InlineData(32133, "地图事件", "UT_MapEvent_32133")]
    [InlineData(32134, "地图事件", "UT_MapEvent_32134")]
    public void OldInstalledSkin_StillListsConfiguredCategoriesWithOfficialNames(int id, string category, string key)
    {
        using var sandbox = new TestSandbox("skin-config-coverage");
        var manager = new SkinManager(TestPaths.AppDirectory);
        var card = Assert.Single(manager.GetCards(sandbox.Root, "custom"), c => c.AssetKey == key);
        Assert.Equal(id, card.CardId);
        Assert.Equal(category, card.Category);
        Assert.NotEqual($"卡牌 {id}", card.CardName);
        Assert.NotEqual(id.ToString(), card.CardName);
        Assert.True(card.CanRestore);
    }

    [Theory]
    [InlineData("../UT_HandCard_21026.png")]
    [InlineData("..\\UT_HandCard_21026.png")]
    [InlineData("skin.json")]
    [InlineData("unknown.png")]
    public void EmbeddedPreview_RejectsNonImageAndUnknownPaths(string path)
    {
        Assert.Null(DefaultSkinPackage.OpenCard(path));
    }

    [Theory]
    [InlineData(21026)]
    [InlineData(21027)]
    [InlineData(21028)]
    [InlineData(21029)]
    public void BuildingCardOriginals_AreRealReadableCardSizedPngs(int id)
    {
        using var sandbox = new TestSandbox("building-originals");
        DefaultSkinPackage.RestoreCard(sandbox.Root, $"UT_HandCard_{id}.png");
        using var stream = DefaultSkinPackage.OpenCard($"UT_HandCard_{id}.png");
        Assert.NotNull(stream);
        using var restored = File.OpenRead(Path.Combine(sandbox.Root, $"UT_HandCard_{id}.png"));
        Assert.Equal(restored.Length, stream.Length);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var image = Assert.Single(decoder.Frames);
        Assert.Equal(404, image.PixelWidth);
        Assert.Equal(400, image.PixelHeight);
    }
}
