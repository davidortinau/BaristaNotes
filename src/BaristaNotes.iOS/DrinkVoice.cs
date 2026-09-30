using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Native.iOS;

internal sealed partial class DrinkViewController
{
    private Task _voiceReadiness = Task.CompletedTask;
    internal Task WaitForVoiceReadinessAsync() => _voiceReadiness;

    internal async Task RefreshVoiceReferencesAsync(DataChangeType change)
    {
        if (!_loaded) return;
        try
        {
            switch (change)
            {
                case DataChangeType.BeanCreated:
                case DataChangeType.BeanUpdated:
                case DataChangeType.BagCreated:
                case DataChangeType.BagUpdated:
                    _draft.AvailableBags = await Services.RunAsync(provider =>
                        provider.GetRequiredService<IBagService>().GetActiveBagsForShotLoggingAsync());
                    break;
                case DataChangeType.EquipmentCreated:
                case DataChangeType.EquipmentUpdated:
                    _draft.AvailableEquipment = await Services.RunAsync(provider =>
                        provider.GetRequiredService<IEquipmentService>().GetAllActiveEquipmentAsync());
                    break;
                case DataChangeType.ProfileCreated:
                case DataChangeType.ProfileUpdated:
                    _draft.AvailableUsers = await Services.RunAsync(provider =>
                        provider.GetRequiredService<IUserProfileService>().GetAllProfilesAsync());
                    break;
                default: return;
            }
            if (IsViewLoaded && View?.Window != null) UpdateTiles();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Could not refresh drink references after voice data change {Change}", change);
        }
    }
}
