using Android.Animation;
using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class NativeFeedbackPresenter(
    Activity activity,
    NativeStyle style,
    ILogger<NativeFeedbackPresenter> logger,
    Action<bool> setInputBlocked) : IDisposable
{
    private readonly Queue<string> _errors = [];
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _presentation;
    private TaskCompletionSource? _dismissal;
    private FeedbackOverlay? _overlay;
    private bool _errorCooldown;
    private bool _disposed;

    public bool IsVisible => _overlay is not null;

    public void Show(string message, bool isError)
    {
        if (_disposed)
            return;
        if (isError && _errorCooldown)
        {
            _errors.Enqueue(message);
            return;
        }
        if (isError)
        {
            _errorCooldown = true;
            ReleaseErrorCooldownAsync();
        }
        _ = PresentAsync(message, isError);
    }

    public Task ShowSuccessAsync(string message) =>
        _disposed ? Task.CompletedTask : PresentAsync(message, isError: false);

    public Task ShowInfoAsync(string message) =>
        _disposed ? Task.CompletedTask : PresentAsync(message, isError: false, isInfo: true);

    private async void ReleaseErrorCooldownAsync()
    {
        try
        {
            // FeedbackService queues errors for five seconds, independently of
            // its Toast's two-second visible hold.
            await Task.Delay(TimeSpan.FromSeconds(5), _lifetime.Token);
            _errorCooldown = false;
            if (_errors.TryDequeue(out var message))
                Show(message, isError: true);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Activity teardown releases the queued presentation.
        }
    }

    private async Task PresentAsync(string message, bool isError, bool isInfo = false)
    {
        CancelPresentation();
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _presentation = cancellation;
        var dismissal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _dismissal = dismissal;
        var overlay = new FeedbackOverlay(activity, style, message, isError, isInfo);
        style.FixTheme(overlay);
        _overlay = overlay;
        EventHandler dismiss = (_, _) => Dismiss();
        overlay.Click += dismiss;
        try
        {
            var windowHost = activity.Window?.DecorView as ViewGroup
                ?? throw new InvalidOperationException("The native feedback window host is unavailable.");
            setInputBlocked(true);
            windowHost.AddView(overlay, new FrameLayout.LayoutParams(-1, -1));
            logger.LogDebug("Showing native {FeedbackKind} feedback", isError ? "error" : isInfo ? "info" : "success");
            await AnimateAsync(overlay.Content, appearing: true, cancellation.Token);
            overlay.AllowDismissal = true;
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(2), cancellation.Token), dismissal.Task);
            cancellation.Token.ThrowIfCancellationRequested();
            overlay.AllowDismissal = false;
            await AnimateAsync(overlay.Content, appearing: false, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Backdrop, Back, a new message or Activity teardown owns dismissal.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Native feedback presentation failed");
            if (!_disposed)
                Toast.MakeText(activity, message, ToastLength.Long)?.Show();
        }
        finally
        {
            overlay.Click -= dismiss;
            (overlay.Parent as ViewGroup)?.RemoveView(overlay);
            if (ReferenceEquals(_overlay, overlay))
            {
                _overlay = null;
                _presentation = null;
                _dismissal = null;
                setInputBlocked(false);
            }
            overlay.Dispose();
            cancellation.Dispose();
        }
    }

    public void Dismiss()
    {
        if (_overlay?.AllowDismissal == true)
            _dismissal?.TrySetResult();
    }

    private void CancelPresentation()
    {
        _presentation?.Cancel();
        if (_overlay is null)
            return;
        (_overlay.Parent as ViewGroup)?.RemoveView(_overlay);
        _overlay = null;
        _presentation = null;
        _dismissal = null;
        setInputBlocked(false);
    }

    private static async Task AnimateAsync(View view, bool appearing, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (Android.OS.Looper.MyLooper() != Android.OS.Looper.MainLooper
            || SynchronizationContext.Current is not { } uiContext)
            throw new InvalidOperationException("Native feedback animation requires the Android UI context.");
        var animator = ValueAnimator.OfFloat(appearing ? 0f : 1f, appearing ? 1f : 0f)
            ?? throw new InvalidOperationException("The native fade animator is unavailable.");
        using var easing = new SourceSpringInterpolator(appearing);
        using var lifetime = new UiAnimationLifetime(uiContext, cancellation, animator.Cancel, animator.Dispose);
        animator.SetDuration(500);
        animator.SetInterpolator(easing);
        EventHandler<ValueAnimator.AnimatorUpdateEventArgs> update = (_, _) =>
        {
            if (lifetime.IsActive && animator.AnimatedValue is Java.Lang.Float value)
                view.Alpha = Math.Clamp(value.FloatValue(), 0, 1);
        };
        EventHandler end = (_, _) => lifetime.Complete();
        animator.Update += update;
        animator.AnimationEnd += end;
        try
        {
            lifetime.RegisterCancellation();
            cancellation.ThrowIfCancellationRequested();
            animator.Start();
            await lifetime.Completion;
            cancellation.ThrowIfCancellationRequested();
        }
        finally
        {
            animator.Update -= update;
            animator.AnimationEnd -= end;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _lifetime.Cancel();
        _errors.Clear();
        CancelPresentation();
        _lifetime.Dispose();
    }

    private sealed class SourceSpringInterpolator(bool appearing) : Java.Lang.Object, IInterpolator
    {
        public float GetInterpolation(float input)
        {
            // Pinned MAUI Easing.SpringIn/SpringOut, used by the source Toast.
            const float overshoot = 1.70158f;
            var x = appearing ? input : input - 1;
            return appearing
                ? x * x * ((overshoot + 1) * x - overshoot)
                : x * x * ((overshoot + 1) * x + overshoot) + 1;
        }
    }

    private sealed class FeedbackOverlay : FrameLayout
    {
        private readonly NativeStyle _style;
        public LinearLayout Content { get; }
        public bool AllowDismissal { get; set; }

        public FeedbackOverlay(Activity activity, NativeStyle style, string message, bool error, bool info) : base(activity)
        {
            _style = style;
            Clickable = true;
            SetBackgroundColor(Color.Argb(170, 0, 0, 0));
            NativeStyle.Identify(this, "FeedbackBackdrop");
            Content = style.Row();
            Content.SetGravity(GravityFlags.CenterVertical);
            Content.SetPadding(style.Dp(24), style.Dp(16), style.Dp(24), style.Dp(16));
            Content.Background = style.Rounded(NativeStyle.ModalSurface, 32, NativeStyle.ModalOutline);
            Content.Clickable = true;
            Content.Alpha = 0;
            NativeStyle.Identify(Content, "FeedbackToast");
            var icon = style.Label(error ? "\ue14c" : info ? "\ue88e" : "\ue5ca", 20,
                color: Color.ParseColor(error ? "#FF6B6B" : info ? "#4A9EFF" : "#7CFC00"));
            icon.Typeface = style.Symbols;
            icon.Gravity = GravityFlags.Center;
            Content.AddView(icon, new LinearLayout.LayoutParams(-2, -2)
            {
                RightMargin = style.Dp(12)
            });
            var title = style.Label(message, 16, color: NativeStyle.ModalText);
            title.Typeface = style.Semibold;
            title.Gravity = GravityFlags.CenterVertical;
            title.AccessibilityLiveRegion = AccessibilityLiveRegion.Assertive;
            NativeStyle.Identify(title, "AppFeedback");
            Content.AddView(title, new LinearLayout.LayoutParams(-2, -2));
            AddView(Content, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Top | GravityFlags.CenterHorizontal));
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            var insets = WindowEdgeInsets.Read(this);
            var layout = (FrameLayout.LayoutParams)Content.LayoutParameters!;
            var left = _style.Dp(30) + insets.Left;
            var right = _style.Dp(30) + insets.Right;
            var top = _style.Dp(20) + insets.Top;
            layout.LeftMargin = left;
            layout.RightMargin = right;
            layout.TopMargin = top;
            layout.BottomMargin = _style.Dp(30);
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        }

        public override bool PerformClick() => AllowDismissal && base.PerformClick();
    }
}
