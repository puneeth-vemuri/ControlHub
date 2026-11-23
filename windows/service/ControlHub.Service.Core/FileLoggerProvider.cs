using Microsoft.Extensions.Logging;

namespace ControlHub.Service.Core;

/// <summary>
/// Microsoft.Extensions.Logging provider that writes formatted entries to ServiceFileLog.
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName);
    public void Dispose() { }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;

        public FileLogger(string category)
        {
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string levelStr = logLevel switch
            {
                LogLevel.Information => "INFO",
                LogLevel.Warning => "WARN",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "FATAL",
                _ => "DEBUG"
            };

            string msg = formatter(state, exception);
            if (exception != null)
            {
                msg += $" Exception: {exception}";
            }

            ServiceFileLog.Log(levelStr, $"[{_category}] {msg}");
        }
    }
}
