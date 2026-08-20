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

    public GrindTranslationCacheRepository(BaristaNotesContext context)
    {
        _context = context;
    }

    public async Task<GrindTranslationCache?> FindAsync(
        string grinderModelNormalized,
        string grindHintNormalized,
        BrewMethod method)
    {
        var now = DateTime.UtcNow;
        var targetModel = grinderModelNormalized;
        var targetHint = grindHintNormalized;
        var targetMethod = method;
        return await _context.GrindTranslationCache
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.GrinderModelNormalized == targetModel &&
                c.GrindHintNormalized == targetHint &&
                c.BrewMethod == targetMethod &&
                c.ExpiresAt > now);
    }

    public async Task UpsertAsync(GrindTranslationCache entry)
    {
        var targetModel = entry.GrinderModelNormalized;
        var targetHint = entry.GrindHintNormalized;
        var targetMethod = entry.BrewMethod;
        var existing = await _context.GrindTranslationCache.FirstOrDefaultAsync(c =>
            c.GrinderModelNormalized == targetModel &&
            c.GrindHintNormalized == targetHint &&
            c.BrewMethod == targetMethod);

        if (existing == null)
        {
            await _context.GrindTranslationCache.AddAsync(entry);
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
        var cutoff = utcNow;
        var expired = await _context.GrindTranslationCache
            .Where(c => c.ExpiresAt <= cutoff)
            .ToListAsync();
        if (expired.Count == 0) return;
        _context.GrindTranslationCache.RemoveRange(expired);
        await _context.SaveChangesAsync();
    }
}
