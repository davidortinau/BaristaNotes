using BaristaNotes.Styles;
using BaristaNotes.Utilities;

namespace BaristaNotes.Components.Forms;

partial class LastProfileWarningComponent : Component
{
    async Task CloseAsync()
    {
        await BottomSheetManager.DismissAsync();
    }

    public override VisualNode Render()
        => VStack(spacing: 16,
            HStack(spacing: 8,
                Label("🚫")
                    .FontSize(24),
                Label("Cannot Delete")
                    .FontSize(20)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .ThemeKey(ThemeKeys.PrimaryText)
            )
            .HCenter(),
            
            Label("This is your last profile. You must have at least one profile to log shots.")
                .FontSize(14)
                .HorizontalTextAlignment(TextAlignment.Center)
                .ThemeKey(ThemeKeys.SecondaryText),
            
            Label("💡 Create another profile first if you want to delete this one.")
                .FontSize(12)
                .HorizontalTextAlignment(TextAlignment.Center)
                .ThemeKey(ThemeKeys.MutedText),
            
            Button("OK")
                .OnClicked(CloseAsync)
                .ThemeKey(ThemeKeys.PrimaryButton)
                .HCenter()
        )
        .Padding(24)
        .ThemeKey(ThemeKeys.BottomSheet);
}
