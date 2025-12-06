using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Components;
using BaristaNotes.Components.FormFields;
using MauiReactor;

namespace BaristaNotes.Pages;

class BeanDetailPageProps
{
    public int? BeanId { get; set; }
}

class BeanDetailPageState
{
    // Form fields
    public int? BeanId { get; set; }
    public string Name { get; set; } = "";
    public string Roaster { get; set; } = "";
    public string Origin { get; set; } = "";
    public bool TrackRoastDate { get; set; }
    public DateTime RoastDate { get; set; } = DateTime.Now;
    public string Notes { get; set; } = "";

    // Form state
    public bool IsSaving { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }

    // Shot history
    public List<ShotRecordDto> Shots { get; set; } = new();
    public bool IsLoadingShots { get; set; }
    public bool HasMoreShots { get; set; }
    public int ShotPageIndex { get; set; }
    public string? ShotLoadError { get; set; }
}

partial class BeanDetailPage : Component<BeanDetailPageState, BeanDetailPageProps>
{
    [Inject] IBeanService _beanService;
    [Inject] IShotService _shotService;
    [Inject] IFeedbackService _feedbackService;

    const int PageSize = 20;

    protected override void OnMounted()
    {
        base.OnMounted();

        if (Props.BeanId.HasValue && Props.BeanId.Value > 0)
        {
            SetState(s =>
            {
                s.BeanId = Props.BeanId;
                s.IsLoading = true;
            });
            _ = LoadBeanAsync();
        }
    }

