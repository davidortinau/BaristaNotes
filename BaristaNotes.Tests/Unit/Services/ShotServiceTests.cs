using Xunit;
using Moq;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using BaristaNotes.Core.Data.Repositories;

namespace BaristaNotes.Tests.Unit.Services;

public class ShotServiceTests
{
    private readonly Mock<IShotRecordRepository> _mockShotRepository;
    private readonly Mock<IPreferencesService> _mockPreferences;
    private readonly ShotService _service;
    
    public ShotServiceTests()
    {
        _mockShotRepository = new Mock<IShotRecordRepository>();
        _mockPreferences = new Mock<IPreferencesService>();
        _service = new ShotService(_mockShotRepository.Object, _mockPreferences.Object);
    }
    
    [Theory]
    [InlineData(4.9, "Dose must be between 5 and 30 grams")] // Below minimum
    [InlineData(30.1, "Dose must be between 5 and 30 grams")] // Above maximum
    public async Task CreateShotAsync_InvalidDose_ThrowsValidationException(decimal doseIn, string expectedError)
    {
        // Arrange
        var dto = new CreateShotDto
        {
            DoseIn = doseIn,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso"
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateShotAsync(dto));
        Assert.Contains(expectedError, exception.Errors[nameof(dto.DoseIn)][0]);
    }
    
    [Theory]
    [InlineData(9, "Expected time must be between 10 and 60 seconds")] // Below minimum
    [InlineData(61, "Expected time must be between 10 and 60 seconds")] // Above maximum
    public async Task CreateShotAsync_InvalidExpectedTime_ThrowsValidationException(decimal expectedTime, string expectedError)
    {
        // Arrange
        var dto = new CreateShotDto
        {
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = expectedTime,
            ExpectedOutput = 40,
            DrinkType = "Espresso"
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateShotAsync(dto));
        Assert.Contains(expectedError, exception.Errors[nameof(dto.ExpectedTime)][0]);
    }
    
    [Theory]
    [InlineData(9, "Expected output must be between 10 and 100 grams")] // Below minimum
    [InlineData(101, "Expected output must be between 10 and 100 grams")] // Above maximum
    public async Task CreateShotAsync_InvalidExpectedOutput_ThrowsValidationException(decimal expectedOutput, string expectedError)
    {
        // Arrange
        var dto = new CreateShotDto
        {
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = expectedOutput,
            DrinkType = "Espresso"
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateShotAsync(dto));
        Assert.Contains(expectedError, exception.Errors[nameof(dto.ExpectedOutput)][0]);
    }
    
    [Fact]
    public async Task CreateShotAsync_MissingGrindSetting_ThrowsValidationException()
    {
        // Arrange
        var dto = new CreateShotDto
        {
            DoseIn = 18,
            GrindSetting = "",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso"
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateShotAsync(dto));
        Assert.Contains("Grind setting is required", exception.Errors[nameof(dto.GrindSetting)][0]);
    }
    
    [Fact]
    public async Task CreateShotAsync_MissingDrinkType_ThrowsValidationException()
    {
        // Arrange
        var dto = new CreateShotDto
        {
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = ""
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateShotAsync(dto));
        Assert.Contains("Drink type is required", exception.Errors[nameof(dto.DrinkType)][0]);
    }
    
    [Theory]
    [InlineData(0, "Rating must be between 1 and 5")]
    [InlineData(6, "Rating must be between 1 and 5")]
    public async Task CreateShotAsync_InvalidRating_ThrowsValidationException(int rating, string expectedError)
    {
        // Arrange
        var dto = new CreateShotDto
        {
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            Rating = rating
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateShotAsync(dto));
        Assert.Contains(expectedError, exception.Errors[nameof(dto.Rating)][0]);
    }
    
