using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Tests.Mocks;
using Microsoft.Extensions.Logging;
using Moq;

namespace BaristaNotes.Tests.Unit.Services;

public class DrinkValueRangeServiceTests
{
    private readonly MockPreferencesStore _store = new();
    private readonly PreferencesService _preferences;

    public DrinkValueRangeServiceTests()
    {
        _preferences = new PreferencesService(_store);
    }

    [Fact]
    public void ResolveDefaultsToAutomaticRange()
    {
        var service = CreateService();

        var range = service.Resolve(DrinkValueMetric.DoseIn, BrewMethod.Espresso);

        Assert.Equal(ValueRangeSource.Auto, range.Source);
        Assert.Equal(5, range.Range.Minimum);
        Assert.Equal(30, range.Range.Maximum);
    }

    [Fact]
    public void CustomModeUsesOverrideAndFallsBackForUneditedMethod()
    {
        var service = CreateService();
        service.SaveOverride(
            DrinkValueMetric.DoseIn,
            BrewMethod.Espresso,
            16,
            22);

        var espresso = service.Resolve(DrinkValueMetric.DoseIn, BrewMethod.Espresso);
        var pourOver = service.Resolve(DrinkValueMetric.DoseIn, BrewMethod.PourOver);

        Assert.Equal(ValueRangeSource.Custom, espresso.Source);
        Assert.Equal(16, espresso.Range.Minimum);
        Assert.Equal(22, espresso.Range.Maximum);
        Assert.Equal(ValueRangeSource.AutoFallback, pourOver.Source);
        Assert.Equal(10, pourOver.Range.Minimum);
        Assert.Equal(60, pourOver.Range.Maximum);
    }

    [Fact]
    public void SwitchingToAutoPreservesCustomOverrides()
    {
        var service = CreateService();
        service.SaveOverride(
            DrinkValueMetric.Yield,
            BrewMethod.Espresso,
            25,
            55);

        service.SetMode(DrinkValueMetric.Yield, ValueRangeMode.Auto);
        Assert.Equal(
            ValueRangeSource.Auto,
            service.Resolve(DrinkValueMetric.Yield, BrewMethod.Espresso).Source);

        service.SetMode(DrinkValueMetric.Yield, ValueRangeMode.Custom);
        var restored = service.Resolve(DrinkValueMetric.Yield, BrewMethod.Espresso);

        Assert.Equal(ValueRangeSource.Custom, restored.Source);
        Assert.Equal(25, restored.Range.Minimum);
        Assert.Equal(55, restored.Range.Maximum);
    }

    [Fact]
    public void CustomRangeClampsAutomaticDefault()
    {
        var service = CreateService();
        service.SaveOverride(
            DrinkValueMetric.DoseIn,
            BrewMethod.Espresso,
            22,
            25);

        var range = service.Resolve(DrinkValueMetric.DoseIn, BrewMethod.Espresso);

        Assert.Equal(22, range.Default);
    }

    [Theory]
    [InlineData(20, 20)]
    [InlineData(21, 20)]
    [InlineData(4, 20)]
    [InlineData(5.55, 20)]
    public void InvalidDoseOverrideIsRejected(decimal minimum, decimal maximum)
    {
        var service = CreateService();

        Assert.ThrowsAny<ArgumentException>(() =>
            service.SaveOverride(
                DrinkValueMetric.DoseIn,
                BrewMethod.Espresso,
                minimum,
                maximum));
    }

    [Fact]
    public void SettingsRoundTripThroughPreferences()
    {
        var first = CreateService();
        first.SaveOverride(
            DrinkValueMetric.GrindMicrons,
            BrewMethod.V60,
            450,
            650);

        var second = CreateService();
        var range = second.Resolve(DrinkValueMetric.GrindMicrons, BrewMethod.V60);

        Assert.Equal(ValueRangeMode.Custom, second.GetMode(DrinkValueMetric.GrindMicrons));
        Assert.Equal(ValueRangeSource.Custom, range.Source);
        Assert.Equal(450, range.Range.Minimum);
        Assert.Equal(650, range.Range.Maximum);
    }

    [Fact]
    public void MalformedSettingsUseAutomaticRangesWithWarning()
    {
        _preferences.SetDrinkValueRangeSettingsJson("{ invalid json");
        var service = CreateService();

        var snapshot = service.GetSettings();
        var range = service.Resolve(DrinkValueMetric.Time, BrewMethod.ColdBrew);

        Assert.NotNull(snapshot.LoadWarning);
        Assert.Equal(ValueRangeSource.Auto, range.Source);
        Assert.Equal(14400, range.Range.Minimum);
    }

    [Fact]
    public void NullSettingsCollectionsUseAutomaticRangesWithWarning()
    {
        _preferences.SetDrinkValueRangeSettingsJson(
            """{"SchemaVersion":1,"Modes":null,"Overrides":null}""");
        var service = CreateService();

        var snapshot = service.GetSettings();
        var range = service.Resolve(DrinkValueMetric.DoseIn, BrewMethod.Espresso);

        Assert.NotNull(snapshot.LoadWarning);
        Assert.Equal(ValueRangeSource.Auto, range.Source);
    }

    [Fact]
    public void ClearAllRemovesRangeSettings()
    {
        var service = CreateService();
        service.SaveOverride(
            DrinkValueMetric.Time,
            BrewMethod.Espresso,
            20,
            40);

        _preferences.ClearAll();

        Assert.Null(_preferences.GetDrinkValueRangeSettingsJson());
    }

    private DrinkValueRangeService CreateService() =>
        new(_preferences, Mock.Of<ILogger<DrinkValueRangeService>>());
}
