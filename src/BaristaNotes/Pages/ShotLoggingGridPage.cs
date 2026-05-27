using MauiReactor;
using BaristaNotes.Core.Services.Exceptions;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

/// <summary>
/// One line of the voice chat transcript shown in the voice overlay.
/// </summary>
public class VoiceChatMessage
{
    public bool IsUser { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool IsError { get; set; }
}

/// <summary>
/// Identifies which full-screen picker is currently open.
/// </summary>
enum GridPickerKind
{
    None,
    BrewMethod,
    Bag,
    MadeBy,
    MadeFor,
    DrinkType,
    Machine,
    Grinder,
    Accessories,
    Rating,
    DoseIn,
    YieldOut,
    ActualTime,
    GrindMicrons,
}

class ShotLoggingGridState
{
    // Shot values
    public BrewMethod BrewMethod { get; set; } = BrewMethod.Espresso;
    public string DrinkType { get; set; } = "Espresso";
    public decimal DoseIn { get; set; } = 18.0m;
    public decimal ExpectedOutput { get; set; } = 36.0m;
    public decimal ExpectedTime { get; set; } = 28;
    public decimal? ActualOutput { get; set; }
    public decimal? ActualTime { get; set; }

    /// <summary>
    /// Grind size in microns (canonical, grinder-agnostic). Null = not
    /// yet selected for this session. The picker shows the brew-method's
    /// default if null when opened.
    /// </summary>
    public int? GrindMicrons { get; set; }

    /// <summary>
    /// Transient grind hint surfaced when a recipe was applied but didn't
    /// resolve to an explicit micron value (e.g. recipe says "medium-fine"
    /// without a number). Display-only — not persisted. The user picks an
    /// explicit µm before save; null GrindMicrons is allowed at save time
    /// and means "not recorded".
    /// </summary>
    public string? PendingGrindHint { get; set; }

    public int Rating { get; set; } = 2; // UI 0-4 (1-5 service)
    public string? TastingNotes { get; set; }

    public int? SelectedBagId { get; set; }
    public UserProfileDto? SelectedMaker { get; set; }
    public UserProfileDto? SelectedRecipient { get; set; }
    public int? SelectedMachineId { get; set; }
    public int? SelectedGrinderId { get; set; }
    public List<int> SelectedAccessoryIds { get; set; } = new();

    // Reference lists
    public List<BagSummaryDto> AvailableBags { get; set; } = new();
    public List<UserProfileDto> AvailableUsers { get; set; } = new();
    public List<EquipmentDto> AvailableEquipment { get; set; } = new();

    // Edit mode display
    public string? BeanName { get; set; }

    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }

    // Picker overlay state
    public GridPickerKind ActivePicker { get; set; } = GridPickerKind.None;

    // Grind picker overlay-only state. Populated when the µm picker opens.
    /// <summary>Currently highlighted (but not yet committed) micron value.</summary>
    public int? PickerGrindMicrons { get; set; }
    /// <summary>Anchors for the currently-selected grinder, if any. Used to
    /// render the live translation badge without re-querying the DB on
    /// every scroll tick.</summary>
    public IReadOnlyList<BaristaNotes.Core.Services.Grind.GrindAnchor>? PickerGrindAnchors { get; set; }
    /// <summary>Cached display name for the picker badge ("DF64", etc.).</summary>
    public string? PickerGrinderName { get; set; }
    /// <summary>Indicates the selected grinder has no anchor data (neither
    /// configured nor seeded). Picker shows the "Set up scale →" CTA.</summary>
    public bool PickerGrinderUncalibrated { get; set; }

    // Voice command state — drives the window-overlay voice UI.
    public bool IsVoiceSheetOpen { get; set; }
    public bool IsRecording { get; set; }
    public string VoiceTranscript { get; set; } = "";
    public SpeechRecognitionState VoiceState { get; set; } = SpeechRecognitionState.Idle;
    public bool VoiceCommandCommitted { get; set; }
    public List<VoiceChatMessage> VoiceChatHistory { get; set; } = new();
    public string LastAIResponse { get; set; } = "";
}

class ShotLoggingGridPageProps
{
    public int? ShotId { get; set; }
}

partial class ShotLoggingGridPage : Component<ShotLoggingGridState, ShotLoggingGridPageProps>
{
    /// <summary>
    /// When true, the next mount of this page will auto-open the voice
    /// overlay after data load. Used by ActivityFeedPage's Voice NavTile.
    /// </summary>
    public static bool OpenVoiceOnNextMount { get; set; }

    [Inject] IShotService _shotService;
    [Inject] IBagService _bagService;
    [Inject] IEquipmentService _equipmentService;
    [Inject] IUserProfileService _userProfileService;
    [Inject] IPreferencesService _preferencesService;
    [Inject] IFeedbackService _feedbackService;
    [Inject] BaristaNotes.Core.Data.Repositories.IGrinderProfileRepository _grinderProfiles;
    [Inject] BaristaNotes.Core.Data.Repositories.IShotRecordRepository _shotRecords;
    [Inject] BaristaNotes.Core.Data.Repositories.IBagRepository _bagRepo;
    [Inject] ILogger<ShotLoggingGridPage> _logger;
    [Inject] ISpeechRecognitionService _speechRecognitionService;
    [Inject] IVoiceCommandService _voiceCommandService;
    [Inject] IOverlayService _overlayService;
    [Inject] IVisionService _visionService;
    [Inject] IDataChangeNotifier _dataChangeNotifier;

    // Cancellation token for voice commands.
    private CancellationTokenSource? _voiceCts;

    // Pauses speech recognition when the camera capture flow is active.
    private bool _speechPaused;

    // Silence detection — 1.5s without partial results stops recognition
    // to trigger a higher-quality final result.
    private System.Timers.Timer? _silenceTimer;
    private const double SilenceTimeoutMs = 1500;
    private readonly object _silenceLock = new();

