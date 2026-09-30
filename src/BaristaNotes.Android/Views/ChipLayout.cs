using Android.Content;
using Android.Views;

namespace BaristaNotes.AndroidApp.Views;

/// <summary>Android-only wrapping chip container matching the source FlexLayout.</summary>
internal sealed class ChipLayout(Context context, int gap) : ViewGroup(context)
{
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        var width = MeasureSpec.GetSize(widthMeasureSpec);
        var x = 0;
        var y = 0;
        var lineHeight = 0;
        for (var i = 0; i < ChildCount; i++)
        {
            var child = GetChildAt(i)!;
            MeasureChild(child, widthMeasureSpec, heightMeasureSpec);
            if (x > 0 && x + child.MeasuredWidth > width)
            {
                x = 0;
                y += lineHeight + gap;
                lineHeight = 0;
            }
            x += child.MeasuredWidth + gap;
            lineHeight = Math.Max(lineHeight, child.MeasuredHeight);
        }
        SetMeasuredDimension(width, ResolveSize(y + lineHeight, heightMeasureSpec));
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        var width = right - left;
        var x = 0;
        var y = 0;
        var lineHeight = 0;
        for (var i = 0; i < ChildCount; i++)
        {
            var child = GetChildAt(i)!;
            if (x > 0 && x + child.MeasuredWidth > width)
            {
                x = 0;
                y += lineHeight + gap;
                lineHeight = 0;
            }
            child.Layout(x, y, x + child.MeasuredWidth, y + child.MeasuredHeight);
            x += child.MeasuredWidth + gap;
            lineHeight = Math.Max(lineHeight, child.MeasuredHeight);
        }
    }

    protected override LayoutParams GenerateDefaultLayoutParams() => new(-2, -2);
}
