using BaristaNotes.Core.Data;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Service for calculating rating aggregates (averages and distributions).
/// Performs on-demand calculations from ShotRecord data with optimized queries.
/// </summary>
public class RatingService : IRatingService
{
    private readonly BaristaNotesContext _context;

    public RatingService(BaristaNotesContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<RatingAggregateDto> GetBeanRatingAsync(int beanId)
    {
        // Query all shots for all bags belonging to this bean
        // Use composite index: IX_ShotRecords_BagId_Rating for performance
        var shots = await _context.ShotRecords
            .AsNoTracking()
            .Include(s => s.Bag)
            .Where(s => !s.IsDeleted && s.Bag.BeanId == beanId)
            .Select(s => new { s.Rating })
            .ToListAsync();

        return CalculateAggregate(shots.Select(s => s.Rating).ToList());
    }

    /// <inheritdoc />
    public async Task<RatingAggregateDto> GetBagRatingAsync(int bagId)
    {
        // Query shots for specific bag
        // Uses index: IX_ShotRecords_BagId
        var shots = await _context.ShotRecords
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.BagId == bagId)
            .Select(s => new { s.Rating })
            .ToListAsync();

        return CalculateAggregate(shots.Select(s => s.Rating).ToList());
    }

    /// <inheritdoc />
    public async Task<Dictionary<int, RatingAggregateDto>> GetBagRatingsBatchAsync(IEnumerable<int> bagIds)
    {
        var bagIdList = bagIds.ToList();
        
        // Batch query optimization: Get all shots for multiple bags in single query
        var shotsByBag = await _context.ShotRecords
            .AsNoTracking()
            .Where(s => !s.IsDeleted && bagIdList.Contains(s.BagId))
            .Select(s => new { s.BagId, s.Rating })
            .ToListAsync();

        // Group by BagId and calculate aggregate for each
        var result = new Dictionary<int, RatingAggregateDto>();
        
        foreach (var bagId in bagIdList)
        {
            var bagShots = shotsByBag
                .Where(s => s.BagId == bagId)
                .Select(s => s.Rating)
                .ToList();
            
            result[bagId] = CalculateAggregate(bagShots);
        }

        return result;
    }

    /// <summary>
    /// Calculates rating aggregate from a collection of ratings.
    /// Core business logic - requires 100% test coverage per NFR-Q1.
    /// </summary>
    /// <param name="ratings">Collection of ratings (may contain nulls)</param>
    /// <returns>Rating aggregate with average, counts, and distribution</returns>
    private RatingAggregateDto CalculateAggregate(List<int?> ratings)
    {
        var aggregate = new RatingAggregateDto
        {
            TotalShots = ratings.Count,
            Distribution = new Dictionary<int, int>()
        };

        // Filter to only rated shots
        var ratedShots = ratings.Where(r => r.HasValue).Select(r => r!.Value).ToList();
        aggregate.RatedShots = ratedShots.Count;

        if (ratedShots.Count == 0)
        {
            aggregate.AverageRating = 0.0;
            return aggregate;
        }

        // Calculate average
        aggregate.AverageRating = ratedShots.Average();

        // Build distribution (1-5 star counts)
        for (int rating = 1; rating <= 5; rating++)
        {
            aggregate.Distribution[rating] = ratedShots.Count(r => r == rating);
        }

        return aggregate;
    }
}
