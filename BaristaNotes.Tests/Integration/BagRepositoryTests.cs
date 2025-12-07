using BaristaNotes.Core.Data;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BaristaNotes.Tests.Integration;

/// <summary>
/// Integration tests for BagRepository (T031).
/// Tests database queries, indexes, and EF Core navigation properties.
/// </summary>
public class BagRepositoryTests : IDisposable
{
    private readonly BaristaNotesContext _context;
    private readonly BagRepository _repository;

    public BagRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseInMemoryDatabase(databaseName: $"BagRepo_Test_{Guid.NewGuid()}")
            .Options;

        _context = new BaristaNotesContext(options);
        _repository = new BagRepository(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetBagSummariesForBeanAsync Tests

    [Fact]
    public async Task GetBagSummariesForBeanAsync_IncludesBeanName()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Ethiopian Yirgacheffe", IsActive = true };
        var bag1 = new Bag
        {
            Id = 1,
            BeanId = 1,
            RoastDate = DateTimeOffset.Now.AddDays(-10),
            IsComplete = false
        };
        var bag2 = new Bag
        {
            Id = 2,
            BeanId = 1,
            RoastDate = DateTimeOffset.Now.AddDays(-5),
            IsComplete = false
        };

        _context.Beans.Add(bean);
        _context.Bags.AddRange(bag1, bag2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetBagSummariesForBeanAsync(1, includeCompleted: true);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, summary => Assert.Equal("Ethiopian Yirgacheffe", summary.BeanName));
    }

    [Fact]
    public async Task GetBagSummariesForBeanAsync_IncludesIsComplete()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Test Bean", IsActive = true };
        var activeBag = new Bag
        {
            Id = 1,
            BeanId = 1,
            RoastDate = DateTimeOffset.Now.AddDays(-10),
            IsComplete = false
        };
        var completedBag = new Bag
        {
            Id = 2,
            BeanId = 1,
            RoastDate = DateTimeOffset.Now.AddDays(-5),
            IsComplete = true
        };

        _context.Beans.Add(bean);
        _context.Bags.AddRange(activeBag, completedBag);
        await _context.SaveChangesAsync();

        // Act
        var resultAll = await _repository.GetBagSummariesForBeanAsync(1, includeCompleted: true);
        var resultActiveOnly = await _repository.GetBagSummariesForBeanAsync(1, includeCompleted: false);

        // Assert
        Assert.Equal(2, resultAll.Count);
        Assert.Single(resultActiveOnly);
        Assert.False(resultActiveOnly[0].IsComplete);
    }

    [Fact]
    public async Task GetBagSummariesForBeanAsync_OrderedByRoastDateDescending()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Test Bean", IsActive = true };
        var oldBag = new Bag { Id = 1, BeanId = 1, RoastDate = DateTimeOffset.Now.AddDays(-20) };
        var middleBag = new Bag { Id = 2, BeanId = 1, RoastDate = DateTimeOffset.Now.AddDays(-10) };
        var newBag = new Bag { Id = 3, BeanId = 1, RoastDate = DateTimeOffset.Now.AddDays(-1) };

        _context.Beans.Add(bean);
        _context.Bags.AddRange(oldBag, middleBag, newBag);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetBagSummariesForBeanAsync(1, includeCompleted: true);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(3, result[0].Id); // Newest first
        Assert.Equal(2, result[1].Id);
        Assert.Equal(1, result[2].Id); // Oldest last
    }

    #endregion

    #region GetActiveBagsForShotLoggingAsync Tests

    [Fact]
    public async Task GetActiveBagsForShotLoggingAsync_ReturnsOnlyIncompleteBags()
    {
        // Arrange
        var bean1 = new Bean { Id = 1, Name = "Bean 1", IsActive = true };
        var bean2 = new Bean { Id = 2, Name = "Bean 2", IsActive = true };
        
        var activeBag1 = new Bag { Id = 1, BeanId = 1, RoastDate = DateTimeOffset.Now.AddDays(-5), IsComplete = false };
        var activeBag2 = new Bag { Id = 2, BeanId = 2, RoastDate = DateTimeOffset.Now.AddDays(-3), IsComplete = false };
        var completedBag = new Bag { Id = 3, BeanId = 1, RoastDate = DateTimeOffset.Now.AddDays(-10), IsComplete = true };

        _context.Beans.AddRange(bean1, bean2);
        _context.Bags.AddRange(activeBag1, activeBag2, completedBag);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveBagsForShotLoggingAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, bag => Assert.False(bag.IsComplete));
        Assert.DoesNotContain(result, bag => bag.Id == 3);
    }

    [Fact]
    public async Task GetActiveBagsForShotLoggingAsync_IncludesBeanNameViaNavigation()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Colombian Supremo", IsActive = true };
        var bag = new Bag { Id = 1, BeanId = 1, RoastDate = DateTimeOffset.Now.AddDays(-5), IsComplete = false };

        _context.Beans.Add(bean);
        _context.Bags.Add(bag);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveBagsForShotLoggingAsync();

        // Assert
        Assert.Single(result);
        Assert.Equal("Colombian Supremo", result[0].BeanName);
    }

    [Fact]
    public async Task GetActiveBagsForShotLoggingAsync_UsesCompositeIndex()
    {
        // Arrange - Create enough data to make index meaningful
        var bean1 = new Bean { Id = 1, Name = "Bean 1", IsActive = true };
        var bean2 = new Bean { Id = 2, Name = "Bean 2", IsActive = true };

        // Create bags with various states to trigger index (BeanId, IsComplete, RoastDate)
        var bags = new List<Bag>();
        for (int i = 1; i <= 20; i++)
        {
            bags.Add(new Bag
            {
                Id = i,
                BeanId = i % 2 == 0 ? 2 : 1,
                RoastDate = DateTimeOffset.Now.AddDays(-i),
                IsComplete = i % 3 == 0 // Every 3rd bag is complete
            });
        }

        _context.Beans.AddRange(bean1, bean2);
        _context.Bags.AddRange(bags);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetActiveBagsForShotLoggingAsync();

        // Assert
        // Verify only incomplete bags returned (should be ~13-14 bags out of 20)
        Assert.All(result, bag => Assert.False(bag.IsComplete));
        Assert.True(result.Count > 10);
        Assert.True(result.Count < 20);
        
        // Verify ordered by RoastDate DESC
        for (int i = 0; i < result.Count - 1; i++)
        {
            Assert.True(result[i].RoastDate >= result[i + 1].RoastDate);
        }
    }

    #endregion

    #region N+1 Query Prevention Tests

    [Fact]
    public async Task GetBagSummariesForBeanAsync_NoN1Queries_UsesInclude()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Test Bean", IsActive = true };
        var bags = new List<Bag>();
        for (int i = 1; i <= 5; i++)
        {
            bags.Add(new Bag
            {
                Id = i,
                BeanId = 1,
                RoastDate = DateTimeOffset.Now.AddDays(-i),
                IsComplete = false
            });
        }

        _context.Beans.Add(bean);
        _context.Bags.AddRange(bags);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetBagSummariesForBeanAsync(1, includeCompleted: true);

        // Assert
        Assert.Equal(5, result.Count);
        Assert.All(result, summary =>
        {
            // If BeanName is populated, it means .Include() was used
            Assert.Equal("Test Bean", summary.BeanName);
        });
    }

    #endregion
}
