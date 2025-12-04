using MauiReactor;

namespace BaristaNotes.Components.Feedback;

partial class LoadingOverlay : Component
{
    [Prop]
    string? _message;

    public override VisualNode Render()
    {
        return Grid(
            // Semi-transparent background
            BoxView()
                .BackgroundColor(Color.FromArgb("#B32D1F1A")),

            // Loading content
            VStack(spacing: 16,
                ActivityIndicator()
                    .IsRunning(true)
                    .Color(Color.FromArgb("#D4A574"))
                    .WidthRequest(48)
                    .HeightRequest(48),

                Label(_message ?? "Loading...")
                    .FontSize(18)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                    .TextColor(Color.FromArgb("#F5E6D3"))
                    .HorizontalTextAlignment(TextAlignment.Center)
            )
            .Center()
        );
    }
}
