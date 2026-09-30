using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public class ProfileDraft
{
    public int? ProfileId { get; set; }
    public string Name { get; set; } = "";
    public string Context { get; set; } = "";
    public byte[]? StagedAvatarBytes { get; set; }
    public bool IsEditing => ProfileId is > 0;
    public string? ValidationError => string.IsNullOrWhiteSpace(Name) ? "Profile name is required" : null;

    public void ApplyLoadedData(UserProfileDto profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ProfileId = profile.Id;
        Name = profile.Name;
        Context = profile.Context ?? "";
    }
}

public sealed class ProfileWorkflow(
    IUserProfileService profiles,
    IDataChangeNotifier notifier,
    ILogger<ProfileWorkflow> logger)
{
    public async Task<UserProfileDto> SaveDetailsAsync(ProfileDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.ValidationError is { } error)
        {
            throw new ValidationException(new Dictionary<string, List<string>>
            {
                [nameof(ProfileDraft.Name)] = [error]
            });
        }

        UserProfileDto saved;
        if (draft.ProfileId is > 0)
        {
            saved = await profiles.UpdateProfileAsync(draft.ProfileId.Value,
                new UpdateUserProfileDto { Name = draft.Name, Context = draft.Context });
            notifier.NotifyDataChanged(DataChangeType.ProfileUpdated, draft.ProfileId.Value);
        }
        else
        {
            saved = await profiles.CreateProfileAsync(new CreateUserProfileDto
            {
                Name = draft.Name,
                Context = string.IsNullOrWhiteSpace(draft.Context) ? null : draft.Context
            });
            draft.ProfileId = saved.Id;
            notifier.NotifyDataChanged(DataChangeType.ProfileCreated, saved);
        }

        logger.LogInformation("Saved profile details for {ProfileId}", saved.Id);
        return saved;
    }

    public async Task<ProfileImageUpdateResult?> SaveStagedAvatarAsync(ProfileDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.StagedAvatarBytes is not { Length: > 0 } bytes)
            return null;
        if (draft.ProfileId is not > 0)
            throw new InvalidOperationException("Save the profile before saving a staged photo.");

        using var stream = new MemoryStream(bytes, writable: false);
        var result = await profiles.UpdateProfileImageAsync(draft.ProfileId.Value, stream);
        if (result.Success)
        {
            draft.StagedAvatarBytes = null;
            notifier.NotifyDataChanged(DataChangeType.ProfileUpdated, draft.ProfileId.Value);
            logger.LogInformation("Saved staged photo for profile {ProfileId}", draft.ProfileId.Value);
        }
        else
        {
            logger.LogWarning("Profile {ProfileId} saved, but its staged photo failed: {Error}",
                draft.ProfileId.Value, result.ErrorMessage);
        }
        return result;
    }

    public async Task DeleteAsync(int profileId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profileId);
        await profiles.DeleteProfileAsync(profileId);
        notifier.NotifyDataChanged(DataChangeType.ProfileUpdated, profileId);
        logger.LogInformation("Deleted profile {ProfileId}", profileId);
    }
}
