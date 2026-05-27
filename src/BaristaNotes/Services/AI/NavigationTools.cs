using System.ComponentModel;
using System.Text;
using Microsoft.Maui.AI.Attributes;
using Microsoft.Maui.ApplicationModel;

namespace BaristaNotes.Services.AI;

/// <summary>
/// AI navigation tool surface. Exposed as source-generated AI tools via
/// [ExportAIFunction] so an LLM (e.g. through VoiceCommandService) can
/// reason about and drive in-app navigation.
/// </summary>
public class NavigationTools
{
    private readonly INavigationRegistry _navigationRegistry;
    private readonly IBeanService _beanService;
    private readonly IBagService _bagService;
    private readonly ILogger<NavigationTools> _logger;

    public NavigationTools(
        INavigationRegistry navigationRegistry,
        IBeanService beanService,
        IBagService bagService,
        ILogger<NavigationTools> logger)
    {
        _navigationRegistry = navigationRegistry;
        _beanService = beanService;
        _bagService = bagService;
        _logger = logger;
    }

    [Description("Gets available pages in the app that can be navigated to. Call this to discover navigation options before navigating.")]
    [ExportAIFunction("get_available_pages")]
    public Task<string> GetAvailablePagesAsync()
    {
        _logger.LogInformation("GetAvailablePages tool called");

        try
        {
            var destinations = _navigationRegistry.GetDestinations();
            if (!destinations.Any())
            {
                return Task.FromResult("No pages discovered. The app may still be initializing.");
            }

            var sb = new StringBuilder();
            sb.AppendLine("Available pages:");
            foreach (var dest in destinations)
            {
                sb.AppendLine($"- {dest.DisplayName} (route: {dest.Route}): {dest.Description}");
            }

            return Task.FromResult(sb.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available pages");
            return Task.FromResult("Error discovering available pages.");
        }
    }

    [Description("Navigates to a specific page in the app. Use get_available_pages first if unsure what pages exist.")]
    [ExportAIFunction("navigate_to")]
    public Task<string> NavigateToAsync(
        [Description("Page name or alias to navigate to (e.g., 'activity', 'new shot', 'settings')")] string pageName)
    {
        _logger.LogInformation("NavigateTo tool called: {Page}", pageName);

        try
        {
            var destination = _navigationRegistry.FindDestination(pageName);
            if (destination == null)
            {
                var destinations = _navigationRegistry.GetDestinations();
                var availablePages = string.Join(", ", destinations.Select(d => d.DisplayName));
                return Task.FromResult($"Unknown page '{pageName}'. Available pages: {availablePages}. Use get_available_pages for more details.");
            }

            var route = destination.Route;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync(route);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error navigating to {Route}", route);
                }
            });

