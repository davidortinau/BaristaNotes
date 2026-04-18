using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Service implementation for managing coffee bags (T037).
/// Handles bag CRUD operations, validation, and completion management.
/// </summary>
public class BagService : IBagService
{
    private readonly IBagRepository _bagRepository;
    private readonly IBeanRepository _beanRepository;
    private readonly ILogger<BagService> _logger;

    public BagService(
        IBagRepository bagRepository,
        IBeanRepository beanRepository,
        ILogger<BagService>? logger = null)
    {
        _bagRepository = bagRepository;
        _beanRepository = beanRepository;
        _logger = logger ?? NullLogger<BagService>.Instance;
    }

    public async Task<OperationResult<Bag>> CreateBagAsync(Bag bag)
    {
        // Validation
        var validation = ValidateBag(bag);
        if (!validation.Success)
        {
            return validation;
        }

        // Verify bean exists
        var bean = await _beanRepository.GetByIdAsync(bag.BeanId);
        if (bean == null)
        {
            return OperationResult<Bag>.Fail("Bean not found");
        }

        try
        {
            var created = await _bagRepository.CreateAsync(bag);
            return OperationResult<Bag>.Ok(created);
        }
        catch (Exception ex)
        {
            return OperationResult<Bag>.Fail($"Failed to create bag: {ex.Message}");
        }
    }

    public async Task<OperationResult<BagSummaryDto>> CreateNewBagForBeanAsync(int beanId, DateTime roastDate, string? notes = null)
    {
        if (beanId <= 0)
        {
            return OperationResult<BagSummaryDto>.Fail("Bean not found");
        }

        var bean = await _beanRepository.GetByIdAsync(beanId);
        if (bean == null || bean.IsDeleted || !bean.IsActive)
        {
            return OperationResult<BagSummaryDto>.Fail("Bean not found");
        }

        if (roastDate.Date > DateTime.Today)
        {
            return OperationResult<BagSummaryDto>.Fail("Roast date cannot be in the future.");
        }

        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes;

        var bag = new Bag
        {
            BeanId = beanId,
            RoastDate = roastDate,
            Notes = normalizedNotes,
            IsComplete = false,
            IsActive = true
        };

        try
        {
            var created = await _bagRepository.CreateAsync(bag);

            _logger.LogInformation(
                "Created bag {BagId} for bean {BeanId} with roast date {RoastDate}",
                created.Id, beanId, created.RoastDate);

            var dto = new BagSummaryDto
            {
                Id = created.Id,
                BeanId = created.BeanId,
                BeanName = bean.Name,
                RoastDate = created.RoastDate,
                Notes = created.Notes,
                IsComplete = created.IsComplete,
                ShotCount = 0,
                AverageRating = null
            };

            return OperationResult<BagSummaryDto>.Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create bag for bean {BeanId}", beanId);
            return OperationResult<BagSummaryDto>.Fail($"Failed to create bag: {ex.Message}");
        }
    }

    public async Task<Bag?> GetBagByIdAsync(int id)
    {
        return await _bagRepository.GetByIdAsync(id);
    }

    public async Task<List<Bag>> GetBagsForBeanAsync(int beanId, bool includeCompleted = true)
    {
        return await _bagRepository.GetBagsForBeanAsync(beanId, includeCompleted);
    }

    public async Task<List<BagSummaryDto>> GetActiveBagsForShotLoggingAsync()
    {
        return await _bagRepository.GetActiveBagsForShotLoggingAsync();
    }

    public async Task<List<BagSummaryDto>> GetBagSummariesForBeanAsync(int beanId, bool includeCompleted = true)
    {
        return await _bagRepository.GetBagSummariesForBeanAsync(beanId, includeCompleted);
    }

    public async Task<Bag?> GetMostRecentActiveBagForBeanAsync(int beanId)
    {
        return await _bagRepository.GetMostRecentActiveBagForBeanAsync(beanId);
    }

    public async Task<OperationResult<Bag>> UpdateBagAsync(Bag bag)
    {
        // Verify bag exists
        var existing = await _bagRepository.GetByIdAsync(bag.Id);
        if (existing == null)
        {
            return OperationResult<Bag>.Fail("Bag not found");
        }

        // Validation
        var validation = ValidateBag(bag);
        if (!validation.Success)
        {
            return validation;
        }

        try
        {
            // Update the tracked entity's properties instead of passing a new entity
            existing.RoastDate = bag.RoastDate;
            existing.Notes = bag.Notes;
            existing.IsComplete = bag.IsComplete;
            existing.LastModifiedAt = DateTime.Now;

            var updated = await _bagRepository.UpdateAsync(existing);
            return OperationResult<Bag>.Ok(updated);
        }
        catch (Exception ex)
        {
            return OperationResult<Bag>.Fail($"Failed to update bag: {ex.Message}");
        }
    }

    public async Task MarkBagCompleteAsync(int id)
    {
        var bag = await _bagRepository.GetByIdAsync(id);
        if (bag == null)
        {
            throw new EntityNotFoundException("Bag", id);
        }

        bag.IsComplete = true;
        bag.LastModifiedAt = DateTime.Now;
        await _bagRepository.UpdateAsync(bag);
    }

    public async Task ReactivateBagAsync(int id)
    {
        var bag = await _bagRepository.GetByIdAsync(id);
        if (bag == null)
        {
            throw new EntityNotFoundException("Bag", id);
        }

        bag.IsComplete = false;
        bag.LastModifiedAt = DateTime.Now;
        await _bagRepository.UpdateAsync(bag);
    }

    public async Task DeleteBagAsync(int id)
    {
        var bag = await _bagRepository.GetByIdAsync(id);
        if (bag == null)
        {
            throw new EntityNotFoundException("Bag", id);
        }

        await _bagRepository.DeleteAsync(id);
    }

    #region Validation

    private OperationResult<Bag> ValidateBag(Bag bag)
    {
        // RoastDate cannot be in the future
        if (bag.RoastDate > DateTime.Now)
        {
            return OperationResult<Bag>.Fail("Roast date cannot be in the future");
        }

        // Notes must be ≤500 characters
        if (bag.Notes != null && bag.Notes.Length > 500)
        {
            return OperationResult<Bag>.Fail("Notes cannot exceed 500 characters");
        }

        return OperationResult<Bag>.Ok(bag);
    }

    #endregion
}
