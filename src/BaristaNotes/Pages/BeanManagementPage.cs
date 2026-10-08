using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using BaristaNotes.Core.Services.Origins;
using NominatimOriginGeocoder = BaristaNotes.Core.Services.Origins.NominatimOriginGeocoder;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

class BeanManagementState
{
    public List<BeanDto> Beans { get; set; } = new();
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
#if IOS || ANDROID
    public BeanMapView? MapView { get; set; }
    public BeanPageChrome? PageChrome { get; set; }
    public IReadOnlyList<BeanDto> SelectedBeans { get; set; } = Array.Empty<BeanDto>();
    public OriginSelection? SelectedOrigin { get; set; }
    public Action? UnfilteredListPosition { get; set; }
#endif
}

partial class BeanManagementPage : Component<BeanManagementState>
{
    [Inject] IBeanService _beanService;
    [Inject] IFeedbackService _feedbackService;
    [Inject] ILogger<BeanManagementPage> _logger;
#if IOS || ANDROID
    [Inject] NominatimOriginGeocoder _geocoder;
#endif
    private bool _loadingData;

    protected override void OnMounted()
    {
        base.OnMounted();
#if IOS || ANDROID
        State.MapView ??= new BeanMapView(_logger, _geocoder);
        State.MapView.OriginSelected = SelectOriginAsync;
        State.MapView.OriginsChanged = () =>
        {
            if (State.SelectedOrigin is { } selection)
                SetState(s => s.SelectedBeans = BeansFor(selection));
        };
        State.MapView.ReloadOrigins = LoadDataAsync;
        State.PageChrome ??= new BeanPageChrome(State.MapView, ClearOriginAsync, RetryBeansAsync);
#endif
        SetState(s => s.IsLoading = true);
        _ = LoadDataAsync();
    }

    protected override void OnPropsChanged()
    {
        base.OnPropsChanged();
        _ = LoadDataAsync();
    }

    void OnPageAppearing()
    {
#if ANDROID
        State.PageChrome?.SetPageVisible(true);
#endif
        _ = LoadDataAsync();
    }

    protected override void OnWillUnmount()
    {
#if IOS || ANDROID
        State.MapView?.Dispose();
        State.PageChrome?.Dispose();
        State.PageChrome = null;
        State.MapView = null;
#endif
        base.OnWillUnmount();
    }

    async Task LoadDataAsync()
    {
        if (_loadingData)
            return;
        _loadingData = true;
#if IOS || ANDROID
        var restoreList = State.MapView?.SaveListPosition();
#endif
        try
        {
#if IOS || ANDROID
            var savedBeans = await _beanService.GetAllSavedBeansAsync();
            var beans = savedBeans.Where(bean => bean.IsActive)
                .OrderBy(bean => bean.Name, StringComparer.Ordinal).ToList();
            State.MapView?.SetBeans(savedBeans);
#else
            var beans = await _beanService.GetAllActiveBeansAsync();
#endif
            SetState(s =>
            {
                if (!s.Beans.SequenceEqual(beans))
                    s.Beans = beans.ToList();
#if IOS || ANDROID
                if (s.SelectedOrigin is { } selection)
                {
                    var selectedBeans = BeansFor(selection);
                    if (!s.SelectedBeans.SequenceEqual(selectedBeans))
                        s.SelectedBeans = selectedBeans;
                }
#endif
                s.IsLoading = false;
                s.ErrorMessage = null;
            });
#if IOS || ANDROID
            if (State.MapView is { } map)
                await map.RestoreListPositionAsync(restoreList);
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load saved beans");
#if IOS || ANDROID
            State.MapView?.ShowOriginLoadError();
#endif
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = ex.Message;
            });
        }
        finally
        {
            _loadingData = false;
        }
    }

#if IOS || ANDROID
    async Task SelectOriginAsync(OriginSelection selection)
    {
        if (State.SelectedOrigin?.Key == selection.Key)
        {
            await ClearOriginAsync();
            return;
        }
        if (State.SelectedOrigin is null)
            State.UnfilteredListPosition = State.MapView?.SaveListPosition();
        var restore = State.MapView?.SaveListPosition();
        SetState(s =>
        {
            s.SelectedOrigin = selection;
            s.SelectedBeans = BeansFor(selection);
        });
        if (State.MapView is { } map)
            await map.RestoreListPositionAsync(restore);
    }

    async Task ClearOriginAsync()
    {
        var restore = State.UnfilteredListPosition;
        SetState(s =>
        {
            s.SelectedOrigin = null;
            s.UnfilteredListPosition = null;
        });
        if (State.MapView is { } map)
            await map.RestoreListPositionAsync(restore);
    }

    IReadOnlyList<BeanDto> BeansFor(OriginSelection selection) =>
        State.MapView?.Origins is { } origins ? selection.BeansFor(origins) : Array.Empty<BeanDto>();

    IReadOnlyList<BeanDto> DisplayedBeans =>
        State.SelectedOrigin is not null ? State.SelectedBeans : State.Beans;

    string? SelectedOriginLabel
    {
        get
        {
            if (State.SelectedOrigin is not { } selection || State.MapView?.Origins is not { } origins)
                return null;
            var locations = origins.Locations.Where(location => selection.LocationKeys.Contains(location.Key)).ToArray();
            return locations.Length == 0 ? "Origins (0)" : new OriginCluster(locations).Selection.Label;
        }
    }

    async Task RetryBeansAsync()
    {
        SetState(s => { s.ErrorMessage = null; s.IsLoading = true; });
        await LoadDataAsync();
    }
