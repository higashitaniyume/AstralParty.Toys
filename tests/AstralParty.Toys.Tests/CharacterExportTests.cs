using System.IO;
using AstralParty.Toys.Services;
using CesiumLoader.CharacterSkins;
using ImageMagick;
using Xunit;
namespace AstralParty.Toys.Tests;
public sealed class CharacterExportTests
{
    [Fact] public async Task QueueRunsInOrderCancelsWaitingAndContinuesAfterFailure()
    {
        var queue=new CharacterExportQueue();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);bool canceledRan=false;
        queue.Changed+=items=>{if(items.Any(x=>x.Name=="last"&&x.Status=="done"))finished.TrySetResult();};
        queue.Enqueue("game","first","first",async(progress,token)=>{progress(new(40,"working"));entered.SetResult();await release.Task.WaitAsync(token);return "done";});
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        string waiting=queue.Enqueue("game","waiting","waiting",(_,_)=>{canceledRan=true;return Task.FromResult("bad");});
        Assert.Throws<InvalidOperationException>(()=>queue.Enqueue("game","first","duplicate",(_,_)=>Task.FromResult("bad")));
        queue.Cancel(waiting);
        queue.Enqueue("game","failure","failure",(_,_)=>Task.FromException<string>(new InvalidOperationException("expected")));
        queue.Enqueue("game","last","last",(_,_)=>Task.FromResult("done"));release.SetResult();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(canceledRan);Assert.Equal(new[]{"done","canceled","failed","done"},queue.Snapshot().Select(x=>x.Status));Assert.Equal(100,queue.Snapshot()[0].Percent);
    }
    [Fact] public async Task RunningCancellationCleansAndStartsNextTask()
    {
        var queue=new CharacterExportQueue();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        string id=queue.Enqueue("game","first","first",async(_,token)=>{entered.SetResult();await Task.Delay(Timeout.Infinite,token);return "unreachable";});await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        queue.Changed+=items=>{if(items.Any(x=>x.Name=="second"&&x.Status=="done"))finished.TrySetResult();};
        queue.Enqueue("game","second","second",(_,_)=>Task.FromResult("done"));queue.Cancel(id);await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("canceled",queue.Snapshot()[0].Status);queue.Dismiss(id);Assert.Single(queue.Snapshot());
    }
    [Theory] [InlineData(true,0)] [InlineData(false,1)]
    public async Task GifPreservesFramesTransparencyDurationsAndLoop(bool loop,uint repeats)
    {
        string root=Path.Combine(Path.GetTempPath(),"CharacterGifTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            using(var image=new MagickImage(MagickColors.Transparent,12,12)){using(var pixels=image.GetPixels()){pixels.SetPixel(2,2,new byte[]{255,0,0,255});}image.Write(Path.Combine(root,"first.png"));}
            using(var image=new MagickImage(MagickColors.Transparent,12,12)){using(var pixels=image.GetPixels()){pixels.SetPixel(8,8,new byte[]{0,0,255,255});}image.Write(Path.Combine(root,"second.png"));}
            var animation=new CharacterAnimation{Fps=30,Loop=loop,Frames=["first.png","second.png","first.png"],DurationsMs=[30,80,40]};string file=Path.Combine(root,"action.gif");
            await CharacterGifExporter.ExportAsync(root,animation,file,_=>{});
            using var gif=new MagickImageCollection(file);Assert.Equal(3,gif.Count);Assert.Equal(new uint[]{3,8,4},gif.Select(x=>x.AnimationDelay));Assert.Equal(repeats,gif[0].AnimationIterations);Assert.All(gif,x=>Assert.Equal(GifDisposeMethod.Background,x.GifDisposeMethod));
            gif.Coalesce();Assert.True(gif[0].HasAlpha);Assert.Equal((byte)0,gif[1].GetPixels().GetPixel(2,2).ToColor()!.A);Assert.Equal((byte)255,gif[1].GetPixels().GetPixel(8,8).ToColor()!.A);
            byte[] before=File.ReadAllBytes(file);using var cancellation=new CancellationTokenSource();cancellation.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>CharacterGifExporter.ExportAsync(root,animation,file,_=>{},cancellation.Token));Assert.Equal(before,File.ReadAllBytes(file));Assert.Empty(Directory.GetFiles(root,"*.tmp"));
        }
        finally{Directory.Delete(root,true);}
    }
}
