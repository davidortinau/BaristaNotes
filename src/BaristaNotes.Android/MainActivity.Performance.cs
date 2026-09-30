#if NATIVE_PERFORMANCE
using System.Globalization;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    internal static MainActivity? PerformanceCurrent { get; private set; }

    private const string RouteActivity = "new-drink-to-activity";
    private const string RouteSettings = "activity-to-settings";
    private readonly Dictionary<string, Action> _performanceNavigation = new(StringComparer.Ordinal);
    private PerformancePreDrawListener? _performancePreDraw;
    private PerformanceCommitCallback? _performanceCommit;
    private string? _performanceRoute;
    private int _performanceSample;
    private string _performanceState = "starting";
    private string? _performanceDestination;
    private string? _performanceFailure;
    private long _performanceStartNanos;
    private long _performanceEndNanos;

    partial void ConfigurePerformance(Intent? intent)
    {
        PerformanceCurrent = this;
        _performanceRoute = intent?.GetStringExtra("perf-route");
        _performanceSample = intent?.GetIntExtra("perf-sample", 0) ?? 0;
    }

    partial void NotifyPerformanceAppReady()
    {
        _performanceState = "ready";
    }

    partial void RegisterPerformanceNavigation(string id, Action action)
    {
        _performanceNavigation[id] = action;
    }

    partial void NotifyPerformanceNavigationAction(string id)
    {
        var expected = (_performanceRoute, id) switch
        {
            (RouteActivity, "NavActivity") => true,
            (RouteSettings, "NavSettings") => true,
            _ => false
        };
        if (!expected)
            return;
        if (_performanceStartNanos != 0)
        {
            RejectPerformance("A second measured navigation was requested in one process.");
            return;
        }

        _performanceStartNanos = SystemClock.ElapsedRealtimeNanos();
        _performanceState = "measuring";
    }

    partial void NotifyPerformanceHistoryReady(int totalCount)
    {
        if (_performanceRoute == RouteActivity && _performanceStartNanos != 0)
            ArmPerformanceFrameCommit("activity", totalCount);
    }

    partial void NotifyPerformanceSettingsReady()
    {
        if (_performanceRoute == RouteSettings && _performanceStartNanos != 0)
            ArmPerformanceFrameCommit("settings", null);
    }

    partial void DisposePerformance()
    {
        RemovePerformancePreDraw();
        _performanceCommit = null;
        _performanceNavigation.Clear();
        if (ReferenceEquals(PerformanceCurrent, this))
            PerformanceCurrent = null;
    }

    internal string ReceivePerformanceCommand(string command)
    {
        switch (command)
        {
            case "status":
                break;
            case "activity":
                InvokePerformanceNavigation("NavActivity");
                break;
            case "settings":
                InvokePerformanceNavigation("NavSettings");
                break;
            case "new-drink":
                InvokePerformanceNavigation("NavDrink");
                break;
            default:
                return Status($"unknown-command:{command}");
        }

        return Status(null);
    }

    private void InvokePerformanceNavigation(string id)
    {
        if (!_performanceNavigation.TryGetValue(id, out var navigate))
        {
            _performanceFailure = $"Navigation action {id} is unavailable on page {_page}.";
            return;
        }

        navigate();
    }

    private void ArmPerformanceFrameCommit(string destination, int? totalCount)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            RejectPerformance("Android frame commit callbacks require API 29.");
            return;
        }
        if (_root.Handle == IntPtr.Zero || !_root.IsAttachedToWindow || !_root.IsHardwareAccelerated)
        {
            RejectPerformance("The native root is not attached and hardware accelerated.");
            return;
        }
        if (destination == "activity" &&
            (totalCount != NativeApplication.PerformanceDataset ||
             _shotAdapter?.ItemCount != Math.Min(50, totalCount ?? 0) ||
             _historyList?.Visibility != ViewStates.Visible ||
             _historyLoading?.Visibility != ViewStates.Gone))
        {
            RejectPerformance("Activity content did not match the performance fixture.");
            return;
        }

        _performanceDestination = destination;
        _performanceState = "waiting-frame";
        RemovePerformancePreDraw();
        _performancePreDraw = new PerformancePreDrawListener();
        _performancePreDraw.Initialize(this, destination);
        _root.ViewTreeObserver?.AddOnPreDrawListener(_performancePreDraw);
    }

    private void OnPerformancePreDraw(PerformancePreDrawListener listener, string destination)
    {
        if (!ReferenceEquals(_performancePreDraw, listener))
            return;
        RemovePerformancePreDraw();
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            RejectPerformance("Android frame commit callbacks require API 29.");
            return;
        }
        if (!DestinationIsAccepted(destination))
        {
            RejectPerformance($"Destination {destination} was not accepted at pre-draw.");
            return;
        }

        var observer = _root.ViewTreeObserver;
        if (observer is null || observer.Handle == IntPtr.Zero || !observer.IsAlive)
        {
            RejectPerformance("The native root has no live ViewTreeObserver.");
            return;
        }

        _performanceCommit = new PerformanceCommitCallback();
        _performanceCommit.Initialize(this, destination);
        observer.RegisterFrameCommitCallback(_performanceCommit);
    }

    private void OnPerformanceFrameCommit(PerformanceCommitCallback callback, string destination)
    {
        if (!ReferenceEquals(_performanceCommit, callback))
            return;
        _performanceCommit = null;
        if (!DestinationIsAccepted(destination))
        {
            RejectPerformance($"Destination {destination} was not accepted at frame commit.");
            return;
        }

        _performanceEndNanos = SystemClock.ElapsedRealtimeNanos();
        _performanceState = "complete";
    }

    private bool DestinationIsAccepted(string destination) =>
        destination switch
        {
            "activity" =>
                _page == "history" &&
                _shotAdapter?.ItemCount == Math.Min(50, NativeApplication.PerformanceDataset) &&
                _historyList?.Visibility == ViewStates.Visible &&
                _historyLoading?.Visibility == ViewStates.Gone,
            "settings" => _page == "settings",
            _ => false
        };

    private void RejectPerformance(string reason)
    {
        RemovePerformancePreDraw();
        _performanceCommit = null;
        _performanceFailure = reason;
        _performanceState = "rejected";
    }

    private void RemovePerformancePreDraw()
    {
        if (_performancePreDraw is not { } listener)
            return;
        var observer = _root.ViewTreeObserver;
        if (observer is not null && observer.Handle != IntPtr.Zero && observer.IsAlive)
            observer.RemoveOnPreDrawListener(listener);
        _performancePreDraw = null;
    }

    private string Status(string? commandError)
    {
        var duration = _performanceEndNanos > _performanceStartNanos && _performanceStartNanos > 0
            ? (_performanceEndNanos - _performanceStartNanos) / 1_000_000d
            : 0d;
        return string.Join(';',
            $"state={_performanceState}",
            $"page={_page}",
            $"dataset={NativeApplication.PerformanceDataset}",
            $"pid={Android.OS.Process.MyPid()}",
            $"route={_performanceRoute ?? string.Empty}",
            $"sample={_performanceSample.ToString(CultureInfo.InvariantCulture)}",
            $"startNanos={_performanceStartNanos.ToString(CultureInfo.InvariantCulture)}",
            $"endNanos={_performanceEndNanos.ToString(CultureInfo.InvariantCulture)}",
            $"durationMs={duration.ToString("F3", CultureInfo.InvariantCulture)}",
            $"destination={_performanceDestination ?? string.Empty}",
            $"activityItems={_shotAdapter?.ItemCount ?? 0}",
            $"failure={commandError ?? _performanceFailure ?? string.Empty}");
    }

    private sealed class PerformancePreDrawListener : Java.Lang.Object, ViewTreeObserver.IOnPreDrawListener
    {
        private MainActivity _owner = null!;
        private string _destination = string.Empty;

        public void Initialize(MainActivity owner, string destination)
        {
            _owner = owner;
            _destination = destination;
        }

        public bool OnPreDraw()
        {
            _owner.OnPerformancePreDraw(this, _destination);
            return true;
        }
    }

    private sealed class PerformanceCommitCallback : Java.Lang.Object, Java.Lang.IRunnable
    {
        private MainActivity _owner = null!;
        private string _destination = string.Empty;

        public void Initialize(MainActivity owner, string destination)
        {
            _owner = owner;
            _destination = destination;
        }

        public void Run() => _owner.OnPerformanceFrameCommit(this, _destination);
    }
}

[BroadcastReceiver(
    Name = "com.baristanotes.NativePerformanceReceiver",
    Enabled = true,
    Exported = true,
    Permission = "android.permission.DUMP")]
[IntentFilter(["com.baristanotes.NATIVE_PERFORMANCE"])]
public sealed class NativePerformanceReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var command = intent?.GetStringExtra("command") ?? "status";
        ResultData = MainActivity.PerformanceCurrent?.ReceivePerformanceCommand(command)
            ?? "state=missing;failure=activity-not-running";
    }
}
#endif
