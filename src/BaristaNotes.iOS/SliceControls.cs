using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal static class SliceUi
{
    public static UIButton Button(string title, string id, Action action, bool filled = false)
    {
        var button = new UIButton(UIButtonType.System) { AccessibilityIdentifier = id };
        button.SetTitle(title, UIControlState.Normal);
        button.TitleLabel.Font = NativeTheme.Font(14);
        button.SetTitleColor(filled ? NativeTheme.Surface : NativeTheme.Primary, UIControlState.Normal);
        button.BackgroundColor = filled ? NativeTheme.TextPrimary : UIColor.Clear;
        button.TouchUpInside += (_, _) => action();
        return button;
    }

    public static UIButton Icon(string glyph, string name, string id, Action action)
    {
        var button = new NavigationIconButton(glyph, name, id);
        button.TouchUpInside += (_, _) => action();
        return button;
    }

    public static UIButton PickerAction(string text, string id, Action action, bool primary = false)
    {
        var button = new PickerActionButton(text, id, primary);
        button.TouchUpInside += (_, _) => action();
        return button;
    }

    internal sealed class PickerActionButton : UIButton
    {
        public PickerActionButton(string text, string id, bool primary)
        {
            AccessibilityIdentifier = id;
            SetTitle(text, UIControlState.Normal);
            TitleLabel.Font = NativeTheme.Font(14, primary);
            SetTitleColor(primary ? NativeTheme.Primary : NativeTheme.Secondary, UIControlState.Normal);
            BackgroundColor = UIColor.Clear;
        }
        public override CGSize SizeThatFits(CGSize size)
        {
            var text = TitleLabel.SizeThatFits(new CGSize(1000, 100));
            return new CGSize((nfloat)Math.Max(44, text.Width + 28), (nfloat)Math.Max(44, text.Height + 20));
        }
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var text = TitleLabel.SizeThatFits(new CGSize(Bounds.Width - 28, 100));
            TitleLabel.Frame = new CGRect(14, (Bounds.Height - text.Height) / 2, text.Width, text.Height);
        }
    }

    public static UIButton FormAction(string text, string id, Action action, bool inverted = false)
    {
        var button = new FormActionButton(text, id, inverted);
        button.TouchUpInside += (_, _) => action();
        return button;
    }

    public static UILabel Label(string text, nfloat size, bool bold = false, bool secondary = false) => new()
    {
        Text = text, Font = NativeTheme.Font(size, bold), Lines = 0,
        TextColor = secondary ? NativeTheme.Secondary : NativeTheme.TextPrimary
    };

    public static void HeaderLabel(UILabel label, string text)
        => TrackedText(label, text, 10, 2, NativeTheme.Secondary);

    public static void TrackedText(UILabel label, string text, nfloat size, nfloat spacing, UIColor color)
    {
        label.Font = NativeTheme.Font(size, true);
        label.TextColor = color;
        label.AttributedText = new NSAttributedString(text, new UIStringAttributes
        {
            Font = label.Font, ForegroundColor = color, KerningAdjustment = (float)spacing
        });
    }

    internal sealed class FormActionButton : UIButton
    {
        public FormActionButton(string text, string id, bool inverted) : base()
        {
            AccessibilityIdentifier = id;
            SetTitle(text, UIControlState.Normal);
            TitleLabel.Font = NativeTheme.Font(18, true);
            var foreground = inverted ? NativeTheme.Surface : NativeTheme.TextPrimary;
            SetTitleColor(foreground, UIControlState.Normal);
            SetAttributedTitle(new NSAttributedString(text, new UIStringAttributes
            {
                Font = TitleLabel.Font, ForegroundColor = foreground, KerningAdjustment = 1
            }), UIControlState.Normal);
            BackgroundColor = inverted ? NativeTheme.TextPrimary : NativeTheme.Surface;
        }
        public override CGSize SizeThatFits(CGSize size)
        {
            var text = TitleLabel.SizeThatFits(new CGSize(1000, 100));
            return new CGSize(text.Width + 16, (nfloat)Math.Max(72, text.Height + 48));
        }
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var text = TitleLabel.SizeThatFits(new CGSize(Bounds.Width - 16, 100));
            TitleLabel.Frame = new CGRect((Bounds.Width - text.Width) / 2,
                18 + (Bounds.Height - 48 - text.Height) / 2, text.Width, text.Height);
        }
    }

    public static CGSize Measure(UILabel label, nfloat width) =>
        label.SizeThatFits(new CGSize((nfloat)Math.Max(1, width), nfloat.MaxValue));

    public static CGRect PickerViewport(UIView root, nfloat top) =>
        new(root.SafeAreaLayoutGuide.LayoutFrame.X, top, root.SafeAreaLayoutGuide.LayoutFrame.Width,
            (nfloat)Math.Max(0, root.SafeAreaLayoutGuide.LayoutFrame.Bottom - top));

    public static nfloat NavigationHeight(IEnumerable<UIButton> buttons) =>
        buttons.Select(button => button.SizeThatFits(CGSize.Empty).Height).DefaultIfEmpty(72).Max();

    public static UIView NavigationTopBorder() => new()
    {
        BackgroundColor = NativeTheme.Outline,
        UserInteractionEnabled = false,
        IsAccessibilityElement = false
    };

    public static nfloat LayoutNavigation(
        UIView root,
        IReadOnlyList<UIButton> buttons,
        UIView topBorder)
    {
        var height = NavigationHeight(buttons);
        var borderY = root.Bounds.Height - height - 2;
        topBorder.Frame = new CGRect(0, borderY, root.Bounds.Width, 1);
        var contentWidth = root.Bounds.Width - 2;
        var columnWidth = (contentWidth - buttons.Count + 1) / buttons.Count;
        for (var i = 0; i < buttons.Count; i++)
        {
            buttons[i].Frame = new CGRect(
                1 + i * (columnWidth + 1),
                borderY + 1,
                columnWidth,
                height);
        }
        return height;
    }
}

