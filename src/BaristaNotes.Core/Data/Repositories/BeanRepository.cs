using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Data.Repositories;

public class BeanRepository : Repository<Bean>, IBeanRepository
{
    public BeanRepository(BaristaNotesContext context) : base(context) { }
    
    public async Task<List<Bean>> GetActiveBeansAsync()
    {
        return await _dbSet
            .Where(b => b.IsActive && !b.IsDeleted)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<List<Bean>> GetActiveBeansWithActivityAsync()
    {
        return await _dbSet
            .Where(b => b.IsActive && !b.IsDeleted)
            .Include(b => b.Bags.Where(bag => !bag.IsDeleted))
                .ThenInclude(bag => bag.ShotRecords.Where(s => !s.IsDeleted))
            .AsSplitQuery()
            .ToListAsync();
    }

    public async Task<List<Bean>> GetNonDeletedBeansAsync(string? roaster = null)
    {
        var query = _dbSet.Where(b => !b.IsDeleted);
        if (!string.IsNullOrWhiteSpace(roaster))
        {
            var trimmed = roaster.Trim();
            // Case-insensitive exact match on roaster; callers needing fuzzy matching
            // should pass null and filter in memory.
            query = query.Where(b => b.Roaster != null && b.Roaster.ToLower() == trimmed.ToLower());
        }
        return await query.ToListAsync();
    }
}
