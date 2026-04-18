using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

public interface IUserProfileService
{
    Task<List<UserProfileDto>> GetAllProfilesAsync();
    Task<UserProfileDto?> GetProfileByIdAsync(int id);
    Task<UserProfileDto> CreateProfileAsync(CreateUserProfileDto dto);
    Task<UserProfileDto> UpdateProfileAsync(int id, UpdateUserProfileDto dto);
    Task DeleteProfileAsync(int id);
    
    // Image management methods
    /// <summary>
    /// Updates profile avatar from image stream.
    /// Validates, saves image, updates database.
    /// </summary>
    /// <param name="profileId">Profile to update.</param>
    /// <param name="imageStream">Image stream from picker.</param>
    /// <returns>Result indicating success or specific failure.</returns>
    Task<ProfileImageUpdateResult> UpdateProfileImageAsync(int profileId, Stream imageStream);
    
    /// <summary>
    /// Removes avatar from profile and deletes image file.
    /// </summary>
    /// <param name="profileId">Profile to update.</param>
    /// <returns>True if removed, false if profile had no avatar.</returns>
    Task<bool> RemoveProfileImageAsync(int profileId);
    
    /// <summary>
    /// Gets full path to profile's avatar image, or null if no avatar.
    /// </summary>
    /// <param name="profileId">Profile to query.</param>
    /// <returns>Full path to image, or null if no avatar set.</returns>
    Task<string?> GetProfileImagePathAsync(int profileId);
}
