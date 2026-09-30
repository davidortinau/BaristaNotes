using CoreGraphics;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal enum NativeFeedbackKind { Success, Error, Information }

internal sealed class WindowFeedbackHost(Func<UIWindow?> getWindow, ILogger<WindowFeedbackHost> logger) : IDisposable
{
    private readonly HashSet<ToastOverlay> _overlays = [];
    private readonly Queue<string> _errorQueue = [];
    private readonly CancellationTokenSource _lifetime = new();
    private bool _errorCooldown;
    private bool _disposed;
    public bool IsShowing => _overlays.Count > 0;

    public void Show(string message, NativeFeedbackKind kind)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (kind == NativeFeedbackKind.Error)
        {
            if (_errorCooldown)
            {
                _errorQueue.Enqueue(message);
                logger.LogDebug("Queued native error feedback; {Count} waiting", _errorQueue.Count);
                return;
            }
            _errorCooldown = true;
            _ = ReleaseErrorCooldownAsync();
        }
        _ = PresentAsync(message, kind);
    }

    public Task ShowAndWaitAsync(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return PresentAsync(message, NativeFeedbackKind.Success);
    }

    private Task PresentAsync(string message, NativeFeedbackKind kind)
    {
        var window = getWindow() ?? throw new InvalidOperationException("Feedback needs an attached native window.");
        var overlay = new ToastOverlay(message, kind, () => { });
        overlay.DismissRequested = () => _ = DismissAsync(overlay);
        overlay.Frame = window.Bounds;
        overlay.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
        _overlays.Add(overlay);
        window.AddSubview(overlay);
        overlay.SetNeedsLayout();
        logger.LogDebug("Showing {FeedbackKind} feedback in the native window", kind);
        _ = RunLifetimeAsync(overlay);
        return overlay.Removal;
    }

    private async Task ReleaseErrorCooldownAsync()
    {
        try
        {
            // Source error admission is five seconds, independent of the visible toast.
            await Task.Delay(5000, _lifetime.Token);
            _errorCooldown = false;
            if (_errorQueue.TryDequeue(out var message))
                Show(message, NativeFeedbackKind.Error);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Native error feedback queue failed");
        }
    }

    private async Task RunLifetimeAsync(ToastOverlay overlay)
    {
        try
        {
            await FadeAsync(overlay.Bubble, appearing: true);
            if (!_overlays.Contains(overlay) || overlay.IsClosing) return;
            overlay.SetInteractive(true);
            // Approved correction: hold starts after appearance, not after dismissal.
            await Task.Delay(2000, _lifetime.Token);
            await DismissAsync(overlay);
        }
        catch (OperationCanceledException) when (_disposed) { Remove(overlay); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Native feedback lifecycle failed");
            Remove(overlay);
        }
    }

    private Task DismissAsync(ToastOverlay overlay)
    {
        if (_overlays.Contains(overlay) && !overlay.IsClosing)
        {
            overlay.IsClosing = true;
            overlay.SetInteractive(false);
            _ = FinishDismissalAsync(overlay);
        }
        // A timer racing an already-started manual fade joins the same removal,
        // rather than allowing awaiting callers to navigate while it is attached.
        return overlay.Removal;
    }

    private async Task FinishDismissalAsync(ToastOverlay overlay)
    {
        try
        {
            await FadeAsync(overlay.Bubble, appearing: false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Native feedback dismissal failed");
        }
        finally { Remove(overlay); }
    }

    private void Remove(ToastOverlay overlay)
    {
        if (!_overlays.Remove(overlay)) return;
        try
        {
            overlay.DismissRequested = static () => { };
            overlay.Layer.RemoveAllAnimations();
            overlay.Bubble.Layer.RemoveAllAnimations();
            overlay.RemoveFromSuperview();
            overlay.Dispose();
        }
        finally { overlay.CompleteRemoval(); }
    }

    private static Task FadeAsync(UIView view, bool appearing) =>
        UIView.AnimateKeyframesAsync(0.5, 0, UIViewKeyframeAnimationOptions.CalculationModeLinear, () =>
        {
            // MAUI Easing.SpringIn/SpringOut, sampled at the source's 16ms cadence.
            for (var step = 1; step <= 32; step++)
            {
                var t = step / 32.0;
                var x = appearing ? t : t - 1;
                var eased = appearing
                    ? x * x * ((1.70158 + 1) * x - 1.70158)
                    : x * x * ((1.70158 + 1) * x + 1.70158) + 1;
                var alpha = (nfloat)Math.Clamp(appearing ? eased : 1 - eased, 0, 1);
                UIView.AddKeyframeWithRelativeStartTime((step - 1) / 32.0, 1 / 32.0, () => view.Alpha = alpha);
            }
        });

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _errorQueue.Clear();
        foreach (var overlay in _overlays.ToArray()) Remove(overlay);
        _lifetime.Dispose();
    }

    private sealed class ToastOverlay : UIView
    {
        private readonly TaskCompletionSource<bool> _removed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly UIButton _backdrop;
        private readonly UILabel _icon = new();
        private readonly UILabel _message;
        public UIView Bubble { get; } = new();
        public Action DismissRequested { get; set; }
        public bool IsClosing { get; set; }
        public Task Removal => _removed.Task;
        private bool _interactive;

        public void CompleteRemoval() => _removed.TrySetResult(true);

        public ToastOverlay(string message, NativeFeedbackKind kind, Action dismiss)
        {
            DismissRequested = dismiss;
            AccessibilityIdentifier = "feedback.overlay";
            _backdrop = SliceUi.Button("", "feedback.backdrop", () =>
            {
                if (_interactive) DismissRequested();
            });
            _backdrop.AccessibilityLabel = "Dismiss feedback";
            _backdrop.BackgroundColor = NativeTheme.Backdrop;
            Bubble.BackgroundColor = NativeTheme.DarkSurface;
            Bubble.Layer.CornerRadius = 32;
            Bubble.Layer.BorderWidth = 1;
            Bubble.Layer.BorderColor = NativeTheme.DarkOutline.CGColor;
            Bubble.Alpha = 0;
            _icon.Font = NativeTheme.Icons(20);
            _icon.Text = kind switch
            {
                NativeFeedbackKind.Success => "\ue5ca",
                NativeFeedbackKind.Error => "\ue14c",
                _ => "\ue88e"
            };
            _icon.TextColor = kind switch
            {
                NativeFeedbackKind.Success => UIColor.FromRGB(0x7C, 0xFC, 0x00),
                NativeFeedbackKind.Error => UIColor.FromRGB(0xFF, 0x6B, 0x6B),
                _ => UIColor.FromRGB(0x4A, 0x9E, 0xFF)
            };
            _message = SliceUi.Label(message, 16, true);
            _message.TextColor = NativeTheme.OnPrimary;
            _message.AccessibilityIdentifier = "feedback.message";
            Bubble.AddSubviews(_icon, _message);
            AddSubviews(_backdrop, Bubble);
            SetInteractive(false);
        }

        public void SetInteractive(bool enabled)
        {
            _interactive = enabled;
            _backdrop.Enabled = enabled;
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            _backdrop.Frame = Bounds;
            var safe = Window?.SafeAreaInsets ?? SafeAreaInsets;
            var maximum = Bounds.Width - safe.Left - safe.Right - 60;
            var icon = SliceUi.Measure(_icon, maximum);
            var title = SliceUi.Measure(_message, maximum - icon.Width - 60);
            var contentHeight = (nfloat)Math.Max(icon.Height, title.Height);
            var width = icon.Width + title.Width + 60;
            Bubble.Frame = new CGRect(safe.Left + (Bounds.Width - safe.Left - safe.Right - width) / 2,
                safe.Top + 20, width, contentHeight + 32);
            _icon.Frame = new CGRect(24, 16 + (contentHeight - icon.Height) / 2, icon.Width, icon.Height);
            _message.Frame = new CGRect(36 + icon.Width, 16, title.Width, contentHeight);
        }
    }
}
