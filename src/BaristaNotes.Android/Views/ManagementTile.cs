using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class ManagementTile : LinearLayout
{
    public TextView Caption { get; }
    public TextView Value { get; }

    public ManagementTile(NativeStyle style, bool showAvatar = false) : base(style.Context)
    {
        Orientation = Orientation.Horizontal;
        SetGravity(GravityFlags.CenterVertical);
        SetBackgroundColor(style.Surface);
        SetMinimumHeight(style.Dp(80));
        SetPadding(style.Dp(16), style.Dp(16), style.Dp(16), style.Dp(16));
        if (showAvatar)
        {
            var avatar = style.Label("\ue7fd", 26.4f, color: style.Secondary);
            avatar.Typeface = style.Symbols;
            avatar.Gravity = GravityFlags.Center;
            avatar.ImportantForAccessibility = ImportantForAccessibility.No;
            avatar.Background = style.Rounded(
                Color.Argb(31, style.Secondary.R, style.Secondary.G, style.Secondary.B), 24,
                Color.Argb(102, style.Secondary.R, style.Secondary.G, style.Secondary.B));
            AddView(avatar, new LayoutParams(style.Dp(48), style.Dp(48)) { RightMargin = style.Dp(12) });
        }

        var text = style.Column();
        Caption = style.Label("", 10, true, style.Secondary);
        Caption.LetterSpacing = 2 * .0624f;
        Value = style.Label("", 20, true);
        foreach (var label in new[] { Caption, Value })
        {
            label.SetSingleLine(true);
            label.Ellipsize = TextUtils.TruncateAt.End;
            label.ImportantForAccessibility = ImportantForAccessibility.No;
            text.AddView(label);
        }
        AddView(text, new LayoutParams(0, -2, 1));
        var chevron = style.Label("\ue5cc", 24);
        chevron.Typeface = style.Symbols;
        chevron.SetTextSize(Android.Util.ComplexUnitType.Dip, 24);
        chevron.Gravity = GravityFlags.Center;
        chevron.ImportantForAccessibility = ImportantForAccessibility.No;
        AddView(chevron, new LayoutParams(-2, -2) { LeftMargin = style.Dp(showAvatar ? 12 : 8) });
        ImportantForAccessibility = ImportantForAccessibility.Yes;
    }
}
