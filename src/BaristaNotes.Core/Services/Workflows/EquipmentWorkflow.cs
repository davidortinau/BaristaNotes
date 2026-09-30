using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public class EquipmentDraft
{
    public int? EquipmentId { get; set; }
    public string Name { get; set; } = "";
    public EquipmentType SelectedType { get; set; } = EquipmentType.Machine;
    public string Notes { get; set; } = "";
    public bool IsEditing => EquipmentId is > 0;
    public string? ValidationError => string.IsNullOrWhiteSpace(Name) ? "Equipment name is required" : null;

    public void ApplyLoadedData(EquipmentDto equipment)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        EquipmentId = equipment.Id;
        Name = equipment.Name;
        SelectedType = equipment.Type;
        Notes = equipment.Notes ?? "";
    }
}

public sealed class EquipmentWorkflow(
    IEquipmentService equipment,
    IDataChangeNotifier notifier,
    ILogger<EquipmentWorkflow> logger)
{
    public async Task<EquipmentDto> SaveAsync(EquipmentDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.ValidationError is { } error)
        {
            throw new ValidationException(new Dictionary<string, List<string>>
            {
                [nameof(EquipmentDraft.Name)] = [error]
            });
        }

        var notes = string.IsNullOrWhiteSpace(draft.Notes) ? null : draft.Notes;
        EquipmentDto saved;
        if (draft.EquipmentId is > 0)
        {
            saved = await equipment.UpdateEquipmentAsync(draft.EquipmentId.Value, new UpdateEquipmentDto
            {
                Name = draft.Name,
                Type = draft.SelectedType,
                Notes = FieldUpdate<string?>.Set(notes)
            });
            notifier.NotifyDataChanged(DataChangeType.EquipmentUpdated, draft.EquipmentId.Value);
        }
        else
        {
            saved = await equipment.CreateEquipmentAsync(new CreateEquipmentDto
            {
                Name = draft.Name,
                Type = draft.SelectedType,
                Notes = notes
            });
            notifier.NotifyDataChanged(DataChangeType.EquipmentCreated, saved);
        }

        logger.LogInformation("Saved equipment {EquipmentId} in {SaveMode} mode",
            saved.Id, draft.IsEditing ? "edit" : "create");
        return saved;
    }

    public async Task ArchiveAsync(int equipmentId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(equipmentId);
        await equipment.ArchiveEquipmentAsync(equipmentId);
        notifier.NotifyDataChanged(DataChangeType.EquipmentUpdated, equipmentId);
        logger.LogInformation("Archived equipment {EquipmentId}", equipmentId);
    }
}
