using Android.Util;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Logging;

internal sealed class AndroidLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new AndroidLogger(categoryName);

    public void Dispose()
    {
    }

    private sealed class AndroidLogger(string categoryName) : ILogger
    {
        // Android logcat has no structured scope facility. This bootstrap logger
        // preserves category, event, formatted message and exception instead.
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var priority = logLevel switch
            {
                LogLevel.Trace => LogPriority.Verbose,
                LogLevel.Debug => LogPriority.Debug,
                LogLevel.Information => LogPriority.Info,
                LogLevel.Warning => LogPriority.Warn,
                LogLevel.Error => LogPriority.Error,
                LogLevel.Critical => LogPriority.Assert,
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
            };
            var message = $"{categoryName} [{eventId.Id}]: {formatter(state, exception)}";
            if (exception is not null)
                message += $"{Environment.NewLine}{exception}";

            Android.Util.Log.WriteLine(priority, "BaristaNotes.Native", message);
        }
    }
}
