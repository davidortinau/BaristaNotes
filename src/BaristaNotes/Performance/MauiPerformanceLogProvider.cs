#if MAUI_PERFORMANCE && IOS
using System.Runtime.InteropServices;
using System.Text;

namespace BaristaNotes;

internal sealed class MauiPerformanceLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new PerformanceLogger(categoryName);
    public void Dispose()
    {
    }

    private sealed class PerformanceLogger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information &&
            categoryName.Contains(nameof(MauiPerformanceProbe), StringComparison.Ordinal);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            var bytes = Encoding.UTF8.GetBytes(formatter(state, exception) + Environment.NewLine);
            var buffer = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, buffer, bytes.Length);
                _ = Write(2, buffer, (nuint)bytes.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "write")]
    private static extern nint Write(int fileDescriptor, IntPtr buffer, nuint count);
}
#endif
