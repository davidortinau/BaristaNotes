using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class BagDateField : UIView
{
    private readonly UILabel _caption = new();
    private readonly UITextField _display = new();
    private readonly UIDatePicker _picker = new();
    private readonly Action<DateTime> _changed;
    public BagDateField(Action<DateTime> changed)
    {
        _changed = changed;
        BackgroundColor = NativeTheme.Surface;
        _display.AccessibilityIdentifier = "bag.date";
        _display.AccessibilityLabel = "Roast date";
        _display.BorderStyle = UITextBorderStyle.None;
        _display.TextColor = NativeTheme.TextPrimary;
        _display.ShouldChangeCharacters = static (_, _, _) => false;
        _picker.Mode = UIDatePickerMode.Date;
        _picker.PreferredDatePickerStyle = UIDatePickerStyle.Wheels;
        _picker.AccessibilityIdentifier = "bag.date.wheel";
        _display.InputView = _picker;
        var changedDate = WeakUiCallback.Create(this, static owner =>
        {
            var date = DateTime.UnixEpoch.AddSeconds(owner._picker.Date.SecondsSince1970).ToLocalTime().Date;
            owner._display.Text = date.ToString("d");
            owner._changed(date);
        });
        _picker.ValueChanged += (_, _) => changedDate();
        var toolbar = new UIToolbar(new CGRect(0, 0, 320, 44));
        var done = WeakUiCallback.Create(this, static owner => owner._display.ResignFirstResponder());
        toolbar.SetItems([new UIBarButtonItem(UIBarButtonSystemItem.FlexibleSpace),
            new UIBarButtonItem(UIBarButtonSystemItem.Done, (_, _) => done())], false);
        _display.InputAccessoryView = toolbar;
        AddSubviews(_caption, _display);
        UpdateFonts(TraitCollection);
    }
    public void SetDate(DateTime date)
    {
        _picker.MaximumDate = NSDate.FromTimeIntervalSince1970((DateTime.Now.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds);
        _picker.SetDate(NSDate.FromTimeIntervalSince1970((date.ToUniversalTime() - DateTime.UnixEpoch).TotalSeconds), false);
        _display.Text = date.ToString("d");
        // Programmatic display does not strip the draft's time component.
    }
    public void UpdateFonts(UITraitCollection traits)
    {
        SourceScaledText.Tracked(_caption, "ROAST DATE", 10, 2, NativeTheme.Secondary, traits);
        _display.Font = SourceScaledText.Font(20, true, traits);
        SetNeedsLayout();
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        (nfloat)Math.Max(90, 36 + SliceUi.Measure(_caption, size.Width - 32).Height + Math.Max(44, _display.Font?.LineHeight ?? 44)));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var height = SliceUi.Measure(_caption, Bounds.Width - 32).Height;
        _caption.Frame = new CGRect(16, 14, Bounds.Width - 32, height);
        _display.Frame = new CGRect(16, 22 + height, Bounds.Width - 32, Bounds.Height - 36 - height);
    }
}

internal sealed class BagDetailViewController : BeanBagPage
{
    private readonly BagDraft _draft;
    private readonly BagDateField _date;
    private readonly BeanNotes _notes;
    private readonly BagStatusView _status;
    private readonly BeanSection _stats;
    private readonly BeanSection _ratings;
    private EquipmentActionButton? _save;
    private RatingAggregateDto? _rating;
    private int _shotCount;
    private bool _loaded;
    private bool _saving;
    private bool _statusBusy;
    private bool _committed;
    private bool _uncertainCreate, _deleted;
    private int _loadVersion;

