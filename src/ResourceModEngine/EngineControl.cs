using System.Threading;

namespace ResourceModLoader;

/// <summary>
/// 引擎暂停/取消控制点。长循环在安全位置调用 Checkpoint：暂停时阻塞，取消时抛 OperationCanceledException。
/// </summary>
public static class EngineControl
{
    private static CancellationToken _token = CancellationToken.None;
    private static readonly ManualResetEventSlim ResumeGate = new(true);
    private static volatile bool _paused;

    public static bool IsPaused => _paused;

    public static void Begin(CancellationToken token)
    {
        _token = token;
        _paused = false;
        ResumeGate.Set();
    }

    public static void End()
    {
        _token = CancellationToken.None;
        _paused = false;
        ResumeGate.Set();
    }

    public static void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused) ResumeGate.Reset();
        else ResumeGate.Set();
    }

    public static void Checkpoint(string? detail = null)
    {
        if (_token.IsCancellationRequested) throw new OperationCanceledException(_token);
        if (_paused)
        {
            ResumeGate.Wait(_token);
            if (_token.IsCancellationRequested) throw new OperationCanceledException(_token);
        }
    }
}
