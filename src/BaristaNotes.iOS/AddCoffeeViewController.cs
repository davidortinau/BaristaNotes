using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class AddCoffeeViewController : PhotoActionModal
{
    private readonly Func<bool> _ownerActive;
    private readonly Action<BagSummaryDto> _created;
    private readonly Action<string, NativeFeedbackKind> _feedback;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource _finished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CoffeeCompletion _completion = new();
    private IReadOnlyList<BeanDto> _recent = [];
    private IReadOnlyList<string> _roasters = [], _origins = [];
    private BeanLabelExtraction? _pending;
    private BeanDto? _match;
    private AddCoffeeDraft _draft = new();
    private CoffeeField? _name, _roaster, _origin, _notes;
    private CoffeeDateField? _date;
    private CoffeeChips? _roasterChips, _originChips;
    private UILabel? _error, _fuzzy;
    private UIButton? _use;
    private CancellationTokenSource? _fuzzyCancellation;
    private bool _initialized, _type, _saving, _scanning, _released, _completing;
    private int _renderVersion;
    public Task Finished => _finished.Task;

    public AddCoffeeViewController(SliceNavigationController host, Func<bool> ownerActive,
        Action<BagSummaryDto> created, Action<string, NativeFeedbackKind> feedback)
        : base(host, "Add Coffee", "coffee")
    {
        _ownerActive = ownerActive;
        _created = created;
        _feedback = feedback;
    }
    public async Task InitializeAsync(BeanLabelExtraction? extraction, CancellationToken token)
    {
        if (_released) return;
        if (extraction != null) _pending = extraction;
        if (_initialized)
        {
            if (extraction != null && !_released)
            {
                _type = true;
                if (IsViewLoaded) Render();
            }
            return;
        }
        _initialized = true;
        try
        {
            var loaded = await Services.RunAsync(async provider =>
            {
                var beans = provider.GetRequiredService<IBeanService>();
                // A scoped DbContext cannot run the source's independent
                // queries concurrently. Preserve their results/order, not overlap.
                var recent = await beans.GetRecentBeansAsync(limit: 6);
                var roasters = await beans.GetDistinctRoastersAsync();
                var origins = await beans.GetDistinctOriginsAsync();
                return (recent, roasters, origins);
            });
            token.ThrowIfCancellationRequested();
            if (_released || !_ownerActive()) return;
            (_recent, _roasters, _origins) = loaded;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error)
        {
            Logger.LogError(error, "Add Coffee initial data read failed");
            _recent = []; _roasters = []; _origins = [];
        }
        _type = extraction != null || _recent.Count == 0;
        if (IsViewLoaded && !_released) Render();
    }
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Scroll.AllowsHorizontalBleed = true;
        Scroll.ClipsToBounds = false;
        Render();
    }
    private void Render()
    {
        if (_released) return;
        _renderVersion++;
        CancelFuzzy();
        SetPrimary("Create", _type && !_scanning, !_saving);
        if (_scanning)
        {
            var spinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium) { Color = NativeTheme.Primary };
            spinner.StartAnimating();
            var label = PhotoUi.Label("Reading label…", 14, TraitCollection);
            label.TextAlignment = UITextAlignment.Center;
            SetContent(new PhotoStack(12, new UIEdgeInsets(24, 16, 24, 16),
                new PhotoFixedBox(spinner, 20), label));
        }
        else if (_type) RenderType();
        else RenderBrowse();
    }
    private void RenderBrowse()
    {
        var hint = PhotoUi.Label("Tap a recent coffee to log a new bag today", 14, TraitCollection);
        hint.TextAlignment = UITextAlignment.Center;
        var weak = new WeakReference<AddCoffeeViewController>(this);
        var row = new CoffeeRecentRow(_recent, TraitCollection, id =>
        {
            if (weak.TryGetTarget(out var owner)) _ = owner.UseExistingAsync(id);
        });
        var scan = PhotoUi.Button("< Scan a label >", "coffee.scan", WeakUiCallback.Create(this, static owner => _ = owner.ScanAsync()), TraitCollection, filled: true);
        var create = PhotoUi.Button("New coffee…", "coffee.new", WeakUiCallback.Create(this, static owner => owner.SwitchMode(true)), TraitCollection, outlined: true);
        SetContent(new PhotoStack(16, new UIEdgeInsets(4, 0, 8, 0),
            new PhotoInsetView(hint, new UIEdgeInsets(0, 16, 0, 16)), row, new CoffeeButtonRow(scan, create, true, 40)));
    }
    private void SwitchMode(bool type)
    {
        if (_released || _saving || _scanning) return;
        _type = type;
        Render();
    }
    private void RenderType()
    {
        var prefill = _pending;
        _pending = null;
        var date = _draft.RoastDate;
        if (prefill?.RoastDate is DateTime parsed && parsed <= DateTime.Today) date = parsed.Date;
        _draft = new AddCoffeeDraft { Name = prefill?.Name ?? "", Roaster = prefill?.Roaster ?? "",
            Origin = prefill?.Origin ?? "", Notes = prefill?.Notes ?? "", RoastDate = date };
        _match = null;
        var version = _renderVersion;
        var weak = new WeakReference<AddCoffeeViewController>(this);
        Action<string> Change(Action<AddCoffeeViewController, string> apply) => value =>
        {
            if (weak.TryGetTarget(out var owner) && !owner._released && owner._renderVersion == version)
                apply(owner, value);
        };
        _name = new CoffeeField("Name *", "Ethiopian Yirgacheffe", "coffee.name", TraitCollection,
            Change((owner, value) => { owner._draft.Name = value; owner.ShowError(null); owner.ScheduleFuzzy(); })) { Text = _draft.Name };
        _roaster = new CoffeeField("Roaster", "Blue Bottle", "coffee.roaster", TraitCollection,
            Change((owner, value) => { owner._draft.Roaster = value; owner.ScheduleFuzzy(); owner._roasterChips?.Update(owner._roasters, value); owner.Relayout(); })) { Text = _draft.Roaster };
        _origin = new CoffeeField("Origin", "Ethiopia", "coffee.origin", TraitCollection,
            Change((owner, value) => { owner._draft.Origin = value; owner._originChips?.Update(owner._origins, value); owner.Relayout(); })) { Text = _draft.Origin };
        _notes = new CoffeeField("Notes", "Tasting notes…", "coffee.notes", TraitCollection,
            Change((owner, value) => owner._draft.Notes = value), multiline: true) { Text = _draft.Notes };
        _date = new CoffeeDateField(_draft.RoastDate, TraitCollection, value =>
        {
            if (weak.TryGetTarget(out var owner) && !owner._released && owner._renderVersion == version) owner._draft.RoastDate = value;
        });
        _error = PhotoUi.Label("", 12, TraitCollection, color: UIColor.Red);
        _error.AccessibilityIdentifier = "coffee.error";
        _error.TextAlignment = UITextAlignment.Center;
        _error.Hidden = true;
        _fuzzy = PhotoUi.Label("", 12, TraitCollection);
        _fuzzy.Hidden = true;
        _fuzzy.AccessibilityIdentifier = "coffee.fuzzy";
        _use = PhotoUi.Button("Use it", "coffee.use", WeakUiCallback.Create(this, static owner =>
        {
            if (owner._match != null) _ = owner.UseExistingAsync(owner._match.Id);
        }), TraitCollection);
        _use.Hidden = true;
        _roasterChips = new CoffeeChips(TraitCollection, Change((owner, value) =>
        {
            owner._roaster!.Text = owner._draft.Roaster = value; owner.ScheduleFuzzy();
            owner._roasterChips?.Update(owner._roasters, value); owner.Relayout();
        }));
        _originChips = new CoffeeChips(TraitCollection, Change((owner, value) =>
        {
            owner._origin!.Text = owner._draft.Origin = value; owner._originChips?.Update(owner._origins, value); owner.Relayout();
        }));
        _roasterChips.Update(_roasters, null);
        _originChips.Update(_origins, null);
        var browse = _recent.Count == 0 ? null : PhotoUi.Button("← Browse recent", "coffee.browse",
            WeakUiCallback.Create(this, static owner => owner.SwitchMode(false)), TraitCollection);
        var scan = PhotoUi.Button("Scan a label", "coffee.scan", WeakUiCallback.Create(this, static owner => _ = owner.ScanAsync()), TraitCollection);
        browse?.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Normal);
        scan.SetTitleColor(NativeTheme.OnPrimary, UIControlState.Normal);
        var hint = PhotoUi.Label("Add a new coffee to your collection", 14, TraitCollection);
        hint.TextAlignment = UITextAlignment.Center;
        SetContent(new CoffeeTypeBody(new CoffeeButtonRow(browse, scan, false, 28), hint, _error,
            _name, _fuzzy, _use, _roaster, _roasterChips, _origin, _originChips, _date, _notes));
    }
    private void ScheduleFuzzy()
    {
        CancelFuzzy();
        var name = _draft.Name.Trim();
        if (name.Length < 2) { _match = null; SetFuzzy(); return; }
        var roaster = string.IsNullOrWhiteSpace(_draft.Roaster) ? null : _draft.Roaster.Trim();
        var cancellation = new CancellationTokenSource();
        _fuzzyCancellation = cancellation;
        var version = _renderVersion;
        _ = FindAsync(name, roaster, version, cancellation.Token);
    }
    private async Task FindAsync(string name, string? roaster, int version, CancellationToken token)
    {
        try
        {
            await Task.Delay(300, token);
            var match = await Services.RunAsync(provider => provider.GetRequiredService<IBeanService>().FuzzyFindByNameRoasterAsync(name, roaster));
            if (token.IsCancellationRequested || _released || version != _renderVersion || !_ownerActive()) return;
            _match = match;
            SetFuzzy();
        }
        catch (OperationCanceledException) { }
        catch (Exception error) { Logger.LogError(error, "Add Coffee fuzzy lookup failed"); }
    }
    private void SetFuzzy()
    {
        if (_fuzzy == null || _use == null) return;
        _fuzzy.Hidden = _use.Hidden = _match == null;
        _fuzzy.Text = _match == null ? "" : $"Looks like \"{_match.Name}\" — use that?";
        Relayout();
    }
    private void ShowError(string? text)
    {
        if (_released || _error == null) return;
        _error.Text = text;
        _error.Hidden = string.IsNullOrEmpty(text);
        Relayout();
    }
    private void SetSaving(bool saving)
    {
        _saving = saving;
        if (_released) return;
        foreach (var field in new[] { _name, _roaster, _origin, _notes }) field?.SetEditable(!saving);
        _date?.SetEditable(!saving);
        SetPrimary(saving ? "Creating..." : "Create", _type && !_scanning, !saving);
    }
    protected override void OnAction() { if (!_saving && !_released) _ = SaveAsync(); }
    private async Task SaveAsync()
    {
        if (!_ownerActive() || _saving || _released) return;
        var snapshot = new AddCoffeeDraft { Name = _draft.Name, Roaster = _draft.Roaster, Origin = _draft.Origin, Notes = _draft.Notes, RoastDate = _draft.RoastDate };
        if (snapshot.GetValidationError(DateTime.Today) is { } validation) { ShowError(validation); return; }
        SetSaving(true);
        ShowError(null);
        try
        {
            var result = await Services.RunAsync(provider => provider.GetRequiredService<AddCoffeeWorkflow>().CreateAsync(snapshot));
            if (_released || !_ownerActive()) return;
            if (!result.Bean.Success || result.Bean.Data == null) { ShowError(result.Bean.ErrorMessage ?? "Failed to create bean"); return; }
            if (result.InitialBag?.Success != true || result.InitialBag.Data == null)
            {
                ShowError(result.InitialBag?.ErrorMessage ?? "Failed to create bag");
                return;
            }
            await CompleteAsync(result.InitialBag.Data, existing: false);
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Add Coffee Type save failed");
            if (!_completion.Started) ShowError(error.Message);
        }
        finally { SetSaving(false); }
    }
    private async Task UseExistingAsync(int beanId)
    {
        if (_saving || _released || !_ownerActive()) return;
        _saving = true;
        try
        {
            var result = await Services.RunAsync(provider => provider.GetRequiredService<AddCoffeeWorkflow>().AddBagForExistingBeanAsync(beanId));
            if (_released || !_ownerActive()) return;
            if (!result.Success || result.Data == null) { _feedback(result.ErrorMessage ?? "Couldn't create bag", NativeFeedbackKind.Error); return; }
            await CompleteAsync(result.Data, existing: true);
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Add Coffee Browse save failed");
            if (!_completion.Started && !_released && _ownerActive()) _feedback("Couldn't create bag", NativeFeedbackKind.Error);
        }
        finally { _saving = false; }
    }
    private Task CompleteAsync(BagSummaryDto bag, bool existing)
    {
        _completing = true;
        var created = _created;
        return _completion.CompleteSavedBagAsync(
            bag,
            existing ? CoffeeCreationPath.ExistingBean : CoffeeCreationPath.TypeForm,
            () => PhotoUi.Haptic(Logger),
            CloseAsync,
            () => !UserCancelled && _ownerActive(),
            created,
            (message, _) => _feedback(message, NativeFeedbackKind.Error),
            Release,
            Logger);
    }
    private async Task ScanAsync()
    {
        if (_scanning || _saving || _released || !_ownerActive()) return;
        var capture = Services.Singleton<INativePhotoCapture>();
        if (!capture.CaptureSupported) { ScanFallback("Camera isn't available. Type it instead."); return; }
        VoicePhoto? photo;
        var version = _renderVersion;
        try { photo = await capture.CaptureAsync(this, NativePhotoRequest.LabelScan, _lifetime.Token); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { return; }
        catch (NativeCameraPermissionException) { ScanFallback("Camera permission denied. Type it instead."); return; }
        catch (NotSupportedException) { ScanFallback("Camera isn't available on this device."); return; }
        catch (Exception error) { Logger.LogError(error, "Label Scan capture failed"); ScanFallback("Couldn't open camera. Type it instead."); return; }
        if (photo == null || _released || !_ownerActive() || version != _renderVersion) return;
        _scanning = true;
        Render();
        version = _renderVersion;
        try
        {
            using var stream = await photo.OpenReadAsync();
            var extraction = await Services.Singleton<IVisionService>().ExtractBeanLabelAsync(stream, CancellationToken.None);
            if (_released || !_ownerActive() || version != _renderVersion) return;
            if (!extraction.Success) { _pending = null; _feedback("Couldn't read the label — type it in", NativeFeedbackKind.Error); }
            else
            {
                _pending = extraction;
                PhotoUi.Haptic(Logger);
                _feedback("Filled in — review and save", NativeFeedbackKind.Information);
            }
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Label extraction failed");
            if (!_released && _ownerActive()) { _pending = null; _feedback("Couldn't read the label — type it in", NativeFeedbackKind.Error); }
        }
        finally
        {
            _scanning = false;
            if (!_released && _ownerActive()) { _type = true; Render(); }
        }
    }
    private void ScanFallback(string message)
    {
        if (_released || !_ownerActive()) return;
        _feedback(message, NativeFeedbackKind.Error);
        _pending = null; _type = true; Render();
    }
    private void CancelFuzzy()
    {
        _fuzzyCancellation?.Cancel();
        _fuzzyCancellation?.Dispose();
        _fuzzyCancellation = null;
    }
    protected override void OnCancelled() { CancelFuzzy(); if (!_released) _lifetime.Cancel(); }
    protected override void OnClosed() { if (!_completing) Release(); }
    private void Release()
    {
        if (_released) return;
        _released = true;
        _renderVersion++;
        try
        {
            CancelFuzzy();
            _lifetime.Cancel();
            if (IsOpen) Retire();
        }
        finally
        {
            _lifetime.Dispose();
            _finished.TrySetResult();
        }
    }
}

internal sealed class CoffeeTypeBody(params UIView[] items) : UIView
{
    public override void MovedToSuperview() { base.MovedToSuperview(); AddSubviews(items); }
    public override CGSize SizeThatFits(CGSize size)
    {
        var visible = items.Where(item => !item.Hidden).ToArray();
        return new(size.Width, visible.Sum(item => (double)(item is UIButton ? 28 : item.SizeThatFits(new CGSize(size.Width, nfloat.MaxValue)).Height))
            + Math.Max(0, visible.Length - 1) * 10);
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        nfloat y = 0;
        foreach (var item in items.Where(item => !item.Hidden))
        {
            var height = item is UIButton ? 28 : item.SizeThatFits(new CGSize(Bounds.Width, nfloat.MaxValue)).Height;
            item.Frame = item is UIButton button
                ? new CGRect(0, y, SliceUi.Measure(button.TitleLabel, Bounds.Width).Width + 16, height)
                : new CGRect(0, y, Bounds.Width, height);
            y += height + 10;
        }
    }
}
