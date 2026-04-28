using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// Pluggable per-roaster recipe scraper. Each implementation knows how to:
///   1. Decide whether it can handle a given <see cref="Bean"/> (typically by
///      matching the normalized <see cref="Bean.Roaster"/> name).
///   2. Fetch the roaster's page(s) for that bean and parse out any recipes.
///
/// Adapters must not throw on network failure — they should log and return an
/// empty list so the sourcing pipeline can fall back to AI generation.
/// </summary>
public interface IRoasterRecipeAdapter
{
    /// <summary>A stable identifier for diagnostics/logging (e.g. "onyx").</summary>
    string Id { get; }

    /// <summary>Human-readable roaster name this adapter handles.</summary>
    string RoasterName { get; }

    /// <summary>
    /// True if this adapter can likely produce recipes for the given bean.
    /// Typically implemented as a case-insensitive match on <see cref="Bean.Roaster"/>.
    /// </summary>
    bool CanHandle(Bean bean);

    /// <summary>
    /// Fetches and parses recipes for <paramref name="bean"/>. Returns an empty
    /// list if nothing is found or on failure. Never throws.
    /// </summary>
    Task<IReadOnlyList<ScrapedRecipe>> FetchAsync(Bean bean, CancellationToken ct);
}