    public BagDetailViewController(SliceNavigationController host, int beanId, string beanName, int? bagId = null) : base(host, "bag")
    {
        _draft = new BagDraft { BeanId = beanId, BeanName = beanName, BagId = bagId };
        var weak = new WeakReference<BagDetailViewController>(this);
        _date = new BagDateField(date => { if (weak.TryGetTarget(out var owner)) owner._draft.RoastDate = date; });
        _notes = new BeanNotes("From Trader Joe's, gift from friend…", "bag.notes",
            text => { if (weak.TryGetTarget(out var owner)) owner._draft.Notes = text; });
        _status = new BagStatusView(WeakUiCallback.Create(this, static owner => _ = owner.ChangeStatusAsync()));
        _stats = new BeanSection("SHOTS LOGGED", "bag.stats", minimum: 100);
        _ratings = new BeanSection("RATINGS", "bag.ratings", spacing: 8, minimum: 90);
    }
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Header.Update(_draft.IsEditing ? "EDIT BAG" : "NEW BAG", string.IsNullOrEmpty(_draft.BeanName) ? "Bag" : _draft.BeanName);
        _date.SetDate(_draft.RoastDate);
        AddSection(_date);
        AddSection(_notes);
        if (_draft.IsEditing)
        {
            AddSection(_status);
            AddSection(_stats);
            AddSection(_ratings);
        }
        else
        {
            AddSection(new BeanSpacer(0));
            AddSection(new BeanSpacer(0));
            AddSection(new BeanSpacer(0));
        }
        AddAction(new EquipmentActionButton("CANCEL", "bag.cancel",
            WeakUiCallback.Create(this, static owner =>
            {
                if (owner.Host.TopViewController == owner && owner.Host.PresentedViewController == null)
                    owner.Host.RequestBack();
            })));
        if (_draft.IsEditing)
            AddAction(new EquipmentActionButton("DELETE", "bag.delete",
                WeakUiCallback.Create(this, static owner => owner.ConfirmDelete()), danger: true));
        _save = new EquipmentActionButton(_draft.IsEditing ? "SAVE" : "ADD", "bag.save",
            WeakUiCallback.Create(this, static owner => _ = owner.SaveAsync()), inverted: true);
        AddAction(_save);
        RefreshFonts();
    }
    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        if (!_loaded) _ = LoadAsync();
    }
    private async Task LoadAsync()
    {
        if (!_draft.IsEditing) { _loaded = true; return; }
        var generation = Generation;
        var version = ++_loadVersion;
        SetLoading(true);
        try
        {
            var result = await Services.RunAsync(provider =>
                provider.GetRequiredService<BagDetailsWorkflow>().LoadAsync(_draft.BagId!.Value));
            if (!Alive(generation) || version != _loadVersion) return;
            if (result.Bag == null) { SetError("Bag not found"); return; }
            _draft.ApplyLoadedData(result.Bag, _draft.BeanName);
            _date.SetDate(_draft.RoastDate);
            _notes.Text = _draft.Notes;
            _shotCount = result.ShotCount;
            _rating = result.Rating;
            _loaded = true;
            Header.Update("EDIT BAG", _draft.BeanName);
            RenderReadOnly();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Failed to load bag {BagId}", _draft.BagId);
            if (Current(generation)) SetError($"Failed to load bag: {error.Message}");
        }
        finally { if (Current(generation)) SetLoading(false); }
    }
    private async Task SaveAsync()
    {
        if (!_loaded || _saving || _committed || _uncertainCreate || Host.TopViewController != this || Host.PresentedViewController != null) return;
        var snapshot = new BagDraft { BagId = _draft.BagId, BeanId = _draft.BeanId, BeanName = _draft.BeanName,
            RoastDate = _draft.RoastDate, Notes = _draft.Notes, IsComplete = _draft.IsComplete };
        if (snapshot.GetValidationError(DateTime.Now) is { } validation) { SetError(validation); return; }
        var generation = Generation;
        _saving = true;
        _save?.SetText("SAVING…");
        SetError(null);
        try
        {
            var result = await Services.RunAsync(provider => provider.GetRequiredService<BagWorkflow>().SaveAsync(snapshot));
            if (!Current(generation)) return;
            if (!result.Success) { SetError(result.ErrorMessage ?? "Failed to save bag"); return; }
            _committed = true;
            Root.EndEditing(true);
            await Host.FeedbackHost.ShowAndWaitAsync(snapshot.IsEditing ? "Bag updated" : $"Bag added for {snapshot.BeanName}");
            if (Current(generation)) Host.RequestBack();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Bag save failed for {BagId}", snapshot.BagId);
            if (!snapshot.IsEditing && !_committed) _uncertainCreate = true;
            if (Current(generation))
                SetError(_committed ? "Bag saved, but return failed. Cancel to check the saved bag." :
                    _uncertainCreate ? $"Save status requires checking the bags before retrying: {error.Message}" : $"Failed to save bag: {error.Message}");
        }
        finally
        {
            _saving = false;
            if (Current(generation)) { _save?.SetText(_draft.IsEditing ? "SAVE" : "ADD"); if (_save != null) _save.Enabled = !_committed && !_uncertainCreate; }
        }
    }
    private async Task ChangeStatusAsync()
    {
        if (!_loaded || !_draft.IsEditing || _statusBusy || Host.TopViewController != this || Host.PresentedViewController != null) return;
        var generation = Generation;
        var id = _draft.BagId!.Value;
        var complete = !_draft.IsComplete;
        _statusBusy = true;
        try
        {
            await Services.RunAsync(async provider =>
            {
                var bags = provider.GetRequiredService<IBagService>();
                if (complete) await bags.MarkBagCompleteAsync(id); else await bags.ReactivateBagAsync(id);
                return true;
            });
            if (Current(generation))
                await Host.FeedbackHost.ShowAndWaitAsync(complete ? "Bag marked as complete" : "Bag reactivated");
            Services.Singleton<IDataChangeNotifier>().NotifyDataChanged(DataChangeType.BagUpdated, id);
            if (Current(generation)) { _draft.IsComplete = complete; RenderReadOnly(); }
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Bag status update failed for {BagId}", id);
            if (Current(generation)) SetError($"Failed to update status: {error.Message}");
        }
        finally { _statusBusy = false; }
    }
    private void ConfirmDelete()
    {
        if (!_loaded || !_draft.IsEditing || Host.TopViewController != this || Host.PresentedViewController != null) return;
        var id = _draft.BagId!.Value;
        var generation = Generation;
        var weak = new WeakReference<BagDetailViewController>(this);
        Host.PresentViewController(new EquipmentConfirmationViewController(Host, "Delete Bag?",
            $"Are you sure you want to delete this bag? This will also delete all {_shotCount} associated shot records. This action cannot be undone.",
            "Delete", async () =>
            {
                if (!weak.TryGetTarget(out var owner)) return;
                if (!owner._deleted)
                {
                    await owner.Services.RunAsync(async provider => { await provider.GetRequiredService<BagWorkflow>().DeleteAsync(id); return true; });
                    owner._deleted = true;
                }
                if (owner.Alive(generation)) await owner.Host.FeedbackHost.ShowAndWaitAsync("Bag deleted");
            }, () =>
            {
                if (weak.TryGetTarget(out var owner) && owner.Current(generation)) owner.Host.RequestBack();
            }, operation: "delete bag"), false, null);
    }
    private void RenderReadOnly()
    {
        _status.Update(_draft.IsComplete, TraitCollection);
        _stats.SetItems([BeanBagUi.Text(_shotCount.ToString(), 28, true, NativeTheme.TextPrimary, TraitCollection)]);
        _ratings.SetItems(_rating is { HasRatings: true }
            ? [new BeanRatingView(_rating, TraitCollection)]
            : [BeanBagUi.Text("No ratings yet", 16, false, NativeTheme.Secondary, TraitCollection)]);
        Relayout();
    }
    protected override void RefreshFonts()
    {
        base.RefreshFonts();
        _date.UpdateFonts(TraitCollection);
        _notes.UpdateFonts(TraitCollection);
        foreach (var section in new[] { _stats, _ratings }) section.UpdateFonts(TraitCollection);
        RenderReadOnly();
    }
}
