using Xunit;
using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Tests.Integration;

public class ShotRecordRepositoryTests : IDisposable
{
    private readonly BaristaNotesContext _context;
    private readonly ShotRecordRepository _repository;
    
    public ShotRecordRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new BaristaNotesContext(options);
        _repository = new ShotRecordRepository(_context);
    }
    
    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
    
    [Fact]
    public async Task AddAsync_ValidShot_SavesSuccessfully()
    {
        // Arrange
        var shot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        // Act
        var result = await _repository.AddAsync(shot);
        
        // Assert
        Assert.NotEqual(0, result.Id);
        Assert.Equal(18, result.DoseIn);
        Assert.Equal("5", result.GrindSetting);
    }
    
    [Fact]
    public async Task GetMostRecentAsync_NoShots_ReturnsNull()
    {
        // Act
        var result = await _repository.GetMostRecentAsync();
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public async Task GetMostRecentAsync_MultipleShots_ReturnsLatest()
    {
        // Arrange
        var oldShot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now.AddDays(-2),
            DoseIn = 17,
            GrindSetting = "4",
            ExpectedTime = 28,
            ExpectedOutput = 38,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        var recentShot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now.AddMinutes(-5),
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Latte",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        await _repository.AddAsync(oldShot);
        await _repository.AddAsync(recentShot);
        
        // Act
        var result = await _repository.GetMostRecentAsync();
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(18, result.DoseIn);
        Assert.Equal("Latte", result.DrinkType);
    }
    
    [Fact]
    public async Task GetMostRecentAsync_IgnoresDeletedShots()
    {
        // Arrange
        var activeShot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now.AddDays(-1),
            DoseIn = 17,
            GrindSetting = "4",
            ExpectedTime = 28,
            ExpectedOutput = 38,
            DrinkType = "Espresso",
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        var deletedShot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Latte",
            IsDeleted = true,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        await _repository.AddAsync(activeShot);
        await _repository.AddAsync(deletedShot);
        
        // Act
        var result = await _repository.GetMostRecentAsync();
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(17, result.DoseIn);
        Assert.Equal("Espresso", result.DrinkType);
    }
    
    [Fact]
    public async Task GetHistoryAsync_ReturnsPaginatedResults()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            var shot = new ShotRecord
            {
                Timestamp = DateTimeOffset.Now.AddMinutes(-i),
                DoseIn = 18 + i,
                GrindSetting = i.ToString(),
                ExpectedTime = 30,
                ExpectedOutput = 40,
                DrinkType = "Espresso",
                SyncId = Guid.NewGuid(),
                LastModifiedAt = DateTimeOffset.Now
            };
            await _repository.AddAsync(shot);
        }
        
        // Act - Get first page
        var firstPage = await _repository.GetHistoryAsync(0, 10);
        
        // Assert
        Assert.Equal(10, firstPage.Count);
        Assert.Equal(18, firstPage[0].DoseIn); // Most recent
        
        // Act - Get second page
        var secondPage = await _repository.GetHistoryAsync(1, 10);
        
        // Assert
        Assert.Equal(5, secondPage.Count);
        Assert.Equal(28, secondPage[0].DoseIn);
    }
    
    [Fact]
    public async Task UpdateAsync_ModifiesExistingShot()
    {
        // Arrange
        var shot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            ActualTime = null,
            Rating = null,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        var created = await _repository.AddAsync(shot);
        
        // Act - Update actuals
        created.ActualTime = 29;
        created.ActualOutput = 38;
        created.Rating = 4;
        var updated = await _repository.UpdateAsync(created);
        
        // Assert
        var retrieved = await _repository.GetByIdAsync(created.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(29, retrieved.ActualTime);
        Assert.Equal(38, retrieved.ActualOutput);
        Assert.Equal(4, retrieved.Rating);
    }
    
    [Fact]
    public async Task GetTotalCountAsync_ExcludesDeletedShots()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            var shot = new ShotRecord
            {
                Timestamp = DateTimeOffset.Now.AddMinutes(-i),
                DoseIn = 18,
                GrindSetting = "5",
                ExpectedTime = 30,
                ExpectedOutput = 40,
                DrinkType = "Espresso",
                IsDeleted = i == 2, // Mark one as deleted
                SyncId = Guid.NewGuid(),
                LastModifiedAt = DateTimeOffset.Now
            };
            await _repository.AddAsync(shot);
        }
        
        // Act
        var count = await _repository.GetTotalCountAsync();
        
        // Assert
        Assert.Equal(4, count); // 5 shots - 1 deleted
    }
}
