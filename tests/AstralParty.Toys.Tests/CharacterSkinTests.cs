using System.IO.Compression;
using System.Text.Json;
using AstralParty.Toys.Services;
using CesiumLoader.CharacterSkins;
using Xunit;
namespace AstralParty.Toys.Tests;
public sealed class CharacterSkinTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "character-skins-test-" + Guid.NewGuid().ToString("N"));
    private readonly CharacterSkinManager manager = new();
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
    [Fact] public void DifferentHeroesCoexistAndWildcardReplacesSameHero()
    {
        manager.Create(root,"blue",114,100114001,"蓝海晴");manager.Create(root,"blue-alt",114,100114002,"蓝海晴2");manager.Create(root,"fenny",102,0,"芬妮");manager.Create(root,"all-blue",114,0,"全部蓝海晴");
        manager.SetActive(root,"blue",true);manager.SetActive(root,"blue-alt",true);manager.SetActive(root,"fenny",true);
        Assert.Equal(3,manager.Settings(root).ActivePackages.Count);
        manager.SetActive(root,"all-blue",true);
        Assert.Equal(new[]{"fenny","all-blue"},manager.Settings(root).ActivePackages);
    }
    [Theory] [InlineData("../escape.png")] [InlineData("C:/escape.png")] [InlineData("images/../../escape.png")] [InlineData("/escape.png")] [InlineData("images\\..\\escape.png")]
    public void RejectsEscapingPaths(string relative) => Assert.ThrowsAny<Exception>(()=>CharacterSkinRules.SafePath(root,relative));
    [Fact] public void ExportImportPreservesFramesAndTexts()
    {
        manager.Create(root,"skin",114,0,"演示");
        string frame=Path.Combine(root,"frame.png");File.WriteAllBytes(frame,new byte[]{1,2,3});
        manager.ReplaceImage(root,"skin","CharacterMap",frame);
        manager.ImportFrames(root,"skin","Idle",new[]{frame},12,true);
        var m=manager.Load(root,"skin");m.Texts["Name"]="自制名字";m.UseOriginalSpriteLayout=true;manager.Save(root,"skin",m);
        string zip=Path.Combine(root,"skin.zip");manager.Export(root,"skin",zip);
        string other=Path.Combine(root,"another-game");Assert.Equal("skin",manager.Import(other,zip));
        var imported=manager.Load(other,"skin");Assert.Equal("自制名字",imported.Texts["Name"]);Assert.Equal(12,imported.Animations["Idle"].Fps);
        Assert.True(imported.UseOriginalSpriteLayout);
        Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(CharacterSkinRules.SafePath(manager.Package(other,"skin"),imported.Images["CharacterMap"])));
    }
    [Fact] public void ImportAndRestoreActionRemoveOnlyItsOriginalSpriteMappings()
    {
        manager.Create(root,"skin",114,100114001,"original");
        string frame=Path.Combine(root,"frame.png");File.WriteAllBytes(frame,new byte[]{1,2,3});
        manager.ImportFrames(root,"skin","Walk",new[]{frame,frame},30,true);
        var m=manager.Load(root,"skin");
        m.SpriteMappings["Walk_00000"]=m.Animations["Walk"].Frames[0];
        m.SpriteMappings["Walk-back_00000"]=m.Animations["Walk"].Frames[1];
        manager.Save(root,"skin",m);
        manager.ImportFrames(root,"skin","Walk",new[]{frame},12,false);
        m=manager.Load(root,"skin");
        Assert.Single(m.Animations["Walk"].Frames);Assert.False(m.SpriteMappings.ContainsKey("Walk_00000"));
        Assert.True(m.SpriteMappings.ContainsKey("Walk-back_00000"));
        m.SpriteMappings["Walk_00001"]=m.Animations["Walk"].Frames[0];manager.Save(root,"skin",m);
        manager.ClearAction(root,"skin","Walk");m=manager.Load(root,"skin");
        Assert.False(m.Animations.ContainsKey("Walk"));Assert.False(m.SpriteMappings.ContainsKey("Walk_00001"));
        Assert.True(m.SpriteMappings.ContainsKey("Walk-back_00000"));
    }
    [Fact] public void TraversalArchiveCannotWriteOutsideStaging()
    {
        Directory.CreateDirectory(root);string zip=Path.Combine(root,"evil.zip");
        using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))
        {
            using(var writer=new StreamWriter(archive.CreateEntry("character-skin.json").Open()))writer.Write(JsonSerializer.Serialize(new CharacterSkinManifest{Id="evil",HeroId=114}));
            using(var writer=new StreamWriter(archive.CreateEntry("../escape.png").Open()))writer.Write("escape");
        }
        Assert.ThrowsAny<Exception>(()=>manager.Import(root,zip));Assert.False(File.Exists(Path.Combine(manager.Root(root),"escape.png")));Assert.False(Directory.Exists(manager.Package(root,"evil")));
    }
    [Fact] public void MissingFilesAndInvalidDurationsAreRejectedWithoutOverwritingManifest()
    {
        manager.Create(root,"skin",114,0,"original");var m=manager.Load(root,"skin");m.Images["Character"]="missing.png";
        Assert.ThrowsAny<Exception>(()=>manager.Save(root,"skin",m));Assert.Empty(manager.Load(root,"skin").Images);
        m.Images.Clear();m.Animations["Idle"]=new(){Frames=new(){"missing.png"},DurationsMs=new(){0},Fps=15};
        Assert.ThrowsAny<Exception>(()=>manager.Save(root,"skin",m));
    }
    [Fact] public void DeleteDisablesAndRetainsRecoverablePackage()
    {
        manager.Create(root,"skin",114,0,"skin");manager.SetActive(root,"skin",true);manager.Delete(root,"skin");
        Assert.Empty(manager.Settings(root).ActivePackages);Assert.Empty(manager.Profiles(root));
        Assert.Single(Directory.GetDirectories(Path.Combine(manager.Root(root),".deleted")));
    }
}
