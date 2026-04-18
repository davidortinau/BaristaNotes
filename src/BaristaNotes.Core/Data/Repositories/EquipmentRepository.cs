using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Data.Repositories;

public class EquipmentRepository : Repository<Equipment>, IEquipmentRepository
{
    public EquipmentRepository(BaristaNotesContext context) : base(context) { }
    
    public async Task<List<Equipment>> GetByTypeAsync(EquipmentType type)
    {
        return await _dbSet
            .Where(e => e.Type == type && !e.IsDeleted)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }
    
    public async Task<List<Equipment>> GetActiveEquipmentAsync()
    {
        return await _dbSet
            .Where(e => e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }
}
