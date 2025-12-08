using BaristaNotes.Core.Data;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BaristaNotes.Tests.Unit;

/// <summary>
/// Unit tests for RatingService - validates rating calculation accuracy.
/// Required coverage: 100% per NFR-Q1 (critical business logic).
/// </summary>
public class RatingServiceTests : IDisposable
{
    private readonly BaristaNotesContext _context;
    private readonly RatingService _ratingService;

    public RatingServiceTests()
    {
        // Use in-memory database for testing
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseInMemoryDatabase(databaseName: $"RatingServiceTest_{Guid.NewGuid()}")
            .Options;

        _context = new BaristaNotesContext(options);
        _ratingService = new RatingService(_context);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region GetBeanRatingAsync Tests

    [Fact]
    public async Task GetBeanRatingAsync_WithMultipleBags_ReturnsCorrectAggregate()
    {
        // Arrange: Create bean with 2 bags and shots across both
        var bean = new Bean
        {
            Name = "Test Bean",
            Roaster = "Test Roaster",
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Beans.Add(bean);
        await _context.SaveChangesAsync();

        var bag1 = new Bag
        {
            BeanId = bean.Id,
            RoastDate = DateTime.Now.AddDays(-7),
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        var bag2 = new Bag
        {
            BeanId = bean.Id,
            RoastDate = DateTime.Now.AddDays(-3),
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Bags.AddRange(bag1, bag2);
        await _context.SaveChangesAsync();

        // Bag 1: Ratings 5, 4, 4
        _context.ShotRecords.AddRange(
            CreateShot(bag1.Id, 5),
            CreateShot(bag1.Id, 4),
            CreateShot(bag1.Id, 4)
        );

        // Bag 2: Ratings 3, 2, 5
        _context.ShotRecords.AddRange(
            CreateShot(bag2.Id, 3),
            CreateShot(bag2.Id, 2),
            CreateShot(bag2.Id, 5)
        );

        await _context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetBeanRatingAsync(bean.Id);

        // Assert: Average = (5+4+4+3+2+5)/6 = 23/6 = 3.833...
        Assert.True(result.HasRatings);
        Assert.Equal(6, result.TotalShots);
        Assert.Equal(6, result.RatedShots);
        Assert.Equal(3.83, result.AverageRating, 2); // 2 decimal precision

        // Distribution: 5:2, 4:2, 3:1, 2:1, 1:0
        Assert.Equal(2, result.GetCountForRating(5));
        Assert.Equal(2, result.GetCountForRating(4));
        Assert.Equal(1, result.GetCountForRating(3));
        Assert.Equal(1, result.GetCountForRating(2));
        Assert.Equal(0, result.GetCountForRating(1));
    }

    [Fact]
    public async Task GetBeanRatingAsync_WithNoShots_ReturnsEmptyAggregate()
    {
        // Arrange: Bean with bag but no shots
        var bean = new Bean
        {
            Name = "Empty Bean",
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Beans.Add(bean);
        await _context.SaveChangesAsync();

        var bag = new Bag
        {
            BeanId = bean.Id,
            RoastDate = DateTime.Now,
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Bags.Add(bag);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetBeanRatingAsync(bean.Id);

        // Assert
        Assert.False(result.HasRatings);
        Assert.Equal(0, result.TotalShots);
        Assert.Equal(0, result.RatedShots);
        Assert.Equal(0.0, result.AverageRating);
        Assert.Equal("N/A", result.FormattedAverage);
        Assert.Empty(result.Distribution);
    }

    [Fact]
    public async Task GetBeanRatingAsync_WithMixedRatedAndUnratedShots_CalculatesCorrectly()
    {
        // Arrange: Some shots have ratings, some don't
        var bean = new Bean
        {
            Name = "Mixed Bean",
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Beans.Add(bean);
        await _context.SaveChangesAsync();

        var bag = new Bag
        {
            BeanId = bean.Id,
            RoastDate = DateTime.Now,
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Bags.Add(bag);
        await _context.SaveChangesAsync();

        _context.ShotRecords.AddRange(
            CreateShot(bag.Id, 5),
            CreateShot(bag.Id, 4),
            CreateShot(bag.Id, null), // No rating
            CreateShot(bag.Id, 3),
            CreateShot(bag.Id, null)  // No rating
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetBeanRatingAsync(bean.Id);

        // Assert: Average only considers rated shots (5+4+3)/3 = 4.0
        Assert.True(result.HasRatings);
        Assert.Equal(5, result.TotalShots);
        Assert.Equal(3, result.RatedShots);
        Assert.Equal(4.0, result.AverageRating, 2);
    }

    [Fact]
    public async Task GetBeanRatingAsync_DistributionCalculation_IsAccurate()
    {
        // Arrange: Test distribution accuracy requirement (NFR-Q4)
        var bean = new Bean
        {
            Name = "Distribution Test",
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Beans.Add(bean);
        await _context.SaveChangesAsync();

        var bag = new Bag
        {
            BeanId = bean.Id,
            RoastDate = DateTime.Now,
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Bags.Add(bag);
        await _context.SaveChangesAsync();

        // Create 100 shots to test accuracy
        var shots = Enumerable.Range(1, 100).Select(i =>
        {
            int rating = (i % 5) + 1; // Cycles through 1-5
            return CreateShot(bag.Id, rating);
        }).ToList();

        _context.ShotRecords.AddRange(shots);
        await _context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetBeanRatingAsync(bean.Id);

        // Assert: Each rating 1-5 should appear 20 times (100 / 5 = 20)
        Assert.Equal(100, result.RatedShots);
        Assert.Equal(20, result.GetCountForRating(1));
        Assert.Equal(20, result.GetCountForRating(2));
        Assert.Equal(20, result.GetCountForRating(3));
        Assert.Equal(20, result.GetCountForRating(4));
        Assert.Equal(20, result.GetCountForRating(5));

        // Each percentage should be 20%
        Assert.Equal(20.0, result.GetPercentageForRating(1));
        Assert.Equal(20.0, result.GetPercentageForRating(5));
    }

    #endregion

    #region GetBagRatingAsync Tests

    [Fact]
    public async Task GetBagRatingAsync_WithValidBag_ReturnsCorrectAggregate()
    {
        // Arrange
        var bean = new Bean
        {
            Name = "Test Bean",
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Beans.Add(bean);
        await _context.SaveChangesAsync();

        var bag = new Bag
        {
            BeanId = bean.Id,
            RoastDate = DateTime.Now,
            IsActive = true,
            CreatedAt = DateTime.Now,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
        _context.Bags.Add(bag);
        await _context.SaveChangesAsync();

        _context.ShotRecords.AddRange(
            CreateShot(bag.Id, 5),
            CreateShot(bag.Id, 4),
            CreateShot(bag.Id, 5)
        );
        await _context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetBagRatingAsync(bag.Id);

        // Assert: Average = (5+4+5)/3 = 4.67
        Assert.Equal(3, result.TotalShots);
        Assert.Equal(3, result.RatedShots);
        Assert.Equal(4.67, result.AverageRating, 2);
        Assert.Equal(2, result.GetCountForRating(5));
        Assert.Equal(1, result.GetCountForRating(4));
    }

    #endregion

    #region Helper Methods

    private ShotRecord CreateShot(int bagId, int? rating)
    {
        return new ShotRecord
        {
            BagId = bagId,
            Timestamp = DateTime.Now,
            DoseIn = 18.0m,
            GrindSetting = "5",
            ExpectedTime = 28.0m,
            ExpectedOutput = 36.0m,
            DrinkType = "Espresso",
            Rating = rating,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTime.Now
        };
    }

    #endregion
}
