using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class FilterViewController : SliceViewController
{
    private readonly ShotFilterCriteria _working;
    private readonly List<BeanFilterOptionDto> _availableBeans;
    private readonly List<UserProfileDto> _availablePeople;
    private readonly Action<ShotFilterCriteria> _apply;
    private readonly UIView _panel = new();
    private readonly UIScrollView _scroll = new();
    private readonly List<UIView> _content = [];
    private readonly List<(FilterChip Button, List<int> Values, int Id)> _chips = [];
    private UIButton? _backdrop;
    private UIButton? _close;
    private UIButton? _applyButton;
    private UIButton? _clear;
    private UILabel? _title;
    private PresentationPhase _phase = PresentationPhase.Preparing;
    private bool _disposed;

    private enum PresentationPhase { Preparing, Appearing, Open, Disappearing, Closed }

    public FilterViewController(SliceNavigationController host, ShotFilterCriteria filters,
        List<BeanFilterOptionDto> beans, List<UserProfileDto> people, Action<ShotFilterCriteria> apply) : base(host)
    {
        _working = filters.Clone();
        _availableBeans = beans;
        _availablePeople = people;
        _apply = apply;
        ModalPresentationStyle = UIModalPresentationStyle.OverFullScreen;
        ModalInPresentation = true;
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = UIColor.Clear;
        Root.AccessibilityIdentifier = "filter.overlay";
        Root.AccessibilityViewIsModal = true;
        _backdrop = SliceUi.Button("", "filter.backdrop", Cancel);
        _backdrop.AccessibilityLabel = "Dismiss filter without applying";
        _backdrop.BackgroundColor = NativeTheme.Backdrop;
        _panel.BackgroundColor = NativeTheme.DarkSurface;
        _panel.AccessibilityIdentifier = "filter.panel";
        _panel.Hidden = true;
        _panel.Layer.CornerRadius = 24;
        _panel.ClipsToBounds = true;
        _title = SliceUi.Label("Filter Shots", 18, true);
        _title.TextColor = NativeTheme.OnPrimary;
        _title.TextAlignment = UITextAlignment.Center;
        _close = new UIButton(UIButtonType.Custom);
        _close.AccessibilityIdentifier = "filter.close";
        _close.SetTitle("\ue14c", UIControlState.Normal);
        _close.TouchUpInside += (_, _) => Cancel();
        _close.AccessibilityLabel = "Close";
        _close.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Normal);
        _close.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Disabled);
        _close.TitleLabel.Font = NativeTheme.Icons(24);
        _applyButton = SliceUi.Button("Apply", "filter.apply", () => Commit(_working));
        _applyButton.BackgroundColor = NativeTheme.Primary;
        _applyButton.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Normal);
        _applyButton.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Disabled);
        _applyButton.TitleLabel.Font = NativeTheme.SystemFont(16);
        _applyButton.Layer.CornerRadius = 20;
        _scroll.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
        AddSection("Beans");
        if (_availableBeans.Count == 0) AddEmpty("No beans with shots");
        else foreach (var bean in _availableBeans) AddChip(bean.Name, $"filter.bean.{bean.Id}", _working.BeanIds, bean.Id);
        AddSection("Made For");
        if (_availablePeople.Count == 0) AddEmpty("No people with shots");
        else foreach (var person in _availablePeople) AddChip(person.Name, $"filter.person.{person.Id}", _working.MadeForIds, person.Id);
        AddSection("Rating");
        string[] glyphs = ["\ue814", "\ue811", "\ue812", "\ue0ed", "\ue815"];
        for (var i = 0; i < glyphs.Length; i++) AddChip(glyphs[i], $"filter.rating.{i}", _working.Ratings, i, true);
        _clear = SliceUi.Button("Clear All", "filter.clear", () =>
        {
            if (_phase != PresentationPhase.Open) return;
            _working.Clear();
            Commit(_working);
        });
        _clear.TitleLabel.Font = NativeTheme.SystemFont(14);
        var spacer = new UIView { Tag = 4 };
        _content.Add(spacer);
        _scroll.AddSubview(spacer);
        _content.Add(_clear);
        _scroll.AddSubview(_clear);
        _panel.AddSubviews(_title, _close, _scroll, _applyButton);
        Root.AddSubviews(_backdrop, _panel);
        RefreshChips();
        SetInteractions(false);
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        if (_phase == PresentationPhase.Preparing)
            _ = AppearAsync();
    }

    private async Task AppearAsync()
    {
        try
        {
            Root.LayoutIfNeeded();
            _phase = PresentationPhase.Appearing;
            Logger.LogDebug("Filter appearing for {DurationMs} ms with {Curve}", 300, "CubicOut");
            await AnimatePanelAsync(appearing: true);
            if (_disposed || _phase != PresentationPhase.Appearing) return;
            _phase = PresentationPhase.Open;
            SetInteractions(true);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Filter appearing animation failed");
            if (!_disposed)
                DismissViewController(false, () => ShowFeedback("Could not open Filter Shots.", isError: true));
        }
    }

    private void Cancel() => _ = CloseAsync(null);
    private void Commit(ShotFilterCriteria value) => _ = CloseAsync(value);

    private async Task CloseAsync(ShotFilterCriteria? result)
    {
        if (_disposed || _phase != PresentationPhase.Open) return;
        var committed = result?.Clone();
        _phase = PresentationPhase.Disappearing;
        SetInteractions(false);
        try
        {
            Logger.LogDebug("Filter disappearing for {DurationMs} ms with {Curve}", 400, "CubicIn");
            await AnimatePanelAsync(appearing: false);
            if (_disposed) return;
            _phase = PresentationPhase.Closed;
            DismissViewController(false, () =>
            {
                if (committed != null) _apply(committed);
            });
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Filter disappearing animation failed");
            if (!_disposed)
            {
                _phase = PresentationPhase.Closed;
                DismissViewController(false, () => ShowFeedback("Could not complete the filter action.", isError: true));
            }
        }
    }

    private async Task AnimatePanelAsync(bool appearing)
    {
        var duration = appearing ? 0.3 : 0.4;
        var steps = (int)Math.Ceiling(duration / 0.016);
        var distance = (Root.Bounds.Height + _panel.Bounds.Height) / 2;
        var completed = await UIView.AnimateKeyframesAsync(
            duration, 0, UIViewKeyframeAnimationOptions.CalculationModeLinear, () =>
            {
                for (var step = 1; step <= steps; step++)
                {
                    var t = step / (double)steps;
                    var eased = appearing ? Math.Pow(t - 1, 3) + 1 : t * t * t;
                    var offset = distance * (nfloat)(appearing ? 1 - eased : eased);
                    UIView.AddKeyframeWithRelativeStartTime((step - 1) / (double)steps, 1.0 / steps,
                        () => _panel.Transform = CGAffineTransform.MakeTranslation(0, offset));
                }
            });
        if (!completed && !_disposed)
            throw new InvalidOperationException("The native Filter animation was interrupted.");
    }

    private void SetInteractions(bool enabled)
    {
        // The full-screen root continues to consume input while popup controls are disabled.
        _panel.UserInteractionEnabled = enabled;
        _scroll.ScrollEnabled = enabled;
        if (_backdrop != null) _backdrop.Enabled = enabled;
        if (_close != null) _close.Enabled = enabled;
        if (_applyButton != null) _applyButton.Enabled = enabled;
        foreach (var (button, _, _) in _chips) button.Enabled = enabled;
        if (_clear != null) _clear.Enabled = enabled && _working.HasFilters;
    }
    private void AddSection(string text)
    {
        var label = SliceUi.Label(text, 14, true);
        label.Font = NativeTheme.SystemFont(14, bold: true);
        label.TextColor = NativeTheme.OnPrimary;
        label.Tag = 1;
        _content.Add(label);
        _scroll.AddSubview(label);
    }
    private void AddEmpty(string text)
    {
        var label = SliceUi.Label(text, 14);
        label.Font = NativeTheme.SystemFont(14, italic: true);
        label.TextColor = NativeTheme.DarkSecondary;
        label.Tag = 2;
        _content.Add(label);
        _scroll.AddSubview(label);
    }
    private void AddChip(string text, string id, List<int> values, int value, bool icon = false)
    {
        var button = new FilterChip(text, id, icon, () =>
        {
            if (_phase != PresentationPhase.Open) return;
            if (!values.Remove(value)) values.Add(value);
            RefreshChips();
        });
        if (icon)
        {
            button.AccessibilityLabel = $"Rating {value}";
        }
        _chips.Add((button, values, value));
        var flow = _content.LastOrDefault() as FilterChipFlow;
        if (flow == null)
        {
            flow = new FilterChipFlow();
            _content.Add(flow);
            _scroll.AddSubview(flow);
        }
        flow.Add(button);
    }
    private void RefreshChips()
    {
        foreach (var (button, values, id) in _chips)
        {
            var selected = values.Contains(id);
            button.SetSelection(selected);
            button.AccessibilityTraits = selected ? UIAccessibilityTrait.Button | UIAccessibilityTrait.Selected : UIAccessibilityTrait.Button;
        }
        if (_clear != null)
        {
            _clear.Enabled = _phase == PresentationPhase.Open && _working.HasFilters;
            var color = _working.HasFilters ? NativeTheme.Primary : NativeTheme.DarkSecondary;
            _clear.SetTitleColor(color, UIControlState.Normal);
            _clear.SetTitleColor(color, UIControlState.Disabled);
        }
    }
    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = Root.SafeAreaInsets;
        var width = Root.Bounds.Width - safe.Left - safe.Right;
        var top = safe.Top;
        if (_backdrop != null) _backdrop.Frame = Root.Bounds;
        var panelHeight = Root.Bounds.Height - top;
        _panel.Bounds = new CGRect(0, 0, width, panelHeight);
        _panel.Center = new CGPoint(safe.Left + width / 2, top + panelHeight / 2);
        var titleSize = _title == null ? CGSize.Empty : SliceUi.Measure(_title, width - 48);
        var closeSize = _close?.TitleLabel.SizeThatFits(new CGSize(100, 100)) ?? CGSize.Empty;
        var headerHeight = (nfloat)Math.Max(titleSize.Height, closeSize.Height - 4);
        if (_title != null) _title.Frame = new CGRect(24, 24, width - 48, titleSize.Height);
        if (_close != null) _close.Frame = new CGRect(width - 24 - closeSize.Width - 10,
            20 - (44 - closeSize.Height) / 2, closeSize.Width + 20, 44);
        if (_applyButton != null) _applyButton.Frame = new CGRect(24, _panel.Bounds.Height - 78, width - 48, 54);
        var bodyTop = 24 + headerHeight + 16;
        _scroll.Frame = new CGRect(24, bodyTop, width - 48, _panel.Bounds.Height - 94 - bodyTop);
        nfloat y = 0;
        foreach (var item in _content)
        {
            if (item is UILabel label)
            {
                var size = SliceUi.Measure(label, _scroll.Bounds.Width - 32);
                if (item.Tag == 1) y += 8;
                item.Frame = new CGRect(16, y, _scroll.Bounds.Width - 32, size.Height);
                y += size.Height + (item.Tag == 1 ? 4 : 0);
            }
            else if (ReferenceEquals(item, _clear))
            {
                var clearWidth = _clear?.SizeThatFits(new CGSize(_scroll.Bounds.Width - 32, 44)).Width ?? 44;
                item.Frame = new CGRect((_scroll.Bounds.Width - clearWidth) / 2, y, clearWidth, 44);
                y += 44;
            }
            else if (item is FilterChipFlow flow)
            {
                var size = flow.SizeThatFits(new CGSize(_scroll.Bounds.Width - 32, nfloat.MaxValue));
                flow.Frame = new CGRect(16, y, size.Width, size.Height);
                y += size.Height;
            }
            else { item.Frame = new CGRect(16, y, _scroll.Bounds.Width - 32, 8); y += 8; }
            y += 16;
        }
        _scroll.ContentSize = new CGSize(_scroll.Bounds.Width, (nfloat)Math.Max(0, y - 16));
        if (_phase == PresentationPhase.Preparing)
        {
            _panel.Transform = CGAffineTransform.MakeTranslation(0, (Root.Bounds.Height + panelHeight) / 2);
            _panel.Hidden = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        _disposed = true;
        if (disposing) _panel.Layer.RemoveAllAnimations();
        base.Dispose(disposing);
    }
}

