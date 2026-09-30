using CoreGraphics;
using Foundation;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class CoffeeField : UIView
{
    private readonly UILabel _caption;
    private readonly UIView _background = new() { BackgroundColor = NativeTheme.DarkVariant };
    private readonly UITextField? _entry;
    private readonly UITextView? _editor;
    private readonly UILabel? _placeholder;
    private readonly Action<string> _changed;
    public string Text
    {
        get => _entry?.Text ?? _editor?.Text ?? "";
        set
        {
            if (_entry != null) _entry.Text = value;
            if (_editor != null) _editor.Text = value;
            UpdatePlaceholder();
        }
    }
    public CoffeeField(string caption, string placeholder, string id, UITraitCollection traits,
        Action<string> changed, bool multiline = false)
    {
        _changed = changed;
        _caption = PhotoUi.Label(caption, 12, traits);
        _background.Layer.CornerRadius = multiline ? 16 : 25;
        AddSubviews(_caption, _background);
        var notify = WeakUiCallback.Create(this, static owner =>
        {
            owner.UpdatePlaceholder();
            owner._changed(owner.Text);
        });
        if (multiline)
        {
            _editor = new UITextView
            {
                AccessibilityIdentifier = id, Font = PhotoUi.Font(14, traits),
                TextColor = NativeTheme.OnPrimary, BackgroundColor = UIColor.Clear
            };
            _editor.Changed += (_, _) => notify();
            _placeholder = PhotoUi.Label(placeholder, 14, traits);
            _placeholder.UserInteractionEnabled = false;
            AddSubviews(_editor, _placeholder);
        }
        else
        {
            _entry = new UITextField
            {
                AccessibilityIdentifier = id, Font = PhotoUi.Font(14, traits), BorderStyle = UITextBorderStyle.None,
                TextColor = NativeTheme.OnPrimary, BackgroundColor = UIColor.Clear,
                LeftView = new UIView(new CGRect(0, 0, 4, 0)), LeftViewMode = UITextFieldViewMode.Always,
                ReturnKeyType = UIReturnKeyType.Done
            };
            _entry.ShouldReturn = static field =>
            {
                field.ResignFirstResponder();
                return true;
            };
            using var prompt = new NSAttributedString(placeholder,
                new UIStringAttributes { ForegroundColor = NativeTheme.DarkSecondary, Font = _entry.Font });
            _entry.AttributedPlaceholder = prompt;
            _entry.EditingChanged += (_, _) => notify();
            AddSubview(_entry);
        }
        if (_editor != null)
        {
            var done = WeakUiCallback.Create(this, static owner => owner._editor?.ResignFirstResponder());
            var toolbar = new UIToolbar(new CGRect(0, 0, 320, 44));
            toolbar.SetItems([new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
                new UIBarButtonItem(UIBarButtonSystemItem.Done, (_, _) => done())], false);
            _editor.InputAccessoryView = toolbar;
        }
    }
    public void SetEditable(bool editable)
    {
        if (_entry != null) _entry.Enabled = editable;
        if (_editor != null) _editor.Editable = editable;
    }
    private void UpdatePlaceholder() { if (_placeholder != null) _placeholder.Hidden = Text.Length != 0; }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        SliceUi.Measure(_caption, size.Width - 16).Height + 4 + (_editor == null ? 50 : 80));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var captionHeight = SliceUi.Measure(_caption, Bounds.Width - 16).Height;
        _caption.Frame = new CGRect(16, 0, Bounds.Width - 16, captionHeight);
        _background.Frame = new CGRect(0, captionHeight + 4, Bounds.Width, _editor == null ? 50 : 80);
        if (_entry != null)
        {
            var entryHeight = _entry.SizeThatFits(new CGSize(Bounds.Width - 32, 50)).Height;
            _entry.Frame = new CGRect(16, captionHeight + 4 + (50 - entryHeight) / 2, Bounds.Width - 32, entryHeight);
        }
        if (_editor != null) _editor.Frame = new CGRect(16, captionHeight + 12, Bounds.Width - 32, 64);
        if (_placeholder != null && _editor != null)
            _placeholder.Frame = new CGRect(16 + _editor.TextContainer.LineFragmentPadding,
                captionHeight + 12 + _editor.TextContainerInset.Top,
                Bounds.Width - 32 - 2 * _editor.TextContainer.LineFragmentPadding, 40);
    }
}

