namespace BaristaNotes.Services;

public sealed class MauiVoicePlatformActions(ILogger<MauiVoicePlatformActions> logger) : IVoicePlatformActions
{
    public bool IsCaptureSupported => MediaPicker.Default.IsCaptureSupported;

    public void Navigate(VoiceNavigationRequest request) =>
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (request.EntityId is not int id)
                {
                    await MauiControls.Shell.Current.GoToAsync(request.Route);
                    return;
                }

                switch (request.Route)
                {
                    case "shot-logging":
                        await MauiControls.Shell.Current.GoToAsync<ShotLoggingGridPageProps>(
                            request.Route, props => props.ShotId = id);
                        break;
                    case "profile-form":
                        await MauiControls.Shell.Current.GoToAsync<ProfileFormPageProps>(
                            request.Route, props => props.ProfileId = id);
                        break;
                    case "bean-detail":
                        await MauiControls.Shell.Current.GoToAsync<BeanDetailPageProps>(
                            request.Route, props => props.BeanId = id);
                        break;
                    case "equipment-detail":
                        await MauiControls.Shell.Current.GoToAsync<EquipmentDetailPageProps>(
                            request.Route, props => props.EquipmentId = id);
                        break;
                    case "bag-detail":
                        await MauiControls.Shell.Current.GoToAsync<BagDetailPageProps>(
                            request.Route, props =>
                            {
                                props.BagId = id;
                                props.BeanId = request.BeanId ?? 0;
                                props.BeanName = request.BeanName ?? "";
                            });
                        break;
                    default:
                        throw new ArgumentException("Unsupported voice detail route.", nameof(request));
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error navigating through voice to {Route}", request.Route);
            }
        });

    public async Task<VoicePhoto?> CapturePhotoAsync(VoiceCaptureOptions options)
    {
        var photo = await MainThread.InvokeOnMainThreadAsync(() =>
            MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = options.Title,
                MaximumWidth = options.MaximumWidth,
                MaximumHeight = options.MaximumHeight,
                CompressionQuality = options.CompressionQuality
            }));
        return photo is null ? null : new VoicePhoto(photo.FileName, photo.OpenReadAsync);
    }

    public async Task OpenBrowserAsync(Uri uri) =>
        await MainThread.InvokeOnMainThreadAsync(async () =>
            await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred));
}
