using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

class BeanDetailPageProps
{
    public int? BeanId { get; set; }
}

class BeanDetailPageState
{
    public int? BeanId { get; set; }
    public string Name { get; set; } = "";
    public string Roaster { get; set; } = "";
    public string Origin { get; set; } = "";
    public string Notes { get; set; } = "";

    public bool IsSaving { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }

    public RatingAggregateDto? RatingAggregate { get; set; }

    public List<BagSummaryDto> Bags { get; set; } = new();
    public bool IsLoadingBags { get; set; }

    public List<ShotRecordDto> Shots { get; set; } = new();
    public bool IsLoadingShots { get; set; }
    public bool HasMoreShots { get; set; }
    public int ShotPageIndex { get; set; }
    public string? ShotLoadError { get; set; }

    public List<RecipeDto> Recipes { get; set; } = new();
    public bool IsLoadingRecipes { get; set; }
    public bool IsRefreshingRecipes { get; set; }
    public string? RecipesLoadError { get; set; }

    public Dictionary<int, BaristaNotes.Core.Services.Grind.GrindTranslationResult> GrindTranslations { get; set; } = new();
    public bool IsTranslatingGrinds { get; set; }
    public int? ActiveGrinderId { get; set; }
    public string? ActiveGrinderName { get; set; }
}

partial class BeanDetailPage : Component<BeanDetailPageState, BeanDetailPageProps>
{
    [Inject] IBeanService _beanService;
    [Inject] IBagService _bagService;
    [Inject] IShotService _shotService;
    [Inject] IRecipeService _recipeService;
    [Inject] IFeedbackService _feedbackService;
    [Inject] BaristaNotes.Core.Data.Repositories.IEquipmentRepository _equipmentRepository;
    [Inject] BaristaNotes.Core.Data.Repositories.IGrinderProfileRepository _grinderProfileRepository;
    [Inject] BaristaNotes.Core.Services.Grind.IGrindTranslationService _grindTranslationService;
    [Inject] IDataChangeNotifier _dataChangeNotifier;

    const int PageSize = 20;

    CancellationTokenSource? _grindTranslationCts;

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

    protected override void OnWillUnmount()
    {
        try { _grindTranslationCts?.Cancel(); } catch { }
        _grindTranslationCts?.Dispose();
        _grindTranslationCts = null;
        base.OnWillUnmount();
    }

    // ============================================================
    // Loaders
    // ============================================================

    async Task LoadBeanAsync()
    {
        if (!State.BeanId.HasValue || State.BeanId.Value <= 0) return;

        try
        {
            var bean = await _beanService.GetBeanWithRatingsAsync(State.BeanId.Value);

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
                s.Notes = bean.Notes ?? "";
                s.RatingAggregate = bean.RatingAggregate;
                s.IsLoading = false;
            });

            _ = LoadBagsAsync();
            _ = LoadShotsAsync();
            _ = LoadRecipesAsync();
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

    async Task LoadBagsAsync()
    {
        if (!State.BeanId.HasValue || State.IsLoadingBags) return;

        SetState(s => s.IsLoadingBags = true);

        try
        {
            var bags = await _bagService.GetBagSummariesForBeanAsync(State.BeanId.Value, includeCompleted: true);

            SetState(s =>
            {
                s.Bags = bags;
                s.IsLoadingBags = false;
            });
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoadingBags = false;
                s.ErrorMessage = $"Failed to load bags: {ex.Message}";
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
                0,
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

    async Task LoadRecipesAsync()
    {
        if (!State.BeanId.HasValue || State.IsLoadingRecipes) return;

        SetState(s =>
        {
            s.IsLoadingRecipes = true;
            s.RecipesLoadError = null;
        });

        try
        {
            var recipes = await _recipeService.GetRecipesForBeanAsync(State.BeanId.Value);

            SetState(s =>
            {
                s.Recipes = recipes.ToList();
                s.IsLoadingRecipes = false;
            });

            _ = TranslateRecipeGrindsAsync();
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoadingRecipes = false;
                s.RecipesLoadError = $"Failed to load recipes: {ex.Message}";
            });
        }
    }

