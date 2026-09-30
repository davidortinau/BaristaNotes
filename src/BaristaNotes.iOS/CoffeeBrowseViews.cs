using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class CoffeeRecentRow : UIView
{
    private readonly UIScrollView _scroll = new() { ShowsHorizontalScrollIndicator = false, ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never };
    private readonly List<UIView> _cards = [];
    public CoffeeRecentRow(IEnumerable<BeanDto> beans, UITraitCollection traits, Action<int> select)
    {
        AddSubview(_scroll);
        foreach (var bean in beans)
        {
            var card = new RecentCard(bean, traits, () => select(bean.Id));
            _cards.Add(card);
            _scroll.AddSubview(card);
        }
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, 80);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _scroll.Frame = new CGRect(-20, 0, Bounds.Width + 40, 80);
        nfloat x = 16;
        foreach (var card in _cards) { card.Frame = new CGRect(x, 0, 140, 80); x += 150; }
        _scroll.ContentSize = new CGSize(x + 6, 80);
    }
    private sealed class RecentCard : UIControl
    {
        private readonly UILabel _name, _roaster;
        public RecentCard(BeanDto bean, UITraitCollection traits, Action select)
        {
            AccessibilityIdentifier = $"coffee.recent.{bean.Id}";
            AccessibilityLabel = bean.Name;
            IsAccessibilityElement = true;
            AccessibilityTraits = UIAccessibilityTrait.Button;
            BackgroundColor = NativeTheme.DarkVariant;
            Layer.CornerRadius = 16;
            _name = PhotoUi.Label(bean.Name, 14, traits, true, NativeTheme.OnPrimary);
            _roaster = PhotoUi.Label(string.IsNullOrWhiteSpace(bean.Roaster) ? "—" : bean.Roaster, 11, traits);
            _name.Lines = _roaster.Lines = 1;
            _name.LineBreakMode = _roaster.LineBreakMode = UILineBreakMode.TailTruncation;
            AddSubviews(_name, _roaster);
            TouchUpInside += (_, _) => select();
        }
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var name = SliceUi.Measure(_name, 116).Height;
            var roaster = SliceUi.Measure(_roaster, 116).Height;
            var y = (Bounds.Height - name - roaster - 4) / 2;
            _name.Frame = new CGRect(12, y, 116, name);
            _roaster.Frame = new CGRect(12, y + name + 4, 116, roaster);
        }
    }
}

internal sealed class CoffeeButtonRow(UIButton? left, UIButton right, bool centered, int height) : UIView
{
    public override void MovedToSuperview()
    {
        base.MovedToSuperview();
        if (left != null && left.Superview != this) AddSubview(left);
        if (right.Superview != this) AddSubview(right);
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, height);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var padding = centered ? 40 : 0;
        var gap = centered ? 10 : 8;
        var leftWidth = left == null ? 0 : SliceUi.Measure(left.TitleLabel, Bounds.Width).Width + padding;
        var rightWidth = SliceUi.Measure(right.TitleLabel, Bounds.Width).Width + padding;
        if (centered)
        {
            var x = (Bounds.Width - leftWidth - gap - rightWidth) / 2;
            if (left != null) left.Frame = new CGRect(x, 0, leftWidth, height);
            right.Frame = new CGRect(x + leftWidth + gap, 0, rightWidth, height);
        }
        else
        {
            if (left != null) left.Frame = new CGRect(0, 0, (nfloat)Math.Min(leftWidth, Math.Max(0, Bounds.Width - rightWidth - gap)), height);
            right.Frame = new CGRect(Bounds.Width - rightWidth, 0, rightWidth, height);
        }
    }
}
