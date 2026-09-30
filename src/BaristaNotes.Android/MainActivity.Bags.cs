using Android.App;
using Android.Text;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.AndroidApp.Services;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private sealed record BagEditor(
        NativeScreen Screen, BagDraft Draft, BeanDetailState Parent, TextView Title,
        Button Date, EditText Notes, TextView Status, Button StatusAction,
        TextView ShotCount, FrameLayout Ratings, LinearLayout ErrorTile,
        TextView ErrorText, Button Save)
    {
        public int LoadedShotCount { get; set; }
        public bool Saving { get; set; }
        public bool ChangingStatus { get; set; }
        public DatePickerDialog? DateDialog { get; set; }
    }

    private BagEditor? _bagEditor;
    private bool IsCurrentBag(BagEditor editor) =>
        !_destroyed && ReferenceEquals(_bagEditor, editor) && ReferenceEquals(_transient, editor.Screen);

    private async Task ShowBagDetailAsync(BeanDetailState parent, int? bagId)
    {
        var beanId = parent.Draft.BeanId ?? throw new InvalidOperationException("A saved bean is required.");
        HideKeyboard();
        ClearTransient();
        _page = "bagDetail";
        var draft = new BagDraft { BagId = bagId, BeanId = beanId, BeanName = parent.Draft.Name };
        var root = _style.Column();
        root.SetBackgroundColor(_style.Outline);
        var screen = new NativeScreen(root);
        var header = (ViewGroup)BuildHeader(draft.IsEditing ? "EDIT BAG" : "NEW BAG",
            string.IsNullOrEmpty(draft.BeanName) ? "Bag" : draft.BeanName);
        var title = (TextView)header.GetChildAt(1)!;
        title.SetMaxLines(2);
        NativeStyle.Identify(title, "BagDetailTitle");
        root.AddView(header, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var body = new FrameLayout(this);
        body.SetBackgroundColor(_style.Surface);
        var scroll = new ScrollView(this) { FillViewport = true };
        NativeStyle.Identify(scroll, "BagDetailScroll");
        var fields = _style.Column();
        fields.SetBackgroundColor(_style.Outline);
        scroll.AddView(fields);
        body.AddView(scroll, new FrameLayout.LayoutParams(-1, -1));
        var loading = new ProgressBar(this);
        loading.Visibility = draft.IsEditing ? ViewStates.Visible : ViewStates.Gone;
        scroll.Visibility = draft.IsEditing ? ViewStates.Gone : ViewStates.Visible;
        body.AddView(loading, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Center));
        root.AddView(body, _style.Fill(weight: 1));
        void Add(View view) => fields.AddView(view, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var dateTile = BeanSection("ROAST DATE", 90);
        var date = _style.Button(draft.RoastDate.ToString("d"), "BagRoastDate", _style.Text);
        date.Typeface = _style.Bold;
        date.SetTextSize(Android.Util.ComplexUnitType.Sp, 20);
        date.Gravity = GravityFlags.Start | GravityFlags.CenterVertical;
        date.SetPadding(0, 0, 0, 0);
        dateTile.AddView(date, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(8) });
        Add(dateTile);
        var notesTile = BeanSection("NOTES", 150);
        var notes = new EditText(this)
        {
            Hint = "From Trader Joe's, gift from friend…",
            Typeface = _style.Regular, InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine,
            Gravity = GravityFlags.Top | GravityFlags.Start
        };
        notes.SetTextColor(_style.Text);
        notes.SetHintTextColor(Android.Graphics.Color.Argb(128, _style.Secondary.R, _style.Secondary.G, _style.Secondary.B));
        notes.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
        notes.SetBackgroundColor(Android.Graphics.Color.Transparent);
        notes.SetPadding(0, 0, 0, 0);
        NativeStyle.Identify(notes, "BagNotes");
        notesTile.AddView(notes, new LinearLayout.LayoutParams(-1, _style.Dp(100)));
        Add(notesTile);
        var statusTile = _style.Row();
        statusTile.SetBackgroundColor(_style.Surface);
        statusTile.SetPadding(_style.Dp(16), _style.Dp(16), _style.Dp(16), _style.Dp(16));
        statusTile.SetMinimumHeight(_style.Dp(100));
        statusTile.SetGravity(GravityFlags.CenterVertical);
        var statusLabels = _style.Column();
        statusLabels.AddView(BeanSectionCaption("STATUS"));
        var status = _style.Label("ACTIVE", 22, true);
        NativeStyle.Identify(status, "BagStatus");
        statusLabels.AddView(status);
        statusTile.AddView(statusLabels, new LinearLayout.LayoutParams(0, -2, 1));
        var statusAction = BeanMiniAction("MARK COMPLETE", "BagToggleStatus");
        statusAction.SetPadding(_style.Dp(14), _style.Dp(10), _style.Dp(14), _style.Dp(10));
        statusAction.SetMinHeight(_style.Dp(44));
        statusAction.SetMinimumHeight(_style.Dp(44));
        statusTile.AddView(statusAction, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = _style.Dp(8) });
        statusTile.Visibility = draft.IsEditing ? ViewStates.Visible : ViewStates.Gone;
        Add(statusTile);
        var statsTile = BeanSection("SHOTS LOGGED", 100);
        statsTile.SetGravity(GravityFlags.CenterVertical);
        statsTile.SetPadding(_style.Dp(16), _style.Dp(16), _style.Dp(16), _style.Dp(16));
        var count = _style.Label("0", 28, true);
        NativeStyle.Identify(count, "BagShotCount");
        statsTile.AddView(count);
        statsTile.Visibility = draft.IsEditing ? ViewStates.Visible : ViewStates.Gone;
        Add(statsTile);
        var ratings = new FrameLayout(this);
        ratings.Visibility = draft.IsEditing ? ViewStates.Visible : ViewStates.Gone;
        Add(ratings);
        var (errorTile, error) = BeanErrorTile();
        NativeStyle.Identify(error, "BagError");
        Add(errorTile);
        var spacer = new View(this);
        spacer.SetBackgroundColor(_style.Surface);
        fields.AddView(spacer, new LinearLayout.LayoutParams(-1, _style.Dp(24)));
        var actions = _style.Row();
        var cancel = BeanAction("CANCEL", "BagCancel");
        var save = BeanAction(draft.IsEditing ? "SAVE" : "ADD", "BagSave", inverted: true);
        var editor = new BagEditor(screen, draft, parent, title, date, notes, status,
            statusAction, count, ratings, errorTile, error, save);
        Bind(screen, date, () => OpenBagDate(editor));
        Bind(screen, statusAction, () => ObserveBeanTask(() => ToggleBagStatusAsync(editor)));
        Bind(screen, cancel, () => ObserveBeanTask(() => ReturnFromBagAsync(editor)));
        Bind(screen, save, () => ObserveBeanTask(() => SaveBagAsync(editor)));
        actions.AddView(cancel, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = _style.Dp(1) });
        if (draft.IsEditing)
        {
            var delete = BeanAction("DELETE", "BagDelete", danger: true);
            Bind(screen, delete, () => RunOperation(() => DeleteBagAsync(editor)));
            actions.AddView(delete, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = _style.Dp(1) });
        }
        actions.AddView(save, new LinearLayout.LayoutParams(0, -1, 1));
        root.AddView(actions);
        EventHandler<TextChangedEventArgs> changed = (_, _) =>
        {
            if (IsCurrentBag(editor)) draft.Notes = notes.Text ?? "";
        };
        notes.TextChanged += changed;
        screen.OnDispose(() =>
        {
            notes.TextChanged -= changed;
            editor.DateDialog?.Dismiss();
            editor.DateDialog?.Dispose();
            editor.DateDialog = null;
            if (ReferenceEquals(_bagEditor, editor)) _bagEditor = null;
        });
        _bagEditor = editor;
        _transient = screen;
        UpdateBagHeader(editor);
        UpdateBagStatus(editor);
        RenderBagRatings(editor, null);
        Present(root, edgeToEdge: true);
        if (draft.BagId is not int id || id <= 0) return;
        try
        {
            var result = await InScopeAsync(services =>
                services.GetRequiredService<BagDetailsWorkflow>().LoadAsync(id));
            if (!IsCurrentBag(editor)) return;
            if (result.Bag is null)
                SetBagError(editor, "Bag not found");
            else
            {
                draft.ApplyLoadedData(result.Bag, parent.Draft.Name);
                editor.LoadedShotCount = result.ShotCount;
                count.Text = editor.LoadedShotCount.ToString();
                notes.Text = draft.Notes;
                date.Text = draft.RoastDate.ToString("d");
                UpdateBagHeader(editor);
                UpdateBagStatus(editor);
                RenderBagRatings(editor, result.Rating);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Loading bag {BagId} failed", id);
            SetBagError(editor, $"Failed to load bag: {ErrorMessage(exception)}");
        }
        finally
        {
            if (IsCurrentBag(editor))
            {
                loading.Visibility = ViewStates.Gone;
                scroll.Visibility = ViewStates.Visible;
            }
        }
    }

    private void UpdateBagHeader(BagEditor editor)
    {
        var text = string.IsNullOrEmpty(editor.Draft.BeanName) ? "Bag" : editor.Draft.BeanName;
        editor.Title.Text = text;
        editor.Title.SetTextSize(Android.Util.ComplexUnitType.Sp,
            text.Length <= 12 ? 28 : text.Length <= 20 ? 22 : text.Length <= 28 ? 18 : 16);
    }

    private void OpenBagDate(BagEditor editor)
    {
        if (!IsCurrentBag(editor) || editor.DateDialog is not null) return;
        var value = editor.Draft.RoastDate;
        var dialog = new DatePickerDialog(this, (_, args) =>
        {
            if (!IsCurrentBag(editor)) return;
            var date = args.Date;
            if (date.Date != editor.Draft.RoastDate.Date)
                editor.Draft.RoastDate = date;
            editor.Date.Text = editor.Draft.RoastDate.ToString("d");
        }, value.Year, value.Month - 1, value.Day);
        dialog.DatePicker.MaxDate = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        EventHandler? dismissed = null;
        dismissed = (_, _) =>
        {
            dialog.DismissEvent -= dismissed;
            if (ReferenceEquals(editor.DateDialog, dialog)) editor.DateDialog = null;
            dialog.Dispose();
        };
        dialog.DismissEvent += dismissed;
        editor.DateDialog = dialog;
        dialog.Show();
    }

    private void UpdateBagStatus(BagEditor editor)
    {
        if (!IsCurrentBag(editor)) return;
        editor.Status.Text = editor.Draft.IsComplete ? "COMPLETE" : "ACTIVE";
        editor.StatusAction.Text = editor.Draft.IsComplete ? "REACTIVATE" : "MARK COMPLETE";
        editor.StatusAction.Enabled = !editor.ChangingStatus && !editor.Saving;
    }

    private void RenderBagRatings(BagEditor editor, RatingAggregateDto? rating)
    {
        editor.Ratings.RemoveAllViews();
        var section = BeanSection("RATINGS", 90);
        View content = rating is { HasRatings: true }
            ? new RatingSummaryView(_style, rating)
            : _style.Label("No ratings yet", 16, color: _style.Secondary);
        section.AddView(content, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(8) });
        editor.Ratings.AddView(section);
    }

    private void SetBagError(BagEditor editor, string? message)
    {
        if (!IsCurrentBag(editor)) return;
        editor.ErrorText.Text = message ?? "";
        editor.ErrorTile.Visibility = message is null ? ViewStates.Gone : ViewStates.Visible;
    }

    private async Task SaveBagAsync(BagEditor editor)
    {
        if (!IsCurrentBag(editor) || editor.Saving || editor.ChangingStatus) return;
        SetBagError(editor, editor.Draft.GetValidationError(DateTime.Now));
        if (editor.Draft.GetValidationError(DateTime.Now) is not null) return;
        var input = new BagDraft
        {
            BagId = editor.Draft.BagId, BeanId = editor.Draft.BeanId, BeanName = editor.Draft.BeanName,
            RoastDate = editor.Draft.RoastDate, Notes = editor.Draft.Notes, IsComplete = editor.Draft.IsComplete
        };
        editor.Saving = true;
        editor.Save.Text = "SAVING…";
        UpdateBagStatus(editor);
        HideKeyboard();
        try
        {
            var result = await InScopeAsync(services => services.GetRequiredService<BagWorkflow>().SaveAsync(input));
            if (!IsCurrentBag(editor)) return;
            if (!result.Success)
            {
                SetBagError(editor, result.ErrorMessage ?? "Failed to save bag");
                return;
            }
            await _feedback.ShowSuccessAsync(input.IsEditing ? "Bag updated" : $"Bag added for {input.BeanName}");
            if (IsCurrentBag(editor)) await ReturnFromBagAsync(editor);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Saving bag {BagId} failed", input.BagId);
            SetBagError(editor, $"Failed to save bag: {ErrorMessage(exception)}");
        }
        finally
        {
            editor.Saving = false;
            if (IsCurrentBag(editor))
            {
                editor.Save.Text = editor.Draft.IsEditing ? "SAVE" : "ADD";
                UpdateBagStatus(editor);
            }
        }
    }

    private async Task ToggleBagStatusAsync(BagEditor editor)
    {
        if (!IsCurrentBag(editor) || editor.ChangingStatus || editor.Saving || editor.Draft.BagId is not int id) return;
        var wasComplete = editor.Draft.IsComplete;
        var cancellation = _lifetime.Token;
        editor.ChangingStatus = true;
        UpdateBagStatus(editor);
        try
        {
            await InScopeAsync(async services =>
            {
                var bags = services.GetRequiredService<IBagService>();
                if (wasComplete) await bags.ReactivateBagAsync(id); else await bags.MarkBagCompleteAsync(id);
                return true;
            });
            await _feedback.ShowSuccessAsync(wasComplete ? "Bag reactivated" : "Bag marked as complete");
            cancellation.ThrowIfCancellationRequested();
            _app.Services.GetRequiredService<IDataChangeNotifier>().NotifyDataChanged(DataChangeType.BagUpdated, id);
            if (IsCurrentBag(editor)) editor.Draft.IsComplete = !wasComplete;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Updating bag {BagId} status failed", id);
            SetBagError(editor, $"Failed to update status: {ErrorMessage(exception)}");
        }
        finally
        {
            editor.ChangingStatus = false;
            if (IsCurrentBag(editor)) UpdateBagStatus(editor);
        }
    }

    private Task ReturnFromBagAsync(BagEditor editor)
    {
        if (!IsCurrentBag(editor)) return Task.CompletedTask;
        HideKeyboard();
        if (TryReturnVoiceNavigation()) return Task.CompletedTask;
        return ShowBeanDetailAsync(editor.Parent, loadBean: false);
    }

    private async Task DeleteBagAsync(BagEditor editor)
    {
        if (!IsCurrentBag(editor) || editor.Draft.BagId is not int id) return;
        await ConfirmBeanBagDeleteAsync(editor.Screen,
            new SimpleActionContent("Delete Bag?",
                $"Are you sure you want to delete this bag? This will also delete all {editor.LoadedShotCount} associated shot records. This action cannot be undone.",
                "Delete", "BagDelete"), "Bag deleted",
            async () => { await InScopeAsync(async services =>
            {
                await services.GetRequiredService<BagWorkflow>().DeleteAsync(id);
                return true;
            }); }, () => ReturnFromBagAsync(editor), message => SetBagError(editor, message));
    }
}
