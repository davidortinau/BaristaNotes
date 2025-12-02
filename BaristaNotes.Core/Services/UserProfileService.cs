using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;

namespace BaristaNotes.Core.Services;

public class UserProfileService : IUserProfileService
{
    private readonly IUserProfileRepository _profileRepository;
    
    public UserProfileService(IUserProfileRepository profileRepository)
    {
        _profileRepository = profileRepository;
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
        
        profile.IsDeleted = true;
        profile.LastModifiedAt = DateTimeOffset.Now;
        await _profileRepository.UpdateAsync(profile);
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
}
