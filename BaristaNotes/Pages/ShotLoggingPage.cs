using MauiReactor;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Components.FormFields;
using BaristaNotes.Components;

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
    public int Rating { get; set; }
    public int? SelectedBeanId { get; set; }
    public int SelectedBeanIndex { get; set; } = -1;
    public int SelectedDrinkIndex { get; set; } = 0;
    public List<BeanDto> AvailableBeans { get; set; } = new();
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

    async Task LoadDataAsync()
    {
        try
        {
            var beans = await _beanService.GetAllActiveBeansAsync();
            var users = await _userProfileService.GetAllProfilesAsync();

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
                    s.AvailableBeans = beans.ToList();
                    s.AvailableUsers = users;

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
                    s.SelectedBeanId = shot.Bean?.Id;
                    s.SelectedBeanIndex = shot.Bean != null ? s.AvailableBeans.FindIndex(b => b.Id == shot.Bean.Id) : -1;
                    s.SelectedDrinkIndex = s.DrinkTypes.IndexOf(shot.DrinkType);

                    // Set maker/recipient from shot
                    s.SelectedMaker = shot.MadeBy;
                    s.SelectedRecipient = shot.MadeFor;

                    s.IsLoading = false;
                });
            }
            // Add mode: Load last shot as template
            else
            {
                var lastShot = await _shotService.GetMostRecentShotAsync();

                SetState(s =>
                {
                    s.AvailableBeans = beans.ToList();
                    s.AvailableUsers = users;

                    if (lastShot != null)
                    {
                        s.DoseIn = lastShot.DoseIn;
                        s.GrindSetting = lastShot.GrindSetting;
                        s.ExpectedTime = lastShot.ExpectedTime;
                        s.ExpectedOutput = lastShot.ExpectedOutput;
                        s.DrinkType = lastShot.DrinkType;
                        s.SelectedBeanId = lastShot.Bean?.Id;
                        s.SelectedBeanIndex = lastShot.Bean != null ? s.AvailableBeans.FindIndex(b => b.Id == lastShot.Bean.Id) : -1;
                        s.SelectedDrinkIndex = s.DrinkTypes.IndexOf(lastShot.DrinkType);

                        // Load last-used maker/recipient from preferences
                        var lastMakerId = _preferencesService.GetLastMadeById();
                        var lastRecipientId = _preferencesService.GetLastMadeForId();

                        if (lastMakerId.HasValue)
                            s.SelectedMaker = users.FirstOrDefault(u => u.Id == lastMakerId.Value);

                        if (lastRecipientId.HasValue)
                            s.SelectedRecipient = users.FirstOrDefault(u => u.Id == lastRecipientId.Value);
                    }

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

    async Task SaveShotAsync()
    {
        try
        {
            // Edit mode: Update existing shot
            if (Props.ShotId.HasValue)
            {
                if (State.SelectedBeanId == null)
                {
                    await _feedbackService.ShowErrorAsync("Please select a bean");
                    return;
                }

                var updateDto = new UpdateShotDto
                {
                    BeanId = State.SelectedBeanId.Value,
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
                if (State.SelectedBeanId == null)
                {

                    await _feedbackService.ShowErrorAsync("Please select a bean", "Choose a bean before logging your shot");
                    return;
                }

                var createDto = new CreateShotDto
                {
                    BeanId = State.SelectedBeanId.Value,
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
                if (State.SelectedBeanId.HasValue)
                {
                    _preferencesService.SetLastBeanId(State.SelectedBeanId.Value);
                }


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

    public override VisualNode Render()
    {
        if (State.IsLoading && !State.AvailableBeans.Any())
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
            );
        }

        return ContentPage(Props.ShotId.HasValue ? "Edit Shot" : "New Shot",
            ScrollView(
                VStack(spacing: 16,

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


                    // Bean Picker
                    new FormPickerField()
                        .Label("Bean")
                        .Title("Select Bean")
                        .ItemsSource(State.AvailableBeans.Select(b => b.Name).ToList())
                        .SelectedIndex(State.SelectedBeanIndex)
                        .OnSelectedIndexChanged(idx =>
                        {
                            if (idx >= 0 && idx < State.AvailableBeans.Count)
                            {
                                SetState(s =>
                                {
                                    s.SelectedBeanIndex = idx;
                                    s.SelectedBeanId = State.AvailableBeans[idx].Id;
                                });
                            }
                        }),

                    // Made By Picker
                    new FormPickerField()
                        .Label("Made By")
                        .Title("Select Barista")
                        .ItemsSource(State.AvailableUsers.Select(u => u.Name).ToList())
                        .SelectedIndex(State.SelectedMaker != null ?
                            State.AvailableUsers.FindIndex(u => u.Id == State.SelectedMaker.Id) : -1)
                        .OnSelectedIndexChanged(idx =>
                        {
                            if (idx >= 0 && idx < State.AvailableUsers.Count)
                            {
                                SetState(s => s.SelectedMaker = State.AvailableUsers[idx]);
                            }
                            else
                            {
                                SetState(s => s.SelectedMaker = null);
                            }
                        }),

                    // Made For Picker
                    new FormPickerField()
                        .Label("Made For")
                        .Title("Select Customer")
                        .ItemsSource(State.AvailableUsers.Select(u => u.Name).ToList())
                        .SelectedIndex(State.SelectedRecipient != null ?
                            State.AvailableUsers.FindIndex(u => u.Id == State.SelectedRecipient.Id) : -1)
                        .OnSelectedIndexChanged(idx =>
                        {
                            if (idx >= 0 && idx < State.AvailableUsers.Count)
                            {
                                SetState(s => s.SelectedRecipient = State.AvailableUsers[idx]);
                            }
                            else
                            {
                                SetState(s => s.SelectedRecipient = null);
                            }
                        }),

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
                        }),


                    // Rating
                    new FormSliderField()
                        .Label($"Rating: {State.Rating}/5")
                        .Minimum(0)
                        .Maximum(5)
                        .Value(State.Rating)
                        .OnValueChanged(val => SetState(s => s.Rating = (int)val)),

                    // Save Button
                    Button("Save Shot")
                        .IsEnabled(!State.IsLoading)
                        .OnClicked(async () => await SaveShotAsync())
                        .HeightRequest(50)
                        .Margin(0, 16)
                )
                .Padding(16)
            )
        );
    }

    VisualNode RenderDoseGauges()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = AppColors.Light.Primary;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;

        return Grid("Auto", "*, *",
            // In Gauge (left column)
            VStack(spacing: 4,
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
                Label("In")
                    .FontSize(14)
                    .TextColor(secondaryTextColor)
                    .HCenter()
            ).GridColumn(0),

            // Out Gauge (right column)
            VStack(spacing: 4,
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
                Label("Out")
                    .FontSize(14)
                    .TextColor(secondaryTextColor)
                    .HCenter()
            ).GridColumn(1)
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
            ).VCenter().HCenter().TranslationY(10)
        ).HeightRequest(160).WidthRequest(160).HCenter();
    }
}
