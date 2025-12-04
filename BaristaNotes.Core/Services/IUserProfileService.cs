using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

public interface IUserProfileService
{
    Task<List<UserProfileDto>> GetAllProfilesAsync();
    Task<UserProfileDto?> GetProfileByIdAsync(int id);
    Task<UserProfileDto> CreateProfileAsync(CreateUserProfileDto dto);
    Task<UserProfileDto> UpdateProfileAsync(int id, UpdateUserProfileDto dto);
    Task DeleteProfileAsync(int id);
}
