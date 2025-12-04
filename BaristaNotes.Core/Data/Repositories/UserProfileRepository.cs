using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Data.Repositories;

public class UserProfileRepository : Repository<UserProfile>, IUserProfileRepository
{
    public UserProfileRepository(BaristaNotesContext context) : base(context) { }
    
    public async Task<List<UserProfile>> GetActiveProfilesAsync()
    {
        return await _dbSet
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
}
