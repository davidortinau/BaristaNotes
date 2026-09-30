using CoreGraphics;
using Foundation;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal static class BeanBagUi
{
    public static UILabel Text(string text, nfloat size, bool bold, UIColor color, UITraitCollection traits)
        => new() { Text = text, Font = SourceScaledText.Font(size, bold, traits), TextColor = color, Lines = 0 };
}

internal sealed class BeanMiniAction : UIButton
{
    public BeanMiniAction(string text, string id, Action action)
    {
        AccessibilityIdentifier = id;
        BackgroundColor = NativeTheme.TextPrimary;
        TouchUpInside += (_, _) => action();
        Update(text, TraitCollection);
    }
    public void Update(string text, UITraitCollection traits)
    {
        TitleLabel.Font = SourceScaledText.Font(11, true, traits);
        using var title = new NSAttributedString(text, new UIStringAttributes
        {
            Font = TitleLabel.Font, ForegroundColor = NativeTheme.Surface, KerningAdjustment = 1.5f
        });
        SetAttributedTitle(title, UIControlState.Normal);
        SetAttributedTitle(title, UIControlState.Disabled);
        AccessibilityLabel = text;
        SetNeedsLayout();
    }
    public override CGSize SizeThatFits(CGSize size)
    {
        var text = TitleLabel.SizeThatFits(new CGSize(1000, nfloat.MaxValue));
        return new CGSize(text.Width + 24, (nfloat)Math.Max(32, text.Height + 16));
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var text = TitleLabel.SizeThatFits(new CGSize(Bounds.Width - 24, nfloat.MaxValue));
        TitleLabel.Frame = new CGRect(12, (Bounds.Height - text.Height) / 2, Bounds.Width - 24, text.Height);
    }
}

internal sealed class BeanSection : UIView
{
    private readonly UILabel _caption = new();
    private readonly string _heading;
    private readonly nfloat _spacing;
    private readonly nfloat _minimum;
    private readonly List<UIView> _items = [];
    private readonly UIActivityIndicatorView _busy = new(UIActivityIndicatorViewStyle.Medium);
    private bool _isBusy;
    public BeanMiniAction? Action { get; }

    public BeanSection(string heading, string id, int spacing = 10, int minimum = 0,
        BeanMiniAction? action = null)
    {
        _heading = heading;
        _spacing = spacing;
        _minimum = minimum;
        Action = action;
        AccessibilityIdentifier = id;
        BackgroundColor = NativeTheme.Surface;
        AddSubview(_caption);
        if (action != null) AddSubview(action);
        _busy.Color = NativeTheme.Primary;
        AddSubview(_busy);
        UpdateFonts(TraitCollection);
    }
    public void UpdateFonts(UITraitCollection traits)
    {
        SourceScaledText.Tracked(_caption, _heading, 10, 2, NativeTheme.Secondary, traits);
        SetNeedsLayout();
    }
    public void SetItems(IEnumerable<UIView> views)
    {
        var next = views.ToList();
        foreach (var old in _items.Where(view => !next.Contains(view)))
        {
            if (old is UIActivityIndicatorView spinner) spinner.StopAnimating();
            old.RemoveFromSuperview();
        }
        _items.Clear();
        _items.AddRange(next);
        foreach (var item in _items) if (item.Superview != this) AddSubview(item);
        SetNeedsLayout();
    }
    public void SetBusy(bool busy)
    {
        _isBusy = busy;
        if (Action != null) Action.Hidden = busy;
        if (busy) _busy.StartAnimating(); else _busy.StopAnimating();
        SetNeedsLayout();
    }
    private CGSize ActionSize => _isBusy ? new CGSize(20, 20) : Action?.SizeThatFits(CGSize.Empty) ?? CGSize.Empty;
    private nfloat HeadingHeight(nfloat width) => (nfloat)Math.Max(
        SliceUi.Measure(_caption, width - 32 - ActionSize.Width).Height, ActionSize.Height);
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, (nfloat)Math.Max(_minimum,
        28 + HeadingHeight(size.Width) + _items.Sum(view => (double)view.SizeThatFits(new CGSize(size.Width - 32, nfloat.MaxValue)).Height + _spacing)));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var action = ActionSize;
        var heading = HeadingHeight(Bounds.Width);
        _caption.Frame = new CGRect(16, 14, Bounds.Width - 32 - action.Width, heading);
        if (Action != null) Action.Frame = new CGRect(Bounds.Width - 16 - action.Width, 14, action.Width, heading);
        _busy.Frame = new CGRect(Bounds.Width - 36, 14 + (heading - 20) / 2, 20, 20);
        var y = 14 + heading;
        foreach (var item in _items)
        {
            y += _spacing;
            var height = item.SizeThatFits(new CGSize(Bounds.Width - 32, nfloat.MaxValue)).Height;
            item.Frame = new CGRect(16, y, Bounds.Width - 32, height);
            y += height;
        }
    }
}

