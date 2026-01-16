namespace BaristaNotes.Core.Services;

/// <summary>
/// Registry for app navigation routes with descriptions for AI reasoning.
/// Allows IChatClient to discover and understand available navigation destinations.
/// </summary>
public interface INavigationRegistry
{
    /// <summary>
    /// Gets all registered navigation destinations.
    /// </summary>
    IReadOnlyList<NavigationDestination> GetDestinations();

    /// <summary>
    /// Finds a destination by name or alias (case-insensitive).
    /// </summary>
    NavigationDestination? FindDestination(string nameOrAlias);

    /// <summary>
    /// Gets a formatted string describing all available destinations for AI context.
    /// </summary>
    string GetDestinationsDescription();
}

/// <summary>
/// Represents a navigable destination in the app.
/// </summary>
public record NavigationDestination
{
    /// <summary>
    /// The Shell route (e.g., "//shots", "//history").
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// Display name shown in UI (e.g., "New Shot", "Activity").
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Description of the page's purpose - used by AI to reason about when to navigate here.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Alternative names/phrases users might say to navigate here.
    /// </summary>
    public required IReadOnlyList<string> Aliases { get; init; }

    /// <summary>
    /// Keywords that indicate this page is relevant to a user's request.
    /// </summary>
    public required IReadOnlyList<string> Keywords { get; init; }
}
