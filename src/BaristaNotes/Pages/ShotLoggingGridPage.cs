using MauiReactor;
using MauiReactor.Shapes;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Microsoft.Extensions.Logging;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

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
    GrindSetting,
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
    public string GrindSetting { get; set; } = "5.5";
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
    public List<string> DrinkTypes { get; set; } = new() { "Espresso", "Americano", "Latte", "Cappuccino", "Flat White", "Cortado", "Pour Over" };

    // Edit mode display
    public string? BeanName { get; set; }

    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }

    // Picker overlay state
    public GridPickerKind ActivePicker { get; set; } = GridPickerKind.None;
}

class ShotLoggingGridPageProps
{
    public int? ShotId { get; set; }
}

partial class ShotLoggingGridPage : Component<ShotLoggingGridState, ShotLoggingGridPageProps>
{
    [Inject] IShotService _shotService;
    [Inject] IBagService _bagService;
    [Inject] IEquipmentService _equipmentService;
    [Inject] IUserProfileService _userProfileService;
    [Inject] IPreferencesService _preferencesService;
    [Inject] IFeedbackService _feedbackService;
    [Inject] ILogger<ShotLoggingGridPage> _logger;

    protected override void OnMounted()
    {
        base.OnMounted();
        SetState(s => s.IsLoading = true);
        _ = LoadDataAsync();
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
                    s.GrindSetting = shot.GrindSetting;
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
                        s.GrindSetting = lastShot.GrindSetting;
                        s.ExpectedTime = lastShot.ExpectedTime;
                        s.ExpectedOutput = lastShot.ExpectedOutput;
                        s.Rating = (lastShot.Rating ?? 3) - 1;
                        s.SelectedBagId = lastShot.Bag?.Id;
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
                    GrindSetting = State.GrindSetting,
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
                    GrindSetting = State.GrindSetting,
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
                    columns: "*,*",
                    Tile("BREW METHOD", State.BrewMethod.DisplayName(),
                            () => Open(GridPickerKind.BrewMethod)).GridRow(0).GridColumn(0),
                    Tile("BAG", BagDisplayValue(),
                            () => Open(GridPickerKind.Bag)).GridRow(0).GridColumn(1),

                    Tile("DRINK TYPE", State.DrinkType,
                            () => Open(GridPickerKind.DrinkType)).GridRow(1).GridColumn(0),
                    Tile("RATING", RatingDisplayValue(),
                            () => Open(GridPickerKind.Rating)).GridRow(1).GridColumn(1),

                    Tile("DOSE IN", $"{State.DoseIn:0.#}", () => Open(GridPickerKind.DoseIn), unit: "g")
                            .GridRow(2).GridColumn(0),
                    Tile("YIELD", $"{State.ExpectedOutput:0.#}", () => Open(GridPickerKind.YieldOut), unit: "g")
                            .GridRow(2).GridColumn(1),

                    Tile("TIME", State.ActualTime.HasValue ? $"{State.ActualTime:0}" : $"{State.ExpectedTime:0}", () => Open(GridPickerKind.ActualTime), unit: "s")
                            .GridRow(3).GridColumn(0),
                    Tile("GRIND", State.GrindSetting,
                            () => Open(GridPickerKind.GrindSetting)).GridRow(3).GridColumn(1),

                    Tile("MADE BY", State.SelectedMaker?.Name ?? "—",
                            () => Open(GridPickerKind.MadeBy)).GridRow(4).GridColumn(0),
                    Tile("MADE FOR", State.SelectedRecipient?.Name ?? "—",
                            () => Open(GridPickerKind.MadeFor)).GridRow(4).GridColumn(1),

                    Tile("MACHINE", EquipmentName(State.SelectedMachineId),
                            () => Open(GridPickerKind.Machine)).GridRow(5).GridColumn(0),
                    Tile("GRINDER", EquipmentName(State.SelectedGrinderId),
                            () => Open(GridPickerKind.Grinder)).GridRow(5).GridColumn(1),

                    Tile("ACCESSORIES", AccessoriesDisplayValue(),
                            () => Open(GridPickerKind.Accessories)).GridRow(6).GridColumn(0),
                    SaveTile().GridRow(6).GridColumn(1),

                    NavTile("ACTIVITY", async () => await MauiControls.Shell.Current.GoToAsync("//history"))
                        .GridRow(7).GridColumn(0),
                    NavTile("SETTINGS", async () => await MauiControls.Shell.Current.GoToAsync("//settings"))
                        .GridRow(7).GridColumn(1)
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
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never));
    }

    void Open(GridPickerKind kind) => SetState(s => s.ActivePicker = kind);
    void ClosePicker() => SetState(s => s.ActivePicker = GridPickerKind.None);

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

    VisualNode NavTile(string label, Action onTap)
    {
        var isLight = Application.Current?.RequestedTheme != AppTheme.Dark;
        var bg = isLight ? AppColors.Light.Surface : AppColors.Dark.Surface;
        var labelColor = isLight ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;

        return Border(
            Label(label.ToUpperInvariant())
                .FontSize(14)
                .CharacterSpacing(3)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(labelColor)
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
                onSelect: o => SetState(s => { s.BrewMethod = (BrewMethod)o; s.ActivePicker = GridPickerKind.None; })),

            GridPickerKind.DrinkType => CategoricalPicker(
                title: "Drink Type",
                items: State.DrinkTypes.Select(d => (Key: (object)d, Display: d)).ToList(),
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
                onDone: () => SetState(s => s.ActivePicker = GridPickerKind.None)),

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
                onSelect: v => SetState(s => { s.ActualTime = (decimal)v; s.ActivePicker = GridPickerKind.None; })),

            GridPickerKind.GrindSetting => NumericScroller(
                title: "Grind Setting",
                unit: null,
                spec: NumericSpecFor(GridPickerKind.GrindSetting),
                current: ParseGrind(State.GrindSetting),
                onSelect: v => SetState(s => { s.GrindSetting = FormatGrind(v); s.ActivePicker = GridPickerKind.None; })),

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
                ? Label("●").FontSize(14).TextColor(accentColor).VCenter().GridColumn(1)
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
        Action onDone)
    {
        var isLight = Application.Current?.RequestedTheme != AppTheme.Dark;
        var textPrimary = isLight ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var textSecondary = isLight ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
        var accent = isLight ? AppColors.Light.Primary : AppColors.Dark.Primary;

        return Grid(rows: "Auto,*,Auto", columns: "*",
            Grid(rows: "*", columns: "Auto,*,Auto",
                Button("Close").OnClicked(ClosePicker)
                    .BackgroundColor(Colors.Transparent).TextColor(textSecondary).GridColumn(0),
                Label(title.ToUpperInvariant())
                    .FontSize(12).CharacterSpacing(3)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(textSecondary).HCenter().VCenter().GridColumn(0).GridColumnSpan(3),
                Button("Done").OnClicked(onDone)
                    .BackgroundColor(Colors.Transparent).TextColor(accent)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .GridColumn(2)
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
                .VCenter().GridColumn(1)
        )
        .Padding(24, 16)
        .OnTapped(onTap);
    }

    // ------------------------------------------------------------
    // Numeric scroller picker
    // ------------------------------------------------------------

    record NumericFieldSpec(double Min, double Max, double Step, string Format);

    NumericFieldSpec NumericSpecFor(GridPickerKind kind)
    {
        var profile = State.BrewMethod.Profile();
        return kind switch
        {
            GridPickerKind.DoseIn => new((double)profile.DoseMin, (double)profile.DoseMax, (double)profile.DoseStep, profile.DoseStep < 1 ? "0.#" : "0"),
            GridPickerKind.YieldOut => new((double)profile.OutputMin, (double)profile.OutputMax, (double)profile.OutputStep, profile.OutputStep < 1 ? "0.#" : "0"),
            GridPickerKind.ActualTime => new((double)profile.TimeMin, (double)profile.TimeMax, (double)profile.TimeStep, "0"),
            GridPickerKind.GrindSetting => new(1.0, 30.0, 0.25, "0.##"),
            _ => new(0, 100, 1, "0")
        };
    }

    static double ParseGrind(string s) => double.TryParse(s, out var v) ? v : 5.5;
    static string FormatGrind(double v) => Math.Abs(v - Math.Round(v)) < 0.001 ? ((int)Math.Round(v)).ToString() : v.ToString("0.##");

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
}
