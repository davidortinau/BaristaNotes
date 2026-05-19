using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;
using MauiReactor;
using MauiReactor.Shapes;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Application = Microsoft.Maui.Controls.Application;

namespace BaristaNotes.Pages;

class EquipmentManagementState
{
    public List<EquipmentDto> Equipment { get; set; } = new();
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
}

partial class EquipmentManagementPage : Component<EquipmentManagementState>
{
    [Inject]
    IEquipmentService _equipmentService;

    [Inject]
    IFeedbackService _feedbackService;

    protected override void OnMounted()
    {
        base.OnMounted();
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
        _ = LoadDataAsync();
    }

    async Task LoadDataAsync()
    {
        try
        {
            var equipment = await _equipmentService.GetAllActiveEquipmentAsync();
            SetState(s =>
            {
                s.Equipment = equipment.ToList();
                s.IsLoading = false;
            });
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

    async Task NavigateToAddEquipment()
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync("equipment-detail");
    }

    async void NavigateToEditEquipment(EquipmentDto equipment)
    {
        await Microsoft.Maui.Controls.Shell.Current.GoToAsync<EquipmentDetailPageProps>("equipment-detail", props =>
        {
            props.EquipmentId = equipment.Id;
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
        return ContentPage("Equipment",
            Grid(rows: "Auto,*,Auto", columns: "*",
                HeaderTile().GridRow(0),
                RenderBody().GridRow(1),
                BottomNavRow().GridRow(2)
            )
            .RowSpacing(1)
            .BackgroundColor(DividerColor())
            .Padding(1)
            .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        )
        .Set(MauiControls.Shell.NavBarIsVisibleProperty, false)
        .Set(MauiControls.Shell.TabBarIsVisibleProperty, false)
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
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
        var count = State.Equipment.Count;
        var countText = count == 1 ? "1 item" : $"{count} items";

        return Border(
            Grid(rows: "Auto,*", columns: "*",
                Label("EQUIPMENT")
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
                VStack(spacing: 8,
                    ActivityIndicator().IsRunning(true),
                    Label("Loading…").FontSize(14).TextColor(TextSecondary()).HCenter()
                ).VCenter().HCenter()
            )
            .BackgroundColor(SurfaceColor())
            .StrokeThickness(0)
            .StrokeShape(new Rectangle());
        }

        if (!string.IsNullOrEmpty(State.ErrorMessage))
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

        if (State.Equipment.Count == 0)
        {
            return Border(
                VStack(spacing: 12,
                    Label("NO EQUIPMENT")
                        .FontSize(10).CharacterSpacing(2)
                        .FontAttributes(MauiControls.FontAttributes.Bold)
                        .TextColor(TextSecondary())
                        .HCenter(),
                    Label("Add machines, grinders, and accessories")
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
            .ItemsSource(State.Equipment, RenderEquipmentRow)
            .BackgroundColor(DividerColor());
    }

    VisualNode RenderEquipmentRow(EquipmentDto equipment)
    {
        return Border(
            Grid(rows: "Auto,Auto", columns: "*,Auto",
                Label(equipment.Type.ToString().ToUpperInvariant())
                    .FontSize(10)
                    .CharacterSpacing(2)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextSecondary())
                    .GridRow(0).GridColumn(0),
                Label(equipment.Name)
                    .FontSize(20)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .LineBreakMode(LineBreakMode.TailTruncation)
                    .GridRow(1).GridColumn(0),
                Label("→")
                    .FontSize(22)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(TextPrimary())
                    .VCenter()
                    .GridRow(0).GridRowSpan(2).GridColumn(1)
            )
            .Padding(16, 14, 16, 14)
        )
        .BackgroundColor(SurfaceColor())
        .StrokeThickness(0)
        .StrokeShape(new Rectangle())
        .MinimumHeightRequest(80)
        .Margin(0, 0, 0, 1)
        .OnTapped(() => NavigateToEditEquipment(equipment));
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
                async () => await NavigateToAddEquipment(), inverted: true)
                .GridColumn(3)
        )
        .SafeAreaEdges(new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None))
        .ColumnSpacing(1)
        .BackgroundColor(DividerColor());
    }

    VisualNode NavTile(FontImageSource imageSource, Action onTap, bool inverted = false)
    {
        // For inverted, build a tinted FontImageSource so the icon glyph is
        // visible against the dark background.
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