internal sealed class FilterChip : UIButton
{
    private readonly UILabel _text;
    private readonly bool _icon;
    public bool HasExplicitWidth => _icon;
    public FilterChip(string text, string id, bool icon, Action action) : base()
    {
        _icon = icon;
        AccessibilityIdentifier = id;
        AccessibilityLabel = text;
        _text = new UILabel
        {
            Text = text, Font = icon ? NativeTheme.Icons(24) : NativeTheme.SystemFont(14),
            TextColor = NativeTheme.OnPrimary, TextAlignment = UITextAlignment.Center,
            Lines = 1, UserInteractionEnabled = false
        };
        Layer.CornerRadius = 20;
        Layer.BorderWidth = 1;
        AddSubview(_text);
        TouchUpInside += (_, _) => action();
    }
    public void SetSelection(bool selected)
    {
        BackgroundColor = selected ? NativeTheme.Primary : NativeTheme.DarkVariant;
        Layer.BorderColor = (selected ? NativeTheme.Primary : NativeTheme.DarkOutline).CGColor;
    }
    public override CGSize SizeThatFits(CGSize size) => new(
        _icon ? 56 : (nfloat)Math.Min(size.Width, Math.Max(60, SliceUi.Measure(_text, size.Width - 24).Width + 24)), 40);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _text.Frame = Bounds.Inset(_icon ? 8 : 12, 0);
    }
}

