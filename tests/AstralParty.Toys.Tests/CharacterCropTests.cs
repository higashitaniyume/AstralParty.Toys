using AstralParty.Toys;
using AstralParty.Toys.Services;
using CesiumLoader.CharacterSkins;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
namespace AstralParty.Toys.Tests;
public sealed class CharacterCropTests
{
    [Fact] public void OriginalSizesResolveForBlueDefaultAndSharedPixelPortrait()
    {
        var app=AppContext.BaseDirectory;var protocol=new GameProtocolContext(Path.Combine(app,"Protocol"));
        var m=new CharacterSkinManifest{HeroId=114,SkinItemId=100114001};
        var portrait=CharacterImageSize.Resolve(protocol,Path.Combine(app,"GameData"),m,"Character");
        Assert.True(portrait.Verified);Assert.Equal(1120,portrait.Width);Assert.Equal(1740,portrait.Height);
        var pixel=CharacterImageSize.Resolve(protocol,Path.Combine(app,"GameData"),m,"CharacterMap");
        Assert.True(pixel.Verified);Assert.Equal(200,pixel.Width);Assert.Equal(200,pixel.Height);
    }
    [Fact] public void SingleFrameReplacementPreservesTimingAndUpdatesSharedMappedReferences()
    {
        Exception? failure=null;
        var thread=new Thread(()=>{
            string root=Path.Combine(Path.GetTempPath(),"CharacterFrameTest_"+Guid.NewGuid().ToString("N"));
            try
            {
                var manager=new CharacterSkinManager();manager.Create(root,"skin",114,100114001,"original");
                string sourceFile=Path.Combine(root,"original.png");File.WriteAllBytes(sourceFile,new byte[]{1,2,3});
                manager.ImportFrames(root,"skin","Walk",new[]{sourceFile,sourceFile},30,false);
                var m=manager.Load(root,"skin");var a=m.Animations["Walk"];
                string original=a.Frames[1];a.Frames.Add(original);a.DurationsMs=new(){40,60,80};
                m.SpriteMappings["Walk_00001"]=original;m.UseOriginalSpriteLayout=true;manager.Save(root,"skin",m);
                var image=BitmapSource.Create(2,1,96,96,PixelFormats.Bgra32,null,new byte[]{0,0,255,255,0,0,0,0},8);
                manager.SaveFrame(root,"skin","Walk",1,image,original);
                m=manager.Load(root,"skin");a=m.Animations["Walk"];
                Assert.Equal(3,a.Frames.Count);Assert.Equal(30,a.Fps);Assert.False(a.Loop);Assert.Equal(new[]{40,60,80},a.DurationsMs);
                Assert.NotEqual(original,a.Frames[1]);Assert.Equal(a.Frames[1],a.Frames[2]);Assert.Equal(a.Frames[1],m.SpriteMappings["Walk_00001"]);
                Assert.True(m.UseOriginalSpriteLayout);Assert.True(File.Exists(CharacterSkinRules.SafePath(manager.Package(root,"skin"),original)));
                var saved=SkinCropImage.Load(CharacterSkinRules.SafePath(manager.Package(root,"skin"),a.Frames[1]));Assert.Equal(2,saved.PixelWidth);
                var pixels=new byte[8];saved.CopyPixels(pixels,8,0);Assert.Equal(255,pixels[3]);Assert.Equal(0,pixels[7]);
                Assert.Throws<InvalidOperationException>(()=>manager.SaveFrame(root,"skin","Walk",1,image,original));
                Assert.Throws<InvalidOperationException>(()=>manager.SaveFrame(root,"skin","Walk",99,image,a.Frames[1]));
                // An independent sequence can reuse a file without sharing the edit across frame slots.
                m.SpriteMappings.Clear();a.Frames[2]=a.Frames[1];manager.Save(root,"skin",m);
                string before=a.Frames[1];manager.SaveFrame(root,"skin","Walk",1,image,before);m=manager.Load(root,"skin");
                Assert.Equal(before,m.Animations["Walk"].Frames[2]);Assert.NotEqual(before,m.Animations["Walk"].Frames[1]);
            }
            catch(Exception ex){failure=ex;}
            finally{if(Directory.Exists(root))Directory.Delete(root,true);}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
    [Fact] public void RectangularCropAndContainPreserveSelectionAndTransparentPadding()
    {
        Exception? failure=null;var thread=new Thread(()=>{try{
            var pixels=new byte[8*4*4];for(int i=0;i<pixels.Length;i+=4){pixels[i+2]=255;pixels[i+3]=255;}
            var source=BitmapSource.Create(8,4,96,96,PixelFormats.Bgra32,null,pixels,32);
            var contained=SkinCropImage.RenderRegion(source,new Int32Rect(0,0,8,4),200,200,true);
            Assert.Equal(200,contained.PixelWidth);Assert.Equal(200,contained.PixelHeight);
            var p=new byte[4];contained.CopyPixels(new Int32Rect(100,10,1,1),p,4,0);Assert.Equal(0,p[3]);
            contained.CopyPixels(new Int32Rect(100,100,1,1),p,4,0);Assert.Equal(255,p[3]);Assert.Equal(255,p[2]);
            var cropped=SkinCropImage.RenderRegion(source,new Int32Rect(4,0,2,4),100,200,false);
            Assert.Equal(100,cropped.PixelWidth);Assert.Equal(200,cropped.PixelHeight);
            cropped.CopyPixels(new Int32Rect(50,100,1,1),p,4,0);Assert.Equal(255,p[3]);
            Assert.Throws<ArgumentException>(()=>SkinCropImage.RenderRegion(source,new Int32Rect(7,0,2,4),100,200,false));
            Assert.Throws<ArgumentException>(()=>SkinCropImage.RenderRegion(source,new Int32Rect(0,0,8,4),9000,200,false));
        }catch(Exception ex){failure=ex;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
