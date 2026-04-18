using System.Reactive.Subjects;
using BaristaNotes.Models;

namespace BaristaNotes.Services;

public interface IFeedbackService
{
    Task ShowSuccessAsync(string message, int durationMs = 2000);
    Task ShowErrorAsync(string message, string? recoveryAction = null, int durationMs = 5000);
    Task ShowInfoAsync(string message, int durationMs = 3000);
    Task ShowWarningAsync(string message, int durationMs = 3000);
    IObservable<FeedbackMessage> FeedbackMessages { get; }
    IObservable<(bool IsLoading, string? Message)> LoadingState { get; }
}