internal sealed class NavigationIconButton : UIButton
{
    public NavigationIconButton(string glyph, string name, string id) : base()
    {
        AccessibilityIdentifier = id;
        AccessibilityLabel = name;
        SetTitle(glyph, UIControlState.Normal);
        TitleLabel.Font = NativeTheme.Icons(name == "AI advice" ? 24 : 32);
        SetTitleColor(NativeTheme.TextPrimary, UIControlState.Normal);
        BackgroundColor = NativeTheme.Surface;
    }
    public override CGSize SizeThatFits(CGSize size)
    {
        var icon = TitleLabel.SizeThatFits(new CGSize(100, 100));
        return new CGSize(icon.Width + 32, (nfloat)Math.Max(72, icon.Height + 48));
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var icon = TitleLabel.SizeThatFits(new CGSize(100, 100));
        TitleLabel.Frame = new CGRect((Bounds.Width - icon.Width) / 2,
            18 + (Bounds.Height - 48 - icon.Height) / 2, icon.Width, icon.Height);
    }
}

internal sealed class DrinkTile : UIControl
{
    private readonly UILabel _caption = new();
    private readonly UILabel _value = new() { Lines = 2 };
    private readonly UILabel _unit = new();
    private readonly bool _inverted;
    private readonly int? _fixedValueSize;
    private UIView? _customContent;
    private int _valueSize;
    private readonly UILongPressGestureRecognizer? _hold;
    private readonly UIAccessibilityCustomAction? _longPressAction;
    private readonly EventHandler _tap;
    public nfloat TopInset { get; set; }

    public DrinkTile(string caption, string id, Action tap, bool inverted = false,
        int? valueFontSize = null, Action? longPress = null)
    {
        AccessibilityIdentifier = id;
        IsAccessibilityElement = true;
        AccessibilityTraits = UIAccessibilityTrait.Button;
        _inverted = inverted;
        _fixedValueSize = valueFontSize;
        BackgroundColor = inverted ? NativeTheme.TextPrimary : NativeTheme.Surface;
        SliceUi.TrackedText(_caption, caption, 10, 2,
            inverted ? NativeTheme.Surface.ColorWithAlpha(0.7f) : NativeTheme.Secondary);
        AddSubviews(_caption, _value, _unit);
        _tap = (_, _) => tap();
        TouchUpInside += _tap;
        if (longPress != null)
        {
            _hold = new UILongPressGestureRecognizer(gesture =>
            {
                if (gesture.State == UIGestureRecognizerState.Began) longPress();
            })
            {
                // A recognized hold must not release into the normal BAG picker.
                CancelsTouchesInView = true
            };
            AddGestureRecognizer(_hold);
            _longPressAction = new UIAccessibilityCustomAction("View recipe",
                new Func<UIAccessibilityCustomAction, bool>(_ => { longPress(); return true; }));
            AccessibilityCustomActions = [_longPressAction];
            AccessibilityHint = "Activate to select a bag. Long press to view its recipe.";
        }
    }

