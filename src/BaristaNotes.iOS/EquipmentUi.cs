using CoreGraphics;
using Foundation;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class EquipmentHeader : UIView
{
    private readonly UILabel _caption = new();
    private readonly UILabel _title = new() { Lines = 2 };
    private string _captionText = "";
    private string _titleText = "";
    private readonly bool _adaptiveTitle;
    public nfloat TopInset { get; set; }

    public EquipmentHeader(string id, bool adaptiveTitle = false, bool wrapTitle = false)
    {
        AccessibilityIdentifier = id;
        BackgroundColor = NativeTheme.Surface;
        _adaptiveTitle = adaptiveTitle;
        if (wrapTitle) _title.LineBreakMode = UILineBreakMode.WordWrap;
        AddSubviews(_caption, _title);
    }

    public void Update(string caption, string title)
    {
        _captionText = caption;
        _titleText = title;
        UpdateFonts(TraitCollection);
    }

    public void UpdateFonts(UITraitCollection traits)
    {
        SourceScaledText.Tracked(_caption, _captionText, 10, 2, NativeTheme.Secondary, traits);
        var size = !_adaptiveTitle || _titleText.Length <= 12 ? 28 :
            _titleText.Length <= 20 ? 22 : _titleText.Length <= 28 ? 18 : 16;
        _title.Font = SourceScaledText.Font(size, true, traits);
        _title.Text = _titleText;
        _title.TextColor = NativeTheme.TextPrimary;
        SetNeedsLayout();
    }

    public override CGSize SizeThatFits(CGSize size) => new(size.Width, (nfloat)Math.Max(120,
        TopInset + 28 + SliceUi.Measure(_caption, size.Width - 32).Height +
        SliceUi.Measure(_title, size.Width - 32).Height));

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var caption = SliceUi.Measure(_caption, Bounds.Width - 32);
        var title = SliceUi.Measure(_title, Bounds.Width - 32);
        _caption.Frame = new CGRect(16, TopInset + 14, Bounds.Width - 32, caption.Height);
        _title.Frame = new CGRect(16, Bounds.Height - 14 - title.Height, Bounds.Width - 32, title.Height);
    }
}

internal sealed class EquipmentActionButton : UIButton
{
    private readonly bool _inverted;
    private readonly bool _danger;
    private string _text;

    public EquipmentActionButton(string text, string id, Action action, bool inverted = false, bool danger = false)
    {
        _text = text;
        _inverted = inverted;
        _danger = danger;
        AccessibilityIdentifier = id;
        TouchUpInside += (_, _) => action();
        UpdateFonts(TraitCollection);
    }

    public void SetText(string text)
    {
        _text = text;
        UpdateFonts(TraitCollection);
    }

    public void UpdateFonts(UITraitCollection traits)
    {
        var foreground = _inverted || _danger ? NativeTheme.Surface : NativeTheme.TextPrimary;
        BackgroundColor = _danger ? NativeTheme.Error : _inverted ? NativeTheme.TextPrimary : NativeTheme.Surface;
        TitleLabel.Font = SourceScaledText.Font(18, true, traits);
        using var title = new NSAttributedString(_text, new UIStringAttributes
        {
            Font = TitleLabel.Font, ForegroundColor = foreground, KerningAdjustment = 1
        });
        SetAttributedTitle(title, UIControlState.Normal);
        SetAttributedTitle(title, UIControlState.Disabled);
        AccessibilityLabel = _text;
        SetNeedsLayout();
    }

    public override CGSize SizeThatFits(CGSize size) =>
        new(TitleLabel.SizeThatFits(new CGSize(1000, 100)).Width + 16,
            (nfloat)Math.Max(72, TitleLabel.SizeThatFits(new CGSize(1000, 100)).Height + 48));

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var text = TitleLabel.SizeThatFits(new CGSize(Bounds.Width - 16, 1000));
        TitleLabel.Frame = new CGRect((Bounds.Width - text.Width) / 2,
            18 + (Bounds.Height - 48 - text.Height) / 2, text.Width, text.Height);
    }
}

internal enum EquipmentRowStyle { Management, SingleChoice, MultiChoice, Grind }

internal sealed class EquipmentTypeButton : UIButton
{
    public EquipmentTypeButton(string title, string id, Action action)
    {
        AccessibilityIdentifier = id;
        SetTitle(title, UIControlState.Normal);
        TitleLabel.Lines = 0;
        TitleLabel.LineBreakMode = UILineBreakMode.WordWrap;
        TitleLabel.TextAlignment = UITextAlignment.Center;
        TouchUpInside += (_, _) => action();
    }

    public override CGSize SizeThatFits(CGSize size) =>
        new(size.Width, (nfloat)Math.Max(44,
            TitleLabel.SizeThatFits(new CGSize((nfloat)Math.Max(1, size.Width - 16), nfloat.MaxValue)).Height));

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var text = TitleLabel.SizeThatFits(new CGSize((nfloat)Math.Max(1, Bounds.Width - 16), nfloat.MaxValue));
        TitleLabel.Frame = new CGRect(8, (Bounds.Height - text.Height) / 2, Bounds.Width - 16, text.Height);
    }
}

