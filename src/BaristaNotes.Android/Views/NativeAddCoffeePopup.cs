using Android.App;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Services;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class NativeAddCoffeePopup : IDisposable
{
    private readonly CoffeeCompletion _completion = new();
    private readonly Activity _activity;
    private readonly NativeStyle _style;
    private readonly IServiceScopeFactory _scopes;
    private readonly IVisionService _vision;
    private readonly Func<CancellationToken, Task<VoicePhoto?>> _scan;
    private readonly Func<bool> _captureSupported;
    private readonly Action<string, bool> _feedback;
    private readonly Action _haptic;
    private readonly Action _released;
    private readonly Func<bool> _ownerActive;
    private readonly ILogger _logger;
    private readonly AddCoffeeViewState _state = new();
    private readonly NativePhotoModal _modal;
    private readonly CancellationToken _cancellation;
    private IReadOnlyList<BeanDto> _recent = [];
    private IReadOnlyList<string> _roasters = [], _origins = [];
    private readonly Dictionary<string, EditText> _entries = [];
    private TextView? _error, _fuzzyHint;
    private Button? _fuzzyUse, _date;
    private BeanDto? _match;
    private DatePickerDialog? _dateDialog;
    private CancellationTokenSource? _fuzzy;
    private bool _saving, _capturing, _scanning, _disposed, _initialized;
    private int _generation;
    public NativePhotoModal Modal => _modal;
    public Action<BagSummaryDto>? OnCreated { get; set; }

    public NativeAddCoffeePopup(Activity activity, NativeStyle style, IServiceScopeFactory scopes, IVisionService vision,
        Func<CancellationToken, Task<VoicePhoto?>> scan, Func<bool> captureSupported, Action<string, bool> feedback,
        Action haptic, Func<bool> ownerActive, Action inputChanged, Action released, ILogger logger,
        CancellationToken cancellation)
    {
        (_activity, _style, _scopes, _vision, _scan, _captureSupported, _feedback, _haptic, _released, _logger) =
            (activity, style, scopes, vision, scan, captureSupported, feedback, haptic, released, logger);
        _ownerActive = ownerActive;
        _modal = new(activity, style, "Add Coffee", "AddCoffee", true, ownerActive, inputChanged, cancellation);
        _cancellation = _modal.Cancellation;
        _modal.CancelRequested = () => Observe(CancelAsync());
        _modal.ActionRequested = () => Observe(SaveTypeAsync());
        _modal.SetContent(LoadingContent(false));
    }

    public async Task InitializeAsync(BeanLabelExtraction? extraction = null)
    {
        if (_initialized)
        {
            if (extraction is not null) { _state.Type(extraction); Render(); }
            return;
        }
        _initialized = true;
        try
        {
            var data = await InScopeAsync(async services =>
            {
                var beans = services.GetRequiredService<IBeanService>();
                // Fresh scoped context; sequential reads avoid concurrent EF operations.
                var recent = await beans.GetRecentBeansAsync(6);
                var roasters = await beans.GetDistinctRoastersAsync(_cancellation);
                var origins = await beans.GetDistinctOriginsAsync(_cancellation);
                return (recent, roasters, origins);
            }, _cancellation);
            if (!_modal.IsAlive) return;
            (_recent, _roasters, _origins) = data;
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { return; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Loading Add Coffee initial choices failed");
            _recent = []; _roasters = []; _origins = [];
        }
        if (!_modal.IsAlive) return;
        _state.Initialize(_recent.Count > 0, extraction);
        Render();
    }

    public Task ShowAsync() => _modal.ShowAsync();
    private bool Current(int generation) => !_disposed && _modal.IsAlive && generation == _generation;
    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> work, CancellationToken cancellation)
    {
        return await Task.Run(async () =>
        {
            using var scope = _scopes.CreateScope();
            return await work(scope.ServiceProvider);
        }, cancellation);
    }
    private async void Observe(Task work)
    {
        try { await work; }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
        catch (Exception exception) { _logger.LogError(exception, "Native Add Coffee operation failed"); }
    }
    private TextView Label(string text, int size, Color color, bool bold = false)
    {
        var label = _style.Label(text, size, color: color);
        label.Typeface = bold ? Typeface.DefaultBold : Typeface.Default;
        return label;
    }
    private Button Button(string text, string id, int height = 28)
    {
        var button = _style.Button(text, id, NativeStyle.ModalText);
        button.Typeface = Typeface.Default;
        button.SetPadding(0, 0, 0, 0);
        button.SetMinWidth(0); button.SetMinimumWidth(0);
        button.SetMinHeight(0); button.SetMinimumHeight(0);
        button.LayoutParameters = new ViewGroup.LayoutParams(-2, _style.Dp(height));
        return button;
    }
    private void Click(NativeScreen screen, View target, int generation, Action action) =>
        screen.Click(target, () => { if (Current(generation) && _modal.CanInteract) action(); });
    private void Render()
    {
        if (!_modal.IsAlive || _disposed) return;
        _generation++;
        _fuzzy?.Cancel(); _fuzzy?.Dispose(); _fuzzy = null;
        _dateDialog?.Dismiss();
        _entries.Clear();
        _error = _fuzzyHint = null; _fuzzyUse = _date = null; _match = null;
        _modal.SetAction(_state.Mode == AddCoffeeMode.Type, _saving ? "Creating..." : "Create", !_saving);
        _modal.SetContent(_state.Mode switch
        {
            AddCoffeeMode.Browse => BuildBrowse(),
            AddCoffeeMode.Scanning => LoadingContent(true),
            _ => BuildType()
        });
    }

    private NativeScreen LoadingContent(bool scanning)
    {
        var body = _style.Column();
        body.SetGravity(GravityFlags.Top | GravityFlags.CenterHorizontal);
        body.SetPadding(_style.Dp(16), _style.Dp(scanning ? 24 : 16), _style.Dp(16), _style.Dp(scanning ? 24 : 16));
        var spinner = new ProgressBar(_activity);
        body.AddView(spinner, new LinearLayout.LayoutParams(-2, -2));
        if (scanning) body.AddView(Label("Reading label…", 14, NativeStyle.ModalSecondary),
            new LinearLayout.LayoutParams(-2, -2) { TopMargin = _style.Dp(12) });
        NativeStyle.Identify(body, scanning ? "AddCoffeeScanning" : "AddCoffeeLoading");
        return new NativeScreen(body);
    }

    private NativeScreen BuildBrowse()
    {
        var generation = _generation;
        var body = _style.Column();
        body.SetClipChildren(false); body.SetClipToPadding(false);
        body.SetPadding(0, _style.Dp(4), 0, _style.Dp(8));
        var screen = new NativeScreen(body);
        var hint = Label("Tap a recent coffee to log a new bag today", 14, NativeStyle.ModalSecondary);
        hint.Gravity = GravityFlags.CenterHorizontal;
        body.AddView(hint, new LinearLayout.LayoutParams(-1, -2) { LeftMargin = _style.Dp(16), RightMargin = _style.Dp(16) });
        var carousel = new HorizontalScrollView(_activity) { HorizontalScrollBarEnabled = false };
        var row = _style.Row();
        row.SetPadding(_style.Dp(16), 0, _style.Dp(16), 0);
        foreach (var bean in _recent)
        {
            var card = _style.Column();
            card.SetGravity(GravityFlags.CenterVertical);
            card.SetPadding(_style.Dp(12), _style.Dp(10), _style.Dp(12), _style.Dp(10));
            card.Background = _style.Rounded(NativeStyle.ModalVariant, 16);
            var name = Label(bean.Name, 14, NativeStyle.ModalText, true);
            name.ImportantForAccessibility = ImportantForAccessibility.No;
            name.SetSingleLine(true); name.Ellipsize = TextUtils.TruncateAt.End;
            var roaster = Label(string.IsNullOrWhiteSpace(bean.Roaster) ? "—" : bean.Roaster, 11, NativeStyle.ModalSecondary);
            roaster.ImportantForAccessibility = ImportantForAccessibility.No;
            roaster.SetSingleLine(true); roaster.Ellipsize = TextUtils.TruncateAt.End;
            card.AddView(name);
            card.AddView(roaster, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(4) });
            card.Focusable = true;
            NativeStyle.Identify(card, $"AddCoffeeRecent_{bean.Id}");
            card.ContentDescription = $"{bean.Name}, {roaster.Text}. Add a bag today.";
            Click(screen, card, generation, () => Observe(UseExistingAsync(bean)));
            row.AddView(card, new LinearLayout.LayoutParams(_style.Dp(140), _style.Dp(80))
            {
                LeftMargin = row.ChildCount > 0 ? _style.Dp(10) : 0
            });
        }
        carousel.AddView(row, new FrameLayout.LayoutParams(-2, -2));
        body.AddView(carousel, new LinearLayout.LayoutParams(-1, -2)
        {
            LeftMargin = -_style.Dp(20), RightMargin = -_style.Dp(20), TopMargin = _style.Dp(16)
        });
        var actions = _style.Row();
        actions.SetGravity(GravityFlags.Center);
        var scan = Button("< Scan a label >", "AddCoffeeBrowseScan", 40);
        scan.SetTextColor(NativeStyle.ModalText);
        scan.Background = _style.Rounded(_style.Primary, 18);
        scan.SetPadding(_style.Dp(20), 0, _style.Dp(20), 0);
        var create = Button("New coffee…", "AddCoffeeNew", 40);
        create.SetTextColor(_style.Primary);
        create.Background = _style.Rounded(Color.Transparent, 18, _style.Primary);
        create.SetPadding(_style.Dp(20), 0, _style.Dp(20), 0);
        Click(screen, scan, generation, () => Observe(ScanAsync()));
        Click(screen, create, generation, () => { _state.Type(); Render(); });
        actions.AddView(scan, new LinearLayout.LayoutParams(-2, _style.Dp(40)) { RightMargin = _style.Dp(10) });
        actions.AddView(create, new LinearLayout.LayoutParams(-2, _style.Dp(40)));
        body.AddView(actions, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(16) });
        return screen;
    }

    private NativeScreen BuildType()
    {
        _state.BeginTypeRender(DateTime.Today);
        var generation = _generation;
        var scroll = new ScrollView(_activity);
        var body = _style.Column();
        body.SetClipChildren(false); body.SetClipToPadding(false);
        scroll.SetClipChildren(false); scroll.SetClipToPadding(false);
        scroll.AddView(body);
        var screen = new NativeScreen(scroll);
        void Add(View child, int top = 10) => body.AddView(child,
            new LinearLayout.LayoutParams(-1, -2) { TopMargin = body.ChildCount == 0 ? 0 : _style.Dp(top) });
        var top = new FrameLayout(_activity);
        if (_recent.Count > 0)
        {
            var browse = Button("← Browse recent", "AddCoffeeBrowse");
            Click(screen, browse, generation, () => { _state.Browse(); Render(); });
            top.AddView(browse, new FrameLayout.LayoutParams(-2, _style.Dp(28), GravityFlags.Start));
        }
        var scan = Button("Scan a label", "AddCoffeeTypeScan");
        Click(screen, scan, generation, () => Observe(ScanAsync()));
        top.AddView(scan, new FrameLayout.LayoutParams(-2, _style.Dp(28), GravityFlags.End));
        Add(top, 0);
        var hint = Label("Add a new coffee to your collection", 14, NativeStyle.ModalSecondary);
        hint.Gravity = GravityFlags.CenterHorizontal;
        Add(hint);
        _error = Label("", 12, Color.Red);
        _error.Gravity = GravityFlags.CenterHorizontal;
        _error.Visibility = ViewStates.Gone;
        NativeStyle.Identify(_error, "AddCoffeeError");
        Add(_error);
        EditText Field(string label, string key, string value, string hintText, bool multiline = false)
        {
            var section = _style.Column();
            var caption = Label(label, 12, NativeStyle.ModalSecondary);
            section.AddView(caption, new LinearLayout.LayoutParams(-1, -2) { LeftMargin = _style.Dp(16) });
            var box = new FrameLayout(_activity);
            box.Background = _style.Rounded(NativeStyle.ModalVariant, multiline ? 16 : 25);
            var entry = new EditText(_activity)
            {
                Text = value, Hint = hintText, Typeface = Typeface.Default,
                InputType = Android.Text.InputTypes.ClassText | (multiline ? Android.Text.InputTypes.TextFlagMultiLine : 0),
                Gravity = multiline ? GravityFlags.Top : GravityFlags.CenterVertical
            };
            entry.SetSingleLine(!multiline);
            entry.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
            entry.SetTextColor(NativeStyle.ModalText);
            entry.SetHintTextColor(NativeStyle.ModalSecondary);
            entry.SetBackgroundColor(Color.Transparent);
            entry.SetPadding(0, 0, 0, 0);
            entry.SetMinHeight(0);
            entry.Enabled = !_saving;
            NativeStyle.Identify(entry, "AddCoffee" + key);
            box.AddView(entry, new FrameLayout.LayoutParams(-1, multiline ? -1 : -2, GravityFlags.CenterVertical)
            {
                LeftMargin = _style.Dp(16), RightMargin = _style.Dp(16),
                TopMargin = multiline ? _style.Dp(8) : 0, BottomMargin = multiline ? _style.Dp(8) : 0
            });
            section.AddView(box, new LinearLayout.LayoutParams(-1, _style.Dp(multiline ? 80 : 50)) { TopMargin = _style.Dp(4) });
            _entries.Add(key, entry);
            Add(section);
            return entry;
        }
        var name = Field("Name *", "Name", _state.Draft.Name, "Ethiopian Yirgacheffe");
        _fuzzyHint = Label("", 12, NativeStyle.ModalSecondary);
        _fuzzyHint.SetPadding(_style.Dp(16), 0, 0, 0); _fuzzyHint.Visibility = ViewStates.Gone;
        NativeStyle.Identify(_fuzzyHint, "AddCoffeeFuzzyHint");
        Add(_fuzzyHint);
        _fuzzyUse = Button("Use it", "AddCoffeeFuzzyUse");
        _fuzzyUse.SetTextColor(_style.Primary);
        _fuzzyUse.SetPadding(_style.Dp(8), 0, _style.Dp(8), 0);
        _fuzzyUse.Visibility = ViewStates.Gone;
        Click(screen, _fuzzyUse, generation, () => { if (_match is { } match) Observe(UseExistingAsync(match)); });
        body.AddView(_fuzzyUse, new LinearLayout.LayoutParams(-2, _style.Dp(28)) { TopMargin = _style.Dp(10) });
        var roaster = Field("Roaster", "Roaster", _state.Draft.Roaster, "Blue Bottle");
        var roasterChips = AddChips(body, screen, roaster, _roasters, generation);
        var origin = Field("Origin", "Origin", _state.Draft.Origin, "Ethiopia");
        var originChips = AddChips(body, screen, origin, _origins, generation);
        var dateSection = _style.Column();
        dateSection.AddView(Label("Roast Date *", 12, NativeStyle.ModalSecondary),
            new LinearLayout.LayoutParams(-1, -2) { LeftMargin = _style.Dp(16) });
        _date = Button("", "AddCoffeeRoastDate", 32);
        _date.SetPadding(_style.Dp(16), 0, _style.Dp(16), 0);
        _date.Enabled = !_saving;
        _date.ContentDescription = "Roast date. Opens a date picker to choose the roast date.";
        Click(screen, _date, generation, () => OpenDate(generation));
        dateSection.AddView(_date, new LinearLayout.LayoutParams(-2, _style.Dp(32)) { TopMargin = _style.Dp(4) });
        Add(dateSection);
        UpdateDate();
        var notes = Field("Notes", "Notes", _state.Draft.Notes, "Tasting notes…", true);
        void Changed(EditText entry, Action<string> change)
        {
            EventHandler<Android.Text.TextChangedEventArgs> handler = (_, _) =>
            {
                if (Current(generation)) change(entry.Text ?? "");
            };
            entry.TextChanged += handler;
            screen.OnDispose(() => entry.TextChanged -= handler);
        }
        Changed(name, text => { _state.Draft.Name = text; ShowError(null); ScheduleFuzzy(generation); });
        Changed(roaster, text => { _state.Draft.Roaster = text; ScheduleFuzzy(generation); roasterChips(text); });
        Changed(origin, text => { _state.Draft.Origin = text; originChips(text); });
        Changed(notes, text => _state.Draft.Notes = text);
        NativeStyle.Identify(scroll, "AddCoffeeTypeScroll");
        return screen;
    }

    private Action<string?> AddChips(LinearLayout body, NativeScreen owner, EditText entry, IReadOnlyList<string> pool, int generation)
    {
        var scroll = new HorizontalScrollView(_activity) { HorizontalScrollBarEnabled = false };
        var host = new FrameLayout(_activity);
        scroll.AddView(host, new FrameLayout.LayoutParams(-2, -2));
        body.AddView(scroll, new LinearLayout.LayoutParams(-1, -2)
        {
            TopMargin = _style.Dp(10), LeftMargin = -_style.Dp(20), RightMargin = -_style.Dp(20)
        });
        NativeScreen? chips = null;
        owner.OnDispose(() => { chips?.Dispose(); chips = null; });
        void Update(string? filter)
        {
            if (!Current(generation)) return;
            host.RemoveAllViews();
            chips?.Dispose();
            var row = _style.Row();
            row.SetPadding(_style.Dp(16), 0, _style.Dp(16), 0);
            chips = new NativeScreen(row);
            foreach (var value in AddCoffeeViewState.Suggestions(pool, filter))
            {
                var chip = Button(value, "AddCoffeeSuggestion", 28);
                chip.SetTextSize(Android.Util.ComplexUnitType.Sp, 12);
                chip.SetPadding(_style.Dp(12), _style.Dp(4), _style.Dp(12), _style.Dp(4));
                chip.Background = _style.Rounded(NativeStyle.ModalVariant, 14);
                Click(chips, chip, generation, () => entry.Text = value);
                row.AddView(chip, new LinearLayout.LayoutParams(-2, _style.Dp(28))
                {
                    LeftMargin = row.ChildCount > 0 ? _style.Dp(6) : 0
                });
            }
            _style.FixTheme(row);
            host.AddView(row, new FrameLayout.LayoutParams(-2, -2));
        }
        Update(null);
        return Update;
    }

    private void OpenDate(int generation)
    {
        if (_saving || _dateDialog is not null) return;
        var value = _state.Draft.RoastDate;
        var dialog = new DatePickerDialog(_activity, (_, args) =>
        {
            if (Current(generation)) { _state.SetDate(args.Date); UpdateDate(); }
        }, value.Year, value.Month - 1, value.Day);
        dialog.DatePicker.MaxDate = new DateTimeOffset(DateTime.Today).ToUnixTimeMilliseconds();
        _dateDialog = dialog;
        EventHandler? dismissed = null;
        dismissed = (_, _) =>
        {
            dialog.DismissEvent -= dismissed;
            if (ReferenceEquals(_dateDialog, dialog)) _dateDialog = null;
            dialog.Dispose();
        };
        dialog.DismissEvent += dismissed;
        dialog.Show();
    }
    private void UpdateDate()
    {
        if (_date is null || !_modal.IsAlive) return;
        var today = _state.Draft.RoastDate.Date == DateTime.Today;
        _date.Text = today ? "Today" : _state.Draft.RoastDate.ToString("MMM d, yyyy");
        _date.SetTextColor(today ? Color.White : NativeStyle.ModalText);
        _date.Background = _style.Rounded(today ? _style.Primary : NativeStyle.ModalVariant, 14);
    }
    private void ScheduleFuzzy(int generation)
    {
        _fuzzy?.Cancel(); _fuzzy?.Dispose();
        _fuzzy = CancellationTokenSource.CreateLinkedTokenSource(_cancellation);
        var cancellation = _fuzzy.Token;
        var name = _state.Draft.Name;
        var roaster = _state.Draft.Roaster;
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2)
        {
            SetFuzzy(null); return;
        }
        Observe(FuzzyAsync(generation, name.Trim(), string.IsNullOrWhiteSpace(roaster) ? null : roaster.Trim(), cancellation));
    }
    private async Task FuzzyAsync(int generation, string name, string? roaster, CancellationToken cancellation)
    {
        try
        {
            await Task.Delay(300, cancellation);
            var match = await InScopeAsync(services =>
                services.GetRequiredService<IBeanService>().FuzzyFindByNameRoasterAsync(name, roaster), cancellation);
            if (!cancellation.IsCancellationRequested && Current(generation)) SetFuzzy(match);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) { _logger.LogError(exception, "Add Coffee fuzzy lookup failed"); }
    }
    private void SetFuzzy(BeanDto? match)
    {
        _match = match;
        if (_fuzzyHint is null || _fuzzyUse is null || !_modal.IsAlive) return;
        _fuzzyHint.Text = match is null ? "" : $"Looks like \"{match.Name}\" — use that?";
        _fuzzyHint.Visibility = _fuzzyUse.Visibility = match is null ? ViewStates.Gone : ViewStates.Visible;
    }
    private void ShowError(string? message)
    {
        if (_error is null || !_modal.IsAlive) return;
        _error.Text = message ?? "";
        _error.Visibility = string.IsNullOrEmpty(message) ? ViewStates.Gone : ViewStates.Visible;
    }
    private void SetSaving(bool saving)
    {
        _saving = saving;
        if (!_modal.IsAlive) return;
        foreach (var entry in _entries.Values) entry.Enabled = !saving;
        if (_date is not null) _date.Enabled = !saving;
        _modal.SetAction(_state.Mode == AddCoffeeMode.Type, saving ? "Creating..." : "Create", !saving);
    }
    private async Task UseExistingAsync(BeanDto bean)
    {
        if (_saving || !_modal.IsAlive) return;
        _saving = true;
        try
        {
            var result = await InScopeAsync(services =>
                services.GetRequiredService<AddCoffeeWorkflow>().AddBagForExistingBeanAsync(bean.Id), CancellationToken.None);
            if (!_modal.IsAlive) return;
            if (!result.Success || result.Data is null)
            {
                _logger.LogError("Creating a bag for existing bean {BeanId} failed: {Error}", bean.Id, result.ErrorMessage);
                _feedback(result.ErrorMessage ?? "Couldn't create bag", true);
                _saving = false; return;
            }
            await CompleteSuccessAsync(result.Data, CoffeeCreationPath.ExistingBean);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Adding existing coffee failed");
            if (_modal.IsAlive) _feedback("Couldn't create bag", true);
            _saving = false;
        }
    }
    private async Task SaveTypeAsync()
    {
        if (_saving || !_modal.IsAlive) return;
        var input = _state.Snapshot();
        if (input.GetValidationError(DateTime.Today) is { } error) { ShowError(error); return; }
        SetSaving(true); ShowError(null);
        try
        {
            var result = await InScopeAsync(services =>
                services.GetRequiredService<AddCoffeeWorkflow>().CreateAsync(input), CancellationToken.None);
            if (!_modal.IsAlive) return;
            if (!result.Bean.Success || result.Bean.Data is null)
            {
                _logger.LogError("Creating Add Coffee bean failed: {Error}", result.Bean.ErrorMessage);
                ShowError(result.Bean.ErrorMessage ?? "Failed to create bean");
            }
            else if (result.InitialBag?.Success != true || result.InitialBag.Data is null)
            {
                _logger.LogError("Creating Add Coffee bag for bean {BeanId} failed: {Error}",
                    result.Bean.Data.Id, result.InitialBag?.ErrorMessage);
                ShowError(result.InitialBag?.ErrorMessage ?? "Failed to create bag");
            }
            else { await CompleteSuccessAsync(result.InitialBag.Data, CoffeeCreationPath.TypeForm); return; }
        }
        catch (Exception exception) { _logger.LogError(exception, "Saving new coffee failed"); ShowError(exception.Message); }
        finally { if (!_disposed) SetSaving(false); }
    }
    private Task CompleteSuccessAsync(BagSummaryDto bag, CoffeeCreationPath path)
    {
        var created = OnCreated;
        var originalOwner = _ownerActive;
        return _completion.CompleteSavedBagAsync(bag, path, _haptic, _modal.CloseAsync,
            originalOwner, created, _feedback, Dispose, _logger);
    }
    private async Task CancelAsync()
    {
        try { await _modal.CloseAsync(); }
        finally { Dispose(); }
    }
    private async Task ScanAsync()
    {
        if (_capturing || _scanning || !_modal.IsAlive) return;
        if (!_captureSupported())
        {
            _logger.LogWarning("Camera capture is not supported for label scanning");
            _feedback("Camera isn't available. Type it instead.", true);
            _state.Type(); Render(); return;
        }
        VoicePhoto? photo;
        var captureGeneration = _generation;
        _capturing = true;
        try { photo = await _scan(_cancellation); }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { return; }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(exception, "Label camera permission denied");
            if (Current(captureGeneration)) { _feedback("Camera permission denied. Type it instead.", true); _state.Type(); Render(); }
            return;
        }
        catch (Java.Lang.SecurityException exception)
        {
            _logger.LogWarning(exception, "Label camera permission denied");
            if (Current(captureGeneration)) { _feedback("Camera permission denied. Type it instead.", true); _state.Type(); Render(); }
            return;
        }
        catch (NotSupportedException exception)
        {
            _logger.LogWarning(exception, "Label camera feature is not supported");
            if (Current(captureGeneration)) { _feedback("Camera isn't available on this device.", true); _state.Type(); Render(); }
            return;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Opening label camera failed");
            if (Current(captureGeneration)) { _feedback("Couldn't open camera. Type it instead.", true); _state.Type(); Render(); }
            return;
        }
        finally { _capturing = false; }
        if (photo is null || !Current(captureGeneration)) return;
        _scanning = true; _state.Scanning(); Render();
        var scanGeneration = _generation;
        BeanLabelExtraction? prefill = null;
        try
        {
            using var stream = await photo.OpenReadAsync();
            var extraction = await _vision.ExtractBeanLabelAsync(stream, CancellationToken.None);
            if (!Current(scanGeneration)) return;
            if (extraction.Success)
            {
                prefill = extraction;
                _haptic();
                _feedback("Filled in — review and save", false);
            }
            else
            {
                _logger.LogWarning("Bean label extraction failed: {Error}", extraction.ErrorMessage);
                _feedback("Couldn't read the label — type it in", true);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Extracting label failed");
            if (Current(scanGeneration)) _feedback("Couldn't read the label — type it in", true);
        }
        finally
        {
            _scanning = false;
            if (Current(scanGeneration)) { _state.Type(prefill); Render(); }
        }
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _generation++;
        _fuzzy?.Cancel(); _fuzzy?.Dispose(); _fuzzy = null;
        _dateDialog?.Dismiss();
        OnCreated = null;
        _modal.Dispose();
        _released();
    }
}
