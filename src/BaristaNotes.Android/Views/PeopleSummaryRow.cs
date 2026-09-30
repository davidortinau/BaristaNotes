using Android.Content;
using Android.Views;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class PeopleSummaryRow : ViewGroup
{
    private int _contentWidth;

    public PeopleSummaryRow(Context context) : base(context)
    {
        // MAUI LayoutHandler does not clip overflowing HStack children.
        SetClipChildren(false);
        SetClipToPadding(false);
    }

    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        _contentWidth = 0;
        var contentHeight = 0;
        var unconstrainedWidth = MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);
        for (var index = 0; index < ChildCount; index++)
        {
            var child = GetChildAt(index)!;
            if (child.Visibility == ViewStates.Gone)
                continue;

            // HorizontalStackLayoutManager measures each child at infinite
            // width; a long maker must not consume the recipient's constraint.
            MeasureChild(child, unconstrainedWidth, heightMeasureSpec);
            _contentWidth += child.MeasuredWidth;
            contentHeight = Math.Max(contentHeight, child.MeasuredHeight);
        }

        SetMeasuredDimension(
            ResolveSize(_contentWidth + PaddingLeft + PaddingRight, widthMeasureSpec),
            ResolveSize(contentHeight + PaddingTop + PaddingBottom, heightMeasureSpec));
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        var availableWidth = right - left - PaddingLeft - PaddingRight;
        var availableHeight = bottom - top - PaddingTop - PaddingBottom;
        // Source ResolveConstraints caps the HStack's desired frame, not its
        // children's widths. Center a fitting group; overflow from the start.
        var x = PaddingLeft + Math.Max(0, (availableWidth - _contentWidth) / 2);
        for (var index = 0; index < ChildCount; index++)
        {
            var child = GetChildAt(index)!;
            if (child.Visibility == ViewStates.Gone)
                continue;

            var y = PaddingTop + (availableHeight - child.MeasuredHeight) / 2;
            child.Layout(x, y, x + child.MeasuredWidth, y + child.MeasuredHeight);
            x += child.MeasuredWidth;
        }
    }

    protected override LayoutParams GenerateDefaultLayoutParams() => new(-2, -2);
}