internal sealed class BeanField : UIView
{
    private readonly UILabel _caption = new();
    private readonly string _captionText;
    private readonly string _placeholder;
    private readonly nfloat _fontSize;
    private readonly nfloat _minimum;
    private readonly Action<string> _changed;
    public UITextField Entry { get; } = new();
    public string Text { get => Entry.Text ?? ""; set => Entry.Text = value; }

    public BeanField(string caption, string placeholder, string id, nfloat fontSize, nfloat minimum, Action<string> changed)
    {
        _captionText = caption;
        _placeholder = placeholder;
        _fontSize = fontSize;
        _minimum = minimum;
        _changed = changed;
        BackgroundColor = NativeTheme.Surface;
        Entry.AccessibilityIdentifier = id;
        Entry.AccessibilityLabel = caption;
        Entry.TextColor = NativeTheme.TextPrimary;
        Entry.BorderStyle = UITextBorderStyle.None;
        Entry.LeftView = new UIView(new CGRect(0, 0, 4, 0));
        Entry.LeftViewMode = UITextFieldViewMode.Always;
        Entry.ReturnKeyType = UIReturnKeyType.Done;
        Entry.ShouldReturn = static field => { field.ResignFirstResponder(); return true; };
        var action = WeakUiCallback.Create(this, static field => field._changed(field.Text));
        Entry.EditingChanged += (_, _) => action();
        AddSubviews(_caption, Entry);
        UpdateFonts(TraitCollection);
    }
    public void UpdateFonts(UITraitCollection traits)
    {
        SourceScaledText.Tracked(_caption, _captionText, 10, 2, NativeTheme.Secondary, traits);
        Entry.Font = SourceScaledText.Font(_fontSize, true, traits);
        using var placeholder = new NSAttributedString(_placeholder, new UIStringAttributes
        {
            Font = Entry.Font, ForegroundColor = NativeTheme.Secondary.ColorWithAlpha(.5f)
        });
        Entry.AttributedPlaceholder = placeholder;
        SetNeedsLayout();
    }
    private nfloat EntryHeight(nfloat width) => (nfloat)Math.Max(44, Entry.SizeThatFits(new CGSize(width - 32, nfloat.MaxValue)).Height);
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, (nfloat)Math.Max(_minimum,
        32 + SliceUi.Measure(_caption, size.Width - 32).Height + EntryHeight(size.Width)));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var caption = SliceUi.Measure(_caption, Bounds.Width - 32).Height;
        var entry = EntryHeight(Bounds.Width);
        var top = (Bounds.Height - caption - entry) / 2;
        _caption.Frame = new CGRect(16, top, Bounds.Width - 32, caption);
        Entry.Frame = new CGRect(16, top + caption, Bounds.Width - 32, entry);
    }
}

