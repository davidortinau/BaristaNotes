using Controls = Microsoft.Maui.Controls;

namespace BaristaNotes.Integrations.Popups;

public enum PhotoIntentChoice
{
    Cancel,
    Coffee,
    Profile,
    Room,
    Retake
}

public sealed class PhotoIntentPopup : ActionModalPopup
{
    private readonly TaskCompletionSource<PhotoIntentChoice> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _completed;

    public PhotoIntentPopup()
    {
        Title = "Use This Photo";
        ShowActionButton = false;
        CloseWhenBackgroundIsClicked = false;
        CloseButtonCommand = new Command(async () => await CompleteAsync(PhotoIntentChoice.Cancel));

        PopupContent = new Controls.VerticalStackLayout
        {
            Spacing = AppSpacing.S,
            Padding = new Thickness(AppSpacing.M, AppSpacing.S),
            Children =
            {
                new Controls.Label
                {
                    Text = "I could not determine the next step. What do you want to create?",
                    FontFamily = "Manrope",
                    FontSize = AppFontSizes.BodySmall,
                    TextColor = AppColors.Dark.TextSecondary,
                    Margin = new Thickness(0, 0, 0, AppSpacing.S)
                },
                CreateChoiceButton(
                    "Add coffee",
                    "Fill a new bean and bag card from the image",
                    "PhotoIntentCoffeeButton",
                    PhotoIntentChoice.Coffee),
                CreateChoiceButton(
                    "Create profile",
                    "Use the image as a new profile photo",
                    "PhotoIntentProfileButton",
                    PhotoIntentChoice.Profile),
                CreateChoiceButton(
                    "Count people",
                    "Calculate coffee needs for a room or group",
                    "PhotoIntentRoomButton",
                    PhotoIntentChoice.Room),
                CreateChoiceButton(
                    "Take another photo",
                    "Open the camera again",
                    "PhotoIntentRetakeButton",
                    PhotoIntentChoice.Retake)
            }
        };
    }

    public Task<PhotoIntentChoice> WaitForChoiceAsync(CancellationToken cancellationToken)
        => _completion.Task.WaitAsync(cancellationToken);

    public override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_completed)
        {
            _completed = true;
            _completion.TrySetResult(PhotoIntentChoice.Cancel);
        }
    }

    private Controls.View CreateChoiceButton(
        string title,
        string description,
        string automationId,
        PhotoIntentChoice choice)
    {
        var content = new Controls.VerticalStackLayout
        {
            Spacing = AppSpacing.XS,
            Children =
            {
                new Controls.Label
                {
                    Text = title,
                    FontFamily = "ManropeSemibold",
                    FontSize = AppFontSizes.BodyMedium,
                    TextColor = AppColors.Dark.TextPrimary
                },
                new Controls.Label
                {
                    Text = description,
                    FontFamily = "Manrope",
                    FontSize = AppFontSizes.Caption,
                    TextColor = AppColors.Dark.TextSecondary
                }
            }
        };

        var border = new Controls.Border
        {
            BackgroundColor = AppColors.Dark.SurfaceVariant,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = AppSpacing.M },
            MinimumHeightRequest = 64,
            Padding = new Thickness(AppSpacing.M, AppSpacing.S),
            AutomationId = automationId,
            Content = content
        };

        var tap = new Controls.TapGestureRecognizer();
        tap.Tapped += async (_, _) => await CompleteAsync(choice);
        border.GestureRecognizers.Add(tap);
        return border;
    }

    private async Task CompleteAsync(PhotoIntentChoice choice)
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        await IPopupService.Current.PopAsync(this);
        _completion.TrySetResult(choice);
    }
}
