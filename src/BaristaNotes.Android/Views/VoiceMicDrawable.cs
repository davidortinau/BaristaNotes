using Android.Graphics;
using Android.Graphics.Drawables;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class VoiceMicDrawable(bool compact) : Drawable
{
    private readonly Paint _paint = new(PaintFlags.AntiAlias);
    private bool _pressed, _listening, _processing;
    private int _alpha = 255;
    public void Update(bool pressed, bool listening, bool processing)
    {
        (_pressed, _listening, _processing) = (pressed, listening, processing);
        InvalidateSelf();
    }

    public override void Draw(Canvas canvas)
    {
        var size = compact ? 66f : 96f;
        var center = size / 2;
        var radius = compact ? 28f : 40f;
        canvas.Save();
        canvas.Translate(Bounds.Left, Bounds.Top);
        canvas.Scale(Bounds.Width() / size, Bounds.Height() / size);
        var active = _pressed || _listening;
        Paint(Color.ParseColor(compact || active || _processing ? "#FF9500" : "#333333"), fill: true);
        canvas.DrawCircle(center, center, radius, _paint);
        if (compact && (_listening || _processing))
        {
            Paint(Color.White, fill: false, 2);
            canvas.DrawCircle(center, center, radius + 4, _paint);
        }
        else if (!compact && active)
        {
            Paint(Color.ParseColor("#FF9500"), fill: false, 2);
            canvas.DrawCircle(center, center, radius + 6, _paint);
        }
        else if (!compact && !_processing)
        {
            Paint(Color.ParseColor("#888888"), fill: false, 2.5f);
            canvas.DrawCircle(center, center, radius, _paint);
        }
        var width = compact ? 12f : 16f;
        var height = compact ? 18f : 24f;
        var top = center - height / 2 - (compact ? 2 : 3);
        Paint(Color.White, fill: true);
        using var rect = new RectF(center - width / 2, top, center + width / 2, top + height);
        canvas.DrawRoundRect(rect, width / 2, width / 2, _paint);
        var standTop = top + height + (compact ? 2 : 3);
        var standWidth = width + (compact ? 6 : 8);
        var left = center - standWidth / 2;
        var curve = compact ? 6f : 8f;
        Paint(Color.White, fill: false, compact ? 2 : 2.5f);
        using var path = new Android.Graphics.Path();
        path.MoveTo(left, top + height / 2);
        path.LineTo(left, standTop);
        path.QuadTo(left, standTop + curve, center, standTop + curve);
        path.QuadTo(left + standWidth, standTop + curve, left + standWidth, standTop);
        path.LineTo(left + standWidth, top + height / 2);
        canvas.DrawPath(path, _paint);
        canvas.DrawLine(center, standTop + curve, center, standTop + (compact ? 12 : 14), _paint);
        canvas.Restore();
    }

    private void Paint(Color color, bool fill, float stroke = 0)
    {
        _paint.Color = color;
        _paint.Alpha = _alpha;
        _paint.SetStyle(fill ? Android.Graphics.Paint.Style.Fill : Android.Graphics.Paint.Style.Stroke);
        _paint.StrokeWidth = stroke;
    }
    public override void SetAlpha(int alpha) { _alpha = alpha; InvalidateSelf(); }
    public override void SetColorFilter(ColorFilter? colorFilter) => _paint.SetColorFilter(colorFilter);
#pragma warning disable CS0672
    public override int Opacity => (int)Format.Translucent;
#pragma warning restore CS0672
    protected override void Dispose(bool disposing)
    {
        if (disposing) _paint.Dispose();
        base.Dispose(disposing);
    }
}