    async Task LoadBeanAsync()
    {
        if (!State.BeanId.HasValue || State.BeanId.Value <= 0) return;

        try
        {
            var bean = await _beanService.GetBeanByIdAsync(State.BeanId.Value);

            if (bean == null)
            {
                SetState(s =>
                {
                    s.IsLoading = false;
                    s.ErrorMessage = "Bean not found";
                });
                return;
            }

            SetState(s =>
            {
                s.Name = bean.Name;
                s.Roaster = bean.Roaster ?? "";
                s.Origin = bean.Origin ?? "";
                s.TrackRoastDate = bean.RoastDate.HasValue;
                s.RoastDate = bean.RoastDate?.DateTime ?? DateTime.Now;
                s.Notes = bean.Notes ?? "";
                s.IsLoading = false;
            });

            // Load shot history after bean loads
            _ = LoadShotsAsync();
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = $"Failed to load bean: {ex.Message}";
            });
        }
    }

    async Task LoadShotsAsync()
    {
        if (!State.BeanId.HasValue || State.IsLoadingShots) return;

        SetState(s =>
        {
            s.IsLoadingShots = true;
            s.ShotLoadError = null;
        });

        try
        {
            var result = await _shotService.GetShotHistoryByBeanAsync(
                State.BeanId.Value,
                0, // First page
                PageSize);

            SetState(s =>
            {
                s.Shots = result.Items.ToList();
                s.HasMoreShots = result.HasNextPage;
                s.ShotPageIndex = 1;
                s.IsLoadingShots = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoadingShots = false;
                s.ShotLoadError = $"Failed to load shots: {ex.Message}";
            });
        }
    }

    async Task LoadMoreShotsAsync()
    {
        if (!State.BeanId.HasValue || State.IsLoadingShots || !State.HasMoreShots) return;

        SetState(s => s.IsLoadingShots = true);

        try
        {
            var result = await _shotService.GetShotHistoryByBeanAsync(
                State.BeanId.Value,
                State.ShotPageIndex,
                PageSize);

            SetState(s =>
            {
                s.Shots = new List<ShotRecordDto>(s.Shots.Concat(result.Items));
                s.HasMoreShots = result.HasNextPage;
                s.ShotPageIndex++;
                s.IsLoadingShots = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoadingShots = false;
                s.ShotLoadError = $"Failed to load more shots: {ex.Message}";
            });
        }
    }

    bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(State.Name))
        {
            SetState(s => s.ErrorMessage = "Bean name is required");
            return false;
        }

        if (State.TrackRoastDate && State.RoastDate > DateTime.Now)
        {
            SetState(s => s.ErrorMessage = "Roast date cannot be in the future");
            return false;
        }

        SetState(s => s.ErrorMessage = null);
        return true;
    }

    async Task SaveBeanAsync()
    {
        if (!ValidateForm()) return;

        SetState(s =>
        {
            s.IsSaving = true;
            s.ErrorMessage = null;
        });

        try
        {
            if (State.BeanId.HasValue && State.BeanId.Value > 0)
            {
                // Update existing
                var updateDto = new UpdateBeanDto
                {
                    Name = State.Name,
                    Roaster = string.IsNullOrWhiteSpace(State.Roaster) ? null : State.Roaster,
                    Origin = string.IsNullOrWhiteSpace(State.Origin) ? null : State.Origin,
                    RoastDate = State.TrackRoastDate ? new DateTimeOffset(State.RoastDate) : null,
                    Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                };

                await _beanService.UpdateBeanAsync(State.BeanId.Value, updateDto);
                await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' updated");
            }
            else
            {
                // Create new
                var createDto = new CreateBeanDto
                {
                    Name = State.Name,
                    Roaster = string.IsNullOrWhiteSpace(State.Roaster) ? null : State.Roaster,
                    Origin = string.IsNullOrWhiteSpace(State.Origin) ? null : State.Origin,
                    RoastDate = State.TrackRoastDate ? new DateTimeOffset(State.RoastDate) : null,
                    Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                };

                var result = await _beanService.CreateBeanAsync(createDto);
                if (!result.Success)
                {
                    SetState(s =>
                    {
                        s.IsSaving = false;
                        s.ErrorMessage = result.ErrorMessage ?? "Failed to create bean";
                    });
                    return;
                }

                await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' created");
            }

            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsSaving = false;
                s.ErrorMessage = $"Failed to save: {ex.Message}";
            });
        }
    }

    async Task DeleteBeanAsync()
    {
        if (!State.BeanId.HasValue || State.BeanId.Value <= 0) return;

        if (Application.Current?.MainPage == null) return;

        var confirmed = await Application.Current.MainPage.DisplayAlert(
            "Delete Bean",
            $"Are you sure you want to delete '{State.Name}'? This action cannot be undone.",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        try
        {
            await _beanService.DeleteBeanAsync(State.BeanId.Value);
            await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' deleted");
            await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            SetState(s => s.ErrorMessage = $"Failed to delete: {ex.Message}");
        }
    }

    async void NavigateToShot(int shotId)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<ShotLoggingPageProps>("shot-logging", props =>
        {
            props.ShotId = shotId;
        });
    }

    public override VisualNode Render()
    {
        var isEditMode = State.BeanId.HasValue && State.BeanId.Value > 0;
        var title = isEditMode
            ? (string.IsNullOrEmpty(State.Name) ? "Edit Bean" : $"Edit {State.Name}")
            : "Add Bean";

        if (State.IsLoading)
        {
            return ContentPage(
                VStack(
                    ActivityIndicator().IsRunning(true)
                )
                .VCenter()
                .HCenter()
            ).Title(title);
        }

        return ContentPage(
            ScrollView(
                VStack(spacing: 16,
                    // Form section
                    RenderForm(),

                    // Shot history section (edit mode only)
                    isEditMode ? RenderShotHistory() : null
                )
                .Padding(16)
            )
        ).Title(title);
    }

    VisualNode RenderForm()
    {
        var isEditMode = State.BeanId.HasValue && State.BeanId.Value > 0;
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var backgroundColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;

        return VStack(spacing: 16,
            // Name field
            new FormEntryField()
                .Label("Name *")
                .Placeholder("Bean name (required)")
                .Text(State.Name)
                .OnTextChanged(text => SetState(s => s.Name = text)),

            // Roaster field
            new FormEntryField()
                .Label("Roaster")
                .Placeholder("Roaster name")
                .Text(State.Roaster)
                .OnTextChanged(text => SetState(s => s.Roaster = text)),

            // Origin field
            new FormEntryField()
                .Label("Origin")
                .Placeholder("Country or region of origin")
                .Text(State.Origin)
                .OnTextChanged(text => SetState(s => s.Origin = text)),

            // Roast date toggle
            HStack(spacing: 8,
                Label("Track Roast Date").ThemeKey(ThemeKeys.SecondaryText).VCenter(),
                Switch()
                    .IsToggled(State.TrackRoastDate)
                    .OnToggled(args => SetState(s => s.TrackRoastDate = args.Value))
            ),

            // Date picker (conditional)
            State.TrackRoastDate
                ? DatePicker()
                    .MaximumDate(DateTime.Now)
                    .Date(State.RoastDate)
                    .OnDateSelected(date => SetState(s => s.RoastDate = date ?? DateTime.Now))
                : null,

            // Notes field
            new FormEditorField()
                .Label("Notes")
                .Placeholder("Tasting notes, processing method, etc.")
                .Text(State.Notes)
                .HeightRequest(100)
                .OnTextChanged(text => SetState(s => s.Notes = text)),

            // Error message
            State.ErrorMessage != null
                ? Border(
                    Label(State.ErrorMessage).TextColor(Colors.Red).Padding(12)
                )
                .BackgroundColor(Colors.Red.WithAlpha(0.1f))
                .StrokeThickness(1)
                .Stroke(Colors.Red)
                : null,

            // Action buttons
            VStack(spacing: 12,
                Button(State.IsSaving ? "Saving..." : (isEditMode ? "Save Changes" : "Create Bean"))
                    .OnClicked(async () => await SaveBeanAsync())
                    .IsEnabled(!State.IsSaving),

                Button("Cancel")
                    .OnClicked(async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync(".."))
                    .IsEnabled(!State.IsSaving)
                    .BackgroundColor(Colors.Gray),

                isEditMode
                    ? Button("Delete Bean")
                        .OnClicked(async () => await DeleteBeanAsync())
                        .IsEnabled(!State.IsSaving)
                        .BackgroundColor(Colors.Red)
                    : null
            )
        );
    }

    VisualNode RenderShotHistory()
    {
        return VStack(spacing: 12,
            // Section header
            BoxView().HeightRequest(1).Color(Colors.Gray.WithAlpha(0.3f)),

            Label("Shot History")
                .ThemeKey(ThemeKeys.SubHeadline),

            // Error state
            State.ShotLoadError != null
                ? VStack(spacing: 8,
                    Label(State.ShotLoadError).TextColor(Colors.Red),
                    Button("Retry").OnClicked(async () => await LoadShotsAsync())
                )
                : null,

            // Loading state (initial)
            State.IsLoadingShots && State.Shots.Count == 0
                ? ActivityIndicator().IsRunning(true)
                : null,

            // Empty state
            !State.IsLoadingShots && State.Shots.Count == 0 && State.ShotLoadError == null
                ? RenderEmptyShots()
                : null,

            // Shot list
            State.Shots.Count > 0
                ? CollectionView()
                    .ItemsSource(State.Shots, RenderShotItem)
                    .RemainingItemsThreshold(5)
                    .OnRemainingItemsThresholdReached(() =>
                    {
                        if (State.HasMoreShots && !State.IsLoadingShots)
                        {
                            _ = LoadMoreShotsAsync();
                        }
                    })
                    .HeightRequest(400) // Constrain height within ScrollView
                : null,

            // Loading more indicator
            State.IsLoadingShots && State.Shots.Count > 0
                ? ActivityIndicator().IsRunning(true).HCenter()
                : null
        );
    }

    VisualNode RenderEmptyShots()
    {
        return VStack(spacing: 12,
            Label("📋").FontSize(48).HCenter(),
            Label("No shots recorded with this bean yet")
                .ThemeKey(ThemeKeys.SecondaryText)
                .HCenter()
        )
        .Padding(24);
    }

    VisualNode RenderShotItem(ShotRecordDto shot)
    {
        return Border(
            new ShotRecordCard().Shot(shot)
        )
        .StrokeThickness(0)
        .OnTapped(() => NavigateToShot(shot.Id))
        .Margin(0, 4);
    }
}
