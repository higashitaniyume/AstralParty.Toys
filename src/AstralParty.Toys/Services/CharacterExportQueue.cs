namespace AstralParty.Toys.Services;
public sealed record CharacterExportProgress(int Percent,string Message);
public sealed record CharacterExportTask(string TaskId,string Game,string PackageId,string Name,string Status,int Percent,string Message);
public sealed class CharacterExportQueue
{
    sealed class Entry
    {
        public required CharacterExportTask State;
        public required Func<Action<CharacterExportProgress>,CancellationToken,Task<string>> Work;
        public readonly CancellationTokenSource Cancellation=new();
    }
    readonly object gate=new();
    readonly List<Entry> entries=[];
    bool pumping;
    public event Action<IReadOnlyList<CharacterExportTask>>? Changed;
    public IReadOnlyList<CharacterExportTask> Snapshot(){lock(gate)return entries.Select(x=>x.State).ToArray();}
    void Notify()=>Changed?.Invoke(Snapshot());
    public string Enqueue(string game,string packageId,string name,Func<Action<CharacterExportProgress>,CancellationToken,Task<string>> work)
    {
        string id=Guid.NewGuid().ToString("N");bool start;
        lock(gate)
        {
            if(entries.Any(x=>x.State.Game==game&&x.State.PackageId==packageId&&x.State.Status is "queued" or "running" or "canceling"))throw new InvalidOperationException("该方案已经在导出队列中");
            entries.Add(new(){State=new(id,game,packageId,name,"queued",0,"等待导出"),Work=work});start=!pumping;pumping=true;
        }
        Notify();if(start)_=Task.Run(Pump);return id;
    }
    public void Cancel(string id)
    {
        lock(gate)
        {
            var entry=entries.Find(x=>x.State.TaskId==id);if(entry==null)return;
            if(entry.State.Status is "queued" or "running")
            {entry.Cancellation.Cancel();entry.State=entry.State with{Status=entry.State.Status=="queued"?"canceled":"canceling",Message=entry.State.Status=="queued"?"已取消":"正在终止并清理临时文件…"};}
        }
        Notify();
    }
    public void Dismiss(string id){lock(gate)entries.RemoveAll(x=>x.State.TaskId==id&&x.State.Status is "done" or "failed" or "canceled");Notify();}
    async Task Pump()
    {
        while(true)
        {
            Entry entry;
            lock(gate)
            {
                var next=entries.FirstOrDefault(x=>x.State.Status=="queued");if(next==null){pumping=false;return;}entry=next;entry.State=entry.State with{Status="running",Message="开始导出"};
            }
            Notify();
            try
            {
                string message=await entry.Work(p=>{
                    lock(gate){if(entry.State.Status!="running")return;entry.State=entry.State with{Percent=Math.Clamp(p.Percent,0,99),Message=p.Message};}Notify();
                },entry.Cancellation.Token);
                lock(gate)entry.State=entry.State with{Status="done",Percent=100,Message=message};
            }
            catch(OperationCanceledException){lock(gate)entry.State=entry.State with{Status="canceled",Message="已终止导出，临时文件已清理"};}
            catch(Exception ex){lock(gate)entry.State=entry.State with{Status="failed",Message=ex.Message};}
            finally{entry.Cancellation.Dispose();}
            Notify();
        }
    }
}
