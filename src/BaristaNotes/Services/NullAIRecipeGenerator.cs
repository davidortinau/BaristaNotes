using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.Recipes;

namespace BaristaNotes.Services;

/// <summary>
/// Placeholder AI recipe generator that reports itself as unavailable.
/// The real implementation (using IChatClient with Apple Intelligence + Azure
/// OpenAI fallback, structured JSON output) will be added in a follow-up so
/// that Phase B delivers deterministic, scraper-first behavior first.
/// </summary>
public sealed class NullAIRecipeGenerator : IAIRecipeGenerator
{
    public bool IsAvailable => false;

    public Task<IReadOnlyList<ScrapedRecipe>> GenerateAsync(Bean bean, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<ScrapedRecipe>>(Array.Empty<ScrapedRecipe>());
}