    public void SetValue(string value, string? unit = null)
    {
        _value.Text = value;
        _unit.Text = unit;
        var length = value.Length;
        var size = length <= 3 ? 44 : length <= 6 ? (unit != null ? 36 : 38) :
            length <= 10 ? 28 : length <= 14 ? 22 : length <= 20 ? 18 : 16;
        size = _fixedValueSize ?? size;
        _valueSize = size;
        _value.Font = NativeTheme.Font(size, true);
        _value.TextColor = _inverted ? NativeTheme.Surface : NativeTheme.TextPrimary;
        _unit.Font = NativeTheme.Font((nfloat)Math.Max(12, size * 0.45));
        _unit.TextColor = _value.TextColor.ColorWithAlpha(0.6f);
        AccessibilityLabel = $"{_caption.Text}: {value} {unit}".Trim();
        SetNeedsLayout();
    }

    public nfloat MeasureHeight(nfloat width, nfloat topInset)
    {
        var caption = SliceUi.Measure(_caption, width - 32);
        var unit = SliceUi.Measure(_unit, width - 32);
        var available = width - 32 - (string.IsNullOrEmpty(_unit.Text) ? 0 : unit.Width + 4);
        var value = SliceUi.Measure(_value, available);
        return (nfloat)Math.Max(120, caption.Height + value.Height + 28 + topInset);
    }

