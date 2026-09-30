using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class RangeHeader : UIView
{
    private readonly UILabel _caption = new();
    private readonly UILabel _title = SliceUi.Label("", 28, true);
    public nfloat TopInset { get; set; }

    public RangeHeader(string caption, string title)
    {
        BackgroundColor = NativeTheme.Surface;
        SliceUi.TrackedText(_caption, caption, 12, 2, NativeTheme.Secondary);
        _title.Text = title;
        AddSubviews(_caption, _title);
    }

    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        (nfloat)Math.Max(120, TopInset + 28 +
            SliceUi.Measure(_caption, size.Width - 32).Height +
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

internal sealed class RangeTextTile : UIView
{
    private readonly UILabel[] _labels;
    private readonly nfloat _spacing;
    private readonly nfloat _verticalPadding;
    private readonly nfloat _minimumHeight;

    public RangeTextTile(nfloat spacing, nfloat verticalPadding, nfloat minimumHeight, params UILabel[] labels)
    {
        _labels = labels;
        _spacing = spacing;
        _verticalPadding = verticalPadding;
        _minimumHeight = minimumHeight;
        BackgroundColor = NativeTheme.Surface;
        AddSubviews(labels);
    }

    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        (nfloat)Math.Max(_minimumHeight, 2 * _verticalPadding +
            _labels.Sum(label => (double)SliceUi.Measure(label, size.Width - 32).Height) +
            Math.Max(0, _labels.Length - 1) * _spacing));

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        nfloat y = _verticalPadding;
        foreach (var label in _labels)
        {
            var height = SliceUi.Measure(label, Bounds.Width - 32).Height;
            label.Frame = new CGRect(16, y, Bounds.Width - 32, height);
            y += height + _spacing;
        }
    }
}

internal sealed class RangeLinkTile : UIControl
{
    private readonly UILabel _caption = new() { Lines = 1, LineBreakMode = UILineBreakMode.TailTruncation };
    private readonly UILabel _value = new() { Lines = 1, LineBreakMode = UILineBreakMode.TailTruncation };
    private readonly UILabel _status = SliceUi.Label("", 10);
    private readonly UILabel _icon = SliceUi.Label("\ue5cc", 24);
    private bool _interactive;
    private nfloat _captionSize = 12;
    private nfloat _captionSpacing = 1.5f;
    private string _captionText = "", _statusText = "";
    private bool _custom;

    public RangeLinkTile(string id, Action action)
    {
        AccessibilityIdentifier = id;
        BackgroundColor = NativeTheme.Surface;
        IsAccessibilityElement = true;
        _value.Font = NativeTheme.Font(18, true);
        _icon.Font = NativeTheme.Icons(24);
        AddSubviews(_caption, _value, _status, _icon);
        TouchUpInside += (_, _) => { if (_interactive) action(); };
    }

    public void UseSettingsCaption()
    {
        _captionSize = 10;
        _captionSpacing = 2;
    }

    public void Update(string caption, string value, string status, bool interactive, bool custom,
        string? accessibilityLabel = null, string icon = "\ue5cc")
    {
        _interactive = interactive;
        UserInteractionEnabled = interactive;
        AccessibilityTraits = interactive ? UIAccessibilityTrait.Button : UIAccessibilityTrait.StaticText;
        _captionText = caption;
        _statusText = status;
        _custom = custom;
        _value.Text = value;
        _value.TextColor = NativeTheme.TextPrimary;
        UpdateFonts(TraitCollection);
        _icon.Text = icon;
        _icon.TextColor = icon != "\ue5cc" ? NativeTheme.Primary :
            interactive ? NativeTheme.TextPrimary : NativeTheme.Secondary.ColorWithAlpha(0.35f);
        AccessibilityLabel = accessibilityLabel ?? $"{caption}. {value}. {status}.";
        Layer.BorderWidth = interactive ? 1 : 0;
        Layer.BorderColor = NativeTheme.Surface.CGColor;
        SetNeedsLayout();
    }

    public void UpdateFonts(UITraitCollection traits)
    {
        SourceScaledText.Tracked(_caption, _captionText, _captionSize, _captionSpacing, NativeTheme.Secondary, traits);
        SourceScaledText.Tracked(_status, _statusText, 10, 1, _custom ? NativeTheme.Primary : NativeTheme.Secondary, traits);
        _value.Font = SourceScaledText.Font(18, true, traits);
        SetNeedsLayout();
    }

    private nfloat Inset => 16 + (_interactive ? 1 : 0);
    private nfloat TrailingWidth => SliceUi.Measure(_icon, 100).Width +
        (string.IsNullOrEmpty(_status.Text) ? 0 : 8 + SliceUi.Measure(_status, 300).Width);
    public override CGSize SizeThatFits(CGSize size)
    {
        var width = (nfloat)Math.Max(1, size.Width - 2 * Inset - TrailingWidth - 8);
        return new CGSize(size.Width, (nfloat)Math.Max(80, 2 * Inset +
            Math.Max(SliceUi.Measure(_caption, width).Height + SliceUi.Measure(_value, width).Height,
                SliceUi.Measure(_icon, 100).Height)));
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        Layer.BorderColor = NativeTheme.Surface.GetResolvedColor(TraitCollection).CGColor;
        var icon = SliceUi.Measure(_icon, 100);
        var status = SliceUi.Measure(_status, 300);
        var width = (nfloat)Math.Max(1, Bounds.Width - 2 * Inset - TrailingWidth - 8);
        var caption = SliceUi.Measure(_caption, width);
        var value = SliceUi.Measure(_value, width);
        var y = (Bounds.Height - caption.Height - value.Height) / 2;
        _caption.Frame = new CGRect(Inset, y, width, caption.Height);
        _value.Frame = new CGRect(Inset, y + caption.Height, width, value.Height);
        _icon.Frame = new CGRect(Bounds.Width - Inset - icon.Width,
            (Bounds.Height - icon.Height) / 2, icon.Width, icon.Height);
        _status.Frame = new CGRect(_icon.Frame.Left - 8 - status.Width,
            (Bounds.Height - status.Height) / 2, status.Width, status.Height);
    }
}

