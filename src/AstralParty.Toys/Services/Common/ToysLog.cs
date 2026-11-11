using System.Text;

namespace AstralParty.Toys.Services;

public enum ToysLogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

/// <summary>
/// 工具箱的运行日志：<c>&lt;数据根&gt;\logs\toys-YYYY-MM-DD.log</c>，按天切分、按大小滚动。
///
/// 日志本身绝不能影响功能：目录不可写、文件被占用都只吞掉异常。宿主里的
/// <c>Debug.WriteLine</c> 只对调试器可见，发行版出问题时什么都看不到，所以才需要这一层。
/// </summary>
public static class ToysLog
{
    public const int DefaultRetentionDays = 7;
    public const long DefaultMaxFileBytes = 10L * 1024 * 1024;

    private static readonly object Gate = new();
    private static readonly UTF8Encoding Utf8 = new(false);

    private static ToysLogLevel _level = ToysLogLevel.Info;
    private static int _retentionDays = DefaultRetentionDays;
    private static long _maxFileBytes = DefaultMaxFileBytes;
    private static string? _directory;

    public static ToysLogLevel Level
    {
        get { lock (Gate) return _level; }
        set { lock (Gate) _level = value; }
    }

    /// <summary>当前日志目录；还没配置时为 null。</summary>
    public static string? Directory
    {
        get { lock (Gate) return _directory; }
    }

    /// <summary>启动时调用一次：确定目录、级别与保留策略，并清理过期日志。</summary>
    public static void Configure(string? directory, ToysLogLevel level, int retentionDays = DefaultRetentionDays,
        long maxFileBytes = DefaultMaxFileBytes)
    {
        lock (Gate)
        {
            _directory = directory;
            _level = level;
            _retentionDays = Math.Clamp(retentionDays, 1, 365);
            _maxFileBytes = Math.Clamp(maxFileBytes, 256 * 1024, 256L * 1024 * 1024);
        }

        if (directory is null) return;
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            Cleanup(directory, retentionDays);
        }
        catch
        {
            // 清理失败不影响写日志
        }
    }

    /// <summary>按配置节重新应用设置（设置页改日志级别时用）。</summary>
    public static void Apply(LoggingConfigSection settings, string? directory = null)
    {
        Configure(directory ?? Directory, ParseLevel(settings.Level), settings.RetentionDays,
            settings.MaxFileBytes);
    }

    public static string NormalizeLevel(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        "debug" or "trace" => "debug",
        "warn" or "warning" => "warn",
        "error" or "fatal" => "error",
        _ => "info"
    };

    public static ToysLogLevel ParseLevel(string? value) => NormalizeLevel(value) switch
    {
        "debug" => ToysLogLevel.Debug,
        "warn" => ToysLogLevel.Warn,
        "error" => ToysLogLevel.Error,
        _ => ToysLogLevel.Info
    };

    public static void Debug(string area, string message) => Write(ToysLogLevel.Debug, area, message);

    public static void Info(string area, string message) => Write(ToysLogLevel.Info, area, message);

    public static void Warn(string area, string message) => Write(ToysLogLevel.Warn, area, message);

    public static void Error(string area, string message, Exception? exception = null) =>
        Write(ToysLogLevel.Error, area, message, exception);

    public static void Write(ToysLogLevel level, string area, string message, Exception? exception = null)
    {
        string? directory;
        ToysLogLevel threshold;
        long maxBytes;
        lock (Gate)
        {
            directory = _directory;
            threshold = _level;
            maxBytes = _maxFileBytes;
        }

        if (directory is null || level < threshold) return;

        try
        {
            System.IO.Directory.CreateDirectory(directory);
            var path = ResolveFile(directory, DateTime.Now, maxBytes);
            var line = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(' ').Append(Format(level))
                .Append(" [").Append(string.IsNullOrWhiteSpace(area) ? "app" : area).Append("] ")
                .Append(message);
            if (exception is not null)
                line.AppendLine().Append("    ").Append(exception.GetType().Name).Append(": ")
                    .Append(exception.Message).AppendLine().Append(exception.StackTrace);
            line.AppendLine();

            lock (Gate)
            {
                File.AppendAllText(path, line.ToString(), Utf8);
            }
        }
        catch
        {
            // 日志失败绝不能往上抛
        }
    }

    /// <summary>在资源管理器里打开日志目录。</summary>
    public static void OpenFolder()
    {
        var directory = Directory;
        if (string.IsNullOrWhiteSpace(directory)) return;
        try
        {
            System.IO.Directory.CreateDirectory(directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }
        catch
        {
            // 打不开就算了
        }
    }

    private static string Format(ToysLogLevel level) => level switch
    {
        ToysLogLevel.Debug => "DEBUG",
        ToysLogLevel.Warn => "WARN ",
        ToysLogLevel.Error => "ERROR",
        _ => "INFO "
    };

    /// <summary>当天文件写满 <paramref name="maxBytes"/> 就换 -1、-2…；极端情况下也不无限增长。</summary>
    private static string ResolveFile(string directory, DateTime now, long maxBytes)
    {
        var stem = Path.Combine(directory, $"toys-{now:yyyy-MM-dd}");
        var path = stem + ".log";
        for (var index = 1; index <= 20; index++)
        {
            long length = 0;
            try
            {
                if (File.Exists(path)) length = new FileInfo(path).Length;
            }
            catch
            {
                return path;
            }

            if (length < maxBytes) return path;
            path = $"{stem}-{index}.log";
        }
        return stem + "-overflow.log";
    }

    private static void Cleanup(string directory, int retentionDays)
    {
        var cutoff = DateTime.Now.Date.AddDays(-Math.Max(1, retentionDays));
        foreach (var file in System.IO.Directory.EnumerateFiles(directory, "toys-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < cutoff) File.Delete(file);
            }
            catch
            {
                // 占用中的文件留到下次
            }
        }
    }
}
