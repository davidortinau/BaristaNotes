namespace BaristaNotes.AndroidApp.Views;

internal readonly record struct AdvicePanelBounds(int Left, int Top, int Right, int Bottom)
{
    public const int DesignPaddingDp = 24;
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public static AdvicePanelBounds InWindow(int width, int height, int leftInset, int topInset, int rightInset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        // Initial attachment can measure zero; never produce negative bounds.
        var left = Math.Clamp(leftInset, 0, width);
        var top = Math.Clamp(topInset, 0, height);
        var right = Math.Clamp(width - Math.Max(0, rightInset), left, width);
        // PopupPage's default is Top|Left|Right, intentionally not Bottom.
        return new(left, top, right, height);
    }
}
