using CommunityToolkit.Maui.Media;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace BaristaNotes.Services;

/// <summary>
/// Speech recognition service using CommunityToolkit.Maui for on-device processing.
/// </summary>
public class SpeechRecognitionService : ISpeechRecognitionService
{
    private readonly ISpeechToText _speechToText;
    private readonly ILogger<SpeechRecognitionService> _logger;
    private SpeechRecognitionState _state = SpeechRecognitionState.Idle;
    private CancellationTokenSource? _currentCts;
    private TaskCompletionSource<SpeechRecognitionResultDto>? _recognitionTcs;

    public SpeechRecognitionState State
    {
        get => _state;
        private set
        {
            if (_state != value)
            {
                _state = value;
                StateChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<SpeechRecognitionState>? StateChanged;
    public event EventHandler<string>? PartialResultReceived;

    public SpeechRecognitionService(ISpeechToText speechToText, ILogger<SpeechRecognitionService> logger)
    {
        _speechToText = speechToText;
        _logger = logger;

        // Subscribe to completion event once (like telepathy sample)
        _speechToText.RecognitionResultCompleted += OnRecognitionResultCompleted;
    }

    public Task<bool> IsAvailableAsync()
    {
        // Speech recognition is available if the device supports it
        return Task.FromResult(true);
    }

    public async Task<bool> RequestPermissionsAsync()
    {
        // On iOS, calling RequestPermissions can crash with TCC/SIGABRT errors
        // Instead, we'll let StartListenAsync trigger the permission prompt naturally
        // and catch any permission-related errors there
        try
        {
            _logger.LogDebug("Checking speech recognition permission status");

            // Check permission status without requesting (avoids crash)
            var micStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            var speechStatus = await Permissions.CheckStatusAsync<Permissions.Speech>();

            _logger.LogInformation("Permission status: Mic={MicStatus}, Speech={SpeechStatus}",
                micStatus, speechStatus);

            // If already granted, return true
            if (micStatus == PermissionStatus.Granted && speechStatus == PermissionStatus.Granted)
            {
                return true;
            }

            // If not determined, we need to trigger the prompt via StartListenAsync
            // Return true to allow the attempt, which will prompt for permission
            if (micStatus == PermissionStatus.Unknown || speechStatus == PermissionStatus.Unknown)
            {
                _logger.LogDebug("Permissions undetermined, will prompt on StartListenAsync");
                return true; // Let StartListenAsync trigger the prompt
            }

            // If denied or restricted, return false
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking speech recognition permissions");
            // Return true to attempt anyway - StartListenAsync will handle the error
            return true;
        }
    }

    public async Task<SpeechRecognitionResultDto> StartListeningAsync(CancellationToken cancellationToken = default)
    {
        if (State == SpeechRecognitionState.Listening)
        {
            _logger.LogWarning("Already listening, ignoring start request");
            return new SpeechRecognitionResultDto
            {
                Success = false,
                ErrorMessage = "Already listening"
            };
        }

        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _recognitionTcs = new TaskCompletionSource<SpeechRecognitionResultDto>();
        State = SpeechRecognitionState.Listening;

        // Add a safety timeout (60 seconds max listening)
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _currentCts.Token, timeoutCts.Token);

        try
        {
            _logger.LogDebug("Starting speech recognition");

            // Subscribe to partial results for live feedback
            _speechToText.RecognitionResultUpdated += OnRecognitionResultUpdated;

            // Start listening with options
            var options = new SpeechToTextOptions
            {
                Culture = CultureInfo.CurrentCulture,
                ShouldReportPartialResults = true
            };

            await _speechToText.StartListenAsync(options, combinedCts.Token);

            // Wait for completion with timeout
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60), combinedCts.Token);
            var completedTask = await Task.WhenAny(_recognitionTcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("Speech recognition timed out after 60 seconds");
                await StopListeningAsync();
                return new SpeechRecognitionResultDto
                {
                    Success = false,
                    ErrorMessage = "Listening timed out. Please try again."
                };
            }

            return await _recognitionTcs.Task;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Speech recognition cancelled");
            State = SpeechRecognitionState.Idle;
            return new SpeechRecognitionResultDto
            {
                Success = false,
                ErrorMessage = "Cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during speech recognition");
            State = SpeechRecognitionState.Error;
            return new SpeechRecognitionResultDto
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            // Unsubscribe from partial results
            _speechToText.RecognitionResultUpdated -= OnRecognitionResultUpdated;
            _currentCts?.Dispose();
            _currentCts = null;
        }
    }

    public async Task StopListeningAsync()
    {
        _logger.LogDebug("Stopping speech recognition");

        try
        {
            await _speechToText.StopListenAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping speech recognition");
        }

        State = SpeechRecognitionState.Idle;
    }

    private void OnRecognitionResultUpdated(object? sender, SpeechToTextRecognitionResultUpdatedEventArgs args)
    {
        _logger.LogDebug("Partial result: {Text}", args.RecognitionResult);
        PartialResultReceived?.Invoke(this, args.RecognitionResult);
    }

    private void OnRecognitionResultCompleted(object? sender, SpeechToTextRecognitionResultCompletedEventArgs args)
    {
        State = SpeechRecognitionState.Processing;

        var result = args.RecognitionResult;
        _logger.LogInformation("Recognition completed: Successful={IsSuccessful}", result.IsSuccessful);
        _logger.LogInformation("Final result text: {Text}", result.Text);
        if (result.IsSuccessful && !string.IsNullOrEmpty(result.Text))
        {
            _logger.LogInformation("Speech recognition successful: {Text}", result.Text);
            State = SpeechRecognitionState.Idle;
            _recognitionTcs?.TrySetResult(new SpeechRecognitionResultDto
            {
                Success = true,
                Transcript = result.Text,
                Confidence = 1.0
            });
        }
        else
        {
            var errorMessage = result.Exception?.Message ?? "No speech recognized";
            _logger.LogWarning("Speech recognition completed but failed: {Error}", errorMessage);
            State = SpeechRecognitionState.Error;
            _recognitionTcs?.TrySetResult(new SpeechRecognitionResultDto
            {
                Success = false,
                ErrorMessage = errorMessage
            });
        }
    }
}
