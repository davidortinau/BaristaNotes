using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private void OpenTimePicker()
    {
        var draft = Draft;
        ShowNumericPicker("Time", "s",
            Ranges.Resolve(DrinkValueMetric.Time, draft.BrewMethod),
            draft.ActualTime ?? draft.ExpectedTime,
            value =>
            {
                draft.ActualTime = value;
                draft.ExpectedTime = value;
            },
            value => $"{DrinkDisplay.TimeValue(value)}{DrinkDisplay.TimeUnit(value)}");
    }

    private void OpenWaterTemperaturePicker()
    {
        var draft = Draft;
        var fahrenheit = draft.TempUnit == TemperatureUnit.Fahrenheit;
        var range = fahrenheit ? new DrinkValueRange(150, 212) : new DrinkValueRange(65, 100);
        var defaultValue = fahrenheit ? 200 : 93;
        var current = draft.WaterTempC.HasValue
            ? fahrenheit ? (decimal)DrinkDisplay.CelsiusToFahrenheit(draft.WaterTempC.Value) : draft.WaterTempC.Value
            : defaultValue;
        var definition = new EffectiveDrinkValueRange(range, range, defaultValue,
            fahrenheit ? 1m : .5m, fahrenheit ? "F" : "C", ValueRangeSource.Auto);
        ShowNumericPicker("Water Temp", fahrenheit ? "°F" : "°C", definition, current,
            value => draft.WaterTempC = fahrenheit ? DrinkDisplay.FahrenheitToCelsius((double)value) : value,
            originalFollowsSelection: true);
    }

    private void ShowNumericPicker(
        string title, string unit, EffectiveDrinkValueRange definition, decimal original,
        Action<decimal> commit, Func<decimal, string>? formatter = null,
        bool originalFollowsSelection = false)
    {
        var state = new NumericPickerState(definition, original);
        ClearTransient();
        _page = "picker";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Surface);
        var screen = new NativeScreen(column);
        column.AddView(PickerHeader(screen, title, () =>
        {
            commit(state.DoneValue);
            ShowDrink();
        }));
        var (scopeBackground, scopeAction, description, scopeLabel) = CreateRangeScopeBar();
        column.AddView(scopeBackground);
        var adapter = new OptionAdapter(_style, numeric: true);
        var list = CreateList(screen, adapter, "NumericValues");
        column.AddView(list, _style.Fill(weight: 1));

        void Refresh()
        {
            description.Text = state.RangeDescription;
            description.SetTextColor(state.IsOutsidePreferredRange ? NativeStyle.Warning : _style.Secondary);
            scopeLabel.Text = state.RangeToggleText;
            scopeAction.ContentDescription = $"{state.RangeDescription} " +
                (state.ShowsFullRange ? "Show preferred range." : "Show full allowed range.");
            adapter.SetItems(state.Values.Select(value => new NativeChoice(
                NumericChoiceIdentity.For(value),
                formatter?.Invoke(value) ?? $"{value.ToString(state.Step < 1 ? "0.#" : "0")}{unit}",
                false, Choice(() =>
                {
                    state.Select(value);
                    // WaterTempPicker passes its current PickerValue as original
                    // on each source render; Time keeps the pre-open original.
                    if (originalFollowsSelection)
                        state = new NumericPickerState(definition, state.StagedValue,
                            hasChanged: state.HasChanged, showsFullRange: state.ShowsFullRange);
                    Refresh();
                    CenterList(list, state.SelectedIndex);
                }))).ToArray());
            adapter.SetSelection(NumericChoiceIdentity.For(state.DisplayedValue));
        }
        Bind(screen, scopeAction, () =>
        {
            state.ToggleRange();
            Refresh();
            CenterList(list, state.SelectedIndex);
        });
        Refresh();
        _transient = screen;
        Present(column);
        CenterList(list, state.SelectedIndex);
    }

    private (FrameLayout Background, Button Action, TextView Description, TextView Toggle) CreateRangeScopeBar()
    {
        var background = new FrameLayout(this);
        background.SetPadding(_style.Dp(16), _style.Dp(10), _style.Dp(16), _style.Dp(10));
        background.SetBackgroundColor(Color.Argb(12, _style.Text.R, _style.Text.G, _style.Text.B));
        background.ImportantForAccessibility = ImportantForAccessibility.No;
        NativeStyle.Identify(background, "RangeScopeStrip");
        var row = _style.Row();
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetMinimumHeight(_style.Dp(44));
        row.ImportantForAccessibility = ImportantForAccessibility.No;
        var description = _style.Label("", 12, color: _style.Secondary);
        var toggle = _style.Label("", 11, true, _style.Primary);
        toggle.LetterSpacing = .0624f;
        description.ImportantForAccessibility = ImportantForAccessibility.No;
        toggle.ImportantForAccessibility = ImportantForAccessibility.No;
        row.AddView(description, new LinearLayout.LayoutParams(0, -2, 1));
        row.AddView(toggle);
        background.AddView(row, new FrameLayout.LayoutParams(-1, -2));
        var action = _style.Button("", "RangeScopeToggle");
        action.SetPadding(0, 0, 0, 0);
        // FrameLayout measures the SP labels first, then fills the content box
        // with this action. The surrounding padding never owns a click handler.
        background.AddView(action, new FrameLayout.LayoutParams(-1, -1));
        return (background, action, description, toggle);
    }
}