internal sealed class BeanNotes : UIView
{
    private readonly UILabel _caption = new();
    private readonly UILabel _placeholder = new() { Lines = 0, UserInteractionEnabled = false };
    private readonly Action<string> _changed;
    public UITextView Editor { get; } = new();
    public string Text
    {
        get => Editor.Text ?? "";
        set { Editor.Text = value; _placeholder.Hidden = value.Length > 0; }
    }
    public BeanNotes(string placeholder, string id, Action<string> changed)
    {
        _changed = changed;
        BackgroundColor = NativeTheme.Surface;
        Editor.AccessibilityIdentifier = id;
        Editor.AccessibilityLabel = "NOTES";
        Editor.TextColor = NativeTheme.TextPrimary;
        Editor.BackgroundColor = UIColor.Clear;
        _placeholder.Text = placeholder;
        _placeholder.TextColor = NativeTheme.Secondary.ColorWithAlpha(.5f);
        _placeholder.IsAccessibilityElement = false;
        var action = WeakUiCallback.Create(this, static field =>
        {
            field._placeholder.Hidden = field.Text.Length > 0;
            field._changed(field.Text);
        });
        Editor.Changed += (_, _) => action();
        AddSubviews(_caption, Editor, _placeholder);
        UpdateFonts(TraitCollection);
    }
    public void UpdateFonts(UITraitCollection traits)
    {
        SourceScaledText.Tracked(_caption, "NOTES", 10, 2, NativeTheme.Secondary, traits);
        Editor.Font = _placeholder.Font = SourceScaledText.Font(16, false, traits);
        SetNeedsLayout();
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        (nfloat)Math.Max(150, 128 + SliceUi.Measure(_caption, size.Width - 32).Height));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var caption = SliceUi.Measure(_caption, Bounds.Width - 32).Height;
        _caption.Frame = new CGRect(16, 14, Bounds.Width - 32, caption);
        Editor.Frame = new CGRect(16, 14 + caption, Bounds.Width - 32, 100);
        var x = 16 + Editor.TextContainerInset.Left + Editor.TextContainer.LineFragmentPadding;
        _placeholder.Frame = new CGRect(x, Editor.Frame.Y + Editor.TextContainerInset.Top, Bounds.Width - x - 16,
            SliceUi.Measure(_placeholder, Bounds.Width - x - 16).Height);
    }
}

internal sealed class BeanSpacer(nfloat height) : UIView
{
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, height);
}

internal sealed class BeanVerticalGroup : UIView
{
    private readonly nfloat _spacing;
    private readonly UIView[] _views;
    public BeanVerticalGroup(nfloat spacing, params UIView[] views)
    {
        _spacing = spacing;
        _views = views;
        AddSubviews(views);
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        (nfloat)_views.Sum(view => (double)view.SizeThatFits(new CGSize(size.Width, nfloat.MaxValue)).Height) + Math.Max(0, _views.Length - 1) * _spacing);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        nfloat y = 0;
        foreach (var view in _views)
        {
            var height = view.SizeThatFits(new CGSize(Bounds.Width, nfloat.MaxValue)).Height;
            view.Frame = new CGRect(0, y, Bounds.Width, height);
            y += height + _spacing;
        }
    }
}

internal abstract class BeanBagPage : SliceViewController
{
    protected EquipmentHeader Header { get; }
    protected UIScrollView Scroll { get; } = new() { ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never,
        KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag };
    protected UIView Content { get; } = new();
    protected List<UIView> Sections { get; } = [];
    protected List<EquipmentActionButton> Actions { get; } = [];
    private readonly UIView _remainder = new() { BackgroundColor = NativeTheme.Surface };
    private readonly UIView _errorPanel = new() { BackgroundColor = NativeTheme.Error, Hidden = true };
    private readonly UILabel _errorCaption = new();
    private readonly UILabel _error = new() { Lines = 0 };
    private readonly UIActivityIndicatorView _spinner = new(UIActivityIndicatorViewStyle.Medium);
    private NSObject? _keyboard;
    private nfloat _keyboardOverlap;
    protected bool Visible { get; private set; }
    protected bool Removed { get; private set; }
    protected int Generation { get; private set; }

