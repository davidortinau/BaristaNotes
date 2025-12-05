using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Components;
using BaristaNotes.Utilities;
using MauiReactor;

namespace BaristaNotes.Pages;

class BeanManagementState
{
    public List<BeanDto> Beans { get; set; } = new();
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
}

partial class BeanManagementPage : Component<BeanManagementState>
{
    [Inject]
    IBeanService _beanService;

    [Inject]
    IFeedbackService _feedbackService;

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
            SetState(s =>
            {
                s.Beans = beans.ToList();
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

    async Task ShowAddBeanSheet()
    {
        await BottomSheetManager.ShowAsync(
            () => new BeanFormSheet()
                .Bean(null)
                .OnSave(bean => OnBeanSaved(bean))
                .OnCancel(() => _ = BottomSheetManager.DismissAsync()),
            sheet =>
            {
                sheet.HasBackdrop = true;
                sheet.HasHandle = true;
                sheet.CornerRadius = 12;
            });
    }

    async Task ShowEditBeanSheet(BeanDto bean)
    {
        await BottomSheetManager.ShowAsync(
            () => new BeanFormSheet()
                .Bean(bean)
                .OnSave(b => OnBeanSaved(b))
                .OnCancel(() => _ = BottomSheetManager.DismissAsync()),
            sheet => sheet.HasBackdrop = true);
    }

    void OnBeanSaved(BeanDto beanDto)
    {
        _ = Task.Run(async () =>
        {
            await BottomSheetManager.DismissAsync();
            await LoadDataAsync();
        });
    }

    async Task ShowDeleteConfirmation(BeanDto bean)
    {
        await BottomSheetManager.ShowAsync(
            () => VStack(spacing: 16,
                HStack(spacing: 8,
                    Label("⚠️")
                        .FontSize(24),
                    Label("Delete Bean")
                        .ThemeKey(ThemeKeys.SubHeadline)
                ),
                Label($"\"{bean.Name}\"")
                    .FontSize(16)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .HCenter(),
                Label("Are you sure you want to delete this bean? Shot records using this bean will retain the historical reference.")
                    .FontSize(14)
                    .ThemeKey(ThemeKeys.SecondaryText)
                    .HCenter(),
                HStack(spacing: 12,
                    Button("Cancel")
                        .ThemeKey(ThemeKeys.SecondaryButton)
                        .OnClicked(async () => await BottomSheetManager.DismissAsync()),
                    Button("Delete")
                        .ThemeKey(ThemeKeys.DangerButton)
                        .OnClicked(async () => await OnDeleteConfirmed(bean))
                )
                .HCenter()
            )
            .Padding(24),
            sheet => sheet.HasBackdrop = true);
    }

    async Task OnDeleteConfirmed(BeanDto bean)
    {
        await BottomSheetManager.DismissAsync();
        await DeleteBean(bean);
    }

    async Task DeleteBean(BeanDto bean)
    {
        try
        {
            await _beanService.DeleteBeanAsync(bean.Id);
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            SetState(s => s.ErrorMessage = ex.Message);
        }
    }

    public override VisualNode Render()
    {
        if (State.IsLoading)
        {
            return ContentPage(
                VStack(
                    ActivityIndicator()
                        .IsRunning(true)
                        .VCenter()
                        .HCenter()
                )
                .VCenter()
                .HCenter()
            ).Title("Beans");
        }

        if (!string.IsNullOrEmpty(State.ErrorMessage))
        {
            return ContentPage(
                VStack(
                    Label("⚠️")
                        .FontSize(48)
                        .HCenter(),
                    Label(State.ErrorMessage)
                        .HCenter(),
                    Button("Retry")
                        .OnClicked(async () =>
                        {
                            SetState(s => s.ErrorMessage = null);
                            await LoadDataAsync();
                        })
                        .Margin(0, 16, 0, 0)
                )
                .VCenter()
                .HCenter()
                .Spacing(16)
            ).Title("Beans");
        }

        return ContentPage(
            ToolbarItem("+ Add")
                .Order(MauiControls.ToolbarItemOrder.Primary)
                .Priority(0)
                .OnClicked(async () => await ShowAddBeanSheet()),
            Grid("Auto,*", "*",
                Label("Beans")
                    .ThemeKey(ThemeKeys.SubHeadline)
                    .Padding(16, 8)
                    .GridRow(0),

                // Bean list
                State.Beans.Count == 0
                    ? RenderEmptyState().GridRow(1)
                    : CollectionView()
                        .ItemsSource(State.Beans, RenderBeanItem)
                        .Margin(16, 0)
                        .GridRow(1)
            )
        ).Title("Beans");
    }

    VisualNode RenderEmptyState()
    {
        return VStack(spacing: 12,
            Label("☕")
                .FontSize(64)
                .HCenter(),
            Label("No Beans Yet")
                .FontSize(20)
                .HCenter(),
            Label("Add your favorite coffee beans to track freshness and tasting notes")
                .FontSize(16)
                .ThemeKey(ThemeKeys.SecondaryText)
                .HCenter()
        )
        .VCenter()
        .HCenter()
        .Padding(24);
    }

    VisualNode RenderBeanItem(BeanDto bean)
    {
        return Border(
            Grid("Auto", "*,Auto",
                VStack(spacing: 4,
                    Label(bean.Name)
                        .ThemeKey(ThemeKeys.CardTitle),
                    bean.Roaster != null
                        ? Label($"🏭 {bean.Roaster}")
                            .FontSize(14)
                            .ThemeKey(ThemeKeys.SecondaryText)
                        : null,
                    bean.Origin != null
                        ? Label($"🌍 {bean.Origin}")
                            .FontSize(14)
                            .ThemeKey(ThemeKeys.SecondaryText)
                        : null,
                    bean.RoastDate.HasValue
                        ? Label($"📅 Roasted: {bean.RoastDate.Value:MMM d, yyyy}")
                            .ThemeKey(ThemeKeys.MutedText)
                        : null
                )
                .GridColumn(0)
                .VCenter(),

                // Action buttons
                HStack(spacing: 8,
                    Button("✏️")
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ShowEditBeanSheet(bean)),
                    Button("🗑️")
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ShowDeleteConfirmation(bean))
                )
                .GridColumn(1)
                .VCenter()
            )
            .Padding(12)
        )
        .Margin(0, 4)
        .ThemeKey(ThemeKeys.Card);
    }
}
