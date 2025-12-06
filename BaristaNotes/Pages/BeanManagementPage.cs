using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using Fonts;
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
            ).Title("Beans")
            .OnAppearing(() => OnPageAppearing());
        }

        if (!string.IsNullOrEmpty(State.ErrorMessage))
        {
            return ContentPage(
                VStack(
                    Label(MaterialSymbolsFont.Warning)
                        .FontFamily(MaterialSymbolsFont.FontFamily)
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
            ).Title("Beans")
            .OnAppearing(() => OnPageAppearing());
        }

        return ContentPage(
            ToolbarItem("+ Add")
                .Order(MauiControls.ToolbarItemOrder.Primary)
                .Priority(0)
                .OnClicked(async () => await NavigateToAddBean()),

            // Bean list
            State.Beans.Count == 0
                ? RenderEmptyState()
                : CollectionView()
                    .ItemsSource(State.Beans, RenderBeanItem)
                    .Margin(16, 16, 16, 32)

        ).Title("Beans")
        .OnAppearing(() => OnPageAppearing());
    }

    void OnPageAppearing()
    {
        // Refresh data when returning from detail page
        _ = LoadDataAsync();
    }

    VisualNode RenderEmptyState()
    {
        return VStack(spacing: 12,
            Label(MaterialSymbolsFont.Coffee)
                .FontFamily(MaterialSymbolsFont.FontFamily)
                .FontSize(64)
                .HCenter(),
            Label("No Beans Yet")
                .ThemeKey(ThemeKeys.CardTitle)
                .HCenter(),
            Label("Add your favorite coffee beans to track freshness and tasting notes")
                .ThemeKey(ThemeKeys.CardSubtitle)
                .HCenter()
        )
        .VCenter()
        .HCenter()
        .Padding(24);
    }

    VisualNode RenderBeanItem(BeanDto bean)
    {
        return Border(
            VStack(spacing: 4,
                Label(bean.Name)
                    .ThemeKey(ThemeKeys.CardTitle),
                bean.Roaster != null
                    ? HStack(spacing: 4,
                        Label(MaterialSymbolsFont.Factory)
                            .FontFamily(MaterialSymbolsFont.FontFamily)
                            .FontSize(14)
                            .ThemeKey(ThemeKeys.SecondaryText),
                        Label(bean.Roaster)
                            .FontSize(14)
                            .ThemeKey(ThemeKeys.SecondaryText)
                      )
                    : null,
                bean.Origin != null
                    ? HStack(spacing: 4,
                        Label(MaterialSymbolsFont.Globe)
                            .FontFamily(MaterialSymbolsFont.FontFamily)
                            .FontSize(14)
                            .ThemeKey(ThemeKeys.SecondaryText),
                        Label(bean.Origin)
                            .FontSize(14)
                            .ThemeKey(ThemeKeys.SecondaryText)
                      )
                    : null,
                bean.RoastDate.HasValue
                    ? HStack(spacing: 4,
                        Label(MaterialSymbolsFont.Calendar_today)
                            .FontFamily(MaterialSymbolsFont.FontFamily)
                            .FontSize(12)
                            .ThemeKey(ThemeKeys.MutedText),
                        Label($"Roasted: {bean.RoastDate.Value:MMM d, yyyy}")
                            .ThemeKey(ThemeKeys.MutedText)
                      )
                    : null
            )
            .Padding(12)
        )
        .ThemeKey(ThemeKeys.CardBorder)
        .OnTapped(() => NavigateToEditBean(bean))
        .Margin(0, 4);
    }
}
