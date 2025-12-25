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
    private readonly Mock<IBagRepository> _mockBagRepository;
    private readonly ShotService _service;
    
    public ShotServiceTests()
    {
        _mockShotRepository = new Mock<IShotRecordRepository>();
        _mockPreferences = new Mock<IPreferencesService>();
        _mockBagRepository = new Mock<IBagRepository>();
        _service = new ShotService(_mockShotRepository.Object, _mockPreferences.Object, _mockBagRepository.Object);
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
            BagId = 1,
            MachineId = 2,
            GrinderId = 3,
            AccessoryIds = new List<int> { 4, 5 },
            MadeById = 6,
            MadeForId = 7
        };
        
        var activeBag = new BaristaNotes.Core.Models.Bag
        {
            Id = 1,
            BeanId = 1,
            RoastDate = DateTime.Now.AddDays(-7),
            IsComplete = false,
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var savedShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            DoseIn = dto.DoseIn,
            GrindSetting = dto.GrindSetting,
            ExpectedTime = dto.ExpectedTime,
            ExpectedOutput = dto.ExpectedOutput,
            DrinkType = dto.DrinkType,
            BagId = 1,
            Timestamp = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now,
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockBagRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(activeBag);
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
        _mockPreferences.Verify(p => p.SetLastBagId(1), Times.Once);
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
            Timestamp = DateTime.Now.AddHours(-1),
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now.AddHours(-1),
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
        Assert.True(existingShot.LastModifiedAt > DateTime.Now.AddMinutes(-1));
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
            Timestamp = DateTime.Now.AddHours(-1),
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now.AddHours(-1),
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
            LastModifiedAt = DateTime.Now.AddHours(-1),
            SyncId = Guid.NewGuid(),
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingShot);
        _mockShotRepository.Setup(r => r.UpdateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync((BaristaNotes.Core.Models.ShotRecord shot) => shot);
        
        var dto = new UpdateShotDto { DrinkType = "Latte" };
        var beforeUpdate = DateTime.Now;
        
        // Act
        await _service.UpdateShotAsync(1, dto);
        
        // Assert
        Assert.True(existingShot.LastModifiedAt >= beforeUpdate);
    }
    
    [Fact]
    public async Task UpdateShotAsync_PreservesImmutableFields()
    {
        // Arrange
        var originalTimestamp = DateTime.Now.AddHours(-2);
        var existingShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            Timestamp = originalTimestamp,
            BagId = 5,
            GrindSetting = "10",
            DoseIn = 18,
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now.AddHours(-1),
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
        Assert.Equal(5, existingShot.BagId);
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
            LastModifiedAt = DateTime.Now.AddHours(-1),
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
            LastModifiedAt = DateTime.Now.AddHours(-1),
            SyncId = Guid.NewGuid(),
            ShotEquipments = new List<BaristaNotes.Core.Models.ShotEquipment>()
        };
        
        _mockShotRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingShot);
        _mockShotRepository.Setup(r => r.UpdateAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync((BaristaNotes.Core.Models.ShotRecord shot) => shot);
        
        var beforeDelete = DateTime.Now;
        
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
            RoastDate = DateTime.Now.AddDays(-10),
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
            RoastDate = DateTime.Now.AddDays(-10),
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
            Bag = activeBag,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid()
        };

        _mockShotRepository.Setup(r => r.AddAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()))
            .ReturnsAsync(expectedShot);
        _mockShotRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(expectedShot);

        // Act
        var result = await serviceWithBagRepo.CreateShotAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bag);
        Assert.Equal(1, result.Bag.Id);
        _mockShotRepository.Verify(r => r.AddAsync(It.IsAny<BaristaNotes.Core.Models.ShotRecord>()), Times.Once);
    }

    #endregion

    #region AI Bean Recommendations Tests (T011-T014, T022-T023)

    [Fact]
    public async Task BeanHasHistoryAsync_NewBeanWithNoShots_ReturnsFalse()
    {
        // Arrange - T011
        var beanId = 99;
        _mockShotRepository.Setup(r => r.GetAllAsync())
            .ReturnsAsync(new List<BaristaNotes.Core.Models.ShotRecord>());

        // Act
        var result = await _service.BeanHasHistoryAsync(beanId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task BeanHasHistoryAsync_BeanWithShots_ReturnsTrue()
    {
        // Arrange
        var beanId = 1;
        var bag = new BaristaNotes.Core.Models.Bag { Id = 1, BeanId = beanId };
        var shots = new List<BaristaNotes.Core.Models.ShotRecord>
        {
            new() { Id = 1, BagId = 1, Bag = bag, IsDeleted = false, DoseIn = 18, GrindSetting = "5", DrinkType = "Espresso", SyncId = Guid.NewGuid() }
        };
        _mockShotRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(shots);

        // Act
        var result = await _service.BeanHasHistoryAsync(beanId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task BeanHasHistoryAsync_BeanWithOnlyDeletedShots_ReturnsFalse()
    {
        // Arrange
        var beanId = 1;
        var bag = new BaristaNotes.Core.Models.Bag { Id = 1, BeanId = beanId };
        var shots = new List<BaristaNotes.Core.Models.ShotRecord>
        {
            new() { Id = 1, BagId = 1, Bag = bag, IsDeleted = true, DoseIn = 18, GrindSetting = "5", DrinkType = "Espresso", SyncId = Guid.NewGuid() }
        };
        _mockShotRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(shots);

        // Act
        var result = await _service.BeanHasHistoryAsync(beanId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetMostRecentBeanIdAsync_WithShots_ReturnsCorrectBeanId()
    {
        // Arrange - T022
        var expectedBeanId = 42;
        var bag = new BaristaNotes.Core.Models.Bag { Id = 1, BeanId = expectedBeanId };
        var mostRecentShot = new BaristaNotes.Core.Models.ShotRecord
        {
            Id = 1,
            BagId = 1,
            Bag = bag,
            Timestamp = DateTime.Now,
            DoseIn = 18,
            GrindSetting = "5",
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid()
        };
        _mockShotRepository.Setup(r => r.GetMostRecentAsync()).ReturnsAsync(mostRecentShot);

        // Act
        var result = await _service.GetMostRecentBeanIdAsync();

        // Assert
        Assert.Equal(expectedBeanId, result);
    }

    [Fact]
    public async Task GetMostRecentBeanIdAsync_NoShots_ReturnsNull()
    {
        // Arrange
        _mockShotRepository.Setup(r => r.GetMostRecentAsync()).ReturnsAsync((BaristaNotes.Core.Models.ShotRecord?)null);

        // Act
        var result = await _service.GetMostRecentBeanIdAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetBeanRecommendationContextAsync_NewBeanWithoutHistory_ReturnsContextWithHasHistoryFalse()
    {
        // Arrange - T012
        var beanId = 1;
        var bean = new BaristaNotes.Core.Models.Bean 
        { 
            Id = beanId, 
            Name = "Test Bean", 
            Roaster = "Test Roaster",
            Origin = "Ethiopia",
            Notes = "Fruity"
        };
        var bag = new BaristaNotes.Core.Models.Bag 
        { 
            Id = 1, 
            BeanId = beanId, 
            Bean = bean,
            RoastDate = DateTime.Now.AddDays(-7),
            IsDeleted = false
        };
        
        _mockShotRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<BaristaNotes.Core.Models.ShotRecord>());
        _mockBagRepository.Setup(r => r.GetBagsForBeanAsync(beanId, true)).ReturnsAsync(new List<BaristaNotes.Core.Models.Bag> { bag });

        // Act
        var result = await _service.GetBeanRecommendationContextAsync(beanId);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.HasHistory);
        Assert.Null(result.HistoricalShots);
        Assert.Equal("Test Bean", result.BeanName);
        Assert.Equal("Test Roaster", result.Roaster);
        Assert.Equal("Ethiopia", result.Origin);
    }

    [Fact]
    public async Task GetBeanRecommendationContextAsync_BeanWithHistory_ReturnsContextWithHistoricalShots()
    {
        // Arrange - T023
        var beanId = 1;
        var bean = new BaristaNotes.Core.Models.Bean { Id = beanId, Name = "Test Bean" };
        var bag = new BaristaNotes.Core.Models.Bag 
        { 
            Id = 1, 
            BeanId = beanId, 
            Bean = bean,
            RoastDate = DateTime.Now.AddDays(-7),
            IsDeleted = false
        };
        
        var shots = new List<BaristaNotes.Core.Models.ShotRecord>
        {
            new() { Id = 1, BagId = 1, Bag = bag, Rating = 4, DoseIn = 18, GrindSetting = "5", ActualOutput = 36, ActualTime = 28, DrinkType = "Espresso", IsDeleted = false, Timestamp = DateTime.Now.AddDays(-1), SyncId = Guid.NewGuid() },
            new() { Id = 2, BagId = 1, Bag = bag, Rating = 3, DoseIn = 18, GrindSetting = "6", ActualOutput = 38, ActualTime = 30, DrinkType = "Espresso", IsDeleted = false, Timestamp = DateTime.Now.AddDays(-2), SyncId = Guid.NewGuid() },
            new() { Id = 3, BagId = 1, Bag = bag, Rating = 2, DoseIn = 18, GrindSetting = "4", ActualOutput = 34, ActualTime = 26, DrinkType = "Espresso", IsDeleted = false, Timestamp = DateTime.Now.AddDays(-3), SyncId = Guid.NewGuid() }
        };
        
        _mockShotRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(shots);
        _mockBagRepository.Setup(r => r.GetBagsForBeanAsync(beanId, true)).ReturnsAsync(new List<BaristaNotes.Core.Models.Bag> { bag });

        // Act
        var result = await _service.GetBeanRecommendationContextAsync(beanId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.HasHistory);
        Assert.NotNull(result.HistoricalShots);
        Assert.Equal(3, result.HistoricalShots.Count);
        // Verify sorted by rating descending
        Assert.Equal(4, result.HistoricalShots[0].Rating);
        Assert.Equal(3, result.HistoricalShots[1].Rating);
        Assert.Equal(2, result.HistoricalShots[2].Rating);
    }

    [Fact]
    public async Task GetBeanRecommendationContextAsync_LimitsHistoricalShotsToTen()
    {
        // Arrange
        var beanId = 1;
        var bean = new BaristaNotes.Core.Models.Bean { Id = beanId, Name = "Test Bean" };
        var bag = new BaristaNotes.Core.Models.Bag 
        { 
            Id = 1, 
            BeanId = beanId, 
            Bean = bean,
            RoastDate = DateTime.Now.AddDays(-30),
            IsDeleted = false
        };
        
        // Create 15 shots
        var shots = Enumerable.Range(1, 15)
            .Select(i => new BaristaNotes.Core.Models.ShotRecord
            {
                Id = i,
                BagId = 1,
                Bag = bag,
                Rating = i % 5,
                DoseIn = 18,
                GrindSetting = "5",
                DrinkType = "Espresso",
                IsDeleted = false,
                Timestamp = DateTime.Now.AddDays(-i),
                SyncId = Guid.NewGuid()
            })
            .ToList();
        
        _mockShotRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(shots);
        _mockBagRepository.Setup(r => r.GetBagsForBeanAsync(beanId, true)).ReturnsAsync(new List<BaristaNotes.Core.Models.Bag> { bag });

        // Act
        var result = await _service.GetBeanRecommendationContextAsync(beanId);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.HasHistory);
        Assert.NotNull(result.HistoricalShots);
        Assert.Equal(10, result.HistoricalShots.Count); // Limited to 10
    }

    [Fact]
    public async Task GetBeanRecommendationContextAsync_NonExistentBean_ReturnsNull()
    {
        // Arrange
        var beanId = 999;
        _mockShotRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<BaristaNotes.Core.Models.ShotRecord>());
        _mockBagRepository.Setup(r => r.GetBagsForBeanAsync(beanId, true)).ReturnsAsync(new List<BaristaNotes.Core.Models.Bag>());

        // Act
        var result = await _service.GetBeanRecommendationContextAsync(beanId);

        // Assert
        Assert.Null(result);
    }

    #endregion
}
