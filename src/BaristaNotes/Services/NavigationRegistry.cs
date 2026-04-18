using System.Text;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Services;

/// <summary>
/// Navigation registry that discovers Shell routes at runtime.
/// Page descriptions help IChatClient reason about navigation intents.
/// </summary>
public class NavigationRegistry : INavigationRegistry
{
    private readonly ILogger<NavigationRegistry> _logger;
    private readonly List<NavigationDestination> _destinations = new();
    private bool _isInitialized;

    // Page descriptions for AI reasoning - keyed by route name
    // Includes both Shell visual hierarchy routes AND globally registered routes
    private static readonly Dictionary<string, PageDescription> _pageDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        // === Shell Tab Bar Routes (visual hierarchy) ===
        ["shots"] = new PageDescription(
            "New Shot",
            "Log a new espresso shot. Use this page to record shot parameters like dose, yield, time, grind setting, and rating. This is where users create new shot records.",
            ["new shot", "log shot", "shot logging", "new", "log", "record shot", "pull shot", "make coffee", "espresso"],
            ["log", "new", "record", "create", "pull", "make", "dose", "yield", "grind", "espresso"],
            IsShellRoute: true
        ),
        ["history"] = new PageDescription(
            "Activity",
            "View shot history and activity feed. Shows all previously logged shots with their details, ratings, and trends. Use this to review past shots, see patterns, or find specific shots.",
            ["activity", "history", "feed", "my shots", "shot history", "past shots", "all shots", "activity feed", "timeline"],
            ["history", "past", "previous", "review", "view", "list", "all", "shots", "feed", "activity", "trend"],
            IsShellRoute: true
        ),
        ["settings"] = new PageDescription(
            "Settings",
            "App settings and configuration. Access to manage beans, equipment, user profiles, and app preferences.",
            ["settings", "preferences", "options", "config", "configuration", "setup"],
            ["settings", "preferences", "configure", "options"],
            IsShellRoute: true
        ),

        // === Global Routes (registered via Routing.RegisterRoute) ===
        ["profiles"] = new PageDescription(
            "User Profiles",
            "Manage user profiles (baristas). View, add, edit, or delete people who make or receive espresso drinks. Navigate here to see all profiles or manage them.",
            ["profiles", "users", "people", "baristas", "profile management", "user management", "who", "person"],
            ["profile", "user", "barista", "person", "people", "who", "manage", "david", "made by", "made for"]
        ),
        ["beans"] = new PageDescription(
            "Coffee Beans",
            "Manage coffee beans. View, add, edit, or delete bean varieties in your collection. See all the different coffee beans you've tried or are using.",
            ["beans", "coffee beans", "bean management", "coffee", "varieties"],
            ["bean", "coffee", "roast", "origin", "variety", "ethiopian", "colombian"]
        ),
        ["equipment"] = new PageDescription(
            "Equipment",
            "Manage espresso equipment. View, add, edit, or delete machines, grinders, tampers, and other gear.",
            ["equipment", "gear", "machines", "grinders", "tools", "equipment management"],
            ["equipment", "machine", "grinder", "tamper", "portafilter", "gear", "niche", "decent"]
        ),
        ["bean-detail"] = new PageDescription(
            "Bean Details",
            "View detailed information about a specific coffee bean including origin, roaster, and tasting notes.",
            ["bean detail", "bean info", "coffee detail"],
            ["bean", "detail", "info", "specific"]
        ),
        ["bag-detail"] = new PageDescription(
            "Bag Details",
            "View detailed information about a specific bag of coffee including roast date and shot history.",
            ["bag detail", "bag info"],
            ["bag", "detail", "roast date"]
        ),
        ["equipment-detail"] = new PageDescription(
            "Equipment Details",
            "View detailed information about a specific piece of equipment.",
            ["equipment detail", "gear detail"],
            ["equipment", "detail", "machine", "grinder"]
        ),
        ["profile-form"] = new PageDescription(
            "Profile Form",
            "Add or edit a user profile.",
            ["add profile", "edit profile", "new profile"],
            ["add", "edit", "new", "profile", "create"]
        )
    };

    public NavigationRegistry(ILogger<NavigationRegistry> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Discovers and registers routes from Shell and global route registry.
    /// Call this after Shell is initialized.
    /// </summary>
    public void DiscoverRoutes()
    {
        if (_isInitialized)
            return;

        _destinations.Clear();

        // Add all known routes from our descriptions dictionary
        // This includes both Shell routes and globally registered routes
        foreach (var kvp in _pageDescriptions)
        {
            var route = kvp.Key;
            var desc = kvp.Value;
            
            // Use absolute route format for Shell routes, relative for global routes
            var fullRoute = desc.IsShellRoute ? $"//{route}" : route;

            var destination = new NavigationDestination
            {
                Route = fullRoute,
                DisplayName = desc.DisplayName,
                Description = desc.Description,
                Aliases = desc.Aliases.ToList(),
                Keywords = desc.Keywords.ToList()
            };

            _destinations.Add(destination);
            _logger.LogDebug("Registered route: {Route} ({Title})", fullRoute, desc.DisplayName);
        }

        _isInitialized = true;
        _logger.LogInformation("Registered {Count} navigation routes", _destinations.Count);
    }

    public IReadOnlyList<NavigationDestination> GetDestinations()
    {
        if (!_isInitialized)
            DiscoverRoutes();
        return _destinations.AsReadOnly();
    }

    public NavigationDestination? FindDestination(string nameOrAlias)
    {
        if (!_isInitialized)
            DiscoverRoutes();

        if (string.IsNullOrWhiteSpace(nameOrAlias))
            return null;

        var normalized = nameOrAlias.ToLowerInvariant().Trim();

        // First try exact route match
        var byRoute = _destinations.FirstOrDefault(d =>
            d.Route.Equals(normalized, StringComparison.OrdinalIgnoreCase) ||
            d.Route.Equals($"//{normalized}", StringComparison.OrdinalIgnoreCase) ||
            d.Route.TrimStart('/').Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (byRoute != null) return byRoute;

        // Try display name
        var byName = _destinations.FirstOrDefault(d =>
            d.DisplayName.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (byName != null) return byName;

        // Try aliases (exact match)
        var byAlias = _destinations.FirstOrDefault(d =>
            d.Aliases.Any(a => a.Equals(normalized, StringComparison.OrdinalIgnoreCase)));
        if (byAlias != null) return byAlias;

        // Try partial match on aliases
        var byPartial = _destinations.FirstOrDefault(d =>
            d.Aliases.Any(a => a.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                              normalized.Contains(a, StringComparison.OrdinalIgnoreCase)));

        return byPartial;
    }

    public string GetDestinationsDescription()
    {
        if (!_isInitialized)
            DiscoverRoutes();

        var sb = new StringBuilder();
        sb.AppendLine("Available pages in the app:");
        sb.AppendLine();

        foreach (var dest in _destinations)
        {
            sb.AppendLine($"- **{dest.DisplayName}** (route: {dest.Route})");
            sb.AppendLine($"  {dest.Description}");
            sb.AppendLine($"  Voice aliases: {string.Join(", ", dest.Aliases.Take(5))}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private record PageDescription(
        string DisplayName,
        string Description, 
        string[] Aliases, 
        string[] Keywords,
        bool IsShellRoute = false);
}
