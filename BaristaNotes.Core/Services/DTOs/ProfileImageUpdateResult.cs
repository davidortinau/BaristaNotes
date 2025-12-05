namespace BaristaNotes.Core.Services.DTOs;

public class ProfileImageUpdateResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? NewAvatarPath { get; set; }
    
    public static ProfileImageUpdateResult SuccessResult(string avatarPath) =>
        new() { Success = true, NewAvatarPath = avatarPath };
    
    public static ProfileImageUpdateResult FailureResult(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}
