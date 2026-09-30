using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class RangeTile : FrameLayout
{
    public TextView Caption { get; }
    public TextView Value { get; }
    public TextView Status { get; }
    public TextView Glyph { get; }
    public Button ActionButton { get; }

    public RangeTile(NativeStyle style) : base(style.Context)
    {
        SetBackgroundColor(style.Surface);
        SetMinimumHeight(style.Dp(80));
        var content = style.Row();
        content.SetGravity(GravityFlags.CenterVertical);
        content.SetPadding(style.Dp(16), style.Dp(16), style.Dp(16), style.Dp(16));
        content.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        var text = style.Column();
        Caption = style.Label("", 12, true, style.Secondary);
        Caption.LetterSpacing = 1.5f * .0624f;
        Value = style.Label("", 18);
        Value.Typeface = style.Semibold;
        foreach (var label in new[] { Caption, Value })
        {
            label.SetSingleLine(true);
            label.Ellipsize = TextUtils.TruncateAt.End;
            text.AddView(label);
        }
        content.AddView(text, new LinearLayout.LayoutParams(0, -2, 1));
        Status = style.Label("", 10, color: style.Secondary);
        Status.LetterSpacing = .0624f;
        Status.Gravity = GravityFlags.CenterVertical;
        Status.Visibility = ViewStates.Gone;
        content.AddView(Status, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = style.Dp(8) });
        Glyph = style.Label("\ue409", 24);
        Glyph.Typeface = style.Symbols;
        Glyph.SetTextSize(Android.Util.ComplexUnitType.Dip, 24);
        Glyph.Gravity = GravityFlags.CenterVertical;
        content.AddView(Glyph, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = style.Dp(8) });
        AddView(content, new LayoutParams(-1, -1));

        ActionButton = style.Button("", "RangeTileAction");
        ActionButton.SetPadding(0, 0, 0, 0);
        ActionButton.SetTextColor(Color.Transparent);
        ActionButton.FocusableInTouchMode = true;
        AddView(ActionButton, new LayoutParams(-1, -1));
    }

    public void SetInteraction(bool enabled, string description)
    {
        ActionButton.Enabled = enabled;
        ActionButton.Visibility = enabled ? ViewStates.Visible : ViewStates.Gone;
        ActionButton.ContentDescription = description;
        ContentDescription = enabled ? null : description;
        ImportantForAccessibility = enabled ? ImportantForAccessibility.No : ImportantForAccessibility.Yes;
    }
}