    [Fact]
    public async Task CreateShotAsync_ValidData_SavesPreferences()
    {
        // Arrange
        var dto = new CreateShotDto
        {
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Latte",
            BeanId = 1,
            MachineId = 2,
            GrinderId = 3,
            AccessoryIds = new List<int> { 4, 5 },
            MadeById = 6,
            MadeForId = 7
        };
        
        var savedShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            DoseIn = dto.DoseIn,
            GrindSetting = dto.GrindSetting,
            ExpectedTime = dto.ExpectedTime,
            ExpectedOutput = dto.ExpectedOutput,
            DrinkType = dto.DrinkType,
            Timestamp = DateTimeOffset.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now,
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.AddAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync((BaristaNotes.Core.Models.ShotRecord shot) => 
            {
                shot.Id = savedShot.Id;
                return shot;
            });
        _mockShotRepository.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(savedShot);
        _mockShotRepository.Setup(r => r.UpdateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync((BaristaNotes.Core.Models.ShotRecord shot) => shot);
        
        // Act
        await _service.CreateShotAsync(dto);
        
        // Assert
        _mockPreferences.Verify(p => p.SetLastDrinkType("Latte"), Times.Once);
        _mockPreferences.Verify(p => p.SetLastBeanId(1), Times.Once);
        _mockPreferences.Verify(p => p.SetLastMachineId(2), Times.Once);
        _mockPreferences.Verify(p => p.SetLastGrinderId(3), Times.Once);
        _mockPreferences.Verify(p => p.SetLastAccessoryIds(It.Is<List<int>>(l => l.Count == 2)), Times.Once);
        _mockPreferences.Verify(p => p.SetLastMadeById(6), Times.Once);
        _mockPreferences.Verify(p => p.SetLastMadeForId(7), Times.Once);
    }
    
    // ===== UpdateShotAsync Tests =====
    
    [Fact]
    public async Task UpdateShotAsync_ValidDto_UpdatesAllFields()
    {
        // Arrange
        var existingShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            DrinkType = "Espresso",
            ActualTime = 25,
            ActualOutput = 38,
            Rating = 3,
            Timestamp = DateTimeOffset.Now.AddHours(-1),
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now.AddHours(-1),
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingShot);
        _mockShotRepository.Setup(r => r.UpdateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync((BaristaNotes.Core.Models.ShotRecord shot) => shot);
        
        var dto = new UpdateShotDto
        {
            ActualTime = 28.5m,
            ActualOutput = 42.0m,
            Rating = 4,
            DrinkType = "Latte"
        };
        
        // Act
        var result = await _service.UpdateShotAsync(1, dto);
        
        // Assert
        Assert.Equal(28.5m, existingShot.ActualTime);
        Assert.Equal(42.0m, existingShot.ActualOutput);
        Assert.Equal(4, existingShot.Rating);
        Assert.Equal("Latte", existingShot.DrinkType);
        Assert.True(existingShot.LastModifiedAt > DateTimeOffset.Now.AddMinutes(-1));
        _mockShotRepository.Verify(r => r.UpdateAsync(existingShot), Times.Once);
    }
    
    [Fact]
    public async Task UpdateShotAsync_PartialDto_UpdatesOnlyProvidedFields()
    {
        // Arrange
        var existingShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            DrinkType = "Espresso",
            ActualTime = 25,
            ActualOutput = 38,
            Rating = 3,
            Timestamp = DateTimeOffset.Now.AddHours(-1),
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now.AddHours(-1),
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingShot);
        _mockShotRepository.Setup(r => r.UpdateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync((BaristaNotes.Core.Models.ShotRecord shot) => shot);
        
        var dto = new UpdateShotDto
        {
            Rating = 5,
            DrinkType = "Espresso" // Required field
        };
        
        // Act
        var result = await _service.UpdateShotAsync(1, dto);
        
        // Assert
        Assert.Equal(25, existingShot.ActualTime); // Unchanged
        Assert.Equal(38, existingShot.ActualOutput); // Unchanged
        Assert.Equal(5, existingShot.Rating); // Changed
        Assert.Equal("Espresso", existingShot.DrinkType);
    }
    
    [Fact]
    public async Task UpdateShotAsync_InvalidId_ThrowsEntityNotFoundException()
    {
        // Arrange
        _mockShotRepository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((BaristaNotes.Core.Models.ShotRecord?)null);
        
        var dto = new UpdateShotDto { DrinkType = "Espresso" };
        
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _service.UpdateShotAsync(999, dto));
    }
    
    [Theory]
    [InlineData(0, "Shot time must be between 0 and 999 seconds")]
    [InlineData(1000, "Shot time must be between 0 and 999 seconds")]
    public async Task UpdateShotAsync_InvalidActualTime_ThrowsValidationException(decimal actualTime, string expectedError)
    {
        // Arrange
        var dto = new UpdateShotDto
        {
            ActualTime = actualTime,
            DrinkType = "Espresso"
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateShotAsync(1, dto));
        Assert.Contains(expectedError, exception.Errors[nameof(dto.ActualTime)][0]);
    }
    
