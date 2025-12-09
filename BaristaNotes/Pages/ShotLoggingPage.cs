using MauiReactor;
using MauiReactor.Shapes;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Components.FormFields;
using BaristaNotes.Components;
using BaristaNotes.Integrations.Popups;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Fonts;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Application = Microsoft.Maui.Controls.Application;


namespace BaristaNotes.Pages;

class ShotLoggingState
{
    public decimal DoseIn { get; set; } = 18.0m;
    public string GrindSetting { get; set; } = "5.5";
    public decimal ExpectedTime { get; set; } = 28;
    public decimal ExpectedOutput { get; set; } = 36.0m;
    public string DrinkType { get; set; } = "Espresso";
    public decimal? ActualTime { get; set; }
    public decimal? ActualOutput { get; set; }
    public int Rating { get; set; } = 2;
    public int? SelectedBagId { get; set; }
    public int SelectedBagIndex { get; set; } = -1;
    public int SelectedDrinkIndex { get; set; } = 0;
    public List<BagSummaryDto> AvailableBags { get; set; } = new();
    public List<string> DrinkTypes { get; set; } = new() { "Espresso", "Americano", "Latte", "Cappuccino", "Flat White", "Cortado" };
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }

    // Edit mode fields
    public DateTimeOffset? Timestamp { get; set; }
    public string? BeanName { get; set; }

    // User tracking fields
    public List<UserProfileDto> AvailableUsers { get; set; } = new();
    public UserProfileDto? SelectedMaker { get; set; }
    public UserProfileDto? SelectedRecipient { get; set; }

    // Equipment tracking fields
    public List<EquipmentDto> AvailableEquipment { get; set; } = new();
    public int? SelectedMachineId { get; set; }
    public int? SelectedGrinderId { get; set; }
    public List<int> SelectedAccessoryIds { get; set; } = new();

    // Bean tracking for inline creation (T001)
    public List<BeanDto> AvailableBeans { get; set; } = new();
}

class ShotLoggingPageProps
{
    public int? ShotId { get; set; }
}

partial class ShotLoggingPage : Component<ShotLoggingState, ShotLoggingPageProps>
{
    [Inject]
    IShotService _shotService;

    [Inject]
    IBeanService _beanService;

    [Inject]
    IBagService _bagService;

    [Inject]
    IEquipmentService _equipmentService;

    [Inject]
    IPreferencesService _preferencesService;

    [Inject]
    IFeedbackService _feedbackService;

    [Inject]
    IUserProfileService _userProfileService;

    protected override void OnMounted()
    {
        base.OnMounted();
        SetState(s => s.IsLoading = true);
        _ = LoadDataAsync();
    }

    void OnPageAppearing()
    {

        //On<iOS>().SetLargeTitleDisplay(LargeTitleDisplayMode.Always);
        // Reload data when tab becomes visible (handles new beans added from Settings)
        _ = LoadDataAsync();
    }

