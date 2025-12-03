using MauiReactor;

namespace BaristaNotes.Components.Feedback;

partial class LoadingOverlay : Component
{
    [Prop]
    string? _message;

    public override VisualNode Render()
    {
        return new Grid
        {
            // Semi-transparent background
            new BoxView()
                .BackgroundColor(Color.FromArgb("#B32D1F1A")),

            // Loading content
            new VStack(spacing: 16)
            {
                new ActivityIndicator()
                    .IsRunning(true)
                    .Color(Color.FromArgb("#D4A574"))
                    .WidthRequest(48)
                    .HeightRequest(48),

                new Label(_message ?? "Loading...")
                    .FontSize(18)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                    .TextColor(Color.FromArgb("#F5E6D3"))
                    .HorizontalTextAlignment(TextAlignment.Center)
            }
            .Center()
        };
    }
}
