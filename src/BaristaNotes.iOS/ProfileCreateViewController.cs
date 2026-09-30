using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class ProfileCreateViewController : SliceViewController
{
    private readonly ProfileDraft _draft;
    private readonly EquipmentHeader _header = new("profile.header", adaptiveTitle: true, wrapTitle: true);
    private readonly UIScrollView _scroll = new() { KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag,
        ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never };
    private readonly UIView _content = new();
    private readonly UIView _nameTile = new();
    private readonly UIView _photoTile = new();
    private readonly UIView _contextTile = new();
    private readonly UIView _errorTile = new();
    private readonly UIView _remainder = new();
    private readonly UILabel _nameCaption = new();
    private readonly UILabel _photoCaption = new();
    private readonly UILabel _contextCaption = new();
    private readonly UILabel _errorCaption = new();
    private readonly UILabel _error = new() { Lines = 0 };
    private readonly UILabel _photoHint = new() { Text = "Save the profile first to add a photo", Lines = 0 };
    private readonly UILabel _contextHint = new() { Text = "Preferences, history, notes the assistant can read and learn from.", Lines = 0 };
    private readonly UILabel _counter = new() { TextAlignment = UITextAlignment.Right };
    private readonly UILabel _contextPlaceholder = new() { Lines = 0, UserInteractionEnabled = false,
        Text = "e.g. Likes single-origin pour overs in the morning. Sensitive to bitter notes." };
    private readonly UITextField _name = new();
    private readonly UITextView _context = new();
    private readonly ProfileAvatarView _avatar = new(120);
    private readonly UIImageView _stagedImage = new() { ContentMode = UIViewContentMode.ScaleAspectFill, ClipsToBounds = true };
    private readonly UIView _stagedCard = new();
    private readonly UILabel _stagedHint = new() { Text = "This photo will be saved when you add the profile.", Lines = 0 };
    private readonly UILabel _imageError = new() { Lines = 0 };
    private readonly UIActivityIndicatorView _imageSpinner = new(UIActivityIndicatorViewStyle.Medium);
    private readonly UIActivityIndicatorView _loading = new(UIActivityIndicatorViewStyle.Medium);
    private readonly CancellationTokenSource _removed = new();
    private UIButton? _changePhoto;
    private UIButton? _removePhoto;
    private EquipmentActionButton? _save;
    private EquipmentActionButton? _delete;
    private EquipmentActionButton? _cancel;
    private UIButton? _retry;
    private NSObject? _keyboardObserver;
    private UIImage? _stagedOwnedImage;
    private string? _imagePath;
    private nfloat _keyboardHeight;
    private bool _saving;
    private bool _imageBusy;
    private bool _loaded;
    private bool _loadingStarted;
    private bool _removedFromParent;
    private bool _deleted;
    private bool _disposed;

    public ProfileCreateViewController(SliceNavigationController host, int? profileId = null, byte[]? stagedAvatarBytes = null) : base(host)
        => _draft = new ProfileDraft { ProfileId = profileId, StagedAvatarBytes = stagedAvatarBytes };

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = _content.BackgroundColor = NativeTheme.Outline;
        _scroll.AccessibilityIdentifier = "profile.form.scroll";
        _scroll.BackgroundColor = NativeTheme.Surface;
        foreach (var tile in new[] { _nameTile, _photoTile, _contextTile, _remainder })
            tile.BackgroundColor = NativeTheme.Surface;
        SliceUi.HeaderLabel(_nameCaption, "NAME");
        _name.AccessibilityIdentifier = "ProfileNameEntry";
        _name.AccessibilityLabel = "NAME";
        _name.Font = NativeTheme.Font(22, true);
        _name.TextColor = NativeTheme.TextPrimary;
        _name.BorderStyle = UITextBorderStyle.None;
        _name.LeftView = new UIView(new CGRect(0, 0, 4, 0));
        _name.LeftViewMode = UITextFieldViewMode.Always;
        _name.AttributedPlaceholder = new NSAttributedString("Profile name", new UIStringAttributes
        {
            Font = _name.Font, ForegroundColor = NativeTheme.Secondary.ColorWithAlpha(0.5f)
        });
        _name.ReturnKeyType = UIReturnKeyType.Done;
        _name.ShouldReturn = static entry => { entry.ResignFirstResponder(); return true; };
        var nameChanged = WeakUiCallback.Create(this, static owner =>
        {
            owner._draft.Name = owner._name.Text ?? "";
            owner.UpdateHeader();
        });
        _name.EditingChanged += (_, _) => nameChanged();
        _context.AccessibilityIdentifier = "ProfileContextEditor";
        _context.AccessibilityLabel = "ABOUT THIS PERSON";
        _context.TextColor = NativeTheme.TextPrimary;
        _context.BackgroundColor = UIColor.Clear;
        _contextPlaceholder.TextColor = NativeTheme.Secondary.ColorWithAlpha(0.5f);
        _contextPlaceholder.IsAccessibilityElement = false;
        var contextChanged = WeakUiCallback.Create(this, static owner =>
        {
            owner._draft.Context = owner._context.Text ?? "";
            owner.UpdateCounter();
        });
        _context.Changed += (_, _) => contextChanged();
        _photoHint.TextColor = _contextHint.TextColor = _stagedHint.TextColor = NativeTheme.Secondary;
        _imageError.TextColor = UIColor.Red;
        _imageError.AccessibilityIdentifier = "ImageErrorMessage";
        _imageSpinner.AccessibilityIdentifier = "ImageLoadingIndicator";
        _imageSpinner.Color = _loading.Color = NativeTheme.Primary;
        _stagedImage.AccessibilityIdentifier = "StagedProfilePhoto";
        _stagedCard.Layer.CornerRadius = 12;
        _stagedCard.ClipsToBounds = true;
        _stagedCard.BackgroundColor = NativeTheme.Surface;
        _stagedCard.AddSubview(_stagedImage);
        _changePhoto = PhotoButton("Change Photo", "ChangePhotoButton",
            WeakUiCallback.Create(this, static owner => _ = owner.ChangePhotoAsync()));
        _removePhoto = PhotoButton("Remove", "RemovePhotoButton",
            WeakUiCallback.Create(this, static owner => _ = owner.RemovePhotoAsync()));
        _nameTile.AddSubviews(_nameCaption, _name);
        _photoTile.AddSubviews(_photoCaption, _photoHint, _avatar, _changePhoto, _removePhoto,
            _imageSpinner, _imageError, _stagedCard, _stagedHint);
        _contextTile.AddSubviews(_contextCaption, _contextHint, _context, _contextPlaceholder, _counter);
        _errorTile.BackgroundColor = NativeTheme.Error;
        _errorTile.AccessibilityIdentifier = "profile.error";
        _errorTile.AddSubviews(_errorCaption, _error);
        _content.AddSubviews(_nameTile, _photoTile, _contextTile, _errorTile, _remainder);
        _scroll.AddSubview(_content);
        _cancel = new EquipmentActionButton("CANCEL", "profile.cancel",
            WeakUiCallback.Create(this, static owner =>
            {
                if (owner.Host.TopViewController == owner && owner.Host.PresentedViewController == null
                    && owner.PresentedViewController == null)
                    owner.Host.RequestBack();
            }));
        _delete = new EquipmentActionButton("DELETE", "profile.delete",
            WeakUiCallback.Create(this, static owner => owner.ConfirmDelete()), danger: true);
        _save = new EquipmentActionButton(_draft.IsEditing ? "SAVE" : "ADD", "profile.add",
            WeakUiCallback.Create(this, static owner => _ = owner.SaveAsync()), inverted: true);
        _retry = SliceUi.Button("Retry", "profile.retry",
            WeakUiCallback.Create(this, static owner => _ = owner.LoadAsync()));
        _retry.Hidden = true;
        _content.AddSubview(_retry);
        Root.AddSubviews(_header, _scroll, _loading, _cancel, _delete, _save);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((ProfileCreateViewController)environment).UpdateFonts());
        SetError(null);
        UpdateHeader();
        UpdateCounter();
        UpdateStagedPreview();
        UpdatePhotoVisibility();
        UpdateFonts();
    }

    private static UIButton PhotoButton(string title, string id, Action action)
    {
        var button = SliceUi.PickerAction(title, id, action);
        button.BackgroundColor = NativeTheme.Primary;
        button.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Normal);
        button.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Disabled);
        button.Layer.CornerRadius = 8;
        return button;
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        var weak = new WeakReference<ProfileCreateViewController>(this);
        _keyboardObserver?.Dispose();
        _keyboardObserver = UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
        {
            if (!weak.TryGetTarget(out var owner)) return;
            var frame = owner.Root.ConvertRectFromView(args.FrameEnd, null);
            owner._keyboardHeight = (nfloat)Math.Max(0, owner.Root.Bounds.Height - frame.Y);
            owner.Root.SetNeedsLayout();
        });
        if (!_loadingStarted)
        {
            _loadingStarted = true;
            _ = LoadAsync();
        }
        UpdateFonts();
    }

    public override void ViewDidDisappear(bool animated)
    {
        _keyboardObserver?.Dispose();
        _keyboardObserver = null;
        base.ViewDidDisappear(animated);
    }

    public override void DidMoveToParentViewController(UIViewController? parent)
    {
        base.DidMoveToParentViewController(parent);
        if (parent == null)
        {
            _removedFromParent = true;
            _removed.Cancel();
            _keyboardObserver?.Dispose();
            _keyboardObserver = null;
        }
    }

    private async Task LoadAsync()
    {
        if (_removedFromParent) return;
        if (!_draft.IsEditing)
        {
            _loaded = true;
            UpdateActions();
            UpdatePhotoVisibility();
            return;
        }
        _loaded = false;
        UpdateActions();
        UpdatePhotoVisibility();
        _loading.StartAnimating();
        _scroll.Hidden = true;
        if (_retry != null) _retry.Hidden = true;
        try
        {
            var profile = await Services.RunAsync(provider =>
                provider.GetRequiredService<IUserProfileService>().GetProfileByIdAsync(_draft.ProfileId!.Value));
            if (_removedFromParent) return;
            if (profile == null) throw new InvalidOperationException("Profile not found");
            _draft.ApplyLoadedData(profile);
            _name.Text = _draft.Name;
            _context.Text = _draft.Context;
            _loaded = true;
            SetError(null);
            UpdateHeader();
            UpdateCounter();
            await ReloadPhotoAsync();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Native profile detail load failed for {ProfileId}", _draft.ProfileId);
            if (!_removedFromParent)
            {
                SetError($"Failed to load profile: {error.Message}");
                if (_retry != null) _retry.Hidden = false;
            }
        }
        finally
        {
            if (!_removedFromParent) { _loading.StopAnimating(); _scroll.Hidden = false; }
        }
    }

    private async Task SaveAsync()
    {
        if (_saving || _imageBusy || !_loaded || _removedFromParent ||
            Host.TopViewController != this || Host.PresentedViewController != null) return;
        var stagedBytes = _draft.StagedAvatarBytes;
        var saving = new ProfileDraft
        {
            ProfileId = _draft.ProfileId, Name = _draft.Name, Context = _draft.Context,
            StagedAvatarBytes = stagedBytes
        };
        if (saving.ValidationError is { } validation) { SetError(validation); return; }
        _saving = true;
        SetError(null);
        UpdateActions();
        try
        {
            await Services.RunAsync(provider => provider.GetRequiredService<ProfileWorkflow>().SaveDetailsAsync(saving));
            if (_removedFromParent) return;
            _draft.ProfileId = saving.ProfileId;
            // Transfer the shared workflow's saved identity before staged photo
            // work, without replacing edits made since the save tap.
            UpdateHeader();
            UpdatePhotoVisibility();
            UpdateActions();
            Root.LayoutIfNeeded();
            var photo = await Services.RunAsync(provider =>
                provider.GetRequiredService<ProfileWorkflow>().SaveStagedAvatarAsync(saving));
            if (_removedFromParent) return;
            if (photo is { Success: false })
            {
                SetError($"Profile saved, but the photo was not saved: {photo.ErrorMessage}");
                return;
            }
            if (ReferenceEquals(_draft.StagedAvatarBytes, stagedBytes))
                _draft.StagedAvatarBytes = saving.StagedAvatarBytes;
            UpdateStagedPreview();
            await ReloadPhotoAsync();
            if (_removedFromParent || Host.TopViewController != this) return;
            Root.EndEditing(true);
            await Host.FeedbackHost.ShowAndWaitAsync($"Profile '{saving.Name}' saved");
            if (!_removedFromParent && Host.TopViewController == this) Host.RequestBack();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Native profile save failed for {ProfileId}", _draft.ProfileId);
            if (!_removedFromParent) SetError($"Failed to save: {error.Message}");
        }
        finally { _saving = false; if (!_removedFromParent) UpdateActions(); }
    }

    private async Task ReloadPhotoAsync()
    {
        if (!_draft.IsEditing) return;
        try
        {
            var path = await Services.RunAsync(provider =>
                provider.GetRequiredService<IUserProfileService>().GetProfileImagePathAsync(_draft.ProfileId!.Value));
            if (_removedFromParent) return;
            _imagePath = path;
            _avatar.SetPath(path, Logger);
            UpdatePhotoVisibility();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Native profile image read failed for {ProfileId}", _draft.ProfileId);
            if (!_removedFromParent)
            {
                _imageError.Text = "Failed to load image";
                UpdatePhotoVisibility();
            }
        }
    }

    private async Task ChangePhotoAsync()
    {
        if (_imageBusy || _saving || !_loaded || !_draft.IsEditing || _removedFromParent ||
            Host.TopViewController != this || Host.PresentedViewController != null) return;
        _imageBusy = true;
        _imageError.Text = null;
        UpdatePhotoVisibility();
        try
        {
            Root.EndEditing(true);
            var picker = new NativePhotoLibraryPicker(Logger, Services.Singleton<IImageProcessingService>());
            using var stream = await picker.PickAsync(this, _removed.Token);
            if (stream == null || _removedFromParent) return;
            var result = await Services.RunAsync(provider =>
                provider.GetRequiredService<IUserProfileService>().UpdateProfileImageAsync(_draft.ProfileId!.Value, stream));
            if (_removedFromParent) return;
            if (result.Success) await ReloadPhotoAsync();
            else _imageError.Text = result.ErrorMessage;
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Failed to update profile image for {ProfileId}", _draft.ProfileId);
            if (!_removedFromParent) _imageError.Text = "Failed to update image";
        }
        finally
        {
            _imageBusy = false;
            if (!_removedFromParent) UpdatePhotoVisibility();
        }
    }

    private async Task RemovePhotoAsync()
    {
        if (_imageBusy || _saving || !_loaded || !_draft.IsEditing || _removedFromParent ||
            Host.TopViewController != this || Host.PresentedViewController != null) return;
        _imageBusy = true;
        UpdatePhotoVisibility();
        try
        {
            var removed = await Services.RunAsync(provider =>
                provider.GetRequiredService<IUserProfileService>().RemoveProfileImageAsync(_draft.ProfileId!.Value));
            if (removed && !_removedFromParent)
            {
                _imagePath = null;
                _avatar.SetPath(null);
            }
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Failed to remove profile image for {ProfileId}", _draft.ProfileId);
            if (!_removedFromParent) _imageError.Text = "Failed to remove image";
        }
        finally { _imageBusy = false; if (!_removedFromParent) UpdatePhotoVisibility(); }
    }

    private void ConfirmDelete()
    {
        if (!_loaded || !_draft.IsEditing || _saving || _imageBusy ||
            Host.TopViewController != this || Host.PresentedViewController != null) return;
        Root.EndEditing(true);
        var weak = new WeakReference<ProfileCreateViewController>(this);
        Host.PresentViewController(new EquipmentConfirmationViewController(Host, "Delete Profile?",
            $"Are you sure you want to delete '{_draft.Name}'? This action cannot be undone.", "Delete",
            async () =>
            {
                if (!weak.TryGetTarget(out var owner)) throw new InvalidOperationException("Profile form is no longer available.");
                if (!owner._deleted)
                {
                    await owner.Services.RunAsync(async provider =>
                    {
                        await provider.GetRequiredService<ProfileWorkflow>().DeleteAsync(owner._draft.ProfileId!.Value);
                        return true;
                    });
                    owner._deleted = true;
                }
                await owner.Host.FeedbackHost.ShowAndWaitAsync($"Profile '{owner._draft.Name}' deleted");
            }, () =>
            {
                if (weak.TryGetTarget(out var owner) && owner.Host.TopViewController == owner)
                    owner.Host.RequestBack();
            }, operation: "delete profile"), false, null);
    }

    private void UpdateCounter()
    {
        _counter.Text = $"{_draft.Context.Length}/2000";
        _counter.TextColor = _draft.Context.Length > 2000 ? NativeTheme.Error : NativeTheme.Secondary;
        _contextPlaceholder.Hidden = _draft.Context.Length > 0;
        Root.SetNeedsLayout();
    }

    private void UpdateHeader()
    {
        _header.Update(_draft.IsEditing ? "EDIT PROFILE" : "NEW PROFILE",
            _draft.IsEditing ? string.IsNullOrEmpty(_draft.Name) ? "Loading…" : _draft.Name : "Add profile");
        UpdateActions();
        Root.SetNeedsLayout();
    }

    private void UpdateActions()
    {
        if (_delete != null)
        {
            _delete.Hidden = !_draft.IsEditing;
            _delete.Enabled = _loaded && !_saving && !_imageBusy;
        }
        if (_save != null)
        {
            _save.AccessibilityIdentifier = _draft.IsEditing ? "profile.save" : "profile.add";
            _save.SetText(_saving ? "SAVING…" : _draft.IsEditing ? "SAVE" : "ADD");
            _save.Enabled = _loaded && !_saving && !_imageBusy;
        }
    }

    private void UpdateStagedPreview()
    {
        _stagedImage.Image = null;
        _stagedOwnedImage?.Dispose();
        _stagedOwnedImage = null;
        if (_draft.StagedAvatarBytes is { Length: > 0 } bytes)
        {
            using var data = NSData.FromArray(bytes);
            _stagedOwnedImage = UIImage.LoadFromData(data);
            _stagedImage.Image = _stagedOwnedImage;
        }
        UpdatePhotoVisibility();
    }

    private void UpdatePhotoVisibility()
    {
        var staged = _draft.StagedAvatarBytes is { Length: > 0 };
        var saved = !staged && _draft.IsEditing;
        _stagedCard.Hidden = _stagedHint.Hidden = !staged;
        _photoHint.Hidden = staged || saved;
        _avatar.Hidden = !saved;
        if (_changePhoto != null) _changePhoto.Hidden = !saved;
        if (_removePhoto != null) _removePhoto.Hidden = !saved || _imagePath == null;
        if (_changePhoto != null) _changePhoto.Enabled = _loaded && !_saving && !_imageBusy;
        if (_removePhoto != null) _removePhoto.Enabled = _loaded && !_saving && !_imageBusy;
        _imageError.Hidden = !saved || string.IsNullOrEmpty(_imageError.Text);
        if (_imageBusy && saved) _imageSpinner.StartAnimating();
        else _imageSpinner.StopAnimating();
        UpdateActions();
        Root.SetNeedsLayout();
    }

    private void SetError(string? message)
    {
        _error.Text = message;
        _errorTile.Hidden = string.IsNullOrEmpty(message);
        Root.SetNeedsLayout();
    }

    private void UpdateFonts()
    {
        _header.UpdateFonts(TraitCollection);
        SourceScaledText.Tracked(_nameCaption, "NAME", 10, 2, NativeTheme.Secondary, TraitCollection);
        _name.Font = SourceScaledText.Font(22, true, TraitCollection);
        using var placeholder = new NSAttributedString("Profile name", new UIStringAttributes
        {
            Font = _name.Font, ForegroundColor = NativeTheme.Secondary.ColorWithAlpha(0.5f)
        });
        _name.AttributedPlaceholder = placeholder;
        SourceScaledText.Tracked(_photoCaption, "PHOTO", 10, 2, NativeTheme.Secondary, TraitCollection);
        SourceScaledText.Tracked(_contextCaption, "ABOUT THIS PERSON", 10, 2, NativeTheme.Secondary, TraitCollection);
        SourceScaledText.Tracked(_errorCaption, "ERROR", 10, 2, NativeTheme.Surface.ColorWithAlpha(.8f), TraitCollection);
        _photoHint.Font = SourceScaledText.Font(14, false, TraitCollection);
        _contextHint.Font = _stagedHint.Font = SourceScaledText.Font(12, false, TraitCollection);
        _counter.Font = SourceScaledText.Font(11, false, TraitCollection);
        _context.Font = _contextPlaceholder.Font = SourceScaledText.Font(15, false, TraitCollection);
        _error.Font = SourceScaledText.Font(16, true, TraitCollection);
        _error.TextColor = NativeTheme.Surface;
        _imageError.Font = SourceScaledText.Font(14, false, TraitCollection);
        _avatar.UpdateFonts(TraitCollection);
        foreach (var button in new[] { _changePhoto, _removePhoto })
            if (button != null) button.TitleLabel.Font = SourceScaledText.Font(14, false, TraitCollection);
        foreach (var button in new[] { _cancel, _save, _delete })
            button?.UpdateFonts(TraitCollection);
        Root.SetNeedsLayout();
    }

