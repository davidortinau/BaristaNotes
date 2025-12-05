using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Components;
using BaristaNotes.Utilities;
using MauiReactor;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;

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

    async void OnBeanSaved(BeanDto beanDto)
    {
        try
        {
            if (beanDto.Id == 0)
            {
                // Create new bean
                var createDto = new CreateBeanDto
                {
                    Name = beanDto.Name,
                    Roaster = beanDto.Roaster,
                    RoastDate = beanDto.RoastDate,
                    Origin = beanDto.Origin,
                    Notes = beanDto.Notes
                };
                var result = await _beanService.CreateBeanAsync(createDto);
                if (!result.Success)
                {
                    await _feedbackService.ShowErrorAsync(result.ErrorMessage ?? "Failed to create bean");
                    return;
                }
                await _feedbackService.ShowSuccessAsync("Bean created successfully");
            }
            else
            {
                // Update existing bean
                var updateDto = new UpdateBeanDto
                {
                    Name = beanDto.Name,
                    Roaster = beanDto.Roaster,
                    RoastDate = beanDto.RoastDate,
                    Origin = beanDto.Origin,
                    Notes = beanDto.Notes
                };
                await _beanService.UpdateBeanAsync(beanDto.Id, updateDto);
                await _feedbackService.ShowSuccessAsync("Bean updated successfully");
            }

            await BottomSheetManager.DismissAsync();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            await _feedbackService.ShowErrorAsync(ex.Message);
        }
    }

    async Task ShowDeleteConfirmation(BeanDto bean)
    {
        var popup = new SimpleActionPopup
        {
            Title = $"Delete \"{bean.Name}\"?",
            Text = "This action cannot be undone.",
            ActionButtonText = "Delete",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                await DeleteBean(bean);
                await IPopupService.Current.PopAsync();
            })
        };

        await IPopupService.Current.PushAsync(popup);

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
