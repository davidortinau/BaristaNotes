using System.ComponentModel;
using System.Text;
using BaristaNotes.Core.Services;
using Microsoft.Maui.AI.Attributes;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;

namespace BaristaNotes.Services.AI;

/// <summary>
/// AI tool surface for multimodal photo-driven recommendations:
/// - capture a photo of a person
/// - identify them against existing user profiles by face
/// - open a roaster URL for a recommended bean in the device browser
/// </summary>
public class PhotoQueryTools
{
    private readonly IUserProfileService _userProfileService;
    private readonly IBeanService _beanService;
    private readonly IVisionService? _visionService;
    private readonly ILogger<PhotoQueryTools> _logger;

    public PhotoQueryTools(
        IUserProfileService userProfileService,
        IBeanService beanService,
        ILogger<PhotoQueryTools> logger,
        IVisionService? visionService = null)
    {
        _userProfileService = userProfileService;
        _beanService = beanService;
        _visionService = visionService;
        _logger = logger;
    }

    [Description("Takes a photo with the device camera and identifies whether the person in it matches one of the saved user profiles. Returns the matched profile ID and name, or 'no match'. After a successful match, call get_profile_context to read their preferences before making a recommendation.")]
    [ExportAIFunction("identify_person_in_camera")]
    public async Task<string> IdentifyPersonInCameraAsync()
    {
        _logger.LogInformation("IdentifyPersonInCamera tool called");

        if (_visionService is null || !await _visionService.IsAvailableAsync())
            return "Vision service is not available. Check Azure OpenAI configuration.";

        if (!MediaPicker.Default.IsCaptureSupported)
            return "Camera is not available on this device.";

        FileResult? photo;
        try
        {
            photo = await MainThread.InvokeOnMainThreadAsync(async () =>
                await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
                {
                    Title = "Take a photo of the person",
                    MaximumWidth = 1024,
                    MaximumHeight = 1024,
                    CompressionQuality = 70
                }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Camera capture failed");
            return $"Camera capture failed: {ex.Message}";
        }

        if (photo is null)
            return "Photo capture was cancelled.";

        byte[] targetBytes;
        using (var ms = new MemoryStream())
        using (var s = await photo.OpenReadAsync())
        {
            await s.CopyToAsync(ms);
            targetBytes = ms.ToArray();
        }

        return await IdentifyFromBytesAsync(targetBytes);
    }

    [Description("Identifies whether the person in a photo at the given file path matches one of the saved user profiles. Use this when a photo has already been captured (test/scripted scenarios). Returns the matched profile ID and name, or 'no match'.")]
    [ExportAIFunction("identify_person_in_photo_file")]
    public async Task<string> IdentifyPersonInPhotoFileAsync(
        [Description("Absolute file path to the JPEG/PNG photo to match against profiles")] string photoPath)
    {
        _logger.LogInformation("IdentifyPersonInPhotoFile tool called: {Path}", photoPath);

        if (_visionService is null || !await _visionService.IsAvailableAsync())
            return "Vision service is not available. Check Azure OpenAI configuration.";

        if (string.IsNullOrWhiteSpace(photoPath) || !File.Exists(photoPath))
            return $"Photo not found at path: {photoPath}";

        try
        {
            var targetBytes = await File.ReadAllBytesAsync(photoPath);
            return await IdentifyFromBytesAsync(targetBytes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read photo at {Path}", photoPath);
            return $"Failed to read photo: {ex.Message}";
        }
    }

    private async Task<string> IdentifyFromBytesAsync(byte[] targetBytes)
    {
        var profiles = await _userProfileService.GetAllProfilesAsync();
        var withAvatars = profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.AvatarPath) && File.Exists(p.AvatarPath))
            .ToList();

        if (withAvatars.Count == 0)
            return "No user profiles with avatars to match against. Add a photo to a profile first.";

        var candidates = new List<PersonIdentificationCandidate>();
        foreach (var p in withAvatars)
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(p.AvatarPath!);
                candidates.Add(new PersonIdentificationCandidate
                {
                    ProfileId = p.Id,
                    Name = p.Name,
                    AvatarBytes = bytes
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping avatar for profile {Id} — read failed", p.Id);
            }
        }

        if (candidates.Count == 0)
            return "No readable profile avatars to match against.";

        var result = await _visionService!.IdentifyPersonFromPhotoAsync(targetBytes, candidates);
        if (!result.Success)
            return $"Could not identify: {result.ErrorMessage}";

        if (result.MatchedProfileId is null)
            return $"No confident match among the {candidates.Count} profile(s) with avatars. {result.Rationale}".Trim();

        return $"Matched profile ID:{result.MatchedProfileId} ({result.MatchedName}). {result.Rationale}".Trim();
    }

    [Description("Lists the active beans the user has on hand (with bean ID, name, roaster, and whether a roaster URL is set). Call before recommending a coffee so you don't recommend a bean the user does not have.")]
    [ExportAIFunction("list_available_beans")]
    public async Task<string> ListAvailableBeansAsync()
    {
        _logger.LogInformation("ListAvailableBeans tool called");

        try
        {
            var beans = await _beanService.GetAllActiveBeansAsync();
            if (beans.Count == 0)
                return "You have no active beans recorded.";

            var sb = new StringBuilder();
            sb.AppendLine($"Active beans ({beans.Count}):");
            foreach (var b in beans)
            {
                var url = string.IsNullOrWhiteSpace(b.RoasterUrl) ? "no URL" : "URL set";
                sb.Append($"- ID:{b.Id} {b.Name}");
                if (!string.IsNullOrWhiteSpace(b.Roaster)) sb.Append($" by {b.Roaster}");
                if (!string.IsNullOrWhiteSpace(b.Origin)) sb.Append($" ({b.Origin})");
                sb.AppendLine($" — {url}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing available beans");
            return "Sorry, I couldn't list your beans.";
        }
    }

    [Description("Opens the device browser to a bean's roaster URL so the user can reorder. Call after the user agrees to 'reorder' or 'order more'. Returns an error if the bean has no RoasterUrl set.")]
    [ExportAIFunction("open_roaster_url")]
    public async Task<string> OpenRoasterUrlAsync(
        [Description("The bean ID (integer) whose roaster URL to open")] int beanId)
    {
        _logger.LogInformation("OpenRoasterUrl tool called: {BeanId}", beanId);

        try
        {
            var bean = await _beanService.GetBeanByIdAsync(beanId);
            if (bean is null)
                return $"No bean found with ID {beanId}.";

            if (string.IsNullOrWhiteSpace(bean.RoasterUrl))
                return $"{bean.Name} has no roaster URL set. Add one on the bean's detail page first.";

            if (!Uri.TryCreate(bean.RoasterUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return $"{bean.Name} has an invalid roaster URL ({bean.RoasterUrl}). Update it to a full https:// URL.";
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
                await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred));

            return $"Opened the roaster page for {bean.Name}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening roaster URL for bean {BeanId}", beanId);
            return $"Sorry, I couldn't open the roaster page: {ex.Message}";
        }
    }
}
