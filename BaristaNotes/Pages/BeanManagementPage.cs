using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
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

    public override VisualNode Render()
    {
        if (State.IsLoading)
        {
            return ContentPage("Beans",
                VStack(
                    ActivityIndicator()
                        .IsRunning(true)
                        .VCenter()
                        .HCenter()
                )
                .VCenter()
                .HCenter()
            );
        }

        if (!string.IsNullOrEmpty(State.ErrorMessage))
        {
            return ContentPage("Beans",
                VStack(
                    Label("⚠️")
                        .FontSize(48)
                        .HCenter(),
                    Label(State.ErrorMessage)
                        .HCenter()
                )
                .VCenter()
                .HCenter()
                .Spacing(16)
            );
        }

        return ContentPage("Beans",
            VStack(spacing: 16,
                Label("Bean Management")
                    .FontSize(24)
                    .HCenter(),
                Label("Coming soon: Add, edit, and manage your coffee beans")
                    .HCenter(),
                CollectionView()
                    .ItemsSource(State.Beans, RenderBeanItem)
            )
            .Padding(16)
        );
    }

    VisualNode RenderBeanItem(BeanDto bean)
    {
        return Border(
            VStack(spacing: 8,
                Label(bean.Name)
                    .FontSize(18)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                bean.Roaster != null 
                    ? Label($"Roaster: {bean.Roaster}")
                    : null,
                bean.Origin != null 
                    ? Label($"Origin: {bean.Origin}")
                    : null,
                bean.RoastDate.HasValue 
                    ? Label($"Roast Date: {bean.RoastDate.Value:d}")
                        .FontSize(12)
                    : null
            )
            .Padding(12)
        )
        .Margin(0, 4)
        .StrokeThickness(1)
        .Stroke(new SolidColorBrush(Colors.Gray));
    }
}
