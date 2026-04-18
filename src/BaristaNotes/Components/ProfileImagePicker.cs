using MauiReactor;
using BaristaNotes.Core.Services;

namespace BaristaNotes.Components;

public class ProfileImagePicker : Component<ProfileImagePickerState>
{
    private readonly int _profileId;
    private readonly IImagePickerService _imagePickerService;
    private readonly IUserProfileService _userProfileService;

    public ProfileImagePicker(
        int profileId,
        IImagePickerService imagePickerService,
        IUserProfileService userProfileService)
    {
        _profileId = profileId;
        _imagePickerService = imagePickerService;
        _userProfileService = userProfileService;
    }

    protected override void OnMounted()
    {
        base.OnMounted();
        _ = LoadProfileImageAsync();
    }

    public override VisualNode Render()
    {
        return new VStack(spacing: 10)
        {
            new CircularAvatar(State.ImagePath, 120),

            new HStack(spacing: 10)
            {
                new MauiReactor.Button("Change Photo")
                    .OnClicked(async () => await PickImageAsync())
                    .AutomationId("ChangePhotoButton"),

                State.ImagePath != null
                    ? new MauiReactor.Button("Remove")
                        .OnClicked(async () => await RemoveImageAsync())
                        .AutomationId("RemovePhotoButton")
                    : null
            }
            .HCenter(),

            State.IsLoading
                ? new MauiReactor.ActivityIndicator()
                    .IsRunning(true)
                    .AutomationId("ImageLoadingIndicator")
                : null,

            State.ErrorMessage != null
                ? new MauiReactor.Label(State.ErrorMessage)
                    .TextColor(Colors.Red)
                    .AutomationId("ImageErrorMessage")
                : null
        };
    }

    private async Task PickImageAsync()
    {
        try
        {
            SetState(s =>
            {
                s.IsLoading = true;
                s.ErrorMessage = null;
            });

            Stream? stream = null;
            try
            {
                stream = await _imagePickerService.PickImageAsync();
                if (stream == null)
                {
                    SetState(s => s.IsLoading = false);
                    return; // User cancelled
                }

                var result = await _userProfileService.UpdateProfileImageAsync(_profileId, stream);

                if (result.Success)
                {
                    // Force reload of image path and refresh component
                    var newPath = await _userProfileService.GetProfileImagePathAsync(_profileId);

                    SetState(s =>
                    {
                        s.ImagePath = newPath;
                        s.IsLoading = false;
                    });

                    // Force component invalidation to refresh the image
                    Invalidate();
                }
                else
                {
                    SetState(s =>
                    {
                        s.ErrorMessage = result.ErrorMessage;
                        s.IsLoading = false;
                    });
                }
            }
            finally
            {
                // Always dispose the stream to prevent resource leaks
                stream?.Dispose();
            }
        }
        catch (PermissionException)
        {
            SetState(s =>
            {
                s.ErrorMessage = "Photo library permission denied";
                s.IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.ErrorMessage = "Failed to update image";
                s.IsLoading = false;
            });
        }
    }

    private async Task RemoveImageAsync()
    {
        try
        {
            SetState(s => s.IsLoading = true);

            var removed = await _userProfileService.RemoveProfileImageAsync(_profileId);

            if (removed)
            {
                SetState(s =>
                {
                    s.ImagePath = null;
                    s.IsLoading = false;
                });

                // Force component invalidation to refresh the image
                Invalidate();
            }
            else
            {
                SetState(s => s.IsLoading = false);
            }
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.ErrorMessage = "Failed to remove image";
                s.IsLoading = false;
            });
        }
    }

    private async Task LoadProfileImageAsync()
    {
        var path = await _userProfileService.GetProfileImagePathAsync(_profileId);
        SetState(s => s.ImagePath = path);
    }
}

public class ProfileImagePickerState
{
    public string? ImagePath { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
}
