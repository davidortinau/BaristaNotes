using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private NativeScreen? _filterScreen;
    private View? _filterPanel;
    private bool _filterReady;

    private async Task OpenFilterAsync()
    {
        var choices = await InScopeAsync(async services =>
        {
            var shots = services.GetRequiredService<IShotService>();
            return (Beans: await shots.GetBeansWithShotsAsync(), People: await shots.GetPeopleWithShotsAsync());
        });
        var working = _filters.Clone();
        var backdrop = new FrameLayout(this);
        backdrop.SetBackgroundColor(Color.Argb(170, 0, 0, 0));
        NativeStyle.Identify(backdrop, "FilterBackdrop");
        var screen = new NativeScreen(backdrop);
        _filterScreen = screen;
        _filterReady = false;
        Bind(screen, backdrop, CloseFilter);
        var panel = _style.Column();
        _filterPanel = panel;
        panel.Clickable = true;
        panel.Background = _style.Rounded(NativeStyle.ModalSurface, 24);
        panel.SetPadding(_style.Dp(24), _style.Dp(24), _style.Dp(24), _style.Dp(24));
        NativeStyle.Identify(panel, "FilterPanel");
        backdrop.AddView(panel, new FrameLayout.LayoutParams(-1, -1) { TopMargin = _style.Dp(24) });
        var header = new FrameLayout(this);
        var title = _style.Label("Filter Shots", 18, true, NativeStyle.ModalText);
        title.Typeface = _style.Semibold;
        title.Gravity = GravityFlags.Center;
        header.AddView(title, new FrameLayout.LayoutParams(-1, -2));
        var close = _style.Label("\ue14c", 24, color: NativeStyle.ModalText);
        NativeStyle.Identify(close, "FilterClose");
        close.Typeface = _style.Symbols;
        close.ContentDescription = "Close filters";
        close.Gravity = GravityFlags.End | GravityFlags.Top;
        close.Focusable = true;
        Bind(screen, close, CloseFilter);
        header.AddView(close, new FrameLayout.LayoutParams(_style.Dp(44), -2, GravityFlags.End)
        {
            TopMargin = -_style.Dp(4)
        });
        panel.AddView(header, new LinearLayout.LayoutParams(-1, -2));
        var scroll = new ScrollView(this) { FillViewport = false };
        NativeStyle.Identify(scroll, "FilterScroll");
        var content = _style.Column();
        content.SetPadding(_style.Dp(16), 0, _style.Dp(16), 0);
        scroll.AddView(content);
        panel.AddView(scroll, new LinearLayout.LayoutParams(-1, 0, 1) { TopMargin = _style.Dp(16) });
        var clear = _style.Button("Clear All", "FilterClear", _style.Primary);
        clear.Typeface = Typeface.Default;

        void AddContent(View child, int top = 0, int bottom = 0, int height = -2, bool centered = false)
        {
            content.AddView(child, new LinearLayout.LayoutParams(centered ? -2 : -1, height)
            {
                TopMargin = _style.Dp((content.ChildCount > 0 ? 16 : 0) + top),
                BottomMargin = _style.Dp(bottom),
                Gravity = centered ? GravityFlags.CenterHorizontal : GravityFlags.NoGravity
            });
        }

        void RefreshClear()
        {
            clear.Enabled = working.HasFilters;
            clear.SetTextColor(working.HasFilters ? _style.Primary : NativeStyle.ModalSecondary);
        }
        void Section(string name)
        {
            var label = _style.Label(name, 14, true, NativeStyle.ModalText);
            // Source native Controls.Label has no font family here; it uses the
            // platform font, unlike the popup template's Manrope title.
            label.Typeface = Typeface.DefaultBold;
            AddContent(label, top: 8, bottom: 4);
        }
        void Empty(string text)
        {
            var label = _style.Label(text, 14, color: NativeStyle.ModalSecondary);
            var italic = Typeface.Create(Typeface.Default, TypefaceStyle.Italic)
                ?? throw new InvalidOperationException("Android italic font is unavailable.");
            screen.Own(italic);
            label.Typeface = italic;
            AddContent(label);
        }
        Button Chip(string text, string id, List<int> values, int value, bool icon = false)
        {
            var button = _style.Button(text, id, NativeStyle.ModalText);
            button.Typeface = icon ? _style.Symbols : Typeface.Default;
            button.SetTextSize(Android.Util.ComplexUnitType.Sp, icon ? 24 : 14);
            button.SetMinHeight(0);
            button.SetMinimumHeight(0);
            button.SetMinWidth(_style.Dp(icon ? 56 : 60));
            button.SetMinimumWidth(_style.Dp(icon ? 56 : 60));
            button.SetPadding(_style.Dp(icon ? 8 : 12), 0, _style.Dp(icon ? 8 : 12), 0);
            button.LayoutParameters = new ViewGroup.LayoutParams(icon ? _style.Dp(56) : -2, _style.Dp(40));
            void Refresh()
            {
                var selected = values.Contains(value);
                button.Selected = selected;
                button.Background = _style.Rounded(selected ? _style.Primary : NativeStyle.ModalVariant,
                    20, selected ? _style.Primary : NativeStyle.ModalOutline);
                button.ContentDescription = $"{(icon ? $"Rating {value}" : text)}, {(selected ? "selected" : "not selected")}";
            }
            Refresh();
            Bind(screen, button, () =>
            {
                if (!values.Remove(value))
                    values.Add(value);
                Refresh();
                RefreshClear();
            });
            return button;
        }
        Section("Beans");
        var beans = new ChipLayout(this, _style.Dp(8));
        foreach (var bean in choices.Beans)
            beans.AddView(Chip(bean.Name, $"FilterBean_{bean.Id}", working.BeanIds, bean.Id));
        if (choices.Beans.Count == 0) Empty("No beans with shots"); else AddContent(beans, bottom: 8);
        Section("Made For");
        var people = new ChipLayout(this, _style.Dp(8));
        foreach (var person in choices.People)
            people.AddView(Chip(person.Name, $"FilterPerson_{person.Id}", working.MadeForIds, person.Id));
        if (choices.People.Count == 0) Empty("No people with shots"); else AddContent(people, bottom: 8);
        Section("Rating");
        var ratings = new ChipLayout(this, _style.Dp(8));
        string[] glyphs = ["\ue814", "\ue811", "\ue812", "\ue0ed", "\ue815"];
        for (var rating = 0; rating < glyphs.Length; rating++)
            ratings.AddView(Chip(glyphs[rating], $"FilterRating_{rating}", working.Ratings, rating, icon: true));
        AddContent(ratings, bottom: 8);
        AddContent(new View(this), height: _style.Dp(8));
        AddContent(clear, height: _style.Dp(44), centered: true);
        Bind(screen, clear, () =>
        {
            RunOperation(async () =>
            {
                await CloseFilterAsync();
                _filters.Clear();
                await ShowHistoryAsync();
            });
        });
        RefreshClear();
        var apply = _style.Button("Apply", "FilterApply", NativeStyle.ModalText);
        apply.Typeface = Typeface.Default;
        apply.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
        apply.Background = _style.Rounded(_style.Primary, 20);
        Bind(screen, apply, () =>
        {
            RunOperation(async () =>
            {
                await CloseFilterAsync();
                _filters = working;
                await ShowHistoryAsync();
            });
        });
        panel.AddView(apply, new LinearLayout.LayoutParams(-1, _style.Dp(54))
        {
            TopMargin = _style.Dp(16)
        });
        _style.FixTheme(backdrop);
        _host.Enabled = false;
        _host.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        // The source popup is window-level, not constrained to Activity content
        // insets. The 24dp panel margin must not be added below another status inset.
        var windowHost = Window?.DecorView as ViewGroup
            ?? throw new InvalidOperationException("The Android window overlay host is unavailable.");
        panel.TranslationY = windowHost.Height;
        panel.Enabled = false;
        windowHost.AddView(backdrop, new FrameLayout.LayoutParams(-1, -1));
        try
        {
            await NativeModalMotion.RunAsync(panel, backdrop, entering: true, _lifetime.Token);
            panel.Enabled = true;
            _filterReady = true;
        }
        catch
        {
            DisposeFilter();
            throw;
        }
    }

    private void CloseFilter()
    {
        if (!_filterReady || _filterScreen is null)
            return;
        RunOperation(CloseFilterAsync);
    }

    private async Task CloseFilterAsync()
    {
        if (!_filterReady || _filterScreen is null || _filterPanel is null)
            return;
        _filterReady = false;
        _filterPanel.Enabled = false;
        try
        {
            await NativeModalMotion.RunAsync(_filterPanel, _filterScreen.Root, entering: false, _lifetime.Token);
        }
        finally
        {
            DisposeFilter();
        }
    }

    private void DisposeFilter()
    {
        if (_filterScreen is null)
            return;
        var screen = _filterScreen;
        _filterScreen = null;
        _filterPanel = null;
        _filterReady = false;
        (screen.Root.Parent as ViewGroup)?.RemoveView(screen.Root);
        screen.Dispose();
        _host.Enabled = true;
        _host.ImportantForAccessibility = ImportantForAccessibility.Auto;
    }
}
