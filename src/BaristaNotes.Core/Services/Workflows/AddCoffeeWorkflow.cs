using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public sealed class AddCoffeeDraft
{
    public string Name { get; set; } = "";
    public string Roaster { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime RoastDate { get; set; } = DateTime.Today;
    public string? BagNotes => string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();

    public string? GetValidationError(DateTime today)
    {
        if (string.IsNullOrWhiteSpace(Name))
            return "Bean name is required";
        return RoastDate.Date > today.Date ? "Roast date cannot be in the future" : null;
    }

    public CreateBeanDto ToCreateDto() => new()
    {
        Name = Name.Trim(),
        Roaster = string.IsNullOrWhiteSpace(Roaster) ? null : Roaster.Trim(),
        Origin = string.IsNullOrWhiteSpace(Origin) ? null : Origin.Trim(),
        Notes = null
    };
}

public sealed class AddCoffeeWorkflow(
    IBeanService beans,
    IBagService bags,
    ILogger<AddCoffeeWorkflow> logger)
{
    public Task<OperationResult<BagSummaryDto>> AddBagForExistingBeanAsync(int beanId) =>
        bags.CreateNewBagForBeanAsync(beanId, DateTime.Today);

    public async Task<BeanCreationResult> CreateAsync(AddCoffeeDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.GetValidationError(DateTime.Today) is { } error)
            return new(OperationResult<BeanDto>.Fail(error), null);

        var beanInput = draft.ToCreateDto();
        var roastDate = draft.RoastDate;
        var notes = draft.BagNotes;
        var bean = await beans.CreateBeanAsync(beanInput);
        if (!bean.Success || bean.Data is null)
            return new(bean, null);

        var bag = await bags.CreateNewBagForBeanAsync(bean.Data.Id, roastDate, notes);
        if (!bag.Success || bag.Data is null)
            logger.LogWarning("Coffee bean {BeanId} was created but its bag was not: {Error}", bean.Data.Id, bag.ErrorMessage);
        return new(bean, bag);
    }
}
