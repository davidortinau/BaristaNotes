using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.AndroidApp.Services;

internal static class PhotoDrinkSelection
{
    public static void Apply(DrinkDraft draft, BagSummaryDto bag)
    {
        draft.AvailableBags.RemoveAll(existing => existing.Id == bag.Id);
        draft.AvailableBags.Insert(0, bag);
        draft.SelectedBagId = bag.Id;
        draft.BeanName = bag.BeanName;
    }
}
