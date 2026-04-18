using Moq;
using Xunit;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;

namespace BaristaNotes.Tests.Unit.Services;

public class UserProfileServiceImageTests
{
    [Fact]
    public async Task UpdateProfileImageAsync_ValidImage_UpdatesProfile()
    {
        // Arrange
        var mockImageProcessing = new Mock<IImageProcessingService>();
        mockImageProcessing
            .Setup(m => m.ValidateImageAsync(It.IsAny<Stream>()))
            .ReturnsAsync(ImageValidationResult.Valid);
        mockImageProcessing
            .Setup(m => m.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>()))
            .ReturnsAsync("/path/to/profile_avatar_1.jpg");
        
        var mockRepository = CreateMockRepository();
        var sut = CreateUserProfileService(mockRepository.Object, mockImageProcessing.Object);
        
        var imageStream = new MemoryStream(new byte[100]);
        
        // Act
        var result = await sut.UpdateProfileImageAsync(1, imageStream);
        
        // Assert
        Assert.True(result.Success);
        Assert.Equal("profile_avatar_1.jpg", result.NewAvatarPath);
    }
    
    [Fact]
    public async Task UpdateProfileImageAsync_InvalidImage_ReturnsFailure()
    {
        // Arrange
        var mockImageProcessing = new Mock<IImageProcessingService>();
        mockImageProcessing
            .Setup(m => m.ValidateImageAsync(It.IsAny<Stream>()))
            .ReturnsAsync(ImageValidationResult.TooLarge);
        
        var mockRepository = CreateMockRepository();
        var sut = CreateUserProfileService(mockRepository.Object, mockImageProcessing.Object);
        
        var imageStream = new MemoryStream(new byte[100]);
        
        // Act
        var result = await sut.UpdateProfileImageAsync(1, imageStream);
        
        // Assert
        Assert.False(result.Success);
        Assert.Contains("too large", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public async Task RemoveProfileImageAsync_ExistingAvatar_RemovesAndDeletesFile()
    {
        // Arrange
        var mockImageProcessing = new Mock<IImageProcessingService>();
        mockImageProcessing
            .Setup(m => m.DeleteImageAsync(It.IsAny<string>()))
            .ReturnsAsync(true);
        
        var mockRepository = CreateMockRepositoryWithAvatar("profile_avatar_1.jpg");
        var sut = CreateUserProfileService(mockRepository.Object, mockImageProcessing.Object);
        
        // Act
        var result = await sut.RemoveProfileImageAsync(1);
        
        // Assert
        Assert.True(result);
        mockImageProcessing.Verify(m => m.DeleteImageAsync("profile_avatar_1.jpg"), Times.Once);
    }
    
    private Mock<IUserProfileRepository> CreateMockRepository()
    {
        var mock = new Mock<IUserProfileRepository>();
        var profile = new UserProfile { Id = 1, Name = "Test", AvatarPath = null };
        mock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(profile);
        mock.Setup(r => r.UpdateAsync(It.IsAny<UserProfile>())).ReturnsAsync(profile);
        return mock;
    }
    
    private Mock<IUserProfileRepository> CreateMockRepositoryWithAvatar(string avatarPath)
    {
        var mock = new Mock<IUserProfileRepository>();
        var profile = new UserProfile { Id = 1, Name = "Test", AvatarPath = avatarPath };
        mock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(profile);
        mock.Setup(r => r.UpdateAsync(It.IsAny<UserProfile>())).ReturnsAsync(profile);
        return mock;
    }
    
    private IUserProfileService CreateUserProfileService(
        IUserProfileRepository repository,
        IImageProcessingService imageProcessing)
    {
        // Note: This is a simplified approach. The actual UserProfileService
        // constructor may require additional dependencies. We'll need to update
        // the actual implementation to accept IImageProcessingService.
        return new UserProfileService(repository, imageProcessing);
    }
}
