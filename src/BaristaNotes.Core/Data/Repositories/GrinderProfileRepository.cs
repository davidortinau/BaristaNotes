using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.Grind;

namespace BaristaNotes.Core.Data.Repositories;

public interface IGrinderProfileRepository : IRepository<GrinderProfile>
{
    Task<GrinderProfile?> GetByEquipmentIdAsync(int equipmentId);

    /// <summary>
    /// Returns the profile for a grinder Equipment, creating a new one with
    /// sensible defaults (and DF64 seed anchors when the equipment name
    /// matches) if none exists. The returned profile always includes the
    /// Equipment navigation.
    /// </summary>
    Task<GrinderProfile> GetOrCreateForEquipmentAsync(Equipment equipment);

    /// <summary>
    /// If the equipment is a DF64 family grinder and its stored anchors look
    /// stale (e.g. seeded against the old 0–9 dial assumption instead of the
    /// real 0–90 stepless dial), replace AnchorsJson and Min/Max/Step with
    /// the current seed and persist. Returns the refreshed (or unchanged)
    /// profile. Safe to call repeatedly; a no-op when the data already looks
    /// current.
    /// </summary>
    Task<GrinderProfile?> EnsureCurrentSeedsAsync(int equipmentId);
}

public class GrinderProfileRepository : Repository<GrinderProfile>, IGrinderProfileRepository
{
    public GrinderProfileRepository(BaristaNotesContext context) : base(context) { }

    public async Task<GrinderProfile?> GetByEquipmentIdAsync(int equipmentId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.Equipment)
            .FirstOrDefaultAsync(p => p.EquipmentId == equipmentId && !p.IsDeleted);
    }

    public async Task<GrinderProfile> GetOrCreateForEquipmentAsync(Equipment equipment)
    {
        var existing = await _dbSet
            .Include(p => p.Equipment)
            .FirstOrDefaultAsync(p => p.EquipmentId == equipment.Id && !p.IsDeleted);
        if (existing != null) return existing;

        var isDF64 = equipment.Name?.Contains("DF64", StringComparison.OrdinalIgnoreCase) == true;

        var now = DateTime.UtcNow;
        var profile = new GrinderProfile
        {
            EquipmentId = equipment.Id,
            MinSetting = isDF64 ? 0m : null,
            MaxSetting = isDF64 ? 90m : null,
            StepSize = isDF64 ? 1m : null,
            AnchorsJson = isDF64
                ? DeterministicGrindInterpolator.SerializeAnchors(DeterministicGrindInterpolator.DF64SeedAnchors)
                : null,
            CreatedAt = now,
            LastModifiedAt = now,
            SyncId = Guid.NewGuid(),
            IsDeleted = false,
        };

        await _dbSet.AddAsync(profile);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Race: another caller created the profile for this equipment first.
            // Detach our candidate and return the persisted one.
            _context.Entry(profile).State = EntityState.Detached;
            var persisted = await _dbSet
                .Include(p => p.Equipment)
                .FirstOrDefaultAsync(p => p.EquipmentId == equipment.Id && !p.IsDeleted);
            if (persisted != null) return persisted;
            throw;
        }
        profile.Equipment = equipment;
        return profile;
    }

    public async Task<GrinderProfile?> EnsureCurrentSeedsAsync(int equipmentId)
    {
        // Tracked load so we can persist any fix-up in place.
        var profile = await _dbSet
            .Include(p => p.Equipment)
            .FirstOrDefaultAsync(p => p.EquipmentId == equipmentId && !p.IsDeleted);
        if (profile == null) return null;

        var name = profile.Equipment?.Name;
        var isDF64 = name?.Contains("DF64", StringComparison.OrdinalIgnoreCase) == true;
        if (!isDF64) return profile;

        // Detect stale anchors. Two staleness signals:
        //   1. Old 0–9 dial assumption — max anchor Setting ≤ 10.
        //   2. Older 6-anchor "community" curve seeded against a different
        //      DF64V chart; replaced Dec 2025 with the "df64v-chart" curve.
        // Both cases get re-seeded with the current anchor set.
        var current = DeterministicGrindInterpolator.ParseAnchors(profile.AnchorsJson);
        var staleAnchors = current.Count == 0
            || current.Max(a => a.Setting) <= 10m
            || !current.All(a => string.Equals(a.Source, "df64v-chart", StringComparison.OrdinalIgnoreCase));
        var staleScale = profile.MaxSetting is null or <= 10m;
        if (!staleAnchors && !staleScale) return profile;

        profile.AnchorsJson = DeterministicGrindInterpolator.SerializeAnchors(
            DeterministicGrindInterpolator.DF64SeedAnchors);
        profile.MinSetting = 0m;
        profile.MaxSetting = 90m;
        profile.StepSize = 1m;
        profile.LastModifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return profile;
    }
}