            _logger.LogInformation("Navigating via voice to: {Route} ({DisplayName})", route, destination.DisplayName);
            return Task.FromResult($"I've taken you to {destination.DisplayName}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating via voice");
            return Task.FromResult("Sorry, I couldn't navigate there. Please try again.");
        }
    }

    [Description("Navigate to a specific shot's detail page by shot ID. Use this after finding a shot with find_shots or get_last_shot when the user says 'show me' a specific shot.")]
    [ExportAIFunction("navigate_to_shot_detail")]
    public Task<string> NavigateToShotDetailAsync(
        [Description("The shot ID (integer) to navigate to")] int shotId)
    {
        _logger.LogInformation("NavigateToShotDetail tool called: {ShotId}", shotId);

        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync<ShotLoggingGridPageProps>(
                        "shot-logging",
                        props => props.ShotId = shotId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error navigating to shot detail {ShotId}", shotId);
                }
            });

            _logger.LogInformation("Navigating via voice to shot detail: {ShotId}", shotId);
            return Task.FromResult("I've opened the shot details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to shot detail via voice");
            return Task.FromResult("Sorry, I couldn't open that shot. Please try again.");
        }
    }

    [Description("Navigate to a specific profile's detail/edit page by profile ID. Use this after finding a profile with find_profiles when the user says 'show me' a specific profile.")]
    [ExportAIFunction("navigate_to_profile_detail")]
    public Task<string> NavigateToProfileDetailAsync(
        [Description("The profile ID (integer) to navigate to")] int profileId)
    {
        _logger.LogInformation("NavigateToProfileDetail tool called: {ProfileId}", profileId);

        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync<ProfileFormPageProps>(
                        "profile-form",
                        props => props.ProfileId = profileId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error navigating to profile detail {ProfileId}", profileId);
                }
            });

            _logger.LogInformation("Navigating via voice to profile detail: {ProfileId}", profileId);
            return Task.FromResult("I've opened the profile details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to profile detail via voice");
            return Task.FromResult("Sorry, I couldn't open that profile. Please try again.");
        }
    }

    [Description("Navigate to a specific bean's detail page by bean ID. Use this after finding a bean with find_beans when the user says 'show me' or 'open' a specific bean.")]
    [ExportAIFunction("navigate_to_bean_detail")]
    public Task<string> NavigateToBeanDetailAsync(
        [Description("The bean ID (integer) to navigate to")] int beanId)
    {
        _logger.LogInformation("NavigateToBeanDetail tool called: {BeanId}", beanId);

        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync<BeanDetailPageProps>(
                        "bean-detail",
                        props => props.BeanId = beanId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error navigating to bean detail {BeanId}", beanId);
                }
            });
            return Task.FromResult("I've opened the bean details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to bean detail via voice");
            return Task.FromResult("Sorry, I couldn't open that bean. Please try again.");
        }
    }

    [Description("Navigate to a specific equipment piece's detail page by equipment ID. Use this after finding equipment with find_equipment when the user says 'show me' or 'open' a specific piece.")]
    [ExportAIFunction("navigate_to_equipment_detail")]
    public Task<string> NavigateToEquipmentDetailAsync(
        [Description("The equipment ID (integer) to navigate to")] int equipmentId)
    {
        _logger.LogInformation("NavigateToEquipmentDetail tool called: {EquipmentId}", equipmentId);

        try
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync<EquipmentDetailPageProps>(
                        "equipment-detail",
                        props => props.EquipmentId = equipmentId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error navigating to equipment detail {EquipmentId}", equipmentId);
                }
            });
            return Task.FromResult("I've opened the equipment details.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to equipment detail via voice");
            return Task.FromResult("Sorry, I couldn't open that equipment. Please try again.");
        }
    }

    [Description("Navigate to a specific bag's detail page by bag ID. Use this after finding a bag with find_bags when the user wants to view a specific bag. The bean info is resolved automatically.")]
    [ExportAIFunction("navigate_to_bag_detail")]
    public async Task<string> NavigateToBagDetailAsync(
        [Description("The bag ID (integer) to navigate to")] int bagId)
    {
        _logger.LogInformation("NavigateToBagDetail tool called: {BagId}", bagId);

        try
        {
            var bag = await _bagService.GetBagByIdAsync(bagId);
            if (bag is null)
            {
                return $"I couldn't find a bag with ID {bagId}.";
            }

            var bean = await _beanService.GetBeanByIdAsync(bag.BeanId);
            var beanName = bean?.Name ?? string.Empty;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    await Microsoft.Maui.Controls.Shell.Current.GoToAsync<BagDetailPageProps>(
                        "bag-detail",
                        props =>
                        {
                            props.BagId = bagId;
                            props.BeanId = bag.BeanId;
                            props.BeanName = beanName;
                        });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error navigating to bag detail {BagId}", bagId);
                }
            });
            return string.IsNullOrEmpty(beanName)
                ? "I've opened the bag details."
                : $"I've opened the {beanName} bag details.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error navigating to bag detail via voice");
            return "Sorry, I couldn't open that bag. Please try again.";
        }
    }
}
