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
            MaxSetting = isDF64 ? 9m : null,
            StepSize = isDF64 ? 0.1m : null,
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
}

