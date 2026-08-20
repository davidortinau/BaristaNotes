using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Data.Repositories;

public class ShotRecordRepository : Repository<ShotRecord>, IShotRecordRepository
{
    public ShotRecordRepository(BaristaNotesContext context) : base(context) { }
    
    public override async Task<ShotRecord?> GetByIdAsync(int id)
    {
        var shotId = id;
        return await _context.ShotRecords
            .Include("Bag.Bean")
            .Include(s => s.Machine)
            .Include(s => s.Grinder)
            .Include(s => s.MadeBy)
            .Include(s => s.MadeFor)
            .Include("ShotEquipments.Equipment")
            .FirstOrDefaultAsync(s => s.Id == shotId);
    }

    public override Task<List<ShotRecord>> GetAllAsync()
        => _context.ShotRecords.ToListAsync();
    
    public async Task<ShotRecord?> GetMostRecentAsync()
    {
        // Load all records first, then order in memory to avoid SQLite DateTimeOffset limitations
        var shots = await _context.ShotRecords
            .AsNoTracking()
            .Include("Bag.Bean") // TODO T038-T039: Navigate through Bag to Bean
            .Include(s => s.Machine)
            .Include(s => s.Grinder)
            .Include(s => s.MadeBy)
            .Include(s => s.MadeFor)
            .Include("ShotEquipments.Equipment")
            .Where(s => !s.IsDeleted)
            .ToListAsync();
            
        return shots.OrderByDescending(s => s.Timestamp).FirstOrDefault();
    }
    
    public async Task<List<ShotRecord>> GetHistoryAsync(int pageIndex, int pageSize)
    {
        // Load all non-deleted records first, then order and paginate in memory
        var allShots = await _context.ShotRecords
            .AsNoTracking()
            .Include("Bag.Bean")
            .Include(s => s.Machine)
            .Include(s => s.Grinder)
            .Include(s => s.MadeBy)
            .Include(s => s.MadeFor)
            .Include("ShotEquipments.Equipment")
            .Where(s => !s.IsDeleted)
            .ToListAsync();
            
        return allShots
            .OrderByDescending(s => s.Timestamp)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();
    }
    
    public async Task<List<ShotRecord>> GetByUserAsync(int userProfileId, int pageIndex, int pageSize)
    {
        var targetUserProfileId = userProfileId;
        var allShots = await _context.ShotRecords
            .AsNoTracking()
            .Include("Bag.Bean")
            .Include(s => s.Machine)
            .Include(s => s.Grinder)
            .Include(s => s.MadeBy)
            .Include(s => s.MadeFor)
            .Where(s => !s.IsDeleted
                && (s.MadeById == targetUserProfileId || s.MadeForId == targetUserProfileId))
            .ToListAsync();
            
        return allShots
            .OrderByDescending(s => s.Timestamp)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();
    }
    
    public async Task<List<ShotRecord>> GetByBeanAsync(int beanId, int pageIndex, int pageSize)
    {
        var targetBeanId = beanId;
        var allShots = await _context.ShotRecords
            .AsNoTracking()
            .Include("Bag.Bean")
            .Include(s => s.Machine)
            .Include(s => s.Grinder)
            .Include(s => s.MadeBy)
            .Include(s => s.MadeFor)
            .Where(s => !s.IsDeleted && s.Bag.BeanId == targetBeanId) // TODO T038-T039: Query through Bag
            .ToListAsync();
            
        return allShots
            .OrderByDescending(s => s.Timestamp)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();
    }
    
    public async Task<List<ShotRecord>> GetByEquipmentAsync(int equipmentId, int pageIndex, int pageSize)
    {
        var targetEquipmentId = equipmentId;
        var allShots = await _context.ShotRecords
            .AsNoTracking()
            .Include("Bag.Bean")
            .Include(s => s.Machine)
            .Include(s => s.Grinder)
            .Include(s => s.MadeBy)
            .Include(s => s.MadeFor)
            .Include("ShotEquipments.Equipment")
            .Where(s => !s.IsDeleted && 
                (s.MachineId == targetEquipmentId ||
                 s.GrinderId == targetEquipmentId ||
                 s.ShotEquipments.Any(se => se.EquipmentId == targetEquipmentId)))
            .ToListAsync();
            
        return allShots
            .OrderByDescending(s => s.Timestamp)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();
    }
    
    public async Task<int> GetTotalCountAsync()
    {
        return await _context.ShotRecords.Where(s => !s.IsDeleted).CountAsync();
    }
    
