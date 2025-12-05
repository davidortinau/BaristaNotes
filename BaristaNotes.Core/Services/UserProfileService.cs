using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;

namespace BaristaNotes.Core.Services;

public class UserProfileService : IUserProfileService
{
    private readonly IUserProfileRepository _profileRepository;
    private readonly IImageProcessingService _imageProcessingService;
    
    public UserProfileService(
        IUserProfileRepository profileRepository,
        IImageProcessingService imageProcessingService)
    {
        _profileRepository = profileRepository;
        _imageProcessingService = imageProcessingService;
    }
    
    public async Task<List<UserProfileDto>> GetAllProfilesAsync()
    {
        var profiles = await _profileRepository.GetActiveProfilesAsync();
        return profiles.Select(MapToDto).ToList();
    }
    
    public async Task<UserProfileDto?> GetProfileByIdAsync(int id)
    {
        var profile = await _profileRepository.GetByIdAsync(id);
        return profile == null ? null : MapToDto(profile);
    }
    
    public async Task<UserProfileDto> CreateProfileAsync(CreateUserProfileDto dto)
    {
        ValidateCreateProfile(dto);
        
        var profile = new UserProfile
        {
            Name = dto.Name,
            AvatarPath = dto.AvatarPath,
            CreatedAt = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        var created = await _profileRepository.AddAsync(profile);
        return MapToDto(created);
    }
    
    public async Task<UserProfileDto> UpdateProfileAsync(int id, UpdateUserProfileDto dto)
    {
        var profile = await _profileRepository.GetByIdAsync(id);
        if (profile == null)
            throw new EntityNotFoundException(nameof(UserProfile), id);
        
        if (dto.Name != null)
            profile.Name = dto.Name;
        if (dto.AvatarPath != null)
            profile.AvatarPath = dto.AvatarPath;
        
        profile.LastModifiedAt = DateTimeOffset.Now;
        
        var updated = await _profileRepository.UpdateAsync(profile);
        return MapToDto(updated);
    }
    
    public async Task DeleteProfileAsync(int id)
    {
        var profile = await _profileRepository.GetByIdAsync(id);
        if (profile == null)
            throw new EntityNotFoundException(nameof(UserProfile), id);
        
        // Delete avatar file if exists
        if (!string.IsNullOrEmpty(profile.AvatarPath))
        {
            await _imageProcessingService.DeleteImageAsync(profile.AvatarPath);
        }
        
        profile.IsDeleted = true;
        profile.LastModifiedAt = DateTimeOffset.Now;
        await _profileRepository.UpdateAsync(profile);
    }
    
    // Image management methods
    public async Task<ProfileImageUpdateResult> UpdateProfileImageAsync(int profileId, Stream imageStream)
    {
        try
        {
            // Validate image
            var validationResult = await _imageProcessingService.ValidateImageAsync(imageStream);
            if (validationResult != ImageValidationResult.Valid)
            {
                return ProfileImageUpdateResult.FailureResult(GetValidationErrorMessage(validationResult));
            }
            
            // Load profile
            var profile = await _profileRepository.GetByIdAsync(profileId);
            if (profile == null)
            {
                return ProfileImageUpdateResult.FailureResult("Profile not found");
            }
            
            // Generate filename
            var filename = $"profile_avatar_{profileId}.jpg";
            
            // Delete old image if exists
            if (!string.IsNullOrEmpty(profile.AvatarPath))
            {
                await _imageProcessingService.DeleteImageAsync(profile.AvatarPath);
            }
            
            // Save new image
            await _imageProcessingService.SaveImageAsync(imageStream, filename);
            
            // Update profile
            profile.AvatarPath = filename;
            profile.LastModifiedAt = DateTimeOffset.UtcNow;
            
            await _profileRepository.UpdateAsync(profile);
            
            return ProfileImageUpdateResult.SuccessResult(filename);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating profile image: {ex.Message}");
            return ProfileImageUpdateResult.FailureResult("Failed to save image");
        }
    }
    
    public async Task<bool> RemoveProfileImageAsync(int profileId)
    {
        var profile = await _profileRepository.GetByIdAsync(profileId);
        if (profile == null || string.IsNullOrEmpty(profile.AvatarPath))
        {
            return false;
        }
        
        await _imageProcessingService.DeleteImageAsync(profile.AvatarPath);
        
        profile.AvatarPath = null;
        profile.LastModifiedAt = DateTimeOffset.UtcNow;
        
        await _profileRepository.UpdateAsync(profile);
        
        return true;
    }
    
    public async Task<string?> GetProfileImagePathAsync(int profileId)
    {
        var profile = await _profileRepository.GetByIdAsync(profileId);
        if (profile == null || string.IsNullOrEmpty(profile.AvatarPath))
        {
            return null;
        }
        
        var path = _imageProcessingService.GetImagePath(profile.AvatarPath);
        return _imageProcessingService.ImageExists(profile.AvatarPath) ? path : null;
    }
    
    private UserProfileDto MapToDto(UserProfile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        AvatarPath = profile.AvatarPath,
        CreatedAt = profile.CreatedAt
    };
    
    private void ValidateCreateProfile(CreateUserProfileDto dto)
    {
        var errors = new Dictionary<string, List<string>>();
        
        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add(nameof(dto.Name), new List<string> { "Name is required" });
        else if (dto.Name.Length > 50)
            errors.Add(nameof(dto.Name), new List<string> { "Name must be 50 characters or less" });
        
        if (dto.AvatarPath?.Length > 500)
            errors.Add(nameof(dto.AvatarPath), new List<string> { "Avatar path must be 500 characters or less" });
        
        if (errors.Any())
            throw new ValidationException(errors);
    }
    
    private string GetValidationErrorMessage(ImageValidationResult result)
    {
        return result switch
        {
            ImageValidationResult.TooLarge => "Image is too large (max 1MB)",
            ImageValidationResult.DimensionsTooLarge => "Image dimensions too large (max 400x400)",
            ImageValidationResult.DimensionsTooSmall => "Image dimensions too small (min 100x100)",
            ImageValidationResult.InvalidFormat => "Invalid image format",
            _ => "Image processing failed"
        };
    }
}
