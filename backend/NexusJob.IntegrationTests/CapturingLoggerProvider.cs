using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace NexusJob.IntegrationTests;

/// <summary>Collects every formatted log line the Host emits during a test, so a test can assert on redaction.</summary>
internal sealed class LogSink
{
    public ConcurrentQueue<string> Lines { get; } = new();

    public IReadOnlyCollection<string> Snapshot() => Lines.ToArray();
}

/// <summary>An <see cref="ILoggerProvider"/> that writes every log line into a <see cref="LogSink"/>.</summary>
internal sealed class CapturingLoggerProvider(LogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, sink);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, LogSink sink) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            sink.Lines.Enqueue($"{logLevel} {category} {message} {state} {exception}");
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
