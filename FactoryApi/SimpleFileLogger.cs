using Microsoft.Extensions.Logging;
using System.Text;

public sealed class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _lock = new();

    public SimpleFileLoggerProvider(string path)
    {
        _path = path;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new SimpleFileLogger(_path, categoryName, _lock);
    }

    public void Dispose()
    {
    }
}

public sealed class SimpleFileLogger : ILogger
{
    private readonly string _path;
    private readonly string _categoryName;
    private readonly object _lock;

    public SimpleFileLogger(string path, string categoryName, object sharedLock)
    {
        _path = path;
        _categoryName = categoryName;
        _lock = sharedLock;
    }

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (formatter == null) return;

        string message = formatter(state, exception);

        var line = new StringBuilder();
        line.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        line.Append(" [");
        line.Append(logLevel);
        line.Append("] ");
        line.Append(_categoryName);
        line.Append(" | ");
        line.Append(message);

        if (exception != null)
        {
            line.Append(" | EX: ");
            line.Append(exception);
        }

        lock (_lock)
        {
            File.AppendAllText(_path, line.ToString() + Environment.NewLine, Encoding.UTF8);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}