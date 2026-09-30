using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Services.Workflows;

public sealed class MassPickerState
{
    private static readonly IReadOnlyList<int> Tenths = Array.AsReadOnly(Enumerable.Range(0, 10).ToArray());
    private readonly EffectiveDrinkValueRange _definition;
    private readonly IReadOnlyList<int> _preferredWholes;
    private readonly IReadOnlyList<int> _fullWholes;

    public MassPickerState(
        EffectiveDrinkValueRange definition,
        decimal originalValue,
        decimal? stagedValue = null,
        bool hasChanged = false,
        bool showsFullRange = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        _definition = definition;
        OriginalValue = originalValue;
        StagedValue = stagedValue ?? originalValue;
        HasChanged = hasChanged;
        ShowsFullRange = showsFullRange;
        _preferredWholes = CreateWholeValues(definition.Range);
        _fullWholes = CreateWholeValues(definition.HardRange);
    }

    public decimal OriginalValue { get; }
    public decimal StagedValue { get; private set; }
    public bool HasChanged { get; private set; }
    public bool ShowsFullRange { get; private set; }
    public DrinkValueRange ActiveRange => ShowsFullRange ? _definition.HardRange : _definition.Range;
    public IReadOnlyList<int> WholeValues => ShowsFullRange ? _fullWholes : _preferredWholes;
    public IReadOnlyList<int> TenthValues => Tenths;
    public decimal DisplayedValue => ClampMass(StagedValue, ActiveRange);
    public int SelectedWhole => (int)decimal.Truncate(DisplayedValue);
    public int SelectedTenth => (int)((DisplayedValue - decimal.Truncate(DisplayedValue)) * 10m);
    public bool IsOutsidePreferredRange => !_definition.Range.Contains(OriginalValue);
    public decimal DoneValue => HasChanged ? DisplayedValue : OriginalValue;
    public string RangeDescription => DrinkDisplay.RangeDescription(
        _definition, OriginalValue, ShowsFullRange);
    public string RangeToggleText => ShowsFullRange ? "PREFERRED" : "FULL RANGE";

    public void ToggleRange() => ShowsFullRange = !ShowsFullRange;

    public void SelectWhole(int value)
    {
        if (value < WholeValues[0] || value > WholeValues[^1])
            throw new ArgumentOutOfRangeException(nameof(value));

        var displayed = DisplayedValue;
        StagedValue = ClampMass(value + displayed - decimal.Truncate(displayed), ActiveRange);
        HasChanged = true;
    }

    public void SelectTenth(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 9);
        StagedValue = ClampMass(decimal.Truncate(DisplayedValue) + value / 10m, ActiveRange);
        HasChanged = true;
    }

    public static decimal ClampMass(decimal value, DrinkValueRange range) =>
        Math.Round(range.Clamp(value), 1, MidpointRounding.AwayFromZero);

    private static IReadOnlyList<int> CreateWholeValues(DrinkValueRange range)
    {
        if (range.Minimum > range.Maximum)
            throw new ArgumentException("Range minimum cannot exceed its maximum.", nameof(range));

        var first = (int)Math.Floor(range.Minimum);
        var last = (int)Math.Floor(range.Maximum);
        return Array.AsReadOnly(Enumerable.Range(first, last - first + 1).ToArray());
    }
}