#else
    IReadOnlyList<BeanDto> DisplayedBeans => State.Beans;
#endif

    async Task NavigateToAddBean()
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync("bean-detail");
    }

    async void NavigateToEditBean(BeanDto bean)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<BeanDetailPageProps>("bean-detail", props =>
        {
            props.BeanId = bean.Id;
        });
    }

    async Task NavigateBack()
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync("..");
    }

    // ============================================================
    // Rendering
    // ============================================================

    public override VisualNode Render()
    {
        return ContentPage("Beans",
#if IOS || ANDROID
            Grid(rows: "*,Auto", columns: "*",
                RenderPageScroller().GridRow(0),
                BottomNavRow().GridRow(1)
#else
            Grid(rows: "Auto,*,Auto", columns: "*",
                HeaderTile().GridRow(0),
                RenderBody().GridRow(1),
                BottomNavRow().GridRow(2)
#endif
            )
            .RowSpacing(1)
            .BackgroundColor(DividerColor())
#if IOS || ANDROID
            .Padding(new Thickness(1, 0, 1, 1))
#else
            .Padding(1)
#endif
            .SafeAreaEdges(new SafeAreaEdges(
#if IOS || ANDROID
                SafeAreaRegions.Container,
                SafeAreaRegions.None,
                SafeAreaRegions.Container,
#else
                SafeAreaRegions.None,
                SafeAreaRegions.None,
                SafeAreaRegions.None,
#endif
                SafeAreaRegions.None))
        )
        .Set(MauiControls.Shell.NavBarIsVisibleProperty, false)
        .Set(MauiControls.Shell.TabBarIsVisibleProperty, false)
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
#if IOS || ANDROID
        .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None))
        .OnSizeChanged((sender, _) =>
        {
            if (sender is MauiControls.Page page && page.Width > 0 && page.Height > 0)
                State.PageChrome?.SetLandscape(page.Width > page.Height);
        })
#endif
#if ANDROID
        .OnDisappearing(() => State.PageChrome?.SetPageVisible(false))
#endif
        .OnAppearing(() => OnPageAppearing());
    }

    // ------------------------------------------------------------
    // Theme helpers
    // ------------------------------------------------------------

    static bool IsLight() => Application.Current?.RequestedTheme != AppTheme.Dark;
    static Color SurfaceColor() => IsLight() ? AppColors.Light.Surface : AppColors.Dark.Surface;
    static Color TextPrimary() => IsLight() ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
    static Color TextSecondary() => IsLight() ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
    static Color AccentColor() => IsLight() ? AppColors.Light.Primary : AppColors.Dark.Primary;
    static Color DividerColor() => IsLight() ? AppColors.Light.Outline : AppColors.Dark.Outline;

    // ------------------------------------------------------------
    // Header tile
    // ------------------------------------------------------------

    VisualNode HeaderTile()
    {
        var count = State.Beans.Count;
        var countText = count == 1 ? "1 bean" : $"{count} beans";

        return Border(
            Grid(rows: "Auto,*", columns: "*",
                Label("BEANS")
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0),
                Label(countText)
                    .FontSize(28)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .VEnd()
                    .GridRow(1)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(120);
    }

    // ------------------------------------------------------------
    // Body
    // ------------------------------------------------------------

#if IOS || ANDROID
    VisualNode RenderPageScroller()
    {
        if (State.PageChrome is not { } chrome)
            return Grid();
        chrome.Update(DisplayedBeans, State.Beans.Count, SelectedOriginLabel,
            DisplayedBeans.Count, State.IsLoading, State.ErrorMessage);
        return Grid(
            CollectionView()
                .ItemsSource(State.IsLoading || State.ErrorMessage is not null
                    ? Array.Empty<BeanDto>() : DisplayedBeans, RenderBeanRow)
                .Set(MauiControls.CollectionView.HeaderProperty, chrome.Header)
                .Set(MauiControls.CollectionView.FooterProperty, chrome.Footer)
                .Set(MauiControls.ItemsView.ItemsUpdatingScrollModeProperty,
                    MauiControls.ItemsUpdatingScrollMode.KeepScrollOffset)
                .OnLoaded((sender, _) =>
                {
                    if (sender is not MauiControls.CollectionView list)
                        return;
                    try
                    {
                        chrome.Attach(list);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Could not attach Beans page scrolling");
                        State.MapView?.ShowError("Could not attach page scrolling. Leave Beans and reopen it to retry.");
                    }
                })
                .BackgroundColor(DividerColor()),
            ContentView()
                .Set(MauiControls.ContentView.ContentProperty, chrome.Title)
                .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None))
                .TranslationY(chrome.TitleOffset)
                .VStart()
                .OnLoaded((sender, _) =>
                {
                    if (sender is MauiControls.ContentView host)
                        chrome.AttachTitleHost(host);
                }),
            ContentView()
                .Set(MauiControls.ContentView.ContentProperty, chrome.StatusBarTint)
                .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None))
                .VStart()
                .InputTransparent(true)
        )
        .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None))
        .Set(MauiControls.Layout.IsClippedToBoundsProperty, true);
    }
