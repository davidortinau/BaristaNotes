using Xunit;
using Moq;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;

namespace BaristaNotes.Tests.Unit.Services;

public class UserProfileServiceTests
{
    private readonly Mock<IUserProfileRepository> _mockRepository;
    private readonly Mock<IImageProcessingService> _mockImageProcessing;
    private readonly UserProfileService _service;

    public UserProfileServiceTests()
    {
        _mockRepository = new Mock<IUserProfileRepository>();
        _mockImageProcessing = new Mock<IImageProcessingService>();
        _service = new UserProfileService(_mockRepository.Object, _mockImageProcessing.Object);
    }

    [Fact]
    public async Task CreateProfileAsync_WithValidData_CreatesProfile()
    {
        // Arrange
        var createDto = new CreateUserProfileDto
        {
            Name = "John Doe",
            AvatarPath = "/avatars/john.png"
        };

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<UserProfile>()))
            .ReturnsAsync((UserProfile p) => p);

        // Act
        var result = await _service.CreateProfileAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createDto.Name, result.Name);
        Assert.Equal(createDto.AvatarPath, result.AvatarPath);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<UserProfile>()), Times.Once);
    }

    [Fact]
    public async Task CreateProfileAsync_WithEmptyName_ThrowsValidationException()
    {
        // Arrange
        var createDto = new CreateUserProfileDto
        {
            Name = ""
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => 
            _service.CreateProfileAsync(createDto));
    }

    [Fact]
    public async Task CreateProfileAsync_WithNameTooLong_ThrowsValidationException()
    {
        // Arrange
        var createDto = new CreateUserProfileDto
        {
            Name = new string('A', 101)
        };

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => 
            _service.CreateProfileAsync(createDto));
    }

    [Fact]
    public async Task GetAllProfilesAsync_ReturnsAllProfiles()
    {
        // Arrange
        var profiles = new List<UserProfile>
        {
            new UserProfile { Id = 1, Name = "John", SyncId = Guid.NewGuid(), CreatedAt = DateTimeOffset.Now, LastModifiedAt = DateTimeOffset.Now },
            new UserProfile { Id = 2, Name = "Jane", SyncId = Guid.NewGuid(), CreatedAt = DateTimeOffset.Now, LastModifiedAt = DateTimeOffset.Now }
        };

        _mockRepository
            .Setup(r => r.GetActiveProfilesAsync())
            .ReturnsAsync(profiles);

        // Act
        var result = await _service.GetAllProfilesAsync();

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task UpdateProfileAsync_ExistingProfile_UpdatesProperties()
    {
        // Arrange
        var profile = new UserProfile
        {
            Id = 1,
            Name = "Old Name",
            SyncId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.Now,
            LastModifiedAt = DateTimeOffset.Now
        };

        var updateDto = new UpdateUserProfileDto
        {
            Name = "New Name",
            AvatarPath = "/avatars/new.png"
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(profile);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<UserProfile>()))
            .ReturnsAsync((UserProfile p) => p);

        // Act
        var result = await _service.UpdateProfileAsync(1, updateDto);

        // Assert
        Assert.Equal("New Name", result.Name);
        Assert.Equal("/avatars/new.png", result.AvatarPath);
    }

    [Fact]
    public async Task UpdateProfileAsync_NonExistentProfile_ThrowsEntityNotFoundException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((UserProfile?)null);

        var updateDto = new UpdateUserProfileDto
        {
            Name = "Test"
        };

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => 
            _service.UpdateProfileAsync(999, updateDto));
    }

    [Fact]
    public async Task DeleteProfileAsync_ExistingProfile_DeletesProfile()
    {
        // Arrange
        var profile = new UserProfile
        {
            Id = 1,
            Name = "John",
            SyncId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.Now,
            LastModifiedAt = DateTimeOffset.Now
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(profile);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<UserProfile>()))
            .ReturnsAsync((UserProfile p) => p);

        // Act
        await _service.DeleteProfileAsync(1);

        // Assert
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<UserProfile>(p => p.IsDeleted)), Times.Once);
    }
}