    protected override void OnMounted()
    {
        base.OnMounted();
        SetState(s => s.IsLoading = true);
        _ = LoadDataAsync().ContinueWith(_ =>
        {
            if (OpenVoiceOnNextMount)
            {
                OpenVoiceOnNextMount = false;
                MainThread.BeginInvokeOnMainThread(async () => await ToggleVoiceSheetAsync());
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());

        // Refresh pickers when voice commands create beans/bags/equipment/profiles.
        _dataChangeNotifier.DataChanged += OnDataChanged;

        // Overlay events drive the voice UI.
        _overlayService.CloseRequested += OnOverlayCloseRequested;
        _overlayService.ExpandRequested += OnOverlayExpandRequested;
        _overlayService.MicPressStarted += OnMicPressStarted;
        _overlayService.MicPressEnded += OnMicPressEnded;

        // Voice command service can request the recognizer pause/resume
        // (e.g. while the camera flow is active).
        _voiceCommandService.PauseSpeechRequested += OnPauseSpeechRequested;
        _voiceCommandService.ResumeSpeechRequested += OnResumeSpeechRequested;
    }

    protected override void OnWillUnmount()
    {
        _voiceCts?.Cancel();
        _voiceCts?.Dispose();
        StopSilenceTimer();

        _dataChangeNotifier.DataChanged -= OnDataChanged;
        _overlayService.CloseRequested -= OnOverlayCloseRequested;
        _overlayService.ExpandRequested -= OnOverlayExpandRequested;
        _overlayService.MicPressStarted -= OnMicPressStarted;
        _overlayService.MicPressEnded -= OnMicPressEnded;
        _voiceCommandService.PauseSpeechRequested -= OnPauseSpeechRequested;
        _voiceCommandService.ResumeSpeechRequested -= OnResumeSpeechRequested;

        base.OnWillUnmount();
    }

    async Task LoadDataAsync()
    {
        try
        {
            var bags = await _bagService.GetActiveBagsForShotLoggingAsync();
            var users = await _userProfileService.GetAllProfilesAsync();
            var equipment = (await _equipmentService.GetAllActiveEquipmentAsync()).ToList();

            if (Props.ShotId.HasValue)
            {
                var shot = await _shotService.GetShotByIdAsync(Props.ShotId.Value);
                if (shot == null)
                {
                    await _feedbackService.ShowErrorAsync("Drink not found");
                    await MauiControls.Shell.Current.GoToAsync("..");
                    return;
                }

                SetState(s =>
                {
                    s.AvailableBags = bags;
                    s.AvailableUsers = users;
                    s.AvailableEquipment = equipment;

                    s.BeanName = shot.Bean?.Name;
                    s.BrewMethod = shot.BrewMethod;
                    s.DrinkType = shot.DrinkType;
                    s.DoseIn = shot.DoseIn;
                    s.GrindMicrons = shot.GrindMicrons;
                    s.ExpectedTime = shot.ExpectedTime;
                    s.ExpectedOutput = shot.ExpectedOutput;
                    s.ActualTime = shot.ActualTime;
                    s.ActualOutput = shot.ActualOutput;
                    s.Rating = shot.Rating.HasValue ? Math.Max(0, shot.Rating.Value - 1) : 2;
                    s.SelectedBagId = shot.Bag?.Id;
                    s.SelectedMaker = shot.MadeBy;
                    s.SelectedRecipient = shot.MadeFor;
                    s.SelectedMachineId = shot.Machine?.Id;
                    s.SelectedGrinderId = shot.Grinder?.Id;
                    s.SelectedAccessoryIds = shot.Accessories?.Select(a => a.Id).ToList() ?? new();
                    s.TastingNotes = shot.TastingNotes;
                    // Backfill: ensure drink type is valid for the loaded brew method
                    // (older records may carry mismatched combinations).
                    var validForLoaded = shot.BrewMethod.DrinkTypesFor();
                    if (!validForLoaded.Contains(s.DrinkType))
                        s.DrinkType = validForLoaded[0];
                    s.IsLoading = false;
                });
            }
            else
            {
                var lastShot = await _shotService.GetMostRecentShotAsync();
                SetState(s =>
                {
                    s.AvailableBags = bags;
                    s.AvailableUsers = users;
                    s.AvailableEquipment = equipment;

                    if (lastShot != null)
                    {
                        s.BrewMethod = lastShot.BrewMethod;
                        s.DrinkType = lastShot.DrinkType;
                        s.DoseIn = lastShot.DoseIn;
                        s.GrindMicrons = lastShot.GrindMicrons;
                        s.ExpectedTime = lastShot.ExpectedTime;
                        s.ExpectedOutput = lastShot.ExpectedOutput;
                        s.Rating = (lastShot.Rating ?? 3) - 1;
                        s.SelectedBagId = lastShot.Bag?.Id;
                        var validForLast = lastShot.BrewMethod.DrinkTypesFor();
                        if (!validForLast.Contains(s.DrinkType))
                            s.DrinkType = validForLast[0];
                    }

                    var lastMakerId = _preferencesService.GetLastMadeById();
                    var lastRecipientId = _preferencesService.GetLastMadeForId();
                    if (lastMakerId.HasValue)
                        s.SelectedMaker = users.FirstOrDefault(u => u.Id == lastMakerId.Value);
                    if (lastRecipientId.HasValue)
                        s.SelectedRecipient = users.FirstOrDefault(u => u.Id == lastRecipientId.Value);

                    s.SelectedMachineId = _preferencesService.GetLastMachineId();
                    s.SelectedGrinderId = _preferencesService.GetLastGrinderId();
                    s.SelectedAccessoryIds = _preferencesService.GetLastAccessoryIds();
                    s.IsLoading = false;
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load drink-logging grid data");
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = ex.Message;
            });
        }
    }

    async Task SaveShotAsync()
    {
        try
        {
            if (State.SelectedBagId == null)
            {
                await _feedbackService.ShowErrorAsync("Please select a bag");
                return;
            }

            if (Props.ShotId.HasValue)
            {
                var dto = new UpdateShotDto
                {
                    BagId = State.SelectedBagId.Value,
                    MachineId = State.SelectedMachineId,
                    GrinderId = State.SelectedGrinderId,
                    AccessoryIds = State.SelectedAccessoryIds,
                    MadeById = State.SelectedMaker?.Id,
                    MadeForId = State.SelectedRecipient?.Id,
                    DoseIn = State.DoseIn,
                    GrindMicrons = State.GrindMicrons,
                    ExpectedTime = State.ExpectedTime,
                    ExpectedOutput = State.ExpectedOutput,
                    ActualTime = State.ActualTime,
                    ActualOutput = State.ActualOutput,
                    Rating = State.Rating + 1,
                    DrinkType = State.DrinkType,
                    BrewMethod = State.BrewMethod,
                    TastingNotes = State.TastingNotes
                };
                await _shotService.UpdateShotAsync(Props.ShotId.Value, dto);
                await _feedbackService.ShowSuccessAsync("Drink updated");
            }
            else
            {
                var dto = new CreateShotDto
                {
                    BagId = State.SelectedBagId.Value,
                    MachineId = State.SelectedMachineId,
                    GrinderId = State.SelectedGrinderId,
                    AccessoryIds = State.SelectedAccessoryIds,
                    MadeById = State.SelectedMaker?.Id,
                    MadeForId = State.SelectedRecipient?.Id,
                    DoseIn = State.DoseIn,
                    GrindMicrons = State.GrindMicrons,
                    ExpectedTime = State.ExpectedTime,
                    ExpectedOutput = State.ExpectedOutput,
                    ActualTime = State.ActualTime,
                    ActualOutput = State.ActualOutput,
                    DrinkType = State.DrinkType,
                    BrewMethod = State.BrewMethod,
                    Rating = State.Rating + 1,
                    TastingNotes = State.TastingNotes
                };
                await _shotService.CreateShotAsync(dto);

                _preferencesService.SetLastDrinkType(State.DrinkType);
                _preferencesService.SetLastBagId(State.SelectedBagId);
                _preferencesService.SetLastMachineId(State.SelectedMachineId);
                _preferencesService.SetLastGrinderId(State.SelectedGrinderId);
                _preferencesService.SetLastAccessoryIds(State.SelectedAccessoryIds);
                if (State.SelectedMaker != null) _preferencesService.SetLastMadeById(State.SelectedMaker.Id);
                if (State.SelectedRecipient != null) _preferencesService.SetLastMadeForId(State.SelectedRecipient.Id);

                await _feedbackService.ShowSuccessAsync($"{State.DrinkType} logged");
                await LoadDataAsync();
            }
        }
        catch (ValidationException vex)
        {
            var msgs = vex.Errors.SelectMany(e => e.Value).ToList();
            await _feedbackService.ShowErrorAsync("Validation error", msgs.FirstOrDefault() ?? "Invalid input");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save drink");
            await _feedbackService.ShowErrorAsync("Failed to save", ex.Message);
        }
    }

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        var title = Props.ShotId.HasValue ? "Edit Drink" : "New Drink";
        var pickerActive = State.ActivePicker != GridPickerKind.None;

        // If a picker is active, the picker view takes over the page.
        if (pickerActive)
        {
            return ContentPage(title, RenderActivePicker())
                .Set(MauiControls.Shell.NavBarIsVisibleProperty, false)
                .Set(MauiControls.Shell.TabBarIsVisibleProperty, false)
                .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never));
        }

        if (State.IsLoading)
        {
            return ContentPage(title,
                VStack(
                    ActivityIndicator().IsRunning(true),
                    Label("Loading…").Margin(0, 8).HCenter()
                ).VCenter().HCenter()
            )
            .Set(MauiControls.Shell.NavBarIsVisibleProperty, false)
            .Set(MauiControls.Shell.TabBarIsVisibleProperty, false)
            .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never));
        }

        return ContentPage(title,
                // ScrollView(
                Grid(
                    rows: "Auto,*,*,*,*,*,*,Auto",
                    columns: "*,*,*,*",
                    Tile("BREW METHOD", State.BrewMethod.DisplayName(),
                            () => Open(GridPickerKind.BrewMethod)).GridRow(0).GridColumn(0).GridColumnSpan(2),
                    Tile("BAG", BagDisplayValue(),
                            () => Open(GridPickerKind.Bag)).GridRow(0).GridColumn(2).GridColumnSpan(2),

                    Tile("DRINK TYPE", State.DrinkType,
                            () => Open(GridPickerKind.DrinkType)).GridRow(1).GridColumn(0).GridColumnSpan(2),
                    Tile("RATING", RatingDisplayValue(),
                            () => Open(GridPickerKind.Rating)).GridRow(1).GridColumn(2).GridColumnSpan(2),

                    Tile("DOSE IN", $"{State.DoseIn:0.#}", () => Open(GridPickerKind.DoseIn), unit: "g")
                            .GridRow(2).GridColumn(0).GridColumnSpan(2),
                    Tile("YIELD", $"{State.ExpectedOutput:0.#}", () => Open(GridPickerKind.YieldOut), unit: "g")
                            .GridRow(2).GridColumn(2).GridColumnSpan(2),

                    Tile("TIME", FormatTimeDisplay(State.ActualTime ?? State.ExpectedTime), () => Open(GridPickerKind.ActualTime), unit: TimeDisplayUnit(State.ActualTime ?? State.ExpectedTime))
                            .GridRow(3).GridColumn(0).GridColumnSpan(2),
                    Tile("GRIND", State.GrindMicrons.HasValue ? $"{State.GrindMicrons}" : "—",
                            () => Open(GridPickerKind.GrindMicrons), unit: State.GrindMicrons.HasValue ? "µm" : null).GridRow(3).GridColumn(2).GridColumnSpan(2),

                    Tile("MADE BY", State.SelectedMaker?.Name ?? "—",
                            () => Open(GridPickerKind.MadeBy)).GridRow(4).GridColumn(0).GridColumnSpan(2),
                    Tile("MADE FOR", State.SelectedRecipient?.Name ?? "—",
                            () => Open(GridPickerKind.MadeFor)).GridRow(4).GridColumn(2).GridColumnSpan(2),

                    Tile("MACHINE", EquipmentName(State.SelectedMachineId),
                            () => Open(GridPickerKind.Machine)).GridRow(5).GridColumn(0).GridColumnSpan(2),
                    Tile("GRINDER", EquipmentName(State.SelectedGrinderId),
                            () => Open(GridPickerKind.Grinder)).GridRow(5).GridColumn(2).GridColumnSpan(2),

                    Tile("ACCESSORIES", AccessoriesDisplayValue(),
                            () => Open(GridPickerKind.Accessories)).GridRow(6).GridColumn(0).GridColumnSpan(2),
                    SaveTile().GridRow(6).GridColumn(2).GridColumnSpan(2),

                    NavTile(AppIcons.Feed, async () => await MauiControls.Shell.Current.GoToAsync("//history"))
                        .GridRow(7).GridColumn(0),
                    NavTile(AppIcons.Settings, async () => await MauiControls.Shell.Current.GoToAsync("//settings"))
                        .GridRow(7).GridColumn(1),
                    NavTile(AppIcons.Voice, async () => await ToggleVoiceSheetAsync())
                        .GridRow(7).GridColumn(2),
                    NavTile(AppIcons.Camera, async () => await CaptureAndAnalyzePhotoAsync())
                        .GridRow(7).GridColumn(3)

                )
                .ColumnSpacing(1)
                .RowSpacing(1)
                .BackgroundColor(DividerColor())
                .Padding(1)
                .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        // )// ScrollView
        // .BackgroundColor(DividerColor())
        // // Extend under the status bar / notch — tiles compensate with topInsetPadding.
        // .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        ) // ContentPage
        .Set(MauiControls.Shell.NavBarIsVisibleProperty, false)
        .Set(MauiControls.Shell.TabBarIsVisibleProperty, false)
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
        .OnAppearing(OnPageAppearing);
    }

    void OnPageAppearing()
    {
        // Cross-page voice trigger: ActivityFeedPage's voice tile sets this
        // flag and navigates here. Fire after the page is visible so the
        // overlay attaches to the right window.
        if (OpenVoiceOnNextMount)
        {
            OpenVoiceOnNextMount = false;
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await ToggleVoiceSheetAsync(); }
                catch (Exception ex) { _logger?.LogError(ex, "Voice toggle from OnAppearing failed"); }
            });
        }
    }

    void Open(GridPickerKind kind)
    {
        // Grind picker has a heavier open path: resolve a sensible default
        // for the picker selection (bean history → method default) and load
        // the active grinder's anchors for the live translation badge.
        if (kind == GridPickerKind.GrindMicrons)
        {
            _ = OpenGrindPickerAsync();
            return;
        }
        SetState(s => s.ActivePicker = kind);
    }
    void ClosePicker() => SetState(s =>
    {
        s.ActivePicker = GridPickerKind.None;
        s.PickerGrindMicrons = null;
        s.PickerGrindAnchors = null;
        s.PickerGrinderName = null;
        s.PickerGrinderUncalibrated = false;
    });

    async Task OpenGrindPickerAsync()
    {
        try
        {
            // 1) Default value resolution: existing state, then bean-method
            // last-shot µm, then brew-method default.
            int? micron = State.GrindMicrons;
            if (micron == null && State.SelectedBagId.HasValue)
            {
                var bag = await _bagRepo.GetByIdAsync(State.SelectedBagId.Value);
                if (bag != null)
                {
                    micron = await _shotRecords.GetMostRecentMicronsByBeanAsync(bag.BeanId, State.BrewMethod);
                }
            }
            var range = State.BrewMethod.GrindMicronRange();
            if (micron == null) micron = range.Default;

            // 2) Anchors for the live badge. No grinder selected → no badge.
            IReadOnlyList<BaristaNotes.Core.Services.Grind.GrindAnchor>? anchors = null;
            string? grinderName = null;
            bool uncalibrated = false;
            if (State.SelectedGrinderId.HasValue)
            {
                var equip = State.AvailableEquipment.FirstOrDefault(e => e.Id == State.SelectedGrinderId.Value);
                grinderName = equip?.Name;
                // Auto-heal DF64 profiles created against the prior 0–9 dial
                // assumption (they shipped with stale AnchorsJson). No-op for
                // already-current data.
                await _grinderProfiles.EnsureCurrentSeedsAsync(State.SelectedGrinderId.Value);
                var profile = await _grinderProfiles.GetByEquipmentIdAsync(State.SelectedGrinderId.Value);
                var parsed = BaristaNotes.Core.Services.Grind.DeterministicGrindInterpolator.ParseAnchors(profile?.AnchorsJson);
                if (parsed.Count >= 2)
                {
                    anchors = parsed;
                }
                else
                {
                    anchors = BaristaNotes.Core.Services.Grind.KnownGrinderSeeds.TryGet(grinderName);
                    uncalibrated = anchors == null;
                }
            }

            SetState(s =>
            {
                s.PickerGrindMicrons = micron;
                s.PickerGrindAnchors = anchors;
                s.PickerGrinderName = grinderName;
                s.PickerGrinderUncalibrated = uncalibrated;
                s.ActivePicker = GridPickerKind.GrindMicrons;
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open grind picker");
            SetState(s => s.ActivePicker = GridPickerKind.GrindMicrons);
        }
    }

    // ------------------------------------------------------------
    // Tile factory
    // ------------------------------------------------------------

    VisualNode Tile(string label, string value, Action onTap, string? unit = null, bool inverted = false, double topInsetPadding = 0, double bottomInsetPadding = 0)
    {
        var isLight = Application.Current?.RequestedTheme != AppTheme.Dark;
        var bg = inverted
            ? (isLight ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
            : (isLight ? AppColors.Light.Surface : AppColors.Dark.Surface);
        var labelColor = inverted
            ? (isLight ? AppColors.Light.Surface : AppColors.Dark.Surface).WithAlpha(0.7f)
            : (isLight ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary);
        var valueColor = inverted
            ? (isLight ? AppColors.Light.Surface : AppColors.Dark.Surface)
            : (isLight ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary);

        // Adaptive value font size + wrapping: long strings shrink and wrap to a
        // second line. Tuned for a half-screen-wide tile in portrait.
        var hasUnit = unit != null;
        var len = value?.Length ?? 0;
        double valueFontSize = (len, hasUnit) switch
        {
            ( <= 3, _) => 44,
            ( <= 6, true) => 36,
            ( <= 6, false) => 38,
            ( <= 10, _) => 28,
            ( <= 14, _) => 22,
            ( <= 20, _) => 18,
            _ => 16
        };

        return Border(
            Grid(rows: "Auto,*", columns: "*",
                Label(label.ToUpperInvariant())
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .TextColor(labelColor)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .GridRow(0),
                HStack(spacing: 4,
                    Label(value)
                        .FontSize(valueFontSize)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(valueColor)
                        .LineBreakMode(LineBreakMode.WordWrap)
                        .MaxLines(2)
                        .VEnd()
                        .HorizontalOptions(LayoutOptions.Fill),
                    hasUnit
                        ? Label(unit)
                            .FontSize(Math.Max(12, valueFontSize * 0.45))
                            .TextColor(valueColor.WithAlpha(0.6f))
                            .Margin(0, 0, 0, valueFontSize >= 32 ? 8 : 4)
                            .VEnd()
                        : null
                )
                .GridRow(1)
                .VEnd()
            )
            .Padding(16, 14 + topInsetPadding, 16, 14 + bottomInsetPadding)
        )
        .BackgroundColor(bg)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(120)
        .OnTapped(onTap);
    }

    VisualNode SaveTile()
    {
        return Tile(
            label: Props.ShotId.HasValue ? "UPDATE" : "SAVE",
            value: Props.ShotId.HasValue ? "Update" : "Log Drink",
            onTap: async () => await SaveShotAsync(),
            unit: null,
            inverted: true);
    }

    VisualNode NavTile(FontImageSource imageSource, Action onTap)
    {
        var isLight = Application.Current?.RequestedTheme != AppTheme.Dark;
        var bg = isLight ? AppColors.Light.Surface : AppColors.Dark.Surface;
        var labelColor = isLight ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;

        return Border(
            Image()
                .Source(imageSource)
                .HCenter()
                .VCenter()
        )
        .BackgroundColor(bg)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(72)
        .Padding(16, 18, 16, 30)
        .OnTapped(onTap);
    }

    Color DividerColor()
    {
        var isLight = Application.Current?.RequestedTheme != AppTheme.Dark;
        return isLight ? AppColors.Light.Outline : AppColors.Dark.Outline;
    }

    // ------------------------------------------------------------
    // Tile display helpers
    // ------------------------------------------------------------

    string BagDisplayValue()
    {
        if (State.SelectedBagId == null) return "—";
        var bag = State.AvailableBags.FirstOrDefault(b => b.Id == State.SelectedBagId.Value);
        return bag?.BeanName ?? State.BeanName ?? "—";
    }

    string RatingDisplayValue()
    {
        var n = State.Rating + 1;
        return $"{n}/5";
    }

    string EquipmentName(int? id)
    {
        if (id == null) return "—";
        var e = State.AvailableEquipment.FirstOrDefault(x => x.Id == id.Value);
        return e?.Name ?? "—";
    }

    string AccessoriesDisplayValue()
    {
        if (State.SelectedAccessoryIds.Count == 0) return "—";
        if (State.SelectedAccessoryIds.Count == 1)
        {
            var id = State.SelectedAccessoryIds[0];
            return State.AvailableEquipment.FirstOrDefault(e => e.Id == id)?.Name ?? "1 item";
        }
        return $"{State.SelectedAccessoryIds.Count} items";
    }

    // ------------------------------------------------------------
    // Picker dispatch
    // ------------------------------------------------------------

    VisualNode RenderActivePicker()
    {
        return State.ActivePicker switch
        {
            GridPickerKind.BrewMethod => CategoricalPicker(
                title: "Brew Method",
                items: BrewMethodExtensions.All.Select(m => (Key: (object)m, Display: m.DisplayName())).ToList(),
                isSelected: o => (BrewMethod)o == State.BrewMethod,
                onSelect: o => SetState(s =>
                {
                    var newMethod = (BrewMethod)o;
                    if (newMethod != s.BrewMethod)
                    {
                        var newProfile = newMethod.Profile();
                        // Re-anchor expected dose/output/time to the new method's defaults
                        // so ranges like espresso (28s) don't bleed into pour-over (3-5 min).
                        s.DoseIn = newProfile.DoseDefault;
                        s.ExpectedOutput = newProfile.OutputDefault;
                        s.ExpectedTime = newProfile.TimeDefault;
                        // Clear actuals captured under the previous method.
                        s.ActualTime = null;
                        s.ActualOutput = null;
                        // Snap drink type to a valid option for the new method.
                        var validDrinks = newMethod.DrinkTypesFor();
                        if (!validDrinks.Contains(s.DrinkType))
                            s.DrinkType = validDrinks[0];
                    }
                    s.BrewMethod = newMethod;
                    s.ActivePicker = GridPickerKind.None;
                })),

            GridPickerKind.DrinkType => CategoricalPicker(
                title: "Drink Type",
                items: State.BrewMethod.DrinkTypesFor().Select(d => (Key: (object)d, Display: d)).ToList(),
                isSelected: o => (string)o == State.DrinkType,
                onSelect: o => SetState(s => { s.DrinkType = (string)o; s.ActivePicker = GridPickerKind.None; })),

            GridPickerKind.Bag => CategoricalPicker(
                title: "Bag",
                items: State.AvailableBags.Select(b => (Key: (object)b.Id, Display: BagDisplayLabel(b))).ToList(),
                isSelected: o => State.SelectedBagId == (int)o,
                onSelect: o => SetState(s => { s.SelectedBagId = (int)o; s.ActivePicker = GridPickerKind.None; }),
                emptyMessage: "No active bags. Add a bag from Settings."),

            GridPickerKind.MadeBy => CategoricalPicker(
                title: "Made By",
                items: State.AvailableUsers.Select(u => (Key: (object)u.Id, Display: u.Name)).ToList(),
                isSelected: o => State.SelectedMaker?.Id == (int)o,
                onSelect: o =>
                {
                    var u = State.AvailableUsers.FirstOrDefault(x => x.Id == (int)o);
                    SetState(s => { s.SelectedMaker = u; s.ActivePicker = GridPickerKind.None; });
                },
                emptyMessage: "No user profiles. Add one from Settings."),

            GridPickerKind.MadeFor => CategoricalPicker(
                title: "Made For",
                items: State.AvailableUsers.Select(u => (Key: (object)u.Id, Display: u.Name)).ToList(),
                isSelected: o => State.SelectedRecipient?.Id == (int)o,
                onSelect: o =>
                {
                    var u = State.AvailableUsers.FirstOrDefault(x => x.Id == (int)o);
                    SetState(s => { s.SelectedRecipient = u; s.ActivePicker = GridPickerKind.None; });
                },
                emptyMessage: "No user profiles. Add one from Settings."),

            GridPickerKind.Machine => CategoricalPicker(
                title: "Machine",
                items: State.AvailableEquipment
                    .Where(e => e.Type == EquipmentType.Machine)
                    .Select(e => (Key: (object)e.Id, Display: e.Name)).ToList(),
                isSelected: o => State.SelectedMachineId == (int)o,
                onSelect: o => SetState(s => { s.SelectedMachineId = (int)o; s.ActivePicker = GridPickerKind.None; }),
                allowClear: true,
                onClear: () => SetState(s => { s.SelectedMachineId = null; s.ActivePicker = GridPickerKind.None; }),
                emptyMessage: "No machines. Add equipment from Settings."),

            GridPickerKind.Grinder => CategoricalPicker(
                title: "Grinder",
                items: State.AvailableEquipment
                    .Where(e => e.Type == EquipmentType.Grinder)
                    .Select(e => (Key: (object)e.Id, Display: e.Name)).ToList(),
                isSelected: o => State.SelectedGrinderId == (int)o,
                onSelect: o => SetState(s => { s.SelectedGrinderId = (int)o; s.ActivePicker = GridPickerKind.None; }),
                allowClear: true,
                onClear: () => SetState(s => { s.SelectedGrinderId = null; s.ActivePicker = GridPickerKind.None; }),
                emptyMessage: "No grinders. Add equipment from Settings."),

            GridPickerKind.Accessories => MultiCategoricalPicker(
                title: "Accessories",
                items: State.AvailableEquipment
                    .Where(e => e.Type != EquipmentType.Machine && e.Type != EquipmentType.Grinder)
                    .Select(e => (Id: e.Id, Display: e.Name)).ToList(),
                isSelected: id => State.SelectedAccessoryIds.Contains(id),
                onToggle: id => SetState(s =>
                {
                    if (s.SelectedAccessoryIds.Contains(id)) s.SelectedAccessoryIds.Remove(id);
                    else s.SelectedAccessoryIds = s.SelectedAccessoryIds.Concat(new[] { id }).ToList();
                }),
                onDone: () => SetState(s => s.ActivePicker = GridPickerKind.None),
                onClear: () => SetState(s => { s.SelectedAccessoryIds = new List<int>(); s.ActivePicker = GridPickerKind.None; })),

            GridPickerKind.Rating => CategoricalPicker(
                title: "Rating",
                items: Enumerable.Range(1, 5).Select(i => (Key: (object)i, Display: new string('★', i) + new string('☆', 5 - i))).ToList(),
                isSelected: o => State.Rating == ((int)o - 1),
                onSelect: o => SetState(s => { s.Rating = (int)o - 1; s.ActivePicker = GridPickerKind.None; })),

            GridPickerKind.DoseIn => NumericScroller(
                title: "Dose In",
                unit: "g",
                spec: NumericSpecFor(GridPickerKind.DoseIn),
                current: (double)State.DoseIn,
                onSelect: v => SetState(s => { s.DoseIn = (decimal)v; s.ActivePicker = GridPickerKind.None; })),

            GridPickerKind.YieldOut => NumericScroller(
                title: "Yield",
                unit: "g",
                spec: NumericSpecFor(GridPickerKind.YieldOut),
                current: (double)State.ExpectedOutput,
                onSelect: v => SetState(s => { s.ExpectedOutput = (decimal)v; s.ActivePicker = GridPickerKind.None; })),

            GridPickerKind.ActualTime => NumericScroller(
                title: "Time",
                unit: "s",
                spec: NumericSpecFor(GridPickerKind.ActualTime),
                current: (double)(State.ActualTime ?? State.ExpectedTime),
                onSelect: v => SetState(s =>
                {
                    // The TIME tile is the only UI for brew duration, so we
                    // mirror to ExpectedTime as well. Otherwise ExpectedTime
                    // carries over from the previous shot's brew method and
                    // can land outside the new method's validation range
                    // (e.g. 28s espresso → V60 which requires ≥ 60s).
                    var time = (decimal)v;
                    s.ActualTime = time;
                    s.ExpectedTime = time;
                    s.ActivePicker = GridPickerKind.None;
                })),

            GridPickerKind.GrindMicrons => GrindMicronsPicker(),

            _ => VStack()
        };
    }

    static string BagDisplayLabel(BagSummaryDto b)
        => $"{b.BeanName} · Roasted {b.FormattedRoastDate}";

    // ------------------------------------------------------------
    // Categorical picker (single-select)
    // ------------------------------------------------------------

    VisualNode CategoricalPicker(
        string title,
        List<(object Key, string Display)> items,
        Func<object, bool> isSelected,
        Action<object> onSelect,
        bool allowClear = false,
        Action? onClear = null,
        string? emptyMessage = null)
    {
        var isLight = Application.Current?.RequestedTheme != AppTheme.Dark;
        var textPrimary = isLight ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var textSecondary = isLight ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
        var accent = isLight ? AppColors.Light.Primary : AppColors.Dark.Primary;

        return Grid(rows: "Auto,*,Auto", columns: "*",
            // Header
            Grid(rows: "*", columns: "Auto,*,Auto",
                Button("Close").OnClicked(ClosePicker)
                    .BackgroundColor(Colors.Transparent)
                    .TextColor(textSecondary)
                    .GridColumn(0),
                Label(title.ToUpperInvariant())
                    .FontSize(12).CharacterSpacing(3)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(textSecondary)
                    .HCenter().VCenter()
                    .GridColumn(0).GridColumnSpan(3),
                allowClear
                    ? Button("Clear").OnClicked(() => onClear?.Invoke())
                        .BackgroundColor(Colors.Transparent).TextColor(textSecondary).GridColumn(2)
                    : null
            ).GridRow(0).Padding(12, 8),

            // Body
            (items.Count == 0
                ? VStack(
                    Label(emptyMessage ?? "Nothing to choose from")
                        .TextColor(textSecondary).HCenter()
                  ).VCenter().HCenter().GridRow(1)
                : ScrollView(
                    VStack(spacing: 0,
                        items.Select(item => RenderPickerRow(item.Display, isSelected(item.Key), () => onSelect(item.Key), textPrimary, accent)).ToArray()
                    )
                ).GridRow(1))
        ).BackgroundColor(isLight ? AppColors.Light.Surface : AppColors.Dark.Surface);
    }

    VisualNode RenderPickerRow(string text, bool selected, Action onTap, Color textColor, Color accentColor)
    {
        return Grid(rows: "*", columns: "*,Auto",
            Label(text)
                .FontSize(selected ? 28 : 22)
                .FontAttributes(selected ? MauiControls.FontAttributes.Bold : MauiControls.FontAttributes.None)
                .TextColor(selected ? accentColor : textColor)
                .VCenter()
                .GridColumn(0),
            selected
                ? Label("●").FontSize(14).TextColor(accentColor).VCenter().GridColumn(2).GridColumnSpan(2)
                : null
        )
        .Padding(24, 18)
        .OnTapped(onTap);
    }

    // ------------------------------------------------------------
    // Multi-select picker
    // ------------------------------------------------------------

    VisualNode MultiCategoricalPicker(
        string title,
        List<(int Id, string Display)> items,
        Func<int, bool> isSelected,
        Action<int> onToggle,
        Action onDone,
        Action? onClear = null)
    {
        var isLight = Application.Current?.RequestedTheme != AppTheme.Dark;
        var textPrimary = isLight ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var textSecondary = isLight ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
        var accent = isLight ? AppColors.Light.Primary : AppColors.Dark.Primary;

        return Grid(rows: "Auto,*,Auto", columns: "*",
            Grid(rows: "*", columns: "Auto,*,Auto,Auto",
                Button("Close").OnClicked(ClosePicker)
                    .BackgroundColor(Colors.Transparent).TextColor(textSecondary).GridColumn(0),
                Label(title.ToUpperInvariant())
                    .FontSize(12).CharacterSpacing(3)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(textSecondary).HCenter().VCenter().GridColumn(0).GridColumnSpan(4),
                onClear != null
                    ? Button("Clear").OnClicked(() => onClear?.Invoke())
                        .BackgroundColor(Colors.Transparent).TextColor(textSecondary).GridColumn(2)
                    : null,
                Button("Done").OnClicked(onDone)
                    .BackgroundColor(Colors.Transparent).TextColor(accent)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .GridColumn(3)
            ).GridRow(0).Padding(12, 8),

            (items.Count == 0
                ? VStack(Label("No accessories available").TextColor(textSecondary).HCenter())
                    .VCenter().HCenter().GridRow(1)
                : ScrollView(
                    VStack(spacing: 0,
                        items.Select(item => RenderMultiRow(item.Display, isSelected(item.Id), () => onToggle(item.Id), textPrimary, accent)).ToArray()
                    )
                ).GridRow(1))
        ).BackgroundColor(isLight ? AppColors.Light.Surface : AppColors.Dark.Surface);
    }

    VisualNode RenderMultiRow(string text, bool selected, Action onTap, Color textColor, Color accentColor)
    {
        return Grid(rows: "*", columns: "Auto,*",
            Label(selected ? "■" : "□")
                .FontSize(24)
                .TextColor(selected ? accentColor : textColor)
                .VCenter().GridColumn(0)
                .Margin(0, 0, 16, 0),
            Label(text)
                .FontSize(22)
                .TextColor(textColor)
                .VCenter().GridColumn(2).GridColumnSpan(2)
        )
        .Padding(24, 16)
        .OnTapped(onTap);
    }

    // ------------------------------------------------------------
    // Numeric scroller picker
    // ------------------------------------------------------------

    // Brew durations span 10s (espresso) to 24h (cold brew). Format the
    // tile readout so each method reads naturally: seconds for espresso,
    // m:ss for pour-over / french press, Hh Mm for cold brew / cold drip.
    static string FormatTimeDisplay(decimal seconds)
    {
        var s = (int)Math.Round((double)seconds);
        if (s < 60) return s.ToString("0");
        if (s < 3600)
        {
            var m = s / 60;
            var rem = s % 60;
            return rem == 0 ? $"{m}:00" : $"{m}:{rem:00}";
        }
        var h = s / 3600;
        var mins = (s % 3600) / 60;
        return mins == 0 ? $"{h}h" : $"{h}h {mins}m";
    }

    static string? TimeDisplayUnit(decimal seconds)
    {
        var s = (int)Math.Round((double)seconds);
        return s < 60 ? "s" : null;
    }

    record NumericFieldSpec(double Min, double Max, double Step, string Format);

    NumericFieldSpec NumericSpecFor(GridPickerKind kind)
    {
        var profile = State.BrewMethod.Profile();
        return kind switch
        {
            GridPickerKind.DoseIn => new((double)profile.DoseMin, (double)profile.DoseMax, (double)profile.DoseStep, profile.DoseStep < 1 ? "0.#" : "0"),
            GridPickerKind.YieldOut => new((double)profile.OutputMin, (double)profile.OutputMax, (double)profile.OutputStep, profile.OutputStep < 1 ? "0.#" : "0"),
            GridPickerKind.ActualTime => new((double)profile.TimeMin, (double)profile.TimeMax, (double)profile.TimeStep, "0"),
            _ => new(0, 100, 1, "0")
        };
    }

    VisualNode NumericScroller(string title, string? unit, NumericFieldSpec spec, double current, Action<double> onSelect)
    {
        var isLight = Application.Current?.RequestedTheme != AppTheme.Dark;
        var textPrimary = isLight ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var textSecondary = isLight ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
        var accent = isLight ? AppColors.Light.Primary : AppColors.Dark.Primary;

        // Build list of values
        var values = new List<double>();
        for (var v = spec.Min; v <= spec.Max + 1e-9; v += spec.Step)
            values.Add(Math.Round(v / spec.Step) * spec.Step);

        // Snap current to nearest step.
        var clamped = Math.Clamp(current, spec.Min, spec.Max);
        var snappedIndex = (int)Math.Round((clamped - spec.Min) / spec.Step);
        snappedIndex = Math.Clamp(snappedIndex, 0, values.Count - 1);

        return Grid(rows: "Auto,*,Auto", columns: "*",
            Grid(rows: "*", columns: "Auto,*,Auto",
                Button("Close").OnClicked(ClosePicker)
                    .BackgroundColor(Colors.Transparent).TextColor(textSecondary).GridColumn(0),
                Label(title.ToUpperInvariant())
                    .FontSize(12).CharacterSpacing(3)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(textSecondary).HCenter().VCenter().GridColumn(0).GridColumnSpan(3)
            ).GridRow(0).Padding(12, 8),

            ScrollView(
                VStack(spacing: 0,
                    values.Select((v, i) =>
                    {
                        var selected = i == snappedIndex;
                        var text = unit != null ? $"{v.ToString(spec.Format)}{unit}" : v.ToString(spec.Format);
                        return Grid(rows: "*", columns: "*",
                            Label(text)
                                .FontSize(selected ? 56 : 32)
                                .FontAttributes(selected ? MauiControls.FontAttributes.Bold : MauiControls.FontAttributes.None)
                                .TextColor(selected ? accent : textPrimary)
                                .HCenter().VCenter()
                        )
                        .HeightRequest(selected ? 96 : 72)
                        .OnTapped(() => onSelect(v));
                    }).ToArray()
                )
            ).GridRow(1)
        ).BackgroundColor(isLight ? AppColors.Light.Surface : AppColors.Dark.Surface);
    }

    // ------------------------------------------------------------
    // Grind picker: micron scroller + live grinder-native badge
    // ------------------------------------------------------------

    VisualNode GrindMicronsPicker()
    {
        var isLight = Application.Current?.RequestedTheme != AppTheme.Dark;
        var textPrimary = isLight ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var textSecondary = isLight ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
        var accent = isLight ? AppColors.Light.Primary : AppColors.Dark.Primary;
        var surface = isLight ? AppColors.Light.Surface : AppColors.Dark.Surface;

        var range = State.BrewMethod.GrindMicronRange();

        // Full µm domain always rendered. Variable density: brew-method's
        // native Step inside [Min,Max], 50µm step outside. This lets users
        // dial into atypical grinds for a given method without hiding the
        // wider field of possibility.
        const int domainMin = 40;
        const int domainMax = 1500;
        const int outsideStep = 50;
        var values = new List<int>();
        for (var v = domainMin; v < range.Min; v += outsideStep) values.Add(v);
        for (var v = range.Min; v <= range.Max; v += range.Step) values.Add(v);
        for (var v = range.Max + outsideStep; v <= domainMax; v += outsideStep) values.Add(v);

        var current = State.PickerGrindMicrons ?? State.GrindMicrons ?? range.Default;
        // Snap to nearest value present in the variable-step list.
        var snappedIndex = 0;
        var bestDelta = int.MaxValue;
        for (var i = 0; i < values.Count; i++)
        {
            var d = Math.Abs(values[i] - current);
            if (d < bestDelta) { bestDelta = d; snappedIndex = i; }
        }
        var snappedValue = values[snappedIndex];

        return Grid(rows: "Auto,*,Auto", columns: "*",
            // Header
            Grid(rows: "*", columns: "Auto,*,Auto",
                Button("Close").OnClicked(ClosePicker)
                    .BackgroundColor(Colors.Transparent).TextColor(textSecondary).GridColumn(0),
                Label("GRIND")
                    .FontSize(12).CharacterSpacing(3)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(textSecondary).HCenter().VCenter().GridColumn(0).GridColumnSpan(3),
                Button("Done")
                    .OnClicked(() => CommitGrindMicrons(snappedValue))
                    .BackgroundColor(Colors.Transparent).TextColor(accent)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .GridColumn(2)
            ).GridRow(0).Padding(12, 8),

            // Scrollable µm list. CollectionView so we can ScrollTo(index)
            // on load to center the user's brew-method range without
            // hiding out-of-range values.
            CollectionView()
                .ItemsSource(values, v =>
                {
                    var inRange = v >= range.Min && v <= range.Max;
                    var selected = v == snappedValue;
                    var rowHeight = selected ? 96 : (inRange ? 72 : 48);
                    var fontSize = selected ? 56 : (inRange ? 32 : 20);
                    var color = selected ? accent
                        : inRange ? textPrimary
                        : textSecondary.WithAlpha(0.5f);

                    return Grid(rows: "*", columns: "*",
                        Label($"{v} µm")
                            .FontSize(fontSize)
                            .FontAttributes(selected ? MauiControls.FontAttributes.Bold : MauiControls.FontAttributes.None)
                            .TextColor(color)
                            .HCenter().VCenter()
                    )
                    .HeightRequest(rowHeight)
                    .OnTapped(() =>
                    {
                        if (selected) CommitGrindMicrons(v);
                        else SetState(s => s.PickerGrindMicrons = v);
                    });
                })
                .SelectionMode(MauiControls.SelectionMode.None)
                .OnLoaded((sender, _) =>
                {
                    if (sender is MauiControls.CollectionView cv)
                    {
                        try
                        {
                            cv.ScrollTo(snappedIndex, position: MauiControls.ScrollToPosition.Center, animate: false);
                        }
                        catch
                        {
                            // Best-effort centering; ignore if the items aren't laid out yet.
                        }
                    }
                })
                .GridRow(1),

            // Sticky bottom translation badge.
            GrindTranslationBadge(snappedValue, textPrimary, textSecondary, accent)
                .GridRow(2)
        ).BackgroundColor(surface);
    }

    VisualNode GrindTranslationBadge(int microns, Color textPrimary, Color textSecondary, Color accent)
    {
        var anchors = State.PickerGrindAnchors;
        string main;
        string? cta = null;
        Action? onTap = null;

        if (!State.SelectedGrinderId.HasValue)
        {
            main = "Select grinder for dial setting";
            cta = "→";
            onTap = () => SetState(s => s.ActivePicker = GridPickerKind.Grinder);
        }
        else if (anchors == null || anchors.Count < 2)
        {
            // Grinder selected but uncalibrated.
            main = $"{State.PickerGrinderName ?? "Grinder"} · Set up scale";
            cta = "→";
            // Navigate to grinder equipment detail so user can add anchors.
            onTap = async () =>
            {
                ClosePicker();
                if (State.SelectedGrinderId.HasValue)
                    await MauiControls.Shell.Current.GoToAsync($"equipmentDetail?id={State.SelectedGrinderId.Value}");
            };
        }
        else
        {
            var result = BaristaNotes.Core.Services.Grind.DeterministicGrindInterpolator
                .Interpolate(anchors, microns);
            if (result?.Suggested is decimal s)
            {
                // Round to a reasonable precision (0.1) for display.
                var rounded = Math.Round(s, 1);
                main = $"{State.PickerGrinderName ?? "Grinder"} · {rounded:0.#}";
            }
            else
            {
                main = $"{State.PickerGrinderName ?? "Grinder"} · —";
            }
        }

        var badge = Grid(rows: "*", columns: "*,Auto",
            Label(main)
                .FontSize(16)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(textPrimary)
                .VCenter().GridColumn(0),
            cta != null
                ? Label(cta).FontSize(20).TextColor(accent).VCenter().GridColumn(2).GridColumnSpan(2)
                : null
        )
        .Padding(20, 18)
        .BackgroundColor(textPrimary.WithAlpha(0.05f));

        if (onTap != null) badge = badge.OnTapped(onTap);
        return badge;
    }

    void CommitGrindMicrons(int microns)
    {
        SetState(s =>
        {
            s.GrindMicrons = microns;
            s.PendingGrindHint = null;
            s.ActivePicker = GridPickerKind.None;
            s.PickerGrindMicrons = null;
            s.PickerGrindAnchors = null;
            s.PickerGrinderName = null;
            s.PickerGrinderUncalibrated = false;
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Voice command + camera flow
    //  Ported from ShotLoggingPage so the grid's NavTile mic/camera buttons
    //  open the window overlay and exercise the AI voice pipeline.
    // ──────────────────────────────────────────────────────────────────────

    private async void OnOverlayCloseRequested(object? sender, EventArgs e)
    {
        if (State.IsVoiceSheetOpen)
        {
            await CloseVoiceOverlayAsync();
        }
    }

    private void OnOverlayExpandRequested(object? sender, EventArgs e)
    {
        if (State.IsVoiceSheetOpen && _overlayService.IsCollapsed)
        {
            _overlayService.Expand();
        }
    }

    private async void OnMicPressStarted(object? sender, EventArgs e)
    {
        _logger.LogInformation("Push-to-talk: mic button pressed");
        if (!State.IsVoiceSheetOpen || State.IsRecording) return;
        await StartRecordingAsync();
    }

    private async void OnMicPressEnded(object? sender, EventArgs e)
    {
        _logger.LogInformation("Push-to-talk: mic button released");
        if (!State.IsRecording) return;
        await StopRecordingAsync();
    }

    private async void OnPauseSpeechRequested(object? sender, EventArgs e)
    {
        _logger.LogDebug("Pause speech requested - stopping listening for camera");
        _speechPaused = true;
        if (State.IsRecording)
        {
            await _speechRecognitionService.StopListeningAsync();
            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Taking photo...",
                Transcript: State.VoiceTranscript,
                IsListening: false,
                IsProcessing: true,
                AIResponse: State.LastAIResponse
            ));
        }
    }

    private void OnResumeSpeechRequested(object? sender, EventArgs e)
    {
        _logger.LogDebug("Resume speech requested via event");
        _speechPaused = false;
    }

    /// <summary>
    /// Refresh picker reference lists when voice commands create new
    /// beans/bags/equipment/profiles in the background.
    /// </summary>
    private void OnDataChanged(object? sender, DataChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                switch (e.ChangeType)
                {
                    case DataChangeType.BeanCreated:
                    case DataChangeType.BeanUpdated:
                    case DataChangeType.BagCreated:
                    case DataChangeType.BagUpdated:
                    {
                        // Bag display labels embed bean names, so refresh bags
                        // whenever either bean or bag data changes.
                        var bags = await _bagService.GetActiveBagsForShotLoggingAsync();
                        SetState(s => s.AvailableBags = bags);
                        break;
                    }
                    case DataChangeType.EquipmentCreated:
                    case DataChangeType.EquipmentUpdated:
                    {
                        var equipment = (await _equipmentService.GetAllActiveEquipmentAsync()).ToList();
                        SetState(s => s.AvailableEquipment = equipment);
                        break;
                    }
                    case DataChangeType.ProfileCreated:
                    case DataChangeType.ProfileUpdated:
                    {
                        var users = await _userProfileService.GetAllProfilesAsync();
                        SetState(s => s.AvailableUsers = users);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing pickers after data change");
            }
        });
    }

    /// <summary>
    /// Toggles the voice overlay. When opening, shows the window overlay in
    /// "Ready" state — mic is NOT auto-armed; user push-and-hold starts the
    /// recording via the overlay's MicPressStarted event.
    /// </summary>
    private async Task ToggleVoiceSheetAsync()
    {
        if (State.IsVoiceSheetOpen)
        {
            await CloseVoiceOverlayAsync();
            return;
        }

        // Fresh conversation context for each session.
        _voiceCommandService.ClearConversationHistory();
        SetState(s => s.VoiceChatHistory = new List<VoiceChatMessage>());

        _overlayService.Show();
        _overlayService.UpdateContent(new OverlayContent(
            StateText: "Ready",
            Transcript: "",
            IsListening: false,
            IsProcessing: false,
            IsReady: true
        ));

        SetState(s => s.IsVoiceSheetOpen = true);
        await Task.CompletedTask;
    }

    private async Task CloseVoiceOverlayAsync()
    {
        if (State.IsRecording)
        {
            await StopRecordingAsync();
        }
        SetState(s => s.IsVoiceSheetOpen = false);
        _overlayService.Hide();
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            _logger.LogInformation("Starting voice recording (push-to-talk)");

            var hasPermission = await _speechRecognitionService.RequestPermissionsAsync();
            if (!hasPermission)
            {
                _logger.LogWarning("Speech recognition permission denied");
                AddChatMessage("Microphone permission is required. Please enable it in Settings.",
                    isUser: false, isError: true);
                await ShowPermissionDeniedDialogAsync();
                return;
            }

            _voiceCts = new CancellationTokenSource();

            SetState(s =>
            {
                s.IsRecording = true;
                s.VoiceTranscript = "";
                s.VoiceState = SpeechRecognitionState.Listening;
                s.VoiceCommandCommitted = false;
            });

            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Listening...",
                Transcript: "",
                IsListening: true,
                IsProcessing: false
            ));

            _speechRecognitionService.PartialResultReceived += OnPartialResultReceived;
            _ = ListenForSpeechAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting voice recording");
            AddChatMessage("Failed to start recording. Please try again.",
                isUser: false, isError: true);
        }
    }

    private async Task ListenForSpeechAsync()
    {
        _logger.LogInformation("ListenForSpeechAsync started (single-shot push-to-talk)");
        try
        {
            if (_voiceCts?.Token.IsCancellationRequested ?? true) return;

            var result = await _speechRecognitionService.StartListeningAsync(
                _voiceCts?.Token ?? CancellationToken.None);

            _logger.LogInformation("Speech recognition returned: Success={Success}, Transcript='{Transcript}'",
                result.Success, result.Transcript ?? "(null)");

            // If cancelled by button release, StopRecordingAsync handles processing.
            if (_voiceCts?.Token.IsCancellationRequested ?? true) return;

            if (result.Success && !string.IsNullOrWhiteSpace(result.Transcript))
            {
                var transcript = result.Transcript;
                SetState(s => s.VoiceTranscript = "");
                await ProcessTranscriptAsync(transcript);
            }
            else if (!string.IsNullOrWhiteSpace(State.VoiceTranscript))
            {
                var transcript = State.VoiceTranscript;
                SetState(s => s.VoiceTranscript = "");
                await ProcessTranscriptAsync(transcript);
            }
            else
            {
                SetState(s =>
                {
                    s.IsRecording = false;
                    s.VoiceState = SpeechRecognitionState.Idle;
                });
                _overlayService.UpdateContent(new OverlayContent(
                    StateText: "Ready",
                    Transcript: "",
                    IsListening: false,
                    IsProcessing: false,
                    IsReady: true,
                    AIResponse: State.LastAIResponse
                ));
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ListenForSpeechAsync cancelled (push-to-talk release)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during speech listening");
        }
        finally
        {
            StopSilenceTimer();
            _speechRecognitionService.PartialResultReceived -= OnPartialResultReceived;
        }
    }

    private async Task StopRecordingAsync()
    {
        _logger.LogInformation("StopRecordingAsync called (push-to-talk release)");
        StopSilenceTimer();

        var pendingTranscript = State.VoiceTranscript;

        _voiceCts?.Cancel();
        await _speechRecognitionService.StopListeningAsync();
        _speechRecognitionService.PartialResultReceived -= OnPartialResultReceived;

        SetState(s =>
        {
            s.IsRecording = false;
            s.VoiceTranscript = "";
            s.VoiceState = SpeechRecognitionState.Idle;
        });

        if (!string.IsNullOrWhiteSpace(pendingTranscript))
        {
            await ProcessTranscriptAsync(pendingTranscript);
        }
        else
        {
            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Ready",
                Transcript: "",
                IsListening: false,
                IsProcessing: false,
                IsReady: true,
                AIResponse: State.LastAIResponse
            ));
        }
    }

    private async Task ProcessTranscriptAsync(string transcript)
    {
        _logger.LogInformation("ProcessTranscriptAsync START: '{Transcript}'", transcript);
        AddChatMessage(transcript, isUser: true);
        SetState(s => s.VoiceState = SpeechRecognitionState.Processing);

        _overlayService.UpdateContent(new OverlayContent(
            StateText: "Processing...",
            Transcript: transcript,
            IsListening: false,
            IsProcessing: true
        ));

        try
        {
            var commandResult = await _voiceCommandService.ProcessCommandAsync(
                new VoiceCommandRequestDto(transcript, 1.0),
                CancellationToken.None);

            AddChatMessage(commandResult.Message, isUser: false, isError: !commandResult.Success);
            SetState(s =>
            {
                s.LastAIResponse = commandResult.Message;
                s.VoiceTranscript = "";
                s.VoiceState = SpeechRecognitionState.Idle;
            });

            if (_speechPaused) _speechPaused = false;

            _overlayService.UpdateContent(new OverlayContent(
                StateText: commandResult.Success ? "Done" : "Error",
                Transcript: "",
                IsListening: false,
                IsProcessing: false,
                IsReady: true,
                AIResponse: commandResult.Message
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing voice command");
            var errorMessage = "Sorry, something went wrong. Please try again.";
            AddChatMessage(errorMessage, isUser: false, isError: true);
            SetState(s =>
            {
                s.LastAIResponse = errorMessage;
                s.VoiceState = SpeechRecognitionState.Idle;
            });
            if (_speechPaused) _speechPaused = false;

            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Error",
                Transcript: "",
                IsListening: false,
                IsProcessing: false,
                IsReady: true,
                ErrorMessage: "Something went wrong",
                AIResponse: errorMessage
            ));
        }
    }

    private void AddChatMessage(string text, bool isUser, bool isError = false)
    {
        SetState(s =>
        {
            s.VoiceChatHistory.Add(new VoiceChatMessage
            {
                IsUser = isUser,
                Text = text,
                Timestamp = DateTime.Now,
                IsError = isError
            });
        });
    }

    private async Task ShowPermissionDeniedDialogAsync()
    {
        var page = ContainerPage;
        if (page == null) return;
        var openSettings = await page.DisplayAlert(
            "Permission Required",
            "Speech recognition requires microphone and speech permissions. Please enable them in Settings.",
            "Open Settings",
            "Cancel");
        if (openSettings) AppInfo.ShowSettingsUI();
    }

    private void OnPartialResultReceived(object? sender, string partialText)
    {
        ResetSilenceTimer();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            string newTranscript = "";
            SetState(s =>
            {
                s.LastAIResponse = "";
                if (!string.IsNullOrEmpty(s.VoiceTranscript) && !string.IsNullOrEmpty(partialText))
                    s.VoiceTranscript += " " + partialText;
                else
                    s.VoiceTranscript = partialText;
                newTranscript = s.VoiceTranscript;
            });

            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Listening...",
                Transcript: newTranscript,
                IsListening: true,
                IsProcessing: false
            ));
        });
    }

    private void ResetSilenceTimer()
    {
        lock (_silenceLock)
        {
            if (_silenceTimer == null)
            {
                _silenceTimer = new System.Timers.Timer(SilenceTimeoutMs) { AutoReset = false };
                _silenceTimer.Elapsed += OnSilenceDetected;
            }
            _silenceTimer.Stop();
            _silenceTimer.Start();
        }
    }

    private void StopSilenceTimer()
    {
        lock (_silenceLock)
        {
            if (_silenceTimer != null)
            {
                _silenceTimer.Stop();
                _silenceTimer.Elapsed -= OnSilenceDetected;
                _silenceTimer.Dispose();
                _silenceTimer = null;
            }
        }
    }

    private void OnSilenceDetected(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!State.IsRecording) return;
        if (string.IsNullOrWhiteSpace(State.VoiceTranscript)) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _speechRecognitionService.StopListeningAsync();
        });
    }

    /// <summary>
    /// Captures a photo and analyzes it (people count → coffee needs).
    /// Mirrors the camera flow from ShotLoggingPage.
    /// </summary>
    private async Task CaptureAndAnalyzePhotoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                if (ContainerPage != null)
                    await ContainerPage.DisplayAlert("Camera Unavailable",
                        "Camera is not available on this device.", "OK");
                return;
            }

            if (!await _visionService.IsAvailableAsync())
            {
                if (ContainerPage != null)
                    await ContainerPage.DisplayAlert("Vision Unavailable",
                        "Vision service is not configured.", "OK");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Take a photo of the room",
                MaximumWidth = 1024,
                MaximumHeight = 1024,
                CompressionQuality = 70
            });
            if (photo == null) return;

            using var stream = await photo.OpenReadAsync();
            var result = await _visionService.AnalyzeImageAsync(
                stream,
                "Count the people in this image and tell me how many cups of coffee I need to make.");

            if (result.Success)
            {
                var message = result.Message ??
                    $"I see {result.PeopleCount} {(result.PeopleCount == 1 ? "person" : "people")}. " +
                    $"You'll need {result.CupsNeeded} {(result.CupsNeeded == 1 ? "cup" : "cups")} of coffee, " +
                    $"which requires about {result.BeansNeededGrams}g of beans.";

                if (ContainerPage != null)
                    await ContainerPage.DisplayAlert("Analysis Complete", message, "OK");
            }
            else if (ContainerPage != null)
            {
                await ContainerPage.DisplayAlert("Analysis Failed",
                    result.ErrorMessage ?? "Could not analyze the photo.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing and analyzing photo");
            if (ContainerPage != null)
                await ContainerPage.DisplayAlert("Error",
                    $"Failed to capture or analyze photo: {ex.Message}", "OK");
        }
    }
}
