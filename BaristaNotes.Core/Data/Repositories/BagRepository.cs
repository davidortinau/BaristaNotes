using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Core.Data.Repositories;

/// <summary>
/// Repository implementation for Bag entity (T036).
/// Uses EF Core LINQ with .Include() for navigation properties to prevent N+1 queries.
/// Leverages composite index (BeanId, IsComplete, RoastDate) for performance.
/// </summary>
public class BagRepository : IBagRepository
{
    private readonly BaristaNotesContext _context;

    public BagRepository(BaristaNotesContext context)
    {
        _context = context;
    }

    public async Task<Bag> CreateAsync(Bag bag)
    {
        bag.CreatedAt = DateTime.Now;
        bag.LastModifiedAt = DateTime.Now;
        
        _context.Bags.Add(bag);
        await _context.SaveChangesAsync();
        return bag;
    }

    public async Task<Bag?> GetByIdAsync(int id)
    {
        return await _context.Bags
            .Include(b => b.Bean)
            .Where(b => !b.IsDeleted)
            .FirstOrDefaultAsync(b => b.Id == id);
    }

    public async Task<List<Bag>> GetBagsForBeanAsync(int beanId, bool includeCompleted = true)
    {
        var query = _context.Bags
            .Include(b => b.Bean)
            .Where(b => b.BeanId == beanId && !b.IsDeleted);

        if (!includeCompleted)
        {
            query = query.Where(b => !b.IsComplete);
        }

        return await query
            .OrderByDescending(b => b.RoastDate)
            .ToListAsync();
    }

    public async Task<List<BagSummaryDto>> GetBagSummariesForBeanAsync(int beanId, bool includeCompleted = true)
    {
        var query = _context.Bags
            .Include(b => b.Bean)
            .Include(b => b.ShotRecords)
            .Where(b => b.BeanId == beanId && !b.IsDeleted);

        if (!includeCompleted)
        {
            query = query.Where(b => !b.IsComplete);
        }

        var bags = await query
            .OrderByDescending(b => b.RoastDate)
            .ToListAsync();

        return bags.Select(b => new BagSummaryDto
        {
            Id = b.Id,
            BeanId = b.BeanId,
            BeanName = b.Bean?.Name ?? "Unknown",
            RoastDate = b.RoastDate,
            Notes = b.Notes,
            IsComplete = b.IsComplete,
            ShotCount = b.ShotRecords.Count(s => !s.IsDeleted),
            AverageRating = b.ShotRecords.Any(s => !s.IsDeleted && s.Rating.HasValue)
                ? b.ShotRecords.Where(s => !s.IsDeleted && s.Rating.HasValue).Average(s => s.Rating!.Value)
                : null
        }).ToList();
    }

    public async Task<List<BagSummaryDto>> GetActiveBagsForShotLoggingAsync()
    {
        // Uses composite index: IX_Bags_BeanId_IsComplete_RoastDate
        var bags = await _context.Bags
            .Include(b => b.Bean)
            .Include(b => b.ShotRecords)
            .Where(b => !b.IsComplete && !b.IsDeleted)
            .OrderByDescending(b => b.RoastDate)
            .ToListAsync();

        return bags.Select(b => new BagSummaryDto
        {
            Id = b.Id,
            BeanId = b.BeanId,
            BeanName = b.Bean?.Name ?? "Unknown",
            RoastDate = b.RoastDate,
            Notes = b.Notes,
            IsComplete = b.IsComplete,
            ShotCount = b.ShotRecords.Count(s => !s.IsDeleted),
            AverageRating = b.ShotRecords.Any(s => !s.IsDeleted && s.Rating.HasValue)
                ? b.ShotRecords.Where(s => !s.IsDeleted && s.Rating.HasValue).Average(s => s.Rating!.Value)
                : null
        }).ToList();
    }

    public async Task<Bag?> GetMostRecentActiveBagForBeanAsync(int beanId)
    {
        return await _context.Bags
            .Include(b => b.Bean)
            .Where(b => b.BeanId == beanId && !b.IsComplete && !b.IsDeleted)
            .OrderByDescending(b => b.RoastDate)
            .FirstOrDefaultAsync();
    }

    public async Task<Bag> UpdateAsync(Bag bag)
    {
        bag.LastModifiedAt = DateTime.Now;
        
        _context.Bags.Update(bag);
        await _context.SaveChangesAsync();
        return bag;
    }

    public async Task DeleteAsync(int id)
    {
        var bag = await GetByIdAsync(id);
        if (bag != null)
        {
            bag.IsDeleted = true;
            bag.LastModifiedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }
    }
}
