using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Data.Repositories;

public interface IGrindTranslationCacheRepository
{
    Task<GrindTranslationCache?> FindAsync(
        string grinderModelNormalized,
        string grindHintNormalized,
        BrewMethod method);

    Task UpsertAsync(GrindTranslationCache entry);

    Task PurgeExpiredAsync(DateTime utcNow);
}

public class GrindTranslationCacheRepository : IGrindTranslationCacheRepository
{
    private readonly BaristaNotesContext _context;
    private readonly DbSet<GrindTranslationCache> _dbSet;

    public GrindTranslationCacheRepository(BaristaNotesContext context)
    {
        _context = context;
        _dbSet = context.GrindTranslationCache;
    }

    public async Task<GrindTranslationCache?> FindAsync(
        string grinderModelNormalized,
        string grindHintNormalized,
        BrewMethod method)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.GrinderModelNormalized == grinderModelNormalized &&
                c.GrindHintNormalized == grindHintNormalized &&
                c.BrewMethod == method &&
                c.ExpiresAt > now);
    }

    public async Task UpsertAsync(GrindTranslationCache entry)
    {
        var existing = await _dbSet.FirstOrDefaultAsync(c =>
            c.GrinderModelNormalized == entry.GrinderModelNormalized &&
            c.GrindHintNormalized == entry.GrindHintNormalized &&
            c.BrewMethod == entry.BrewMethod);

        if (existing == null)
        {
            await _dbSet.AddAsync(entry);
        }
        else
        {
            existing.MinSetting = entry.MinSetting;
            existing.MaxSetting = entry.MaxSetting;
            existing.SuggestedSetting = entry.SuggestedSetting;
            existing.Confidence = entry.Confidence;
            existing.Source = entry.Source;
            existing.Explanation = entry.Explanation;
            existing.CreatedAt = entry.CreatedAt;
            existing.ExpiresAt = entry.ExpiresAt;
        }
        await _context.SaveChangesAsync();
    }

    public async Task PurgeExpiredAsync(DateTime utcNow)
    {
        var expired = await _dbSet.Where(c => c.ExpiresAt <= utcNow).ToListAsync();
        if (expired.Count == 0) return;
        _dbSet.RemoveRange(expired);
        await _context.SaveChangesAsync();
    }
}
