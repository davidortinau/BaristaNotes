using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private void ShowSettings()
    {
        ClearTransient();
        _page = "settings";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Outline);
        column.SetPadding(_style.Dp(1), _style.Dp(1), _style.Dp(1), _style.Dp(1));
        var screen = new NativeScreen(column);
        var header = (ViewGroup)BuildHeader("SETTINGS", _app.ThemeService.Label);
        var modeTitle = (TextView)header.GetChildAt(1)!;
        NativeStyle.Identify(modeTitle, "SettingsThemeTitle");
        column.AddView(header, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var body = _style.Column();
        body.SetBackgroundColor(_style.Outline);
        AddSettingsSection(body, "APPEARANCE");
        var appearance = _style.Row();
        appearance.SetBackgroundColor(_style.Outline);
        var tiles = new List<(ThemeMode Mode, LinearLayout Tile, TextView Caption, TextView Value)>();
        foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark, ThemeMode.System })
        {
            var tile = _style.Column();
            tile.SetMinimumHeight(_style.Dp(96));
            tile.SetPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(14));
            tile.Focusable = true;
            var caption = _style.Label(mode == ThemeMode.System ? "AUTO" : mode.ToString().ToUpperInvariant(), 10, true);
            caption.LetterSpacing = 2 * .0624f;
            caption.ImportantForAccessibility = ImportantForAccessibility.No;
            tile.AddView(caption);
            var value = _style.Label("", 28, true);
            value.Gravity = GravityFlags.Bottom;
            value.ImportantForAccessibility = ImportantForAccessibility.No;
            tile.AddView(value, _style.Fill(weight: 1));
            NativeStyle.Identify(tile, "ThemeMode_" + mode);
            Bind(screen, tile, () => _app.ThemeService.Select(mode));
            tiles.Add((mode, tile, caption, value));
            appearance.AddView(tile, new LinearLayout.LayoutParams(0, -1, 1)
            {
                RightMargin = mode == ThemeMode.System ? 0 : _style.Dp(1)
            });
        }
        AddSettingsItem(body, appearance);
        void Refresh()
        {
            modeTitle.Text = _app.ThemeService.Label;
            foreach (var (mode, tile, caption, value) in tiles)
            {
                var selected = mode == _app.ThemeService.CurrentMode;
                var foreground = selected ? _style.Surface : _style.Text;
                tile.Selected = selected;
                tile.SetBackgroundColor(selected ? _style.Text : _style.Surface);
                caption.SetTextColor(Color.Argb(179, foreground.R, foreground.G, foreground.B));
                value.SetTextColor(foreground);
                value.Text = selected ? "●" : "○";
                tile.ContentDescription = $"{caption.Text} theme" + (selected ? ", selected" : "");
            }
        }
        _refreshSettingsAppearance = Refresh;
        screen.OnDispose(() =>
        {
            if (_refreshSettingsAppearance == Refresh) _refreshSettingsAppearance = null;
        });
        Refresh();
        AddSettingsSection(body, "MANAGE");
        void Manage(string caption, string subtitle, string id, Action action)
        {
            var tile = new ManagementTile(_style);
            tile.Caption.Text = caption;
            tile.Value.Text = subtitle;
            tile.ContentDescription = $"{caption}: {subtitle}";
            tile.Focusable = tile.FocusableInTouchMode = true;
            NativeStyle.Identify(tile, id);
            Bind(screen, tile, action);
            AddSettingsItem(body, tile);
        }
        Manage("EQUIPMENT", "Machines, grinders, accessories", "SettingsEquipment", () => RunOperation(ShowEquipmentAsync));
        Manage("BEANS", "Coffee beans and roasters", "SettingsBeans", () => ObserveBeanTask(ShowBeansAsync));
        Manage("PROFILES", "Coffee lovers", "SettingsProfiles", () => RunOperation(ShowProfilesAsync));
        AddSettingsTemperatureUnits(screen, body);
        AddSettingsRanges(screen, body);
        AddSettingsSection(body, "ABOUT");
        var about = _style.Column();
        about.SetBackgroundColor(_style.Surface);
        about.SetMinimumHeight(_style.Dp(96));
        about.SetPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(18));
        var name = _style.Label("BARISTANOTES", 10, true, _style.Secondary);
        name.LetterSpacing = 2 * .0624f;
        about.AddView(name);
        about.AddView(_style.Label("Version 1.0", 18, true));
        about.AddView(_style.Label("Track your espresso journey", 13, color: _style.Secondary));
        NativeStyle.Identify(about, "SettingsAbout");
        AddSettingsItem(body, about);
        var fill = new View(this);
        fill.SetBackgroundColor(_style.Surface);
        fill.SetMinimumHeight(_style.Dp(16));
        body.AddView(fill, new LinearLayout.LayoutParams(-1, _style.Dp(16), 1));
        var scroll = new ScrollView(this) { FillViewport = true };
        NativeStyle.Identify(scroll, "SettingsScroll");
        scroll.SetBackgroundColor(_style.Surface);
        scroll.AddView(body);
        column.AddView(scroll, _style.Fill(weight: 1));
        column.AddView(BuildNavigation(screen,
            ("\uefef", "New Drink", "NavDrink", () => { _editingShotId = null; ShowDrink(); }),
            ("\uf009", "Activity", "NavActivity", () => RunOperation(ShowHistoryAsync)),
            ("\ue029", "Voice", "NavVoice", ToggleVoiceFromPage)),
            new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(1) });
        _transient = screen;
        Present(column, edgeToEdge: true);
        NotifyPerformanceSettingsReady();
    }

    partial void NotifyPerformanceSettingsReady();

    private void AddSettingsSection(LinearLayout body, string text)
    {
        var label = _style.Label(text, 10, true, _style.Secondary);
        label.SetBackgroundColor(_style.Surface);
        label.LetterSpacing = 2 * .0624f;
        label.SetPadding(_style.Dp(16), _style.Dp(24), _style.Dp(16), _style.Dp(10));
        AddSettingsItem(body, label);
    }

    private void AddSettingsItem(LinearLayout body, View item)
    {
        body.AddView(item, new LinearLayout.LayoutParams(-1, -2));
        var divider = new View(this);
        divider.SetBackgroundColor(_style.Outline);
        body.AddView(divider, new LinearLayout.LayoutParams(-1, _style.Dp(1)));
    }
}