internal sealed class RangeFieldTile : UIView
{
    private readonly UILabel _caption = new();
    private readonly UILabel _unit;
    public UITextField Entry { get; } = new();

    public RangeFieldTile(string caption, string unit, string placeholder, string id, Action<string> changed)
    {
        BackgroundColor = NativeTheme.Surface;
        SliceUi.TrackedText(_caption, caption, 12, 2, NativeTheme.Secondary);
        _unit = SliceUi.Label(unit, 14, secondary: true);
        Entry.AccessibilityIdentifier = id;
        Entry.AccessibilityLabel = $"{caption}, {unit}";
        Entry.Placeholder = placeholder;
        Entry.Font = NativeTheme.Font(22, true);
        Entry.TextColor = NativeTheme.TextPrimary;
        Entry.BorderStyle = UITextBorderStyle.None;
        Entry.LeftView = new UIView(new CGRect(0, 0, 4, 0));
        Entry.LeftViewMode = UITextFieldViewMode.Always;
        Entry.KeyboardType = UIKeyboardType.DecimalPad;
        Entry.EditingChanged += (_, _) => changed(Entry.Text ?? "");
        AddSubviews(_caption, Entry, _unit);
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        (nfloat)Math.Max(96, 32 + SliceUi.Measure(_caption, size.Width - 32).Height + 44));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var unit = SliceUi.Measure(_unit, Bounds.Width - 32);
        var width = (nfloat)Math.Max(1, Bounds.Width - 40 - unit.Width);
        var caption = SliceUi.Measure(_caption, width);
        var top = (Bounds.Height - caption.Height - 44) / 2;
        _caption.Frame = new CGRect(16, top, width, caption.Height);
        Entry.Frame = new CGRect(16, top + caption.Height, width, 44);
        _unit.Frame = new CGRect(Bounds.Width - 16 - unit.Width,
            (Bounds.Height - unit.Height) / 2, unit.Width, unit.Height);
    }
}

internal abstract class RangePageViewController : SliceViewController
{
    protected RangeHeader Header { get; }
    protected UIScrollView Scroll { get; } = new();
    protected UIView Content { get; } = new();
    protected List<UIView> Tiles { get; } = [];
    protected List<UIButton> Actions { get; } = [];
    private Foundation.NSObject? _keyboardObserver;
    private nfloat _keyboardOverlap;

    protected RangePageViewController(SliceNavigationController host, string caption, string title, string id) : base(host)
    {
        Header = new RangeHeader(caption, title);
        Scroll.AccessibilityIdentifier = id;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = Content.BackgroundColor = NativeTheme.Outline;
        Scroll.BackgroundColor = NativeTheme.Surface;
        Scroll.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
        Scroll.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;
        Scroll.AddSubview(Content);
        Root.AddSubviews(Header, Scroll);
    }

    protected void AddTile(UIView tile)
    {
        Tiles.Add(tile);
        Content.AddSubview(tile);
    }

    protected void AddAction(UIButton button)
    {
        Actions.Add(button);
        Root.AddSubview(button);
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        _keyboardObserver = UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
        {
            var frame = Root.ConvertRectFromView(args.FrameEnd, null);
            _keyboardOverlap = (nfloat)Math.Max(0, Root.Bounds.Height - frame.Y);
            Root.SetNeedsLayout();
        });
    }

    public override void ViewDidDisappear(bool animated)
    {
        _keyboardObserver?.Dispose();
        _keyboardObserver = null;
        _keyboardOverlap = 0;
        base.ViewDidDisappear(animated);
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var width = Root.Bounds.Width;
        Header.TopInset = Root.SafeAreaInsets.Top;
        var header = Header.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;
        Header.Frame = new CGRect(0, 0, width, header);
        var bottom = SliceUi.NavigationHeight(Actions);
        var usableHeight = Root.Bounds.Height - _keyboardOverlap;
        Scroll.Frame = new CGRect(0, header + 1, width, (nfloat)Math.Max(0, usableHeight - header - bottom - 2));
        nfloat y = 0;
        foreach (var tile in Tiles)
        {
            var height = tile.Hidden ? 0 : tile.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;
            tile.Frame = new CGRect(0, y, width, height);
            y += height + 1;
        }
        Content.Frame = new CGRect(0, 0, width, (nfloat)Math.Max(y, Scroll.Bounds.Height));
        Scroll.ContentSize = Content.Bounds.Size;
        if (Actions.Count == 0) return;
        var column = (width - Actions.Count + 1) / Actions.Count;
        for (var i = 0; i < Actions.Count; i++)
            Actions[i].Frame = new CGRect(i * (column + 1), usableHeight - bottom, column, bottom);
    }
}
