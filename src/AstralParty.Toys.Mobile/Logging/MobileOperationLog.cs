using System.Text;

namespace AstralParty.Toys.Mobile.Logging;

/// <summary>一条操作日志。</summary>
public sealed record MobileLogEntry(string Time, string Level, string Message)
{
    public string Line => $"{Time} [{Level}] {Message}";
}

/// <summary>
/// 设备操作的全程日志：一边追加写进日志文件，一边通过 <see cref="Written"/> 推给界面实时渲染。
/// 「详细到读取了什么文件」由 <see cref="Channels.LoggingFileSystem"/> 保证；这里只负责落盘与广播。
/// 日志本身绝不能让操作失败，所有 IO 异常都被吞掉。
/// </summary>
public sealed class MobileOperationLog
{
    /// <summary>内存里保留的最近条数（刷新页面后仍能补上）；界面另有自己的上限。</summary>
    public const int RecentLimit = 1000;

    private readonly object _gate = new();
    private readonly List<MobileLogEntry> _recent = [];

    public MobileOperationLog(string directory, string sessionName)
    {
        Directory.CreateDirectory(directory);
        FilePath = Path.Combine(directory, sessionName + ".log");
        Write("INFO", $"日志开始：{FilePath}");
    }

    /// <summary>日志文件的绝对路径（界面用它告诉玩家去哪儿找）。</summary>
    public string FilePath { get; }

    public int Count
    {
        get { lock (_gate) return _recent.Count; }
    }

    /// <summary>每写一条就触发一次，宿主据此把日志实时推给页面。</summary>
    public event Action<MobileLogEntry>? Written;

    public IReadOnlyList<MobileLogEntry> Recent
    {
        get { lock (_gate) return _recent.ToArray(); }
    }

    public void Info(string message) => Write("INFO", message);

    /// <summary>文件级细节（读了哪个文件、多大、多久、哈希）。</summary>
    public void File(string message) => Write("FILE", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(string message) => Write("ERROR", message);

    public void Write(string level, string message)
    {
        var entry = new MobileLogEntry(DateTimeOffset.Now.ToString("HH:mm:ss.fff"), level, message);
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";

        List<Action<MobileLogEntry>> handlers;
        lock (_gate)
        {
            _recent.Add(entry);
            if (_recent.Count > RecentLimit)
                _recent.RemoveRange(0, _recent.Count - RecentLimit);
            handlers = Written is null ? [] : [.. Written.GetInvocationList().Cast<Action<MobileLogEntry>>()];
        }

        try
        {
            System.IO.File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception)
        {
            // 日志写不进去也不能影响设备操作本身
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(entry);
            }
            catch (Exception)
            {
                // 订阅方（界面推送）出错同样不能影响操作
            }
        }
    }
}
