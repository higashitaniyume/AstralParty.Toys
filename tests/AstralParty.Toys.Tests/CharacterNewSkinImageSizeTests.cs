using AstralParty.Toys.Services;
using AstralParty.Toys.Tests.Support;
using CesiumLoader.CharacterSkins;

namespace AstralParty.Toys.Tests;

public sealed class CharacterNewSkinImageSizeTests
{
    // Dimensions verified from Texture2D metadata in the local 3.3.0 bundles.
    [Theory]
    [InlineData(113, 100113006, 1416, 1612)]
    [InlineData(117, 100117006, 1261, 2048)]
    [InlineData(119, 100119005, 1857, 2048)]
    public void NewlyConfiguredSkinsUseVerifiedOriginalImageSizes(int heroId, int skinId, int width, int height)
    {
        var protocol = new GameProtocolContext(Path.GetDirectoryName(TestPaths.ProtocolAssembly)!);
        var manifest = new CharacterSkinManifest { HeroId = heroId, SkinItemId = skinId };
        CharacterImageTarget Resolve(string slot) => CharacterImageSize.Resolve(protocol, TestPaths.GameDataDirectory, manifest, slot);

        var portrait = Resolve("Character");
        Assert.True(portrait.Verified);
        Assert.Equal(width, portrait.Width);
        Assert.Equal(height, portrait.Height);

        var photo = Resolve("ProfilePhoto");
        Assert.True(photo.Verified);
        Assert.Equal((220, 220), (photo.Width, photo.Height));
        var thin = Resolve("CharacterThin");
        Assert.True(thin.Verified);
        Assert.Equal((128, 308), (thin.Width, thin.Height));

        foreach (string slot in CharacterSkinRules.ImageSlots)
        {
            var target = Resolve(slot);
            if (target.Asset.Length == 0) continue; // An absent SFW/alternate slot intentionally falls back to the game.
            Assert.True(target.Verified, $"{heroId}/{skinId}/{slot}: {target.Asset} is missing from the original-size table");
        }
    }
}
