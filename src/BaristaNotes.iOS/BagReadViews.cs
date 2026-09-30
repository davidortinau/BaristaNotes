using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Foundation;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class BagStatusView : UIView
{
    private readonly UILabel _caption = new();
    private readonly UILabel _value = new();
    private readonly UIButton _action;
    public BagStatusView(Action change)
    {
        BackgroundColor = NativeTheme.Surface;
        _action = SliceUi.PickerAction("MARK COMPLETE", "bag.status.change", change);
        _action.BackgroundColor = NativeTheme.TextPrimary;
        _action.Layer.CornerRadius = 0;
        AddSubviews(_caption, _value, _action);
    }
    public void Update(bool complete, UITraitCollection traits)
    {
        SourceScaledText.Tracked(_caption, "STATUS", 10, 2, NativeTheme.Secondary, traits);
        _value.Text = complete ? "COMPLETE" : "ACTIVE";
        _value.Font = SourceScaledText.Font(22, true, traits);
        _value.TextColor = NativeTheme.TextPrimary;
        _action.TitleLabel.Font = SourceScaledText.Font(11, true, traits);
        using var text = new NSAttributedString(complete ? "REACTIVATE" : "MARK COMPLETE", new UIStringAttributes
        {
            Font = _action.TitleLabel.Font, ForegroundColor = NativeTheme.Surface, KerningAdjustment = 1.5f
        });
        _action.SetAttributedTitle(text, UIControlState.Normal);
        SetNeedsLayout();
    }
    public override CGSize SizeThatFits(CGSize size)
    {
        var width = size.Width - 40 - _action.SizeThatFits(CGSize.Empty).Width;
        return new CGSize(size.Width, (nfloat)Math.Max(100, 32 + Math.Max(
            SliceUi.Measure(_caption, width).Height + SliceUi.Measure(_value, width).Height,
            _action.SizeThatFits(CGSize.Empty).Height)));
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var action = _action.SizeThatFits(CGSize.Empty);
        var width = Bounds.Width - 40 - action.Width;
        var caption = SliceUi.Measure(_caption, width);
        var value = SliceUi.Measure(_value, width);
        var y = (Bounds.Height - caption.Height - value.Height) / 2;
        _caption.Frame = new CGRect(16, y, width, caption.Height);
        _value.Frame = new CGRect(16, y + caption.Height, width, value.Height);
        _action.Frame = new CGRect(Bounds.Width - 16 - action.Width, (Bounds.Height - action.Height) / 2, action.Width, action.Height);
    }
}

internal sealed class BagStatsLine : UIView
{
    private readonly UILabel _shots;
    private readonly UILabel _rating;
    private readonly UILabel? _icon;
    public BagStatsLine(BagSummaryDto bag, UITraitCollection traits)
    {
        _shots = BeanBagUi.Text($"{bag.ShotCount} shots", 12, false, NativeTheme.Secondary, traits);
        _rating = BeanBagUi.Text(bag.AverageRating.HasValue ? bag.FormattedRating : "no ratings", 12,
            bag.AverageRating.HasValue, bag.AverageRating.HasValue ? NativeTheme.TextPrimary : NativeTheme.Secondary, traits);
        AddSubviews(_shots, _rating);
        if (bag.AverageRating.HasValue)
        {
            _icon = new UILabel { Text = BeanDisplay.RatingGlyph((int)Math.Round(bag.AverageRating.Value)),
                Font = UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.Icons(14), traits), TextColor = NativeTheme.TextPrimary };
            AddSubview(_icon);
        }
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, (nfloat)Math.Max(
        SliceUi.Measure(_shots, size.Width).Height, Math.Max(SliceUi.Measure(_rating, size.Width).Height, _icon?.Font.LineHeight ?? 0)));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var shots = SliceUi.Measure(_shots, Bounds.Width);
        var rating = SliceUi.Measure(_rating, Bounds.Width);
        _shots.Frame = new CGRect(0, 0, shots.Width, Bounds.Height);
        _rating.Frame = new CGRect(shots.Width + 16, 0, rating.Width, Bounds.Height);
        if (_icon != null)
        {
            var icon = SliceUi.Measure(_icon, Bounds.Width);
            _icon.Frame = new CGRect(_rating.Frame.Right + 4, 0, icon.Width, Bounds.Height);
        }
    }
}
