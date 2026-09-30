using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private void AddSettingsTemperatureUnits(NativeScreen screen, LinearLayout body)
    {
        var preferences = _app.Services.GetRequiredService<IPreferencesService>();
        AddSettingsSection(body, "UNITS");
        var row = _style.Row();
        row.SetBackgroundColor(_style.Outline);
        var tiles = new List<(TemperatureUnit Unit, LinearLayout Tile, TextView Caption, TextView Value)>();
        void Refresh()
        {
            var selectedUnit = preferences.GetTemperatureUnit();
            foreach (var (unit, tile, caption, value) in tiles)
            {
                var selected = unit == selectedUnit;
                var foreground = selected ? _style.Surface : _style.Text;
                tile.Selected = selected;
                tile.SetBackgroundColor(selected ? _style.Text : _style.Surface);
                caption.SetTextColor(Color.Argb(179, foreground.R, foreground.G, foreground.B));
                value.SetTextColor(foreground);
                tile.ContentDescription = $"{caption.Text}, {value.Text}" + (selected ? ", selected" : "");
            }
        }
        foreach (var unit in new[] { TemperatureUnit.Fahrenheit, TemperatureUnit.Celsius })
        {
            var tile = _style.Column();
            tile.SetMinimumHeight(_style.Dp(96));
            tile.SetPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(14));
            tile.Focusable = true;
            var caption = _style.Label(unit == TemperatureUnit.Fahrenheit ? "FAHRENHEIT" : "CELSIUS", 10, true);
            caption.LetterSpacing = 2 * .0624f;
            caption.ImportantForAccessibility = ImportantForAccessibility.No;
            tile.AddView(caption);
            var value = _style.Label(unit == TemperatureUnit.Fahrenheit ? "°F" : "°C", 28, true);
            value.Gravity = GravityFlags.Bottom;
            value.ImportantForAccessibility = ImportantForAccessibility.No;
            tile.AddView(value, _style.Fill(weight: 1));
            NativeStyle.Identify(tile, $"TemperatureUnit_{unit}");
            Bind(screen, tile, () =>
            {
                preferences.SetTemperatureUnit(unit);
                Refresh();
            });
            tiles.Add((unit, tile, caption, value));
            row.AddView(tile, new LinearLayout.LayoutParams(0, -1, 1)
            {
                RightMargin = unit == TemperatureUnit.Fahrenheit ? _style.Dp(1) : 0
            });
        }
        body.AddView(row, new LinearLayout.LayoutParams(-1, -2));
        var divider = new View(this);
        divider.SetBackgroundColor(_style.Outline);
        body.AddView(divider, new LinearLayout.LayoutParams(-1, _style.Dp(1)));
        Refresh();
    }
}
