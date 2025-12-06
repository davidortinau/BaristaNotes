using BaristaNotes.Styles;
using BaristaNotes.Utilities;
using Fonts;

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
                Label(MaterialSymbolsFont.Block)
                    .FontFamily(MaterialSymbolsFont.FontFamily)
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

            HStack(spacing: 4,
                Label(MaterialSymbolsFont.Lightbulb)
                    .FontFamily(MaterialSymbolsFont.FontFamily)
                    .FontSize(12)
                    .ThemeKey(ThemeKeys.MutedText),
                Label("Create another profile first if you want to delete this one.")
                    .FontSize(12)
                    .ThemeKey(ThemeKeys.MutedText)
            )
            .HCenter(),

            Button("OK")
                .OnClicked(CloseAsync)
                .ThemeKey(ThemeKeys.PrimaryButton)
                .HCenter()
        )
        .Padding(24)
        .ThemeKey(ThemeKeys.BottomSheet);
}
