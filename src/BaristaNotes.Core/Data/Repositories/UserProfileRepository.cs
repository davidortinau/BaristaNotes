using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Data.Repositories;

public class UserProfileRepository : Repository<UserProfile>, IUserProfileRepository
{
    public UserProfileRepository(BaristaNotesContext context) : base(context) { }

    public override Task<UserProfile?> GetByIdAsync(int id)
    {
        var profileId = id;
        return _context.UserProfiles.FirstOrDefaultAsync(p => p.Id == profileId);
    }

    public override Task<List<UserProfile>> GetAllAsync()
        => _context.UserProfiles.ToListAsync();
    
    public async Task<List<UserProfile>> GetActiveProfilesAsync()
    {
        return await _context.UserProfiles
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }
}
