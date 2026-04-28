using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.Recipes;
using Xunit;

namespace BaristaNotes.Tests.Unit;

public class OnyxCoffeeLabAdapterTests
{
    private static Bean MakeBean(string? roaster, string? name = "Monarch")
        => new() { Name = name ?? "X", Roaster = roaster };

    [Fact]
    public void BuildProductUrl_SlugifiesName()
    {
        Assert.Equal("https://onyxcoffeelab.com/products/monarch",
            OnyxCoffeeLabAdapter.BuildProductUrl("Monarch"));
    }

    [Fact]
    public void BuildProductUrl_HandlesSpacesAndPunctuation()
    {
        Assert.Equal("https://onyxcoffeelab.com/products/geometry-colombia-natural",
            OnyxCoffeeLabAdapter.BuildProductUrl("Geometry: Colombia Natural"));
    }

    [Fact]
    public void ParseRecipes_ExtractsEspressoRecipeWithDoseYieldTime()
    {
        var html = """
            <html><body>
              <h2>Espresso Recipe</h2>
              <p>Dose: 18g : 36g yield in 28 seconds</p>
              <p>Grind: medium-fine</p>
              <p>Temperature: 200°F</p>
              <h2>Notes</h2>
              <p>some other stuff</p>
            </body></html>
            """;

        var recipes = OnyxCoffeeLabAdapter.ParseRecipes(html, "https://example/onyx");

        var espresso = Assert.Single(recipes);
        Assert.Equal(BrewMethod.Espresso, espresso.BrewMethod);
        Assert.Equal(18m, espresso.DoseIn);
        Assert.Equal(36m, espresso.OutputAmount);
        Assert.Equal(28m, espresso.TotalTimeSeconds);
        Assert.Equal("medium-fine", espresso.GrindHint);
        Assert.NotNull(espresso.BrewTempC);
        Assert.InRange(espresso.BrewTempC!.Value, 93m, 94m);
        Assert.Equal("https://example/onyx", espresso.SourceUrl);
    }

    [Fact]
    public void ParseRecipes_ExtractsPourOverWithMmSsTime()
    {
        var html = """
            <h3>Pour Over</h3>
            <p>22g : 360g, 3:30</p>
            <p>Grind: medium</p>
            <h3>Shipping</h3>
            """;

        var recipes = OnyxCoffeeLabAdapter.ParseRecipes(html, "https://x");

        var po = Assert.Single(recipes);
        Assert.Equal(BrewMethod.PourOver, po.BrewMethod);
        Assert.Equal(22m, po.DoseIn);
        Assert.Equal(360m, po.OutputAmount);
        Assert.Equal(210m, po.TotalTimeSeconds);
    }

    [Fact]
    public void ParseRecipes_ReturnsBothEspressoAndPourOver()
    {
        var html = """
            <h2>Espresso</h2>
            <p>18g to 36g in 28s</p>
            <h2>Pour Over</h2>
            <p>22g : 360g, 3:15</p>
            """;

        var recipes = OnyxCoffeeLabAdapter.ParseRecipes(html, "https://x");

        Assert.Equal(2, recipes.Count);
        Assert.Contains(recipes, r => r.BrewMethod == BrewMethod.Espresso);
        Assert.Contains(recipes, r => r.BrewMethod == BrewMethod.PourOver);
    }

    [Fact]
    public void ParseRecipes_ReturnsEmptyWhenNoRecognizableHeadings()
    {
        var html = "<html><body><p>Just some marketing copy.</p></body></html>";

        var recipes = OnyxCoffeeLabAdapter.ParseRecipes(html, "https://x");

        Assert.Empty(recipes);
    }

    [Fact]
    public void ParseRecipes_SkipsSectionWithNoUsefulData()
    {
        var html = """
            <h2>Espresso</h2>
            <p>We love espresso at Onyx.</p>
            <h2>Shipping</h2>
            """;

        var recipes = OnyxCoffeeLabAdapter.ParseRecipes(html, "https://x");

        Assert.Empty(recipes);
    }

    [Fact]
    public void ExtractSectionBody_StopsAtNextEqualLevelHeading()
    {
        var html = "<h2>Espresso</h2><p>body1</p><h2>Pour Over</h2><p>body2</p>";

        var body = OnyxCoffeeLabAdapter.ExtractSectionBody(html, "espresso");

        Assert.NotNull(body);
        Assert.Contains("body1", body);
        Assert.DoesNotContain("body2", body);
    }

    [Fact]
    public void CanHandle_AcceptsVariationsOfOnyxRoaster()
    {
        var adapter = new OnyxCoffeeLabAdapter(new HttpClient());

        Assert.True(adapter.CanHandle(MakeBean("Onyx")));
        Assert.True(adapter.CanHandle(MakeBean("Onyx Coffee")));
        Assert.True(adapter.CanHandle(MakeBean("Onyx Coffee Lab")));
        Assert.False(adapter.CanHandle(MakeBean("Blue Bottle")));
        Assert.False(adapter.CanHandle(MakeBean(null)));
    }
}