    public void SetCustomContent(UIView content)
    {
        _customContent = content;
        content.UserInteractionEnabled = false;
        _value.Hidden = _unit.Hidden = true;
        AddSubview(content);
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var caption = SliceUi.Measure(_caption, Bounds.Width - 32);
        _caption.Frame = new CGRect(16, 14 + TopInset, Bounds.Width - 32, caption.Height);
        var unitSize = SliceUi.Measure(_unit, Bounds.Width - 32);
        var available = Bounds.Width - 32 - (string.IsNullOrEmpty(_unit.Text) ? 0 : unitSize.Width + 4);
        var textSize = SliceUi.Measure(_value, available);
        var height = (nfloat)Math.Max(0, Math.Min(textSize.Height, Bounds.Height - 28 - caption.Height - TopInset));
        _value.Frame = new CGRect(16, Bounds.Height - 14 - height, available, height);
        var width = (nfloat)Math.Min(available, textSize.Width);
        _unit.Frame = new CGRect(20 + width, Bounds.Height - 14 - (_valueSize >= 32 ? 8 : 4) - unitSize.Height, unitSize.Width, unitSize.Height);
        if (_customContent != null)
            _customContent.Frame = new CGRect(16, 14 + caption.Height, Bounds.Width - 32, Bounds.Height - 28 - caption.Height);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            TouchUpInside -= _tap;
            if (_hold != null)
            {
                RemoveGestureRecognizer(_hold);
                _hold.Dispose();
            }
            AccessibilityCustomActions = null;
            _longPressAction?.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class PeopleTileContent : UIView
{
    private readonly UILabel _maker = SliceUi.Label("—", 11, secondary: true);
    private readonly UILabel _recipient = SliceUi.Label("—", 11, secondary: true);
    private readonly ProfileAvatarView _makerIcon = new(40);
    private readonly ProfileAvatarView _recipientIcon = new(40);
    private readonly UILabel _arrow = SliceUi.Label("\ue941", 22);
    public PeopleTileContent()
    {
        _maker.TextAlignment = _recipient.TextAlignment = UITextAlignment.Center;
        _maker.Lines = _recipient.Lines = 1;
        _maker.LineBreakMode = _recipient.LineBreakMode = UILineBreakMode.TailTruncation;
        _arrow.Font = NativeTheme.Icons(22);
        _arrow.TextColor = NativeTheme.Primary;
        _makerIcon.ConfigurePeople(40, selected: false);
        _recipientIcon.ConfigurePeople(40, selected: false);
        AddSubviews(_maker, _recipient, _makerIcon, _recipientIcon, _arrow);
    }
    public void Update(UserProfileDto? maker, UserProfileDto? recipient, IImageProcessingService images, ILogger logger)
    {
        var makerName = maker?.Name;
        var recipientName = recipient?.Name;
        _maker.Text = string.IsNullOrWhiteSpace(makerName) ? "—" : makerName;
        _recipient.Text = string.IsNullOrWhiteSpace(recipientName) ? "—" : recipientName;
        _maker.TextColor = string.IsNullOrWhiteSpace(makerName) ? NativeTheme.Secondary : NativeTheme.TextPrimary;
        _recipient.TextColor = string.IsNullOrWhiteSpace(recipientName) ? NativeTheme.Secondary : NativeTheme.TextPrimary;
        _makerIcon.SetProfile(maker, images, logger);
        _recipientIcon.SetProfile(recipient, images, logger);
        SetNeedsLayout();
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var nameHeight = (nfloat)Math.Max(SliceUi.Measure(_maker, 60).Height, SliceUi.Measure(_recipient, 60).Height);
        var makerWidth = (nfloat)Math.Max(40, SliceUi.Measure(_maker, Bounds.Width / 3).Width);
        var recipientWidth = (nfloat)Math.Max(40, SliceUi.Measure(_recipient, Bounds.Width / 3).Width);
        var arrow = SliceUi.Measure(_arrow, Bounds.Width);
        var total = makerWidth + recipientWidth + arrow.Width + 16;
        var x = (nfloat)Math.Max(0, (Bounds.Width - total) / 2);
        var top = (Bounds.Height - 43 - nameHeight) / 2;
        _makerIcon.Frame = new CGRect(x + (makerWidth - 40) / 2, top, 40, 40);
        _maker.Frame = new CGRect(x, top + 43, makerWidth, nameHeight);
        _arrow.Frame = new CGRect(x + makerWidth + 8, (Bounds.Height - arrow.Height) / 2, arrow.Width, arrow.Height);
        x += makerWidth + arrow.Width + 16;
        _recipientIcon.Frame = new CGRect(x + (recipientWidth - 40) / 2, top, 40, 40);
        _recipient.Frame = new CGRect(x, top + 43, recipientWidth, nameHeight);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _makerIcon.Dispose();
            _recipientIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed record ChoiceRow(string Id, string Text, bool Selected, Action Select);

internal sealed class ChoiceList : UICollectionView
{
    private readonly ChoiceSource _source;
    private readonly ChoiceLayoutDelegate _layoutDelegate;
    private readonly bool _numeric;
    private bool _centerAfterLayout;
    private nfloat _layoutWidth;
    public ChoiceList(bool numeric = false, bool scalesNumericText = false) : base(CGRect.Empty, new UICollectionViewFlowLayout
    {
        MinimumLineSpacing = 0, MinimumInteritemSpacing = 0, ScrollDirection = UICollectionViewScrollDirection.Vertical
    })
    {
        _numeric = numeric;
        BackgroundColor = NativeTheme.Surface;
        ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
        _source = new ChoiceSource(numeric, scalesNumericText);
        _layoutDelegate = new ChoiceLayoutDelegate(_source, numeric);
        DataSource = _source;
        Delegate = _layoutDelegate;
        RegisterClassForCell(typeof(ChoiceCell), "choice");
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        if (_layoutWidth != Bounds.Width)
        {
            _layoutWidth = Bounds.Width;
            CollectionViewLayout.InvalidateLayout();
        }
        if (_numeric || _source.Rows.Any(row => row.Selected))
        {
            var padding = Bounds.Height / 2;
            if (ContentInset.Top != padding)
                ContentInset = new UIEdgeInsets(padding, 0, padding, 0);
        }
        if (_centerAfterLayout && Bounds.Height > 0 && Bounds.Width > 0 && _source.Rows.Count > 0)
        {
            _centerAfterLayout = false;
            var index = _source.Rows.Select((row, i) => (row, i)).FirstOrDefault(x => x.row.Selected).i;
            var attributes = GetLayoutAttributesForItem(NSIndexPath.FromItemSection(index, 0));
            if (attributes == null) { _centerAfterLayout = true; return; }
            var row = attributes.Frame;
            var minimum = -ContentInset.Top;
            var maximum = (nfloat)Math.Max(minimum, ContentSize.Height - Bounds.Height + ContentInset.Bottom);
            var offset = (nfloat)Math.Clamp(row.Y + row.Height / 2 - Bounds.Height / 2, minimum, maximum);
            SetContentOffset(new CGPoint(0, offset), false);
        }
    }

    public void SetRows(IReadOnlyList<ChoiceRow> rows, bool center, UITraitCollection? fontTraits = null)
    {
        _source.Rows = rows;
        // A parent's trait notification can precede trait propagation to its native children.
        _source.FontTraits = fontTraits;
        // Navigation appearance can precede this table's first nonzero layout.
        _centerAfterLayout |= center;
        ReloadData();
        SetNeedsLayout();
        LayoutIfNeeded();
    }

    private sealed class ChoiceLayoutDelegate(ChoiceSource source, bool numeric) : UICollectionViewDelegateFlowLayout
    {
        private readonly UILabel _measurement = SliceUi.Label("", 22);
        private readonly UILabel _marker = SliceUi.Label("●", 14);
        public override CGSize GetSizeForItem(UICollectionView collectionView, UICollectionViewLayout layout, NSIndexPath indexPath)
        {
            var row = source.Rows[(int)indexPath.Item];
            if (numeric) return new CGSize(collectionView.Bounds.Width, row.Selected ? 96 : 72);
            _measurement.Text = row.Text;
            _measurement.Font = NativeTheme.Font(row.Selected ? 28 : 22, row.Selected);
            var markerWidth = row.Selected ? SliceUi.Measure(_marker, 30).Width : 0;
            return new CGSize(collectionView.Bounds.Width,
                SliceUi.Measure(_measurement, collectionView.Bounds.Width - 48 - markerWidth).Height + 36);
        }
    }

    private sealed class ChoiceSource(bool numeric, bool scalesNumericText) : UICollectionViewDataSource
    {
        public IReadOnlyList<ChoiceRow> Rows { get; set; } = [];
        public UITraitCollection? FontTraits { get; set; }
        public override nint GetItemsCount(UICollectionView collectionView, nint section) => Rows.Count;
        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (ChoiceCell)collectionView.DequeueReusableCell("choice", indexPath);
            cell.Bind(Rows[(int)indexPath.Item], numeric, scalesNumericText, FontTraits ?? collectionView.TraitCollection);
            return cell;
        }
    }

    [Register("NativeChoiceCell")]
    private sealed class ChoiceCell : UICollectionViewCell
    {
        private readonly UIButton _button = new(UIButtonType.Custom);
        private readonly UILabel _text = SliceUi.Label("", 22);
        private readonly UILabel _mark = SliceUi.Label("●", 14);
        private Action? _select;
        private bool _numeric;
        public ChoiceCell(ObjCRuntime.NativeHandle handle) : base(handle)
        {
            BackgroundColor = NativeTheme.Surface;
            var activate = WeakUiCallback.Create(this, static cell => cell._select?.Invoke());
            _button.TouchUpInside += (_, _) => activate();
            _button.IsAccessibilityElement = true;
            _text.UserInteractionEnabled = false;
            _mark.TextColor = NativeTheme.Primary;
            _mark.UserInteractionEnabled = false;
            _mark.IsAccessibilityElement = false;
            _button.AddSubviews(_text, _mark);
            ContentView.AddSubview(_button);
        }
        public void Bind(ChoiceRow row, bool numeric, bool scalesNumericText, UITraitCollection traits)
        {
            _numeric = numeric;
            _mark.Hidden = numeric || !row.Selected;
            _select = row.Select;
            _button.AccessibilityIdentifier = row.Id;
            _button.AccessibilityTraits = row.Selected ? UIAccessibilityTrait.Button | UIAccessibilityTrait.Selected : UIAccessibilityTrait.Button;
            _button.AccessibilityLabel = row.Text;
            _text.Text = row.Text;
            var fontSize = numeric ? (row.Selected ? 56 : 32) : (row.Selected ? 28 : 22);
            _text.Font = scalesNumericText
                ? SourceScaledText.Font(fontSize, row.Selected, traits)
                : NativeTheme.Font(fontSize, row.Selected);
            _text.Lines = numeric ? 1 : 0;
            _text.TextColor = row.Selected ? NativeTheme.Primary : NativeTheme.TextPrimary;
            _text.TextAlignment = numeric ? UITextAlignment.Center : UITextAlignment.Left;
            SetNeedsLayout();
        }
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            _button.Frame = ContentView.Bounds;
            var marker = _mark.Hidden ? CGSize.Empty : SliceUi.Measure(_mark, Bounds.Width);
            if (_numeric)
                _text.Frame = ContentView.Bounds;
            else
            {
                _text.Frame = new CGRect(24, 18, Bounds.Width - 48 - marker.Width, Bounds.Height - 36);
                _mark.Frame = new CGRect(Bounds.Width - 24 - marker.Width, (Bounds.Height - marker.Height) / 2, marker.Width, marker.Height);
            }
        }
    }
}
