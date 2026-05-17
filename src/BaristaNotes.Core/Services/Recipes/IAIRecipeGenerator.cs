using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// Abstracts the AI-based recipe generator used as a fallback when no
/// roaster adapter can supply recipes. The concrete implementation lives in
/// the app project so that <c>BaristaNotes.Core</c> has no dependency on
/// Microsoft.Extensions.AI or a specific chat client.
/// </summary>
public interface IAIRecipeGenerator
{
    /// <summary>True if the generator is available (API key configured, etc.).</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Generates one or more recipes for the bean. Returns an empty list on
    /// failure. Must never throw.
    /// </summary>
    Task<IReadOnlyList<ScrapedRecipe>> GenerateAsync(Bean bean, CancellationToken ct);
}
