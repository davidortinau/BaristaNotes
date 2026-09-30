using System.Globalization;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.Grind;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.Tests.Unit;

public sealed class GrindPickerStateTests
{
    private static EffectiveDrinkValueRange Range() =>
        new(new(175, 380), new(40, 1500), 270, 5, "um", ValueRangeSource.Auto);

    [Fact]
    public void PreferredValues_PreserveCurrentAndBothBounds()
    {
        var picker = new GrindPickerState(Range(), 273);

        Assert.Equal(175, picker.Values[0]);
        Assert.Equal(380, picker.Values[^1]);
        Assert.Contains(270, picker.Values);
        Assert.Contains(273, picker.Values);
        Assert.Equal(273, picker.Values[picker.SelectedIndex]);
        Assert.Equal(273, picker.DoneValue);
        Assert.False(picker.HasChanged);
    }

    [Fact]
    public void FullValues_UseSeparateOutsideStepsAndIncludeDomainEndpoints()
    {
        var picker = new GrindPickerState(Range(), 270);

        picker.ToggleRange();

        Assert.Equal(new[] { 40, 90, 140 }, picker.Values.Where(value => value < 175));
        Assert.Equal(new[] { 430, 480, 530 }, picker.Values.Where(value => value > 380).Take(3));
        Assert.Equal(1500, picker.Values[^1]);
        Assert.Contains(1480, picker.Values);
        Assert.Equal(270, picker.DoneValue);
        Assert.False(picker.HasChanged);
        Assert.Equal("PREFERRED", picker.RangeToggleText);
    }

    [Fact]
    public void PreferredMaximum_IsIncludedWhenItDoesNotAlignWithStep()
    {
        var definition = Range() with { Range = new(201, 303) };
        var picker = new GrindPickerState(definition, null);

        Assert.Equal(201, picker.Values[0]);
        Assert.Contains(301, picker.Values);
        Assert.Equal(303, picker.Values[^1]);
        Assert.Equal(270, picker.DoneValue);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(1600)]
    public void ExistingValueOutsideDomain_IsNotClamped(int original)
    {
        var picker = new GrindPickerState(Range(), original);

        Assert.Contains(original, picker.Values);
        Assert.Equal(original, picker.DoneValue);
        Assert.True(picker.IsOutsidePreferredRange);
        Assert.False(picker.IsPreferred(original));
        picker.ToggleRange();
        Assert.Contains(original, picker.Values);
        Assert.Equal(original, picker.DoneValue);
        picker.ToggleRange();
        Assert.Contains(original, picker.Values);
    }

    [Fact]
    public void FullRangeSelection_RemainsAvailableAfterReturningToPreferred()
    {
        var picker = new GrindPickerState(Range(), 270);
        picker.ToggleRange();
        picker.Select(1480);
        picker.ToggleRange();

        Assert.Equal(1480, picker.DoneValue);
        Assert.Contains(1480, picker.Values);
        Assert.DoesNotContain(1430, picker.Values);
        Assert.False(picker.IsPreferred(1480));
        Assert.False(picker.IsOutsidePreferredRange);
        Assert.Equal("Showing your preferred range.", picker.RangeDescription);
    }

    [Fact]
    public void NullOriginal_CommitsHistoryOrDefaultWithoutRequiringSelection()
    {
        var fromHistory = new GrindPickerState(Range(), null, 800);
        var fromDefault = new GrindPickerState(Range(), null);

        Assert.Equal(800, fromHistory.DoneValue);
        Assert.False(fromHistory.HasChanged);
        Assert.False(fromHistory.IsOutsidePreferredRange);
        Assert.Equal(270, fromDefault.DoneValue);
        Assert.Equal("Showing your preferred range.", fromHistory.RangeDescription);
    }

    [Fact]
    public void UnchangedRehydration_PreservesOriginalUntilExplicitSelection()
    {
        var picker = new GrindPickerState(Range(), 273, 300, false, true);

        Assert.Equal(300, picker.StagedValue);
        Assert.Equal(273, picker.DoneValue);
        picker.Select(305);
        Assert.Equal(305, picker.DoneValue);
        Assert.True(picker.HasChanged);
    }

    [Fact]
    public void Selection_RemovesOldOffStepValueAsSourceRerenderDoes()
    {
        var picker = new GrindPickerState(Range(), 273);
        picker.Select(275);

        Assert.DoesNotContain(273, picker.Values);
        Assert.Equal(275, picker.Values[picker.SelectedIndex]);
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.Select(274));
        Assert.Equal(275, picker.DoneValue);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(0.5)]
    public void InvalidStep_IsRejected(decimal step)
    {
        Assert.Throws<ArgumentException>(() => new GrindPickerState(Range() with { Step = step }, null));
    }

    [Fact]
    public void Badge_PreservesSourceActionsAndRoundedSettingText()
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("Select grinder for dial setting", DrinkDisplay.GrindBadge(null, null, null, 550));
            Assert.Equal("Manual grinder \u00B7 Set up scale",
                DrinkDisplay.GrindBadge(1, "Manual grinder", null, 550));
            Assert.Equal("Grinder \u00B7 Set up scale",
                DrinkDisplay.GrindBadge(1, null, Array.Empty<GrindAnchor>(), 550));
            Assert.Equal("DF64 \u00B7 45.5",
                DrinkDisplay.GrindBadge(1, "DF64", DeterministicGrindInterpolator.DF64SeedAnchors, 550));
        }
        finally
        {
            CultureInfo.CurrentCulture = prior;
        }
    }
}
