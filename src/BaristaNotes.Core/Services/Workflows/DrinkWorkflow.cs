using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public sealed class DrinkWorkflow(
    DatabaseInitializationService initialization,
    IShotService shots,
    IBagService bags,
    IUserProfileService profiles,
    IEquipmentService equipment,
    IPreferencesService preferences,
    IDrinkValueRangeService ranges,
    ILogger<DrinkWorkflow> logger)
{
    public async Task<DrinkLoadResult> LoadAsync(int? editingShotId = null)
    {
        logger.LogDebug("Loading drink editor for shot {ShotId}", editingShotId);
        await initialization.InitializeAsync();

        var availableBags = await bags.GetActiveBagsForShotLoggingAsync();
        var availableUsers = await profiles.GetAllProfilesAsync();
        var availableEquipment = await equipment.GetAllActiveEquipmentAsync();
        var shot = editingShotId.HasValue
            ? await shots.GetShotByIdAsync(editingShotId.Value)
            : await shots.GetMostRecentShotAsync();

        if (editingShotId.HasValue && shot is null)
            throw new EntityNotFoundException(nameof(ShotRecord), editingShotId.Value);

        return new DrinkLoadResult(
            editingShotId, shot, availableBags, availableUsers, availableEquipment);
    }

    public void ApplyLoadedData(DrinkDraft draft, DrinkLoadResult loaded)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(loaded);

        draft.AvailableBags = loaded.Bags;
        draft.AvailableUsers = loaded.Users;
        draft.AvailableEquipment = loaded.Equipment;

        if (loaded.Shot is { } shot)
        {
            draft.BrewMethod = shot.BrewMethod;
            draft.DrinkType = shot.DrinkType;
            draft.DoseIn = shot.DoseIn;
            draft.GrindMicrons = shot.GrindMicrons;
            draft.WaterTempC = shot.WaterTempC;
            draft.ExpectedTime = shot.ExpectedTime;
            draft.ExpectedOutput = shot.ExpectedOutput;
            draft.Rating = shot.Rating ?? 2;
            draft.SelectedBagId = shot.Bag?.Id;

            var validDrinks = shot.BrewMethod.DrinkTypesFor();
            if (!validDrinks.Contains(draft.DrinkType))
                draft.DrinkType = validDrinks[0];

            if (loaded.EditingShotId.HasValue)
            {
                draft.BeanName = shot.Bean?.Name;
                draft.ActualTime = shot.ActualTime;
                draft.ActualOutput = shot.ActualOutput;
                draft.SelectedMaker = shot.MadeBy;
                draft.SelectedRecipient = shot.MadeFor;
                draft.SelectedMachineId = shot.Machine?.Id;
                draft.SelectedGrinderId = shot.Grinder?.Id;
                draft.SelectedAccessoryIds = shot.Accessories?.Select(item => item.Id).ToList() ?? [];
                draft.TastingNotes = shot.TastingNotes;
            }
        }

        if (!loaded.EditingShotId.HasValue)
        {
            var makerId = preferences.GetLastMadeById();
            var recipientId = preferences.GetLastMadeForId();
            if (makerId.HasValue)
                draft.SelectedMaker = loaded.Users.FirstOrDefault(user => user.Id == makerId);
            if (recipientId.HasValue)
                draft.SelectedRecipient = loaded.Users.FirstOrDefault(user => user.Id == recipientId);

            draft.SelectedMachineId = preferences.GetLastMachineId();
            draft.SelectedGrinderId = preferences.GetLastGrinderId();
            draft.SelectedAccessoryIds = preferences.GetLastAccessoryIds();
        }

        draft.TempUnit = preferences.GetTemperatureUnit();
    }

    public void ChangeBrewMethod(DrinkDraft draft, BrewMethod method, bool isEditing)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.BrewMethod == method)
            return;

        if (!isEditing)
        {
            draft.DoseIn = ranges.Resolve(DrinkValueMetric.DoseIn, method).Default;
            draft.ExpectedOutput = ranges.Resolve(DrinkValueMetric.Yield, method).Default;
            draft.ExpectedTime = ranges.Resolve(DrinkValueMetric.Time, method).Default;
            draft.ActualTime = null;
            draft.ActualOutput = null;
        }

        var validDrinks = method.DrinkTypesFor();
        if (!validDrinks.Contains(draft.DrinkType))
            draft.DrinkType = validDrinks[0];
        draft.BrewMethod = method;
    }

    public async Task<ShotRecordDto> SaveAsync(DrinkDraft draft, int? editingShotId = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (draft.SelectedBagId is not int bagId)
        {
            throw new ValidationException(new Dictionary<string, List<string>>
            {
                [nameof(CreateShotDto.BagId)] = ["Please select a bag"]
            });
        }

        ShotRecordDto saved;
        if (editingShotId.HasValue)
        {
            saved = await shots.UpdateShotAsync(editingShotId.Value, new UpdateShotDto
            {
                BagId = bagId,
                MachineId = FieldUpdate<int?>.Set(draft.SelectedMachineId),
                GrinderId = FieldUpdate<int?>.Set(draft.SelectedGrinderId),
                AccessoryIds = draft.SelectedAccessoryIds,
                MadeById = draft.SelectedMaker?.Id,
                MadeForId = draft.SelectedRecipient?.Id,
                DoseIn = draft.DoseIn,
                GrindMicrons = draft.GrindMicrons,
                WaterTempC = draft.WaterTempC,
                ExpectedTime = draft.ExpectedTime,
                ExpectedOutput = draft.ExpectedOutput,
                ActualTime = draft.ActualTime,
                ActualOutput = draft.ActualOutput,
                Rating = FieldUpdate<int?>.Set(draft.Rating),
                DrinkType = draft.DrinkType,
                BrewMethod = draft.BrewMethod,
                TastingNotes = FieldUpdate<string?>.Set(draft.TastingNotes)
            });
        }
        else
        {
            saved = await shots.CreateShotAsync(new CreateShotDto
            {
                BagId = bagId,
                MachineId = draft.SelectedMachineId,
                GrinderId = draft.SelectedGrinderId,
                AccessoryIds = draft.SelectedAccessoryIds,
                MadeById = draft.SelectedMaker?.Id,
                MadeForId = draft.SelectedRecipient?.Id,
                DoseIn = draft.DoseIn,
                GrindMicrons = draft.GrindMicrons,
                WaterTempC = draft.WaterTempC,
                ExpectedTime = draft.ExpectedTime,
                ExpectedOutput = draft.ExpectedOutput,
                ActualTime = draft.ActualTime,
                ActualOutput = draft.ActualOutput,
                DrinkType = draft.DrinkType,
                BrewMethod = draft.BrewMethod,
                Rating = draft.Rating,
                TastingNotes = draft.TastingNotes
            });

            preferences.SetLastDrinkType(draft.DrinkType);
            preferences.SetLastBagId(draft.SelectedBagId);
            preferences.SetLastMachineId(draft.SelectedMachineId);
            preferences.SetLastGrinderId(draft.SelectedGrinderId);
            preferences.SetLastAccessoryIds(draft.SelectedAccessoryIds);
            if (draft.SelectedMaker is { } maker)
                preferences.SetLastMadeById(maker.Id);
            if (draft.SelectedRecipient is { } recipient)
                preferences.SetLastMadeForId(recipient.Id);
        }

        logger.LogInformation(
            "Saved drink {ShotId} in {SaveMode} mode",
            saved.Id, editingShotId.HasValue ? "edit" : "create");
        return saved;
    }
}
