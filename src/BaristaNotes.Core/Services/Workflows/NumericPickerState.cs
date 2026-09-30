using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Services.Workflows;

public sealed class NumericPickerState
{
    private readonly EffectiveDrinkValueRange _definition;

    public NumericPickerState(
        EffectiveDrinkValueRange definition,
        decimal originalValue,
        decimal? stagedValue = null,
        bool hasChanged = false,
        bool showsFullRange = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(definition.Step);
        if (definition.Range.Minimum > definition.Range.Maximum
            || definition.HardRange.Minimum > definition.HardRange.Maximum)
        {
            throw new ArgumentException("Range minimum cannot exceed its maximum.", nameof(definition));
        }

        _definition = definition;
        OriginalValue = originalValue;
        StagedValue = stagedValue ?? originalValue;
        HasChanged = hasChanged;
        ShowsFullRange = showsFullRange;
        RebuildValues();
    }

    public decimal OriginalValue { get; }
    public decimal StagedValue { get; private set; }
    public bool HasChanged { get; private set; }
    public bool ShowsFullRange { get; private set; }
    public DrinkValueRange ActiveRange => ShowsFullRange ? _definition.HardRange : _definition.Range;
    public decimal Step => _definition.Step;
    public IReadOnlyList<decimal> Values { get; private set; } = Array.Empty<decimal>();
    public int SelectedIndex { get; private set; }
    public decimal DisplayedValue => Values[SelectedIndex];
    public decimal DoneValue => HasChanged ? DisplayedValue : OriginalValue;
    public bool IsOutsidePreferredRange => !_definition.Range.Contains(OriginalValue);
    public string RangeDescription => DrinkDisplay.RangeDescription(
        _definition, OriginalValue, ShowsFullRange);
    public string RangeToggleText => ShowsFullRange ? "PREFERRED" : "FULL RANGE";

    public void ToggleRange()
    {
        ShowsFullRange = !ShowsFullRange;
        RebuildValues();
    }

    public void Select(decimal value)
    {
        if (!Values.Contains(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        StagedValue = value;
        HasChanged = true;
        RebuildValues();
    }

    private void RebuildValues()
    {
        var range = ActiveRange;
        var values = new List<decimal>();
        for (var value = range.Minimum; value <= range.Maximum; value += Step)
        {
            values.Add(value);
        }
        if (values[^1] != range.Maximum)
        {
            values.Add(range.Maximum);
        }

        if (range.Contains(StagedValue) && !values.Contains(StagedValue))
        {
            values.Add(StagedValue);
            values.Sort();
        }

        var clamped = range.Clamp(StagedValue);
        SelectedIndex = values
            .Select((value, index) => (index, delta: Math.Abs(value - clamped)))
            .OrderBy(candidate => candidate.delta)
            .First()
            .index;
        Values = values.AsReadOnly();
    }
}