    public async Task<List<ShotRecord>> GetFilteredAsync(ShotFilterCriteriaDto? criteria, int pageIndex, int pageSize)
    {
        var allShots = await _context.ShotRecords
            .AsNoTracking()
            .Include("Bag.Bean")
            .Include(s => s.Machine)
            .Include(s => s.Grinder)
            .Include(s => s.MadeBy)
            .Include(s => s.MadeFor)
            .Include("ShotEquipments.Equipment")
            .Where(s => !s.IsDeleted)
            .ToListAsync();
        
        // Apply filters in memory after loading (SQLite limitations with complex queries)
        var filtered = allShots.AsEnumerable();
        
        if (criteria?.BeanIds?.Count > 0)
        {
            filtered = filtered.Where(s => s.Bag != null && criteria.BeanIds.Contains(s.Bag.BeanId));
        }
        
        if (criteria?.MadeForIds?.Count > 0)
        {
            filtered = filtered.Where(s => s.MadeForId != null && criteria.MadeForIds.Contains(s.MadeForId.Value));
        }
        
        if (criteria?.Ratings?.Count > 0)
        {
            filtered = filtered.Where(s => s.Rating != null && criteria.Ratings.Contains(s.Rating.Value));
        }
        
        return filtered
            .OrderByDescending(s => s.Timestamp)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .ToList();
    }
    
    public async Task<int> GetFilteredCountAsync(ShotFilterCriteriaDto? criteria)
    {
        var allShots = await _context.ShotRecords
            .AsNoTracking()
            .Include(s => s.Bag)
            .Where(s => !s.IsDeleted)
            .ToListAsync();
        
        var filtered = allShots.AsEnumerable();
        
        if (criteria?.BeanIds?.Count > 0)
        {
            filtered = filtered.Where(s => s.Bag != null && criteria.BeanIds.Contains(s.Bag.BeanId));
        }
        
        if (criteria?.MadeForIds?.Count > 0)
        {
            filtered = filtered.Where(s => s.MadeForId != null && criteria.MadeForIds.Contains(s.MadeForId.Value));
        }
        
        if (criteria?.Ratings?.Count > 0)
        {
            filtered = filtered.Where(s => s.Rating != null && criteria.Ratings.Contains(s.Rating.Value));
        }
        
        return filtered.Count();
    }
    
    public async Task<List<int>> GetBeanIdsWithShotsAsync()
    {
        var shots = await _context.ShotRecords
            .AsNoTracking()
            .Include(s => s.Bag)
            .Where(s => !s.IsDeleted && s.Bag != null)
            .ToListAsync();
        
        return shots
            .Where(s => s.Bag != null)
            .Select(s => s.Bag!.BeanId)
            .Distinct()
            .ToList();
    }
    
    public async Task<List<int>> GetMadeForIdsWithShotsAsync()
    {
        var shots = await _context.ShotRecords
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.MadeForId != null)
            .ToListAsync();
        
        return shots
            .Where(s => s.MadeForId != null)
            .Select(s => s.MadeForId!.Value)
            .Distinct()
            .ToList();
    }

    public async Task<ShotRecord?> GetMostRecentWithGrindAsync(int grinderId, int? beanId = null, BrewMethod? method = null)
    {
        var targetGrinderId = grinderId;
        var candidates = await _context.ShotRecords
            .AsNoTracking()
            .Include(s => s.Bag)
            .Where(s => !s.IsDeleted
                && s.GrinderId == targetGrinderId
                && s.GrindMicrons != null)
            .ToListAsync();

        return candidates
            .Where(s => !method.HasValue || s.BrewMethod == method.Value)
            .Where(s => !beanId.HasValue || (s.Bag != null && s.Bag.BeanId == beanId.Value))
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefault();
    }

    public async Task<int?> GetMostRecentMicronsByBeanAsync(int beanId, BrewMethod method)
    {
        var targetBeanId = beanId;
        var targetMethod = method;
        return await _context.ShotRecords
            .AsNoTracking()
            .Include(s => s.Bag)
            .Where(s => !s.IsDeleted
                && s.GrindMicrons != null
                && s.BrewMethod == targetMethod
                && s.Bag != null
                && s.Bag.BeanId == targetBeanId)
            .OrderByDescending(s => s.Timestamp)
            .Select(s => s.GrindMicrons)
            .FirstOrDefaultAsync();
    }
}
