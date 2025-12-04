using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Data.Repositories;

public interface IBeanRepository : IRepository<Bean>
{
    Task<List<Bean>> GetActiveBeansAsync();
}
