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
        LoadProfileImage();
    }
    
    public override VisualNode Render()
    {
        return new VStack(spacing: 10)
        {
            new CircularAvatar(State.ImagePath, 120),
            
            new HStack(spacing: 10)
            {
                new Button("Change Photo")
                    .OnClicked(PickImageAsync)
                    .AutomationId("ChangePhotoButton"),
                
                State.ImagePath != null
                    ? new Button("Remove")
                        .OnClicked(RemoveImageAsync)
                        .AutomationId("RemovePhotoButton")
                    : null
            }
            .HCenter(),
            
            State.IsLoading
                ? new ActivityIndicator()
                    .IsRunning(true)
                    .AutomationId("ImageLoadingIndicator")
                : null,
            
            State.ErrorMessage != null
                ? new Label(State.ErrorMessage)
                    .TextColor(Colors.Red)
                    .AutomationId("ImageErrorMessage")
                : null
        };
    }
    
    private async void PickImageAsync()
    {
        try
        {
            SetState(s => s.IsLoading = true, s => s.ErrorMessage = null);
            
            var stream = await _imagePickerService.PickImageAsync();
            if (stream == null)
            {
                SetState(s => s.IsLoading = false);
                return; // User cancelled
            }
            
            var result = await _userProfileService.UpdateProfileImageAsync(_profileId, stream);
            
            if (result.Success)
            {
                await LoadProfileImage();
            }
            else
            {
                SetState(s => s.ErrorMessage = result.ErrorMessage);
            }
        }
        catch (PermissionException)
        {
            SetState(s => s.ErrorMessage = "Photo library permission denied");
        }
        catch (Exception ex)
        {
            SetState(s => s.ErrorMessage = "Failed to update image");
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            SetState(s => s.IsLoading = false);
        }
    }
    
    private async void RemoveImageAsync()
    {
        try
        {
            SetState(s => s.IsLoading = true);
            
            await _userProfileService.RemoveProfileImageAsync(_profileId);
            await LoadProfileImage();
        }
        catch (Exception ex)
        {
            SetState(s => s.ErrorMessage = "Failed to remove image");
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            SetState(s => s.IsLoading = false);
        }
    }
    
    private async Task LoadProfileImage()
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
