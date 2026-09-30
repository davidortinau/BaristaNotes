using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public class BeanDraft
{
    public int? BeanId { get; set; }
    public string Name { get; set; } = "";
    public string Roaster { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Notes { get; set; } = "";
    public string RoasterUrl { get; set; } = "";
    public bool IsEditing => BeanId is > 0;
    public string? ValidationError => string.IsNullOrWhiteSpace(Name) ? "Bean name is required" : null;

    public void ApplyLoadedData(BeanDto bean)
    {
        ArgumentNullException.ThrowIfNull(bean);
        BeanId = bean.Id;
        Name = bean.Name;
        Roaster = bean.Roaster ?? "";
        Origin = bean.Origin ?? "";
        Notes = bean.Notes ?? "";
        RoasterUrl = bean.RoasterUrl ?? "";
    }

    public CreateBeanDto ToCreateDto() => new()
    {
        Name = Name,
        Roaster = Optional(Roaster),
        Origin = Optional(Origin),
        Notes = Optional(Notes),
        RoasterUrl = string.IsNullOrWhiteSpace(RoasterUrl) ? null : RoasterUrl.Trim()
    };

    public UpdateBeanDto ToUpdateDto() => new()
    {
        Name = Name,
        Roaster = FieldUpdate<string?>.Set(Optional(Roaster)),
        Origin = FieldUpdate<string?>.Set(Optional(Origin)),
        Notes = FieldUpdate<string?>.Set(Optional(Notes)),
        RoasterUrl = string.IsNullOrWhiteSpace(RoasterUrl) ? "" : RoasterUrl.Trim()
    };

    private static string? Optional(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

public sealed class BeanWorkflow(
    IBeanService beans,
    IDataChangeNotifier notifier,
    ILogger<BeanWorkflow> logger)
{
    public async Task<BeanDto> UpdateAsync(BeanDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.BeanId is not > 0)
            throw new InvalidOperationException("A saved bean is required for an update.");
        if (draft.ValidationError is { } error)
        {
            throw new ValidationException(new Dictionary<string, List<string>>
            {
                [nameof(BeanDraft.Name)] = [error]
            });
        }

        var saved = await beans.UpdateBeanAsync(draft.BeanId.Value, draft.ToUpdateDto());
        notifier.NotifyDataChanged(DataChangeType.BeanUpdated, draft.BeanId.Value);
        logger.LogInformation("Updated bean {BeanId}", draft.BeanId.Value);
        return saved;
    }

    public async Task DeleteAsync(int beanId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(beanId);
        await beans.DeleteBeanAsync(beanId);
        notifier.NotifyDataChanged(DataChangeType.BeanUpdated, beanId);
        logger.LogInformation("Deleted bean {BeanId}", beanId);
    }
}
