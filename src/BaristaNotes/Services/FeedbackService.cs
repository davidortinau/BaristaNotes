using System.Reactive.Subjects;
using BaristaNotes.Models;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Microsoft.Extensions.Logging;
using UXDivers.Popups.Services;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups;
using Fonts;

namespace BaristaNotes.Services;

public class FeedbackService : IFeedbackService
{
    private readonly ILogger<FeedbackService> _logger;
    private readonly Subject<FeedbackMessage> _feedbackSubject = new();
    private readonly Subject<(bool IsLoading, string? Message)> _loadingSubject = new();
    private readonly Queue<FeedbackMessage> _errorQueue = new();
    private bool _isShowingError = false;

    public FeedbackService(ILogger<FeedbackService> logger)
    {
        _logger = logger;
    }

    public IObservable<FeedbackMessage> FeedbackMessages => _feedbackSubject;
    public IObservable<(bool IsLoading, string? Message)> LoadingState => _loadingSubject;

    public async Task ShowSuccessAsync(string message, int durationMs = 2000)
    {
        ShowToast(Color.FromArgb("#2D5016"), Color.FromArgb("#7CFC00"), MaterialSymbolsFont.Check, message, durationMs);
    }

    public async Task ShowErrorAsync(string message, string? recoveryAction = null, int durationMs = 5000)
    {
        var errorMessage = new FeedbackMessage
        {
            Type = FeedbackType.Error,
            Message = message,
            RecoveryAction = recoveryAction,
            DurationMs = durationMs
        };

        if (_isShowingError)
        {
            _errorQueue.Enqueue(errorMessage);
        }
        else
        {
            _isShowingError = true;

            ShowToast(Color.FromArgb("#5C1A1A"), Color.FromArgb("#FF6B6B"), MaterialSymbolsFont.Close, message, durationMs);

            _ = Task.Delay(durationMs).ContinueWith(_ =>
            {
                _isShowingError = false;
                if (_errorQueue.Count > 0)
                {
                    var nextError = _errorQueue.Dequeue();
                    _ = ShowErrorAsync(nextError.Message, nextError.RecoveryAction, nextError.DurationMs);
                }
            });
        }
    }

    public async Task ShowInfoAsync(string message, int durationMs = 3000)
    {
        ShowToast(Color.FromArgb("#1A3A5C"), Color.FromArgb("#4A9EFF"), MaterialSymbolsFont.Info, message, durationMs);
    }

    public async Task ShowWarningAsync(string message, int durationMs = 3000)
    {
        ShowToast(Color.FromArgb("#5C4A1A"), Color.FromArgb("#FFB74A"), MaterialSymbolsFont.Warning, message, durationMs);
    }

    public async Task ShowActionToastAsync(string message, string actionText, Func<Task> onAction, TimeSpan? duration = null)
    {
        var effectiveDuration = duration ?? TimeSpan.FromSeconds(4);

        try
        {
            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#2D5016"),
                TextColor = Colors.White,
                ActionButtonTextColor = Color.FromArgb("#7CFC00"),
                CornerRadius = new CornerRadius(8),
            };

            var snackbar = Snackbar.Make(
                message: message,
                action: () =>
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            await onAction();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Snackbar action callback threw for message: {Message}", message);
                        }
                    });
                },
                actionButtonText: actionText,
                duration: effectiveDuration,
                visualOptions: snackbarOptions);

            await snackbar.Show();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Snackbar unsupported on this platform; falling back to success toast for message: {Message}", message);
            await ShowSuccessAsync(message);
        }
    }

    public void TriggerSuccessHaptic()
    {
        try
        {
            HapticFeedback.Default.Perform(HapticFeedbackType.Click);
        }
        catch (FeatureNotSupportedException)
        {
            _logger.LogDebug("Haptic feedback not supported on this platform");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Haptic feedback failed");
        }
    }

    private async void ShowToast(Color bgColor, Color iconColor, string icon, string message, int durationMs)
    {
        var toast = new UXDivers.Popups.Maui.Controls.Toast()
        {
            Title = message,
            IconText = icon,
            IconColor = iconColor,
            AppearingAnimation = new UXDivers.Popups.Maui.FadeInPopupAnimation() { Easing = EasingType.SpringIn },
            DisappearingAnimation = new UXDivers.Popups.Maui.FadeOutPopupAnimation() { Easing = EasingType.SpringOut }
        };

        await IPopupService.Current.PushAsync(toast);
        await Task.Delay(2000);
        await IPopupService.Current.PopAsync(toast);
    }
}
