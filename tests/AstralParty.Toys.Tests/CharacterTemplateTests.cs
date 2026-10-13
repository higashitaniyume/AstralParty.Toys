using AstralParty.Toys.Services;
using Xunit;
namespace AstralParty.Toys.Tests;
public sealed class CharacterTemplateTests
{
    private sealed record Skin(int ItemID,string CharacterLabel,string Character);
    private static (object Hero,object Skins,Skin Default,Skin Alternative) SourceFixture()
    {
        var original=new Skin(100114001,"Hero114","original-image");
        var alternative=new Skin(100114002,"Hero114_B","alternative-image");
        var otherHero=new Skin(100102002,"Hero102_B","other-image");
        var hero=new { Id=114,StandingPainting=701 };
        var skins=new { StandingPaintings=new[] {
            new { Id=702,SkinStandingPaintingConfigureItems=new[]{otherHero} },
            new { Id=701,SkinStandingPaintingConfigureItems=new[]{alternative,original} }
        }};
        return (hero,skins,original,alternative);
    }
    [Fact] public void UnspecifiedSourcePrefersDefaultEvenWhenItIsNotFirst()
    {
        var f=SourceFixture();
        Assert.Same(f.Default,CharacterTemplateExporter.SelectSourceSkin(f.Hero,f.Skins));
    }
    [Fact] public void SpecifiedSourceUsesItsOwnImagesAndAnimationLabelWithoutMutatingIdentity()
    {
        var f=SourceFixture();
        var selected=Assert.IsType<Skin>(CharacterTemplateExporter.SelectSourceSkin(f.Hero,f.Skins,100114002));
        Assert.Same(f.Alternative,selected);
        Assert.Equal(100114002,selected.ItemID);
        Assert.Equal("Hero114_B",selected.CharacterLabel);
        Assert.Equal("alternative-image",selected.Character);
        Assert.Equal(100114001,f.Default.ItemID);
        Assert.Equal("Hero114",f.Default.CharacterLabel);
        Assert.Same(f.Default,CharacterTemplateExporter.SelectSourceSkin(f.Hero,f.Skins,100114001));
    }
    [Theory]
    [InlineData(100102002)]
    [InlineData(999999999)]
    [InlineData(-1)]
    public void MissingOrOtherHeroSourceIsRejectedWithoutDefaultFallback(int source)
    {
        var f=SourceFixture();
        var error=Assert.Throws<InvalidOperationException>(()=>CharacterTemplateExporter.SelectSourceSkin(f.Hero,f.Skins,source));
        Assert.Contains(source.ToString(),error.Message);
        Assert.Contains("不会回退",error.Message);
    }
    [Fact] public void UnspecifiedSourceRetainsFirstEntryFallbackWhenDefaultIsAbsent()
    {
        var alternate=new Skin(100114003,"Hero114_C","third-image");
        var hero=new { Id=114,StandingPainting=701 };
        var skins=new { StandingPaintings=new[]{new { Id=701,SkinStandingPaintingConfigureItems=new[]{alternate} }} };
        Assert.Same(alternate,CharacterTemplateExporter.SelectSourceSkin(hero,skins));
        Assert.Throws<InvalidOperationException>(()=>CharacterTemplateExporter.SelectSourceSkin(hero,skins,100114001));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(100114002)]
    public void MissingHeroGroupCannotSelectAnotherGroupsSkin(int source)
    {
        var f=SourceFixture();
        var hero=new { Id=114,StandingPainting=999 };
        Assert.Throws<InvalidOperationException>(()=>CharacterTemplateExporter.SelectSourceSkin(hero,f.Skins,source));
    }
    [Fact] public async Task InvalidIdAndExistingPackageAreRejectedBeforeExtraction()
    {
        var root=Path.Combine(Path.GetTempPath(),"CharacterTemplateTests-"+Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(()=>CharacterTemplateExporter.ExportAsync(root,"missing","../escape","name",114,"missing","missing",_=>{}));
            var manager=new CharacterSkinManager();manager.Create(root,"existing",114,100114001,"keep original");
            var before=File.ReadAllText(Path.Combine(manager.Package(root,"existing"),"character-skin.json"));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>CharacterTemplateExporter.ExportAsync(root,"missing","existing","changed",114,"missing","missing",_=>{}));
            Assert.Equal(before,File.ReadAllText(Path.Combine(manager.Package(root,"existing"),"character-skin.json")));
            Assert.DoesNotContain(Directory.GetDirectories(manager.Root(root)),path=>Path.GetFileName(path).StartsWith(".template-"));
        }
        finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
