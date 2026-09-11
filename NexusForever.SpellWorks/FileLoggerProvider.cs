using System.IO;
using Microsoft.Extensions.Logging;

namespace NexusForever.SpellWorks
{
    /// <summary>
    /// Minimal file log. Blazor surfaces component and interop failures through ILogger, and a WebView has
    /// no console to read them from.
    /// </summary>
    public sealed class FileLoggerProvider(string path) : ILoggerProvider
    {
        private static readonly object gate = new();

        public ILogger CreateLogger(string categoryName) => new FileLogger(path, categoryName);

        public void Dispose() { }

        public static void Write(string path, string line)
        {
            lock (gate)
            {
                try
                {
                    File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
                }
                catch (IOException)
                {
                }
            }
        }

        private sealed class FileLogger(string path, string category) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                Write(path, $"[{logLevel}] {category}: {formatter(state, exception)}{(exception == null ? "" : "\n" + exception)}");
            }
        }
    }
}
