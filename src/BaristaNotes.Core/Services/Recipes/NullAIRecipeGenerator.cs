using BaristaNotes.Core.Models;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// Placeholder AI recipe generator that reports itself as unavailable.
/// The real implementation (using IChatClient with Apple Intelligence + Azure
/// OpenAI fallback, structured JSON output) will be added in a follow-up so
/// that Phase B delivers deterministic, scraper-first behavior first.
/// </summary>
public sealed class NullAIRecipeGenerator(ILogger<NullAIRecipeGenerator> logger) : IAIRecipeGenerator
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<ScrapedRecipe>> GenerateAsync(Bean bean, CancellationToken ct)
    {
        logger.LogDebug("AI recipe generation is unavailable");
        return Task.FromResult<IReadOnlyList<ScrapedRecipe>>(Array.Empty<ScrapedRecipe>());
    }
}