    async Task LoadDataAsync()
    {
        try
        {
            var bags = await _bagService.GetActiveBagsForShotLoggingAsync();
            var users = await _userProfileService.GetAllProfilesAsync();
            var equipment = await _equipmentService.GetAllActiveEquipmentAsync();
            var beans = await _beanService.GetAllActiveBeansAsync(); // T002: Load beans for inline creation

            // Edit mode: Load existing shot
            if (Props.ShotId.HasValue)
            {
                var shot = await _shotService.GetShotByIdAsync(Props.ShotId.Value);
                if (shot == null)
                {
                    await _feedbackService.ShowErrorAsync("Shot not found");
                    await Navigation.PopAsync();
                    return;
                }

                SetState(s =>
                {
                    s.AvailableBags = bags;
                    s.AvailableUsers = users;
                    s.AvailableEquipment = equipment.ToList();
                    s.AvailableBeans = beans; // T002: Track beans for inline creation

                    // Populate from existing shot
                    s.Timestamp = shot.Timestamp;
                    s.BeanName = shot.Bean?.Name;
                    s.DoseIn = shot.DoseIn;
                    s.GrindSetting = shot.GrindSetting;
                    s.ExpectedTime = shot.ExpectedTime;
                    s.ExpectedOutput = shot.ExpectedOutput;
                    s.ActualTime = shot.ActualTime;
                    s.ActualOutput = shot.ActualOutput;
                    s.Rating = shot.Rating ?? 0;
                    s.DrinkType = shot.DrinkType;
                    s.SelectedBagId = shot.Bag?.Id;
                    s.SelectedBagIndex = shot.Bag != null ? s.AvailableBags.FindIndex(b => b.Id == shot.Bag.Id) : -1;
                    s.SelectedDrinkIndex = s.DrinkTypes.IndexOf(shot.DrinkType);

                    // Set maker/recipient from shot
                    s.SelectedMaker = shot.MadeBy;
                    s.SelectedRecipient = shot.MadeFor;

                    // Set equipment from shot
                    s.SelectedMachineId = shot.Machine?.Id;
                    s.SelectedGrinderId = shot.Grinder?.Id;
                    s.SelectedAccessoryIds = shot.Accessories?.Select(a => a.Id).ToList() ?? new();

                    s.IsLoading = false;
                });
            }
            // Add mode: Load last shot as template
            else
            {
                var lastShot = await _shotService.GetMostRecentShotAsync();

                SetState(s =>
                {
                    s.AvailableBags = bags;
                    s.AvailableUsers = users;
                    s.AvailableEquipment = equipment.ToList();
                    s.AvailableBeans = beans; // T002: Track beans for inline creation

                    if (lastShot != null)
                    {
                        s.DoseIn = lastShot.DoseIn;
                        s.GrindSetting = lastShot.GrindSetting;
                        s.ExpectedTime = lastShot.ExpectedTime;
                        s.ExpectedOutput = lastShot.ExpectedOutput;
                        s.ActualTime = lastShot.ActualTime;
                        s.ActualOutput = lastShot.ActualOutput;
                        s.Rating = lastShot.Rating ?? 2;
                        s.DrinkType = lastShot.DrinkType;
                        s.SelectedBagId = lastShot.Bag?.Id;
                        s.SelectedBagIndex = lastShot.Bag != null ? s.AvailableBags.FindIndex(b => b.Id == lastShot.Bag.Id) : -1;
                        s.SelectedDrinkIndex = s.DrinkTypes.IndexOf(lastShot.DrinkType);

                        // Load last-used maker/recipient from preferences
                        var lastMakerId = _preferencesService.GetLastMadeById();
                        var lastRecipientId = _preferencesService.GetLastMadeForId();

                        if (lastMakerId.HasValue)
                            s.SelectedMaker = users.FirstOrDefault(u => u.Id == lastMakerId.Value);

                        if (lastRecipientId.HasValue)
                            s.SelectedRecipient = users.FirstOrDefault(u => u.Id == lastRecipientId.Value);
                    }

                    // Load last-used equipment from preferences (always, even without lastShot)
                    s.SelectedMachineId = _preferencesService.GetLastMachineId();
                    s.SelectedGrinderId = _preferencesService.GetLastGrinderId();
                    s.SelectedAccessoryIds = _preferencesService.GetLastAccessoryIds();

                    s.IsLoading = false;
                });
            }
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = ex.Message;
            });
        }
    }

    async Task LoadBestShotSettingsAsync(int bagId)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ShotLoggingPage] LoadBestShotSettingsAsync called for bagId: {bagId}");
            var bestShot = await _shotService.GetBestRatedShotByBagAsync(bagId);

            if (bestShot != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ShotLoggingPage] Found best shot: DoseIn={bestShot.DoseIn}, GrindSetting={bestShot.GrindSetting}, ExpectedOutput={bestShot.ExpectedOutput}, ExpectedTime={bestShot.ExpectedTime}");
                SetState(s =>
                {
                    s.DoseIn = bestShot.DoseIn;
                    s.ExpectedOutput = bestShot.ExpectedOutput;
                    s.ExpectedTime = bestShot.ExpectedTime;
                    s.GrindSetting = bestShot.GrindSetting;
                });
                await _feedbackService.ShowSuccessAsync("Loaded settings from your best rated shot");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ShotLoggingPage] No rated shots found for bagId: {bagId}");
                await _feedbackService.ShowSuccessAsync("No rated shots found for this bag");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShotLoggingPage] Error loading best shot settings: {ex.Message}");
            await _feedbackService.ShowErrorAsync("Failed to load shot settings");
        }
    }

    async Task SaveShotAsync()
    {
        try
        {
            // Edit mode: Update existing shot
            if (Props.ShotId.HasValue)
            {
                if (State.SelectedBagId == null)
                {
                    await _feedbackService.ShowErrorAsync("Please select a bag");
                    return;
                }

                var updateDto = new UpdateShotDto
                {
                    BagId = State.SelectedBagId.Value,
                    MadeById = State.SelectedMaker?.Id,
                    MadeForId = State.SelectedRecipient?.Id,
                    ActualTime = State.ActualTime,
                    ActualOutput = State.ActualOutput,
                    Rating = State.Rating > 0 ? State.Rating : null,
                    DrinkType = State.DrinkType
                };

                await _shotService.UpdateShotAsync(Props.ShotId.Value, updateDto);

                System.Diagnostics.Debug.WriteLine("[ShotLoggingPage] About to call ShowSuccessAsync");
                await _feedbackService.ShowSuccessAsync("Shot updated successfully");
                System.Diagnostics.Debug.WriteLine("[ShotLoggingPage] ShowSuccessAsync completed");

                System.Diagnostics.Debug.WriteLine("[ShotLoggingPage] About to navigate back");
                await Navigation.PopAsync();
                System.Diagnostics.Debug.WriteLine("[ShotLoggingPage] Navigation completed");
            }
            // Add mode: Create new shot
            else
            {
                if (State.SelectedBagId == null)
                {
                    if (State.AvailableBags.Count == 0)
                    {
                        await _feedbackService.ShowErrorAsync("No active bags", "Please add a bag before logging a shot");
                    }
                    else
                    {
                        await _feedbackService.ShowErrorAsync("Please select a bag", "Choose a bag before logging your shot");
                    }
                    return;
                }

                var createDto = new CreateShotDto
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
                    Rating = State.Rating
                };

                await _shotService.CreateShotAsync(createDto);

                _preferencesService.SetLastDrinkType(State.DrinkType);
                if (State.SelectedBagId.HasValue)
                {
                    _preferencesService.SetLastBagId(State.SelectedBagId.Value);
                }

                // Save last-used equipment to preferences
                _preferencesService.SetLastMachineId(State.SelectedMachineId);
                _preferencesService.SetLastGrinderId(State.SelectedGrinderId);
                _preferencesService.SetLastAccessoryIds(State.SelectedAccessoryIds);

                await _feedbackService.ShowSuccessAsync($"{State.DrinkType} shot logged successfully");

                await LoadDataAsync();
            }
        }
        catch (Exception ex)
        {

            await _feedbackService.ShowErrorAsync(Props.ShotId.HasValue ? "Failed to update shot" : "Failed to save shot", "Please try again");
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = ex.Message;
            });
        }
    }

    #region Inline Bean/Bag Creation (T011-T017, T018-T022)

    /// <summary>
    /// Shows combined bean+bag creation popup for inline creation flow.
    /// Used when no beans exist in the system - creates both in one step.
    /// </summary>
    async Task ShowBeanAndBagCreationPopup()
    {
        var popup = new BeanAndBagCreationPopup(_beanService, _bagService)
        {
            OnCreated = HandleBagCreated  // Reuse existing handler
        };

        await IPopupService.Current.PushAsync(popup);
    }

    /// <summary>
    /// Shows bag creation popup with bean picker (T019).
    /// Used when beans exist but no active bags.
    /// </summary>
    async Task ShowBagCreationPopupWithPicker()
    {
        var popup = new BagCreationPopup(_bagService)
        {
            AvailableBeans = State.AvailableBeans,
            OnBagCreated = HandleBagCreated
        };
        popup.Build();  // Build content after setting properties

        await IPopupService.Current.PushAsync(popup);
    }

    /// <summary>
    /// Handles successful bag creation - refreshes data and auto-selects bag (T016, T017, T024).
    /// </summary>
    void HandleBagCreated(BagSummaryDto newBag)
    {
        // Refresh data and auto-select the new bag (T017, T024)
        _ = RefreshAndSelectBag(newBag.Id);
    }

    /// <summary>
    /// Refreshes data after bag creation and auto-selects the new bag (T017, T023, T024).
    /// </summary>
    async Task RefreshAndSelectBag(int newBagId)
    {
        await LoadDataAsync();

        // Find and select the new bag (T024)
        var newBagIndex = State.AvailableBags.FindIndex(b => b.Id == newBagId);
        if (newBagIndex >= 0)
        {
            SetState(s =>
            {
                s.SelectedBagIndex = newBagIndex;
                s.SelectedBagId = newBagId;
            });

            // Load best shot settings for the new bag
            await LoadBestShotSettingsAsync(newBagId);
        }
    }

    /// <summary>
    /// Renders the "no beans" empty state with Create Bean CTA (T012).
    /// Shown when AvailableBeans.Count == 0.
    /// </summary>
    VisualNode RenderNoBeanEmptyState()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = isLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;

        return VStack(spacing: 16,
            // Coffee bean icon - MUST use MaterialSymbolsFont per Constitution Principle III
            Label(MaterialSymbolsFont.Coffee)
                .FontFamily(MaterialSymbolsFont.FontFamily)
                .FontSize(48)
                .TextColor(textColor)
                .HCenter(),

            Label("No beans configured")
                .FontSize(20)
                .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                .TextColor(textColor)
                .HCenter(),

            Label("Create your first bean to start logging shots")
                .FontSize(14)
                .TextColor(secondaryTextColor)
                .HCenter()
                .HorizontalTextAlignment(TextAlignment.Center),

            Button("Add Coffee")
                .OnClicked(async () => await ShowBeanAndBagCreationPopup())
                .BackgroundColor(primaryColor)
                .TextColor(Colors.White)
                .HeightRequest(50)
                .WidthRequest(200)
                .HCenter()
        )
        .VCenter()
        .HCenter()
        .Padding(32);
    }

    #endregion

    public override VisualNode Render()
    {
        if (State.IsLoading && !State.AvailableBags.Any())
        {
            return ContentPage(Props.ShotId.HasValue ? "Edit Shot" : "New Shot",
                VStack(
                    ActivityIndicator()
                        .IsRunning(true),
                    Label("Loading...")
                        .Margin(0, 8)
                )
                .VCenter()
                .HCenter()
            )
            .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
            .OnAppearing(() => OnPageAppearing());
        }

        // T011: Show "no beans" empty state when no beans exist (new users)
        // Only for add mode - edit mode should still show the form
        if (!Props.ShotId.HasValue && !State.IsLoading && State.AvailableBeans.Count == 0)
        {
            return ContentPage("New Shot",
                RenderNoBeanEmptyState()
            )
            .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
            .OnAppearing(() => OnPageAppearing());
        }

        return ContentPage(Props.ShotId.HasValue ? "Edit Shot" : "New Shot",
            ScrollView(
                VStack(
                    // Error message
                    State.ErrorMessage != null ?
                        Label(State.ErrorMessage)
                            .TextColor(Colors.Red)
                            .FontSize(14)
                            .Margin(0, 8) :
                        null,

                    // In/Out Gauges in 2-column grid
                    RenderDoseGauges(),

                    new FormSliderField()
                        .Label($"Time: {State.ActualTime?.ToString("F0") ?? "0"}s")
                        .Minimum(0)
                        .Maximum(60)
                        .Value((double)(State.ActualTime ?? 0))
                        .OnValueChanged(val => SetState(s => s.ActualTime = (decimal)val)),

                    // User Selection Row (Made By -> Made For)
                    RenderUserSelectionRow(),

                    // Rating
                    RenderRatingSelector(),

                    // Save Button
                    Button(Props.ShotId.HasValue ? "Update Shot" : "Add Shot")
                        .IsEnabled(!State.IsLoading)
                        .OnClicked(async () => await SaveShotAsync())
                        .HeightRequest(50),

                    BoxView()
                        .HorizontalOptions(LayoutOptions.Fill)
                        .HeightRequest(1)
                        .Margin(0, AppSpacing.L, 0, 0),

                    Label()
                        .Text("Additional Details")
                        .ThemeKey(ThemeKeys.MutedText),

                    // Bag Picker with empty state handling
                    State.AvailableBags.Count > 0 ?
                        new FormPickerField()
                            .Label("Bag")
                            .Title("Select Bag")
                            .ItemsSource(State.AvailableBags.Select(b => b.DisplayLabel).ToList())
                            .SelectedIndex(State.SelectedBagIndex)
                            .OnSelectedIndexChanged(idx =>
                            {
                                if (idx >= 0 && idx < State.AvailableBags.Count)
                                {
                                    var bagId = State.AvailableBags[idx].Id;
                                    SetState(s =>
                                    {
                                        s.SelectedBagIndex = idx;
                                        s.SelectedBagId = bagId;
                                    });
                                    _ = LoadBestShotSettingsAsync(bagId);
                                }
                            }) :
                        // T018: Enhanced "no active bags" empty state with inline bag creation
                        VStack(spacing: 12,
                            Label("No active bags available")
                                .ThemeKey(ThemeKeys.SecondaryText)
                                .FontSize(16)
                                .HCenter(),
                            Label("Create a bag to start logging shots")
                                .ThemeKey(ThemeKeys.MutedText)
                                .FontSize(14)
                                .HCenter(),
                            // T019: Use inline bag creation popup with bean picker
                            Button("Add New Bag")
                                .OnClicked(async () => await ShowBagCreationPopupWithPicker())
                                .HCenter()
                        ).Padding(16),

                    // Grind Setting
                    new FormEntryField()
                        .Label("Grind Setting")
                        .Text(State.GrindSetting)
                        .OnTextChanged(text => SetState(s => s.GrindSetting = text)),

                    // Expected Time
                    new FormEntryField()
                        .Label("Expected Time (s)")
                        .Text(State.ExpectedTime.ToString())
                        .Keyboard(Microsoft.Maui.Keyboard.Numeric)
                        .OnTextChanged(text =>
                        {
                            if (decimal.TryParse(text, out var val))
                                SetState(s => s.ExpectedTime = val);
                        }),

                    // Expected Output
                    new FormEntryField()
                        .Label("Expected Output (g)")
                        .Text(State.ExpectedOutput.ToString("F1"))
                        .Keyboard(Microsoft.Maui.Keyboard.Numeric)
                        .OnTextChanged(text =>
                        {
                            if (decimal.TryParse(text, out var val))
                                SetState(s => s.ExpectedOutput = val);
                        }),

                    // Drink Type
                    new FormPickerField()
                        .Label("Drink Type")
                        .Title("Select Drink")
                        .ItemsSource(State.DrinkTypes)
                        .SelectedIndex(State.SelectedDrinkIndex)
                        .OnSelectedIndexChanged(idx =>
                        {
                            if (idx >= 0 && idx < State.DrinkTypes.Count)
                            {
                                SetState(s =>
                                {
                                    s.SelectedDrinkIndex = idx;
                                    s.DrinkType = State.DrinkTypes[idx];
                                });
                            }
                        })



                ).Spacing(AppSpacing.M)
                .Padding(16, 0, 16, 32)
            )
        )
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
        .OnAppearing(() => OnPageAppearing());
    }

    VisualNode RenderDoseGauges()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = AppColors.Light.Primary;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;

        // Count selected equipment for badge
        var selectedCount = (State.SelectedMachineId.HasValue ? 1 : 0) +
                           (State.SelectedGrinderId.HasValue ? 1 : 0) +
                           State.SelectedAccessoryIds.Count;

        return Grid("Auto", "*, Auto, *",
            // In Gauge (left column)
            Grid(
                RenderSingleGauge(
                    value: (double)State.DoseIn,
                    min: 15,
                    max: 20,
                    primaryColor: primaryColor,
                    textColor: textColor,
                    secondaryTextColor: secondaryTextColor,
                    surfaceColor: surfaceColor,
                    onValueChanged: val => SetState(st => st.DoseIn = (decimal)val)
                ),
                Label()
                    .Text("u")
                    .FontFamily("coffee-icons")
                    .TextColor(secondaryTextColor)
                    .FontSize(24)
                    .HCenter()
                    .VEnd()
            ).GridColumn(0),

            // Equipment button (center column)
            Grid(
                Border(
                    Label()
                        .Text("s") // Machine icon from coffee-icons font
                        .FontFamily("coffee-icons")
                        .FontSize(32)
                        // .TextColor(selectedCount > 0 ? primaryColor : secondaryTextColor)
                        .HCenter()
                        .VCenter()
                        .TranslationX(3)
                )
                .StrokeShape(new RoundRectangle().CornerRadius(25))
                .BackgroundColor(surfaceColor)
                .HeightRequest(50)
                .WidthRequest(50)
                .OnTapped(() => _ = ShowEquipmentSelectionPopup()),

                // Badge showing count of selected equipment
                selectedCount > 0 ?
                    Border(
                        Label(selectedCount.ToString())
                            .FontSize(10)
                            .TextColor(Colors.White)
                            .HCenter()
                            .VCenter()
                    )
                    .StrokeShape(new RoundRectangle().CornerRadius(8))
                    .BackgroundColor(primaryColor)
                    .HeightRequest(16)
                    .WidthRequest(16)
                    .HEnd()
                    .VStart()
                    .Margin(0, -4, -4, 0) : null
            )
            .GridColumn(1).VCenter(),

            // Out Gauge (right column)
            Grid(
                RenderSingleGauge(
                    value: (double)(State.ActualOutput ?? 0),
                    min: 25,
                    max: 50,
                    primaryColor: primaryColor,
                    textColor: textColor,
                    secondaryTextColor: secondaryTextColor,
                    surfaceColor: surfaceColor,
                    onValueChanged: val => SetState(st => st.ActualOutput = (decimal)val)
                ),
                Label()
                    .Text("t")
                    .FontFamily("coffee-icons")
                    .TextColor(secondaryTextColor)
                    .FontSize(24)
                    .HCenter()
                    .VEnd()
            ).GridColumn(2)
        );
    }

    VisualNode RenderSingleGauge(
        double value,
        double min,
        double max,
        Color primaryColor,
        Color textColor,
        Color secondaryTextColor,
        Color surfaceColor,
        Action<double> onValueChanged)
    {
        return Grid(
            new SfRadialGauge()
                .HeightRequest(160)
                .WidthRequest(160)
                .BackgroundColor(Colors.Transparent)
                .WithAxis(
                    new RadialAxis()
                        .Minimum(min)
                        .Maximum(max)
                        .EnableLoadingAnimation(true)
                        .Interval((max - min) / 5)
                        .MinorTicksPerInterval(1)
                        .ShowLabels(true)
                        .ShowTicks(false)
                        .RadiusFactor(0.8)
                        .LabelFormat("0")
                        .AxisLabelStyle(new Syncfusion.Maui.Gauges.GaugeLabelStyle
                        {
                            TextColor = secondaryTextColor,
                            FontSize = 10
                        })
                        .AxisLineStyle(new Syncfusion.Maui.Gauges.RadialLineStyle
                        {
                            Fill = new SolidColorBrush(surfaceColor),
                            Thickness = 20,
                            CornerStyle = Syncfusion.Maui.Gauges.CornerStyle.BothCurve
                        })
                        .WithPointers(
                            new RangePointer()
                                .Value(value)
                                .CornerStyle(Syncfusion.Maui.Gauges.CornerStyle.BothCurve)
                                .PointerWidth(20)
                                .Fill(new SolidColorBrush(primaryColor)),

                            new ShapePointer()
                                .Value(value)
                                .IsInteractive(true)
                                .StepFrequency(0.1)
                                .ShapeType(Syncfusion.Maui.Gauges.ShapeType.Circle)
                                .ShapeHeight(28)
                                .ShapeWidth(28)
                                .Fill(new SolidColorBrush(primaryColor))
                                .HasShadow(true)
                                .Offset(0)
                                .OnValueChanged((s, e) =>
                                {
                                    if (e is Syncfusion.Maui.Gauges.ValueChangedEventArgs syncArgs)
                                    {
                                        var roundedValue = Math.Round(syncArgs.Value, 1);
                                        onValueChanged(roundedValue);
                                    }
                                })
                        )
                ),

            // Overlay center labels
            VStack(spacing: 0,
                Label(value.ToString("F1"))
                    .FontSize(20)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                    .TextColor(textColor)
                    .HCenter(),
                Label("g")
                    .FontSize(9)
                    .TextColor(secondaryTextColor)
                    .HCenter()
            ).VCenter().HCenter().TranslationY(10),

            // Add/subtract buttons
            ImageButton().Source(AppIcons.Decrement).HStart().VEnd().TranslationY(10).Aspect(Aspect.Center)
                .OnClicked(() => onValueChanged(Math.Round(value - 0.1, 1))),
            ImageButton().Source(AppIcons.Increment).HEnd().VEnd().TranslationY(10).Aspect(Aspect.Center)
                .OnClicked(() => onValueChanged(Math.Round(value + 0.1, 1)))

        ).HeightRequest(160).WidthRequest(160).HCenter();
    }

    VisualNode RenderUserSelectionRow()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = AppColors.Light.Primary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;

        return Grid("Auto, Auto", "Auto, Auto, Auto",
            // Made By avatar
            RenderUserAvatar(
                user: State.SelectedMaker,
                backgroundColor: surfaceColor,
                iconColor: secondaryTextColor,
                onTapped: () => _ = ShowUserSelectionPopup("Made By", user => SetState(s => s.SelectedMaker = user))
            ).GridRow(0).GridColumn(0),

            // Arrow
            Label(MaterialSymbolsFont.Arrow_forward)
                .FontFamily(MaterialSymbolsFont.FontFamily)
                .FontSize(24)
                .TextColor(secondaryTextColor)
                .VCenter()
                .HCenter()
                .GridRow(0).GridColumn(1),

            // Made For avatar
            RenderUserAvatar(
                user: State.SelectedRecipient,
                backgroundColor: surfaceColor,
                iconColor: secondaryTextColor,
                onTapped: () => _ = ShowUserSelectionPopup("Made For", user => SetState(s => s.SelectedRecipient = user))
            ).GridRow(0).GridColumn(2),

            // Labels row
            Label("Made by")
                .FontSize(12)
                .TextColor(secondaryTextColor)
                .HCenter()
                .GridRow(1).GridColumn(0),

            Label("For")
                .FontSize(12)
                .TextColor(secondaryTextColor)
                .HCenter()
                .GridRow(1).GridColumn(2)
        ).ColumnSpacing(16).RowSpacing(4).HCenter();
    }

    VisualNode RenderRatingSelector()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = isLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var mutedColor = isLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted;

        return HStack(spacing: 8,
            AppIcons.RatingIcons.Select((icon, index) =>
                Border(
                    Label(icon)
                        .FontFamily(MaterialSymbolsFont.FontFamily)
                        .FontSize(32)
                        .TextColor(State.Rating == index ? primaryColor : mutedColor)
                        .HCenter()
                        .VCenter()
                )
                // .StrokeShape(new RoundRectangle().CornerRadius(8))
                // .BackgroundColor(State.Rating == index ? surfaceColor : Colors.Transparent)
                // .Stroke(State.Rating == index ? primaryColor : Colors.Transparent)
                // .StrokeThickness(State.Rating == index ? 1 : 0)
                .StrokeThickness(0)
                .HeightRequest(48)
                .WidthRequest(48)
                .OnTapped(() => SetState(s => s.Rating = index))
            ).ToArray()
        ).HCenter();
    }

    VisualNode RenderUserAvatar(UserProfileDto? user, Color backgroundColor, Color iconColor, Action onTapped)
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;

        // Show profile image if available, otherwise show default icon
        if (user != null && !string.IsNullOrEmpty(user.AvatarPath))
        {
            // Convert filename to full file path
            var imagePath = System.IO.Path.Combine(FileSystem.AppDataDirectory, user.AvatarPath);

            // User has a profile photo - display it
            return Border(
                Image(imagePath)
                    .Aspect(Aspect.AspectFill)
            )
            .StrokeShape(new RoundRectangle().CornerRadius(30))
            .BackgroundColor(backgroundColor)
            .HeightRequest(60)
            .WidthRequest(60)
            .OnTapped(onTapped);
        }
        else
        {
            // No profile photo - show default person icon
            return Border(
                Grid(
                    Label(MaterialSymbolsFont.Account_circle)
                        .FontFamily(MaterialSymbolsFont.FontFamily)
                        .FontSize(36)
                        .TextColor(user != null ? textColor : iconColor)
                        .HCenter()
                        .VCenter()
                )
            )
            .StrokeShape(new RoundRectangle().CornerRadius(30))
            .BackgroundColor(backgroundColor)
            .HeightRequest(60)
            .WidthRequest(60)
            .OnTapped(onTapped);
        }
    }

    async Task ShowUserSelectionPopup(string title, Action<UserProfileDto> onSelected)
    {
        // Create a simple wrapper class for display
        var userItems = State.AvailableUsers.Select(user => new UserSelectionItem
        {
            User = user,
            Name = user.Name,
            Icon = MaterialSymbolsFont.Account_circle,
            // Convert filename to full path
            AvatarPath = string.IsNullOrEmpty(user.AvatarPath)
                ? null
                : System.IO.Path.Combine(FileSystem.AppDataDirectory, user.AvatarPath)
        }).ToList();

        ListActionPopup? popup = null;
        popup = new ListActionPopup
        {
            Title = title,
            ShowActionButton = false,
            ItemsSource = userItems,
            ItemDataTemplate = new Microsoft.Maui.Controls.DataTemplate(() =>
            {
                var tapGesture = new Microsoft.Maui.Controls.TapGestureRecognizer();
                tapGesture.SetBinding(Microsoft.Maui.Controls.TapGestureRecognizer.CommandParameterProperty, ".");
                tapGesture.Tapped += async (s, e) =>
                {
                    if (e is Microsoft.Maui.Controls.TappedEventArgs args && args.Parameter is UserSelectionItem item)
                    {
                        onSelected(item.User);
                        await IPopupService.Current.PopAsync();
                    }
                };

                var layout = new Microsoft.Maui.Controls.HorizontalStackLayout
                {
                    Spacing = 12,
                    Padding = new Thickness(0, 8)
                };
                layout.GestureRecognizers.Add(tapGesture);

                // Avatar container - conditionally show image or icon
                var avatarContainer = new Microsoft.Maui.Controls.Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                    HeightRequest = 40,
                    WidthRequest = 40,
                    VerticalOptions = LayoutOptions.Center
                };

                // Bind to determine if we show image or icon
                var avatarPathBinding = new Microsoft.Maui.Controls.Binding("AvatarPath");
                avatarContainer.SetBinding(Microsoft.Maui.Controls.BindableObject.BindingContextProperty, ".");

                // Create both image and icon, we'll show one based on AvatarPath
                var avatarImage = new Microsoft.Maui.Controls.Image
                {
                    Aspect = Aspect.AspectFill,
                    HeightRequest = 40,
                    WidthRequest = 40
                };
                avatarImage.SetBinding(Microsoft.Maui.Controls.Image.SourceProperty, "AvatarPath");
                avatarImage.SetBinding(Microsoft.Maui.Controls.Image.IsVisibleProperty, new Microsoft.Maui.Controls.Binding("AvatarPath", converter: new NotNullOrEmptyConverter()));

                var icon = new Microsoft.Maui.Controls.Label
                {
                    FontFamily = MaterialSymbolsFont.FontFamily,
                    FontSize = 32,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.White
                };
                icon.SetBinding(Microsoft.Maui.Controls.Label.TextProperty, "Icon");
                icon.SetBinding(Microsoft.Maui.Controls.Label.IsVisibleProperty, new Microsoft.Maui.Controls.Binding("AvatarPath", converter: new NullOrEmptyConverter()));

                var avatarGrid = new Microsoft.Maui.Controls.Grid();
                avatarGrid.Children.Add(avatarImage);
                avatarGrid.Children.Add(icon);

                avatarContainer.Content = avatarGrid;

                var label = new Microsoft.Maui.Controls.Label
                {
                    FontSize = 16,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.White
                };
                label.SetBinding(Microsoft.Maui.Controls.Label.TextProperty, "Name");

                layout.Children.Add(avatarContainer);
                layout.Children.Add(label);

                return layout;
            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    async Task ShowEquipmentSelectionPopup()
    {
        // Close any existing popup first
        try { await IPopupService.Current.PopAsync(); } catch { }

        // Group equipment by type with display names
        var typeDisplayNames = new Dictionary<EquipmentType, string>
        {
            { EquipmentType.Machine, "Machines" },
            { EquipmentType.Grinder, "Grinders" },
            { EquipmentType.Tamper, "Tampers" },
            { EquipmentType.PuckScreen, "Puck Screens" },
            { EquipmentType.Other, "Other" }
        };

        // Build grouped equipment items for display
        var equipmentItems = State.AvailableEquipment
            .OrderBy(e => e.Type)
            .ThenBy(e => e.Name)
            .Select(e => new EquipmentSelectionItem
            {
                Equipment = e,
                Name = e.Name,
                GroupName = typeDisplayNames.GetValueOrDefault(e.Type, "Other"),
                IsSelected = IsEquipmentSelected(e)
            })
            .ToList();

        if (!equipmentItems.Any())
        {
            await _feedbackService.ShowErrorAsync("No equipment available", "Add equipment in Settings first");
            return;
        }

        var popup = new ListActionPopup
        {
            Title = "Select Equipment",
            ActionButtonText = "Done",
            ShowActionButton = true,
            ItemsSource = equipmentItems,
            ItemDataTemplate = new Microsoft.Maui.Controls.DataTemplate(() =>
            {
                var tapGesture = new Microsoft.Maui.Controls.TapGestureRecognizer();
                tapGesture.SetBinding(Microsoft.Maui.Controls.TapGestureRecognizer.CommandParameterProperty, ".");
                tapGesture.Tapped += (s, e) =>
                {
                    if (e is Microsoft.Maui.Controls.TappedEventArgs args && args.Parameter is EquipmentSelectionItem item)
                    {
                        ToggleEquipmentSelection(item.Equipment);
                    }
                };

                var layout = new Microsoft.Maui.Controls.HorizontalStackLayout
                {
                    Spacing = 12,
                    Padding = new Thickness(0, 8)
                };
                layout.GestureRecognizers.Add(tapGesture);

                // Checkbox icon
                var checkIcon = new Microsoft.Maui.Controls.Label
                {
                    FontFamily = MaterialSymbolsFont.FontFamily,
                    FontSize = 24,
                    VerticalOptions = LayoutOptions.Center
                };
                checkIcon.SetBinding(Microsoft.Maui.Controls.Label.TextProperty,
                    new Microsoft.Maui.Controls.Binding("IsSelected", converter: new BoolToCheckIconConverter()));
                checkIcon.SetBinding(Microsoft.Maui.Controls.Label.TextColorProperty,
                    new Microsoft.Maui.Controls.Binding("IsSelected", converter: new BoolToCheckColorConverter()));

                // Equipment name
                var nameLabel = new Microsoft.Maui.Controls.Label
                {
                    FontSize = 16,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.White
                };
                nameLabel.SetBinding(Microsoft.Maui.Controls.Label.TextProperty, "Name");

                // Group name (smaller, secondary)
                var groupLabel = new Microsoft.Maui.Controls.Label
                {
                    FontSize = 12,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.Gray
                };
                groupLabel.SetBinding(Microsoft.Maui.Controls.Label.TextProperty, "GroupName");

                var textStack = new Microsoft.Maui.Controls.VerticalStackLayout { Spacing = 2 };
                textStack.Children.Add(nameLabel);
                textStack.Children.Add(groupLabel);

                layout.Children.Add(checkIcon);
                layout.Children.Add(textStack);

                return layout;
            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    bool IsEquipmentSelected(EquipmentDto equipment)
    {
        return equipment.Type switch
        {
            EquipmentType.Machine => State.SelectedMachineId == equipment.Id,
            EquipmentType.Grinder => State.SelectedGrinderId == equipment.Id,
            _ => State.SelectedAccessoryIds.Contains(equipment.Id)
        };
    }

    void ToggleEquipmentSelection(EquipmentDto equipment)
    {
        SetState(s =>
        {
            switch (equipment.Type)
            {
                case EquipmentType.Machine:
                    // Toggle: if already selected, deselect; otherwise select this one
                    s.SelectedMachineId = s.SelectedMachineId == equipment.Id ? null : equipment.Id;
                    break;

                case EquipmentType.Grinder:
                    s.SelectedGrinderId = s.SelectedGrinderId == equipment.Id ? null : equipment.Id;
                    break;

                case EquipmentType.Tamper:
                case EquipmentType.PuckScreen:
                case EquipmentType.Other:
                    // For accessories, only one per type
                    var existingOfSameType = s.AvailableEquipment
                        .Where(e => e.Type == equipment.Type && s.SelectedAccessoryIds.Contains(e.Id))
                        .Select(e => e.Id)
                        .ToList();

                    // Remove any existing selection of the same type
                    foreach (var id in existingOfSameType)
                    {
                        s.SelectedAccessoryIds.Remove(id);
                    }

                    // If we didn't just deselect this item, add it
                    if (!existingOfSameType.Contains(equipment.Id))
                    {
                        s.SelectedAccessoryIds.Add(equipment.Id);
                    }
                    break;
            }
        });

        // Re-show the popup with updated selections
        _ = ShowEquipmentSelectionPopup();
    }

    // Helper class for user selection display
    class UserSelectionItem
    {
        public UserProfileDto User { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
    }

    // Helper class for equipment selection display
    class EquipmentSelectionItem
    {
        public EquipmentDto Equipment { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    // Converter to show check icon based on selection state
    class BoolToCheckIconConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool isSelected && isSelected
                ? MaterialSymbolsFont.Check_circle
                : MaterialSymbolsFont.Radio_button_unchecked;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter to show check color based on selection state
    class BoolToCheckColorConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool isSelected && isSelected
                ? AppColors.Light.Primary
                : Colors.Gray;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter to check if string is null or empty
    class NullOrEmptyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return value == null || (value is string str && string.IsNullOrEmpty(str));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Converter to check if string is NOT null or empty
    class NotNullOrEmptyConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            return value != null && value is string str && !string.IsNullOrEmpty(str);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
