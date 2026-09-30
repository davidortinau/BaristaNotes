using Android.Content;
using Android.Content.Res;
using Android.Views;
using Android.Widget;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class AdviceSafeHost(Context context, View window) : FrameLayout(context)
{
    private readonly WeakReference<View> _window = new(window);
    private WindowEdgeInsets _lastInsets;
    private bool _disposed;

    public override WindowInsets? OnApplyWindowInsets(WindowInsets? insets)
    {
        if (_disposed || insets is null) return insets;
        var edges = WindowEdgeInsets.From(insets);
        if (edges != _lastInsets)
        {
            _lastInsets = edges;
            RequestLayout();
        }
        // Descendants do not fit system windows. Only this host applies safe
        // padding; returning insets leaves other window siblings unaffected.
        return base.OnApplyWindowInsets(insets);
    }

    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        RequestApplyInsets();
    }

    protected override void OnConfigurationChanged(Configuration? newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        if (_disposed) return;
        RequestApplyInsets();
        RequestLayout();
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        if (!_disposed && _window.TryGetTarget(out var windowRoot) && windowRoot.Handle != IntPtr.Zero
            && windowRoot.RootWindowInsets is { } insets)
            _lastInsets = WindowEdgeInsets.From(insets);
        var width = MeasureSpec.GetSize(widthMeasureSpec);
        var height = MeasureSpec.GetSize(heightMeasureSpec);
        var frame = AdvicePanelBounds.InWindow(width, height, _lastInsets.Left, _lastInsets.Top, _lastInsets.Right);
        var right = width - frame.Right;
        if (PaddingLeft != frame.Left || PaddingTop != frame.Top || PaddingRight != right || PaddingBottom != 0)
            SetPadding(frame.Left, frame.Top, right, 0);
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _disposed = true;
        base.Dispose(disposing);
    }
}
