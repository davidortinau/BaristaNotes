using Android.Views;
using Android.Widget;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class RatingSummaryView : LinearLayout
{
    public RatingSummaryView(NativeStyle style, RatingAggregateDto aggregate) : base(style.Context)
    {
        Orientation = Orientation.Vertical;
        var average = style.Label(aggregate.FormattedAverage, 36);
        average.Gravity = GravityFlags.Center;
        AddView(average);
        var count = style.Label($"{aggregate.TotalShots} shots", 14, color: style.Secondary);
        count.Gravity = GravityFlags.Center;
        AddView(count, new LayoutParams(-1, -2) { TopMargin = style.Dp(4) });
        var distribution = style.Column();
        AddView(distribution, new LayoutParams(-1, -2) { TopMargin = style.Dp(16) });
        for (var rating = 4; rating >= 0; rating--)
        {
            var row = style.Row();
            row.SetGravity(GravityFlags.CenterVertical);
            var icon = style.Label(BeanDisplay.RatingGlyph(rating), 20, color: style.Secondary);
            icon.Typeface = style.Symbols;
            icon.Gravity = GravityFlags.Center;
            row.AddView(icon, new LayoutParams(style.Dp(30), -2) { RightMargin = style.Dp(8) });
            var bar = new FrameLayout(style.Context);
            bar.SetBackgroundColor(style.SurfaceVariant);
            var filled = new View(style.Context);
            filled.SetBackgroundColor(style.Primary);
            var percentage = aggregate.GetPercentageForRating(rating);
            bar.AddView(filled, new FrameLayout.LayoutParams(style.Dp(percentage > 0 ? percentage * 2 : 0), -1));
            row.AddView(bar, new LayoutParams(style.Dp(200), style.Dp(20)) { RightMargin = style.Dp(8) });
            var number = style.Label(aggregate.GetCountForRating(rating).ToString(), 14, color: style.Secondary);
            number.Gravity = GravityFlags.End;
            row.AddView(number, new LayoutParams(style.Dp(30), -2));
            distribution.AddView(row, new LayoutParams(-1, -2) { TopMargin = rating == 4 ? 0 : style.Dp(8) });
        }
    }
}