internal sealed record EquipmentRow(string Id, string Text, Action Activate,
    bool Selected = false, bool Preferred = true, string? Caption = null);

// Separate from the numeric primitive under review: this owner also supports
// source multi-choice rows and Grind's deliberately different outside-range rows.
internal sealed class EquipmentRows : UICollectionView
{
    private readonly RowsSource _source;
    private readonly RowsLayout _layout;
    private bool _centerPending;
    private nfloat _width;

    public EquipmentRows(EquipmentRowStyle style, string id)
        : base(CGRect.Empty, new UICollectionViewFlowLayout
        {
            MinimumLineSpacing = 0, MinimumInteritemSpacing = 0,
            ScrollDirection = UICollectionViewScrollDirection.Vertical
        })
    {
        AccessibilityIdentifier = id;
        BackgroundColor = NativeTheme.Surface;
        ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
        _source = new RowsSource(style);
        _layout = new RowsLayout(_source);
        DataSource = _source;
        Delegate = _layout;
        RegisterClassForCell(typeof(RowCell), "equipment-row");
    }

    public void Update(IReadOnlyList<EquipmentRow> rows, UITraitCollection traits, bool center = false)
    {
        _source.Rows = rows;
        _source.FontTraits = traits;
        _centerPending |= center;
        ReloadData();
        CollectionViewLayout.InvalidateLayout();
        SetNeedsLayout();
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        if (_width != Bounds.Width)
        {
            _width = Bounds.Width;
            CollectionViewLayout.InvalidateLayout();
        }
        var selectedIndex = _source.Rows.ToList().FindIndex(row => row.Selected);
        var inset = selectedIndex >= 0 && _source.Style != EquipmentRowStyle.Management ? Bounds.Height / 2 : 0;
        if (ContentInset.Top != inset) ContentInset = new UIEdgeInsets(inset, 0, inset, 0);
        if (!_centerPending || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        if (selectedIndex < 0) { _centerPending = false; return; }
        var attributes = GetLayoutAttributesForItem(NSIndexPath.FromItemSection(selectedIndex, 0));
        if (attributes == null) return;
        var minimum = -ContentInset.Top;
        var maximum = (nfloat)Math.Max(minimum, ContentSize.Height - Bounds.Height + ContentInset.Bottom);
        SetContentOffset(new CGPoint(0, (nfloat)Math.Clamp(
            attributes.Frame.Y + attributes.Frame.Height / 2 - Bounds.Height / 2, minimum, maximum)), false);
        _centerPending = false;
    }

    private sealed class RowsSource(EquipmentRowStyle style) : UICollectionViewDataSource
    {
        public EquipmentRowStyle Style { get; } = style;
        public IReadOnlyList<EquipmentRow> Rows { get; set; } = [];
        public UITraitCollection FontTraits { get; set; } = new();
        public override nint GetItemsCount(UICollectionView collectionView, nint section) => Rows.Count;
        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (RowCell)collectionView.DequeueReusableCell("equipment-row", indexPath);
            cell.Bind(Rows[(int)indexPath.Item], Style, FontTraits);
            return cell;
        }
    }

    private sealed class RowsLayout(RowsSource source) : UICollectionViewDelegateFlowLayout
    {
        private readonly UILabel _measure = new() { Lines = 0 };
        public override CGSize GetSizeForItem(UICollectionView collectionView, UICollectionViewLayout layout, NSIndexPath indexPath)
        {
            var row = source.Rows[(int)indexPath.Item];
            var width = collectionView.Bounds.Width;
            if (source.Style == EquipmentRowStyle.Grind)
                return new CGSize(width, row.Selected ? 96 : row.Preferred ? 72 : 48);
            if (source.Style == EquipmentRowStyle.Management)
            {
                _measure.Lines = 1;
                _measure.Text = row.Text;
                _measure.Font = SourceScaledText.Font(20, true, source.FontTraits);
                var value = SliceUi.Measure(_measure, width - 74).Height;
                _measure.Text = row.Caption;
                _measure.Font = SourceScaledText.Font(10, true, source.FontTraits);
                return new CGSize(width, (nfloat)Math.Max(80, value + SliceUi.Measure(_measure, width - 74).Height + 34) + 1);
            }
            var multiple = source.Style == EquipmentRowStyle.MultiChoice;
            _measure.Text = row.Text;
            _measure.Font = SourceScaledText.Font(multiple ? 22 : row.Selected ? 28 : 22,
                !multiple && row.Selected, source.FontTraits);
            var text = SliceUi.Measure(_measure, width - 48 - (multiple ? 49 : row.Selected ? 18 : 0)).Height;
            if (multiple) text = (nfloat)Math.Max(text, SourceScaledText.Font(24, false, source.FontTraits).LineHeight);
            return new CGSize(width, text + (multiple ? 32 : 36));
        }
    }

    [Register("EquipmentSourceRowCell")]
    private sealed class RowCell : UICollectionViewCell
    {
        private readonly UIButton _button = new(UIButtonType.Custom);
        private readonly UILabel _caption = new() { Lines = 1, LineBreakMode = UILineBreakMode.TailTruncation };
        private readonly UILabel _text = new() { Lines = 0 };
        private readonly UILabel _marker = new();
        private EquipmentRowStyle _style;
        private Action? _activate;

