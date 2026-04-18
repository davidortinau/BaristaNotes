using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Data.Repositories;

public interface IUserProfileRepository : IRepository<UserProfile>
{
    Task<List<UserProfile>> GetActiveProfilesAsync();
}
