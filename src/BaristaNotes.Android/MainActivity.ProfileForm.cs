using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private sealed class ProfileEditor(NativeScreen screen, ProfileDraft draft, bool returnToList)
    {
        public NativeScreen Screen { get; } = screen;
        public ProfileDraft Draft { get; } = draft;
        public bool ReturnToList { get; } = returnToList;
        public required TextView HeaderMode { get; init; }
        public required TextView HeaderTitle { get; init; }
        public required EditText Name { get; init; }
        public required EditText Context { get; init; }
        public required TextView Counter { get; init; }
        public required FrameLayout PhotoHost { get; init; }
        public required FrameLayout ActionsHost { get; init; }
        public required LinearLayout ErrorTile { get; init; }
        public required TextView ErrorText { get; init; }
        public NativeScreen? PhotoScreen { get; set; }
        public NativeScreen? ActionsScreen { get; set; }
        public ProfileAvatarView? Avatar { get; set; }
        public Button? ChangePhoto { get; set; }
        public Button? RemovePhoto { get; set; }
        public ProgressBar? ImageSpinner { get; set; }
        public TextView? ImageErrorText { get; set; }
        public string? ImagePath { get; set; }
        public string? ImageError { get; set; }
        public bool Saving { get; set; }
        public bool ImageLoading { get; set; }
        public int ImageVersion { get; set; }
    }

    private ProfileEditor? _profileEditor;
    private readonly SemaphoreSlim _profileWriteGate = new(1, 1);

    private bool IsCurrentProfile(ProfileEditor editor) =>
        !_destroyed && ReferenceEquals(_profileEditor, editor) && ReferenceEquals(_transient, editor.Screen);

    private void ShowProfileForm(bool returnToProfiles = false, int? profileId = null, byte[]? stagedAvatarBytes = null) =>
        ObserveProfileTask(() => ShowProfileEditorAsync(returnToProfiles, profileId, stagedAvatarBytes));

    private async void ObserveProfileTask(Func<Task> operation)
    {
        if (_destroyed)
            return;
        try { await operation(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Activity teardown owns cancellation; no detached UI update.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Native profile operation failed");
            if (!_destroyed)
                ShowFeedback(ErrorMessage(exception), isError: true);
        }
    }

    private async Task ShowProfileEditorAsync(bool returnToList, int? profileId, byte[]? stagedBytes)
    {
        ClearTransient();
        _page = "profile";
        var draft = new ProfileDraft
        {
            ProfileId = profileId,
            StagedAvatarBytes = stagedBytes is { Length: > 0 } ? stagedBytes.ToArray() : null
        };
        var column = _style.Column();
        column.SetBackgroundColor(_style.Outline);
        var screen = new NativeScreen(column);
        var header = (ViewGroup)BuildHeader(draft.IsEditing ? "EDIT PROFILE" : "NEW PROFILE",
            draft.IsEditing ? "Loading…" : "Add profile");
        var mode = (TextView)header.GetChildAt(0)!;
        var title = (TextView)header.GetChildAt(1)!;
        title.SetMaxLines(2);
        NativeStyle.Identify(mode, "ProfileFormMode");
        NativeStyle.Identify(title, "ProfileFormTitle");
        column.AddView(header, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var body = new FrameLayout(this);
        body.SetBackgroundColor(_style.Surface);
        var scroll = new ScrollView(this) { FillViewport = true };
        NativeStyle.Identify(scroll, "ProfileFormScroll");
        var fields = _style.Column();
        fields.SetBackgroundColor(_style.Outline);
        scroll.AddView(fields);
        body.AddView(scroll, new FrameLayout.LayoutParams(-1, -1));
        var loading = new ProgressBar(this);
        loading.Visibility = draft.IsEditing ? ViewStates.Visible : ViewStates.Gone;
        scroll.Visibility = draft.IsEditing ? ViewStates.Gone : ViewStates.Visible;
        body.AddView(loading, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Center));
        column.AddView(body, _style.Fill(weight: 1));

        LinearLayout Field(string label, int minimum = 0, int verticalPadding = 14)
        {
            var tile = _style.Column();
            tile.SetBackgroundColor(_style.Surface);
            tile.SetMinimumHeight(_style.Dp(minimum));
            tile.SetPadding(_style.Dp(16), _style.Dp(verticalPadding), _style.Dp(16), _style.Dp(verticalPadding));
            var caption = _style.Label(label, 10, true, _style.Secondary);
            caption.LetterSpacing = 2 * .0624f;
            tile.AddView(caption);
            fields.AddView(tile, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
            return tile;
        }
        EditText Input(string placeholder, string id, int size, bool multiline)
        {
            var entry = new EditText(this)
            {
                Hint = placeholder, Typeface = multiline ? _style.Regular : _style.Bold,
                InputType = InputTypes.ClassText | (multiline ? InputTypes.TextFlagMultiLine : InputTypes.TextFlagCapSentences),
                Gravity = GravityFlags.Top | GravityFlags.Start
            };
            entry.SetSingleLine(!multiline);
            entry.SetTextSize(Android.Util.ComplexUnitType.Sp, size);
            entry.SetTextColor(_style.Text);
            entry.SetHintTextColor(Color.Argb(128, _style.Secondary.R, _style.Secondary.G, _style.Secondary.B));
            entry.SetBackgroundColor(Color.Transparent);
            entry.SetMinHeight(_style.Dp(44));
            entry.SetPadding(0, _style.Dp(4), 0, 0);
            NativeStyle.Identify(entry, id);
            return entry;
        }
        var nameTile = Field("NAME", 100, verticalPadding: 16);
        nameTile.SetGravity(GravityFlags.CenterVertical);
        var name = Input("Profile name", "ProfileNameEntry", 22, false);
        name.ContentDescription = "Profile name";
        nameTile.AddView(name, new LinearLayout.LayoutParams(-1, -2));
        var photo = Field("PHOTO");
        var photoHost = new FrameLayout(this);
        photo.AddView(photoHost, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(10) });
        var contextTile = Field("ABOUT THIS PERSON");
        contextTile.AddView(_style.Label("Preferences, history, notes the assistant can read and learn from.", 12,
            color: _style.Secondary), new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(8) });
        var context = Input("e.g. Likes single-origin pour overs in the morning. Sensitive to bitter notes.",
            "ProfileContextEditor", 15, true);
        context.ContentDescription = "About this person";
        contextTile.AddView(context, new LinearLayout.LayoutParams(-1, _style.Dp(140)) { TopMargin = _style.Dp(8) });
        var counter = _style.Label("0/2000", 11, color: _style.Secondary);
        counter.Gravity = GravityFlags.End;
        NativeStyle.Identify(counter, "ProfileContextCounter");
        contextTile.AddView(counter, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(8) });
        var errorTile = _style.Column();
        errorTile.SetBackgroundColor(_style.Error);
        errorTile.SetMinimumHeight(_style.Dp(60));
        errorTile.SetPadding(_style.Dp(16), _style.Dp(12), _style.Dp(16), _style.Dp(12));
        errorTile.AccessibilityLiveRegion = AccessibilityLiveRegion.Assertive;
        errorTile.Visibility = ViewStates.Gone;
        var errorCaption = _style.Label("ERROR", 10, true,
            Color.Argb(204, _style.Surface.R, _style.Surface.G, _style.Surface.B));
        errorCaption.LetterSpacing = 2 * .0624f;
        errorTile.AddView(errorCaption);
        var error = _style.Label("", 16, true, _style.Surface);
        NativeStyle.Identify(error, "ProfileError");
        errorTile.AddView(error);
        fields.AddView(errorTile);
        var spacer = new View(this);
        spacer.SetBackgroundColor(_style.Surface);
        fields.AddView(spacer, new LinearLayout.LayoutParams(-1, _style.Dp(24)));
        var actionsHost = new FrameLayout(this);
        column.AddView(actionsHost, new LinearLayout.LayoutParams(-1, -2));
        var editor = new ProfileEditor(screen, draft, returnToList)
        {
            HeaderMode = mode, HeaderTitle = title, Name = name, Context = context,
            Counter = counter, PhotoHost = photoHost, ActionsHost = actionsHost,
            ErrorTile = errorTile, ErrorText = error
        };
        EventHandler<TextChangedEventArgs> nameChanged = (_, _) =>
        {
            if (!IsCurrentProfile(editor))
                return;
            draft.Name = name.Text ?? "";
            UpdateProfileHeader(editor);
        };
        EventHandler<TextChangedEventArgs> contextChanged = (_, _) =>
        {
            if (!IsCurrentProfile(editor))
                return;
            draft.Context = context.Text ?? "";
            counter.Text = $"{draft.Context.Length}/2000";
            counter.SetTextColor(draft.Context.Length > 2000 ? _style.Error : _style.Secondary);
        };
        name.TextChanged += nameChanged;
        context.TextChanged += contextChanged;
        screen.OnDispose(() =>
        {
            name.TextChanged -= nameChanged;
            context.TextChanged -= contextChanged;
            editor.ImageVersion++;
            editor.PhotoScreen?.Dispose();
            editor.ActionsScreen?.Dispose();
            if (ReferenceEquals(_profileEditor, editor))
                _profileEditor = null;
        });
        _profileEditor = editor;
        _transient = screen;
        Present(column, edgeToEdge: true);
        UpdateProfileHeader(editor);
        RenderProfilePhoto(editor);
        RenderProfileActions(editor);
        if (draft.ProfileId is not int loadId || loadId <= 0)
            return;
        try
        {
            var profile = await InScopeAsync(services =>
                services.GetRequiredService<IUserProfileService>().GetProfileByIdAsync(loadId));
            if (!IsCurrentProfile(editor))
                return;
            if (profile is null)
                SetProfileError(editor, "Profile not found");
            else
            {
                draft.ApplyLoadedData(profile);
                name.Text = draft.Name;
                context.Text = draft.Context;
                UpdateProfileHeader(editor);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Loading profile {ProfileId} failed", loadId);
            SetProfileError(editor, $"Failed to load profile: {ErrorMessage(exception)}");
        }
        finally
        {
            if (IsCurrentProfile(editor))
            {
                loading.Visibility = ViewStates.Gone;
                scroll.Visibility = ViewStates.Visible;
            }
        }
        if (IsCurrentProfile(editor) && draft.StagedAvatarBytes is not { Length: > 0 })
            await RefreshProfilePhotoPathAsync(editor);
    }

    private void UpdateProfileHeader(ProfileEditor editor)
    {
        if (!IsCurrentProfile(editor))
            return;
        editor.HeaderMode.Text = editor.Draft.IsEditing ? "EDIT PROFILE" : "NEW PROFILE";
        var text = editor.Draft.IsEditing
            ? string.IsNullOrEmpty(editor.Draft.Name) ? "Loading…" : editor.Draft.Name
            : "Add profile";
        editor.HeaderTitle.Text = text;
        editor.HeaderTitle.SetTextSize(Android.Util.ComplexUnitType.Sp,
            text.Length <= 12 ? 28 : text.Length <= 20 ? 22 : text.Length <= 28 ? 18 : 16);
    }

    private void SetProfileError(ProfileEditor editor, string? message)
    {
        if (!IsCurrentProfile(editor))
            return;
        editor.ErrorText.Text = message ?? "";
        editor.ErrorTile.Visibility = message is null ? ViewStates.Gone : ViewStates.Visible;
    }

    private void RenderProfileActions(ProfileEditor editor)
    {
        if (!IsCurrentProfile(editor))
            return;
        editor.ActionsHost.RemoveAllViews();
        editor.ActionsScreen?.Dispose();
        var row = _style.Row();
        var screen = new NativeScreen(row);
        editor.ActionsScreen = screen;
        Button Action(string text, string id, bool inverted = false, bool danger = false)
        {
            var button = _style.Button(text, id, inverted || danger ? _style.Surface : _style.Text);
            ConfigureBeanAction(button);
            button.SetBackgroundColor(danger ? _style.Error : inverted ? _style.Text : _style.Surface);
            return button;
        }
        var cancel = Action("CANCEL", "ProfileCancel");
        Bind(screen, cancel, () => ObserveProfileTask(() => ReturnFromProfileEditorAsync(editor)));
        row.AddView(cancel, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = _style.Dp(1) });
        if (editor.Draft.IsEditing)
        {
            var delete = Action("DELETE", "ProfileDelete", danger: true);
            delete.Enabled = !editor.Saving && !editor.ImageLoading;
            Bind(screen, delete, () => RunOperation(() => ConfirmProfileDeleteAsync(editor)));
            row.AddView(delete, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = _style.Dp(1) });
        }
        var save = Action(editor.Saving ? "SAVING…" : editor.Draft.IsEditing ? "SAVE" : "ADD",
            editor.Draft.IsEditing ? "ProfileSave" : "ProfileAdd", inverted: true);
        save.Enabled = !editor.Saving && !editor.ImageLoading;
        Bind(screen, save, () => ObserveProfileTask(() => SaveProfileEditorAsync(editor)));
        row.AddView(save, new LinearLayout.LayoutParams(0, -1, 1));
        editor.ActionsHost.AddView(row, new FrameLayout.LayoutParams(-1, -2));
    }

    private async Task SaveProfileEditorAsync(ProfileEditor editor)
    {
        if (!IsCurrentProfile(editor) || editor.Saving || editor.ImageLoading)
            return;
        SetProfileError(editor, editor.Draft.ValidationError);
        if (editor.Draft.ValidationError is not null)
            return;
        var input = new ProfileDraft
        {
            ProfileId = editor.Draft.ProfileId, Name = editor.Draft.Name,
            Context = editor.Draft.Context, StagedAvatarBytes = editor.Draft.StagedAvatarBytes
        };
        var cancellation = _lifetime.Token;
        editor.Saving = true;
        RenderProfileActions(editor);
        RefreshProfilePhotoControls(editor);
        HideKeyboard();
        var entered = false;
        try
        {
            await _profileWriteGate.WaitAsync(cancellation);
            entered = true;
            var saved = await InScopeAsync(services =>
                services.GetRequiredService<ProfileWorkflow>().SaveDetailsAsync(input));
            editor.Draft.ProfileId = input.ProfileId;
            if (IsCurrentProfile(editor))
            {
                // Show EDIT/SAVE before attempting the non-atomic staged image.
                UpdateProfileHeader(editor);
                RenderProfileActions(editor);
                RenderProfilePhoto(editor);
            }
            if (input.StagedAvatarBytes is { Length: > 0 })
            {
                var image = await InScopeAsync(services =>
                    services.GetRequiredService<ProfileWorkflow>().SaveStagedAvatarAsync(input));
                editor.Draft.StagedAvatarBytes = input.StagedAvatarBytes;
                if (image is { Success: false })
                {
                    SetProfileError(editor, $"Profile saved, but the photo was not saved: {image.ErrorMessage}");
                    return;
                }
                if (IsCurrentProfile(editor))
                {
                    RenderProfilePhoto(editor);
                    ObserveProfileTask(() => RefreshProfilePhotoPathAsync(editor));
                }
            }
            if (!IsCurrentProfile(editor))
                return;
            await _feedback.ShowSuccessAsync($"Profile '{saved.Name}' saved");
            cancellation.ThrowIfCancellationRequested();
            if (IsCurrentProfile(editor))
                await ReturnFromProfileEditorAsync(editor);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            // Even when image/feedback fails after create, the assigned ID stays
            // on the draft; another Save updates it instead of creating again.
            editor.Draft.ProfileId = input.ProfileId;
            _logger.LogError(exception, "Saving profile {ProfileId} failed", input.ProfileId);
            SetProfileError(editor, $"Failed to save: {ErrorMessage(exception)}");
            UpdateProfileHeader(editor);
            RenderProfilePhoto(editor);
        }
        finally
        {
            if (entered)
                _profileWriteGate.Release();
            editor.Saving = false;
            if (IsCurrentProfile(editor))
            {
                RenderProfileActions(editor);
                RefreshProfilePhotoControls(editor);
            }
        }
    }

    private async Task ReturnFromProfileEditorAsync(ProfileEditor editor)
    {
        if (!IsCurrentProfile(editor))
            return;
        HideKeyboard();
        if (TryReturnVoiceNavigation()) return;
        if (editor.ReturnToList)
        {
            await ShowProfilesAsync();
            return;
        }
        ShowDrink();
        var revision = _presentationRevision;
        if (!_profilesDirty)
            return;
        try
        {
            await RefreshProfilesAsync();
            if (!_destroyed && revision == _presentationRevision && _page == "drink")
                UpdateDrink(_editingShotId.HasValue ? _editEditor! : _newEditor!);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _profilesDirty = true;
            _logger.LogError(exception, "Profile saved/returned but People references could not refresh");
            if (!_destroyed && revision == _presentationRevision)
                ShowFeedback("People could not refresh. Tap MADE BY / FOR to retry.", isError: true);
        }
    }
}