    async Task TranslateRecipeGrindsAsync()
    {
        _grindTranslationCts?.Cancel();
        _grindTranslationCts?.Dispose();
        var cts = new CancellationTokenSource();
        _grindTranslationCts = cts;
        var ct = cts.Token;

        try
        {
            SetState(s => s.IsTranslatingGrinds = true);

            var grinders = await _equipmentRepository.GetByTypeAsync(EquipmentType.Grinder);
            if (ct.IsCancellationRequested) return;
            var grinder = grinders.FirstOrDefault(g => g.IsActive && !g.IsDeleted);
            if (grinder == null)
            {
                if (ct.IsCancellationRequested) return;
                SetState(s =>
                {
                    s.IsTranslatingGrinds = false;
                    s.ActiveGrinderId = null;
                    s.ActiveGrinderName = null;
                    s.GrindTranslations = new();
                });
                return;
            }

            await _grinderProfileRepository.GetOrCreateForEquipmentAsync(grinder);
            if (ct.IsCancellationRequested) return;

            var translations = new Dictionary<int, BaristaNotes.Core.Services.Grind.GrindTranslationResult>();
            foreach (var recipe in State.Recipes)
            {
                if (ct.IsCancellationRequested) return;
                if (string.IsNullOrWhiteSpace(recipe.GrindHint)) continue;
                try
                {
                    var result = await _grindTranslationService.TranslateAsync(
                        new BaristaNotes.Core.Services.Grind.GrindTranslationRequest(
                            EquipmentId: grinder.Id,
                            GrinderModel: grinder.Name,
                            GrindHint: recipe.GrindHint!,
                            Method: recipe.BrewMethod,
                            BeanId: State.BeanId));
                    translations[recipe.Id] = result;
                }
                catch { }
            }

            if (ct.IsCancellationRequested) return;
            SetState(s =>
            {
                s.IsTranslatingGrinds = false;
                s.ActiveGrinderId = grinder.Id;
                s.ActiveGrinderName = grinder.Name;
                s.GrindTranslations = translations;
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            if (!ct.IsCancellationRequested)
            {
                SetState(s => s.IsTranslatingGrinds = false);
            }
        }
    }

    async Task RefreshRecipesAsync()
    {
        if (!State.BeanId.HasValue || State.IsRefreshingRecipes) return;

        SetState(s =>
        {
            s.IsRefreshingRecipes = true;
            s.RecipesLoadError = null;
        });

        try
        {
            var recipes = await _beanService.RefreshRecipesAsync(State.BeanId.Value);
            SetState(s =>
            {
                s.Recipes = recipes.ToList();
                s.IsRefreshingRecipes = false;
            });

            _ = TranslateRecipeGrindsAsync();

            if (recipes.Count == 0)
            {
                await _feedbackService.ShowInfoAsync("No recipes found for this bean yet.");
            }
            else
            {
                await _feedbackService.ShowSuccessAsync($"Refreshed {recipes.Count} recipe(s).");
            }
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsRefreshingRecipes = false;
                s.RecipesLoadError = $"Failed to refresh recipes: {ex.Message}";
            });
            await _feedbackService.ShowErrorAsync("Recipe refresh failed.");
        }
    }

    // ============================================================
    // Actions
    // ============================================================

    bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(State.Name))
        {
            SetState(s => s.ErrorMessage = "Bean name is required");
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
                var updateDto = new UpdateBeanDto
                {
                    Name = State.Name,
                    Roaster = string.IsNullOrWhiteSpace(State.Roaster) ? null : State.Roaster,
                    Origin = string.IsNullOrWhiteSpace(State.Origin) ? null : State.Origin,
                    Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                };

                await _beanService.UpdateBeanAsync(State.BeanId.Value, updateDto);
                _dataChangeNotifier.NotifyDataChanged(DataChangeType.BeanUpdated, State.BeanId.Value);
                await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' updated");
            }
            else
            {
                var createDto = new CreateBeanDto
                {
                    Name = State.Name,
                    Roaster = string.IsNullOrWhiteSpace(State.Roaster) ? null : State.Roaster,
                    Origin = string.IsNullOrWhiteSpace(State.Origin) ? null : State.Origin,
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

                _dataChangeNotifier.NotifyDataChanged(DataChangeType.BeanCreated, result.Data);
                await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' created");
            }

            await MauiControls.Shell.Current.GoToAsync("..");
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

        var popup = new SimpleActionPopup
        {
            Title = "Delete Bean?",
            Text = $"Are you sure you want to delete '{State.Name}'? This action cannot be undone.",
            ActionButtonText = "Delete",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                await _beanService.DeleteBeanAsync(State.BeanId!.Value);
                _dataChangeNotifier.NotifyDataChanged(DataChangeType.BeanUpdated, State.BeanId!.Value);
                await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' deleted");
                await IPopupService.Current.PopAsync();
                await MauiControls.Shell.Current.GoToAsync("..");
            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    async Task CancelAsync()
    {
        await MauiControls.Shell.Current.GoToAsync("..");
    }

    async void NavigateToShot(int shotId)
    {
        await MauiControls.Shell.Current.GoToAsync<ShotLoggingGridPageProps>("shot-logging", props =>
        {
            props.ShotId = shotId;
        });
    }

    async void NavigateToBag(int bagId)
    {
        await MauiControls.Shell.Current.GoToAsync<BagDetailPageProps>("bag-detail", props =>
        {
            props.BagId = bagId;
            props.BeanId = State.BeanId ?? 0;
            props.BeanName = State.Name;
        });
    }

    async void NavigateToAddBag()
    {
        if (!State.BeanId.HasValue) return;

        await MauiControls.Shell.Current.GoToAsync<BagDetailPageProps>("bag-detail", props =>
        {
            props.BagId = null;
            props.BeanId = State.BeanId.Value;
            props.BeanName = State.Name;
        });
    }

    void OnPageAppearing()
    {
        if (State.BeanId.HasValue && State.BeanId.Value > 0)
        {
            _ = LoadBagsAsync();
            _ = LoadRecipesAsync();
        }
    }

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return ContentPage("Bean",
            Grid(rows: "Auto,*,Auto", columns: "*",
                HeaderTile().GridRow(0),
                RenderBody().GridRow(1),
                BottomNavRow().GridRow(2)
            )
            .RowSpacing(1)
            .BackgroundColor(DividerColor())
            .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        )
        .Set(MauiControls.Shell.NavBarIsVisibleProperty, false)
        .Set(MauiControls.Shell.TabBarIsVisibleProperty, false)
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
        .OnAppearing(OnPageAppearing);
    }

    // ------------------------------------------------------------
    // Theme helpers
    // ------------------------------------------------------------

    static bool IsLight() => Application.Current?.RequestedTheme != AppTheme.Dark;
    static Color SurfaceColor() => IsLight() ? AppColors.Light.Surface : AppColors.Dark.Surface;
    static Color SurfaceVariantColor() => IsLight() ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
    static Color TextPrimary() => IsLight() ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
    static Color TextSecondary() => IsLight() ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
    static Color AccentColor() => IsLight() ? AppColors.Light.Primary : AppColors.Dark.Primary;
    static Color DividerColor() => IsLight() ? AppColors.Light.Outline : AppColors.Dark.Outline;
    static Color ErrorColor() => AppColors.Error;

    // ------------------------------------------------------------
    // Header tile
    // ------------------------------------------------------------

    VisualNode HeaderTile()
    {
        var isEditMode = State.BeanId.HasValue && State.BeanId.Value > 0;
        var label = isEditMode ? "EDIT BEAN" : "NEW BEAN";
        var title = isEditMode
            ? (string.IsNullOrEmpty(State.Name) ? "Loading…" : State.Name)
            : "Add bean";

        var len = title.Length;
        double valueFontSize = len switch
        {
            <= 12 => 28,
            <= 20 => 22,
            <= 28 => 18,
            _ => 16
        };

        return Border(
            Grid(rows: "Auto,*", columns: "*",
                Label(label)
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Label(title)
                    .FontSize(valueFontSize)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.WordWrap)
                    .MaxLines(2)
                    .VEnd()
                    .GridRow(1)
            )
            .Padding(16, 56, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(120);
    }

    // ------------------------------------------------------------
    // Body
    // ------------------------------------------------------------

    VisualNode RenderBody()
    {
        if (State.IsLoading)
        {
            return Border(
                ActivityIndicator()
                    .IsRunning(true)
                    .VCenter()
                    .HCenter()
            )
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle());
        }

        var isEditMode = State.BeanId.HasValue && State.BeanId.Value > 0;

        var sections = new List<VisualNode>
        {
            EntryTile("NAME", State.Name, "Bean name (required)",
                text => SetState(s => s.Name = text), bigFont: true),
            EntryTile("ROASTER", State.Roaster, "Roaster name",
                text => SetState(s => s.Roaster = text)),
            EntryTile("ORIGIN", State.Origin, "Country or region",
                text => SetState(s => s.Origin = text)),
            EditorTile("NOTES", State.Notes, "Tasting notes, processing method…",
                text => SetState(s => s.Notes = text))
        };

        if (isEditMode)
        {
            sections.Add(RatingsSection());
            sections.Add(RecipesSection());
            sections.Add(BagsSection());
            sections.Add(ShotHistorySection());
        }

        if (State.ErrorMessage != null)
        {
            sections.Add(ErrorTile(State.ErrorMessage));
        }

        sections.Add(
            Border()
                .BackgroundColor(SurfaceColor())
                .StrokeThickness(0)
                .StrokeShape(new Rectangle())
                .MinimumHeightRequest(24)
        );

        var rowsSpec = string.Join(",", Enumerable.Repeat("Auto", sections.Count)) + ",*";
        var children = sections.Select((node, i) => node.GridRow(i)).ToList();
        children.Add(Border()
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle())
            .MinimumHeightRequest(16)
            .VerticalOptions(LayoutOptions.Fill)
            .GridRow(sections.Count));

        return ScrollView(
            Grid(rowsSpec, "*", children.ToArray())
                .RowSpacing(1)
                .BackgroundColor(DividerColor())
        )
        .BackgroundColor(SurfaceColor());
    }

    // ------------------------------------------------------------
    // Form field tiles
    // ------------------------------------------------------------

    VisualNode EntryTile(string label, string value, string placeholder, Action<string> onChanged, bool bigFont = false)
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*",
                Label(label)
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Entry()
                    .Text(value)
                    .Placeholder(placeholder)
                    .PlaceholderColor(TextSecondary().WithAlpha(0.5f))
                    .TextColor(TextPrimary())
                    .FontSize(bigFont ? 22 : 18)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .BackgroundColor(Colors.Transparent)
                    .OnTextChanged(text => onChanged(text))
                    .GridRow(1)
            )
            .Padding(16, 14, 16, 10)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(bigFont ? 100 : 90);
    }

    VisualNode EditorTile(string label, string value, string placeholder, Action<string> onChanged)
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*",
                Label(label)
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Editor()
                    .Text(value)
                    .Placeholder(placeholder)
                    .PlaceholderColor(TextSecondary().WithAlpha(0.5f))
                    .TextColor(TextPrimary())
                    .FontSize(16)
                    .BackgroundColor(Colors.Transparent)
                    .AutoSize(EditorAutoSizeOption.TextChanges)
                    .HeightRequest(100)
                    .OnTextChanged((s, e) => onChanged(e.NewTextValue))
                    .GridRow(1)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(150);
    }

    // ------------------------------------------------------------
    // Read-only sections (edit mode)
    // ------------------------------------------------------------

    VisualNode RatingsSection()
    {
        var hasRatings = State.RatingAggregate != null && State.RatingAggregate.HasRatings;

        return Border(
            VStack(spacing: 10,
                Label("RATINGS")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary()),
                hasRatings
                    ? (VisualNode)new RatingDisplayComponent().RatingAggregate(State.RatingAggregate)
                    : Label("No ratings yet")
                        .FontSize(16)
                        .TextColor(TextSecondary())
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(80);
    }

    VisualNode RecipesSection()
    {
        var actionLabel = State.IsRefreshingRecipes
            ? "…"
            : (State.Recipes.Count == 0 ? "FIND" : "REFRESH");

        return Border(
            VStack(spacing: 10,
                Grid(rows: "Auto", columns: "*,Auto",
                    Label("RECIPES")
                        .FontSize(10)
                        .CharacterSpacing(2)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextSecondary())
                        .VCenter()
                        .GridColumn(0),
                    State.IsRefreshingRecipes
                        ? (VisualNode)ActivityIndicator()
                            .IsRunning(true)
                            .HeightRequest(20)
                            .WidthRequest(20)
                            .VCenter()
                            .GridColumn(1)
                        : MiniActionChip(actionLabel,
                            async () => await RefreshRecipesAsync())
                            .GridColumn(1)
                ),

                State.RecipesLoadError != null
                    ? (VisualNode)Label(State.RecipesLoadError)
                        .FontSize(13)
                        .TextColor(ErrorColor())
                    : null,

                State.IsLoadingRecipes && State.Recipes.Count == 0
                    ? (VisualNode)ActivityIndicator().IsRunning(true).HCenter()
                    : null,

                !State.IsLoadingRecipes && !State.IsRefreshingRecipes && State.Recipes.Count == 0 && State.RecipesLoadError == null
                    ? (VisualNode)VStack(spacing: 4,
                        Label("No recipes yet.")
                            .FontSize(14)
                            .TextColor(TextPrimary()),
                        Label("Tap REFRESH to look up brew guides.")
                            .FontSize(12)
                            .TextColor(TextSecondary())
                    )
                    : null,

                State.Recipes.Count > 0
                    ? (VisualNode)VStack(spacing: 8,
                        State.Recipes.Select(RenderRecipeRow).ToArray()
                    )
                    : null
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    VisualNode RenderRecipeRow(RecipeDto recipe)
    {
        string sourceLabel = recipe.Source switch
        {
            RecipeSource.RoasterSite => "ROASTER",
            RecipeSource.AIGenerated => "AI",
            RecipeSource.Manual => "CUSTOM",
            _ => recipe.Source.ToString().ToUpperInvariant()
        };

        var paramsRow = BuildParamsRow(recipe);

        return Border(
            VStack(spacing: 6,
                Grid(rows: "Auto", columns: "*,Auto,Auto",
                    Label(recipe.BrewMethod.DisplayName())
                        .FontSize(16)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextPrimary())
                        .GridColumn(0),
                    Label(sourceLabel)
                        .FontSize(9)
                        .CharacterSpacing(1.5)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextSecondary())
                        .VCenter()
                        .Padding(6, 2)
                        .GridColumn(1),
                    recipe.IsEditedByUser
                        ? (VisualNode)Label("EDITED")
                            .FontSize(9)
                            .CharacterSpacing(1.5)
                            .FontAttributes(MauiControls.FontAttributes.Bold)
                            .TextColor(TextSecondary())
                            .VCenter()
                            .Padding(6, 2)
                            .GridColumn(2)
                        : null
                )
                .ColumnSpacing(6),

                !string.IsNullOrWhiteSpace(recipe.Title)
                    ? (VisualNode)Label(recipe.Title!)
                        .FontSize(12)
                        .TextColor(TextSecondary())
                    : null,

                paramsRow,

                !string.IsNullOrWhiteSpace(recipe.GrindHint)
                    ? (VisualNode)Label($"Grind: {recipe.GrindHint}")
                        .FontSize(12)
                        .TextColor(TextSecondary())
                    : null,

                !string.IsNullOrWhiteSpace(recipe.GrindHint) && State.ActiveGrinderId.HasValue
                    ? (VisualNode)GrindTranslationChip.Render(
                        State.GrindTranslations.TryGetValue(recipe.Id, out var t) ? t : null,
                        loading: State.IsTranslatingGrinds
                                 && !State.GrindTranslations.ContainsKey(recipe.Id))
                    : null,

                !string.IsNullOrWhiteSpace(recipe.Notes)
                    ? (VisualNode)Label(recipe.Notes!)
                        .FontSize(12)
                        .TextColor(TextSecondary())
                        .LineBreakMode(LineBreakMode.WordWrap)
                    : null,

                !string.IsNullOrWhiteSpace(recipe.SourceUrl)
                    ? (VisualNode)Label("View source →")
                        .FontSize(12)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(AccentColor())
                        .OnTapped(async () =>
                        {
                            try
                            {
                                await Microsoft.Maui.ApplicationModel.Browser.OpenAsync(
                                    recipe.SourceUrl!,
                                    Microsoft.Maui.ApplicationModel.BrowserLaunchMode.SystemPreferred);
                            }
                            catch { }
                        })
                    : null
            )
            .Padding(12)
        )
        .BackgroundColor(SurfaceVariantColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    static VisualNode BuildParamsRow(RecipeDto recipe)
    {
        var parts = new List<string>();
        if (recipe.DoseIn.HasValue) parts.Add($"{recipe.DoseIn:0.#}g in");
        if (recipe.OutputAmount.HasValue) parts.Add($"{recipe.OutputAmount:0.#}g out");
        if (recipe.TotalTimeSeconds.HasValue)
        {
            var t = recipe.TotalTimeSeconds.Value;
            if (t >= 60)
            {
                var mm = (int)(t / 60);
                var ss = (int)(t % 60);
                parts.Add($"{mm}:{ss:D2}");
            }
            else
            {
                parts.Add($"{t:0}s");
            }
        }
        if (recipe.BrewTempC.HasValue) parts.Add($"{recipe.BrewTempC:0.#}°C");

        if (parts.Count == 0)
        {
            return Label("No parameters captured.")
                .FontSize(12)
                .TextColor(TextSecondary());
        }

        return Label(string.Join(" · ", parts))
            .FontSize(13)
            .FontAttributes(MauiControls.FontAttributes.Bold)
            .TextColor(TextPrimary());
    }

    VisualNode BagsSection()
    {
        return Border(
            VStack(spacing: 10,
                Grid(rows: "Auto", columns: "*,Auto",
                    Label("BAGS")
                        .FontSize(10)
                        .CharacterSpacing(2)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextSecondary())
                        .VCenter()
                        .GridColumn(0),
                    MiniActionChip("+ BAG", NavigateToAddBag)
                        .GridColumn(1)
                ),

                State.IsLoadingBags
                    ? (VisualNode)ActivityIndicator().IsRunning(true).HCenter()
                    : null,

                !State.IsLoadingBags && State.Bags.Count == 0
                    ? (VisualNode)Label("No bags added yet")
                        .FontSize(14)
                        .TextColor(TextSecondary())
                    : null,

                State.Bags.Count > 0
                    ? (VisualNode)VStack(spacing: 8,
                        State.Bags.Select(RenderBagRow).ToArray()
                    )
                    : null
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    VisualNode RenderBagRow(BagSummaryDto bag)
    {
        return Border(
            VStack(spacing: 6,
                Grid(rows: "Auto", columns: "*,Auto",
                    Label($"Roasted {bag.FormattedRoastDate}")
                        .FontSize(16)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextPrimary())
                        .GridColumn(0),
                    Label(bag.StatusBadge.ToUpperInvariant())
                        .FontSize(9)
                        .CharacterSpacing(1.5)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextSecondary())
                        .VCenter()
                        .Padding(6, 2)
                        .GridColumn(1)
                ),

                bag.Notes != null
                    ? (VisualNode)Label(bag.Notes)
                        .FontSize(13)
                        .TextColor(TextSecondary())
                        .LineBreakMode(LineBreakMode.WordWrap)
                    : null,

                HStack(spacing: 16,
                    Label($"{bag.ShotCount} shots")
                        .FontSize(12)
                        .TextColor(TextSecondary()),
                    bag.AverageRating.HasValue
                        ? (VisualNode)HStack(spacing: 4,
                            Label(bag.FormattedRating)
                                .FontSize(12)
                                .FontAttributes(MauiControls.FontAttributes.Bold)
                                .TextColor(TextPrimary()),
                            Label(AppIcons.GetRatingIcon((int)Math.Round(bag.AverageRating.Value)))
                                .FontFamily(MaterialSymbolsFont.FontFamily)
                                .FontSize(14)
                                .TextColor(TextPrimary())
                        )
                        : Label("no ratings")
                            .FontSize(12)
                            .TextColor(TextSecondary())
                )
            )
            .Padding(12)
        )
        .BackgroundColor(SurfaceVariantColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .OnTapped(() => NavigateToBag(bag.Id));
    }

    VisualNode ShotHistorySection()
    {
        return Border(
            VStack(spacing: 10,
                Label("SHOT HISTORY")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary()),

                State.ShotLoadError != null
                    ? (VisualNode)VStack(spacing: 6,
                        Label(State.ShotLoadError)
                            .FontSize(13)
                            .TextColor(ErrorColor()),
                        MiniActionChip("RETRY", async () => await LoadShotsAsync())
                    )
                    : null,

                State.IsLoadingShots && State.Shots.Count == 0
                    ? (VisualNode)ActivityIndicator().IsRunning(true).HCenter()
                    : null,

                !State.IsLoadingShots && State.Shots.Count == 0 && State.ShotLoadError == null
                    ? (VisualNode)Label("No shots recorded with this bean yet")
                        .FontSize(14)
                        .TextColor(TextSecondary())
                    : null,

                State.Shots.Count > 0
                    ? (VisualNode)CollectionView()
                        .ItemsSource(State.Shots, RenderShotItem)
                        .RemainingItemsThreshold(5)
                        .OnRemainingItemsThresholdReached(() =>
                        {
                            if (State.HasMoreShots && !State.IsLoadingShots)
                            {
                                _ = LoadMoreShotsAsync();
                            }
                        })
                        .HeightRequest(420)
                    : null,

                State.IsLoadingShots && State.Shots.Count > 0
                    ? (VisualNode)ActivityIndicator().IsRunning(true).HCenter()
                    : null
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle());
    }

    VisualNode RenderShotItem(ShotRecordDto shot)
    {
        return Border(
            new ShotRecordCard().Shot(shot)
        )
        .StrokeThickness(0)
        .BackgroundColor(SurfaceVariantColor())
        .Margin(0, 4)
        .OnTapped(() => NavigateToShot(shot.Id));
    }

    // ------------------------------------------------------------
    // Mini action chip
    // ------------------------------------------------------------

    VisualNode MiniActionChip(string label, Action onTap)
    {
        return Border(
            Label(label)
                .FontSize(11)
                .CharacterSpacing(1.5)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(SurfaceColor())
                .HCenter()
                .VCenter()
        )
        .BackgroundColor(TextPrimary())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .Padding(12, 8)
        .MinimumHeightRequest(32)
        .OnTapped(onTap);
    }

    VisualNode ErrorTile(string message)
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*",
                Label("ERROR")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(SurfaceColor().WithAlpha(0.8f))
                    .GridRow(0),
                Label(message)
                    .FontSize(16)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(SurfaceColor())
                    .GridRow(1)
            )
            .Padding(16, 12, 16, 12)
        )
        .BackgroundColor(ErrorColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(60);
    }

    // ------------------------------------------------------------
    // Bottom nav row
    // ------------------------------------------------------------

    VisualNode BottomNavRow()
    {
        var isEditMode = State.BeanId.HasValue && State.BeanId.Value > 0;

        if (isEditMode)
        {
            return Grid(rows: "Auto", columns: "*,*,*",
                ActionTile("CANCEL", inverted: false,
                    onTap: async () => await CancelAsync()).GridColumn(0),
                ActionTile("DELETE", inverted: false, danger: true,
                    onTap: async () => await DeleteBeanAsync()).GridColumn(1),
                ActionTile(State.IsSaving ? "SAVING…" : "SAVE", inverted: true,
                    onTap: async () => { if (!State.IsSaving) await SaveBeanAsync(); }).GridColumn(2)
            )
            .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
            .ColumnSpacing(1)
            .BackgroundColor(DividerColor());
        }

        return Grid(rows: "Auto", columns: "*,*",
            ActionTile("CANCEL", inverted: false,
                onTap: async () => await CancelAsync()).GridColumn(0),
            ActionTile(State.IsSaving ? "SAVING…" : "ADD", inverted: true,
                onTap: async () => { if (!State.IsSaving) await SaveBeanAsync(); }).GridColumn(1)
        )
        .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());
    }

    VisualNode ActionTile(string label, bool inverted, Action onTap, bool danger = false)
    {
        Color bg;
        Color fg;
        if (danger)
        {
            bg = ErrorColor();
            fg = SurfaceColor();
        }
        else if (inverted)
        {
            bg = TextPrimary();
            fg = SurfaceColor();
        }
        else
        {
            bg = SurfaceColor();
            fg = TextPrimary();
        }

        return Border(
            Label(label)
                .FontSize(18)
                .FontFamily("ManropeSemibold")
                .CharacterSpacing(1)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(fg)
                .HCenter()
                .VCenter()
        )
        .BackgroundColor(bg)
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(72)
        .Padding(8, 18, 8, 30)
        .OnTapped(onTap);
    }
}
