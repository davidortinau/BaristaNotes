using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class SettingsViewController(SliceNavigationController host) : SliceViewController(host)
{
    private readonly DrinkTile _header = new("SETTINGS", "settings.header", () => { }, valueFontSize: 28);
    private readonly UIScrollView _scroll = new();
    private readonly UIView _content = new();
    private readonly UILabel _manage = new();
    private readonly UILabel _appearanceCaption = new();
    private readonly UILabel _aboutCaption = new();
    private readonly SettingsAboutTile _about = new();
    private readonly UIView _filler = new() { BackgroundColor = NativeTheme.Surface };
    private readonly UIView[] _sectionBackgrounds = Enumerable.Range(0, 5)
        .Select(_ => new UIView { BackgroundColor = NativeTheme.Surface }).ToArray();
    private readonly Dictionary<ThemeMode, TemperatureUnitTile> _themeChoices = [];
    private ThemeMode _themeMode;
    private readonly UILabel _unitsCaption = new();
    private readonly UIView _unitDivider = new() { BackgroundColor = NativeTheme.Outline };
    private readonly UIView _unitsTopDivider = new() { BackgroundColor = NativeTheme.Outline };
    private readonly UIView _unitsBottomDivider = new() { BackgroundColor = NativeTheme.Outline };
    private readonly UILabel _rangesCaption = new();
    private readonly Dictionary<DrinkValueMetric, RangeLinkTile> _ranges = [];
    private readonly List<UIButton> _navigation = [];
    private readonly UIView _navigationTopBorder = SliceUi.NavigationTopBorder();
    private ProfilesLink? _profiles;
    private ProfilesLink? _equipment;
    private BeanSettingsLink? _beans;
    private TemperatureUnitTile? _fahrenheit;
    private TemperatureUnitTile? _celsius;
    private bool _visible;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = NativeTheme.Outline;
        _header.Enabled = false;
        _scroll.BackgroundColor = NativeTheme.Surface;
        _content.BackgroundColor = NativeTheme.Outline;
        _scroll.AccessibilityIdentifier = "settings.list";
        _scroll.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
        _content.AddSubviews(_sectionBackgrounds);
        _content.AddSubviews(_appearanceCaption, _aboutCaption, _about, _filler);
        foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System })
        {
            var label = mode == ThemeMode.System ? "AUTO" : mode.ToString().ToUpperInvariant();
            var tile = new TemperatureUnitTile(label, "○", $"settings.theme.{mode.ToString().ToLowerInvariant()}",
                WeakUiCallback.Create(this, mode, static (owner, value) => owner.SelectTheme(value)), indicator: true);
            _themeChoices.Add(mode, tile);
            _content.AddSubview(tile);
        }
        SliceUi.HeaderLabel(_manage, "MANAGE");
        _profiles = new ProfilesLink(WeakUiCallback.Create(this, static owner => owner.Host.Profiles()));
        _equipment = new ProfilesLink(WeakUiCallback.Create(this, static owner => owner.Host.Equipment()),
            "EQUIPMENT", "Machines, grinders, accessories", "settings.equipment");
        _beans = new BeanSettingsLink(WeakUiCallback.Create(this, static owner => owner.Host.Beans()));
        _content.AddSubviews(_manage, _equipment, _beans, _profiles);
        SliceUi.HeaderLabel(_unitsCaption, "UNITS");
        _fahrenheit = new TemperatureUnitTile("FAHRENHEIT", "\u00b0F", "settings.units.fahrenheit",
            WeakUiCallback.Create(this, static owner => owner.SelectTemperatureUnit(TemperatureUnit.Fahrenheit)));
        _celsius = new TemperatureUnitTile("CELSIUS", "\u00b0C", "settings.units.celsius",
            WeakUiCallback.Create(this, static owner => owner.SelectTemperatureUnit(TemperatureUnit.Celsius)));
        _content.AddSubviews(_unitsCaption, _fahrenheit, _celsius, _unitDivider,
            _unitsTopDivider, _unitsBottomDivider);
        SliceUi.HeaderLabel(_rangesCaption, "VALUE RANGES");
        _content.AddSubview(_rangesCaption);
        foreach (var metric in new[] { DrinkValueMetric.DoseIn, DrinkValueMetric.Yield, DrinkValueMetric.GrindMicrons, DrinkValueMetric.Time })
        {
            var tile = new RangeLinkTile($"ValueRange_{metric}",
                WeakUiCallback.Create(this, metric, static (owner, value) => owner.Host.Ranges(value)));
            tile.UseSettingsCaption();
            _ranges.Add(metric, tile);
            _content.AddSubview(tile);
        }
        _scroll.AddSubview(_content);
        _navigation.Add(SliceUi.Icon("\uefef", "New Drink", "nav.drink",
            WeakUiCallback.Create(this, static owner => owner.Host.NewDrink())));
        _navigation.Add(SliceUi.Icon("\uf009", "Activity", "nav.activity",
            WeakUiCallback.Create(this, static owner => owner.Host.Activity())));
        _navigation.Add(SliceUi.Icon("\ue029", "Voice", "nav.voice",
            WeakUiCallback.Create(this, static owner => _ = owner.Host.ToggleVoiceAsync(navigateToDrink: true))));
        Root.AddSubviews(_header, _scroll);
        Root.AddSubviews(_navigation.ToArray());
        Root.AddSubview(_navigationTopBorder);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
        {
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((SettingsViewController)environment).UpdateUnitFonts());
            RegisterForTraitChanges<UITraitUserInterfaceStyle>(static (environment, _) =>
                ((SettingsViewController)environment).UpdateUnitFonts());
        }
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        _visible = true;
        Services.Singleton<IDrinkValueRangeService>().SettingsChanged += OnRangeSettingsChanged;
        ReloadRanges();
        ReloadTheme();
        ReloadTemperatureUnit();
        UpdateUnitFonts();
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        Host.Performance.SettingsReady(Host);
    }

    public override void ViewDidDisappear(bool animated)
    {
        _visible = false;
        Services.Singleton<IDrinkValueRangeService>().SettingsChanged -= OnRangeSettingsChanged;
        base.ViewDidDisappear(animated);
    }

    private void OnRangeSettingsChanged(object? sender, EventArgs args) => BeginInvokeOnMainThread(() =>
    {
        if (_visible) ReloadRanges();
    });

    private void ReloadRanges()
    {
        try
        {
            var snapshot = Services.Singleton<IDrinkValueRangeService>().GetSettings();
            foreach (var (metric, tile) in _ranges)
            {
                var mode = snapshot.Modes.GetValueOrDefault(metric, ValueRangeMode.Auto);
                var count = snapshot.Overrides.Count(item => item.Metric == metric);
                var subtitle = mode == ValueRangeMode.Auto ? "Automatic by drink method" :
                    count == 1 ? "Custom - 1 drink method" : $"Custom - {count} drink methods";
                var title = DrinkValueRangeFormatting.MetricTitle(metric);
                tile.Update(title.ToUpperInvariant(), subtitle, mode == ValueRangeMode.Auto ? "AUTO" : "CUSTOM",
                    true, mode == ValueRangeMode.Custom, $"{title}. {subtitle}. {mode}.");
            }
            Root.SetNeedsLayout();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to load range settings summaries");
            ShowFeedback(exception.Message, isError: true);
        }
    }

    private void ReloadTemperatureUnit()
    {
        try
        {
            var unit = Services.Singleton<IPreferencesService>().GetTemperatureUnit();
            _fahrenheit?.SetSelected(unit == TemperatureUnit.Fahrenheit);
            _celsius?.SetSelected(unit == TemperatureUnit.Celsius);
            Root.SetNeedsLayout();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to read temperature unit");
            ShowFeedback(exception.Message, isError: true);
        }
    }

    private void UpdateUnitFonts()
    {
        SourceScaledText.Tracked(_appearanceCaption, "APPEARANCE", 10, 2, NativeTheme.Secondary, TraitCollection);
        SourceScaledText.Tracked(_manage, "MANAGE", 10, 2, NativeTheme.Secondary, TraitCollection);
        SourceScaledText.Tracked(_rangesCaption, "VALUE RANGES", 10, 2, NativeTheme.Secondary, TraitCollection);
        SourceScaledText.Tracked(_aboutCaption, "ABOUT", 10, 2, NativeTheme.Secondary, TraitCollection);
        SourceScaledText.Tracked(_unitsCaption, "UNITS", 10, 2, NativeTheme.Secondary, TraitCollection);
        _fahrenheit?.UpdateFonts(TraitCollection);
        _celsius?.UpdateFonts(TraitCollection);
        foreach (var tile in _themeChoices.Values) tile.UpdateFonts(TraitCollection);
        foreach (var tile in _ranges.Values) tile.UpdateFonts(TraitCollection);
        _profiles?.UpdateFonts(TraitCollection);
        _equipment?.UpdateFonts(TraitCollection);
        _about.UpdateFonts(TraitCollection);
        Root.SetNeedsLayout();
    }

    private void ReloadTheme()
    {
        _themeMode = ThemePreference.Read(Services.Singleton<IPreferencesStore>());
        _header.SetValue(_themeMode switch { ThemeMode.Light => "Light theme", ThemeMode.Dark => "Dark theme", _ => "System theme" });
        foreach (var (mode, tile) in _themeChoices) tile.SetSelected(mode == _themeMode);
    }

    private void SelectTheme(ThemeMode mode)
    {
        if (Host.TopViewController != this || Host.PresentedViewController != null || Host.FeedbackHost.IsShowing) return;
        try
        {
            var window = Root.Window ?? throw new InvalidOperationException("The Settings window is not available.");
            ThemePreference.Write(Services.Singleton<IPreferencesStore>(), mode);
            NativeAppearance.Apply(window, mode);
            ReloadTheme();
            UpdateUnitFonts();
            Logger.LogInformation("App theme changed to {Mode}; effective style {Style}", mode, window.TraitCollection.UserInterfaceStyle);
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Failed to change app theme to {Mode}", mode);
            ShowFeedback("Could not change theme.", isError: true);
        }
    }

    // iOS 15/16 do not support RegisterForTraitChanges.
