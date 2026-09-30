using Android.Graphics;
using Android.Text;
using Android.Text.Style;

namespace BaristaNotes.AndroidApp.Views;

// TypefaceSpan(Typeface) starts at API 28. This equivalent span preserves the
// source's regular unit font on every supported Android version, including 24.
internal sealed class NativeTypefaceSpan(Typeface typeface) : MetricAffectingSpan
{
    public override void UpdateDrawState(TextPaint? textPaint) => Apply(textPaint);
    public override void UpdateMeasureState(TextPaint? textPaint) => Apply(textPaint);

    private void Apply(TextPaint? textPaint)
    {
        if (textPaint is null)
            throw new ArgumentNullException(nameof(textPaint));
        textPaint.SetTypeface(typeface);
    }
}
