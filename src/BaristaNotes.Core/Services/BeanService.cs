using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Core.Services;

public class BeanService : IBeanService
{
    private readonly IBeanRepository _beanRepository;
    private readonly IRatingService _ratingService;
    private readonly ILogger<BeanService> _logger;

    public BeanService(IBeanRepository beanRepository, IRatingService ratingService)
        : this(beanRepository, ratingService, NullLogger<BeanService>.Instance)
    {
    }

    public BeanService(IBeanRepository beanRepository, IRatingService ratingService, ILogger<BeanService> logger)
    {
        _beanRepository = beanRepository;
        _ratingService = ratingService;
        _logger = logger;
    }

    public async Task<List<BeanDto>> GetAllActiveBeansAsync()
    {
        var beans = await _beanRepository.GetActiveBeansAsync();
        return beans.Select(MapToDto).ToList();
    }

    public async Task<BeanDto?> GetBeanByIdAsync(int id)
    {
        var bean = await _beanRepository.GetByIdAsync(id);
        return bean == null ? null : MapToDto(bean);
    }

    public async Task<BeanDto?> GetBeanWithRatingsAsync(int id)
    {
        var bean = await _beanRepository.GetByIdAsync(id);
        if (bean == null) return null;

        var ratings = await _ratingService.GetBeanRatingAsync(id);

        return MapToDto(bean) with { RatingAggregate = ratings };
    }

    public async Task<OperationResult<BeanDto>> CreateBeanAsync(CreateBeanDto dto)
    {
        try
        {
            ValidateCreateBean(dto);
        }
        catch (ValidationException ex)
        {
            var firstError = ex.Errors.FirstOrDefault();
            var errorMessage = firstError.Value?.FirstOrDefault() ?? "Validation failed";
            return OperationResult<BeanDto>.Fail(
                errorMessage,
                "Please correct the errors and try again"
            );
        }

        try
        {
            var bean = new Bean
            {
                Name = dto.Name,
                Roaster = dto.Roaster,
                Origin = dto.Origin,
                Notes = dto.Notes,
                IsActive = true,
                CreatedAt = DateTime.Now,
                SyncId = Guid.NewGuid(),
                LastModifiedAt = DateTime.Now
            };

            var created = await _beanRepository.AddAsync(bean);
            return OperationResult<BeanDto>.Ok(MapToDto(created), $"{dto.Name} saved successfully");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "DbUpdateException creating bean Name={BeanName}", dto.Name);
            return OperationResult<BeanDto>.Fail(
                "Failed to save bean to database",
                "Check your connection and try again",
                "DB_UPDATE_FAILED"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error creating bean Name={BeanName}", dto.Name);
            return OperationResult<BeanDto>.Fail(
                "An unexpected error occurred",
                "Please try again later",
                "UNKNOWN_ERROR"
            );
        }
    }

    public async Task<BeanDto> UpdateBeanAsync(int id, UpdateBeanDto dto)
    {
        var bean = await _beanRepository.GetByIdAsync(id);
        if (bean == null)
            throw new EntityNotFoundException(nameof(Bean), id);

        if (dto.Name != null)
            bean.Name = dto.Name;
        if (dto.Roaster != null)
            bean.Roaster = dto.Roaster;
        if (dto.Origin != null)
            bean.Origin = dto.Origin;
        if (dto.Notes != null)
            bean.Notes = dto.Notes;
        if (dto.IsActive.HasValue)
            bean.IsActive = dto.IsActive.Value;

        bean.LastModifiedAt = DateTime.Now;

        var updated = await _beanRepository.UpdateAsync(bean);
        return MapToDto(updated);
    }

    public async Task ArchiveBeanAsync(int id)
    {
        var bean = await _beanRepository.GetByIdAsync(id);
        if (bean == null)
            throw new EntityNotFoundException(nameof(Bean), id);

        bean.IsActive = false;
        bean.LastModifiedAt = DateTime.Now;
        await _beanRepository.UpdateAsync(bean);
    }

    public async Task DeleteBeanAsync(int id)
    {
        var bean = await _beanRepository.GetByIdAsync(id);
        if (bean == null)
            throw new EntityNotFoundException(nameof(Bean), id);

        bean.IsDeleted = true;
        bean.LastModifiedAt = DateTime.Now;
        await _beanRepository.UpdateAsync(bean);
    }

