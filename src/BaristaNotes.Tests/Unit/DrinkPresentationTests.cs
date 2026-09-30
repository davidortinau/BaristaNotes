using System.Globalization;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.Tests.Unit;

public sealed class DrinkPresentationTests
{
    private static EffectiveDrinkValueRange MassRange() =>
        new(new(14, 22), new(1, 100), 18, 0.1m, "g", ValueRangeSource.Auto);

    [Fact]
    public void MassSelection_CombinesColumnsAndClampsAtTheBoundary()
    {
        var picker = new MassPickerState(MassRange(), 18.3m);
        var rows = picker.WholeValues;

        Assert.Equal(Enumerable.Range(14, 9), rows);
        Assert.Equal(Enumerable.Range(0, 10), picker.TenthValues);
        Assert.Equal(18, picker.SelectedWhole);
        Assert.Equal(3, picker.SelectedTenth);

        picker.SelectWhole(20);
        picker.SelectTenth(9);
        Assert.Equal(20.9m, picker.DoneValue);
        Assert.True(picker.HasChanged);
        Assert.Same(rows, picker.WholeValues);

        picker.SelectWhole(22);
        Assert.Equal(22m, picker.DoneValue);
        Assert.Equal(0, picker.SelectedTenth);
    }

    [Fact]
    public void UnchangedDone_PreservesTheOriginalOutsidePreferredRange()
    {
        var picker = new MassPickerState(MassRange(), 12.35m);

        Assert.Equal(14m, picker.DisplayedValue);
        Assert.Equal(12.35m, picker.DoneValue);
        Assert.True(picker.IsOutsidePreferredRange);
        Assert.False(picker.HasChanged);

        picker.ToggleRange();
        Assert.Equal(12.4m, picker.DisplayedValue);
        Assert.Equal(12.35m, picker.DoneValue);
        Assert.False(picker.HasChanged);
        Assert.Equal("Current value is outside your preferred range.", picker.RangeDescription);
        Assert.Equal("PREFERRED", picker.RangeToggleText);
    }

    [Fact]
    public void RangeToggle_PreservesStagedValueButCommitUsesActiveClamp()
    {
        var picker = new MassPickerState(MassRange(), 18.3m);
        picker.ToggleRange();
        picker.SelectWhole(30);
        Assert.Equal(30.3m, picker.StagedValue);

        picker.ToggleRange();

        Assert.Equal(30.3m, picker.StagedValue);
        Assert.Equal(22m, picker.DisplayedValue);
        Assert.Equal(22m, picker.DoneValue);
        Assert.Equal("Showing your preferred range.", picker.RangeDescription);
    }

    [Fact]
    public void RehydratedMassState_MatchesTheRetainedMauiState()
    {
        var picker = new MassPickerState(MassRange(), 18m, 24.25m, true, true);

        Assert.True(picker.ShowsFullRange);
        Assert.True(picker.HasChanged);
        Assert.Equal(24.3m, picker.DoneValue);
        Assert.Equal("Showing the full allowed range.", picker.RangeDescription);
    }

    [Fact]
    public void FractionalMinimum_CannotBeUndercutByTenthSelection()
    {
        var definition = MassRange() with { Range = new DrinkValueRange(20.5m, 25.5m) };
        var picker = new MassPickerState(definition, 20.5m);

        picker.SelectTenth(0);

        Assert.Equal(20.5m, picker.DoneValue);
        Assert.Equal(5, picker.SelectedTenth);
    }

    [Fact]
    public void InvalidRows_ThrowWithoutChangingSelection()
    {
        var picker = new MassPickerState(MassRange(), 18m);

        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SelectWhole(13));
        Assert.Throws<ArgumentOutOfRangeException>(() => picker.SelectTenth(10));
        Assert.False(picker.HasChanged);
        Assert.Equal(18m, picker.DoneValue);
    }

    [Theory]
    [InlineData(28, "28", "s")]
    [InlineData(60, "1:00", null)]
    [InlineData(125, "2:05", null)]
    [InlineData(3600, "1h", null)]
    [InlineData(3660, "1h 1m", null)]
    public void TimeFormatting_MatchesTheSource(decimal seconds, string value, string? unit)
    {
        Assert.Equal(value, DrinkDisplay.TimeValue(seconds));
        Assert.Equal(unit, DrinkDisplay.TimeUnit(seconds));
    }

    [Fact]
    public void RatingFormatting_UsesOneThroughFiveFilledStars()
    {
        Assert.Equal("\u2605\u2606\u2606\u2606\u2606", DrinkDisplay.RatingText(0));
        Assert.Equal(new string('\u2605', 5), DrinkDisplay.RatingText(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => DrinkDisplay.RatingText(5));
    }

    [Fact]
    public void TemperatureFormatting_PreservesCanonicalConversion()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Equal("199", DrinkDisplay.WaterTemperatureValue(93m, TemperatureUnit.Fahrenheit));
            Assert.Equal("93", DrinkDisplay.WaterTemperatureValue(93m, TemperatureUnit.Celsius));
            Assert.Equal("\u00B0F", DrinkDisplay.WaterTemperatureUnit(93m, TemperatureUnit.Fahrenheit));
            Assert.Equal("93 \u00B0C", DrinkDisplay.WaterTemperatureSecondary(93m, TemperatureUnit.Fahrenheit));
            Assert.Equal(93.33m, DrinkDisplay.FahrenheitToCelsius(200));
            Assert.Equal("\u2014", DrinkDisplay.WaterTemperatureValue(null, TemperatureUnit.Celsius));
            Assert.Null(DrinkDisplay.WaterTemperatureUnit(null, TemperatureUnit.Celsius));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ActivityFormatting_UsesActualValuesAndDefensiveRatingClamp()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var today = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Local);
            var shot = new ShotRecordDto
            {
                BrewMethod = BrewMethod.Espresso,
                DoseIn = 18.3m,
                ExpectedOutput = 36.5m,
                ActualOutput = 39m,
                ExpectedTime = 28,
                ActualTime = 30,
                Rating = 5,
                Bag = new BagSummaryDto { BeanName = "Fallback bean" },
                Timestamp = today.AddHours(13).AddMinutes(5)
            };

            var display = DrinkDisplay.ActivityRow(shot, today);

            Assert.Equal("Espresso", display.Title);
            Assert.Equal("18.3g \u2192 39g", display.Ratio);
            Assert.Equal("Fallback bean  \u00B7  Today 1:05 PM", display.Subtitle);
            Assert.Equal("30s   " + new string('\u2605', 5), display.Result);
            Assert.StartsWith("Yesterday ", DrinkDisplay.FormatTimestamp(shot.Timestamp.AddDays(-1), today));
            Assert.Equal("Sep 15, 2026", DrinkDisplay.FormatTimestamp(shot.Timestamp.AddDays(-10), today));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void FilterWorkingCopy_DoesNotApplyUntilSelected()
    {
        var applied = new ShotFilterCriteria { BeanIds = [1], Ratings = [3] };
        var working = applied.Clone();
        working.BeanIds.Add(2);
        working.Ratings.Clear();

        Assert.Equal(new[] { 1 }, applied.BeanIds);
        Assert.Equal(new[] { 3 }, applied.Ratings);
        Assert.Equal(2, working.FilterCount);
        var dto = working.ToDto();
        Assert.Null(dto.Ratings);
        working.Clear();
        Assert.False(working.HasFilters);
        Assert.Equal(new[] { 1, 2 }, dto.BeanIds);
    }
}
