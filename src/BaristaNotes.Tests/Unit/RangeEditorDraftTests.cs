using System.Globalization;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using BaristaNotes.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Tests.Unit;

public sealed class RangeEditorDraftTests : IDisposable
{
    private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;

    public RangeEditorDraftTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    }

    public void Dispose() => CultureInfo.CurrentCulture = _originalCulture;

    private static DrinkValueRangeSettingsSnapshot Settings(
        params DrinkValueRangeOverride[] overrides) =>
        new(new Dictionary<DrinkValueMetric, ValueRangeMode>(), overrides, null);

    public static IEnumerable<object[]> MethodMetrics() =>
        from method in BrewMethodExtensions.All
        from metric in Enum.GetValues<DrinkValueMetric>()
        select new object[] { method, metric };

    [Theory]
    [MemberData(nameof(MethodMetrics))]
    public void UntouchedAutomaticRange_PreservesCatalogValues(
        BrewMethod method, DrinkValueMetric metric)
    {
        var draft = new RangeEditorDraft(metric, method, Settings());

        Assert.False(draft.HasOverride);
        Assert.False(draft.IsDirty);
        Assert.Null(draft.ValidationError);
        Assert.True(draft.TryGetRange(out var range, out var error));
        Assert.Null(error);
        Assert.Equal(BrewMethodValueRangeCatalog.GetDefinition(method, metric).AutoRange, range);
    }

    [Fact]
    public void Load_UsesLastMatchingOverrideEvenWhenModeIsAutomatic()
    {
        var draft = new RangeEditorDraft(DrinkValueMetric.DoseIn, BrewMethod.Espresso, Settings(
            new(DrinkValueMetric.DoseIn, BrewMethod.Espresso, 16, 22),
            new(DrinkValueMetric.Yield, BrewMethod.Espresso, 25, 55),
            new(DrinkValueMetric.DoseIn, BrewMethod.V60, 12, 32),
            new(DrinkValueMetric.DoseIn, BrewMethod.Espresso, 20, 25)));

        Assert.True(draft.HasOverride);
        Assert.Equal("20", draft.MinimumText);
        Assert.Equal("25", draft.MaximumText);
        Assert.False(draft.IsDirty);
    }

    [Theory]
    [InlineData("", "25", "Enter valid values in grams.", false)]
    [InlineData("20", " ", "Enter valid values in grams.", false)]
    [InlineData("not a number", "25", "Enter valid values in grams.", true)]
    [InlineData("25", "25", "Minimum must be less than maximum.", true)]
    [InlineData("26", "25", "Minimum must be less than maximum.", true)]
    [InlineData("4", "25", "Use values from 5 g - 30 g.", true)]
    [InlineData("20", "31", "Use values from 5 g - 30 g.", true)]
    [InlineData("20.05", "25", "Use no more than one decimal place.", true)]
    public void InvalidInput_PreservesSourceValidation(
        string minimum, string maximum, string expectedError, bool liveError)
    {
        var draft = new RangeEditorDraft(DrinkValueMetric.DoseIn, BrewMethod.Espresso, Settings())
        {
            MinimumText = minimum,
            MaximumText = maximum
        };

        Assert.False(draft.TryGetRange(out _, out var error));
        Assert.Equal(expectedError, error);
        Assert.Equal(liveError ? expectedError : null, draft.ValidationError);
    }

    [Theory]
    [InlineData(DrinkValueMetric.DoseIn, BrewMethod.Espresso, "5", "30", "5", "30")]
    [InlineData(DrinkValueMetric.Yield, BrewMethod.Espresso, "20.5", "40.2", "20.5", "40.2")]
    [InlineData(DrinkValueMetric.GrindMicrons, BrewMethod.V60, "40", "1500", "40", "1500")]
    [InlineData(DrinkValueMetric.Time, BrewMethod.Espresso, "20", "45", "20", "45")]
    [InlineData(DrinkValueMetric.Time, BrewMethod.V60, "1.5", "4.5", "90", "270")]
    [InlineData(DrinkValueMetric.Time, BrewMethod.ColdBrew, "1.5", "12.5", "5400", "45000")]
    [InlineData(DrinkValueMetric.Time, BrewMethod.ColdDrip, "0.5", "8", "1800", "28800")]
    public void ValidInput_ConvertsToCanonicalUnits(
        DrinkValueMetric metric, BrewMethod method, string minimum, string maximum,
        string expectedMinimum, string expectedMaximum)
    {
        var draft = new RangeEditorDraft(metric, method, Settings())
        {
            MinimumText = minimum,
            MaximumText = maximum
        };

        Assert.Null(draft.ValidationError);
        Assert.True(draft.TryGetRange(out var range, out var error));
        Assert.Null(error);
        Assert.Equal(new DrinkValueRange(
            decimal.Parse(expectedMinimum, CultureInfo.InvariantCulture),
            decimal.Parse(expectedMaximum, CultureInfo.InvariantCulture)), range);
    }

    [Theory]
    [InlineData(DrinkValueMetric.GrindMicrons, BrewMethod.V60, "500.5", "700")]
    [InlineData(DrinkValueMetric.Time, BrewMethod.Espresso, "20.5", "45")]
    [InlineData(DrinkValueMetric.Time, BrewMethod.V60, "1.02", "4")]
    [InlineData(DrinkValueMetric.Time, BrewMethod.ColdBrew, "4.001", "20")]
    public void FractionalCanonicalWholeUnits_AreRejected(
        DrinkValueMetric metric, BrewMethod method, string minimum, string maximum)
    {
        var draft = new RangeEditorDraft(metric, method, Settings())
        {
            MinimumText = minimum,
            MaximumText = maximum
        };

        Assert.False(draft.TryGetRange(out _, out var error));
        Assert.Equal("Use values that convert to whole units.", error);
    }

    [Theory]
    [InlineData(BrewMethod.V60, 61, 241, "1.02", "4.02", "5", 300)]
    [InlineData(BrewMethod.ColdBrew, 3601, 7201, "1", "2", "3", 10800)]
    public void UnchangedRoundedEndpoint_PreservesOriginalSeconds(
        BrewMethod method, decimal minimum, decimal maximum,
        string displayedMinimum, string displayedMaximum, string editedMaximum,
        decimal expectedMaximum)
    {
        var draft = new RangeEditorDraft(DrinkValueMetric.Time, method, Settings(
            new DrinkValueRangeOverride(DrinkValueMetric.Time, method, minimum, maximum)));

        Assert.Equal(displayedMinimum, draft.MinimumText);
        Assert.Equal(displayedMaximum, draft.MaximumText);
        Assert.True(draft.TryGetRange(out var untouched, out _));
        Assert.Equal(new DrinkValueRange(minimum, maximum), untouched);

        draft.MaximumText = editedMaximum;

        Assert.True(draft.IsDirty);
        Assert.True(draft.TryGetRange(out var edited, out _));
        Assert.Equal(new DrinkValueRange(minimum, expectedMaximum), edited);
    }

    [Fact]
    public void DirtyState_ComparesTextAndClearsWhenTextIsRestored()
    {
        var draft = new RangeEditorDraft(DrinkValueMetric.DoseIn, BrewMethod.Espresso, Settings());

        draft.MinimumText = "5.0";
        Assert.True(draft.IsDirty);
        Assert.True(draft.TryGetRange(out var range, out _));
        Assert.Equal(5m, range.Minimum);

        draft.MinimumText = "5";
        Assert.False(draft.IsDirty);
    }

    [Fact]
    public void ParsingAndFormatting_UseCurrentCulture()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        var draft = new RangeEditorDraft(DrinkValueMetric.DoseIn, BrewMethod.Espresso, Settings(
            new DrinkValueRangeOverride(DrinkValueMetric.DoseIn, BrewMethod.Espresso, 20.5m, 25.5m)));
        Assert.Equal("20,5", draft.MinimumText);

        draft.MinimumText = "21,5";
        Assert.True(draft.TryGetRange(out var range, out _));
        Assert.Equal(new DrinkValueRange(21.5m, 25.5m), range);
    }

    [Fact]
    public void Recommended_StagesValuesWithoutRemovingAnOverride()
    {
        var settings = Settings(new DrinkValueRangeOverride(
            DrinkValueMetric.DoseIn, BrewMethod.Espresso, 20, 25));
        var draft = new RangeEditorDraft(DrinkValueMetric.DoseIn, BrewMethod.Espresso, settings);

        draft.UseRecommended();

        Assert.Equal("5", draft.MinimumText);
        Assert.Equal("30", draft.MaximumText);
        Assert.True(draft.IsDirty);
        Assert.True(draft.HasOverride);
        Assert.Single(settings.Overrides);
        Assert.Equal(20m, settings.Overrides[0].Minimum);
    }

    [Fact]
    public void RecommendedWithoutOverride_RestoresOriginalText()
    {
        var draft = new RangeEditorDraft(DrinkValueMetric.DoseIn, BrewMethod.Espresso, Settings())
        {
            MinimumText = "invalid"
        };
        Assert.NotNull(draft.ValidationError);

        draft.UseRecommended();

        Assert.False(draft.IsDirty);
        Assert.False(draft.HasOverride);
        Assert.Null(draft.ValidationError);
    }

    [Fact]
    public void Draft_DoesNotPersistUntilTheCallerSaves()
    {
        var store = new MockPreferencesStore();
        var preferences = new PreferencesService(store);
        var service = new DrinkValueRangeService(
            preferences, NullLogger<DrinkValueRangeService>.Instance);
        var draft = new RangeEditorDraft(
            DrinkValueMetric.DoseIn, BrewMethod.Espresso, service.GetSettings())
        {
            MinimumText = "20",
            MaximumText = "25"
        };

        Assert.Null(preferences.GetDrinkValueRangeSettingsJson());
        Assert.True(draft.TryGetRange(out var range, out _));
        service.SaveOverride(draft.Metric, draft.Method, range.Minimum, range.Maximum);
        service.SetMode(draft.Metric, ValueRangeMode.Auto);

        var reloaded = new DrinkValueRangeService(
            preferences, NullLogger<DrinkValueRangeService>.Instance);
        var reopened = new RangeEditorDraft(draft.Metric, draft.Method, reloaded.GetSettings());
        Assert.Equal("20", reopened.MinimumText);
        Assert.Equal("25", reopened.MaximumText);
        Assert.True(reopened.HasOverride);
        Assert.False(reopened.IsDirty);
        Assert.Equal(18m, reloaded.Resolve(draft.Metric, draft.Method).Default);

        reloaded.SetMode(draft.Metric, ValueRangeMode.Custom);
        Assert.Equal(20m, reloaded.Resolve(draft.Metric, draft.Method).Default);
    }
}
