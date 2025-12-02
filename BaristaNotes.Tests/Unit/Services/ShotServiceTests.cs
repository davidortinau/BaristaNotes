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
}
