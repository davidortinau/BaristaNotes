using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public sealed record BeanCreationResult(
    OperationResult<BeanDto> Bean,
    OperationResult<BagSummaryDto>? InitialBag);

public sealed class BeanCreationWorkflow(
    IBeanService beans,
    IBagService bags,
    IDataChangeNotifier changes,
    ILogger<BeanCreationWorkflow> logger)
{
    public async Task<BeanCreationResult> CreateWithInitialBagAsync(CreateBeanDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var bean = await beans.CreateBeanAsync(input);
        if (!bean.Success || bean.Data is null)
            return new BeanCreationResult(bean, null);

        changes.NotifyDataChanged(DataChangeType.BeanCreated, bean.Data);
        var bag = await bags.CreateNewBagForBeanAsync(bean.Data.Id, DateTime.Today);
        if (bag.Success && bag.Data is not null)
        {
            changes.NotifyDataChanged(DataChangeType.BagCreated, bag.Data.Id);
        }
        else
        {
            logger.LogWarning(
                "Bean {BeanId} was created but its initial bag was not: {ErrorMessage}",
                bean.Data.Id, bag.ErrorMessage);
        }

        return new BeanCreationResult(bean, bag);
    }
}
