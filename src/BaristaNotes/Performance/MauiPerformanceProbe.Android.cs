#if MAUI_PERFORMANCE && ANDROID
using System.Globalization;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views;
using Microsoft.Maui;

namespace BaristaNotes;

internal sealed class MauiPerformanceProbe
{
    private const string RouteActivity = "new-drink-to-activity";
    private const string RouteSettings = "activity-to-settings";
    private readonly ILogger<MauiPerformanceProbe> _logger;
    private Activity? _activity;
    private PerformancePreDrawListener? _preDraw;
    private PerformanceCommitCallback? _commit;
    private string _route = string.Empty;
    private int _sample;
    private string _state = "starting";
    private string _page = "starting";
    private string? _destination;
    private string? _failure;
    private long _startNanos;
    private long _endNanos;
    private int _activityItems;

    public MauiPerformanceProbe(ILogger<MauiPerformanceProbe> logger)
    {
        _logger = logger;
    }

    public void ConfigureAndroid(Activity activity, Intent? intent)
    {
        _activity = activity;
        _route = intent?.GetStringExtra("perf-route") ?? string.Empty;
        _sample = intent?.GetIntExtra("perf-sample", 0) ?? 0;
    }

    public void DrinkReady()
    {
        if (_state is not ("starting" or "navigating"))
            return;
        _page = "drink";
        _state = "ready";
    }

    public void ActivityReady(int totalCount, int visibleCount)
    {
        if (totalCount != MauiPerformanceFixture.Dataset ||
            visibleCount != Math.Min(50, MauiPerformanceFixture.Dataset))
        {
            Reject($"Activity fixture mismatch: total={totalCount}, visible={visibleCount}.");
            return;
        }
        if (_state is not ("navigating" or "measuring" or "waiting-frame"))
            return;

        _page = "history";
        _activityItems = visibleCount;
        if (_route == RouteActivity && _startNanos != 0)
            ArmFrameCommit("activity");
        else if (_state is not ("complete" or "rejected"))
            _state = "ready";
    }

    public void SettingsReady()
    {
        if (_state is not ("navigating" or "measuring" or "waiting-frame"))
            return;
        _page = "settings";
        if (_route == RouteSettings && _startNanos != 0)
            ArmFrameCommit("settings");
        else if (_state is not ("complete" or "rejected"))
            _state = "ready";
    }

    internal string ReceiveCommand(string command)
    {
        switch (command)
        {
            case "status":
                break;
            case "activity":
                Navigate("//history", "activity", _route == RouteActivity);
                break;
            case "settings":
                Navigate("//settings", "settings", _route == RouteSettings);
                break;
            case "new-drink":
                Navigate("//shots", "new-drink", false);
                break;
            default:
                return Status($"unknown-command:{command}");
        }

        return Status(null);
    }

    private void Navigate(string route, string destination, bool measure)
    {
        if (_state is "complete" or "rejected")
            return;
        if (measure)
        {
            if (_startNanos != 0)
            {
                Reject("A second measured navigation was requested in one process.");
                return;
            }
            _startNanos = SystemClock.ElapsedRealtimeNanos();
            _destination = destination;
            _state = "measuring";
        }
        else
        {
            _state = "navigating";
        }

        _ = NavigateAsync(route);
    }

    private async Task NavigateAsync(string route)
    {
        try
        {
            await MauiControls.Shell.Current.GoToAsync(route);
        }
        catch (Exception error)
        {
            Reject($"Navigation failed: {error.Message}");
        }
    }

