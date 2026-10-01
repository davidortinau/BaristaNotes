using Microsoft.Extensions.Logging;

#if NATIVE_PERFORMANCE
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CoreAnimation;
using Foundation;
using UIKit;
#endif

namespace BaristaNotes.Native.iOS;

internal sealed class NativePerformanceProbe : IDisposable
{
#if NATIVE_PERFORMANCE
    private const int RusageInfoV4 = 4;
    private const int RusageBufferSize = 512;
    private const int ResidentSizeOffset = 64;
    private const int PhysicalFootprintOffset = 72;
    private const int ProcessStartOffset = 80;
    private readonly ILogger _logger;
    private readonly string _outputPath;
    private readonly string _mode;
    private readonly string _route;
    private readonly string _memoryState;
    private readonly int _sample;
    private readonly HashSet<OneShotDisplayLink> _displayLinks = [];
    private string _phase = "starting";
    private string? _measuredDestination;
    private ulong _transitionStart;
    private bool _initialActivitySeen;
    private int _repeatCycles;
    private bool _workScheduled;
    private bool _complete;

    public NativePerformanceProbe(NativeServices services, ILoggerFactory logging)
    {
        _logger = logging.CreateLogger<NativePerformanceProbe>();
        _outputPath = Path.Combine(services.DataDirectory, "performance", "status.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(_outputPath)!);
        var arguments = ParseArguments(NSProcessInfo.ProcessInfo.Arguments);
        _mode = arguments.GetValueOrDefault("perf-mode", "startup");
        _route = arguments.GetValueOrDefault("perf-route", string.Empty);
        _memoryState = arguments.GetValueOrDefault("perf-state", string.Empty);
        _sample = int.TryParse(arguments.GetValueOrDefault("perf-sample"), CultureInfo.InvariantCulture, out var sample)
            ? sample
            : 0;
        WriteStatus("starting");
    }

    public void NavigationStarting(string destination)
    {
        if (_complete || _measuredDestination != destination)
            return;
        _transitionStart = MachAbsoluteTime();
        _phase = "measuring";
        WriteStatus("measuring");
    }

    public void DrinkReady(SliceNavigationController host)
    {
        if (_complete || _workScheduled)
            return;

        if (_mode == "startup")
        {
            ScheduleAfterDisplayFrame(() => CompleteStartup());
            return;
        }

        if (_mode == "transition")
        {
            if (_route == "new-drink-to-activity" && _phase == "starting")
            {
                _measuredDestination = "activity";
                ScheduleNavigation(host.Activity);
            }
            else if (_route == "activity-to-settings" && _phase == "starting")
            {
                _phase = "setup-activity";
                ScheduleNavigation(host.Activity);
            }
            return;
        }

        if (_mode != "memory")
        {
            Fail($"Unknown performance mode '{_mode}'.");
            return;
        }

        if (_memoryState == "initial-new-drink" && _phase == "starting")
            ScheduleMemory("initial-new-drink");
        else if (_memoryState is "first-activity" or "repeated-navigation" &&
                 (!_initialActivitySeen || _phase == "returning-drink"))
        {
            _phase = _initialActivitySeen ? "returning-activity" : "setup-activity";
            ScheduleNavigation(host.Activity);
        }
        else if (_memoryState is not ("initial-new-drink" or "first-activity" or "repeated-navigation"))
            Fail($"Unknown memory state '{_memoryState}'.");
    }

    public void ActivityReady(SliceNavigationController host, int totalCount, int visibleCount)
    {
        if (_complete || totalCount != NativePerformanceFixture.Dataset ||
            visibleCount != Math.Min(50, NativePerformanceFixture.Dataset))
        {
            if (!_complete && (totalCount != NativePerformanceFixture.Dataset ||
                               visibleCount != Math.Min(50, NativePerformanceFixture.Dataset)))
                Fail($"Activity fixture mismatch: total={totalCount}, visible={visibleCount}.");
            return;
        }

        if (_mode == "transition")
        {
            if (_route == "new-drink-to-activity" && _measuredDestination == "activity")
                ScheduleAfterDisplayFrame(() => CompleteTransition("activity"));
            else if (_route == "activity-to-settings" && _phase == "setup-activity")
            {
                _measuredDestination = "settings";
                ScheduleNavigation(host.Settings);
            }
            return;
        }

        if (_mode != "memory")
            return;

        if (!_initialActivitySeen)
        {
            _initialActivitySeen = true;
            if (_memoryState == "first-activity")
            {
                ScheduleMemory("first-activity");
                return;
            }
        }
        else if (_phase == "returning-activity")
        {
            _repeatCycles++;
        }

        if (_memoryState == "repeated-navigation")
        {
            if (_repeatCycles >= 5)
                ScheduleMemory("repeated-navigation");
            else
            {
                _phase = "cycling-settings";
                ScheduleNavigation(host.Settings);
            }
        }
    }

    public void SettingsReady(SliceNavigationController host)
    {
        if (_complete)
            return;
        if (_mode == "transition" && _route == "activity-to-settings" && _measuredDestination == "settings")
        {
            ScheduleAfterDisplayFrame(() => CompleteTransition("settings"));
            return;
        }
        if (_mode == "memory" && _memoryState == "repeated-navigation" && _phase == "cycling-settings")
        {
            _phase = "returning-drink";
            ScheduleNavigation(host.NewDrink);
        }
    }

    private void ScheduleNavigation(Action navigation)
    {
        if (_workScheduled)
            return;
        _workScheduled = true;
        ScheduleAfterDisplayFrame(() =>
        {
            _workScheduled = false;
            navigation();
        });
    }

    private void ScheduleMemory(string state)
    {
        if (_workScheduled)
            return;
        _workScheduled = true;
        ScheduleAfterDisplayFrame(() => _ = CollectMemoryAsync(state));
    }

    private async Task CollectMemoryAsync(string state)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            var snapshots = new List<ProcessSnapshot>();
            for (var index = 0; index < 3; index++)
            {
                if (index > 0)
                    await Task.Delay(TimeSpan.FromSeconds(1));
                snapshots.Add(ReadProcessSnapshot());
            }
            Complete(new Dictionary<string, string>
            {
                ["memoryState"] = state,
                ["residentBytes"] = Median(snapshots.Select(item => item.ResidentBytes)).ToString(CultureInfo.InvariantCulture),
                ["physicalFootprintBytes"] = Median(snapshots.Select(item => item.PhysicalFootprintBytes)).ToString(CultureInfo.InvariantCulture),
                ["managedBytes"] = Median(snapshots.Select(item => item.ManagedBytes)).ToString(CultureInfo.InvariantCulture),
                ["memorySamples"] = string.Join(",", snapshots.Select(item =>
                    $"{item.ResidentBytes}:{item.PhysicalFootprintBytes}:{item.ManagedBytes}"))
            });
        }
        catch (Exception error)
        {
            Fail(error.Message);
        }
    }

    private void CompleteStartup()
    {
        var snapshot = ReadProcessSnapshot();
        if (snapshot.ProcessStartTicks == 0)
        {
            Fail("The kernel did not provide a process start timestamp.");
            return;
        }
        var duration = TicksToMilliseconds(MachAbsoluteTime() - snapshot.ProcessStartTicks);
        Complete(new Dictionary<string, string>
        {
            ["durationMs"] = duration.ToString("F3", CultureInfo.InvariantCulture)
        });
    }

    private void CompleteTransition(string destination)
    {
        if (_transitionStart == 0)
        {
            Fail("The measured transition did not record a start timestamp.");
            return;
        }
        var duration = TicksToMilliseconds(MachAbsoluteTime() - _transitionStart);
        Complete(new Dictionary<string, string>
        {
            ["destination"] = destination,
            ["durationMs"] = duration.ToString("F3", CultureInfo.InvariantCulture)
        });
    }

    private void Complete(IReadOnlyDictionary<string, string> values)
    {
        _complete = true;
        _phase = "complete";
        WriteStatus("complete", values);
    }

    private void Fail(string reason)
    {
        _complete = true;
        _phase = "failed";
        _logger.LogError("Native performance run failed: {Reason}", reason);
        WriteStatus("failed", new Dictionary<string, string> { ["failure"] = reason });
    }

    private void ScheduleAfterDisplayFrame(Action action)
    {
        var link = new OneShotDisplayLink(this, action);
        _displayLinks.Add(link);
        link.Start();
    }

    private void Remove(OneShotDisplayLink link)
    {
        _displayLinks.Remove(link);
        link.Dispose();
    }

    private void WriteStatus(string state, IReadOnlyDictionary<string, string>? values = null)
    {
        var lines = new List<string>
        {
            $"state={state}",
            $"phase={_phase}",
            $"mode={_mode}",
            $"route={_route}",
            $"memoryState={_memoryState}",
            $"sample={_sample.ToString(CultureInfo.InvariantCulture)}",
            $"dataset={NativePerformanceFixture.Dataset.ToString(CultureInfo.InvariantCulture)}",
            $"pid={Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}",
            $"dynamicCodeSupported={RuntimeFeature.IsDynamicCodeSupported.ToString(CultureInfo.InvariantCulture)}",
            $"dynamicCodeCompiled={RuntimeFeature.IsDynamicCodeCompiled.ToString(CultureInfo.InvariantCulture)}",
            $"bundle={NSBundle.MainBundle.BundleIdentifier ?? string.Empty}",
            $"title={NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleDisplayName")?.ToString() ?? string.Empty}"
        };
        if (values != null)
            lines.AddRange(values.Select(pair => $"{pair.Key}={Sanitize(pair.Value)}"));
        var temporary = _outputPath + ".tmp";
        File.WriteAllLines(temporary, lines);
        File.Move(temporary, _outputPath, true);
        _logger.LogInformation(
            "BARISTA_PERF {Status}",
            string.Join(';', lines));
    }

    private static Dictionary<string, string> ParseArguments(string[] arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < arguments.Length; index++)
        {
            if (!arguments[index].StartsWith("--", StringComparison.Ordinal))
                continue;
            values[arguments[index][2..]] = arguments[++index];
        }
        return values;
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');

    private static long Median(IEnumerable<long> values)
    {
        var ordered = values.Order().ToArray();
        return ordered[ordered.Length / 2];
    }

    private static ProcessSnapshot ReadProcessSnapshot()
    {
        var buffer = Marshal.AllocHGlobal(RusageBufferSize);
        try
        {
            Marshal.Copy(new byte[RusageBufferSize], 0, buffer, RusageBufferSize);
            if (ProcPidRusage(Environment.ProcessId, RusageInfoV4, buffer) != 0)
                throw new InvalidOperationException($"proc_pid_rusage failed with errno {Marshal.GetLastPInvokeError()}.");
            return new ProcessSnapshot(
                unchecked((ulong)Marshal.ReadInt64(buffer, ProcessStartOffset)),
                Marshal.ReadInt64(buffer, ResidentSizeOffset),
                Marshal.ReadInt64(buffer, PhysicalFootprintOffset),
                GC.GetTotalMemory(false));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static double TicksToMilliseconds(ulong ticks)
    {
        var timebase = new MachTimebaseInfo();
        if (GetMachTimebaseInfo(ref timebase) != 0 || timebase.Denom == 0)
            throw new InvalidOperationException("mach_timebase_info failed.");
        return ticks * (double)timebase.Numer / timebase.Denom / 1_000_000d;
    }

    public void Dispose()
    {
        foreach (var link in _displayLinks.ToArray())
            link.Dispose();
        _displayLinks.Clear();
    }

    private readonly record struct ProcessSnapshot(
        ulong ProcessStartTicks,
        long ResidentBytes,
        long PhysicalFootprintBytes,
        long ManagedBytes);

    [StructLayout(LayoutKind.Sequential)]
    private struct MachTimebaseInfo
    {
        public uint Numer;
        public uint Denom;
    }

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "proc_pid_rusage", SetLastError = true)]
    private static extern int ProcPidRusage(int pid, int flavor, IntPtr buffer);

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_absolute_time")]
    private static extern ulong MachAbsoluteTime();

    [DllImport("/usr/lib/libSystem.B.dylib", EntryPoint = "mach_timebase_info")]
    private static extern int GetMachTimebaseInfo(ref MachTimebaseInfo info);

    private sealed class OneShotDisplayLink : NSObject
    {
        private readonly NativePerformanceProbe _owner;
        private Action? _action;
        private CADisplayLink? _link;
        private int _remainingFrames = 2;

        public OneShotDisplayLink(NativePerformanceProbe owner, Action action)
        {
            _owner = owner;
            _action = action;
        }

        public void Start()
        {
            _link = CADisplayLink.Create(this, new ObjCRuntime.Selector(nameof(Tick)));
            _link.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
        }

        [Export(nameof(Tick))]
        private void Tick()
        {
            if (--_remainingFrames > 0)
                return;
            var action = _action;
            _action = null;
            _link?.Invalidate();
            _link = null;
            action?.Invoke();
            _owner.Remove(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _link?.Invalidate();
                _link = null;
                _action = null;
            }
            base.Dispose(disposing);
        }
    }
#else
    public NativePerformanceProbe(NativeServices services, ILoggerFactory logging)
    {
    }

    public void NavigationStarting(string destination)
    {
    }

    public void DrinkReady(SliceNavigationController host)
    {
    }

    public void ActivityReady(SliceNavigationController host, int totalCount, int visibleCount)
    {
    }

    public void SettingsReady(SliceNavigationController host)
    {
    }

    public void Dispose()
    {
    }
#endif
}