#else
    VisualNode RenderBody()
    {
        return RenderBeanBody();
    }

    VisualNode RenderBeanBody()
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

        if (State.ErrorMessage != null)
        {
            return Border(
                VStack(spacing: 12,
                    Label("ERROR")
                        .FontSize(10).CharacterSpacing(2)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextSecondary())
                        .HCenter(),
                    Label(State.ErrorMessage ?? "Unknown error")
                        .FontSize(16)
                        .TextColor(TextPrimary())
                        .HCenter(),
                    Button("Retry")
                        .OnClicked(async () =>
                        {
                            SetState(s => { s.ErrorMessage = null; s.IsLoading = true; });
                            await LoadDataAsync();
                        })
                        .BackgroundColor(AccentColor())
                        .TextColor(SurfaceColor())
                ).VCenter().HCenter().Padding(24)
            )
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle());
        }

        if (DisplayedBeans.Count == 0)
        {
            return Border(
                VStack(spacing: 12,
                    Label("NO BEANS")
                        .FontSize(10).CharacterSpacing(2)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextSecondary())
                        .HCenter(),
                    Label(
                        "Add your favorite coffee beans")
                        .FontSize(16)
                        .TextColor(TextPrimary())
                        .HCenter()
                        .HorizontalTextAlignment(TextAlignment.Center)
                ).VCenter().HCenter().Padding(32)
            )
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle());
        }

        return CollectionView()
            .ItemsSource(DisplayedBeans, RenderBeanRow)
            .BackgroundColor(DividerColor());
    }
#endif

    VisualNode RenderBeanRow(BeanDto bean)
    {
        var subtitle = !string.IsNullOrWhiteSpace(bean.Roaster)
            ? bean.Roaster!.ToUpperInvariant()
            : (!string.IsNullOrWhiteSpace(bean.Origin) ? bean.Origin!.ToUpperInvariant() : "BEAN");
        if (!bean.IsActive)
            subtitle = $"ARCHIVED / {subtitle}";

        return new AdaptiveTwoLineTile(
            Label(subtitle)
                .FontSize(10)
                .CharacterSpacing(2)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(TextSecondary())
                .LineBreakMode(LineBreakMode.TailTruncation)
                .MaxLines(1),
            Label(bean.Name)
                .FontSize(20)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .TextColor(TextPrimary())
                .LineBreakMode(LineBreakMode.TailTruncation)
                .MaxLines(1))
        .Trailing(
            AdaptiveTwoLineTile.DecorativeGlyph(
                MaterialSymbolsFont.Chevron_right,
                TextPrimary()))
        .BackgroundColor(SurfaceColor())
        .Margin(new Thickness(0, 0, 0, 1))
        .OnTapped(
            $"{subtitle}: {bean.Name}",
            () => NavigateToEditBean(bean));
    }

    // ------------------------------------------------------------
    // Bottom nav row
    // ------------------------------------------------------------

    VisualNode BottomNavRow()
    {
        return Grid(rows: "Auto", columns: "*,*,*,*",
            NavTile(AppIcons.CoffeeCup,
                async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//shots"))
                .GridColumn(0),
            NavTile(AppIcons.Feed,
                async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("//history"))
                .GridColumn(1),
            NavTile(AppIcons.Settings,
                async () => await NavigateBack())
                .GridColumn(2),
            NavTile(AppIcons.Add,
                async () => await NavigateToAddBean(), inverted: true)
                .GridColumn(3)
        )
        .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());
    }

    VisualNode NavTile(FontImageSource imageSource, Action onTap, bool inverted = false)
    {
        FontImageSource source = imageSource;
        if (inverted)
        {
            source = new FontImageSource
            {
                FontFamily = imageSource.FontFamily,
                Glyph = imageSource.Glyph,
                Size = imageSource.Size,
                Color = SurfaceColor()
            };
        }

        var bg = inverted ? TextPrimary() : SurfaceColor();

        return Border(
            Image()
                .Source(source)
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
}
