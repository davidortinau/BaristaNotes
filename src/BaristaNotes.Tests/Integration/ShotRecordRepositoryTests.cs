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
    private int _defaultBagId;
    
    public ShotRecordRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        
        _context = new BaristaNotesContext(options);
        _repository = new ShotRecordRepository(_context);
        
        // Create a default bean and bag for tests
        _defaultBagId = CreateDefaultBag().GetAwaiter().GetResult();
    }
    
    private async Task<int> CreateDefaultBag()
    {
        var bean = new Bean
        {
            Name = "Test Bean",
            Roaster = "Test Roaster"
        };
        _context.Beans.Add(bean);
        await _context.SaveChangesAsync();
        
        var bag = new Bag
        {
            BeanId = bean.Id,
            RoastDate = DateTime.Now,
            IsComplete = false
        };
        _context.Bags.Add(bag);
        await _context.SaveChangesAsync();
        
        return bag.Id;
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
            BagId = _defaultBagId,
            Timestamp = DateTime.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
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
            BagId = _defaultBagId,
            Timestamp = DateTime.Now.AddDays(-2),
            DoseIn = 17,
            GrindSetting = "4",
            ExpectedTime = 28,
            ExpectedOutput = 38,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var recentShot = new ShotRecord
        {
            BagId = _defaultBagId,
            Timestamp = DateTime.Now.AddMinutes(-5),
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Latte",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
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
            BagId = _defaultBagId,
            Timestamp = DateTime.Now.AddDays(-1),
            DoseIn = 17,
            GrindSetting = "4",
            ExpectedTime = 28,
            ExpectedOutput = 38,
            DrinkType = "Espresso",
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var deletedShot = new ShotRecord
        {
            BagId = _defaultBagId,
            Timestamp = DateTime.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Latte",
            IsDeleted = true,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
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
                BagId = _defaultBagId,
                Timestamp = DateTime.Now.AddMinutes(-i),
                DoseIn = 18 + i,
                GrindSetting = i.ToString(),
                ExpectedTime = 30,
                ExpectedOutput = 40,
                DrinkType = "Espresso",
                SyncId = Guid.NewGuid(),
                LastModifiedAt = DateTime.Now
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
            BagId = _defaultBagId,
            Timestamp = DateTime.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            ActualTime = null,
            Rating = null,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
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
                BagId = _defaultBagId,
                Timestamp = DateTime.Now.AddMinutes(-i),
                DoseIn = 18,
                GrindSetting = "5",
                ExpectedTime = 30,
                ExpectedOutput = 40,
                DrinkType = "Espresso",
                IsDeleted = i == 2, // Mark one as deleted
                SyncId = Guid.NewGuid(),
                LastModifiedAt = DateTime.Now
            };
            await _repository.AddAsync(shot);
        }
        
        // Act
        var count = await _repository.GetTotalCountAsync();
        
        // Assert
        Assert.Equal(4, count); // 5 shots - 1 deleted
    }
    
    [Fact]
    public async Task UpdateAsync_SoftDeleteShot_PersistsIsDeletedFlag()
    {
        // Arrange
        var shot = new ShotRecord
        {
            BagId = _defaultBagId,
            Timestamp = DateTime.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var saved = await _repository.AddAsync(shot);
        
        // Act - Soft delete
        saved.IsDeleted = true;
        saved.LastModifiedAt = DateTime.Now;
        await _repository.UpdateAsync(saved);
        
        // Assert - Shot still exists in DB but marked as deleted
        var retrieved = await _context.ShotRecords
            .IgnoreQueryFilters() // Bypass soft delete filter
            .FirstOrDefaultAsync(s => s.Id == saved.Id);
        
        Assert.NotNull(retrieved);
        Assert.True(retrieved.IsDeleted);
    }
    
    [Fact]
    public async Task GetHistoryAsync_ExcludesDeletedShots()
    {
        // Arrange
        var activeShot = new ShotRecord
        {
            BagId = _defaultBagId,
            Timestamp = DateTime.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        var deletedShot = new ShotRecord
        {
            BagId = _defaultBagId,
            Timestamp = DateTime.Now.AddMinutes(-5),
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Latte",
            IsDeleted = true,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        
        await _repository.AddAsync(activeShot);
        await _repository.AddAsync(deletedShot);
        
        // Act
        var result = await _repository.GetHistoryAsync(0, 10);
        
        // Assert
        Assert.Single(result);
        Assert.Equal("Espresso", result[0].DrinkType);
    }
    
    [Fact]
    public async Task UpdateAsync_UpdatesEditableFields_PersistsChanges()
    {
        // Arrange
        var shot = new ShotRecord
        {
            BagId = _defaultBagId,
            Timestamp = DateTime.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            ActualTime = 25,
            ActualOutput = 38,
            Rating = 3,
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now.AddHours(-1)
        };
        
        var saved = await _repository.AddAsync(shot);
        
        // Act - Update editable fields
        saved.ActualTime = 28.5m;
        saved.ActualOutput = 42.0m;
        saved.Rating = 5;
        saved.DrinkType = "Latte";
        saved.LastModifiedAt = DateTime.Now;
        
        await _repository.UpdateAsync(saved);
        
        // Assert - Changes persisted
        var retrieved = await _repository.GetByIdAsync(saved.Id);
        
        Assert.NotNull(retrieved);
        Assert.Equal(28.5m, retrieved.ActualTime);
        Assert.Equal(42.0m, retrieved.ActualOutput);
        Assert.Equal(5, retrieved.Rating);
        Assert.Equal("Latte", retrieved.DrinkType);
        Assert.True(retrieved.LastModifiedAt > DateTime.Now.AddMinutes(-1));
    }
    
    [Fact]
    public async Task UpdateAsync_UpdatePreservesImmutableFields()
    {
        // Arrange
        var originalTimestamp = DateTime.Now.AddHours(-2);
        var shot = new ShotRecord
        {
            BagId = _defaultBagId,
            Timestamp = originalTimestamp,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 30,
            ExpectedOutput = 40,
            DrinkType = "Espresso",
            ActualTime = 25,
            IsDeleted = false,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now.AddHours(-1)
        };
        
        var saved = await _repository.AddAsync(shot);
        
        // Act - Update editable fields only
        saved.ActualTime = 28.5m;
        saved.DrinkType = "Latte";
        saved.LastModifiedAt = DateTime.Now;
        
        await _repository.UpdateAsync(saved);
        
        // Assert - Immutable fields unchanged
        var retrieved = await _repository.GetByIdAsync(saved.Id);
        
        Assert.NotNull(retrieved);
        Assert.Equal(originalTimestamp, retrieved.Timestamp);
        Assert.Equal(18, retrieved.DoseIn);
        Assert.Equal("5", retrieved.GrindSetting);
        Assert.Equal(30, retrieved.ExpectedTime);
        Assert.Equal(40, retrieved.ExpectedOutput);
    }
}
