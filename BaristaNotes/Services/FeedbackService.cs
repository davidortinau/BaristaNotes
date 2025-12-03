using System.Reactive.Subjects;
using BaristaNotes.Models;
using UXDivers.Popups.Services;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using UXDivers.Popups;
using UXDivers.Popups.Maui.Controls;

namespace BaristaNotes.Services;

public class FeedbackService : IFeedbackService
{
    private readonly Subject<FeedbackMessage> _feedbackSubject = new();
    private readonly Subject<(bool IsLoading, string? Message)> _loadingSubject = new();
    private readonly Queue<FeedbackMessage> _errorQueue = new();
    private bool _isShowingError = false;

    public IObservable<FeedbackMessage> FeedbackMessages => _feedbackSubject;
    public IObservable<(bool IsLoading, string? Message)> LoadingState => _loadingSubject;

    public async Task ShowSuccessAsync(string message, int durationMs = 2000)
    {
        ValidateMessage(message, durationMs);
        var feedbackMessage = new FeedbackMessage
        {
            Type = FeedbackType.Success,
            Message = message,
            DurationMs = durationMs
        };

        PublishMessage(feedbackMessage);
        await ShowToastAsync(Color.FromArgb("#2D5016"), Color.FromArgb("#7CFC00"), "✓", message, durationMs);
    }

    public async Task ShowErrorAsync(string message, string? recoveryAction = null, int durationMs = 5000)
    {
        ValidateMessage(message, durationMs);

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
            PublishMessage(errorMessage);
            await ShowToastAsync(Color.FromArgb("#5C1A1A"), Color.FromArgb("#FF6B6B"), "✕", message, durationMs);

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
        ValidateMessage(message, durationMs);
        var feedbackMessage = new FeedbackMessage
        {
            Type = FeedbackType.Info,
            Message = message,
            DurationMs = durationMs
        };

        PublishMessage(feedbackMessage);
        await ShowToastAsync(Color.FromArgb("#1A3A5C"), Color.FromArgb("#4A9EFF"), "ℹ", message, durationMs);
    }

    public async Task ShowWarningAsync(string message, int durationMs = 3000)
    {
        ValidateMessage(message, durationMs);
        var feedbackMessage = new FeedbackMessage
        {
            Type = FeedbackType.Warning,
            Message = message,
            DurationMs = durationMs
        };

        PublishMessage(feedbackMessage);
        await ShowToastAsync(Color.FromArgb("#5C4A1A"), Color.FromArgb("#FFB74A"), "⚠", message, durationMs);
    }

    public void ShowLoading(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Loading message cannot be empty", nameof(message));

        Application.Current?.Dispatcher.Dispatch(() =>
        {
            _loadingSubject.OnNext((true, message));
        });
    }

    public void HideLoading()
    {
        Application.Current?.Dispatcher.Dispatch(() =>
        {
            _loadingSubject.OnNext((false, null));
        });
    }

    private void ValidateMessage(string message, int durationMs)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message cannot be empty", nameof(message));

        if (message.Length > 200)
            throw new ArgumentException("Message must be 200 characters or less", nameof(message));

        if (durationMs < 1000 || durationMs > 10000)
            throw new ArgumentException("Duration must be between 1-10 seconds", nameof(durationMs));
    }

    private void PublishMessage(FeedbackMessage message)
    {
        Application.Current?.Dispatcher.Dispatch(() =>
        {
            _feedbackSubject.OnNext(message);
        });
    }

    private async Task ShowToastAsync(Color bgColor, Color iconColor, string icon, string message, int durationMs)
    {
        System.Diagnostics.Debug.WriteLine($"[FeedbackService] ShowToastAsync called: {message}");
        
        await Application.Current!.Dispatcher.DispatchAsync(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FeedbackService] Inside dispatcher, about to create popup");
                // await AppShell.DisplayToastAsync(message);

                var popup = new UXDivers.Popups.Maui.PopupPage
                {
                    BackgroundColor = Colors.Transparent,
                    CloseWhenBackgroundIsClicked = false,
                    InputTransparent = true, // Don't block input to underlying page
                };

                popup.AppearingAnimation = new UXDivers.Popups.Maui.FadeInPopupAnimation();
                popup.DisappearingAnimation = new UXDivers.Popups.Maui.FadeOutPopupAnimation();
                popup.VerticalOptions = LayoutOptions.End;
                popup.CloseWhenBackgroundIsClicked = true;
                popup.HeightRequest = 200;

                var content = new Border
                {
                    BackgroundColor = bgColor,
                    Stroke = bgColor,
                    StrokeShape = new RoundRectangle { CornerRadius = 12 },
                    Padding = new Thickness(16, 12),
                    Margin = new Thickness(20, 50, 20, 0), // Top margin to position at top
                    VerticalOptions = LayoutOptions.Start, // Position at top
                    HorizontalOptions = LayoutOptions.Center,
                    Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.3f, Radius = 10, Offset = new Point(0, 4) },
                    Content = new HorizontalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Label
                            {
                                Text = icon,
                                FontSize = 20,
                                TextColor = iconColor,
                                VerticalOptions = LayoutOptions.Center
                            },
                            new Label
                            {
                                Text = message,
                                FontSize = 14,
                                TextColor = Colors.White,
                                VerticalOptions = LayoutOptions.Center,
                                LineBreakMode = LineBreakMode.WordWrap
                            }
                        }
                    }
                };

                popup.Content = content;
                // popup.PopupContent = content;

                // var p = new Toast()
                // {
                //     Title = "David's Successful Toast"
                // };

                // await IPopupService.Current.PushAsync(p);

                System.Diagnostics.Debug.WriteLine($"[FeedbackService] About to push popup");
                await IPopupService.Current.PushAsync(popup);
                System.Diagnostics.Debug.WriteLine($"[FeedbackService] Popup pushed, waiting {durationMs}ms");
                await Task.Delay(durationMs);
                System.Diagnostics.Debug.WriteLine($"[FeedbackService] About to pop popup");
                await IPopupService.Current.PopAsync();
                System.Diagnostics.Debug.WriteLine($"[FeedbackService] Popup popped successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FeedbackService] Failed to show toast: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[FeedbackService] Stack trace: {ex.StackTrace}");
            }
        });
    }
}