        public RowCell(ObjCRuntime.NativeHandle handle) : base(handle)
        {
            var activate = WeakUiCallback.Create(this, static cell => cell._activate?.Invoke());
            _button.TouchUpInside += (_, _) => activate();
            foreach (var label in new[] { _caption, _text, _marker })
            {
                label.UserInteractionEnabled = false;
                label.IsAccessibilityElement = false;
            }
            _button.AddSubviews(_caption, _text, _marker);
            ContentView.AddSubview(_button);
        }

        public void Bind(EquipmentRow row, EquipmentRowStyle style, UITraitCollection traits)
        {
            _style = style;
            _activate = row.Activate;
            _button.AccessibilityIdentifier = row.Id;
            _button.AccessibilityLabel = row.Caption == null ? row.Text : $"{row.Caption}: {row.Text}";
            _button.AccessibilityTraits = UIAccessibilityTrait.Button | (row.Selected ? UIAccessibilityTrait.Selected : UIAccessibilityTrait.None);
            BackgroundColor = NativeTheme.Outline;
            _button.BackgroundColor = NativeTheme.Surface;
            _text.Text = row.Text;
            _text.Lines = style == EquipmentRowStyle.Grind || style == EquipmentRowStyle.Management ? 1 : 0;
            _caption.Hidden = style != EquipmentRowStyle.Management;
            _marker.Hidden = style == EquipmentRowStyle.Grind || style == EquipmentRowStyle.SingleChoice && !row.Selected;
            if (style == EquipmentRowStyle.Management)
            {
                // Only the source's decorative chevron opts out of scaling.
                SourceScaledText.Tracked(_caption, row.Caption ?? "", 10, 2, NativeTheme.Secondary, traits);
                _text.Font = SourceScaledText.Font(20, true, traits);
                _text.TextColor = NativeTheme.TextPrimary;
                _text.LineBreakMode = UILineBreakMode.TailTruncation;
                _marker.Font = NativeTheme.Icons(24);
                _marker.Text = "\ue5cc";
                _marker.TextColor = NativeTheme.TextPrimary;
            }
            else
            {
                var size = style == EquipmentRowStyle.Grind ? row.Selected ? 56 : row.Preferred ? 32 : 20 :
                    style == EquipmentRowStyle.SingleChoice && row.Selected ? 28 : 22;
                _text.Font = SourceScaledText.Font(size, row.Selected && style != EquipmentRowStyle.MultiChoice, traits);
                _text.TextColor = style == EquipmentRowStyle.MultiChoice ? NativeTheme.TextPrimary :
                    row.Selected ? NativeTheme.Primary : row.Preferred ? NativeTheme.TextPrimary : NativeTheme.Secondary.ColorWithAlpha(0.5f);
                _marker.Font = SourceScaledText.Font(style == EquipmentRowStyle.MultiChoice ? 24 : 14, false, traits);
                _marker.Text = style == EquipmentRowStyle.MultiChoice ? row.Selected ? "■" : "□" : "●";
                _marker.TextColor = row.Selected ? NativeTheme.Primary : NativeTheme.TextPrimary;
            }
            _text.TextAlignment = style == EquipmentRowStyle.Grind ? UITextAlignment.Center : UITextAlignment.Left;
            SetNeedsLayout();
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            _button.Frame = new CGRect(0, 0, Bounds.Width, Bounds.Height - (_style == EquipmentRowStyle.Management ? 1 : 0));
            if (_style == EquipmentRowStyle.Grind) { _text.Frame = _button.Bounds; return; }
            var marker = SliceUi.Measure(_marker, Bounds.Width);
            if (_style == EquipmentRowStyle.Management)
            {
                var width = Bounds.Width - 42 - marker.Width;
                var caption = SliceUi.Measure(_caption, width);
                var text = SliceUi.Measure(_text, width);
                var y = (_button.Bounds.Height - caption.Height - text.Height) / 2;
                _caption.Frame = new CGRect(17, y, width, caption.Height);
                _text.Frame = new CGRect(17, y + caption.Height, width, text.Height);
                _marker.Frame = new CGRect(Bounds.Width - 17 - marker.Width, (_button.Bounds.Height - marker.Height) / 2, marker.Width, marker.Height);
            }
            else if (_style == EquipmentRowStyle.MultiChoice)
            {
                _marker.Frame = new CGRect(24, (Bounds.Height - marker.Height) / 2, marker.Width, marker.Height);
                _text.Frame = new CGRect(40 + marker.Width, 16, Bounds.Width - 64 - marker.Width, Bounds.Height - 32);
            }
            else
            {
                _text.Frame = new CGRect(24, 18, Bounds.Width - 48 - (_marker.Hidden ? 0 : marker.Width), Bounds.Height - 36);
                _marker.Frame = new CGRect(Bounds.Width - 24 - marker.Width, (Bounds.Height - marker.Height) / 2, marker.Width, marker.Height);
            }
        }
    }
}
