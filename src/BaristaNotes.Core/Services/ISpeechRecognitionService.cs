using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Service for speech-to-text recognition using on-device processing.
/// </summary>
public interface ISpeechRecognitionService
{
    /// <summary>
    /// Current state of the speech recognition service.
    /// </summary>
    SpeechRecognitionState State { get; }

    /// <summary>
    /// Event raised when the recognition state changes.
    /// </summary>
    event EventHandler<SpeechRecognitionState>? StateChanged;

    /// <summary>
    /// Event raised when partial recognition results are available.
    /// </summary>
    event EventHandler<string>? PartialResultReceived;

    /// <summary>
    /// Checks if speech recognition is available on this device.
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Requests permission for speech recognition and microphone access.
    /// </summary>
    /// <returns>True if permissions granted, false otherwise.</returns>
    Task<bool> RequestPermissionsAsync();

    /// <summary>
    /// Starts listening for speech input.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The recognition result with transcript and confidence.</returns>
    Task<SpeechRecognitionResultDto> StartListeningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the current listening session.
    /// </summary>
    Task StopListeningAsync();
}
