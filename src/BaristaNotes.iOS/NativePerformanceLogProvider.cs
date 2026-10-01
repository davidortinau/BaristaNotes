#if NATIVE_PERFORMANCE
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Native.iOS;

internal sealed class NativePerformanceLogProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new NativePerformanceLogger(categoryName);
    public void Dispose()
    {
    }

    private sealed class NativePerformanceLogger(string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= LogLevel.Information &&
            categoryName.Contains(nameof(NativePerformanceProbe), StringComparison.Ordinal);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            var message = formatter(state, exception) + Environment.NewLine;
            var bytes = Encoding.UTF8.GetBytes(message);
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
