using System.IO;
using CesiumLoader.CharacterSkins;
using ImageMagick;
namespace AstralParty.Toys.Services;
public static class CharacterGifExporter
{
    public static Task ExportAsync(string package,CharacterAnimation animation,string destination,Action<CharacterExportProgress> progress,CancellationToken cancellationToken=default)=>Task.Run(()=>Export(package,animation,destination,progress,cancellationToken),cancellationToken);
    static void Export(string package,CharacterAnimation animation,string destination,Action<CharacterExportProgress> progress,CancellationToken cancellationToken)
    {
        if(animation.Frames.Count==0)throw new InvalidOperationException("当前动作没有动画帧");
        if(animation.Fps<=0||!float.IsFinite(animation.Fps))throw new InvalidOperationException("FPS 必须大于零");
        string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        using var collection=new MagickImageCollection();
        try
        {
            uint width=0,height=0;
            foreach(string path in animation.Frames)
            {
                cancellationToken.ThrowIfCancellationRequested();var info=new MagickImageInfo(CharacterSkinRules.SafePath(package,path));width=Math.Max(width,info.Width);height=Math.Max(height,info.Height);
            }
            if(width>4096||height>4096||(long)width*height*animation.Frames.Count>500_000_000)throw new InvalidOperationException("当前动作过大，请缩小帧尺寸或减少帧数后导出 GIF");
            double elapsed=0;int previousTicks=0;
            for(int i=0;i<animation.Frames.Count;i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var image=new MagickImage(CharacterSkinRules.SafePath(package,animation.Frames[i]));
                try
                {
                    image.BackgroundColor=MagickColors.Transparent;
                    image.Extent(width,height,Gravity.South);
                    double duration=animation.DurationsMs!=null&&animation.DurationsMs.Count==animation.Frames.Count?animation.DurationsMs[i]:1000d/animation.Fps;
                    if(!double.IsFinite(duration)||duration<=0||duration>655350)throw new InvalidOperationException("帧时长必须在 0～655350 ms 之间");
                    elapsed+=duration;int ticks=(int)Math.Round(elapsed/10),delay=Math.Clamp(ticks-previousTicks,1,65535);previousTicks+=delay;
                    image.AnimationDelay=(uint)delay;image.AnimationIterations=animation.Loop?0u:1u;image.GifDisposeMethod=GifDisposeMethod.Background;
                    image.Quantize(new QuantizeSettings{Colors=256,DitherMethod=DitherMethod.No});
                    collection.Add(image);
                }
                catch{image.Dispose();throw;}
                progress(new(5+85*(i+1)/animation.Frames.Count,$"生成 GIF：{i+1} / {animation.Frames.Count} 帧"));
            }
            progress(new(95,"编码并保存 GIF"));
            collection.Write(temporary,MagickFormat.Gif);
            cancellationToken.ThrowIfCancellationRequested();File.Move(temporary,destination,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
