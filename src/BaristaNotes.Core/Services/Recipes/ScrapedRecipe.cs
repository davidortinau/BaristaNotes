using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// A recipe as returned by a roaster adapter, before it is persisted.
/// </summary>
public sealed record ScrapedRecipe
{
    public required BrewMethod BrewMethod { get; init; }
    public string? Title { get; init; }
    public string? SourceUrl { get; init; }
    public decimal? DoseIn { get; init; }
    public decimal? OutputAmount { get; init; }
    public string? GrindHint { get; init; }
    public decimal? BrewTempC { get; init; }
    public decimal? TotalTimeSeconds { get; init; }
    public string? ParametersJson { get; init; }
    public string? Notes { get; init; }
}