internal sealed class FilterChipFlow : UIView
{
    private readonly List<FilterChip> _chips = [];
    public void Add(FilterChip chip) { _chips.Add(chip); AddSubview(chip); }
    public override CGSize SizeThatFits(CGSize size)
    {
        nfloat x = 0, y = 0, line = 0, bottom = 0;
        foreach (var chip in _chips)
        {
            var desired = chip.SizeThatFits(size);
            // MAUI Flex's measure pass receives DesiredSize including margins,
            // then applies flex margins; its arrange pass uses explicit requests.
            var measured = new CGSize(desired.Width + 8, desired.Height + 8);
            if (x > 0 && x + measured.Width + 8 > size.Width) { x = 0; y += line; line = 0; }
            bottom = (nfloat)Math.Max(bottom, y + measured.Height);
            x += measured.Width + 8;
            line = (nfloat)Math.Max(line, measured.Height + 8);
        }
        return new CGSize(size.Width, bottom);
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        nfloat x = 0, y = 0, line = 0;
        foreach (var chip in _chips)
        {
            var size = chip.SizeThatFits(Bounds.Size);
            var slotWidth = chip.HasExplicitWidth ? size.Width : size.Width + 8;
            if (x > 0 && x + slotWidth + 8 > Bounds.Width) { x = 0; y += line; line = 0; }
            // LayoutExtensions.ComputeFrame centers explicit requests against
            // the flex slot using DesiredSize (which includes the trailing margin).
            var left = chip.HasExplicitWidth ? (slotWidth - (size.Width + 8)) / 2 : 0;
            var top = (size.Height - (size.Height + 8)) / 2;
            chip.Frame = new CGRect(x + left, y + top, size.Width, size.Height);
            x += slotWidth + 8;
            line = (nfloat)Math.Max(line, size.Height + 8);
        }
    }
}
