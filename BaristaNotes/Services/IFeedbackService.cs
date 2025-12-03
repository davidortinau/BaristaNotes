using System.Reactive.Subjects;
using BaristaNotes.Models;

namespace BaristaNotes.Services;

public interface IFeedbackService
{
    void ShowSuccess(string message, int durationMs = 2000);
    void ShowError(string message, string? recoveryAction = null, int durationMs = 5000);
    void ShowInfo(string message, int durationMs = 3000);
    void ShowWarning(string message, int durationMs = 3000);
    void ShowLoading(string message);
    void HideLoading();
    IObservable<FeedbackMessage> FeedbackMessages { get; }
    IObservable<(bool IsLoading, string? Message)> LoadingState { get; }
}
