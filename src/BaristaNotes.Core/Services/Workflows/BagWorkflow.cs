using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public class BagDraft
{
    public int? BagId { get; set; }
    public int BeanId { get; set; }
    public string BeanName { get; set; } = "";
    public DateTime RoastDate { get; set; } = DateTime.Now;
    public string Notes { get; set; } = "";
    public bool IsComplete { get; set; }
    public bool IsEditing => BagId is > 0;

    public string? GetValidationError(DateTime today)
    {
        if (RoastDate.Date > today.Date)
            return "Roast date cannot be in the future";
        if (!string.IsNullOrEmpty(Notes) && Notes.Length > 500)
            return "Notes cannot exceed 500 characters";
        return null;
    }

    public void ApplyLoadedData(Bag bag, string fallbackBeanName)
    {
        ArgumentNullException.ThrowIfNull(bag);
        BagId = bag.Id;
        BeanId = bag.BeanId;
        BeanName = bag.Bean?.Name ?? fallbackBeanName;
        RoastDate = bag.RoastDate;
        Notes = bag.Notes ?? "";
        IsComplete = bag.IsComplete;
    }

    public Bag ToEntity() => new()
    {
        Id = BagId ?? 0,
        BeanId = BeanId,
        RoastDate = RoastDate,
        Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes,
        IsComplete = IsComplete
    };
}

public sealed class BagWorkflow(
    IBagService bags,
    IDataChangeNotifier notifier,
    ILogger<BagWorkflow> logger)
{
    public async Task<OperationResult<Bag>> SaveAsync(BagDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.GetValidationError(DateTime.Now) is { } error)
            return OperationResult<Bag>.Fail(error);

        var bag = draft.ToEntity();
        var result = draft.IsEditing
            ? await bags.UpdateBagAsync(bag)
            : await bags.CreateBagAsync(bag);
        if (result.Success)
        {
            notifier.NotifyDataChanged(
                draft.IsEditing ? DataChangeType.BagUpdated : DataChangeType.BagCreated, result.Data);
            logger.LogInformation("Saved bag {BagId}", result.Data?.Id);
        }
        else
        {
            logger.LogWarning("Could not save bag for bean {BeanId}: {Error}", draft.BeanId, result.ErrorMessage);
        }
        return result;
    }

    public async Task DeleteAsync(int bagId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bagId);
        await bags.DeleteBagAsync(bagId);
        notifier.NotifyDataChanged(DataChangeType.BagUpdated, bagId);
        logger.LogInformation("Deleted bag {BagId}", bagId);
    }
}