    public async Task<IReadOnlyList<BeanDto>> GetRecentBeansAsync(int limit = 6, int withinDays = 90)
    {
        _logger.LogDebug("Fetching recent beans Limit={Limit} WithinDays={WithinDays}", limit, withinDays);

        if (limit <= 0)
            return Array.Empty<BeanDto>();

        var cutoff = DateTime.Now.AddDays(-Math.Abs(withinDays));
        var beans = await _beanRepository.GetActiveBeansWithActivityAsync();

        var ranked = beans
            .Select(b =>
            {
                DateTime? lastBag = b.Bags
                    .Where(bag => !bag.IsDeleted)
                    .Select(bag => (DateTime?)bag.CreatedAt)
                    .DefaultIfEmpty(null)
                    .Max();

                DateTime? lastShot = b.Bags
                    .Where(bag => !bag.IsDeleted)
                    .SelectMany(bag => bag.ShotRecords)
                    .Where(s => !s.IsDeleted)
                    .Select(s => (DateTime?)s.Timestamp)
                    .DefaultIfEmpty(null)
                    .Max();

                DateTime? lastActivity = null;
                if (lastBag.HasValue && lastShot.HasValue)
                    lastActivity = lastBag.Value > lastShot.Value ? lastBag : lastShot;
                else
                    lastActivity = lastBag ?? lastShot;

                return (Bean: b, LastActivity: lastActivity);
            })
            .Where(x => x.LastActivity.HasValue && x.LastActivity.Value >= cutoff)
            .OrderByDescending(x => x.LastActivity!.Value)
            .Take(limit)
            .Select(x => MapToDto(x.Bean))
            .ToList();

        _logger.LogDebug("Returning {Count} recent beans", ranked.Count);
        return ranked;
    }

    public async Task<BeanDto?> FuzzyFindByNameRoasterAsync(string name, string? roaster)
    {
        _logger.LogDebug("Fuzzy find Name={BeanName} Roaster={Roaster}", name, roaster);

        var normalizedName = StringSimilarity.Normalize(name);
        if (string.IsNullOrEmpty(normalizedName))
            return null;

        var normalizedRoaster = StringSimilarity.Normalize(roaster);
        var hasRoaster = !string.IsNullOrEmpty(normalizedRoaster);

        var candidates = await _beanRepository.GetNonDeletedBeansAsync();

        bool RoasterMatches(Bean b)
        {
            if (!hasRoaster) return true;
            var bNorm = StringSimilarity.Normalize(b.Roaster);
            return bNorm == normalizedRoaster;
        }

        // 1. Exact (normalized) name match, roaster must match if provided.
        var exact = candidates.FirstOrDefault(b =>
            StringSimilarity.Normalize(b.Name) == normalizedName && RoasterMatches(b));
        if (exact != null)
        {
            _logger.LogDebug("Fuzzy find hit (exact) BeanId={BeanId}", exact.Id);
            return MapToDto(exact);
        }

        // 2. Near match: only when a roaster was provided. Levenshtein ≤ 2 on name
        //    AND roaster must match (normalized).
        if (hasRoaster)
        {
            Bean? best = null;
            var bestDistance = int.MaxValue;
            foreach (var b in candidates)
            {
                if (!RoasterMatches(b)) continue;
                var bNameNorm = StringSimilarity.Normalize(b.Name);
                if (string.IsNullOrEmpty(bNameNorm)) continue;
                var d = StringSimilarity.Levenshtein(normalizedName, bNameNorm);
                if (d <= 2 && d < bestDistance)
                {
                    best = b;
                    bestDistance = d;
                }
            }
            if (best != null)
            {
                _logger.LogDebug("Fuzzy find hit (near Distance={Distance}) BeanId={BeanId}", bestDistance, best.Id);
                return MapToDto(best);
            }
        }

        _logger.LogDebug("Fuzzy find miss Name={BeanName} Roaster={Roaster}", name, roaster);
        return null;
    }

    public async Task<IReadOnlyList<string>> GetDistinctRoastersAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _logger.LogDebug("Fetching distinct roasters");
        var beans = await _beanRepository.GetNonDeletedBeansAsync();
        var result = beans
            .Select(b => b.Roaster)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r!.Trim())
            .Where(r => r.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return result;
    }

    public async Task<IReadOnlyList<string>> GetDistinctOriginsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _logger.LogDebug("Fetching distinct origins");
        var beans = await _beanRepository.GetNonDeletedBeansAsync();
        var result = beans
            .Select(b => b.Origin)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o!.Trim())
            .Where(o => o.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(o => o, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return result;
    }

    private BeanDto MapToDto(Bean bean) => new()
    {
        Id = bean.Id,
        Name = bean.Name,
        Roaster = bean.Roaster,
        RoastDate = null,
        Origin = bean.Origin,
        Notes = bean.Notes,
        IsActive = bean.IsActive,
        CreatedAt = bean.CreatedAt
    };

    private void ValidateCreateBean(CreateBeanDto dto)
    {
        var errors = new Dictionary<string, List<string>>();

        if (string.IsNullOrWhiteSpace(dto.Name))
            errors.Add(nameof(dto.Name), new List<string> { "Name is required" });
        else if (dto.Name.Length > 100)
            errors.Add(nameof(dto.Name), new List<string> { "Name must be 100 characters or less" });

        if (dto.Roaster?.Length > 100)
            errors.Add(nameof(dto.Roaster), new List<string> { "Roaster must be 100 characters or less" });

        if (dto.Origin?.Length > 100)
            errors.Add(nameof(dto.Origin), new List<string> { "Origin must be 100 characters or less" });

        if (dto.Notes?.Length > 500)
            errors.Add(nameof(dto.Notes), new List<string> { "Notes must be 500 characters or less" });

        if (dto.RoastDate.HasValue && dto.RoastDate.Value > DateTime.Now)
            errors.Add(nameof(dto.RoastDate), new List<string> { "Roast date cannot be in the future" });

        if (errors.Any())
            throw new ValidationException(errors);
    }
}
