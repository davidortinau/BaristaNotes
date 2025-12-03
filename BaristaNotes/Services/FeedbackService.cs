using System.Reactive.Subjects;
using BaristaNotes.Models;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;

namespace BaristaNotes.Services;

public class FeedbackService : IFeedbackService
{
    private readonly Subject<FeedbackMessage> _feedbackSubject = new();
    private readonly Subject<(bool IsLoading, string? Message)> _loadingSubject = new();
    private readonly Queue<FeedbackMessage> _errorQueue = new();
    private bool _isShowingError = false;

    public IObservable<FeedbackMessage> FeedbackMessages => _feedbackSubject;
    public IObservable<(bool IsLoading, string? Message)> LoadingState => _loadingSubject;

    public void ShowSuccess(string message, int durationMs = 2000)
    {
        ValidateMessage(message, durationMs);
        var feedbackMessage = new FeedbackMessage
        {
            Type = FeedbackType.Success,
            Message = message,
            DurationMs = durationMs
        };
        
        PublishMessage(feedbackMessage);
        ShowToast("✓", Colors.Green, message, durationMs);
    }

    public void ShowError(string message, string? recoveryAction = null, int durationMs = 5000)
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
            ShowToast("✕", Colors.Red, message, durationMs);
            
            Task.Delay(durationMs).ContinueWith(_ =>
            {
                _isShowingError = false;
                if (_errorQueue.Count > 0)
                {
                    var nextError = _errorQueue.Dequeue();
                    ShowError(nextError.Message, nextError.RecoveryAction, nextError.DurationMs);
                }
            });
        }
    }

    public void ShowInfo(string message, int durationMs = 3000)
    {
        ValidateMessage(message, durationMs);
        var feedbackMessage = new FeedbackMessage
        {
            Type = FeedbackType.Info,
            Message = message,
            DurationMs = durationMs
        };
        
        PublishMessage(feedbackMessage);
        ShowToast("ⓘ", Colors.Blue, message, durationMs);
    }

    public void ShowWarning(string message, int durationMs = 3000)
    {
        ValidateMessage(message, durationMs);
        var feedbackMessage = new FeedbackMessage
        {
            Type = FeedbackType.Warning,
            Message = message,
            DurationMs = durationMs
        };
        
        PublishMessage(feedbackMessage);
        ShowToast("⚠", Colors.Orange, message, durationMs);
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

    private async void ShowToast(string icon, Color iconColor, string message, int durationMs)
    {
        Application.Current?.Dispatcher.Dispatch(async () =>
        {
            try
            {
                var toast = new Toast
                {
                    IconText = icon,
                    IconColor = iconColor,
                    Title = message
                };

                await IPopupService.Current.PushAsync(toast);
                
                // Auto-dismiss after duration
                await Task.Delay(durationMs);
                await IPopupService.Current.PopAsync();
            }
            catch (Exception ex)
            {
                // Fallback: just log if popup fails
                System.Diagnostics.Debug.WriteLine($"Failed to show toast: {ex.Message}");
            }
        });
    }
}
