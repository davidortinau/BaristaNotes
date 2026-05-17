using BaristaNotes.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// Intelligentsia adapter — stub. See <see cref="CounterCultureAdapter"/>.
/// </summary>
public sealed class IntelligentsiaAdapter : HttpRoasterRecipeAdapterBase
{
    public override string Id => "intelligentsia";
    public override string RoasterName => "Intelligentsia";

    public IntelligentsiaAdapter(HttpClient httpClient)
        : base(httpClient, NullLogger<IntelligentsiaAdapter>.Instance) { }

    public IntelligentsiaAdapter(HttpClient httpClient, ILogger<IntelligentsiaAdapter> logger)
        : base(httpClient, logger) { }

    public override bool CanHandle(Bean bean)
    {
        if (string.IsNullOrWhiteSpace(bean.Roaster)) return false;
        var r = bean.Roaster.Trim().ToLowerInvariant();
        return r.Contains("intelligentsia", StringComparison.Ordinal);
    }

    protected override Task<IReadOnlyList<ScrapedRecipe>> FetchCoreAsync(
        Bean bean, CancellationToken ct)
    {
        Logger.LogDebug("IntelligentsiaAdapter stub invoked BeanId={BeanId}", bean.Id);
        return Task.FromResult<IReadOnlyList<ScrapedRecipe>>(Array.Empty<ScrapedRecipe>());
    }
}