    protected BeanBagPage(SliceNavigationController host, string id) : base(host)
    {
        Header = new EquipmentHeader(id + ".header", adaptiveTitle: true, wrapTitle: true);
        Scroll.AccessibilityIdentifier = id + ".scroll";
        _errorPanel.AccessibilityIdentifier = id + ".error";
    }
    protected bool Current(int generation) => !Removed && generation == Generation && Visible && Host.TopViewController == this;
    protected bool Alive(int generation) => !Removed && generation == Generation;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = Content.BackgroundColor = NativeTheme.Outline;
        Scroll.BackgroundColor = NativeTheme.Surface;
        _spinner.Color = NativeTheme.Primary;
        _errorPanel.AddSubviews(_errorCaption, _error);
        Content.AddSubviews(_errorPanel, _remainder);
        Scroll.AddSubview(Content);
        Root.AddSubviews(Header, Scroll, _spinner);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((BeanBagPage)environment).RefreshFonts());
    }
    protected void AddSection(UIView view)
    {
        Sections.Add(view);
        Content.AddSubview(view);
    }
    protected void AddAction(EquipmentActionButton action) { Actions.Add(action); Root.AddSubview(action); }
    protected void SetError(string? error)
    {
        _error.Text = error;
        _errorPanel.Hidden = string.IsNullOrEmpty(error);
        Relayout();
    }
    protected void SetLoading(bool loading)
    {
        Scroll.Hidden = loading;
        if (loading) _spinner.StartAnimating(); else _spinner.StopAnimating();
    }
    protected void Relayout()
    {
        if (!Removed) Root.SetNeedsLayout();
    }
    protected virtual void RefreshFonts()
    {
        Header.UpdateFonts(TraitCollection);
        SourceScaledText.Tracked(_errorCaption, "ERROR", 10, 2, NativeTheme.Surface.ColorWithAlpha(.8f), TraitCollection);
        _error.Font = SourceScaledText.Font(16, true, TraitCollection);
        _error.TextColor = NativeTheme.Surface;
        foreach (var action in Actions) action.UpdateFonts(TraitCollection);
        Relayout();
    }
    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        Visible = true;
        var weak = new WeakReference<BeanBagPage>(this);
        _keyboard?.Dispose();
        _keyboard = UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
        {
            if (!weak.TryGetTarget(out var page) || page.Removed) return;
            var frame = page.Root.ConvertRectFromView(args.FrameEnd, null);
            page._keyboardOverlap = (nfloat)Math.Max(0, page.Root.Bounds.Height - frame.Y);
            page.Relayout();
        });
        RefreshFonts();
    }
    public override void ViewDidDisappear(bool animated)
    {
        Visible = false;
        _keyboard?.Dispose();
        _keyboard = null;
        _spinner.StopAnimating();
        base.ViewDidDisappear(animated);
    }
    public override void DidMoveToParentViewController(UIViewController? parent)
    {
        base.DidMoveToParentViewController(parent);
        if (parent == null)
        {
            Removed = true;
            Generation++;
            _keyboard?.Dispose();
            _keyboard = null;
            OnRemoved();
        }
    }
    protected virtual void OnRemoved() { }
#pragma warning disable CS0672, CA1422
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        if (!OperatingSystem.IsIOSVersionAtLeast(17) && IsViewLoaded) RefreshFonts();
    }
#pragma warning restore CS0672, CA1422
    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var width = Root.Bounds.Width;
        var height = Root.Bounds.Height - _keyboardOverlap;
        Header.TopInset = Root.SafeAreaInsets.Top;
        var headerHeight = Header.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;
        var bottom = Actions.Where(button => !button.Hidden).Select(button => button.SizeThatFits(CGSize.Empty).Height).DefaultIfEmpty(72).Max();
        Header.Frame = new CGRect(0, 0, width, headerHeight);
        Scroll.Frame = new CGRect(0, headerHeight + 1, width, (nfloat)Math.Max(1, height - headerHeight - bottom - 2));
        _spinner.Center = new CGPoint(width / 2, headerHeight + Scroll.Bounds.Height / 2);
        nfloat y = 0;
        foreach (var section in Sections.Where(view => !view.Hidden))
        {
            var sectionHeight = section.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;
            section.Frame = new CGRect(0, y, width, sectionHeight);
            y += sectionHeight + 1;
        }
        if (!_errorPanel.Hidden)
        {
            var caption = SliceUi.Measure(_errorCaption, width - 32).Height;
            var error = SliceUi.Measure(_error, width - 32).Height;
            var panelHeight = (nfloat)Math.Max(60, 24 + caption + error);
            _errorPanel.Frame = new CGRect(0, y, width, panelHeight);
            _errorCaption.Frame = new CGRect(16, 12, width - 32, caption);
            _error.Frame = new CGRect(16, 12 + caption, width - 32, error);
            y += panelHeight + 1;
        }
        _remainder.Frame = new CGRect(0, y, width, (nfloat)Math.Max(24, Scroll.Bounds.Height - y));
        Content.Frame = new CGRect(0, 0, width, _remainder.Frame.Bottom);
        Scroll.ContentSize = Content.Bounds.Size;
        var actions = Actions.Where(button => !button.Hidden).ToArray();
        var column = (width - actions.Length + 1) / Math.Max(1, actions.Length);
        for (var i = 0; i < actions.Length; i++) actions[i].Frame = new CGRect(i * (column + 1), height - bottom, column, bottom);
    }
}
