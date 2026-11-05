using System.IO;
using AstralParty.Toys.Services;
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
}