    [Theory]
    [InlineData(0, "Output weight must be between 0 and 200 grams")]
    [InlineData(201, "Output weight must be between 0 and 200 grams")]
    public async Task UpdateShotAsync_InvalidActualOutput_ThrowsValidationException(decimal actualOutput, string expectedError)
    {
        // Arrange
        var dto = new UpdateShotDto
        {
            ActualOutput = actualOutput,
            DrinkType = "Espresso"
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateShotAsync(1, dto));
        Assert.Contains(expectedError, exception.Errors[nameof(dto.ActualOutput)][0]);
    }
    
    [Theory]
    [InlineData(0, "Rating must be between 1 and 5 stars")]
    [InlineData(6, "Rating must be between 1 and 5 stars")]
    public async Task UpdateShotAsync_InvalidRating_ThrowsValidationException(int rating, string expectedError)
    {
        // Arrange
        var dto = new UpdateShotDto
        {
            Rating = rating,
            DrinkType = "Espresso"
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateShotAsync(1, dto));
        Assert.Contains(expectedError, exception.Errors[nameof(dto.Rating)][0]);
    }
    
    [Fact]
    public async Task UpdateShotAsync_EmptyDrinkType_ThrowsValidationException()
    {
        // Arrange
        var dto = new UpdateShotDto
        {
            DrinkType = ""
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateShotAsync(1, dto));
        Assert.Contains("Drink type is required", exception.Errors[nameof(dto.DrinkType)][0]);
    }
    
    [Fact]
    public async Task UpdateShotAsync_UpdatesLastModifiedAtTimestamp()
    {
        // Arrange
        var existingShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            DrinkType = "Espresso",
            ActualTime = 25,
            IsDeleted = false,
            LastModifiedAt = DateTimeOffset.Now.AddHours(-1),
            SyncId = Guid.NewGuid(),
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingShot);
        _mockShotRepository.Setup(r => r.UpdateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync((BaristaNotes.Core.Models.ShotRecord shot) => shot);
        
        var dto = new UpdateShotDto { DrinkType = "Latte" };
        var beforeUpdate = DateTimeOffset.Now;
        
        // Act
        await _service.UpdateShotAsync(1, dto);
        
        // Assert
        Assert.True(existingShot.LastModifiedAt >= beforeUpdate);
    }
    
    [Fact]
    public async Task UpdateShotAsync_PreservesImmutableFields()
    {
        // Arrange
        var originalTimestamp = DateTimeOffset.Now.AddHours(-2);
        var existingShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            Timestamp = originalTimestamp,
            BeanId = 5,
            GrindSetting = "10",
            DoseIn = 18,
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now.AddHours(-1),
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingShot);
        _mockShotRepository.Setup(r => r.UpdateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync((BaristaNotes.Core.Models.ShotRecord shot) => shot);
        
        var dto = new UpdateShotDto
        {
            ActualTime = 28.5m,
            DrinkType = "Latte"
        };
        
        // Act
        await _service.UpdateShotAsync(1, dto);
        
        // Assert - Immutable fields unchanged
        Assert.Equal(originalTimestamp, existingShot.Timestamp);
        Assert.Equal(5, existingShot.BeanId);
        Assert.Equal("10", existingShot.GrindSetting);
        Assert.Equal(18, existingShot.DoseIn);
        Assert.Equal(30, existingShot.ExpectedTime);
        Assert.Equal(40, existingShot.ExpectedOutput);
    }
    
