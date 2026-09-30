using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.Tests.Unit;

public sealed class NumericPickerStateTests
{
    private static EffectiveDrinkValueRange TimeRange() =>
        new(new(63, 597), new(60, 600), 180, 5, "s", ValueRangeSource.Custom);

    [Fact]
    public void Values_IncludeTheMaximumAndCurrentNonStepValueInOrder()
    {
        var picker = new NumericPickerState(TimeRange(), 180);

        Assert.Equal(63m, picker.Values[0]);
        Assert.Equal(597m, picker.Values[^1]);
        Assert.Contains(593m, picker.Values);
        Assert.Contains(180m, picker.Values);
        Assert.Equal(180m, picker.DisplayedValue);
        Assert.Equal(180m, picker.Values[picker.SelectedIndex]);
        Assert.Equal(picker.Values.OrderBy(value => value), picker.Values);
        Assert.Equal(picker.Values.Count, picker.Values.Distinct().Count());
        Assert.False(picker.HasChanged);
    }

    [Fact]
    public void Selection_ReplacesThePreviousNonStepValueAsSourceRerenderDoes()
    {
        var picker = new NumericPickerState(TimeRange(), 180);

        picker.Select(188);

        Assert.Equal(188m, picker.StagedValue);
        Assert.Equal(188m, picker.DisplayedValue);
        Assert.Equal(188m, picker.DoneValue);
        Assert.DoesNotContain(180m, picker.Values);
        Assert.True(picker.HasChanged);
    }

    [Theory]
    [InlineData(60, 63)]
    [InlineData(600, 597)]
    [InlineData(720, 597)]
    public void UnchangedDone_PreservesOriginalOutsideThePreferredRange(
        decimal original, decimal displayed)
    {
        var picker = new NumericPickerState(TimeRange(), original);

        Assert.Equal(displayed, picker.DisplayedValue);
        Assert.Equal(original, picker.DoneValue);
        Assert.True(picker.IsOutsidePreferredRange);
        Assert.Equal("Current value is outside your preferred range.", picker.RangeDescription);

        picker.ToggleRange();

        Assert.Equal(original, picker.DoneValue);
        Assert.False(picker.HasChanged);
        Assert.Equal("PREFERRED", picker.RangeToggleText);
    }

    [Fact]
    public void ReturningToPreferredRange_ClampsOnlyAChangedValue()
    {
        var picker = new NumericPickerState(TimeRange(), 180);
        picker.ToggleRange();
        picker.Select(600);
        picker.ToggleRange();

        Assert.Equal(600m, picker.StagedValue);
        Assert.Equal(597m, picker.DisplayedValue);
        Assert.Equal(597m, picker.DoneValue);
        Assert.Equal("FULL RANGE", picker.RangeToggleText);
    }

    [Fact]
    public void Rehydration_PreservesTheStagedSelectionAndFlags()
    {
        var picker = new NumericPickerState(TimeRange(), 180, 500, true, true);

        Assert.True(picker.ShowsFullRange);
        Assert.True(picker.HasChanged);
        Assert.Equal(500m, picker.DoneValue);
        Assert.Equal(60m, picker.Values[0]);
        Assert.Equal(600m, picker.Values[^1]);
    }

    [Fact]
    public void SelectingAnUnavailableValue_DoesNotChangeTheDraft()
    {
        var picker = new NumericPickerState(TimeRange(), 180);

        Assert.Throws<ArgumentOutOfRangeException>(() => picker.Select(181));
        Assert.False(picker.HasChanged);
        Assert.Equal(180m, picker.StagedValue);
    }

    [Fact]
    public void FractionalTemperatureSteps_IncludeExactCurrentValue()
    {
        var definition = new EffectiveDrinkValueRange(
            new(65, 100), new(65, 100), 93, 0.5m, "C", ValueRangeSource.Auto);
        var picker = new NumericPickerState(definition, 93.33m);

        Assert.Equal(72, picker.Values.Count);
        Assert.Contains(93.33m, picker.Values);
        Assert.Contains(93.5m, picker.Values);
        Assert.Equal(93.33m, picker.DoneValue);

        picker.Select(93.5m);

        Assert.Equal(71, picker.Values.Count);
        Assert.Equal(93.5m, picker.DoneValue);
    }

    [Fact]
    public void ColdTime_UsesCatalogStepAndIncludesExactExistingSeconds()
    {
        var definition = new EffectiveDrinkValueRange(
            new(14400, 86400), new(3600, 86400), 43200, 1800, "s", ValueRangeSource.Auto);
        var picker = new NumericPickerState(definition, 14409);

        Assert.Equal(42, picker.Values.Count);
        Assert.Equal(14409m, picker.DisplayedValue);
        Assert.Equal(14409m, picker.DoneValue);
        picker.Select(16200);
        Assert.Equal(16200m, picker.DoneValue);
    }

    [Fact]
    public void EqualBounds_HaveOneSelectableValue()
    {
        var definition = TimeRange() with { Range = new(100, 100) };
        var picker = new NumericPickerState(definition, 100);

        Assert.Equal(100m, Assert.Single(picker.Values));
        Assert.Equal(0, picker.SelectedIndex);
    }

    [Fact]
    public void InvalidRangeOrStep_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NumericPickerState(TimeRange() with { Step = 0 }, 180));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new NumericPickerState(TimeRange() with { Step = -1 }, 180));
        Assert.Throws<ArgumentException>(() =>
            new NumericPickerState(TimeRange() with { Range = new(200, 100) }, 180));
    }
}
