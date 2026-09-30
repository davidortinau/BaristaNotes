using System.Globalization;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Services.Workflows;

public sealed class RangeEditorDraft
{
    private readonly string _originalMinimumText;
    private readonly string _originalMaximumText;
    private readonly DrinkValueRange _originalRange;

    public RangeEditorDraft(
        DrinkValueMetric metric,
        BrewMethod method,
        DrinkValueRangeSettingsSnapshot settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Metric = metric;
        Method = method;
        Definition = BrewMethodValueRangeCatalog.GetDefinition(method, metric);
        EditorUnit = DrinkValueRangeFormatting.GetEditorUnit(metric, method);

        var custom = settings.Overrides.LastOrDefault(
            item => item.Metric == metric && item.Method == method);
        HasOverride = custom is not null;
        _originalRange = custom is null
            ? Definition.AutoRange
            : new DrinkValueRange(custom.Minimum, custom.Maximum);
        MinimumText = _originalMinimumText =
            DrinkValueRangeFormatting.FormatEditorValue(_originalRange.Minimum, EditorUnit);
        MaximumText = _originalMaximumText =
            DrinkValueRangeFormatting.FormatEditorValue(_originalRange.Maximum, EditorUnit);
    }

    public DrinkValueMetric Metric { get; }
    public BrewMethod Method { get; }
    public DrinkValueRangeDefinition Definition { get; }
    public RangeEditorUnit EditorUnit { get; }
    public bool HasOverride { get; }
    public string MinimumText { get; set; }
    public string MaximumText { get; set; }
    public bool IsDirty =>
        MinimumText != _originalMinimumText || MaximumText != _originalMaximumText;

    public string? ValidationError
    {
        get
        {
            if (string.IsNullOrWhiteSpace(MinimumText) || string.IsNullOrWhiteSpace(MaximumText))
            {
                return null;
            }

            TryGetRange(out _, out var error);
            return error;
        }
    }

    public void UseRecommended()
    {
        MinimumText = DrinkValueRangeFormatting.FormatEditorValue(
            Definition.AutoRange.Minimum, EditorUnit);
        MaximumText = DrinkValueRangeFormatting.FormatEditorValue(
            Definition.AutoRange.Maximum, EditorUnit);
    }

    public bool TryGetRange(out DrinkValueRange range, out string? error)
    {
        range = Definition.AutoRange;
        if (!TryParseDisplayValue(MinimumText, out var displayMinimum)
            || !TryParseDisplayValue(MaximumText, out var displayMaximum))
        {
            error = $"Enter valid values in {EditorUnit.Label}.";
            return false;
        }

        // Rounded display text must not change an untouched canonical value.
        var minimum = MinimumText == _originalMinimumText
            ? _originalRange.Minimum
            : displayMinimum * EditorUnit.Scale;
        var maximum = MaximumText == _originalMaximumText
            ? _originalRange.Maximum
            : displayMaximum * EditorUnit.Scale;

        if (minimum >= maximum)
        {
            error = "Minimum must be less than maximum.";
            return false;
        }

        if (!Definition.HardRange.Contains(minimum) || !Definition.HardRange.Contains(maximum))
        {
            error = $"Use values from {DrinkValueRangeFormatting.FormatRange(Metric, Definition.HardRange)}.";
            return false;
        }

        if (Metric is DrinkValueMetric.DoseIn or DrinkValueMetric.Yield
            && (decimal.Round(minimum, 1) != minimum || decimal.Round(maximum, 1) != maximum))
        {
            error = "Use no more than one decimal place.";
            return false;
        }

        if (Metric is DrinkValueMetric.GrindMicrons or DrinkValueMetric.Time
            && (decimal.Truncate(minimum) != minimum || decimal.Truncate(maximum) != maximum))
        {
            error = "Use values that convert to whole units.";
            return false;
        }

        range = new(minimum, maximum);
        error = null;
        return true;
    }

    private static bool TryParseDisplayValue(string text, out decimal value) =>
        decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value);
}
