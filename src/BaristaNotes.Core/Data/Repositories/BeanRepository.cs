using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Data.Repositories;

public class BeanRepository : Repository<Bean>, IBeanRepository
{
    public BeanRepository(BaristaNotesContext context) : base(context) { }

    public override Task<Bean?> GetByIdAsync(int id)
    {
        var beanId = id;
        return _context.Beans.FirstOrDefaultAsync(b => b.Id == beanId);
    }

    public override Task<List<Bean>> GetAllAsync()
        => _context.Beans.ToListAsync();
    
    public async Task<List<Bean>> GetActiveBeansAsync()
    {
        return await _context.Beans
            .Where(b => b.IsActive && !b.IsDeleted)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<List<Bean>> GetActiveBeansWithActivityAsync()
    {
        var beans = await _context.Beans
            .Where(b => b.IsActive && !b.IsDeleted)
            .Include(b => b.Bags.Where(bag => !bag.IsDeleted))
            .AsSplitQuery()
            .ToListAsync();

        _ = await _context.ShotRecords
            .Where(s => !s.IsDeleted
                && !s.Bag.IsDeleted
                && s.Bag.Bean.IsActive
                && !s.Bag.Bean.IsDeleted)
            .ToListAsync();

        return beans;
    }

    public async Task<List<Bean>> GetNonDeletedBeansAsync(string? roaster = null)
    {
        if (string.IsNullOrWhiteSpace(roaster))
            return await _context.Beans.Where(b => !b.IsDeleted).ToListAsync();

        var normalizedRoaster = roaster.Trim().ToLower();
        // Case-insensitive exact match on roaster; callers needing fuzzy matching
        // should pass null and filter in memory.
        return await _context.Beans
            .Where(b => !b.IsDeleted
                && b.Roaster != null
                && b.Roaster.ToLower() == normalizedRoaster)
            .ToListAsync();
    }
}
