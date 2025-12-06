using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Styles;
using BaristaNotes.Utilities;
using Fonts;

namespace BaristaNotes.Components.Forms;

partial class DeleteProfileConfirmationComponent : Component
{
    private readonly UserProfileDto _profile;
    private readonly Action _onConfirm;

    public DeleteProfileConfirmationComponent(UserProfileDto profile, Action onConfirm)
    {
        _profile = profile;
        _onConfirm = onConfirm;
    }

    async Task ConfirmAsync()
    {
        await BottomSheetManager.DismissAsync();
        _onConfirm();
    }

    async Task CancelAsync()
    {
        await BottomSheetManager.DismissAsync();
    }

    public override VisualNode Render()
        => VStack(spacing: 16,
            HStack(spacing: 8,
                Label(MaterialSymbolsFont.Warning)
                    .FontFamily(MaterialSymbolsFont.FontFamily)
                    .FontSize(24),
                Label("Delete Profile")
                    .FontSize(20)
                    .FontAttributes(MauiControls.FontAttributes.Bold)
                    .TextColor(AppColors.Error)
            ),
            
            Label($"\"{_profile.Name}\"")
                .FontSize(16)
                .FontAttributes(MauiControls.FontAttributes.Bold)
                .HorizontalTextAlignment(TextAlignment.Center)
                .ThemeKey(ThemeKeys.PrimaryText),
            
            Label("Are you sure you want to delete this profile? Shot records associated with this profile will retain the historical reference.")
                .FontSize(14)
                .HorizontalTextAlignment(TextAlignment.Center)
                .ThemeKey(ThemeKeys.SecondaryText),
            
            HStack(spacing: 12,
                Button("Cancel")
                    .OnClicked(CancelAsync)
                    .ThemeKey(ThemeKeys.SecondaryButton),
                Button("Delete")
                    .OnClicked(ConfirmAsync)
                    .BackgroundColor(AppColors.Error)
                    .TextColor(Colors.White)
            )
            .HCenter()
        )
        .Padding(24)
        .ThemeKey(ThemeKeys.BottomSheet);
}
