using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

public interface IBeanService
{
    Task<List<BeanDto>> GetAllActiveBeansAsync();
    Task<BeanDto?> GetBeanByIdAsync(int id);
    Task<BeanDto?> GetBeanWithRatingsAsync(int id);
    Task<OperationResult<BeanDto>> CreateBeanAsync(CreateBeanDto dto);
    Task<BeanDto> UpdateBeanAsync(int id, UpdateBeanDto dto);
    Task ArchiveBeanAsync(int id);
    Task DeleteBeanAsync(int id);

    /// <summary>
    /// Returns up to <paramref name="limit"/> recently-active, non-deleted, active beans.
    /// Activity is the max of the most recent non-deleted Bag's CreatedAt and the most
    /// recent non-deleted ShotRecord's Timestamp across the bean's bags.
    /// Beans with no activity or whose most recent activity is older than
    /// <paramref name="withinDays"/> days are excluded. Ordered by most-recent activity first.
    /// </summary>
    Task<IReadOnlyList<BeanDto>> GetRecentBeansAsync(int limit = 6, int withinDays = 90);

    /// <summary>
    /// Finds a non-deleted bean by name (and optional roaster), tolerating minor
    /// typos via Levenshtein distance ≤ 2 on the normalized name.
    /// Returns null if no suitable match is found.
    /// </summary>
    Task<BeanDto?> FuzzyFindByNameRoasterAsync(string name, string? roaster);

    /// <summary>
    /// Returns distinct, trimmed, non-empty roaster values from non-deleted beans,
    /// ordered alphabetically (case-insensitive).
    /// </summary>
    Task<IReadOnlyList<string>> GetDistinctRoastersAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns distinct, trimmed, non-empty origin values from non-deleted beans,
    /// ordered alphabetically (case-insensitive).
    /// </summary>
    Task<IReadOnlyList<string>> GetDistinctOriginsAsync(CancellationToken ct = default);

    /// <summary>
    /// Manually re-runs recipe sourcing for a bean. Returns the updated set of
    /// recipes. No-op if recipe sourcing is not configured (returns empty).
    /// </summary>
    Task<IReadOnlyList<DTOs.RecipeDto>> RefreshRecipesAsync(int beanId, CancellationToken ct = default);
}
