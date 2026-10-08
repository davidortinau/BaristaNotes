using Android.Content;
using Android.Views;
using Android.Widget;

namespace BaristaNotes.AndroidApp.Views;

internal readonly record struct WindowEdgeInsets(int Left, int Top, int Right, int Bottom)
{
    public static WindowEdgeInsets Read(View root) => From(root.RootWindowInsets);

    public static WindowEdgeInsets From(WindowInsets? insets)
    {
        if (insets is null)
            return default;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var edges = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
            return new(edges.Left, edges.Top, edges.Right, edges.Bottom);
        }
#pragma warning disable CA1422, CS0618 // API 24-29 compatibility; modern branch is above.
        var left = insets.SystemWindowInsetLeft;
        var top = insets.SystemWindowInsetTop;
        var right = insets.SystemWindowInsetRight;
        var bottom = insets.SystemWindowInsetBottom;
#pragma warning restore CA1422, CS0618
        if (OperatingSystem.IsAndroidVersionAtLeast(28) && insets.DisplayCutout is { } cutout)
        {
            left = Math.Max(left, cutout.SafeInsetLeft);
            top = Math.Max(top, cutout.SafeInsetTop);
            right = Math.Max(right, cutout.SafeInsetRight);
            bottom = Math.Max(bottom, cutout.SafeInsetBottom);
        }
        return new(left, top, right, bottom);
    }
}

// Mirrors the reference layout's Container safe-area contract: only the leaf's
// actual overlap is added to its visual padding; parent None does not consume it.
internal sealed class EdgeAwareColumn(Context context, Func<View?> windowRoot) : LinearLayout(context)
{
    public bool ExtendBehindStatusBar { get; init; }
    private readonly int[] _location = new int[2];
    private int _left;
    private int _top;
    private int _right;
    private int _bottom;

    public void SetContentPadding(int left, int top, int right, int bottom)
    {
        (_left, _top, _right, _bottom) = (left, top, right, bottom);
        SetPadding(left, top, right, bottom);
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);
        var root = windowRoot();
        if (root is null || Width == 0 || Height == 0)
            return;
        var insets = WindowEdgeInsets.Read(root);
        GetLocationInWindow(_location);
        var x = _location[0];
        var y = _location[1];
        var safeLeft = Math.Clamp(insets.Left - x, 0, insets.Left);
        var safeTop = !ExtendBehindStatusBar && y >= 0 ? Math.Clamp(insets.Top - y, 0, insets.Top) : 0;
        var safeRight = Math.Clamp(x + Width - (root.Width - insets.Right), 0, insets.Right);
        var safeBottom = Math.Clamp(y + Height - (root.Height - insets.Bottom), 0, insets.Bottom);
        if (PaddingLeft == _left + safeLeft && PaddingTop == _top + safeTop
            && PaddingRight == _right + safeRight && PaddingBottom == _bottom + safeBottom)
            return;
        SetPadding(_left + safeLeft, _top + safeTop, _right + safeRight, _bottom + safeBottom);
        Post(RequestLayout);
    }
}