#pragma warning disable CS0672, CA1422
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        if (!OperatingSystem.IsIOSVersionAtLeast(17) && IsViewLoaded) UpdateFonts();
    }
#pragma warning restore CS0672, CA1422

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var width = Root.Bounds.Width;
        var height = Root.Bounds.Height - _keyboardHeight;
        _header.TopInset = Root.SafeAreaInsets.Top;
        var header = _header.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;
        _header.Frame = new CGRect(0, 0, width, header);
        var actions = new[] { _cancel, _delete, _save }.OfType<EquipmentActionButton>().Where(button => !button.Hidden).ToArray();
        var bottom = actions.Select(button => button.SizeThatFits(CGSize.Empty).Height).DefaultIfEmpty(72).Max();
        _scroll.Frame = new CGRect(0, header + 1, width, (nfloat)Math.Max(1, height - header - bottom - 2));
        _loading.Center = new CGPoint(width / 2, header + _scroll.Bounds.Height / 2);
        var nameCaption = SliceUi.Measure(_nameCaption, width - 32).Height;
        var entryHeight = (nfloat)Math.Max(44, _name.SizeThatFits(new CGSize(width - 32, nfloat.MaxValue)).Height);
        var nameHeight = (nfloat)Math.Max(100, 32 + nameCaption + entryHeight);
        _nameTile.Frame = new CGRect(0, 0, width, nameHeight);
        _nameCaption.Frame = new CGRect(16, (nameHeight - nameCaption - entryHeight) / 2, width - 32, nameCaption);
        _name.Frame = new CGRect(16, _nameCaption.Frame.Bottom, width - 32, entryHeight);
        nfloat y = nameHeight + 1;
        var photoCaption = SliceUi.Measure(_photoCaption, width - 32).Height;
        _photoCaption.Frame = new CGRect(16, 14, width - 32, photoCaption);
        nfloat photoY = 24 + photoCaption;
        if (_draft.StagedAvatarBytes is { Length: > 0 })
        {
            _stagedCard.Frame = new CGRect(16, photoY, width - 32, 160);
            _stagedImage.Frame = new CGRect(16, 16, width - 64, 128);
            var hint = SliceUi.Measure(_stagedHint, width - 32).Height;
            _stagedHint.Frame = new CGRect(16, photoY + 168, width - 32, hint);
            photoY += 168 + hint;
        }
        else if (_draft.IsEditing)
        {
            _avatar.Frame = new CGRect(16, photoY, width - 32, 136);
            photoY += 146;
            var change = _changePhoto?.SizeThatFits(CGSize.Empty) ?? CGSize.Empty;
            var remove = _removePhoto is { Hidden: false } ? _removePhoto.SizeThatFits(CGSize.Empty) : CGSize.Empty;
            var total = change.Width + (remove.Width > 0 ? remove.Width + 10 : 0);
            var rowHeight = (nfloat)Math.Max(change.Height, remove.Height);
            var x = (width - total) / 2;
            if (_changePhoto != null) _changePhoto.Frame = new CGRect(x, photoY, change.Width, rowHeight);
            if (_removePhoto != null) _removePhoto.Frame = new CGRect(x + change.Width + 10, photoY, remove.Width, rowHeight);
            photoY += rowHeight;
            if (_imageBusy)
            {
                var spinner = _imageSpinner.SizeThatFits(CGSize.Empty);
                _imageSpinner.Frame = new CGRect((width - spinner.Width) / 2, photoY + 10, spinner.Width, spinner.Height);
                photoY += 10 + spinner.Height;
            }
            if (!_imageError.Hidden)
            {
                var error = SliceUi.Measure(_imageError, width - 32).Height;
                _imageError.Frame = new CGRect(16, photoY + 10, width - 32, error);
                photoY += error + 10;
            }
        }
        else
        {
            var hint = SliceUi.Measure(_photoHint, width - 32).Height;
            _photoHint.Frame = new CGRect(16, photoY, width - 32, hint);
            photoY += hint;
        }
        _photoTile.Frame = new CGRect(0, y, width, photoY + 14);
        y += photoY + 15;
        var caption = SliceUi.Measure(_contextCaption, width - 32).Height;
        var description = SliceUi.Measure(_contextHint, width - 32).Height;
        var counter = SliceUi.Measure(_counter, width - 32).Height;
        var contextHeight = 28 + caption + description + 140 + counter + 24;
        _contextTile.Frame = new CGRect(0, y, width, contextHeight);
        _contextCaption.Frame = new CGRect(16, 14, width - 32, caption);
        _contextHint.Frame = new CGRect(16, 22 + caption, width - 32, description);
        _context.Frame = new CGRect(16, 30 + caption + description, width - 32, 140);
        var left = 16 + _context.TextContainerInset.Left + _context.TextContainer.LineFragmentPadding;
        _contextPlaceholder.Frame = new CGRect(left, _context.Frame.Y + _context.TextContainerInset.Top,
            width - left - 16, SliceUi.Measure(_contextPlaceholder, width - left - 16).Height);
        _counter.Frame = new CGRect(16, _context.Frame.Bottom + 8, width - 32, counter);
        y += contextHeight + 1;
        if (!_errorTile.Hidden)
        {
            var errorCaption = SliceUi.Measure(_errorCaption, width - 32).Height;
            var errorText = SliceUi.Measure(_error, width - 32).Height;
            var errorHeight = (nfloat)Math.Max(60, 24 + errorCaption + errorText);
            _errorTile.Frame = new CGRect(0, y, width, errorHeight);
            _errorCaption.Frame = new CGRect(16, 12, width - 32, errorCaption);
            _error.Frame = new CGRect(16, 12 + errorCaption, width - 32, errorText);
            y += errorHeight;
        }
        y++;
        if (_retry is { Hidden: false })
        {
            _retry.Frame = new CGRect(16, y + 8, width - 32, 44);
            y += 60;
        }
        _remainder.Frame = new CGRect(0, y, width, (nfloat)Math.Max(24, _scroll.Bounds.Height - y));
        _content.Frame = new CGRect(0, 0, width, _remainder.Frame.Bottom);
        _scroll.ContentSize = _content.Bounds.Size;
        var actionWidth = (width - actions.Length + 1) / actions.Length;
        for (var i = 0; i < actions.Length; i++)
            actions[i].Frame = new CGRect(i * (actionWidth + 1), height - bottom, actionWidth, bottom);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _removedFromParent = true;
            _removed.Cancel();
            _removed.Dispose();
            _keyboardObserver?.Dispose();
            _keyboardObserver = null;
            _stagedImage.Image = null;
            _stagedOwnedImage?.Dispose();
            _stagedOwnedImage = null;
        }
        base.Dispose(disposing);
    }
}
