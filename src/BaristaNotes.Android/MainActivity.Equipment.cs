using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private enum EquipmentReturn { Drink, List }
    private enum EquipmentPickerKind { Machine, Grinder, Accessories }
    private static readonly (EquipmentType Type, string Text)[] EquipmentTypes =
    [
        (EquipmentType.Machine, "MACHINE"), (EquipmentType.Grinder, "GRINDER"),
        (EquipmentType.Tamper, "TAMPER"), (EquipmentType.PuckScreen, "PUCK SCREEN"),
        (EquipmentType.Other, "OTHER")
    ];
    private sealed record EquipmentEditor(
        NativeScreen Screen, EquipmentDraft Draft, EquipmentReturn ReturnTo,
        TextView Header, EditText Name, EditText Notes, LinearLayout Error,
        TextView ErrorMessage, Button Save);

    private EquipmentEditor? _equipmentEditor;
    private NativeArchiveConfirmation? _equipmentConfirmation;
    private bool _equipmentDirty = true;
    private long _equipmentRevision;

    private SelectorRecyclerView CreateOwnedEquipmentList(NativeScreen screen, RecyclerView.Adapter adapter, string id)
    {
        var list = new SelectorRecyclerView(this);
        list.SetAdapter(adapter);
        NativeStyle.Identify(list, id);
        screen.Own(list);
        screen.Own(adapter);
        return list;
    }

    private async Task<List<EquipmentDto>> RefreshEquipmentReferencesAsync()
    {
        var cancellation = _lifetime.Token;
        var revision = _equipmentRevision;
        var equipment = await InScopeAsync(services =>
            services.GetRequiredService<IEquipmentService>().GetAllActiveEquipmentAsync());
        cancellation.ThrowIfCancellationRequested();
        _newDraft.AvailableEquipment = equipment;
        if (_editDraft is not null)
            _editDraft.AvailableEquipment = equipment;
        _equipmentDirty = revision != _equipmentRevision;
        return equipment;
    }

    private async Task ShowEquipmentAsync()
    {
        ClearTransient();
        _page = "equipment";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Outline);
        column.SetPadding(_style.Dp(1), _style.Dp(1), _style.Dp(1), _style.Dp(1));
        var screen = new NativeScreen(column);
        var header = (ViewGroup)BuildHeader("EQUIPMENT", "0 items");
        var count = (TextView)header.GetChildAt(1)!;
        NativeStyle.Identify(count, "EquipmentCount");
        column.AddView(header, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var body = new FrameLayout(this);
        body.SetBackgroundColor(_style.Surface);
        var adapter = new EquipmentListAdapter(_style, id =>
            Choice(() => RunOperation(() => ShowEquipmentFormAsync(EquipmentReturn.List, id)))());
        var list = CreateOwnedEquipmentList(screen, adapter, "EquipmentList");
        list.SetBackgroundColor(_style.Outline);
        list.Visibility = ViewStates.Gone;
        body.AddView(list, new FrameLayout.LayoutParams(-1, -1));
        var status = _style.Column();
        status.SetPadding(_style.Dp(24), _style.Dp(24), _style.Dp(24), _style.Dp(24));
        status.SetGravity(GravityFlags.Center);
        var progress = new ProgressBar(this);
        status.AddView(progress);
        var statusTitle = _style.Label("", 10, true, _style.Secondary);
        statusTitle.LetterSpacing = 2 * .0624f;
        statusTitle.Gravity = GravityFlags.Center;
        statusTitle.Visibility = ViewStates.Gone;
        status.AddView(statusTitle);
        var message = _style.Label("Loading…", 14, color: _style.Secondary);
        message.Gravity = GravityFlags.Center;
        NativeStyle.Identify(message, "EquipmentListMessage");
        status.AddView(message, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(8) });
        var retry = _style.Button("Retry", "EquipmentListRetry", _style.Surface);
        retry.Background = _style.Rounded(_style.Primary, 0);
        retry.Visibility = ViewStates.Gone;
        Bind(screen, retry, () => RunOperation(ShowEquipmentAsync));
        status.AddView(retry, new LinearLayout.LayoutParams(-2, -2) { TopMargin = _style.Dp(12) });
        body.AddView(status, new FrameLayout.LayoutParams(-1, -1));
        column.AddView(body, _style.Fill(weight: 1));
        var navigation = BuildNavigation(screen,
            ("\uefef", "New Drink", "NavDrink", () => { _editingShotId = null; ShowDrink(); }),
            ("\uf009", "Activity", "NavActivity", () => RunOperation(ShowHistoryAsync)),
            ("\ue8b8", "Settings", "EquipmentSettings", ShowSettings),
            ("\ue145", "Add equipment", "EquipmentAdd", () => RunOperation(() => ShowEquipmentFormAsync(EquipmentReturn.List))));
        var add = (Button)((LinearLayout)navigation).GetChildAt(3)!;
        add.SetBackgroundColor(_style.Text);
        add.SetTextColor(_style.Surface);
        column.AddView(navigation, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(1) });
        _transient = screen;
        Present(column, edgeToEdge: true);
        try
        {
            var equipment = await RefreshEquipmentReferencesAsync();
            if (_destroyed || !ReferenceEquals(_transient, screen))
                return;
            count.Text = equipment.Count == 1 ? "1 item" : $"{equipment.Count} items";
            progress.Visibility = ViewStates.Gone;
            if (equipment.Count == 0)
            {
                status.SetPadding(_style.Dp(32), _style.Dp(32), _style.Dp(32), _style.Dp(32));
                statusTitle.Text = "NO EQUIPMENT";
                statusTitle.Visibility = ViewStates.Visible;
                message.Text = "Add machines, grinders, and accessories";
                message.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
                message.SetTextColor(_style.Text);
                message.LayoutParameters = new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(12) };
            }
            else
            {
                adapter.SetItems(equipment);
                status.Visibility = ViewStates.Gone;
                list.Visibility = ViewStates.Visible;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load the native active equipment list");
            if (_destroyed || !ReferenceEquals(_transient, screen))
                return;
            progress.Visibility = ViewStates.Gone;
            statusTitle.Text = "ERROR";
            statusTitle.Visibility = ViewStates.Visible;
            message.Text = ErrorMessage(exception);
            message.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
            message.SetTextColor(_style.Text);
            message.LayoutParameters = new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(12) };
            retry.Visibility = ViewStates.Visible;
        }
    }

    private bool IsCurrentEquipmentEditor(EquipmentEditor editor) =>
        !_destroyed && ReferenceEquals(_equipmentEditor, editor) && ReferenceEquals(_transient, editor.Screen);

    private void SetEquipmentError(EquipmentEditor editor, string? error)
    {
        if (!IsCurrentEquipmentEditor(editor))
            return;
        editor.ErrorMessage.Text = error ?? "";
        editor.Error.Visibility = error is null ? ViewStates.Gone : ViewStates.Visible;
    }

    private void UpdateEquipmentHeader(EquipmentEditor editor)
    {
        if (!IsCurrentEquipmentEditor(editor))
            return;
        var text = editor.Draft.IsEditing
            ? string.IsNullOrEmpty(editor.Draft.Name) ? "Loading…" : editor.Draft.Name
            : "Add equipment";
        editor.Header.Text = text;
        editor.Header.SetTextSize(Android.Util.ComplexUnitType.Sp,
            text.Length <= 12 ? 28 : text.Length <= 20 ? 22 : text.Length <= 28 ? 18 : 16);
    }

    private async Task ShowEquipmentFormAsync(EquipmentReturn returnTo, int? equipmentId = null, EquipmentType? preset = null)
    {
        ClearTransient();
        _page = "equipmentForm";
        var draft = new EquipmentDraft
        {
            EquipmentId = equipmentId,
            SelectedType = preset ?? EquipmentType.Machine
        };
        var column = _style.Column();
        column.SetBackgroundColor(_style.Outline);
        var screen = new NativeScreen(column);
        var header = (ViewGroup)BuildHeader(draft.IsEditing ? "EDIT EQUIPMENT" : "NEW EQUIPMENT",
            draft.IsEditing ? "Loading…" : "Add equipment");
        var headerText = (TextView)header.GetChildAt(1)!;
        headerText.SetMaxLines(2);
        NativeStyle.Identify(headerText, "EquipmentFormTitle");
        column.AddView(header, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var body = new FrameLayout(this);
        body.SetBackgroundColor(_style.Surface);
        var scroll = new ScrollView(this) { FillViewport = true };
        NativeStyle.Identify(scroll, "EquipmentFormScroll");
        var fields = _style.Column();
        fields.SetBackgroundColor(_style.Outline);
        scroll.AddView(fields);
        body.AddView(scroll, new FrameLayout.LayoutParams(-1, -1));
        var loading = new ProgressBar(this);
        body.AddView(loading, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Center));
        loading.Visibility = draft.IsEditing ? ViewStates.Visible : ViewStates.Gone;
        scroll.Visibility = draft.IsEditing ? ViewStates.Gone : ViewStates.Visible;
        column.AddView(body, _style.Fill(weight: 1));

        LinearLayout Field(string label, int minimum, int verticalPadding = 14)
        {
            var tile = _style.Column();
            tile.SetGravity(GravityFlags.CenterVertical);
            tile.SetBackgroundColor(_style.Surface);
            tile.SetMinimumHeight(_style.Dp(minimum));
            tile.SetPadding(_style.Dp(16), _style.Dp(verticalPadding), _style.Dp(16), _style.Dp(verticalPadding));
            var caption = _style.Label(label, 10, true, _style.Secondary);
            caption.LetterSpacing = 2 * .0624f;
            tile.AddView(caption);
            fields.AddView(tile, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
            return tile;
        }
        var nameTile = Field("NAME", 100, verticalPadding: 16);
        var name = new EditText(this)
        {
            Hint = "Equipment name", Typeface = _style.Bold, InputType = InputTypes.ClassText
        };
        name.SetSingleLine(true);
        name.SetTextSize(Android.Util.ComplexUnitType.Sp, 22);
        name.SetTextColor(_style.Text);
        name.SetHintTextColor(Color.Argb(128, _style.Secondary.R, _style.Secondary.G, _style.Secondary.B));
        name.SetBackgroundColor(Color.Transparent);
        name.SetPadding(0, _style.Dp(4), 0, 0);
        NativeStyle.Identify(name, "EquipmentName");
        nameTile.AddView(name, new LinearLayout.LayoutParams(-1, -2));

        var typeTile = Field("TYPE", 0);
        typeTile.SetPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(16));
        var typeButtons = new Dictionary<EquipmentType, Button>();
        void RefreshTypes()
        {
            foreach (var (type, button) in typeButtons)
            {
                var selected = type == draft.SelectedType;
                button.Selected = selected;
                button.SetBackgroundColor(selected ? _style.Text : _style.SurfaceVariant);
                button.SetTextColor(selected ? _style.Surface : _style.Text);
                button.ContentDescription = $"{button.Text}, {(selected ? "selected" : "not selected")}";
            }
        }
        for (var rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            var row = _style.Row();
            for (var columnIndex = 0; columnIndex < 3; columnIndex++)
            {
                var index = rowIndex * 3 + columnIndex;
                View child;
                if (index < EquipmentTypes.Length)
                {
                    var (type, text) = EquipmentTypes[index];
                    var button = _style.Button(text, $"EquipmentType_{type}");
                    button.Typeface = _style.Bold;
                    button.SetTextSize(Android.Util.ComplexUnitType.Sp, 11);
                    button.LetterSpacing = 1.5f * .0624f;
                    button.SetPadding(_style.Dp(8), 0, _style.Dp(8), 0);
                    Bind(screen, button, () => { draft.SelectedType = type; RefreshTypes(); });
                    typeButtons.Add(type, button);
                    child = button;
                }
                else
                    child = new Space(this);
                row.AddView(child, new LinearLayout.LayoutParams(0, -2, 1)
                {
                    RightMargin = columnIndex < 2 ? _style.Dp(8) : 0
                });
            }
            typeTile.AddView(row, new LinearLayout.LayoutParams(-1, -2)
            {
                TopMargin = _style.Dp(rowIndex == 0 ? 10 : 8)
            });
        }
        var notesTile = Field("NOTES", 160);
        notesTile.SetGravity(GravityFlags.Top);
        var notes = new EditText(this)
        {
            Hint = "Additional details", Typeface = _style.Regular,
            InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine,
            Gravity = GravityFlags.Top | GravityFlags.Start
        };
        notes.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
        notes.SetTextColor(_style.Text);
        notes.SetHintTextColor(Color.Argb(128, _style.Secondary.R, _style.Secondary.G, _style.Secondary.B));
        notes.SetBackgroundColor(Color.Transparent);
        notes.SetPadding(0, 0, 0, 0);
        NativeStyle.Identify(notes, "EquipmentNotes");
        notesTile.AddView(notes, new LinearLayout.LayoutParams(-1, _style.Dp(120)));

        var error = _style.Column();
        error.SetBackgroundColor(_style.Error);
        error.SetMinimumHeight(_style.Dp(60));
        error.SetPadding(_style.Dp(16), _style.Dp(12), _style.Dp(16), _style.Dp(12));
        error.AccessibilityLiveRegion = AccessibilityLiveRegion.Assertive;
        var errorCaption = _style.Label("ERROR", 10, true,
            Color.Argb(204, _style.Surface.R, _style.Surface.G, _style.Surface.B));
        errorCaption.LetterSpacing = 2 * .0624f;
        error.AddView(errorCaption);
        var errorText = _style.Label("", 16, true, _style.Surface);
        error.AddView(errorText);
        NativeStyle.Identify(error, "EquipmentError");
        error.Visibility = ViewStates.Gone;
        fields.AddView(error);
        var spacer = new View(this);
        spacer.SetBackgroundColor(_style.Surface);
        fields.AddView(spacer, new LinearLayout.LayoutParams(-1, _style.Dp(24)));

        var actions = _style.Row();
        Button Action(string text, string id, bool inverted = false, bool danger = false)
        {
            var button = _style.Button(text, id, inverted || danger ? _style.Surface : _style.Text);
            ConfigureBeanAction(button);
            button.SetBackgroundColor(danger ? _style.Error : inverted ? _style.Text : _style.Surface);
            return button;
        }
        var cancel = Action("CANCEL", "EquipmentCancel");
        var save = Action(draft.IsEditing ? "SAVE" : "ADD", "EquipmentSave", inverted: true);
        var editor = new EquipmentEditor(screen, draft, returnTo, headerText, name, notes, error, errorText, save);
        // Source Cancel remains available while a load/save is awaiting I/O.
        // The operation can finish, but its stale form cannot mutate the new page.
        screen.Click(cancel, () => CancelEquipmentForm(editor));
        actions.AddView(cancel, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = _style.Dp(1) });
        if (draft.IsEditing)
        {
            var delete = Action("DELETE", "EquipmentArchive", danger: true);
            Bind(screen, delete, () => RunOperation(() => ConfirmEquipmentArchiveAsync(editor)));
            actions.AddView(delete, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = _style.Dp(1) });
        }
        Bind(screen, save, () => RunOperation(() => SaveEquipmentAsync(editor)));
        actions.AddView(save, new LinearLayout.LayoutParams(0, -1, 1));
        column.AddView(actions, new LinearLayout.LayoutParams(-1, -2));
        EventHandler<TextChangedEventArgs> nameChanged = (_, _) =>
        {
            if (!IsCurrentEquipmentEditor(editor))
                return;
            draft.Name = name.Text ?? "";
            UpdateEquipmentHeader(editor);
        };
        EventHandler<TextChangedEventArgs> notesChanged = (_, _) =>
        {
            if (IsCurrentEquipmentEditor(editor))
                draft.Notes = notes.Text ?? "";
        };
        name.TextChanged += nameChanged;
        notes.TextChanged += notesChanged;
        screen.OnDispose(() =>
        {
            name.TextChanged -= nameChanged;
            notes.TextChanged -= notesChanged;
            if (ReferenceEquals(_equipmentEditor, editor))
                _equipmentEditor = null;
        });
        _equipmentEditor = editor;
        _transient = screen;
        RefreshTypes();
        Present(column, edgeToEdge: true);
        UpdateEquipmentHeader(editor);
        if (draft.EquipmentId is not int loadId || loadId <= 0)
            return;
        try
        {
            var equipment = await InScopeAsync(services =>
                services.GetRequiredService<IEquipmentService>().GetEquipmentByIdAsync(loadId));
            if (!IsCurrentEquipmentEditor(editor))
                return;
            if (equipment is null)
                SetEquipmentError(editor, "Equipment not found");
            else
            {
                draft.ApplyLoadedData(equipment);
                name.Text = draft.Name;
                notes.Text = draft.Notes;
                RefreshTypes();
                UpdateEquipmentHeader(editor);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load equipment {EquipmentId}", equipmentId);
            SetEquipmentError(editor, $"Failed to load equipment: {ErrorMessage(exception)}");
        }
        finally
        {
            if (IsCurrentEquipmentEditor(editor))
            {
                loading.Visibility = ViewStates.Gone;
                scroll.Visibility = ViewStates.Visible;
            }
        }
    }

    private async Task SaveEquipmentAsync(EquipmentEditor editor)
    {
        if (!IsCurrentEquipmentEditor(editor))
            return;
        SetEquipmentError(editor, editor.Draft.ValidationError);
        if (editor.Draft.ValidationError is not null)
            return;
        var input = new EquipmentDraft
        {
            EquipmentId = editor.Draft.EquipmentId, Name = editor.Draft.Name,
            SelectedType = editor.Draft.SelectedType, Notes = editor.Draft.Notes
        };
        var cancellation = _lifetime.Token;
        editor.Save.Text = "SAVING…";
        HideKeyboard();
        try
        {
            var saved = await InScopeAsync(services =>
                services.GetRequiredService<EquipmentWorkflow>().SaveAsync(input));
            if (!IsCurrentEquipmentEditor(editor))
                return;
            await _feedback.ShowSuccessAsync($"'{saved.Name}' {(input.IsEditing ? "updated" : "created")}");
            cancellation.ThrowIfCancellationRequested();
            if (IsCurrentEquipmentEditor(editor))
                await ReturnFromEquipmentAsync(editor);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Saving equipment {EquipmentId} failed", input.EquipmentId);
            SetEquipmentError(editor, $"Failed to save: {ErrorMessage(exception)}");
        }
        finally
        {
            if (IsCurrentEquipmentEditor(editor))
                editor.Save.Text = editor.Draft.IsEditing ? "SAVE" : "ADD";
        }
    }

    private async Task ReturnFromEquipmentAsync(EquipmentEditor editor)
    {
        if (!IsCurrentEquipmentEditor(editor))
            return;
        HideKeyboard();
        if (TryReturnVoiceNavigation()) return;
        if (editor.ReturnTo == EquipmentReturn.List)
        {
            await ShowEquipmentAsync();
            return;
        }
        // Return before the reference refresh, so a successful create is never
        // retried merely because a subsequent list read failed.
        ShowDrink();
        var revision = _presentationRevision;
        try
        {
            await RefreshEquipmentReferencesAsync();
            if (!_destroyed && _page == "drink" && revision == _presentationRevision)
                UpdateDrink(_editingShotId.HasValue ? _editEditor! : _newEditor!);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _equipmentDirty = true;
            _logger.LogError(exception, "Refreshing equipment after returning to the drink failed");
            if (!_destroyed && revision == _presentationRevision)
                ShowFeedback("Equipment list could not refresh. Open the selector to retry.", isError: true);
        }
    }

    private async void CancelEquipmentForm(EquipmentEditor editor)
    {
        if (!IsCurrentEquipmentEditor(editor) || _feedback.IsVisible || _equipmentConfirmation is not null)
            return;
        try
        {
            await ReturnFromEquipmentAsync(editor);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The captured operation may complete, but this Activity is gone.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Returning from the canceled equipment form failed");
            if (!_destroyed)
                ShowFeedback(ErrorMessage(exception), isError: true);
        }
    }

    private async Task ConfirmEquipmentArchiveAsync(EquipmentEditor editor)
    {
        if (!IsCurrentEquipmentEditor(editor) || editor.Draft.EquipmentId is not int id)
            return;
        HideKeyboard();
        var committed = false;
        using var confirmation = new NativeArchiveConfirmation(this, _style, editor.Draft.Name,
            async cancellation =>
            {
                await InScopeAsync(async services =>
                {
                    await services.GetRequiredService<EquipmentWorkflow>().ArchiveAsync(id);
                    return true;
                });
                committed = true;
                cancellation.ThrowIfCancellationRequested();
                if (IsCurrentEquipmentEditor(editor))
                    await _feedback.ShowSuccessAsync($"'{editor.Draft.Name}' archived");
                cancellation.ThrowIfCancellationRequested();
            }, () => !_destroyed && !_feedback.IsVisible, _logger, _lifetime.Token);
        _equipmentConfirmation = confirmation;
        _host.Enabled = false;
        _host.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        try
        {
            var confirmed = await confirmation.ShowAsync();
            if (confirmed && IsCurrentEquipmentEditor(editor))
                await ReturnFromEquipmentAsync(editor);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Equipment archive confirmation failed after commit={Committed}", committed);
            if (committed && IsCurrentEquipmentEditor(editor))
                await ReturnFromEquipmentAsync(editor);
            else
                SetEquipmentError(editor, $"Failed to archive: {ErrorMessage(exception)}");
        }
        finally
        {
            if (ReferenceEquals(_equipmentConfirmation, confirmation))
                _equipmentConfirmation = null;
            if (!_destroyed)
            {
                var blocked = _feedback.IsVisible || _filterScreen is not null;
                _host.Enabled = !blocked;
                _host.ImportantForAccessibility = blocked
                    ? ImportantForAccessibility.NoHideDescendants : ImportantForAccessibility.Auto;
            }
        }
    }
}