#pragma warning disable CS0672, CA1422
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        if (!OperatingSystem.IsIOSVersionAtLeast(17) && IsViewLoaded &&
            (previousTraitCollection?.PreferredContentSizeCategory != TraitCollection.PreferredContentSizeCategory ||
             previousTraitCollection?.UserInterfaceStyle != TraitCollection.UserInterfaceStyle))
            UpdateUnitFonts();
    }
#pragma warning restore CS0672, CA1422

    private void SelectTemperatureUnit(TemperatureUnit unit)
    {
        if (Host.TopViewController != this || Host.FeedbackHost.IsShowing) return;
        try
        {
            Services.Singleton<IPreferencesService>().SetTemperatureUnit(unit);
            ReloadTemperatureUnit();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to save temperature unit {Unit}", unit);
            ShowFeedback(exception.Message, isError: true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _visible = false;
            Services.Singleton<IDrinkValueRangeService>().SettingsChanged -= OnRangeSettingsChanged;
        }
        base.Dispose(disposing);
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var width = Root.Bounds.Width;
        var topInset = (nfloat)Math.Max(0, Root.SafeAreaInsets.Top - 1);
        var headerHeight = _header.MeasureHeight(width - 2, topInset);
        var bottom = SliceUi.LayoutNavigation(Root, _navigation, _navigationTopBorder);
        _header.TopInset = topInset;
        _header.Frame = new CGRect(1, 1, width - 2, headerHeight);
        _scroll.Frame = new CGRect(1, headerHeight + 2, width - 2,
            (nfloat)Math.Max(0, Root.Bounds.Height - headerHeight - bottom - 3));
        var appearanceHeight = SliceUi.Measure(_appearanceCaption, _scroll.Bounds.Width - 32).Height;
        _appearanceCaption.Frame = new CGRect(16, 24, _scroll.Bounds.Width - 32, appearanceHeight);
        _sectionBackgrounds[0].Frame = new CGRect(0, 0, _scroll.Bounds.Width, appearanceHeight + 34);
        var themeTop = appearanceHeight + 35;
        var themeWidth = (_scroll.Bounds.Width - 2) / 3;
        var themeHeight = _themeChoices.Values.Max(tile => tile.SizeThatFits(new CGSize(themeWidth, nfloat.MaxValue)).Height);
        var themeIndex = 0;
        foreach (var tile in _themeChoices.Values)
            tile.Frame = new CGRect(themeIndex++ * (themeWidth + 1), themeTop, themeWidth, themeHeight);
        var manageTop = themeTop + themeHeight + 1;
        var captionHeight = SliceUi.Measure(_manage, _scroll.Bounds.Width - 32).Height;
        _manage.Frame = new CGRect(16, manageTop + 24, _scroll.Bounds.Width - 32, captionHeight);
        _sectionBackgrounds[1].Frame = new CGRect(0, manageTop, _scroll.Bounds.Width, captionHeight + 34);
        var linkTop = manageTop + 35 + captionHeight;
        var equipmentHeight = _equipment?.SizeThatFits(new CGSize(_scroll.Bounds.Width, nfloat.MaxValue)).Height ?? 80;
        if (_equipment != null) _equipment.Frame = new CGRect(0, linkTop, _scroll.Bounds.Width, equipmentHeight);
        linkTop += equipmentHeight + 1;
        var beansHeight = _beans?.SizeThatFits(new CGSize(_scroll.Bounds.Width, nfloat.MaxValue)).Height ?? 80;
        if (_beans != null) _beans.Frame = new CGRect(0, linkTop, _scroll.Bounds.Width, beansHeight);
        linkTop += beansHeight + 1;
        var linkHeight = _profiles?.SizeThatFits(new CGSize(_scroll.Bounds.Width, nfloat.MaxValue)).Height ?? 80;
        if (_profiles != null) _profiles.Frame = new CGRect(0, linkTop, _scroll.Bounds.Width, linkHeight);
        var unitsHeight = SliceUi.Measure(_unitsCaption, _scroll.Bounds.Width - 32).Height;
        _unitsCaption.Frame = new CGRect(16, linkTop + linkHeight + 25, _scroll.Bounds.Width - 32, unitsHeight);
        _sectionBackgrounds[2].Frame = new CGRect(0, linkTop + linkHeight + 1, _scroll.Bounds.Width, unitsHeight + 34);
        var unitTop = _unitsCaption.Frame.Bottom + 11;
        var unitWidth = (_scroll.Bounds.Width - 1) / 2;
        var unitHeight = (nfloat)Math.Max(
            _fahrenheit?.SizeThatFits(new CGSize(unitWidth, nfloat.MaxValue)).Height ?? 96,
            _celsius?.SizeThatFits(new CGSize(unitWidth, nfloat.MaxValue)).Height ?? 96);
        if (_fahrenheit != null) _fahrenheit.Frame = new CGRect(0, unitTop, unitWidth, unitHeight);
        if (_celsius != null) _celsius.Frame = new CGRect(unitWidth + 1, unitTop, unitWidth, unitHeight);
        _unitDivider.Frame = new CGRect(unitWidth, unitTop, 1, unitHeight);
        _unitsTopDivider.Frame = new CGRect(0, unitTop - 1, _scroll.Bounds.Width, 1);
        _unitsBottomDivider.Frame = new CGRect(0, unitTop + unitHeight, _scroll.Bounds.Width, 1);
        var rangeCaptionHeight = SliceUi.Measure(_rangesCaption, _scroll.Bounds.Width - 32).Height;
        _rangesCaption.Frame = new CGRect(16, unitTop + unitHeight + 25, _scroll.Bounds.Width - 32, rangeCaptionHeight);
        _sectionBackgrounds[3].Frame = new CGRect(0, unitTop + unitHeight + 1, _scroll.Bounds.Width, rangeCaptionHeight + 34);
        var y = _rangesCaption.Frame.Bottom + 11;
        foreach (var tile in _ranges.Values)
        {
            var height = tile.SizeThatFits(new CGSize(_scroll.Bounds.Width, nfloat.MaxValue)).Height;
            tile.Frame = new CGRect(0, y, _scroll.Bounds.Width, height);
            y += height + 1;
        }
        var aboutCaptionHeight = SliceUi.Measure(_aboutCaption, _scroll.Bounds.Width - 32).Height;
        _sectionBackgrounds[4].Frame = new CGRect(0, y, _scroll.Bounds.Width, aboutCaptionHeight + 34);
        _aboutCaption.Frame = new CGRect(16, y + 24, _scroll.Bounds.Width - 32, aboutCaptionHeight);
        y += aboutCaptionHeight + 35;
        var aboutHeight = _about.SizeThatFits(new CGSize(_scroll.Bounds.Width, nfloat.MaxValue)).Height;
        _about.Frame = new CGRect(0, y, _scroll.Bounds.Width, aboutHeight);
        y += aboutHeight + 1;
        _filler.Frame = new CGRect(0, y, _scroll.Bounds.Width, (nfloat)Math.Max(16, _scroll.Bounds.Height - y));
        _content.Frame = new CGRect(0, 0, _scroll.Bounds.Width, _filler.Frame.Bottom);
        _scroll.ContentSize = _content.Bounds.Size;
    }

    private sealed class TemperatureUnitTile : UIControl
    {
        private readonly UILabel _caption = new();
        private readonly UILabel _glyph = SliceUi.Label("", 28, true);
        private readonly string _label;
        private readonly bool _indicator;
        private bool _selected;

        public TemperatureUnitTile(string label, string glyph, string id, Action select, bool indicator = false)
        {
            _label = label;
            _indicator = indicator;
            _glyph.Text = glyph;
            AccessibilityIdentifier = id;
            AccessibilityLabel = $"{label}: {glyph}";
            IsAccessibilityElement = true;
            AddSubviews(_caption, _glyph);
            TouchUpInside += (_, _) => select();
        }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            if (_indicator)
            {
                _glyph.Text = selected ? "●" : "○";
                AccessibilityLabel = $"{_label}: {_glyph.Text}";
            }
            var foreground = selected ? NativeTheme.Surface : NativeTheme.TextPrimary;
            BackgroundColor = selected ? NativeTheme.TextPrimary : NativeTheme.Surface;
            _glyph.TextColor = foreground;
            AccessibilityTraits = UIAccessibilityTrait.Button |
                (selected ? UIAccessibilityTrait.Selected : UIAccessibilityTrait.None);
            UpdateFonts();
        }

        public void UpdateFonts(UITraitCollection? traits = null)
        {
            traits ??= TraitCollection;
            var foreground = _selected ? NativeTheme.Surface : NativeTheme.TextPrimary;
            SourceScaledText.Tracked(_caption, _label, 10, 2, foreground.ColorWithAlpha(0.7f), traits);
            _glyph.Font = SourceScaledText.Font(28, true, traits);
            SetNeedsLayout();
        }

        public override CGSize SizeThatFits(CGSize size) => new(size.Width, (nfloat)Math.Max(96,
            SliceUi.Measure(_caption, size.Width - 32).Height + SliceUi.Measure(_glyph, size.Width - 32).Height + 28));

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var caption = SliceUi.Measure(_caption, Bounds.Width - 32);
            var glyph = SliceUi.Measure(_glyph, Bounds.Width - 32);
            _caption.Frame = new CGRect(16, 14, Bounds.Width - 32, caption.Height);
            _glyph.Frame = new CGRect(16, Bounds.Height - 14 - glyph.Height, Bounds.Width - 32, glyph.Height);
        }
    }

    private sealed class ProfilesLink : UIControl
    {
        private readonly string _captionText;
        private readonly UILabel _caption = new() { Lines = 1, LineBreakMode = UILineBreakMode.TailTruncation };
        private readonly UILabel _value = SliceUi.Label("Coffee lovers", 20, true);
        private readonly UILabel _chevron = SliceUi.Label("\ue5cc", 24);

        public ProfilesLink(Action action, string caption = "PROFILES", string value = "Coffee lovers",
            string id = "settings.profiles")
        {
            _captionText = caption;
            AccessibilityIdentifier = id;
            AccessibilityLabel = $"{caption}: {value}";
            AccessibilityTraits = UIAccessibilityTrait.Button;
            IsAccessibilityElement = true;
            BackgroundColor = NativeTheme.Surface;
            SliceUi.HeaderLabel(_caption, caption);
            _value.Text = value;
            _value.Lines = 1;
            _value.LineBreakMode = UILineBreakMode.TailTruncation;
            _chevron.Font = NativeTheme.Icons(24);
            AddSubviews(_caption, _value, _chevron);
            TouchUpInside += (_, _) => action();
            UpdateFonts(TraitCollection);
        }

        public void UpdateFonts(UITraitCollection traits)
        {
            SourceScaledText.Tracked(_caption, _captionText, 10, 2, NativeTheme.Secondary, traits);
            _value.Font = SourceScaledText.Font(20, true, traits);
            _value.TextColor = _chevron.TextColor = NativeTheme.TextPrimary;
            SetNeedsLayout();
        }

        public override CGSize SizeThatFits(CGSize size) =>
            new(size.Width, (nfloat)Math.Max(80,
                SliceUi.Measure(_caption, size.Width - 64).Height +
                SliceUi.Measure(_value, size.Width - 64).Height + 32));

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var chevron = SliceUi.Measure(_chevron, Bounds.Width);
            var textWidth = (nfloat)Math.Max(1, Bounds.Width - 40 - chevron.Width);
            var caption = SliceUi.Measure(_caption, textWidth);
            var value = SliceUi.Measure(_value, textWidth);
            var top = (Bounds.Height - caption.Height - value.Height) / 2;
            _caption.Frame = new CGRect(16, top, textWidth, caption.Height);
            _value.Frame = new CGRect(16, top + caption.Height, textWidth, value.Height);
            _chevron.Frame = new CGRect(Bounds.Width - 16 - chevron.Width,
                (Bounds.Height - chevron.Height) / 2, chevron.Width, chevron.Height);
        }
    }
}
