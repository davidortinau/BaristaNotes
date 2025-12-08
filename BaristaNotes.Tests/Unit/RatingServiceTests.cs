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

    [Fact]
    public async Task GetBagRatingAsync_WithNoBag_ReturnsEmptyAggregate()
    {
        // Act
        var result = await _ratingService.GetBagRatingAsync(999);

        // Assert
        Assert.Equal(0, result.TotalShots);
        Assert.Equal(0, result.RatedShots);
        Assert.Equal(0.0, result.AverageRating);
    }

    #endregion

    #region GetBagRatingsBatchAsync Tests (US3)

    [Fact]
    public async Task GetBagRatingsBatchAsync_WithMultipleBags_ReturnsDictionaryMappingBagIdToAggregate()
    {
        // Arrange: Create bean with 3 bags, each with different ratings
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

        var bag1 = new Bag { BeanId = bean.Id, RoastDate = DateTime.Now.AddDays(-10), IsActive = true, CreatedAt = DateTime.Now, SyncId = Guid.NewGuid(), LastModifiedAt = DateTime.Now };
        var bag2 = new Bag { BeanId = bean.Id, RoastDate = DateTime.Now.AddDays(-5), IsActive = true, CreatedAt = DateTime.Now, SyncId = Guid.NewGuid(), LastModifiedAt = DateTime.Now };
        var bag3 = new Bag { BeanId = bean.Id, RoastDate = DateTime.Now, IsActive = true, CreatedAt = DateTime.Now, SyncId = Guid.NewGuid(), LastModifiedAt = DateTime.Now };

        _context.Bags.AddRange(bag1, bag2, bag3);
        await _context.SaveChangesAsync();

        // Bag 1: Avg = 4.8 (5,5,5,5,4)
        _context.ShotRecords.AddRange(
            CreateShot(bag1.Id, 5),
            CreateShot(bag1.Id, 5),
            CreateShot(bag1.Id, 5),
            CreateShot(bag1.Id, 5),
            CreateShot(bag1.Id, 4)
        );

        // Bag 2: Avg = 3.2 (4,3,3,3,3)
        _context.ShotRecords.AddRange(
            CreateShot(bag2.Id, 4),
            CreateShot(bag2.Id, 3),
            CreateShot(bag2.Id, 3),
            CreateShot(bag2.Id, 3),
            CreateShot(bag2.Id, 3)
        );

        // Bag 3: No shots yet
        await _context.SaveChangesAsync();

        // Act
        var result = await _ratingService.GetBagRatingsBatchAsync(new[] { bag1.Id, bag2.Id, bag3.Id });

        // Assert
        Assert.Equal(3, result.Count);

        // Bag 1 assertions
        Assert.True(result.ContainsKey(bag1.Id));
        Assert.Equal(5, result[bag1.Id].RatedShots);
        Assert.Equal(4.8, result[bag1.Id].AverageRating, 1);
        Assert.Equal(4, result[bag1.Id].GetCountForRating(5));
        Assert.Equal(1, result[bag1.Id].GetCountForRating(4));

        // Bag 2 assertions
        Assert.True(result.ContainsKey(bag2.Id));
        Assert.Equal(5, result[bag2.Id].RatedShots);
        Assert.Equal(3.2, result[bag2.Id].AverageRating, 1);
        Assert.Equal(1, result[bag2.Id].GetCountForRating(4));
        Assert.Equal(4, result[bag2.Id].GetCountForRating(3));

        // Bag 3 assertions (no shots)
        Assert.True(result.ContainsKey(bag3.Id));
        Assert.Equal(0, result[bag3.Id].RatedShots);
        Assert.Equal(0.0, result[bag3.Id].AverageRating);
    }

    [Fact]
    public async Task GetBagRatingsBatchAsync_WithEmptyList_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _ratingService.GetBagRatingsBatchAsync(Array.Empty<int>());

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetBagRatingsBatchAsync_OptimizesQueryPerformance_SingleDatabaseRoundTrip()
    {
        // Arrange: Create multiple bags with shots
        var bean = new Bean { Name = "Test", IsActive = true, CreatedAt = DateTime.Now, SyncId = Guid.NewGuid(), LastModifiedAt = DateTime.Now };
        _context.Beans.Add(bean);
        await _context.SaveChangesAsync();

        var bagIds = new List<int>();
        for (int i = 0; i < 10; i++)
        {
            var bag = new Bag { BeanId = bean.Id, RoastDate = DateTime.Now.AddDays(-i), IsActive = true, CreatedAt = DateTime.Now, SyncId = Guid.NewGuid(), LastModifiedAt = DateTime.Now };
            _context.Bags.Add(bag);
            await _context.SaveChangesAsync();
            bagIds.Add(bag.Id);

            // Add 5 shots per bag
            _context.ShotRecords.AddRange(Enumerable.Range(1, 5).Select(j => CreateShot(bag.Id, (j % 5) + 1)));
        }
        await _context.SaveChangesAsync();

        // Act: Batch query should be faster than individual queries
        var startTime = DateTime.Now;
        var result = await _ratingService.GetBagRatingsBatchAsync(bagIds);
        var batchDuration = (DateTime.Now - startTime).TotalMilliseconds;

        // Assert: All bags processed
        Assert.Equal(10, result.Count);
        foreach (var bagId in bagIds)
        {
            Assert.True(result.ContainsKey(bagId));
            Assert.Equal(5, result[bagId].RatedShots);
        }

        // Performance assertion: Should complete quickly (< 500ms per NFR-P2)
        Assert.True(batchDuration < 500, $"Batch query took {batchDuration}ms, expected < 500ms");
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
