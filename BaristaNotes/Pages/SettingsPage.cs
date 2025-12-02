using MauiReactor;

namespace BaristaNotes.Pages;

class SettingsPageState
{
    public bool IsLoading { get; set; }
}

partial class SettingsPage : Component<SettingsPageState>
{
    public override VisualNode Render()
    {
        return ContentPage("Settings",
            ScrollView(
                VStack(spacing: 16,
                    // Header
                    Label("Manage")
                        .FontSize(14)
                        .TextColor(Colors.Gray)
                        .Padding(16, 16, 16, 8),

                    // Equipment management option
                    RenderSettingsItem(
                        "Equipment",
                        "Manage machines, grinders, and accessories",
                        async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("equipment")),

                    // Bean management option
                    RenderSettingsItem(
                        "Beans",
                        "Manage coffee beans and roasters",
                        async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("beans")),

                    // User Profile management option
                    RenderSettingsItem(
                        "User Profiles",
                        "Manage household members",
                        async () => await Microsoft.Maui.Controls.Shell.Current.GoToAsync("profiles")),

                    // About section
                    Label("About")
                        .FontSize(14)
                        .TextColor(Colors.Gray)
                        .Padding(16, 24, 16, 8),

                    Border(
                        VStack(spacing: 8,
                            Label("BaristaNotes")
                                .FontSize(16)
                                .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                            Label("Version 1.0")
                                .FontSize(12)
                                .TextColor(Colors.Gray),
                            Label("Track your espresso journey")
                                .FontSize(12)
                                .TextColor(Colors.Gray)
                        )
                        .Padding(16)
                    )
                    .Stroke(Colors.LightGray)
                    .BackgroundColor(Colors.White)
                    .Margin(16, 0, 16, 16)
                )
            )
            .BackgroundColor(Color.FromArgb("#F5F5F5"))
        );
    }

    VisualNode RenderSettingsItem(string title, string description, Action onTapped)
    {
        return Border(
            Grid("*", "*,Auto",
                VStack(spacing: 4,
                    Label(title)
                        .FontSize(16)
                        .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                    Label(description)
                        .FontSize(12)
                        .TextColor(Colors.Gray)
                )
                .VCenter(),
                Label("›")
                    .FontSize(20)
                    .TextColor(Colors.Gray)
                    .GridColumn(1)
                    .VCenter()
            )
            .Padding(16)
        )
        .Stroke(Colors.LightGray)
        .BackgroundColor(Colors.White)
        .Margin(16, 0)
        .OnTapped(onTapped);
    }
}
