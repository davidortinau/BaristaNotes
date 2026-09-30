using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private string? ResolveProfileAvatarPath(string? filename)
    {
        if (string.IsNullOrEmpty(filename))
            return null;
        try
        {
            using var scope = _app.Services.CreateScope();
            var images = scope.ServiceProvider.GetRequiredService<IImageProcessingService>();
            return images.ImageExists(filename) ? images.GetImagePath(filename) : null;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Resolving an app profile avatar path failed");
            return null;
        }
    }

    private void RenderProfilePhoto(ProfileEditor editor)
    {
        if (!IsCurrentProfile(editor))
            return;
        editor.ImageVersion++;
        editor.PhotoHost.RemoveAllViews();
        editor.PhotoScreen?.Dispose();
        editor.Avatar = null;
        editor.ChangePhoto = null;
        editor.RemovePhoto = null;
        editor.ImageSpinner = null;
        editor.ImageErrorText = null;
        var body = _style.Column();
        var screen = new NativeScreen(body);
        editor.PhotoScreen = screen;
        if (editor.Draft.StagedAvatarBytes is { Length: > 0 } bytes)
        {
            var preview = new StagedProfilePreview(_style, _logger);
            screen.Own(preview);
            body.AddView(preview, new LinearLayout.LayoutParams(-1, _style.Dp(160)));
            body.AddView(_style.Label("This photo will be saved when you add the profile.", 12,
                color: _style.Secondary), new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(8) });
            _ = preview.LoadAsync(bytes);
        }
        else if (!editor.Draft.IsEditing)
        {
            body.AddView(_style.Label("Save the profile first to add a photo", 14, color: _style.Secondary));
        }
        else
        {
            var avatar = new ProfileAvatarView(_style, _logger, 120);
            avatar.ContentDescription = "Profile photo";
            editor.Avatar = avatar;
            screen.Own(avatar);
            body.AddView(avatar, new LinearLayout.LayoutParams(_style.Dp(120), _style.Dp(120))
            {
                Gravity = GravityFlags.CenterHorizontal, LeftMargin = _style.Dp(8),
                RightMargin = _style.Dp(8), TopMargin = _style.Dp(8), BottomMargin = _style.Dp(8)
            });
            var actions = _style.Row();
            actions.SetGravity(GravityFlags.Center);
            Button PhotoButton(string text, string id)
            {
                var button = _style.Button(text, id, NativeStyle.ModalText);
                button.Background = _style.Rounded(_style.Primary, 8);
                button.SetPadding(_style.Dp(14), _style.Dp(10), _style.Dp(14), _style.Dp(10));
                return button;
            }
            editor.ChangePhoto = PhotoButton("Change Photo", "ChangePhotoButton");
            editor.RemovePhoto = PhotoButton("Remove", "RemovePhotoButton");
            Bind(screen, editor.ChangePhoto, () => ObserveProfileTask(() => ChangeProfilePhotoAsync(editor)));
            Bind(screen, editor.RemovePhoto, () => ObserveProfileTask(() => RemoveProfilePhotoAsync(editor)));
            actions.AddView(editor.ChangePhoto);
            actions.AddView(editor.RemovePhoto, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = _style.Dp(10) });
            body.AddView(actions, new LinearLayout.LayoutParams(-2, -2)
            {
                Gravity = GravityFlags.CenterHorizontal, TopMargin = _style.Dp(10)
            });
            editor.ImageSpinner = new ProgressBar(this);
            NativeStyle.Identify(editor.ImageSpinner, "ImageLoadingIndicator");
            body.AddView(editor.ImageSpinner, new LinearLayout.LayoutParams(-2, -2)
            {
                Gravity = GravityFlags.CenterHorizontal, TopMargin = _style.Dp(10)
            });
            editor.ImageErrorText = _style.Label("", 14, color: Color.Red);
            NativeStyle.Identify(editor.ImageErrorText, "ImageErrorMessage");
            body.AddView(editor.ImageErrorText, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(10) });
        }
        editor.PhotoHost.AddView(body, new FrameLayout.LayoutParams(-1, -2));
        RefreshProfilePhotoControls(editor);
    }

    private void RefreshProfilePhotoControls(ProfileEditor editor)
    {
        if (!IsCurrentProfile(editor))
            return;
        var available = !editor.Saving && !editor.ImageLoading;
        if (editor.ChangePhoto is { } change)
            change.Enabled = available;
        if (editor.RemovePhoto is { } remove)
        {
            remove.Enabled = available;
            remove.Visibility = editor.ImagePath is null ? ViewStates.Gone : ViewStates.Visible;
        }
        if (editor.ImageSpinner is { } spinner)
            spinner.Visibility = editor.ImageLoading ? ViewStates.Visible : ViewStates.Gone;
        if (editor.ImageErrorText is { } error)
        {
            error.Text = editor.ImageError ?? "";
            error.Visibility = editor.ImageError is null ? ViewStates.Gone : ViewStates.Visible;
        }
        if (editor.Avatar is { } avatar)
            _ = avatar.LoadFileAsync(editor.ImagePath);
    }

    private async Task RefreshProfilePhotoPathAsync(ProfileEditor editor)
    {
        if (!IsCurrentProfile(editor) || editor.Draft.ProfileId is not int id)
            return;
        var version = ++editor.ImageVersion;
        try
        {
            var path = await InScopeAsync(services =>
                services.GetRequiredService<IUserProfileService>().GetProfileImagePathAsync(id));
            if (!IsCurrentProfile(editor) || version != editor.ImageVersion)
                return;
            editor.ImagePath = path;
            RefreshProfilePhotoControls(editor);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Loading profile {ProfileId} photo path failed", id);
            if (IsCurrentProfile(editor) && version == editor.ImageVersion)
            {
                editor.ImageError = "Failed to load image";
                RefreshProfilePhotoControls(editor);
            }
        }
    }

    private async Task ChangeProfilePhotoAsync(ProfileEditor editor)
    {
        if (!IsCurrentProfile(editor) || editor.Draft.ProfileId is not int id || id <= 0
            || editor.Saving || editor.ImageLoading)
            return;
        var cancellation = _lifetime.Token;
        var version = ++editor.ImageVersion;
        editor.ImageLoading = true;
        editor.ImageError = null;
        RefreshProfilePhotoControls(editor);
        RenderProfileActions(editor);
        var entered = false;
        try
        {
            using var imageScope = _app.Services.CreateScope();
            using var stream = await ProfilePhotoPicker.PickAsync(
                imageScope.ServiceProvider.GetRequiredService<IImageProcessingService>(), cancellation);
            if (stream is null)
            {
                _logger.LogDebug("Profile {ProfileId} photo selection canceled without mutation", id);
                return;
            }
            await _profileWriteGate.WaitAsync(cancellation);
            entered = true;
            var result = await InScopeAsync(services =>
                services.GetRequiredService<IUserProfileService>().UpdateProfileImageAsync(id, stream));
            if (result.Success)
            {
                var path = await InScopeAsync(services =>
                    services.GetRequiredService<IUserProfileService>().GetProfileImagePathAsync(id));
                if (IsCurrentProfile(editor) && version == editor.ImageVersion)
                    editor.ImagePath = path;
            }
            else if (IsCurrentProfile(editor) && version == editor.ImageVersion)
                editor.ImageError = result.ErrorMessage;
            // Unlike the staged workflow, the source image picker emits no
            // global ProfileUpdated notification for this immediate operation.
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(exception, "Photo library access denied for profile {ProfileId}", id);
            if (IsCurrentProfile(editor))
                editor.ImageError = "Photo library permission denied";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Updating profile {ProfileId} photo failed", id);
            if (IsCurrentProfile(editor))
                editor.ImageError = "Failed to update image";
        }
        finally
        {
            if (entered)
                _profileWriteGate.Release();
            editor.ImageLoading = false;
            if (IsCurrentProfile(editor))
            {
                RefreshProfilePhotoControls(editor);
                RenderProfileActions(editor);
            }
        }
    }

    private async Task RemoveProfilePhotoAsync(ProfileEditor editor)
    {
        if (!IsCurrentProfile(editor) || editor.Draft.ProfileId is not int id || id <= 0
            || editor.Saving || editor.ImageLoading)
            return;
        var cancellation = _lifetime.Token;
        var version = ++editor.ImageVersion;
        editor.ImageLoading = true;
        RefreshProfilePhotoControls(editor);
        RenderProfileActions(editor);
        var entered = false;
        try
        {
            await _profileWriteGate.WaitAsync(cancellation);
            entered = true;
            var removed = await InScopeAsync(services =>
                services.GetRequiredService<IUserProfileService>().RemoveProfileImageAsync(id));
            if (removed && IsCurrentProfile(editor) && version == editor.ImageVersion)
                editor.ImagePath = null;
            // Immediate persistence is deliberately independent of form Save/Cancel.
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Removing profile {ProfileId} photo failed", id);
            if (IsCurrentProfile(editor))
                editor.ImageError = "Failed to remove image";
        }
        finally
        {
            if (entered)
                _profileWriteGate.Release();
            editor.ImageLoading = false;
            if (IsCurrentProfile(editor))
            {
                RefreshProfilePhotoControls(editor);
                RenderProfileActions(editor);
            }
        }
    }
}