internal sealed class CoffeeDateField : UIView
{
    private readonly UILabel _caption, _value;
    private readonly UIView _chip = new();
    private readonly UITextField _input = new();
    private readonly UIDatePicker _picker = new() { Mode = UIDatePickerMode.Date, PreferredDatePickerStyle = UIDatePickerStyle.Wheels };
    private readonly Action<DateTime> _changed;
    public DateTime Value { get; private set; }
    public CoffeeDateField(DateTime value, UITraitCollection traits, Action<DateTime> changed)
    {
        _changed = changed;
        _caption = PhotoUi.Label("Roast Date *", 12, traits);
        _value = PhotoUi.Label("", 14, traits);
        _value.TextAlignment = UITextAlignment.Center;
        _chip.Layer.CornerRadius = 14;
        _chip.AddSubview(_value);
        _input.AccessibilityIdentifier = "coffee.date";
        _input.AccessibilityLabel = "Roast date";
        _input.TextColor = _input.TintColor = UIColor.Clear;
        _input.BackgroundColor = UIColor.Clear;
        _input.BorderStyle = UITextBorderStyle.None;
        _input.ShouldChangeCharacters = static (_, _, _) => false;
        _input.InputView = _picker;
        _picker.MaximumDate = ToNative(DateTime.Today);
        Set(value);
        var notify = WeakUiCallback.Create(this, static owner =>
        {
            var date = DateTime.UnixEpoch.AddSeconds(owner._picker.Date.SecondsSince1970).ToLocalTime().Date;
            owner.Set(date);
            owner._changed(date);
        });
        _picker.ValueChanged += (_, _) => notify();
        var done = WeakUiCallback.Create(this, static owner => owner._input.ResignFirstResponder());
        var toolbar = new UIToolbar(new CGRect(0, 0, 320, 44));
        toolbar.SetItems([new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
            new UIBarButtonItem(UIBarButtonSystemItem.Done, (_, _) => done())], false);
        _input.InputAccessoryView = toolbar;
        AddSubviews(_caption, _chip, _input);
    }
    private static NSDate ToNative(DateTime value) => NSDate.FromTimeIntervalSince1970((value.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds);
    private void Set(DateTime value)
    {
        Value = value;
        _picker.SetDate(ToNative(value), false);
        var today = value.Date == DateTime.Today;
        _value.Text = today ? "Today" : value.ToString("MMM d, yyyy");
        _value.TextColor = today ? UIColor.White : NativeTheme.OnPrimary;
        _chip.BackgroundColor = today ? NativeTheme.Primary : NativeTheme.DarkVariant;
        SetNeedsLayout();
    }
    public void SetEditable(bool editable) => _input.Enabled = editable;
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, SliceUi.Measure(_caption, size.Width - 16).Height + 36);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var height = SliceUi.Measure(_caption, Bounds.Width - 16).Height;
        var value = SliceUi.Measure(_value, Bounds.Width - 32);
        _caption.Frame = new CGRect(16, 0, Bounds.Width - 16, height);
        _chip.Frame = new CGRect(0, height + 4, value.Width + 32, 32);
        _value.Frame = new CGRect(16, (32 - value.Height) / 2, value.Width, value.Height);
        _input.Frame = _chip.Frame;
    }
}

internal sealed class CoffeeChips : UIView
{
    private readonly UIScrollView _scroll = new() { ShowsHorizontalScrollIndicator = false, ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never };
    private readonly List<(UIControl Control, UILabel Label)> _chips = [];
    private readonly UITraitCollection _traits;
    private readonly Action<string> _select;
    public CoffeeChips(UITraitCollection traits, Action<string> select)
    {
        _traits = traits; _select = select;
        ClipsToBounds = false;
        _scroll.ClipsToBounds = true;
        AddSubview(_scroll);
    }
    public void Update(IReadOnlyList<string> pool, string? filter)
    {
        foreach (var (control, _) in _chips) control.RemoveFromSuperview();
        _chips.Clear();
        var matches = string.IsNullOrWhiteSpace(filter) ? pool : pool.Where(value => value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase));
        var weak = new WeakReference<CoffeeChips>(this);
        foreach (var value in matches.Take(3))
        {
            var label = PhotoUi.Label(value, 12, _traits, color: NativeTheme.OnPrimary);
            var control = new UIControl { BackgroundColor = NativeTheme.DarkVariant, AccessibilityLabel = value, IsAccessibilityElement = true, AccessibilityTraits = UIAccessibilityTrait.Button };
            control.Layer.CornerRadius = 14;
            control.AddSubview(label);
            control.TouchUpInside += (_, _) => { if (weak.TryGetTarget(out var owner)) owner._select(value); };
            _chips.Add((control, label));
            _scroll.AddSubview(control);
        }
        SetNeedsLayout();
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, _chips.Count == 0 ? 0 : 28);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _scroll.Frame = new CGRect(-20, 0, Bounds.Width + 40, Bounds.Height);
        nfloat x = 16;
        foreach (var (control, label) in _chips)
        {
            var size = SliceUi.Measure(label, 1000);
            control.Frame = new CGRect(x, 0, size.Width + 24, 28);
            label.Frame = new CGRect(12, (28 - size.Height) / 2, size.Width, size.Height);
            x += size.Width + 30;
        }
        _scroll.ContentSize = new CGSize(x + 10, 28);
    }
}
