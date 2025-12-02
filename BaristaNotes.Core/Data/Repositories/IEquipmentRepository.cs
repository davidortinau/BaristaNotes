using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Data.Repositories;

public interface IEquipmentRepository : IRepository<Equipment>
{
    Task<List<Equipment>> GetByTypeAsync(EquipmentType type);
    Task<List<Equipment>> GetActiveEquipmentAsync();
}
