using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using AstralParty.Toys.Services;
using Xunit;

namespace AstralParty.Toys.Tests;

public sealed class LegacyCharacterAnimationImportTests : IDisposable
{
    readonly string root=Path.Combine(Path.GetTempPath(),"legacy-animation-tests-"+Guid.NewGuid().ToString("N"));
    string Game=>Path.Combine(root,"game");
    readonly CharacterSkinManager manager=new();
    public void Dispose(){if(Directory.Exists(root))Directory.Delete(root,true);}
    Task<LegacyCharacterSkinImportResult> Import(Action<CharacterExportProgress>? progress=null,CancellationToken token=default)
        =>LegacyCharacterSkinImporter.ImportAsync(Game,Environment.GetEnvironmentVariable("ASTRAL_TEST_LEGACY_ANIMATION_ZIP")!,Path.Combine(AppContext.BaseDirectory,"GameData"),Path.Combine(AppContext.BaseDirectory,"Protocol"),progress??(_=>{}),token);

    [RealLegacyAnimationArchiveFact]
    public async Task SwordArchivePreservesOriginalAnimationOrderLayoutAndInactiveMedia()
    {
        var percents=new List<int>();var result=await Import(p=>percents.Add(p.Percent));
        Assert.Equal(115,result.HeroId);Assert.Equal(100115001,result.SkinItemId);Assert.Equal(8,result.Images);Assert.Equal(22,result.Animations);Assert.Equal(769,result.Sprites);Assert.Equal(1,result.PreservedMedia);
        Assert.Equal(percents.Order(),percents);Assert.True(percents.Count>25);
        var manifest=manager.Load(Game,result.Id);string package=manager.Package(Game,result.Id);
        Assert.True(manifest.UseOriginalSpriteLayout);Assert.Equal(769,manifest.SpriteMappings.Count);Assert.Equal(22,manifest.Animations.Count);Assert.Empty(manifest.Media);
        Assert.Equal(11,Directory.GetFiles(Path.Combine(package,"source"),"*.bundle",SearchOption.AllDirectories).Length);
        Assert.True(File.Exists(Path.Combine(package,"source","游戏根目录","4044742d0da871aef712420e83c85b1b.bundle")));
        Assert.False(File.Exists(manager.Config(Game)));Assert.Empty(manager.Settings(Game).ActivePackages);Assert.False(Directory.Exists(Path.Combine(package,".work")));
        foreach(var (name,animation) in manifest.Animations)
        {
            Assert.Equal(30,animation.Fps);Assert.Equal(name is "Idle" or "Walk" or "Walk-back",animation.Loop);
            Assert.All(animation.Frames,p=>Assert.Contains(p,manifest.SpriteMappings.Values));
        }
        Assert.Equal(24,manifest.Animations["Walk"].Frames.Count);Assert.Equal(63,manifest.Animations["Fight-attack"].Frames.Count);
        Assert.Equal(manifest.SpriteMappings["Walk_00000"],manifest.Animations["Walk"].Frames[0]);
        foreach(var (name,size) in new[]{("Idle_00000",(431,474)),("Walk_00000",(431,474)),("Fight-attack_00000",(1600,1000))})
        {
            using var stream=File.OpenRead(Path.Combine(package,manifest.SpriteMappings[name]));
            var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
            Assert.Equal(size.Item1,frame.PixelWidth);Assert.Equal(size.Item2,frame.PixelHeight);
        }
        using var metadata=JsonDocument.Parse(File.ReadAllText(Path.Combine(package,"legacy-animation-source.json")));
        Assert.False(metadata.RootElement.GetProperty("EnabledMedia").GetBoolean());
        Assert.Equal(769,metadata.RootElement.GetProperty("Sprites").GetArrayLength());
        Assert.Equal("VSkill_Hero115",metadata.RootElement.GetProperty("PreservedMedia")[0].GetString());
        var idle=metadata.RootElement.GetProperty("Sprites").EnumerateArray().Single(x=>x.GetProperty("Name").GetString()=="Idle_00000");
        Assert.Equal(100,idle.GetProperty("PPU").GetSingle());Assert.Equal(.47f,idle.GetProperty("Pivot")[0].GetSingle(),3);Assert.Equal(.19f,idle.GetProperty("Pivot")[1].GetSingle(),3);
        long total=Directory.GetFiles(package,"*",SearchOption.AllDirectories).Sum(p=>new FileInfo(p).Length);Assert.True(total<=LegacyCharacterSkinImporter.MaxArchiveBytes);
    }
    [RealLegacyAnimationArchiveFact]
    public async Task CancellingDuringSpriteConversionRollsBackEntireScheme()
    {
        using var cancellation=new CancellationTokenSource();bool reached=false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>Import(p=>{if(p.Message.StartsWith("已转换动画帧",StringComparison.Ordinal)){reached=true;cancellation.Cancel();}},cancellation.Token));
        Assert.True(reached);Assert.Empty(manager.Profiles(Game));Assert.False(File.Exists(manager.Config(Game)));
        if(Directory.Exists(manager.Root(Game)))Assert.Empty(Directory.GetDirectories(manager.Root(Game)));
    }
}
public sealed class RealLegacyAnimationArchiveFactAttribute:FactAttribute
{
    public RealLegacyAnimationArchiveFactAttribute(){string? path=Environment.GetEnvironmentVariable("ASTRAL_TEST_LEGACY_ANIMATION_ZIP");if(string.IsNullOrEmpty(path)||!File.Exists(path))Skip="设置 ASTRAL_TEST_LEGACY_ANIMATION_ZIP 可验证真实角色动画皮肤包。";}
}
