using Xunit;
using Moq;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;

namespace BaristaNotes.Tests.Unit.Services;

public class ShotServiceGetMostRecentTests
{
    private readonly Mock<IShotRecordRepository> _mockShotRepository;
    private readonly Mock<IPreferencesService> _mockPreferences;
    private readonly Mock<IBagRepository> _mockBagRepository;
    private readonly ShotService _service;
    
    public ShotServiceGetMostRecentTests()
    {
        _mockShotRepository = new Mock<IShotRecordRepository>();
        _mockPreferences = new Mock<IPreferencesService>();
        _mockBagRepository = new Mock<IBagRepository>();
        _service = new ShotService(_mockShotRepository.Object, _mockPreferences.Object, _mockBagRepository.Object);
    }
    
    [Fact]
    public async Task GetMostRecentShotAsync_NoShotsExist_ReturnsNull()
    {
        // Arrange
        _mockShotRepository.Setup(r => r.GetMostRecentAsync())
            .ReturnsAsync((ShotRecord?)null);
        
        // Act
        var result = await _service.GetMostRecentShotAsync();
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public async Task GetMostRecentShotAsync_ShotsExist_ReturnsLatestShot()
    {
        // Arrange
        var expectedTimestamp = DateTime.Now.AddMinutes(-5);
        var mostRecentShot = new ShotRecord
        {
            Id = 42,
            Timestamp = expectedTimestamp,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            ActualTime = 29,
            ActualOutput = 38,
            Rating = 4,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        _mockShotRepository.Setup(r => r.GetMostRecentAsync())
            .ReturnsAsync(mostRecentShot);
        
        // Act
        var result = await _service.GetMostRecentShotAsync();
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(42, result.Id);
        Assert.Equal(expectedTimestamp, result.Timestamp);
        Assert.Equal(18, result.DoseIn);
        Assert.Equal("5", result.GrindSetting);
        Assert.Equal(30, result.ExpectedTime);
        Assert.Equal(40, result.ExpectedOutput);
        Assert.Equal("Espresso", result.DrinkType);
        Assert.Equal(29, result.ActualTime);
        Assert.Equal(38, result.ActualOutput);
        Assert.Equal(4, result.Rating);
    }
    
    [Fact]
    public async Task GetMostRecentShotAsync_WithRelationships_MapsAllProperties()
    {
        // Arrange
        var bean = new Bean
        {
            Id = 1,
            Name = "Ethiopia Yirgacheffe",
            Roaster = "Local Roasters",
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var machine = new Equipment
        {
            Id = 2,
            Name = "Gaggia Classic Pro",
            Type = BaristaNotes.Core.Models.Enums.EquipmentType.Machine,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var grinder = new Equipment
        {
            Id = 3,
            Name = "Baratza Sette 270",
            Type = BaristaNotes.Core.Models.Enums.EquipmentType.Grinder,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var madeBy = new UserProfile
        {
            Id = 4,
            Name = "Alice",
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var madeFor = new UserProfile
        {
            Id = 5,
            Name = "Bob",
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var bag = new Bag
        {
            Id = 6,
            BeanId = bean.Id,
            Bean = bean,
            RoastDate = DateTime.Now.AddDays(-7),
            IsComplete = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var mostRecentShot = new ShotRecord
        {
            Id = 42,
            Timestamp = DateTime.Now.AddMinutes(-5),
            Bag = bag,
            BagId = bag.Id,
            Machine = machine,
            MachineId = machine.Id,
            Grinder = grinder,
            GrinderId = grinder.Id,
            MadeBy = madeBy,
            MadeById = madeBy.Id,
            MadeFor = madeFor,
            MadeForId = madeFor.Id,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        _mockShotRepository.Setup(r => r.GetMostRecentAsync())
            .ReturnsAsync(mostRecentShot);
        
        // Act
        var result = await _service.GetMostRecentShotAsync();
        
        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Bean);
        Assert.Equal("Ethiopia Yirgacheffe", result.Bean.Name);
        Assert.NotNull(result.Machine);
        Assert.Equal("Gaggia Classic Pro", result.Machine.Name);
        Assert.NotNull(result.Grinder);
        Assert.Equal("Baratza Sette 270", result.Grinder.Name);
        Assert.NotNull(result.MadeBy);
        Assert.Equal("Alice", result.MadeBy.Name);
        Assert.NotNull(result.MadeFor);
        Assert.Equal("Bob", result.MadeFor.Name);
    }
}
