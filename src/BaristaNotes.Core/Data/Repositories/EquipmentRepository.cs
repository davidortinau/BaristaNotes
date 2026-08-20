using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Data.Repositories;

public class EquipmentRepository : Repository<Equipment>, IEquipmentRepository
{
    public EquipmentRepository(BaristaNotesContext context) : base(context) { }

    public override Task<Equipment?> GetByIdAsync(int id)
    {
        var equipmentId = id;
        return _context.Equipment.FirstOrDefaultAsync(e => e.Id == equipmentId);
    }

    public override Task<List<Equipment>> GetAllAsync()
        => _context.Equipment.ToListAsync();
    
    public async Task<List<Equipment>> GetByTypeAsync(EquipmentType type)
    {
        var equipmentType = type;
        return await _context.Equipment
            .Where(e => e.Type == equipmentType && !e.IsDeleted)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }
    
    public async Task<List<Equipment>> GetActiveEquipmentAsync()
    {
        return await _context.Equipment
            .Where(e => e.IsActive && !e.IsDeleted)
            .OrderBy(e => e.Name)
            .ToListAsync();
    }
}
