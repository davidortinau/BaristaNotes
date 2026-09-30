using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Services.Workflows;

public sealed class GrindPickerState
{
    private readonly EffectiveDrinkValueRange _definition;

    public GrindPickerState(
        EffectiveDrinkValueRange definition,
        int? originalValue,
        int? stagedValue = null,
        bool hasChanged = false,
        bool showsFullRange = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Step < 1 || decimal.Truncate(definition.Step) != definition.Step
            || definition.Range.Minimum > definition.Range.Maximum
            || definition.HardRange.Minimum > definition.HardRange.Maximum)
        {
            throw new ArgumentException("Grind ranges require ordered bounds and a positive whole-number step.",
                nameof(definition));
        }

        _definition = definition;
        OriginalValue = originalValue;
        StagedValue = stagedValue ?? originalValue ?? (int)definition.Default;
        HasChanged = hasChanged;
        ShowsFullRange = showsFullRange;
        RebuildValues();
    }

    public int? OriginalValue { get; }
    public int StagedValue { get; private set; }
    public bool HasChanged { get; private set; }
    public bool ShowsFullRange { get; private set; }
    public IReadOnlyList<int> Values { get; private set; } = Array.Empty<int>();
    public int SelectedIndex { get; private set; }
    public int DoneValue => !HasChanged && OriginalValue.HasValue ? OriginalValue.Value : StagedValue;
    public bool IsOutsidePreferredRange =>
        !_definition.Range.Contains(OriginalValue ?? _definition.Default);
    public string RangeDescription => DrinkDisplay.RangeDescription(
        _definition, OriginalValue ?? _definition.Default, ShowsFullRange);
    public string RangeToggleText => ShowsFullRange ? "PREFERRED" : "FULL RANGE";

    public bool IsPreferred(int value) => _definition.Range.Contains(value);

    public void ToggleRange()
    {
        ShowsFullRange = !ShowsFullRange;
        RebuildValues();
    }

    public void Select(int value)
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
        var minimum = (int)_definition.Range.Minimum;
        var maximum = (int)_definition.Range.Maximum;
        var step = (int)_definition.Step;
        var domainMinimum = (int)_definition.HardRange.Minimum;
        var domainMaximum = (int)_definition.HardRange.Maximum;
        const int outsideStep = 50;
        var values = new List<int>();

        if (ShowsFullRange)
        {
            for (var value = domainMinimum; value < minimum; value += outsideStep)
                values.Add(value);
        }
        for (var value = minimum; value <= maximum; value += step)
            values.Add(value);
        if (values.Count == 0 || values[^1] != maximum)
            values.Add(maximum);
        if (ShowsFullRange)
        {
            for (var value = maximum + outsideStep; value <= domainMaximum; value += outsideStep)
                values.Add(value);
            values.Add(domainMinimum);
            values.Add(domainMaximum);
        }

        // Unlike mass/time, the source keeps the current micron value even outside the domain.
        values.Add(StagedValue);
        var ordered = values.Distinct().OrderBy(value => value).ToList();
        SelectedIndex = ordered.IndexOf(StagedValue);
        Values = ordered.AsReadOnly();
    }
}
