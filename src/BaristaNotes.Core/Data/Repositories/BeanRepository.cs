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
}
