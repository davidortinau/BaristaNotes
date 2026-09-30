using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.Grind;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public sealed record GrindPickerLoadResult(
    int Microns,
    IReadOnlyList<GrindAnchor>? Anchors,
    string? GrinderName,
    bool IsUncalibrated);

public sealed class GrindPickerWorkflow(
    IBagRepository bags,
    IShotRecordRepository shots,
    IGrinderProfileRepository grinderProfiles,
    IDrinkValueRangeService ranges,
    ILogger<GrindPickerWorkflow> logger)
{
    public async Task<GrindPickerLoadResult> LoadAsync(DrinkDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        logger.LogDebug("Loading grind picker for bag {BagId} and method {Method}",
            draft.SelectedBagId, draft.BrewMethod);

        var microns = draft.GrindMicrons;
        if (microns is null && draft.SelectedBagId.HasValue)
        {
            var bag = await bags.GetByIdAsync(draft.SelectedBagId.Value);
            if (bag is not null)
                microns = await shots.GetMostRecentMicronsByBeanAsync(bag.BeanId, draft.BrewMethod);
        }

        var range = ranges.Resolve(DrinkValueMetric.GrindMicrons, draft.BrewMethod);
        microns ??= (int)range.Default;
        IReadOnlyList<GrindAnchor>? anchors = null;
        string? grinderName = null;
        var uncalibrated = false;
        if (draft.SelectedGrinderId is int grinderId)
        {
            grinderName = draft.AvailableEquipment.FirstOrDefault(item => item.Id == grinderId)?.Name;
            await grinderProfiles.EnsureCurrentSeedsAsync(grinderId);
            var profile = await grinderProfiles.GetByEquipmentIdAsync(grinderId);
            var parsed = DeterministicGrindInterpolator.ParseAnchors(profile?.AnchorsJson);
            if (parsed.Count >= 2)
            {
                anchors = parsed;
            }
            else
            {
                anchors = KnownGrinderSeeds.TryGet(grinderName);
                uncalibrated = anchors is null;
            }
        }

        return new(microns.Value, anchors, grinderName, uncalibrated);
    }
}
