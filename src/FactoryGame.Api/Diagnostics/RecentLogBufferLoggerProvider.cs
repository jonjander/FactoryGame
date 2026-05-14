using System.Text;
using Microsoft.Extensions.Logging;

namespace FactoryGame.Api.Diagnostics;

public sealed class RecentLogBufferLoggerProvider(RecentLogBuffer buffer, LogLevel minimumLevel) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) =>
        new BufferLogger(categoryName, buffer, minimumLevel);

    public void Dispose()
    {
    }

    private sealed class BufferLogger : ILogger
    {
        private readonly string _category;
        private readonly RecentLogBuffer _buffer;
        private readonly LogLevel _minimumLevel;

        public BufferLogger(string category, RecentLogBuffer buffer, LogLevel minimumLevel)
        {
            _category = category;
            _buffer = buffer;
            _minimumLevel = minimumLevel;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var sb = new StringBuilder(128);
            sb.Append(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(" [");
            sb.Append(logLevel);
            sb.Append("] ");
            sb.Append(_category);
            sb.Append(": ");
            sb.Append(message);
            if (exception != null)
            {
                sb.AppendLine();
                sb.Append(exception);
            }

            _buffer.AddLine(sb.ToString());
        }
    }
}
