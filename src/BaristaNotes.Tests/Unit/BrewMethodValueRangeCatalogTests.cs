using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Tests.Unit;

public class BrewMethodValueRangeCatalogTests
{
    [Fact]
    public void EveryMethodAndMetricHasValidAutoAndHardRanges()
    {
        foreach (var method in BrewMethodExtensions.All)
        {
            foreach (var metric in Enum.GetValues<DrinkValueMetric>())
            {
                var definition = BrewMethodValueRangeCatalog.GetDefinition(method, metric);

                Assert.True(definition.AutoRange.Minimum < definition.AutoRange.Maximum);
                Assert.True(definition.HardRange.Minimum < definition.HardRange.Maximum);
                Assert.True(definition.HardRange.Contains(definition.AutoRange.Minimum));
                Assert.True(definition.HardRange.Contains(definition.AutoRange.Maximum));
                Assert.True(definition.AutoRange.Contains(definition.Default));
                Assert.True(definition.Step > 0);
                Assert.False(string.IsNullOrWhiteSpace(definition.CanonicalUnit));
            }
        }
    }

    [Theory]
    [InlineData(BrewMethod.Espresso, DrinkValueMetric.DoseIn, 5, 30, 18)]
    [InlineData(BrewMethod.PourOver, DrinkValueMetric.Yield, 100, 800, 320)]
    [InlineData(BrewMethod.ColdBrew, DrinkValueMetric.Time, 14400, 86400, 43200)]
    [InlineData(BrewMethod.FrenchPress, DrinkValueMetric.GrindMicrons, 690, 1300, 1000)]
    public void DefinitionPreservesRecommendedRanges(
        BrewMethod method,
        DrinkValueMetric metric,
        decimal minimum,
        decimal maximum,
        decimal defaultValue)
    {
        var definition = BrewMethodValueRangeCatalog.GetDefinition(method, metric);

        Assert.Equal(minimum, definition.AutoRange.Minimum);
        Assert.Equal(maximum, definition.AutoRange.Maximum);
        Assert.Equal(defaultValue, definition.Default);
    }
}
