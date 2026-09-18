namespace DesktopReminder.Infrastructure.Logging;

/// <summary>
/// 日志兜底（冻结需求 R8.5）：所有异常写入日志文件。
/// 线程安全（简单锁）；文件按天滚动保留，避免无限增长。
/// </summary>
public sealed class FileLogger : IDisposable
{
    private readonly string _directory;
    private readonly object _lock = new();

    public FileLogger(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception is null ? message : $"{message} | {exception}");
    }

    private void Write(string level, string message)
    {
        lock (_lock)
        {
            var file = Path.Combine(_directory, $"desktop-reminder-{DateTime.Now:yyyyMMdd}.log");
            File.AppendAllText(file, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}");
        }
    }

    public void Dispose()
    {
    }
}
