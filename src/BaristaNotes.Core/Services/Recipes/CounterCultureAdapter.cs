using BaristaNotes.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// Counter Culture Coffee adapter — stub. Marks the roaster as "recognized"
/// so the registry routes to this adapter instead of the AI fallback path,
/// but currently returns an empty list pending implementation. A real
/// implementation would fetch https://counterculturecoffee.com/products/{slug}
/// and parse out the "Brewing Guide" section.
/// </summary>
public sealed class CounterCultureAdapter : HttpRoasterRecipeAdapterBase
{
    public override string Id => "counterculture";
    public override string RoasterName => "Counter Culture";

    public CounterCultureAdapter(HttpClient httpClient)
        : base(httpClient, NullLogger<CounterCultureAdapter>.Instance) { }

    public CounterCultureAdapter(HttpClient httpClient, ILogger<CounterCultureAdapter> logger)
        : base(httpClient, logger) { }

    public override bool CanHandle(Bean bean)
    {
        if (string.IsNullOrWhiteSpace(bean.Roaster)) return false;
        var r = bean.Roaster.Trim().ToLowerInvariant();
        return r.Contains("counter culture", StringComparison.Ordinal);
    }

    protected override Task<IReadOnlyList<ScrapedRecipe>> FetchCoreAsync(
        Bean bean, CancellationToken ct)
    {
        Logger.LogDebug("CounterCultureAdapter stub invoked BeanId={BeanId}", bean.Id);
        return Task.FromResult<IReadOnlyList<ScrapedRecipe>>(Array.Empty<ScrapedRecipe>());
    }
}
