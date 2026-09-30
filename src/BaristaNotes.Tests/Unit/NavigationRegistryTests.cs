using BaristaNotes.Core.Hosting;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Tests.Unit;

public sealed class NavigationRegistryTests
{
    [Fact]
    public void SourceRoutes_AreRegisteredOnceInSourceOrder()
    {
        var registry = new NavigationRegistry(NullLogger<NavigationRegistry>.Instance);
        string[] expected =
        [
            "//shots", "//history", "//settings", "profiles", "beans", "equipment",
            "bean-detail", "bag-detail", "equipment-detail", "profile-form", "value-ranges"
        ];

        Assert.Equal(expected, registry.GetDestinations().Select(item => item.Route));
        registry.DiscoverRoutes();
        registry.DiscoverRoutes();
        Assert.Equal(expected, registry.GetDestinations().Select(item => item.Route));
    }

    [Theory]
    [InlineData("shots", "//shots")]
    [InlineData("//history", "//history")]
    [InlineData("  SETTINGS  ", "//settings")]
    [InlineData("Activity", "//history")]
    [InlineData("baristas", "profiles")]
    [InlineData("coffee beans", "beans")]
    [InlineData("grinders", "equipment")]
    [InlineData("new profile", "profile-form")]
    [InlineData("yield range", "value-ranges")]
    [InlineData("please show my equipment", "equipment")]
    public void Lookup_PreservesExactAndAliasPrecedence(string text, string route)
    {
        var registry = new NavigationRegistry(NullLogger<NavigationRegistry>.Instance);
        Assert.Equal(route, registry.FindDestination(text)?.Route);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("unrelated destination")]
    public void UnmatchedLookup_DoesNotInventRoutes(string text)
    {
        var registry = new NavigationRegistry(NullLogger<NavigationRegistry>.Instance);
        Assert.Null(registry.FindDestination(text));
    }

    [Fact]
    public void PromptDescription_ContainsSourceNamesAndAliases()
    {
        var registry = new NavigationRegistry(NullLogger<NavigationRegistry>.Instance);
        var description = registry.GetDestinationsDescription();

        Assert.StartsWith("Available pages in the app:", description);
        Assert.Contains("**New Drink** (route: //shots)", description);
        Assert.Contains("**Value Ranges** (route: value-ranges)", description);
        Assert.Contains("Voice aliases:", description);
    }

    [Fact]
    public void SharedRegistration_IsSingletonAndIdempotent()
    {
        var services = new ServiceCollection();
        services.AddBaristaNotesDomain();
        services.AddBaristaNotesDomain();
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<INavigationRegistry>();
        using var scope = provider.CreateScope();

        Assert.Same(registry, scope.ServiceProvider.GetRequiredService<INavigationRegistry>());
        Assert.Single(provider.GetServices<INavigationRegistry>());
        Assert.Equal(11, registry.GetDestinations().Count);
    }
}