    private void ArmFrameCommit(string destination)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            Reject("Android frame commit callbacks require API 29.");
            return;
        }
        if (_activity?.Window?.DecorView is not { } root ||
            root.Handle == IntPtr.Zero ||
            !root.IsAttachedToWindow ||
            !root.IsHardwareAccelerated)
        {
            Reject("The native root is not attached and hardware accelerated.");
            return;
        }

        _destination = destination;
        _state = "waiting-frame";
        RemovePreDraw();
        _preDraw = new PerformancePreDrawListener(this, destination);
        root.ViewTreeObserver?.AddOnPreDrawListener(_preDraw);
    }

    private void OnPreDraw(PerformancePreDrawListener listener, string destination)
    {
        if (!ReferenceEquals(_preDraw, listener))
            return;
        RemovePreDraw();
        if (!DestinationIsAccepted(destination))
        {
            Reject($"Destination {destination} was not accepted at pre-draw.");
            return;
        }

        var observer = _activity?.Window?.DecorView?.ViewTreeObserver;
        if (observer is null || observer.Handle == IntPtr.Zero || !observer.IsAlive)
        {
            Reject("The native root has no live ViewTreeObserver.");
            return;
        }
        if (!OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            Reject("Android frame commit callbacks require API 29.");
            return;
        }

        _commit = new PerformanceCommitCallback(this, destination);
        observer.RegisterFrameCommitCallback(_commit);
    }

    private void OnFrameCommit(PerformanceCommitCallback callback, string destination)
    {
        if (!ReferenceEquals(_commit, callback))
            return;
        _commit = null;
        if (!DestinationIsAccepted(destination))
        {
            Reject($"Destination {destination} was not accepted at frame commit.");
            return;
        }

        _endNanos = SystemClock.ElapsedRealtimeNanos();
        _state = "complete";
    }

    private bool DestinationIsAccepted(string destination)
    {
        return destination switch
        {
            "activity" => _page == "history" &&
                IsCurrentRoute("history") &&
                _activityItems == Math.Min(50, MauiPerformanceFixture.Dataset),
            "settings" => _page == "settings" && IsCurrentRoute("settings"),
            _ => false
        };
    }

    private static bool IsCurrentRoute(string route)
    {
        var location = MauiControls.Shell.Current?.CurrentState.Location.OriginalString
            .Split('?')[0]
            .TrimEnd('/');
        return location is not null &&
            (location == route || location.EndsWith($"/{route}", StringComparison.Ordinal));
    }

    private void Reject(string reason)
    {
        RemovePreDraw();
        _commit = null;
        _failure = reason;
        _state = "rejected";
        _logger.LogError("MAUI Android performance run failed: {Reason}", reason);
    }

    private void RemovePreDraw()
    {
        if (_preDraw is not { } listener)
            return;
        var observer = _activity?.Window?.DecorView?.ViewTreeObserver;
        if (observer is not null && observer.Handle != IntPtr.Zero && observer.IsAlive)
            observer.RemoveOnPreDrawListener(listener);
        _preDraw = null;
    }

    private string Status(string? commandError)
    {
        var duration = _endNanos > _startNanos && _startNanos > 0
            ? (_endNanos - _startNanos) / 1_000_000d
            : 0d;
        return string.Join(';',
            $"state={_state}",
            $"page={_page}",
            $"dataset={MauiPerformanceFixture.Dataset.ToString(CultureInfo.InvariantCulture)}",
            $"pid={Android.OS.Process.MyPid().ToString(CultureInfo.InvariantCulture)}",
            $"route={_route}",
            $"sample={_sample.ToString(CultureInfo.InvariantCulture)}",
            $"startNanos={_startNanos.ToString(CultureInfo.InvariantCulture)}",
            $"endNanos={_endNanos.ToString(CultureInfo.InvariantCulture)}",
            $"durationMs={duration.ToString("F3", CultureInfo.InvariantCulture)}",
            $"destination={_destination ?? string.Empty}",
            $"activityItems={_activityItems.ToString(CultureInfo.InvariantCulture)}",
            $"failure={commandError ?? _failure ?? string.Empty}");
    }

    private sealed class PerformancePreDrawListener(
        MauiPerformanceProbe owner,
        string destination) : Java.Lang.Object, ViewTreeObserver.IOnPreDrawListener
    {
        public bool OnPreDraw()
        {
            owner.OnPreDraw(this, destination);
            return true;
        }
    }

    private sealed class PerformanceCommitCallback(
        MauiPerformanceProbe owner,
        string destination) : Java.Lang.Object, Java.Lang.IRunnable
    {
        public void Run() => owner.OnFrameCommit(this, destination);
    }
}

[BroadcastReceiver(
    Name = "com.baristanotes.MauiPerformanceReceiver",
    Enabled = true,
    Exported = true,
    Permission = "android.permission.DUMP")]
[IntentFilter(["com.baristanotes.MAUI_PERFORMANCE"])]
public sealed class MauiPerformanceReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var command = intent?.GetStringExtra("command") ?? "status";
        ResultData = IPlatformApplication.Current?.Services
            .GetService<MauiPerformanceProbe>()
            ?.ReceiveCommand(command)
            ?? "state=missing;failure=app-not-running";
    }
}
#endif