    [Fact]
    public async Task UpdateShotAsync_DeletedShot_ThrowsEntityNotFoundException()
    {
        // Arrange
        var deletedShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            IsDeleted = true,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(deletedShot);
        
        var dto = new UpdateShotDto { DrinkType = "Latte" };
        
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _service.UpdateShotAsync(1, dto));
    }
    
    // ===== DeleteShotAsync Tests =====
    
    [Fact]
    public async Task DeleteShotAsync_ValidId_SoftDeletesShot()
    {
        // Arrange
        var existingShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            IsDeleted = false,
            DrinkType = "Espresso",
            LastModifiedAt = DateTimeOffset.Now.AddHours(-1),
            SyncId = Guid.NewGuid(),
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingShot);
        _mockShotRepository.Setup(r => r.UpdateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync((BaristaNotes.Core.Models.ShotRecord shot) => shot);
        
        // Act
        await _service.DeleteShotAsync(1);
        
        // Assert
        Assert.True(existingShot.IsDeleted);
        _mockShotRepository.Verify(r => r.UpdateAsync(existingShot), Times.Once);
    }
    
    [Fact]
    public async Task DeleteShotAsync_InvalidId_ThrowsEntityNotFoundException()
    {
        // Arrange
        _mockShotRepository.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((BaristaNotes.Core.Models.ShotRecord?)null);
        
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _service.DeleteShotAsync(999));
    }
    
    [Fact]
    public async Task DeleteShotAsync_UpdatesLastModifiedAtTimestamp()
    {
        // Arrange
        var existingShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            IsDeleted = false,
            DrinkType = "Espresso",
            LastModifiedAt = DateTimeOffset.Now.AddHours(-1),
            SyncId = Guid.NewGuid(),
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingShot);
        _mockShotRepository.Setup(r => r.UpdateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync((BaristaNotes.Core.Models.ShotRecord shot) => shot);
        
        var beforeDelete = DateTimeOffset.Now;
        
        // Act
        await _service.DeleteShotAsync(1);
        
        // Assert
        Assert.True(existingShot.LastModifiedAt >= beforeDelete);
    }
    
    [Fact]
    public async Task DeleteShotAsync_AlreadyDeleted_ThrowsEntityNotFoundException()
    {
        // Arrange
        var deletedShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            IsDeleted = true,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(deletedShot);
        
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _service.DeleteShotAsync(1));
    }

    #region Bag Validation Tests (T032 - Phase 4)

    [Fact]
    public async Task CreateShotAsync_WithNonExistentBag_ThrowsValidationException()
    {
        // Arrange
        var mockBagRepo = new Mock<IBagRepository>();
        mockBagRepo.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((BaristaNotes.Core.Models.Bag?)null);
        
        var serviceWithBagRepo = new ShotService(
            _mockShotRepository.Object,
            _mockPreferences.Object,
            mockBagRepo.Object
        );

        var dto = new CreateShotDto
        {
            BagId = 999, // Non-existent bag
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => serviceWithBagRepo.CreateShotAsync(dto));
        Assert.Contains("Bag", exception.Errors["BagId"][0]);
    }

    [Fact]
    public async Task CreateShotAsync_WithCompletedBag_ThrowsValidationException()
    {
        // Arrange
        var mockBagRepo = new Mock<IBagRepository>();
        var completedBag = new BaristaNotes.Core.Models.Bag
        {
            Id = 1,
            BeanId = 1,
            RoastDate = DateTimeOffset.Now.AddDays(-10),
            IsComplete = true // Bag is marked as complete
        };
        mockBagRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(completedBag);
        
        var serviceWithBagRepo = new ShotService(
            _mockShotRepository.Object,
            _mockPreferences.Object,
            mockBagRepo.Object
        );

        var dto = new CreateShotDto
        {
            BagId = 1,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(() => serviceWithBagRepo.CreateShotAsync(dto));
        Assert.Contains("complete", exception.Errors["BagId"][0].ToLower());
    }

    [Fact]
    public async Task CreateShotAsync_WithActiveBag_Success()
    {
        // Arrange
        var mockBagRepo = new Mock<IBagRepository>();
        var activeBag = new BaristaNotes.Core.Models.Bag
        {
            Id = 1,
            BeanId = 1,
            RoastDate = DateTimeOffset.Now.AddDays(-10),
            IsComplete = false // Active bag
        };
        mockBagRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(activeBag);
        
        var serviceWithBagRepo = new ShotService(
            _mockShotRepository.Object,
            _mockPreferences.Object,
            mockBagRepo.Object
        );

        var dto = new CreateShotDto
        {
            BagId = 1,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso"
        };

        var expectedShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            BagId = 1,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid()
        };

        _mockShotRepository.Setup(r => r.CreateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync(expectedShot);

        // Act
        var result = await serviceWithBagRepo.CreateShotAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.BagId);
        _mockShotRepository.Verify(r => r.CreateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()), Times.Once);
    }

    #endregion
}
