using BaristaNotes.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// Blue Bottle adapter — stub. See <see cref="CounterCultureAdapter"/> for
/// the rationale. A real implementation would fetch Blue Bottle's product
/// page and parse any published brewing parameters.
/// </summary>
public sealed class BlueBottleAdapter : HttpRoasterRecipeAdapterBase
{
    public override string Id => "bluebottle";
    public override string RoasterName => "Blue Bottle";

    public BlueBottleAdapter(HttpClient httpClient)
        : base(httpClient, NullLogger<BlueBottleAdapter>.Instance) { }

    public BlueBottleAdapter(HttpClient httpClient, ILogger<BlueBottleAdapter> logger)
        : base(httpClient, logger) { }

    public override bool CanHandle(Bean bean)
    {
        if (string.IsNullOrWhiteSpace(bean.Roaster)) return false;
        var r = bean.Roaster.Trim().ToLowerInvariant();
        return r.Contains("blue bottle", StringComparison.Ordinal);
    }

    protected override Task<IReadOnlyList<ScrapedRecipe>> FetchCoreAsync(
        Bean bean, CancellationToken ct)
    {
        Logger.LogDebug("BlueBottleAdapter stub invoked BeanId={BeanId}", bean.Id);
        return Task.FromResult<IReadOnlyList<ScrapedRecipe>>(Array.Empty<ScrapedRecipe>());
    }
}
