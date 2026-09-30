using Android.Graphics;
using Android.Graphics.Drawables;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class PaletteDrawable : GradientDrawable
{
    private int _fill;
    private int? _stroke;
    private readonly int _strokeWidth;

    public PaletteDrawable(Color fill, float radiusPixels, Color? stroke, int strokeWidthPixels)
    {
        _fill = fill.ToArgb();
        _stroke = stroke?.ToArgb();
        _strokeWidth = strokeWidthPixels;
        SetColor(fill);
        SetCornerRadius(radiusPixels);
        if (stroke is { } outline) SetStroke(_strokeWidth, outline);
    }

    public void Refresh(NativePalette before, NativePalette after)
    {
        _fill = before.TranslateTo(_fill, after);
        SetColor(new Color(_fill));
        if (_stroke is int outline)
        {
            _stroke = before.TranslateTo(outline, after);
            SetStroke(_strokeWidth, new Color(_stroke.Value));
        }
    }
}
