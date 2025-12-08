using Xunit;
using Moq;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;

namespace BaristaNotes.Tests.Unit.Services;

public class BeanServiceTests
{
    private readonly Mock<IBeanRepository> _mockRepository;
    private readonly Mock<IRatingService> _mockRatingService;
    private readonly BeanService _service;

    public BeanServiceTests()
    {
        _mockRepository = new Mock<IBeanRepository>();
        _mockRatingService = new Mock<IRatingService>();
        _service = new BeanService(_mockRepository.Object, _mockRatingService.Object);
    }

    [Fact]
    public async Task CreateBeanAsync_WithValidData_CreatesBean()
    {
        // Arrange
        var createDto = new CreateBeanDto
        {
            Name = "Ethiopia Yirgacheffe",
            Roaster = "Local Roasters",
            RoastDate = DateTime.Now.AddDays(-7),
            Origin = "Ethiopia",
            Notes = "Floral and citrus notes"
        };

        _mockRepository
            .Setup(r => r.AddAsync(It.IsAny<Bean>()))
            .ReturnsAsync((Bean b) => b);

        // Act
        var result = await _service.CreateBeanAsync(createDto);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Data);
        Assert.Equal(createDto.Name, result.Data.Name);
        Assert.Equal(createDto.Roaster, result.Data.Roaster);
        Assert.Equal(createDto.Origin, result.Data.Origin);
        Assert.True(result.Data.IsActive);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Bean>()), Times.Once);
    }

    [Fact]
    public async Task CreateBeanAsync_WithEmptyName_ThrowsValidationException()
    {
        // Arrange
        var createDto = new CreateBeanDto
        {
            Name = "",
            Roaster = "Test Roaster"
        };

        // Act
        var result = await _service.CreateBeanAsync(createDto);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Name is required", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateBeanAsync_WithNameTooLong_ThrowsValidationException()
    {
        // Arrange
        var createDto = new CreateBeanDto
        {
            Name = new string('A', 101),
            Roaster = "Test Roaster"
        };

        // Act
        var result = await _service.CreateBeanAsync(createDto);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Name must be 100 characters or less", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateBeanAsync_WithFutureRoastDate_ThrowsValidationException()
    {
        // Arrange
        var createDto = new CreateBeanDto
        {
            Name = "Future Bean",
            Roaster = "Test Roaster",
            RoastDate = DateTime.Now.AddDays(1)
        };

        // Act
        var result = await _service.CreateBeanAsync(createDto);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Roast date cannot be in the future", result.ErrorMessage);
    }

    [Fact]
    public async Task ArchiveBeanAsync_ExistingBean_SetsIsActiveFalse()
    {
        // Arrange
        var bean = new Bean
        {
            Id = 1,
            Name = "Old Bean",
            IsActive = true,
            SyncId = Guid.NewGuid(),
            CreatedAt = DateTime.Now,
            LastModifiedAt = DateTime.Now
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(bean);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Bean>()))
            .ReturnsAsync((Bean b) => b);

        // Act
        await _service.ArchiveBeanAsync(1);

        // Assert
        Assert.False(bean.IsActive);
        _mockRepository.Verify(r => r.UpdateAsync(It.Is<Bean>(b => !b.IsActive)), Times.Once);
    }

    [Fact]
    public async Task ArchiveBeanAsync_NonExistentBean_ThrowsEntityNotFoundException()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((Bean?)null);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => 
            _service.ArchiveBeanAsync(999));
    }

    [Fact]
    public async Task GetAllActiveBeansAsync_ReturnsOnlyActiveBeans()
    {
        // Arrange
        var beans = new List<Bean>
        {
            new Bean { Id = 1, Name = "Active Bean", IsActive = true, SyncId = Guid.NewGuid(), CreatedAt = DateTime.Now, LastModifiedAt = DateTime.Now },
            new Bean { Id = 2, Name = "Archived Bean", IsActive = false, SyncId = Guid.NewGuid(), CreatedAt = DateTime.Now, LastModifiedAt = DateTime.Now }
        };

        _mockRepository
            .Setup(r => r.GetActiveBeansAsync())
            .ReturnsAsync(beans.Where(b => b.IsActive).ToList());

        // Act
        var result = await _service.GetAllActiveBeansAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Active Bean", result.First().Name);
    }

    [Fact]
    public async Task UpdateBeanAsync_ExistingBean_UpdatesProperties()
    {
        // Arrange
        var bean = new Bean
        {
            Id = 1,
            Name = "Old Name",
            Roaster = "Old Roaster",
            IsActive = true,
            SyncId = Guid.NewGuid(),
            CreatedAt = DateTime.Now,
            LastModifiedAt = DateTime.Now
        };

        var updateDto = new UpdateBeanDto
        {
            Name = "New Name",
            Roaster = "New Roaster",
            Origin = "Colombia"
        };

        _mockRepository
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(bean);

        _mockRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Bean>()))
            .ReturnsAsync((Bean b) => b);

        // Act
        var result = await _service.UpdateBeanAsync(1, updateDto);

        // Assert
        Assert.Equal("New Name", result.Name);
        Assert.Equal("New Roaster", result.Roaster);
        Assert.Equal("Colombia", result.Origin);
    }
}
