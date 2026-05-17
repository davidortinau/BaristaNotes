using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Components;
using BaristaNotes.Components.FormFields;
using Fonts;
using MauiReactor;
using UXDivers.Popups.Services;
using UXDivers.Popups.Maui.Controls;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

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
    public string Notes { get; set; } = "";

    // Form state
    public bool IsSaving { get; set; }
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }

    // Rating aggregate
    public RatingAggregateDto? RatingAggregate { get; set; }

    // Bags section
    public List<BagSummaryDto> Bags { get; set; } = new();
    public bool IsLoadingBags { get; set; }

    // Shot history
    public List<ShotRecordDto> Shots { get; set; } = new();
    public bool IsLoadingShots { get; set; }
    public bool HasMoreShots { get; set; }
    public int ShotPageIndex { get; set; }
    public string? ShotLoadError { get; set; }

    // Recipes
    public List<RecipeDto> Recipes { get; set; } = new();
    public bool IsLoadingRecipes { get; set; }
    public bool IsRefreshingRecipes { get; set; }
    public string? RecipesLoadError { get; set; }

    // Grind translations (keyed by recipe.Id). Populated after recipes load
    // and the user has an active grinder.
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

            // Load bags, shot history, and recipes after bean loads
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

            // Pick the user's first active grinder (scope keeps this simple — a
            // proper "active equipment" picker is a separate feature).
            var grinders = await _equipmentRepository.GetByTypeAsync(
                BaristaNotes.Core.Models.Enums.EquipmentType.Grinder);
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

            // Ensure a profile exists so the deterministic path has anchors
            // for known grinders (e.g. DF64 seed data).
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
                catch
                {
                    // Per-recipe failure is non-fatal — just skip.
                }
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
            // Page unmounted or new translation kicked off - nothing to do.
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
                // Update existing
                var updateDto = new UpdateBeanDto
                {
                    Name = State.Name,
                    Roaster = string.IsNullOrWhiteSpace(State.Roaster) ? null : State.Roaster,
                    Origin = string.IsNullOrWhiteSpace(State.Origin) ? null : State.Origin,
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

        var popup = new SimpleActionPopup
        {
            Title = $"Delete Bean?",
            Text = $"Are you sure you want to delete '{State.Name}'? This action cannot be undone.",
            ActionButtonText = "Delete",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                await _beanService.DeleteBeanAsync(State.BeanId.Value);
                await _feedbackService.ShowSuccessAsync($"Bean '{State.Name}' deleted");
                await IPopupService.Current.PopAsync();
                await MauiControls.Shell.Current.GoToAsync("..");

            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    async void NavigateToShot(int shotId)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<ShotLoggingPageProps>("shot-logging", props =>
        {
            props.ShotId = shotId;
        });
    }

    async void NavigateToBag(int bagId)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<BagDetailPageProps>("bag-detail", props =>
        {
            props.BagId = bagId;
            props.BeanId = State.BeanId ?? 0;
            props.BeanName = State.Name;
        });
    }

    async void NavigateToAddBag()
    {
        if (!State.BeanId.HasValue) return;

        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<BagDetailPageProps>("bag-detail", props =>
        {
            props.BagId = null;  // null means create new
            props.BeanId = State.BeanId.Value;
            props.BeanName = State.Name;
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

        var page = ContentPage(
            (isEditMode) ?
                ToolbarItem().Text("Add Bag").IconImageSource(AppIcons.Add).Order(ToolbarItemOrder.Secondary).OnClicked(NavigateToAddBag) : null,
            (isEditMode) ?
                ToolbarItem().Text("Delete").IconImageSource(AppIcons.Delete).Order(ToolbarItemOrder.Secondary).OnClicked(DeleteBeanAsync) : null,
            ScrollView(
                VStack(spacing: 16,
                    // Form section
                    RenderForm(),

                    // Rating section (edit mode only)
                    isEditMode ? RenderRatings() : null,

                    // Recipes section (edit mode only)
                    isEditMode ? RenderRecipes() : null,

                    // Bags section (edit mode only)
                    isEditMode ? RenderBags() : null,

                    // Shot history section (edit mode only)
                    isEditMode ? RenderShotHistory() : null
                )
                .Padding(16)
            )
        ).Title(title)
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Always))
        .OnAppearing(OnPageAppearing);

        return page;
    }

    void OnPageAppearing()
    {
        // Refresh bags and recipes when returning from child pages
        if (State.BeanId.HasValue && State.BeanId.Value > 0)
        {
            _ = LoadBagsAsync();
            _ = LoadRecipesAsync();
        }
    }

    VisualNode RenderForm()
    {
        var isEditMode = State.BeanId.HasValue && State.BeanId.Value > 0;

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

            // Action button            
            Button(State.IsSaving ? "Saving..." : (isEditMode ? "Save Changes" : "Create Bean"))
                .OnClicked(async () => await SaveBeanAsync())
                .IsEnabled(!State.IsSaving)



        );
    }

    VisualNode RenderRatings()
    {
        return VStack(spacing: 12,
            // Section header
            BoxView().HeightRequest(1).Color(Colors.Gray.WithAlpha(0.3f)),

            Label("Bean Ratings")
                .ThemeKey(ThemeKeys.SubHeadline),

            // Rating display component
            new RatingDisplayComponent()
                .RatingAggregate(State.RatingAggregate)
        );
    }

    VisualNode RenderBags()
    {
        return VStack(spacing: 12,
            // Section header
            BoxView().HeightRequest(1).Color(Colors.Gray.WithAlpha(0.3f)),

            Label("Bags")
                .ThemeKey(ThemeKeys.SubHeadline),

            // Loading state
            State.IsLoadingBags
                ? ActivityIndicator().IsRunning(true).HCenter()
                : null,

            // Empty state
            !State.IsLoadingBags && State.Bags.Count == 0
                ? VStack(spacing: 8,
                    Label("No bags added yet")
                        .TextColor(Colors.Gray)
                        .FontSize(14)
                        .HCenter()
                )
                : null,

            // Bag list
            State.Bags.Count > 0
                ? VStack(spacing: 8,
                    [.. State.Bags.Select(RenderBagItem)]
                )
                : null
        );
    }

    VisualNode RenderBagItem(BagSummaryDto bag)
    {
        return Border(
            VStack(spacing: 8,
                // Roast date and status
                HStack(spacing: 8,
                    Label($"Roasted {bag.FormattedRoastDate}")
                        .ThemeKey(ThemeKeys.CardTitle)
                        .HStart()
                        .VCenter(),

                    // Status badge
                    Label(bag.StatusBadge)
                        .ThemeKey(ThemeKeys.SecondaryText)
                        .Padding(6, 2)
                ),

                // Notes if present
                bag.Notes != null
                    ? Label(bag.Notes)
                        .ThemeKey(ThemeKeys.SecondaryText)
                        .LineBreakMode(Microsoft.Maui.LineBreakMode.WordWrap)
                    : null,

                // Stats row
                HStack(spacing: 16,
                    Label($"{bag.ShotCount} shots")
                        .ThemeKey(ThemeKeys.SecondaryText),

                    bag.AverageRating.HasValue
                        ? HStack(spacing: 4,
                            Label(bag.FormattedRating)
                                .ThemeKey(ThemeKeys.PrimaryText),
                            Label(AppIcons.GetRatingIcon((int)Math.Round(bag.AverageRating.Value)))
                                .FontFamily(MaterialSymbolsFont.FontFamily)
                                .FontSize(16)
                                .ThemeKey(ThemeKeys.PrimaryText)
                        )
                        : Label("No ratings")
                            .ThemeKey(ThemeKeys.SecondaryText)
                )
            )
            .Padding(12)
        )
        .ThemeKey(ThemeKeys.CardBorder)
        .OnTapped(() => NavigateToBag(bag.Id));
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
            Label(MaterialSymbolsFont.Assignment)
                .FontFamily(MaterialSymbolsFont.FontFamily)
                .FontSize(48)
                .HCenter(),
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

    VisualNode RenderRecipes()
    {
        return VStack(spacing: 12,
            BoxView().HeightRequest(1).Color(Colors.Gray.WithAlpha(0.3f)),

            HStack(spacing: 8,
                Label("Recipes")
                    .ThemeKey(ThemeKeys.SubHeadline)
                    .HStart()
                    .VCenter(),

                State.IsRefreshingRecipes
                    ? (VisualNode)ActivityIndicator().IsRunning(true).HeightRequest(20).WidthRequest(20).VCenter()
                    : Button()
                        .Text(State.Recipes.Count == 0 ? "Find Recipes" : "Refresh")
                        .OnClicked(async () => await RefreshRecipesAsync())
                        .IsEnabled(!State.IsRefreshingRecipes)
                        .HEnd()
            ),

            // Error state
            State.RecipesLoadError != null
                ? (VisualNode)Label(State.RecipesLoadError).TextColor(Colors.Red)
                : null,

            // Loading state
            State.IsLoadingRecipes && State.Recipes.Count == 0
                ? (VisualNode)ActivityIndicator().IsRunning(true).HCenter()
                : null,

            // Empty state
            !State.IsLoadingRecipes && !State.IsRefreshingRecipes && State.Recipes.Count == 0 && State.RecipesLoadError == null
                ? (VisualNode)VStack(spacing: 8,
                    Label("No recipes yet for this bean.")
                        .ThemeKey(ThemeKeys.SecondaryText)
                        .HCenter(),
                    Label("Tap \"Find Recipes\" to look up official brew guides.")
                        .ThemeKey(ThemeKeys.SecondaryText)
                        .FontSize(12)
                        .HCenter()
                )
                .Padding(0, 12)
                : null,

            // Recipe list
            State.Recipes.Count > 0
                ? (VisualNode)VStack(spacing: 8,
                    [.. State.Recipes.Select(RenderRecipeItem)]
                )
                : null
        );
    }

    VisualNode RenderRecipeItem(RecipeDto recipe)
    {
        string sourceLabel = recipe.Source switch
        {
            RecipeSource.RoasterSite => "Roaster",
            RecipeSource.AIGenerated => "AI suggested",
            RecipeSource.Manual => "Custom",
            _ => recipe.Source.ToString()
        };

        var paramsRow = BuildParamsRow(recipe);

        return Border(
            VStack(spacing: 6,
                HStack(spacing: 8,
                    Label(recipe.BrewMethod.DisplayName())
                        .ThemeKey(ThemeKeys.CardTitle)
                        .HStart()
                        .VCenter(),

                    Label(sourceLabel)
                        .ThemeKey(ThemeKeys.SecondaryText)
                        .FontSize(11)
                        .Padding(6, 2),

                    recipe.IsEditedByUser
                        ? (VisualNode)Label("edited")
                            .ThemeKey(ThemeKeys.SecondaryText)
                            .FontSize(11)
                            .Padding(6, 2)
                        : null
                ),

                !string.IsNullOrWhiteSpace(recipe.Title)
                    ? (VisualNode)Label(recipe.Title!)
                        .ThemeKey(ThemeKeys.SecondaryText)
                        .FontSize(12)
                    : null,

                paramsRow,

                !string.IsNullOrWhiteSpace(recipe.GrindHint)
                    ? (VisualNode)Label($"Grind: {recipe.GrindHint}")
                        .ThemeKey(ThemeKeys.SecondaryText)
                        .FontSize(12)
                    : null,

                // Grinder-specific translation chip, shown only when we have
                // an active grinder and either a result or a loading state.
                !string.IsNullOrWhiteSpace(recipe.GrindHint) && State.ActiveGrinderId.HasValue
                    ? GrindTranslationChip.Render(
                        State.GrindTranslations.TryGetValue(recipe.Id, out var t) ? t : null,
                        loading: State.IsTranslatingGrinds
                                 && !State.GrindTranslations.ContainsKey(recipe.Id))
                    : null,

                !string.IsNullOrWhiteSpace(recipe.Notes)
                    ? (VisualNode)Label(recipe.Notes!)
                        .ThemeKey(ThemeKeys.SecondaryText)
                        .FontSize(12)
                        .LineBreakMode(Microsoft.Maui.LineBreakMode.WordWrap)
                    : null,

                !string.IsNullOrWhiteSpace(recipe.SourceUrl)
                    ? (VisualNode)Label("View source")
                        .TextColor(Colors.Blue)
                        .FontSize(12)
                        .OnTapped(async () =>
                        {
                            try
                            {
                                await Microsoft.Maui.ApplicationModel.Browser.OpenAsync(
                                    recipe.SourceUrl!,
                                    Microsoft.Maui.ApplicationModel.BrowserLaunchMode.SystemPreferred);
                            }
                            catch { /* best effort */ }
                        })
                    : null
            )
            .Padding(12)
        )
        .ThemeKey(ThemeKeys.CardBorder);
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
            return Label("No parameters captured.").ThemeKey(ThemeKeys.SecondaryText).FontSize(12);

        return Label(string.Join(" · ", parts))
            .ThemeKey(ThemeKeys.PrimaryText)
            .FontSize(13);
    }
}
