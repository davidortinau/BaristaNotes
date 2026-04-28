using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Data.Repositories;

public interface IBeanRepository : IRepository<Bean>
{
    Task<List<Bean>> GetActiveBeansAsync();

    /// <summary>
    /// Returns non-deleted, active beans with their non-deleted Bags and non-deleted ShotRecords loaded.
    /// Used for computing "recent activity" ordering on the service layer.
    /// </summary>
    Task<List<Bean>> GetActiveBeansWithActivityAsync();

    /// <summary>
    /// Returns all non-deleted beans (may include inactive), optionally filtered by roaster (exact, case-insensitive).
    /// Used by fuzzy-find and autocomplete sources.
    /// </summary>
    Task<List<Bean>> GetNonDeletedBeansAsync(string? roaster = null);
}
