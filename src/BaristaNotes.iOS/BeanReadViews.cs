using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Grind;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class BeanReadCard : UIView
{
    private readonly UILabel _title;
    private readonly UILabel _badge = new();
    private readonly UILabel _extra = new();
    private readonly int _headingSpacing;
    private readonly List<UIView> _lines;
    private readonly UIButton? _action;
    public BeanReadCard(string title, string badge, IEnumerable<UIView> lines, UITraitCollection traits,
        string id, Action? action = null, string? extraBadge = null, int headingSpacing = 0)
    {
        AccessibilityIdentifier = id;
        BackgroundColor = EquipmentColors.SurfaceVariant;
        _title = BeanBagUi.Text(title, 16, true, NativeTheme.TextPrimary, traits);
        _headingSpacing = headingSpacing;
        SourceScaledText.Tracked(_badge, badge, 9, 1.5f, NativeTheme.Secondary, traits);
        SourceScaledText.Tracked(_extra, extraBadge ?? "", 9, 1.5f, NativeTheme.Secondary, traits);
        _extra.Hidden = extraBadge == null;
        _extra.TextAlignment = UITextAlignment.Center;
        _badge.TextAlignment = UITextAlignment.Center;
        _lines = lines.ToList();
        AddSubviews(_title, _badge, _extra);
        AddSubviews(_lines.ToArray());
        if (action != null)
        {
            _action = new UIButton(UIButtonType.Custom) { AccessibilityIdentifier = id + ".open", AccessibilityLabel = title };
            _action.TouchUpInside += (_, _) => action();
            AddSubview(_action);
        }
    }
    private CGSize Badge => SliceUi.Measure(_badge, 300);
    private CGSize Extra => _extra.Hidden ? CGSize.Empty : SliceUi.Measure(_extra, 300);
    private nfloat BadgesWidth => Badge.Width + 12 + (_extra.Hidden ? 0 : Extra.Width + 12 + _headingSpacing);
    private nfloat TitleHeight(nfloat width) => (nfloat)Math.Max(SliceUi.Measure(_title, width - 24 - BadgesWidth - _headingSpacing).Height, Badge.Height + 4);
    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        24 + TitleHeight(size.Width) + _lines.Sum(line => 6 + (double)line.SizeThatFits(new CGSize(size.Width - 24, nfloat.MaxValue)).Height));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var badge = Badge;
        var heading = TitleHeight(Bounds.Width);
        _title.Frame = new CGRect(12, 12, Bounds.Width - 24 - BadgesWidth - _headingSpacing, heading);
        var badgeX = Bounds.Width - 12 - BadgesWidth;
        _badge.Frame = new CGRect(badgeX, 12 + (heading - badge.Height - 4) / 2, badge.Width + 12, badge.Height + 4);
        if (!_extra.Hidden)
            _extra.Frame = new CGRect(_badge.Frame.Right + _headingSpacing, 12 + (heading - Extra.Height - 4) / 2, Extra.Width + 12, Extra.Height + 4);
        var y = 12 + heading;
        foreach (var line in _lines)
        {
            y += 6;
            var height = line.SizeThatFits(new CGSize(Bounds.Width - 24, nfloat.MaxValue)).Height;
            line.Frame = new CGRect(12, y, Bounds.Width - 24, height);
            y += height;
        }
        if (_action != null) _action.Frame = Bounds;
    }
}

internal static class BeanRecipePresentation
{
    public static UIView? Translation(GrindTranslationResult? result, bool loading, UITraitCollection traits)
    {
        if (loading) return BeanBagUi.Text("Translating grind…", 11, false, NativeTheme.Secondary, traits);
        if (result == null) return null;
        var presentation = BeanDisplay.Translation(result);
        if (presentation.Headline is null)
            return string.IsNullOrWhiteSpace(presentation.Explanation) ? null
                : BeanBagUi.Text(presentation.Explanation, 11, false, NativeTheme.Secondary, traits);
        var color = presentation.BadgeKind switch
        {
            GrindTranslationBadgeKind.UserHistory => UIColor.FromRGB(46, 139, 87),
            GrindTranslationBadgeKind.Calculated or GrindTranslationBadgeKind.KnownMatch => UIColor.FromRGB(70, 130, 180),
            GrindTranslationBadgeKind.AI => UIColor.FromRGB(147, 112, 219),
            _ => UIColor.FromRGB(128, 128, 128)
        };
        return new TranslationView(presentation.Headline, presentation.Badge, color, presentation.Explanation, traits);
    }

    internal sealed class BeanSourceLink : UIControl
    {
        private readonly UILabel _label;
        public BeanSourceLink(string id, UITraitCollection traits, Action action)
        {
            AccessibilityIdentifier = id;
            AccessibilityLabel = "View source";
            AccessibilityTraits = UIAccessibilityTrait.Link;
            IsAccessibilityElement = true;
            _label = BeanBagUi.Text("View source →", 12, true, NativeTheme.Primary, traits);
            AddSubview(_label);
            TouchUpInside += (_, _) => action();
        }
        public override CGSize SizeThatFits(CGSize size) => new(size.Width, SliceUi.Measure(_label, size.Width).Height);
        public override void LayoutSubviews() { base.LayoutSubviews(); _label.Frame = Bounds; }
    }

    private sealed class TranslationView : UIView
    {
        private readonly UILabel _headline;
        private readonly UILabel _badge;
        private readonly UILabel _explanation;
        public TranslationView(string headline, string badge, UIColor color, string? explanation, UITraitCollection traits)
        {
            _headline = BeanBagUi.Text(headline, 12, true, NativeTheme.TextPrimary, traits);
            _badge = BeanBagUi.Text(badge, 10, false, UIColor.White, traits);
            _badge.TextAlignment = UITextAlignment.Center;
            _badge.BackgroundColor = color;
            _explanation = BeanBagUi.Text(explanation ?? "", 11, false, NativeTheme.Secondary, traits);
            _explanation.Hidden = string.IsNullOrWhiteSpace(explanation);
            AddSubviews(_headline, _badge, _explanation);
        }
        private nfloat HeadingHeight(nfloat width) => (nfloat)Math.Max(SliceUi.Measure(_headline, width - SliceUi.Measure(_badge, width).Width - 18).Height,
            SliceUi.Measure(_badge, width).Height + 4);
        public override CGSize SizeThatFits(CGSize size) => new(size.Width, HeadingHeight(size.Width)
            + (_explanation.Hidden ? 0 : 2 + SliceUi.Measure(_explanation, size.Width).Height));
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var badge = SliceUi.Measure(_badge, Bounds.Width);
            var height = HeadingHeight(Bounds.Width);
            _headline.Frame = new CGRect(0, 0, Bounds.Width - badge.Width - 18, height);
            _badge.Frame = new CGRect(Bounds.Width - badge.Width - 12, (height - badge.Height - 4) / 2, badge.Width + 12, badge.Height + 4);
            _explanation.Frame = new CGRect(0, height + 2, Bounds.Width, SliceUi.Measure(_explanation, Bounds.Width).Height);
        }
    }
}
