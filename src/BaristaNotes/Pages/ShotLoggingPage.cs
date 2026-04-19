using MauiReactor;
using MauiReactor.Animations;
using MauiReactor.Shapes;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Services;
using BaristaNotes.Styles;
using BaristaNotes.Components.FormFields;
using BaristaNotes.Components;
using BaristaNotes.Integrations.Popups;
using UXDivers.Popups.Maui.Controls;
using UXDivers.Popups.Services;
using Fonts;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
using Application = Microsoft.Maui.Controls.Application;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace BaristaNotes.Pages;

class ShotLoggingState
{
    public decimal DoseIn { get; set; } = 18.0m;
    public string GrindSetting { get; set; } = "5.5";
    public decimal ExpectedTime { get; set; } = 28;
    public decimal ExpectedOutput { get; set; } = 36.0m;
    public string DrinkType { get; set; } = "Espresso";
    /// <summary>
    /// Brew method for this drink. Drives form defaults and equipment
    /// filtering. Defaults to Espresso for back-compat.
    /// </summary>
    public BrewMethod BrewMethod { get; set; } = BrewMethod.Espresso;
    public int SelectedBrewMethodIndex { get; set; } = 0;
    public decimal? ActualTime { get; set; }
    public decimal? ActualOutput { get; set; }
    public int Rating { get; set; } = 2;
    public string? TastingNotes { get; set; }
    public int? SelectedBagId { get; set; }
    public int SelectedBagIndex { get; set; } = -1;
    public int SelectedDrinkIndex { get; set; } = 0;
    public List<BagSummaryDto> AvailableBags { get; set; } = new();
    public List<string> DrinkTypes { get; set; } = new() { "Espresso", "Americano", "Latte", "Cappuccino", "Flat White", "Cortado", "Pour Over" };
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }

    // Edit mode fields
    public DateTimeOffset? Timestamp { get; set; }
    public string? BeanName { get; set; }

    // User tracking fields
    public List<UserProfileDto> AvailableUsers { get; set; } = new();
    public UserProfileDto? SelectedMaker { get; set; }
    public UserProfileDto? SelectedRecipient { get; set; }

    // Equipment tracking fields
    public List<EquipmentDto> AvailableEquipment { get; set; } = new();
    public int? SelectedMachineId { get; set; }
    public int? SelectedGrinderId { get; set; }
    public List<int> SelectedAccessoryIds { get; set; } = new();

    // Bean tracking for inline creation (T001)
    public List<BeanDto> AvailableBeans { get; set; } = new();

    // AI advice state (for existing shots in edit/view mode)
    public bool IsLoadingAdvice { get; set; }
    public AIAdviceResponseDto? AdviceResponse { get; set; }
    public bool ShowAdviceSection { get; set; }
    public bool ShowPromptDetails { get; set; }

    // Animation state for loading bar
    public double LoadingBarPosition { get; set; } = -80;

    // Voice command state
    public bool IsVoiceSheetOpen { get; set; } // Whether the bottom sheet is visible
    public bool IsRecording { get; set; } // Whether actively recording
    public string VoiceTranscript { get; set; } = ""; // Current recording transcript
    public SpeechRecognitionState VoiceState { get; set; } = SpeechRecognitionState.Idle;
    public bool VoiceCommandCommitted { get; set; } // Flag to prevent double-processing
    public double VoicePulseScale { get; set; } = 1.0; // Animation scale for pulsing mic icon
    public List<VoiceChatMessage> VoiceChatHistory { get; set; } = new(); // Conversation history
    public string LastAIResponse { get; set; } = ""; // Last AI response to keep visible until user speaks

    // Recipe lookup for the currently selected bean + brew method. Used by
    // the recipe preview card on the drink-logging page (shows the official
    // roaster recipe if available + a one-tap "Apply" button).
    public RecipeDto? CurrentRecipe { get; set; }
    public bool IsLoadingRecipe { get; set; }
    // (beanId, method) tuple we last looked up, to avoid redundant fetches.
    public (int beanId, BrewMethod method)? LastRecipeLookup { get; set; }
}

/// <summary>
/// Represents a message in the voice chat history.
/// </summary>
class VoiceChatMessage
{
    public bool IsUser { get; set; } // true = user spoke, false = AI response
    public string Text { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsError { get; set; } // For error messages
}

class ShotLoggingPageProps
{
    public int? ShotId { get; set; }
}

partial class ShotLoggingPage : Component<ShotLoggingState, ShotLoggingPageProps>
{
    [Inject]
    IShotService _shotService;

    [Inject]
    IBeanService _beanService;

    [Inject]
    IBagService _bagService;

    [Inject]
    IEquipmentService _equipmentService;

    [Inject]
    IPreferencesService _preferencesService;

    [Inject]
    IFeedbackService _feedbackService;

    [Inject]
    IUserProfileService _userProfileService;

    [Inject]
    IAIAdviceService _aiAdviceService;

    [Inject]
    ILogger<ShotLoggingPage> _logger;

    [Inject]
    IDataChangeNotifier _dataChangeNotifier;

    [Inject]
    ISpeechRecognitionService _speechRecognitionService;

    [Inject]
    IVoiceCommandService _voiceCommandService;

    [Inject]
    IOverlayService _overlayService;

    [Inject]
    IVisionService _visionService;

    [Inject]
    IRecipeService _recipeService;

    [Inject]
    IServiceProvider _services;

    // Cancellation token for AI recommendation requests
    private CancellationTokenSource? _recommendationCts;

    // Cancellation token for voice commands
    private CancellationTokenSource? _voiceCts;

    // Flag to pause speech recognition (e.g., during camera capture)
    private bool _speechPaused = false;

    // Silence detection timer - fires when user stops speaking
    private System.Timers.Timer? _silenceTimer;
    private const double SilenceTimeoutMs = 1500; // 1.5 seconds of silence triggers processing
    private readonly object _silenceLock = new();

    protected override void OnMounted()
    {
        base.OnMounted();
        SetState(s => s.IsLoading = true);
        _ = LoadDataAsync();
        DeviceDisplay.Current.MainDisplayInfoChanged += OnMainDisplayInfoChanged;

        // Subscribe to data changes from voice commands
        _dataChangeNotifier.DataChanged += OnDataChanged;

        // Subscribe to overlay events
        _overlayService.CloseRequested += OnOverlayCloseRequested;
        _overlayService.ExpandRequested += OnOverlayExpandRequested;
        _overlayService.MicPressStarted += OnMicPressStarted;
        _overlayService.MicPressEnded += OnMicPressEnded;

        // Subscribe to voice command speech control events
        _voiceCommandService.PauseSpeechRequested += OnPauseSpeechRequested;
        _voiceCommandService.ResumeSpeechRequested += OnResumeSpeechRequested;
    }

    protected override void OnWillUnmount()
    {
        _recommendationCts?.Cancel();
        _recommendationCts?.Dispose();
        _voiceCts?.Cancel();
        _voiceCts?.Dispose();
        DeviceDisplay.Current.MainDisplayInfoChanged -= OnMainDisplayInfoChanged;
        _dataChangeNotifier.DataChanged -= OnDataChanged;
        _overlayService.CloseRequested -= OnOverlayCloseRequested;
        _overlayService.ExpandRequested -= OnOverlayExpandRequested;
        _overlayService.MicPressStarted -= OnMicPressStarted;
        _overlayService.MicPressEnded -= OnMicPressEnded;
        _voiceCommandService.PauseSpeechRequested -= OnPauseSpeechRequested;
        _voiceCommandService.ResumeSpeechRequested -= OnResumeSpeechRequested;
        base.OnWillUnmount();
    }

    /// <summary>
    /// Handle overlay close request from user tapping close button.
    /// </summary>
    private async void OnOverlayCloseRequested(object? sender, EventArgs e)
    {
        if (State.IsVoiceSheetOpen)
        {
            await CloseVoiceOverlayAsync();
        }
    }

    /// <summary>
    /// Handle overlay expand request from user tapping the collapsed FAB.
    /// </summary>
    private void OnOverlayExpandRequested(object? sender, EventArgs e)
    {
        if (State.IsVoiceSheetOpen && _overlayService.IsCollapsed)
        {
            _overlayService.Expand();
        }
    }

    /// <summary>
    /// Handle push-to-talk: user pressed down on the mic button.
    /// Starts recording immediately.
    /// </summary>
    private async void OnMicPressStarted(object? sender, EventArgs e)
    {
        _logger.LogInformation("Push-to-talk: mic button pressed");
        if (!State.IsVoiceSheetOpen || State.IsRecording) return;
        await StartRecordingAsync();
    }

    /// <summary>
    /// Handle push-to-talk: user released the mic button.
    /// Stops recording and processes the transcript.
    /// </summary>
    private async void OnMicPressEnded(object? sender, EventArgs e)
    {
        _logger.LogInformation("Push-to-talk: mic button released");
        if (!State.IsRecording) return;
        await StopRecordingAsync();
    }

    /// <summary>
    /// Handle request to pause speech recognition (e.g., when camera opens).
    /// </summary>
    private async void OnPauseSpeechRequested(object? sender, EventArgs e)
    {
        _logger.LogDebug("Pause speech requested - stopping listening for camera");
        _speechPaused = true;
        if (State.IsRecording)
        {
            await _speechRecognitionService.StopListeningAsync();
            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Taking photo...",
                Transcript: State.VoiceTranscript,
                IsListening: false,
                IsProcessing: true,
                AIResponse: State.LastAIResponse
            ));
        }
    }

    /// <summary>
    /// Handle request to resume speech recognition (e.g., after camera closes).
    /// Note: Currently not invoked - speech resume is handled via _speechPaused flag
    /// in ProcessTranscriptAsync after full AI processing completes.
    /// </summary>
    private void OnResumeSpeechRequested(object? sender, EventArgs e)
    {
        _logger.LogDebug("Resume speech requested via event");
        _speechPaused = false;
    }

    private void OnMainDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
        Invalidate();
    }

    /// <summary>
    /// Handle data changes from voice commands (or other sources).
    /// Refreshes relevant pickers when new beans/bags/equipment/profiles are created.
    /// </summary>
    private void OnDataChanged(object? sender, DataChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            switch (e.ChangeType)
            {
                case DataChangeType.BeanCreated:
                case DataChangeType.BagCreated:
                    // Refresh bags when beans or bags are added via voice
                    var bags = await _bagService.GetActiveBagsForShotLoggingAsync();
                    var beans = await _beanService.GetAllActiveBeansAsync();
                    SetState(s =>
                    {
                        s.AvailableBags = bags;
                        s.AvailableBeans = beans;
                    });
                    break;

                case DataChangeType.EquipmentCreated:
                    // Refresh equipment when added via voice
                    var equipment = await _equipmentService.GetAllActiveEquipmentAsync();
                    SetState(s => s.AvailableEquipment = equipment.ToList());
                    break;

                case DataChangeType.ProfileCreated:
                    // Refresh users when profile added via voice
                    var users = await _userProfileService.GetAllProfilesAsync();
                    SetState(s => s.AvailableUsers = users);
                    break;
            }
        });
    }

    /// <summary>
    /// Toggles the voice chat sheet open/closed.
    /// When opening, automatically starts recording and hides the tab bar.
    /// </summary>
    private async Task ToggleVoiceSheetAsync()
    {
        if (State.IsVoiceSheetOpen)
        {
            await CloseVoiceOverlayAsync();
        }
        else
        {
            // Clear conversation history for new session
            _voiceCommandService.ClearConversationHistory();

            // Clear chat history UI as well
            SetState(s => s.VoiceChatHistory = new List<VoiceChatMessage>());

            // Show the window overlay in Ready state (mic NOT listening)
            _overlayService.Show();
            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Ready",
                Transcript: "",
                IsListening: false,
                IsProcessing: false,
                IsReady: true
            ));

            // Update state - overlay is open but NOT recording yet
            SetState(s => s.IsVoiceSheetOpen = true);
        }
    }

    /// <summary>
    /// Closes the voice overlay and stops recording.
    /// </summary>
    private async Task CloseVoiceOverlayAsync()
    {
        if (State.IsRecording)
        {
            await StopRecordingAsync();
        }
        SetState(s => s.IsVoiceSheetOpen = false);

        // Hide the window overlay
        _overlayService.Hide();
    }

    /// <summary>
    /// Toggles recording on/off within the voice sheet.
    /// </summary>
    private async Task ToggleRecordingAsync()
    {
        _logger.LogInformation("ToggleRecordingAsync called, IsRecording={IsRecording}", State.IsRecording);

        if (State.IsRecording)
        {
            _logger.LogInformation("Stopping recording, current transcript: '{Transcript}'", State.VoiceTranscript);
            // Just stop recording - any pending utterance will be processed
            await StopRecordingAsync();
        }
        else
        {
            await StartRecordingAsync();
        }
    }

    /// <summary>
    /// Starts voice recording for a single push-to-talk session.
    /// Recording will stop when the user releases the mic button.
    /// </summary>
    private async Task StartRecordingAsync()
    {
        try
        {
            _logger.LogInformation("Starting voice recording (push-to-talk)");

            // Check permissions
            var permissionStatus = await _speechRecognitionService.RequestPermissionsAsync();
            if (!permissionStatus)
            {
                _logger.LogWarning("Speech recognition permission denied");
                AddChatMessage("Microphone permission is required. Please enable it in Settings.", isUser: false, isError: true);
                return;
            }

            _voiceCts = new CancellationTokenSource();

            SetState(s =>
            {
                s.IsRecording = true;
                s.VoiceTranscript = "";
                s.VoiceState = SpeechRecognitionState.Listening;
                s.VoiceCommandCommitted = false;
            });

            // Update overlay content
            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Listening...",
                Transcript: "",
                IsListening: true,
                IsProcessing: false
            ));

            // Subscribe to partial results
            _speechRecognitionService.PartialResultReceived += OnPartialResultReceived;

            // Start a single listening session (not a continuous loop)
            _ = ListenForSpeechAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting voice recording");
            AddChatMessage("Failed to start recording. Please try again.", isUser: false, isError: true);
        }
    }

    /// <summary>
    /// Background task that performs a single speech recognition session.
    /// The user holds the mic button to record; releasing triggers StopRecordingAsync
    /// which cancels this task and processes the result.
    /// </summary>
    private async Task ListenForSpeechAsync()
    {
        _logger.LogInformation("ListenForSpeechAsync started (single-shot push-to-talk)");
        try
        {
            if (_voiceCts?.Token.IsCancellationRequested ?? true)
            {
                _logger.LogInformation("Already cancelled before starting");
                return;
            }

            _logger.LogDebug("Starting speech recognition, IsRecording={IsRecording}", State.IsRecording);

            var result = await _speechRecognitionService.StartListeningAsync(_voiceCts?.Token ?? CancellationToken.None);

            _logger.LogInformation("Speech recognition returned: Success={Success}, Transcript='{Transcript}', Error={Error}",
                result.Success, result.Transcript ?? "(null)", result.ErrorMessage ?? "(none)");

            // If cancelled (user released button), StopRecordingAsync handles processing
            if (_voiceCts?.Token.IsCancellationRequested ?? true)
            {
                _logger.LogInformation("Recognition ended via cancellation (button release)");
                return;
            }

            // If recognition completed naturally (e.g., timeout), process result
            if (result.Success && !string.IsNullOrWhiteSpace(result.Transcript))
            {
                _logger.LogInformation("Got natural completion with transcript: '{Text}'", result.Transcript);
                var transcript = result.Transcript;
                SetState(s => s.VoiceTranscript = "");
                await ProcessTranscriptAsync(transcript);
            }
            else if (!string.IsNullOrWhiteSpace(State.VoiceTranscript))
            {
                _logger.LogInformation("Recognition ended with accumulated partial text: '{Text}'", State.VoiceTranscript);
                var transcript = State.VoiceTranscript;
                SetState(s => s.VoiceTranscript = "");
                await ProcessTranscriptAsync(transcript);
            }
            else
            {
                _logger.LogDebug("Recognition ended with no usable text");
                // Return to ready state
                SetState(s =>
                {
                    s.IsRecording = false;
                    s.VoiceState = SpeechRecognitionState.Idle;
                });
                _overlayService.UpdateContent(new OverlayContent(
                    StateText: "Ready",
                    Transcript: "",
                    IsListening: false,
                    IsProcessing: false,
                    IsReady: true,
                    AIResponse: State.LastAIResponse
                ));
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ListenForSpeechAsync cancelled (push-to-talk button released)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during speech listening");
        }
        finally
        {
            _logger.LogInformation("ListenForSpeechAsync cleanup");
            StopSilenceTimer();
            _speechRecognitionService.PartialResultReceived -= OnPartialResultReceived;
        }
    }

    /// <summary>
    /// Stops recording and processes any pending transcript.
    /// Returns the overlay to Ready state after processing.
    /// </summary>
    private async Task StopRecordingAsync()
    {
        _logger.LogInformation("StopRecordingAsync called (push-to-talk release)");

        // Stop silence detection
        StopSilenceTimer();

        // Capture any pending transcript before stopping
        var pendingTranscript = State.VoiceTranscript;
        _logger.LogInformation("Pending transcript captured: '{Transcript}'", pendingTranscript ?? "(null)");

        _voiceCts?.Cancel();
        _logger.LogDebug("CancellationToken cancelled");

        await _speechRecognitionService.StopListeningAsync();
        _logger.LogDebug("StopListeningAsync completed");

        _speechRecognitionService.PartialResultReceived -= OnPartialResultReceived;
        SetState(s =>
        {
            s.IsRecording = false;
            s.VoiceTranscript = "";
            s.VoiceState = SpeechRecognitionState.Idle;
        });

        // Process pending transcript if we have one
        if (!string.IsNullOrWhiteSpace(pendingTranscript))
        {
            _logger.LogInformation("Processing pending transcript on stop: '{Text}'", pendingTranscript);
            await ProcessTranscriptAsync(pendingTranscript);
        }
        else
        {
            _logger.LogInformation("No pending transcript to process on stop");
            // Return to ready state with any previous AI response
            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Ready",
                Transcript: "",
                IsListening: false,
                IsProcessing: false,
                IsReady: true,
                AIResponse: State.LastAIResponse
            ));
        }
    }

    /// <summary>
    /// Processes a transcript and adds messages to chat history.
    /// Returns to Ready state after processing (push-to-talk model).
    /// </summary>
    private async Task ProcessTranscriptAsync(string transcript)
    {
        _logger.LogInformation("ProcessTranscriptAsync START: '{Transcript}'", transcript);

        // Add user message to chat
        AddChatMessage(transcript, isUser: true);
        _logger.LogDebug("Added user message to chat");

        // Show processing state
        SetState(s => s.VoiceState = SpeechRecognitionState.Processing);

        // Update overlay to show processing
        _overlayService.UpdateContent(new OverlayContent(
            StateText: "Processing...",
            Transcript: transcript,
            IsListening: false,
            IsProcessing: true
        ));

        try
        {
            _logger.LogInformation("Calling VoiceCommandService.ProcessCommandAsync");
            var commandResult = await _voiceCommandService.ProcessCommandAsync(
                new VoiceCommandRequestDto(transcript, 1.0),
                CancellationToken.None);

            _logger.LogInformation("VoiceCommandService returned: Success={Success}, Message='{Message}'",
                commandResult.Success, commandResult.Message);

            // Add AI response to chat
            AddChatMessage(commandResult.Message, isUser: false, isError: !commandResult.Success);

            // Store the last AI response for display persistence
            SetState(s => s.LastAIResponse = commandResult.Message);

            // Return to Ready state with AI response visible
            SetState(s =>
            {
                s.VoiceTranscript = "";
                s.VoiceState = SpeechRecognitionState.Idle;
            });

            // Resume speech if it was paused (e.g., during camera capture)
            if (_speechPaused)
            {
                _logger.LogDebug("Clearing speech pause flag after command processing");
                _speechPaused = false;
            }

            // Show Ready state with AI response — user can press mic again
            _overlayService.UpdateContent(new OverlayContent(
                StateText: commandResult.Success ? "Done" : "Error",
                Transcript: "",
                IsListening: false,
                IsProcessing: false,
                IsReady: true,
                AIResponse: commandResult.Message
            ));

            _logger.LogInformation("ProcessTranscriptAsync END: Success, returned to Ready state");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing voice command");
            AddChatMessage("Sorry, something went wrong. Please try again.", isUser: false, isError: true);
            
            var errorMessage = "Sorry, something went wrong. Please try again.";
            SetState(s => s.LastAIResponse = errorMessage);
            SetState(s => s.VoiceState = SpeechRecognitionState.Idle);
            
            // Resume speech if it was paused
            if (_speechPaused)
            {
                _logger.LogDebug("Clearing speech pause flag after error");
                _speechPaused = false;
            }

            // Return to Ready state with error visible
            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Error",
                Transcript: "",
                IsListening: false,
                IsProcessing: false,
                IsReady: true,
                ErrorMessage: "Something went wrong",
                AIResponse: errorMessage
            ));
        }
    }

    /// <summary>
    /// Adds a message to the voice chat history.
    /// </summary>
    private void AddChatMessage(string text, bool isUser, bool isError = false)
    {
        SetState(s =>
        {
            s.VoiceChatHistory.Add(new VoiceChatMessage
            {
                IsUser = isUser,
                Text = text,
                Timestamp = DateTime.Now,
                IsError = isError
            });
        });
    }

    /// <summary>
    /// Shows a dialog explaining permission denial with option to open Settings.
    /// </summary>
    private async Task ShowPermissionDeniedDialogAsync()
    {
        var openSettings = await Application.Current!.Windows[0].Page!.DisplayAlertAsync(
            "Permission Required",
            "Speech recognition requires microphone and speech permissions. Please enable them in Settings.",
            "Open Settings",
            "Cancel");

        if (openSettings)
        {
            AppInfo.ShowSettingsUI();
        }
    }

    /// <summary>
    /// Handles partial speech recognition results for live transcript display.
    /// On iOS, partial results come as individual words that need to be accumulated.
    /// Resets the silence timer on each partial result.
    /// </summary>
    private void OnPartialResultReceived(object? sender, string partialText)
    {
        _logger.LogInformation("PARTIAL: Received '{PartialText}', current transcript: '{Current}'",
            partialText, State.VoiceTranscript ?? "(null)");

        // Reset silence timer - user is still speaking
        ResetSilenceTimer();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            string newTranscript = "";
            SetState(s =>
            {
                // Clear last AI response when user starts speaking again
                s.LastAIResponse = "";
                
                // iOS sends individual words, so append with space
                if (!string.IsNullOrEmpty(s.VoiceTranscript) && !string.IsNullOrEmpty(partialText))
                {
                    s.VoiceTranscript += " " + partialText;
                }
                else
                {
                    s.VoiceTranscript = partialText;
                }
                newTranscript = s.VoiceTranscript;
                _logger.LogInformation("PARTIAL: Accumulated transcript: '{Transcript}'", s.VoiceTranscript);
            });

            // Update overlay with current transcript (AI response now cleared)
            _overlayService.UpdateContent(new OverlayContent(
                StateText: "Listening...",
                Transcript: newTranscript,
                IsListening: true,
                IsProcessing: false
            ));
        });
    }

    /// <summary>
    /// Resets the silence detection timer. Called when partial results arrive.
    /// </summary>
    private void ResetSilenceTimer()
    {
        lock (_silenceLock)
        {
            if (_silenceTimer == null)
            {
                _silenceTimer = new System.Timers.Timer(SilenceTimeoutMs);
                _silenceTimer.AutoReset = false;
                _silenceTimer.Elapsed += OnSilenceDetected;
                _logger.LogInformation("SILENCE: Created new timer with {Timeout}ms timeout", SilenceTimeoutMs);
            }

            _silenceTimer.Stop();
            _silenceTimer.Start();
            _logger.LogInformation("SILENCE: Timer reset/started - waiting for {Timeout}ms of silence", SilenceTimeoutMs);
        }
    }

    /// <summary>
    /// Stops and disposes the silence detection timer.
    /// </summary>
    private void StopSilenceTimer()
    {
        lock (_silenceLock)
        {
            if (_silenceTimer != null)
            {
                _silenceTimer.Stop();
                _silenceTimer.Elapsed -= OnSilenceDetected;
                _silenceTimer.Dispose();
                _silenceTimer = null;
                _logger.LogInformation("SILENCE: Timer stopped and disposed");
            }
            else
            {
                _logger.LogDebug("SILENCE: StopSilenceTimer called but timer was null");
            }
        }
    }

    /// <summary>
    /// Called when silence is detected (no partial results for SilenceTimeoutMs).
    /// Stops recognition to trigger the final result (higher quality than partials).
    /// The ListenForSpeechAsync loop will handle processing and restarting.
    /// </summary>
    private void OnSilenceDetected(object? sender, System.Timers.ElapsedEventArgs e)
    {
        _logger.LogInformation("SILENCE: >>> Timer elapsed! Silence detected after {Timeout}ms <<<", SilenceTimeoutMs);
        _logger.LogInformation("SILENCE: Current state - IsRecording={IsRecording}, VoiceTranscript='{Transcript}'",
            State.IsRecording, State.VoiceTranscript ?? "(null)");

        if (!State.IsRecording)
        {
            _logger.LogWarning("SILENCE: Not recording anymore, skipping");
            return;
        }

        if (string.IsNullOrWhiteSpace(State.VoiceTranscript))
        {
            _logger.LogWarning("SILENCE: No transcript accumulated, skipping stop");
            return;
        }

        // Stop listening to trigger final recognition result
        // This will cause RecognitionResultCompleted to fire with higher-quality text
        // The ListenForSpeechAsync loop will process it and restart
        _logger.LogInformation("SILENCE: Stopping recognition to get final result...");
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await _speechRecognitionService.StopListeningAsync();
            _logger.LogInformation("SILENCE: StopListeningAsync completed, waiting for recognition loop to process");
        });
    }

    void OnPageAppearing()
    {

        //On<iOS>().SetLargeTitleDisplay(LargeTitleDisplayMode.Always);
        // Reload data when tab becomes visible (handles new beans added from Settings)
        _ = LoadDataAsync();
    }

    async Task LoadDataAsync()
    {
        try
        {
            var bags = await _bagService.GetActiveBagsForShotLoggingAsync();
            var users = await _userProfileService.GetAllProfilesAsync();
            var equipment = await _equipmentService.GetAllActiveEquipmentAsync();
            var beans = await _beanService.GetAllActiveBeansAsync(); // T002: Load beans for inline creation

            // Edit mode: Load existing shot
            if (Props.ShotId.HasValue)
            {
                var shot = await _shotService.GetShotByIdAsync(Props.ShotId.Value);
                if (shot == null)
                {
                    await _feedbackService.ShowErrorAsync("Shot not found");
                    await Navigation.PopAsync();
                    return;
                }

                SetState(s =>
                {
                    s.AvailableBags = bags;
                    s.AvailableUsers = users;
                    s.AvailableEquipment = equipment.ToList();
                    s.AvailableBeans = beans; // T002: Track beans for inline creation

                    // Populate from existing shot
                    s.Timestamp = shot.Timestamp;
                    s.BeanName = shot.Bean?.Name;
                    s.DoseIn = shot.DoseIn;
                    s.GrindSetting = shot.GrindSetting;
                    s.ExpectedTime = shot.ExpectedTime;
                    s.ExpectedOutput = shot.ExpectedOutput;
                    s.ActualTime = shot.ActualTime;
                    s.ActualOutput = shot.ActualOutput;
                    // Convert from 1-5 service scale to 0-4 UI index (Rating 1 -> Index 0, Rating 5 -> Index 4)
                    s.Rating = shot.Rating.HasValue ? Math.Max(0, shot.Rating.Value - 1) : 2;
                    s.DrinkType = shot.DrinkType;
                    s.BrewMethod = shot.BrewMethod;
                    s.SelectedBrewMethodIndex = BrewMethodExtensions.All.ToList().IndexOf(shot.BrewMethod);
                    if (s.SelectedBrewMethodIndex < 0) s.SelectedBrewMethodIndex = 0;
                    s.SelectedBagId = shot.Bag?.Id;
                    s.SelectedBagIndex = shot.Bag != null ? s.AvailableBags.FindIndex(b => b.Id == shot.Bag.Id) : -1;
                    s.SelectedDrinkIndex = s.DrinkTypes.IndexOf(shot.DrinkType);

                    // Set maker/recipient from shot
                    s.SelectedMaker = shot.MadeBy;
                    s.SelectedRecipient = shot.MadeFor;

                    // Set equipment from shot
                    s.SelectedMachineId = shot.Machine?.Id;
                    s.SelectedGrinderId = shot.Grinder?.Id;
                    s.SelectedAccessoryIds = shot.Accessories?.Select(a => a.Id).ToList() ?? new();

                    s.IsLoading = false;
                });
            }
            // Add mode: Load last shot as template
            else
            {
                var lastShot = await _shotService.GetMostRecentShotAsync();

                SetState(s =>
                {
                    s.AvailableBags = bags;
                    s.AvailableUsers = users;
                    s.AvailableEquipment = equipment.ToList();
                    s.AvailableBeans = beans; // T002: Track beans for inline creation

                    if (lastShot != null)
                    {
                        s.DoseIn = lastShot.DoseIn;
                        s.GrindSetting = lastShot.GrindSetting;
                        s.ExpectedTime = lastShot.ExpectedTime;
                        s.ExpectedOutput = lastShot.ExpectedOutput;
                        s.ActualTime = lastShot.ActualTime;
                        s.ActualOutput = lastShot.ActualOutput;
                        s.Rating = lastShot.Rating ?? 2;
                        s.DrinkType = lastShot.DrinkType;
                        s.BrewMethod = lastShot.BrewMethod;
                        s.SelectedBrewMethodIndex = BrewMethodExtensions.All.ToList().IndexOf(lastShot.BrewMethod);
                        if (s.SelectedBrewMethodIndex < 0) s.SelectedBrewMethodIndex = 0;
                        s.SelectedBagId = lastShot.Bag?.Id;
                        s.SelectedBagIndex = lastShot.Bag != null ? s.AvailableBags.FindIndex(b => b.Id == lastShot.Bag.Id) : -1;
                        s.SelectedDrinkIndex = s.DrinkTypes.IndexOf(lastShot.DrinkType);

                        // Load last-used maker/recipient from preferences
                        var lastMakerId = _preferencesService.GetLastMadeById();
                        var lastRecipientId = _preferencesService.GetLastMadeForId();

                        if (lastMakerId.HasValue)
                            s.SelectedMaker = users.FirstOrDefault(u => u.Id == lastMakerId.Value);

                        if (lastRecipientId.HasValue)
                            s.SelectedRecipient = users.FirstOrDefault(u => u.Id == lastRecipientId.Value);
                    }

                    // Load last-used equipment from preferences (always, even without lastShot)
                    s.SelectedMachineId = _preferencesService.GetLastMachineId();
                    s.SelectedGrinderId = _preferencesService.GetLastGrinderId();
                    s.SelectedAccessoryIds = _preferencesService.GetLastAccessoryIds();

                    s.IsLoading = false;
                });

                // Kick off recipe lookup for the initial bag + brew method.
                _ = LoadRecipeForSelectionAsync();
            }
        }
        catch (Exception ex)
        {
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = ex.Message;
            });
        }
    }

    async Task LoadBestShotSettingsAsync(int bagId)
    {
        try
        {
            _logger.LogDebug("LoadBestShotSettingsAsync called for bagId: {BagId}", bagId);
            var bestShot = await _shotService.GetBestRatedShotByBagAsync(bagId);

            if (bestShot != null)
            {
                _logger.LogDebug("Found best shot: DoseIn={DoseIn}g, GrindSetting={GrindSetting}, ExpectedOutput={ExpectedOutput}g, ExpectedTime={ExpectedTime}s",
                    bestShot.DoseIn, bestShot.GrindSetting, bestShot.ExpectedOutput, bestShot.ExpectedTime);
                SetState(s =>
                {
                    s.DoseIn = bestShot.DoseIn;
                    s.ExpectedOutput = bestShot.ExpectedOutput;
                    s.ExpectedTime = bestShot.ExpectedTime;
                    s.GrindSetting = bestShot.GrindSetting;
                });
                await _feedbackService.ShowSuccessAsync("Loaded settings from your best rated shot");
            }
            else
            {
                _logger.LogDebug("No rated shots found for bagId: {BagId}", bagId);
                await _feedbackService.ShowSuccessAsync("No rated shots found for this bag");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading best shot settings");
            await _feedbackService.ShowErrorAsync("Failed to load shot settings");
        }
    }

    /// <summary>
    /// Requests AI recommendations for a bean when selected.
    /// Shows loading bar, populates fields with recommendations, and displays toast.
    /// </summary>
    async Task RequestAIRecommendationsAsync(int beanId)
    {
        // Cancel any pending request
        _recommendationCts?.Cancel();
        _recommendationCts = new CancellationTokenSource();

        SetState(s => s.IsLoadingAdvice = true);

        try
        {
            var result = await _aiAdviceService.GetRecommendationsForBeanAsync(
                beanId, _recommendationCts.Token);

            if (result.Success)
            {
                SetState(s =>
                {
                    s.DoseIn = result.Dose;
                    s.GrindSetting = result.GrindSetting;
                    s.ExpectedOutput = result.Output;
                    s.ExpectedTime = result.Duration;
                });

                var message = result.RecommendationType == RecommendationType.NewBean
                    ? $"We didn't have any shots for this bean, so we've created a recommended starting point: {result.Dose}g dose, {result.GrindSetting} grind, {result.Output}g output, {result.Duration}s."
                    : $"I see you're switching beans, so here's a recommended starting point: {result.Dose}g dose, {result.GrindSetting} grind, {result.Output}g output, {result.Duration}s.";

                await _feedbackService.ShowInfoAsync(message);
            }
            else
            {
                _logger.LogWarning("AI recommendations failed: {Error}", result.ErrorMessage);
                await _feedbackService.ShowErrorAsync("Couldn't get AI recommendations. Enter values manually or try again.");
            }
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled (user switched beans), ignore
            _logger.LogDebug("AI recommendation request cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting AI recommendations for bean {BeanId}", beanId);
            await _feedbackService.ShowErrorAsync("Couldn't get AI recommendations. Enter values manually or try again.");
        }
        finally
        {
            SetState(s => s.IsLoadingAdvice = false);
        }
    }

    /// <summary>
    /// Handles bag selection - determines whether to use AI recommendations or load best shot settings.
    /// Uses AI when: (1) bean has no history, or (2) switching to a different bean than most recent.
    /// </summary>
    async Task HandleBagSelectionAsync(int beanId, int bagId)
    {
        try
        {
            // Check if bean has history and if it's the most recent bean
            var hasHistory = await _shotService.BeanHasHistoryAsync(beanId);
            var mostRecentBeanId = await _shotService.GetMostRecentBeanIdAsync();

            if (!hasHistory || beanId != mostRecentBeanId)
            {
                // New bean or switching beans - get AI recommendations
                await RequestAIRecommendationsAsync(beanId);
            }
            else
            {
                // Same bean as most recent - load best shot settings
                await LoadBestShotSettingsAsync(bagId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling bag selection for bean {BeanId}", beanId);
            // Fall back to loading best shot settings
            await LoadBestShotSettingsAsync(bagId);
        }
    }

    async Task SaveShotAsync()
    {
        try
        {
            // Edit mode: Update existing shot
            if (Props.ShotId.HasValue)
            {
                if (State.SelectedBagId == null)
                {
                    await _feedbackService.ShowErrorAsync("Please select a bag");
                    return;
                }

                var updateDto = new UpdateShotDto
                {
                    BagId = State.SelectedBagId.Value,
                    MachineId = State.SelectedMachineId,
                    GrinderId = State.SelectedGrinderId,
                    AccessoryIds = State.SelectedAccessoryIds,
                    MadeById = State.SelectedMaker?.Id,
                    MadeForId = State.SelectedRecipient?.Id,
                    DoseIn = State.DoseIn,
                    GrindSetting = State.GrindSetting,
                    ExpectedTime = State.ExpectedTime,
                    ExpectedOutput = State.ExpectedOutput,
                    ActualTime = State.ActualTime,
                    ActualOutput = State.ActualOutput,
                    // Convert from 0-4 UI index to 1-5 service scale
                    Rating = State.Rating + 1,
                    DrinkType = State.DrinkType,
                    BrewMethod = State.BrewMethod,
                    TastingNotes = State.TastingNotes
                };

                await _shotService.UpdateShotAsync(Props.ShotId.Value, updateDto);

                await _feedbackService.ShowSuccessAsync("Shot updated successfully");

                // await Navigation.PopAsync();
            }
            // Add mode: Create new shot
            else
            {
                if (State.SelectedBagId == null)
                {
                    if (State.AvailableBags.Count == 0)
                    {
                        await _feedbackService.ShowErrorAsync("No active bags", "Please add a bag before logging a shot");
                    }
                    else
                    {
                        await _feedbackService.ShowErrorAsync("Please select a bag", "Choose a bag before logging your shot");
                    }
                    return;
                }

                var createDto = new CreateShotDto
                {
                    BagId = State.SelectedBagId.Value,
                    MachineId = State.SelectedMachineId,
                    GrinderId = State.SelectedGrinderId,
                    AccessoryIds = State.SelectedAccessoryIds,
                    MadeById = State.SelectedMaker?.Id,
                    MadeForId = State.SelectedRecipient?.Id,
                    DoseIn = State.DoseIn,
                    GrindSetting = State.GrindSetting,
                    ExpectedTime = State.ExpectedTime,
                    ExpectedOutput = State.ExpectedOutput,
                    ActualTime = State.ActualTime,
                    ActualOutput = State.ActualOutput,
                    DrinkType = State.DrinkType,
                    BrewMethod = State.BrewMethod,
                    // Convert from 0-4 UI index to 1-5 service scale
                    Rating = State.Rating + 1,
                    TastingNotes = State.TastingNotes
                };

                await _shotService.CreateShotAsync(createDto);

                _preferencesService.SetLastDrinkType(State.DrinkType);
                if (State.SelectedBagId.HasValue)
                {
                    _preferencesService.SetLastBagId(State.SelectedBagId.Value);
                }

                // Save last-used equipment to preferences
                _preferencesService.SetLastMachineId(State.SelectedMachineId);
                _preferencesService.SetLastGrinderId(State.SelectedGrinderId);
                _preferencesService.SetLastAccessoryIds(State.SelectedAccessoryIds);

                await _feedbackService.ShowSuccessAsync($"{State.DrinkType} shot logged successfully");

                await LoadDataAsync();
            }
        }
        catch (ValidationException vex)
        {
            // Extract all validation error messages into a single readable string
            var errorMessages = vex.Errors
                .SelectMany(e => e.Value)
                .ToList();

            var errorDetails = errorMessages.Count == 1
                ? errorMessages.First()
                : string.Join("\n• ", errorMessages.Prepend(""));

            _logger.LogWarning(vex, "Validation failed when saving shot: {Errors}", string.Join(", ", errorMessages));

            await _feedbackService.ShowErrorAsync(
                "Validation Error",
                errorMessages.Count == 1 ? errorMessages.First() : errorDetails.TrimStart());

            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = string.Join(", ", errorMessages);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save shot");
            await _feedbackService.ShowErrorAsync(Props.ShotId.HasValue ? "Failed to update shot" : "Failed to save shot", "Please try again");
            SetState(s =>
            {
                s.IsLoading = false;
                s.ErrorMessage = ex.Message;
            });
        }
    }

    #region Inline Bean/Bag Creation (T011-T017, T018-T022)

    /// <summary>
    /// Shows combined bean+bag creation popup for inline creation flow.
    /// Used when no beans exist in the system - creates both in one step.
    /// </summary>
    async Task ShowBeanAndBagCreationPopup()
    {
        var popup = _services.GetRequiredService<AddCoffeePopup>();
        popup.OnCreated = HandleBagCreated;

        await popup.InitializeAsync();
        await IPopupService.Current.PushAsync(popup);
    }

    /// <summary>
    /// Shows bag creation popup with bean picker (T019).
    /// Used when beans exist but no active bags.
    /// </summary>
    async Task ShowBagCreationPopupWithPicker()
    {
        var popup = new BagCreationPopup(_bagService)
        {
            AvailableBeans = State.AvailableBeans,
            OnBagCreated = HandleBagCreated
        };
        popup.Build();  // Build content after setting properties

        await IPopupService.Current.PushAsync(popup);
    }

    /// <summary>
    /// Handles successful bag creation - refreshes data and auto-selects bag (T016, T017, T024).
    /// </summary>
    void HandleBagCreated(BagSummaryDto newBag)
    {
        // Refresh data and auto-select the new bag (T017, T024)
        _ = RefreshAndSelectBag(newBag.Id);
    }

    /// <summary>
    /// Refreshes data after bag creation and auto-selects the new bag (T017, T023, T024).
    /// </summary>
    async Task RefreshAndSelectBag(int newBagId)
    {
        await LoadDataAsync();

        // Find and select the new bag (T024)
        var newBagIndex = State.AvailableBags.FindIndex(b => b.Id == newBagId);
        if (newBagIndex >= 0)
        {
            SetState(s =>
            {
                s.SelectedBagIndex = newBagIndex;
                s.SelectedBagId = newBagId;
            });

            // Load best shot settings for the new bag
            await LoadBestShotSettingsAsync(newBagId);
        }
    }

    /// <summary>
    /// Renders the "no beans" empty state with Create Bean CTA (T012).
    /// Shown when AvailableBeans.Count == 0.
    /// </summary>
    VisualNode RenderNoBeanEmptyState()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = isLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;

        return VStack(spacing: 16,
            // Coffee bean icon - MUST use MaterialSymbolsFont per Constitution Principle III
            Label(MaterialSymbolsFont.Coffee)
                .FontFamily(MaterialSymbolsFont.FontFamily)
                .FontSize(48)
                .TextColor(textColor)
                .HCenter(),

            Label("No beans configured")
                .FontSize(20)
                .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                .TextColor(textColor)
                .HCenter(),

            Label("Create your first bean to start logging shots")
                .FontSize(14)
                .TextColor(secondaryTextColor)
                .HCenter()
                .HorizontalTextAlignment(TextAlignment.Center),

            Button("Add Coffee")
                .OnClicked(async () => await ShowBeanAndBagCreationPopup())
                .BackgroundColor(primaryColor)
                .TextColor(Colors.White)
                .HeightRequest(50)
                .WidthRequest(200)
                .HCenter()
        )
        .VCenter()
        .HCenter()
        .Padding(32);
    }

    #endregion

    public override VisualNode Render()
    {
        if (State.IsLoading && !State.AvailableBags.Any())
        {
            return ContentPage(Props.ShotId.HasValue ? "Edit Drink" : "New Drink",
                VStack(
                    ActivityIndicator()
                        .IsRunning(true),
                    Label("Loading...")
                        .Margin(0, 8)
                )
                .VCenter()
                .HCenter()
            )
            .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
            .OnAppearing(() => OnPageAppearing());
        }

        // T011: Show "no beans" empty state when no beans exist (new users)
        // Only for add mode - edit mode should still show the form
        if (!Props.ShotId.HasValue && !State.IsLoading && State.AvailableBeans.Count == 0)
        {
            return ContentPage("New Drink",
                RenderNoBeanEmptyState()
            )
            .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Never))
            .OnAppearing(() => OnPageAppearing());
        }

        SafeAreaEdges safeEdges = DeviceDisplay.Current.MainDisplayInfo.Rotation switch
        {
            DisplayRotation.Rotation0 => new(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None),
            DisplayRotation.Rotation90 => new(SafeAreaRegions.All, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None),
            DisplayRotation.Rotation180 => new(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None),
            DisplayRotation.Rotation270 => new(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.All, SafeAreaRegions.None),
            _ => new(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None)
        };

        // MauiControls.Shell.Current.BackgroundColor = Colors.Transparent;
        // MauiControls.Shell.Current.Background = Colors.Transparent;

        return ContentPage(Props.ShotId.HasValue ? "Edit Drink" : "New Drink",
            // Voice command toolbar item (available for new shots)
            !Props.ShotId.HasValue ?
                ToolbarItem("Voice")
                    .IconImageSource(new FontImageSource
                    {
                        FontFamily = MaterialSymbolsFont.FontFamily,
                        Glyph = MaterialSymbolsFont.Mic,
                        Color = State.IsVoiceSheetOpen ? Colors.Red : AppColors.Light.TextPrimary,
                        Size = 24
                    })
                    .Order(MauiControls.ToolbarItemOrder.Primary)
                    .OnClicked(async () => await ToggleVoiceSheetAsync())
                : null,

            // Camera toolbar item for vision/people counting (available for new shots)
            !Props.ShotId.HasValue ?
                ToolbarItem("Camera")
                    .IconImageSource(new FontImageSource
                    {
                        FontFamily = MaterialSymbolsFont.FontFamily,
                        Glyph = MaterialSymbolsFont.Photo_camera,
                        Color = AppColors.Light.TextPrimary,
                        Size = 24
                    })
                    .Order(MauiControls.ToolbarItemOrder.Primary)
                    .OnClicked(async () => await CaptureAndAnalyzePhotoAsync())
                : null,

            Props.ShotId.HasValue ?
                ToolbarItem("")
                    .IconImageSource(AppIcons.Ai)
                    .Order(MauiControls.ToolbarItemOrder.Primary)
                    .OnClicked(async () => await RequestAdviceAsync())
                : null,

            Props.ShotId.HasValue ?
                ToolbarItem("Delete")
                    .IconImageSource(AppIcons.Delete)
                    .Order(MauiControls.ToolbarItemOrder.Secondary)
                    .OnClicked(async () => await DeleteShotAsync())
                : null,

            Grid("Auto, *, Auto", "*",
                // Row 0: Animated loading bar (outside ScrollView)
                State.IsLoadingAdvice ? RenderAnimatedLoadingBar().GridRow(0) : null,

                // Row 1: Main scrollable content
                ScrollView(
                    VStack(

                        // AI Advice at top (inside ScrollView so it can scroll)
                        State.ShowAdviceSection && State.AdviceResponse != null
                            ? RenderAdviceDisplay()
                            : null,

                        // Error message
                        State.ErrorMessage != null ?
                            Label(State.ErrorMessage)
                                .TextColor(Colors.Red)
                                .FontSize(14)
                                .Margin(0, 8) :
                            null,

                        // [Zoe UX, 2026-04-19] The drink-logging page now leads
                        // with the brew method as the primary choice — dose,
                        // yield, and time all re-scale based on it. Bean/bag +
                        // recipe preview follow, then the adaptive gauges.

                        // 1. Brew method chip row (primary selection)
                        RenderBrewMethodChips(),

                        // 2. Bag picker + recipe preview card
                        RenderBagAndRecipeSection(),

                        // 3. In/Out Gauges — adaptive to selected brew method
                        RenderDoseGauges(),

                        // 4. Time slider — adaptive max per brew method
                        RenderTimeSlider(),

                        // User Selection Row (Made By -> Made For)
                        RenderUserSelectionRow(),

                        // Rating
                        RenderRatingSelector(),

                        // Tasting Notes (optional)
                        VStack(spacing: 8,
                            Label("Tasting Notes (optional)")
                                .ThemeKey(ThemeKeys.FormLabel),
                            Editor()
                                .Text(State.TastingNotes ?? "")
                                .OnTextChanged((s, e) => SetState(state => state.TastingNotes = e.NewTextValue))
                                .Placeholder("E.g., bright, fruity, slightly sour...")
                                .HeightRequest(80)
                                .ThemeKey(ThemeKeys.Entry)
                        ),

                    // Save Button
                    Button(Props.ShotId.HasValue ? "Update Drink" : "Log Drink")
                        .IsEnabled(!State.IsLoading)
                        .OnClicked(async () => await SaveShotAsync())
                        .HeightRequest(50),

                    BoxView()
                        .HorizontalOptions(LayoutOptions.Fill)
                        .HeightRequest(1)
                        .Margin(0, AppSpacing.L, 0, 0),

                    Label()
                        .Text("Additional Details")
                        .ThemeKey(ThemeKeys.MutedText),

                    // Grind Setting
                    new FormEntryField()
                        .Label("Grind Setting")
                        .Text(State.GrindSetting)
                        .OnTextChanged(text => SetState(s => s.GrindSetting = text)),

                    // Expected Time
                    new FormEntryField()
                        .Label("Expected Time (s)")
                        .Text(State.ExpectedTime.ToString())
                        .Keyboard(Microsoft.Maui.Keyboard.Numeric)
                        .OnTextChanged(text =>
                        {
                            if (decimal.TryParse(text, out var val))
                                SetState(s => s.ExpectedTime = val);
                        }),

                    // Expected Output
                    new FormEntryField()
                        .Label("Expected Output (g)")
                        .Text(State.ExpectedOutput.ToString("F1"))
                        .Keyboard(Microsoft.Maui.Keyboard.Numeric)
                        .OnTextChanged(text =>
                        {
                            if (decimal.TryParse(text, out var val))
                                SetState(s => s.ExpectedOutput = val);
                        }),

                    // Drink Type (Espresso → Americano → Latte etc.) kept
                    // under Additional Details since it's largely a
                    // milk/dilution label that's independent of brew method.
                    new FormPickerField()
                        .Label("Drink Type")
                        .Title("Select Drink")
                        .ItemsSource(State.DrinkTypes)
                        .SelectedIndex(State.SelectedDrinkIndex)
                        .OnSelectedIndexChanged(idx =>
                        {
                            if (idx >= 0 && idx < State.DrinkTypes.Count)
                            {
                                SetState(s =>
                                {
                                    s.SelectedDrinkIndex = idx;
                                    s.DrinkType = State.DrinkTypes[idx];
                                });
                            }
                        })



                    ).Spacing(AppSpacing.M)
                    .Padding(16, 0, 16, 32)
                ).GridRow(1)

                // Voice overlay is now rendered via WindowOverlay (IOverlayService)
            ).SafeAreaEdges(safeEdges)
        )
        .SafeAreaEdges(safeEdges)
        .OniOS(_ => _.Set(MauiControls.PlatformConfiguration.iOSSpecific.Page.LargeTitleDisplayProperty, LargeTitleDisplayMode.Always))
        .OnAppearing(() => OnPageAppearing());
    }

    private async Task DeleteShotAsync()
    {
        if (Props.ShotId == null)
            return;

        var popup = new SimpleActionPopup
        {
            Title = $"Delete Shot?",
            Text = "This action cannot be undone.",
            ActionButtonText = "Delete",
            SecondaryActionButtonText = "Cancel",
            ActionButtonCommand = new Command(async () =>
            {
                // Delete logic here
                await _shotService.DeleteShotAsync(Props.ShotId.Value);
                await IPopupService.Current.PopAsync();
                await MauiControls.Shell.Current.GoToAsync("..");
            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    /// <summary>
    /// Captures a photo and analyzes it to count people for coffee needs.
    /// </summary>
    private async Task CaptureAndAnalyzePhotoAsync()
    {
        try
        {
            // Check if capture is supported
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await ContainerPage!.DisplayAlert("Camera Unavailable", 
                    "Camera is not available on this device.", "OK");
                return;
            }

            // Check if vision service is available
            if (!await _visionService.IsAvailableAsync())
            {
                await ContainerPage!.DisplayAlert("Vision Unavailable", 
                    "Vision service is not configured.", "OK");
                return;
            }

            // Capture photo
            var photo = await MediaPicker.Default.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Take a photo of the room",
                MaximumWidth = 1024,
                MaximumHeight = 1024,
                CompressionQuality = 70
            });

            if (photo == null)
            {
                // User cancelled
                return;
            }

            // Show analyzing state
            await ContainerPage!.DisplayAlert("Analyzing", 
                "Analyzing photo for people count...", "OK");

            // Analyze the photo
            using var stream = await photo.OpenReadAsync();
            var result = await _visionService.AnalyzeImageAsync(
                stream, 
                "Count the people in this image and tell me how many cups of coffee I need to make.");

            if (result.Success)
            {
                var message = result.Message ?? 
                    $"I see {result.PeopleCount} {(result.PeopleCount == 1 ? "person" : "people")}. " +
                    $"You'll need {result.CupsNeeded} {(result.CupsNeeded == 1 ? "cup" : "cups")} of coffee, " +
                    $"which requires about {result.BeansNeededGrams}g of beans.";
                
                await ContainerPage!.DisplayAlert("Analysis Complete", message, "OK");
            }
            else
            {
                await ContainerPage!.DisplayAlert("Analysis Failed", 
                    result.ErrorMessage ?? "Could not analyze the photo.", "OK");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing and analyzing photo");
            await ContainerPage!.DisplayAlert("Error", 
                $"Failed to capture or analyze photo: {ex.Message}", "OK");
        }
    }

    VisualNode RenderAnimatedLoadingBar()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = isLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;

        // Gradient brush: transparent at edges, solid color in center
        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Colors.Transparent, 0.0f),
                new GradientStop(primaryColor, 0.5f),
                new GradientStop(Colors.Transparent, 1.0f)
            }
        };

        return Grid(
            // Background track
            BoxView()
                .HeightRequest(4)
                .Color(surfaceColor)
                .HorizontalOptions(LayoutOptions.Fill),

            // Animated bar segment with gradient - use lambda to read position reactively
            BoxView()
                .HeightRequest(4)
                .WidthRequest(120)
                .Background(gradientBrush)
                .HorizontalOptions(LayoutOptions.Start)
                .TranslationX(() => State.LoadingBarPosition),

            // Animation controller for continuous looping
            new AnimationController
            {
                new SequenceAnimation
                {
                    new DoubleAnimation()
                        .StartValue(-120)
                        .TargetValue(400) // Animate across screen width
                        .Duration(TimeSpan.FromSeconds(1))
                        .Easing(Easing.Linear)
                        .OnTick(v => SetState(s => s.LoadingBarPosition = v, false))
                }
                .RepeatForever()
            }
            .IsEnabled(State.IsLoadingAdvice)
        ).HeightRequest(4);
    }

    /// <summary>
    /// Renders the voice command overlay as a chat-style bottom sheet.
    /// </summary>
    VisualNode RenderVoiceOverlay()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var backgroundColor = isLightTheme ? AppColors.Light.SurfaceElevated : AppColors.Dark.SurfaceElevated;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
        var accentColor = isLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary;
        var userBubbleColor = accentColor;
        var aiBubbleColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
        var errorColor = Colors.Red;

        // Calculate half screen height
        var screenHeight = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density;
        var sheetHeight = screenHeight * 0.55; // 55% of screen height

        // Safe area edges - only apply bottom safe area for the button area
        var bottomSafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.All);

        return Border(
            Grid("Auto, *, Auto", "*",
                // Row 0: Header with close button
                Grid("*", "Auto, *, Auto",
                    // Close button (X) at left
                    Button(MaterialSymbolsFont.Close)
                        .FontFamily(MaterialSymbolsFont.FontFamily)
                        .FontSize(24)
                        .TextColor(textColor)
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(async () => await ToggleVoiceSheetAsync())
                        .GridColumn(0),

                    // Title
                    Label("Voice Commands")
                        .FontSize(18)
                        .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                        .TextColor(textColor)
                        .HCenter()
                        .VCenter()
                        .GridColumn(1),

                    // Clear history button
                    Button(MaterialSymbolsFont.Delete_sweep)
                        .FontFamily(MaterialSymbolsFont.FontFamily)
                        .FontSize(20)
                        .TextColor(secondaryTextColor)
                        .BackgroundColor(Colors.Transparent)
                        .OnClicked(() => SetState(s => s.VoiceChatHistory.Clear()))
                        .GridColumn(2)
                ).GridRow(0).Padding(0, 0, 0, 8),

                // Row 1: Chat history using CollectionView with auto-scroll to bottom
                RenderChatCollectionView(textColor, secondaryTextColor, userBubbleColor, aiBubbleColor, errorColor, accentColor),

                // Row 2: Record/Stop button - wrapped in ContentView with bottom safe area
                ContentView(
                    Grid("*", "*",
                        // Large circular mic button
                        Button(State.IsRecording ? MaterialSymbolsFont.Stop : MaterialSymbolsFont.Mic)
                            .FontFamily(MaterialSymbolsFont.FontFamily)
                            .FontSize(32)
                            .TextColor(Colors.White)
                            .BackgroundColor(State.IsRecording ? Colors.Red : accentColor)
                            .WidthRequest(64)
                            .HeightRequest(64)
                            .CornerRadius(32)
                            .OnClicked(async () => await ToggleRecordingAsync())
                            .ScaleX(() => State.VoicePulseScale)
                            .ScaleY(() => State.VoicePulseScale)
                            .HCenter()
                            .VCenter()
                    ).Padding(0, 16, 0, 8).SafeAreaEdges(SafeAreaEdges.None)
                )
                .SafeAreaEdges(SafeAreaEdges.None)
                .GridRow(2),

                // Pulsing animation for mic button when recording
                State.IsRecording ?
                    new AnimationController
                    {
                        new SequenceAnimation
                        {
                            new DoubleAnimation()
                                .StartValue(1.0)
                                .TargetValue(1.15)
                                .Duration(TimeSpan.FromMilliseconds(600))
                                .Easing(Easing.SinInOut)
                                .OnTick(v => SetState(s => s.VoicePulseScale = v, false)),
                            new DoubleAnimation()
                                .StartValue(1.15)
                                .TargetValue(1.0)
                                .Duration(TimeSpan.FromMilliseconds(600))
                                .Easing(Easing.SinInOut)
                                .OnTick(v => SetState(s => s.VoicePulseScale = v, false))
                        }
                        .RepeatForever()
                    }
                    .IsEnabled(true)
                : null
            )
            .Padding(16, 16, 16, 0)
            .SafeAreaEdges(SafeAreaEdges.None)
        )
        .SafeAreaEdges(SafeAreaEdges.None)
        .BackgroundColor(backgroundColor)
        .StrokeThickness(0)
        .StrokeShape(new RoundRectangle().CornerRadius(20, 20, 0, 0))
        .Margin(0)
        .HeightRequest(sheetHeight);
    }

    /// <summary>
    /// Renders the chat interface using CollectionView with auto-scroll to bottom.
    /// </summary>
    VisualNode RenderChatCollectionView(Color textColor, Color secondaryTextColor, Color userBubbleColor, Color aiBubbleColor, Color errorColor, Color accentColor)
    {
        // Build the list of items to display (messages + live transcript + processing indicator)
        var displayItems = new List<object>();

        // Add chat history
        displayItems.AddRange(State.VoiceChatHistory);

        // Add live transcript as a special item if recording
        if (State.IsRecording && !string.IsNullOrEmpty(State.VoiceTranscript))
        {
            displayItems.Add(new VoiceChatMessage
            {
                IsUser = true,
                Text = State.VoiceTranscript,
                Timestamp = DateTime.Now,
                IsError = false
            });
        }

        // Add processing indicator as special marker
        if (State.VoiceState == SpeechRecognitionState.Processing)
        {
            displayItems.Add("__PROCESSING__");
        }

        // Show hint if empty
        if (displayItems.Count == 0)
        {
            return VStack(
                Label("Say something like:\n\"Log shot 18 in 36 out 28 seconds\"\n\"What was my last shot?\"\n\"Find shots with Ethiopia\"")
                    .FontSize(14)
                    .TextColor(secondaryTextColor)
                    .HCenter()
                    .Margin(20, 40)
            ).GridRow(1);
        }

        return CollectionView()
            .ItemsSource(displayItems, item =>
            {
                // Handle processing indicator
                if (item is string marker && marker == "__PROCESSING__")
                {
                    return HStack(spacing: 8,
                        ActivityIndicator()
                            .IsRunning(true)
                            .Color(accentColor)
                            .HeightRequest(16)
                            .WidthRequest(16),
                        Label("Processing...")
                            .FontSize(12)
                            .TextColor(secondaryTextColor)
                    ).HStart().Margin(8, 4);
                }

                // Handle chat messages
                if (item is VoiceChatMessage msg)
                {
                    var bubbleColor = msg.IsUser ? userBubbleColor : (msg.IsError ? errorColor.WithAlpha(0.2f) : aiBubbleColor);
                    var messageTextColor = msg.IsUser ? Colors.White : (msg.IsError ? errorColor : textColor);
                    var bubbleOpacity = (State.IsRecording && !string.IsNullOrEmpty(State.VoiceTranscript) && msg == displayItems.LastOrDefault())
                        ? 0.7f : 1.0f;

                    return Grid("*", "*",
                        Border(
                            Label(msg.Text)
                                .FontSize(14)
                                .TextColor(messageTextColor)
                                .LineBreakMode(LineBreakMode.WordWrap)
                        )
                        .BackgroundColor(bubbleColor.WithAlpha(bubbleOpacity))
                        .StrokeThickness(0)
                        .StrokeShape(new RoundRectangle().CornerRadius(msg.IsUser
                            ? new CornerRadius(12, 12, 0, 12)
                            : new CornerRadius(12, 12, 12, 0)))
                        .Padding(12, 8)
                        .MaximumWidthRequest(280)
                        .HorizontalOptions(msg.IsUser ? LayoutOptions.End : LayoutOptions.Start)
                    ).Padding(8, 4);
                }

                return null;
            })
            .ItemsUpdatingScrollMode(MauiControls.ItemsUpdatingScrollMode.KeepLastItemInView)
            .SelectionMode(MauiControls.SelectionMode.None)
            .GridRow(1);
    }

    VisualNode RenderDoseGauges()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = AppColors.Light.Primary;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;

        // Count selected equipment for badge
        var selectedCount = (State.SelectedMachineId.HasValue ? 1 : 0) +
                           (State.SelectedGrinderId.HasValue ? 1 : 0) +
                           State.SelectedAccessoryIds.Count;

        // Brew-method-adaptive ranges: espresso 15–20g in / 25–50g out,
        // pour over 10–60g in / 100–800g out, etc. Profile is the single
        // source of truth (also drives ShotService validation).
        var profile = State.BrewMethod.Profile();

        return Grid("Auto, Auto", "*, Auto, *",
            // In Gauge (left column)
            Grid(
                RenderSingleGauge(
                    value: (double)State.DoseIn,
                    getValueText: () => State.DoseIn.ToString("F1"),
                    min: (double)profile.DoseMin,
                    max: (double)profile.DoseMax,
                    step: (double)profile.DoseStep,
                    primaryColor: primaryColor,
                    textColor: textColor,
                    secondaryTextColor: secondaryTextColor,
                    surfaceColor: surfaceColor,
                    onValueChanged: val => SetState(st => st.DoseIn = (decimal)val)
                ),
                Label()
                    .Text("u")
                    .FontFamily("coffee-icons")
                    .TextColor(secondaryTextColor)
                    .FontSize(24)
                    .HCenter()
                    .VEnd()
            ).GridColumn(0).GridRow(0),

            // Equipment button (center column)
            Grid(
                Border(
                    Label()
                        .Text("s") // Machine icon from coffee-icons font
                        .FontFamily("coffee-icons")
                        .FontSize(32)
                        // .TextColor(selectedCount > 0 ? primaryColor : secondaryTextColor)
                        .HCenter()
                        .VCenter()
                        .TranslationX(3)
                )
                .StrokeShape(new RoundRectangle().CornerRadius(25))
                .BackgroundColor(surfaceColor)
                .HeightRequest(50)
                .WidthRequest(50)
                .OnTapped(() => _ = ShowEquipmentSelectionPopup()),

                // Badge showing count of selected equipment
                selectedCount > 0 ?
                    Border(
                        Label(selectedCount.ToString())
                            .FontSize(10)
                            .TextColor(Colors.White)
                            .HCenter()
                            .VCenter()
                    )
                    .StrokeShape(new RoundRectangle().CornerRadius(8))
                    .BackgroundColor(primaryColor)
                    .HeightRequest(16)
                    .WidthRequest(16)
                    .HEnd()
                    .VStart()
                    .Margin(0, -4, -4, 0) : null
            )
            .GridColumn(1).GridRow(0).VCenter(),

            // Out Gauge (right column)
            Grid(
                RenderSingleGauge(
                    value: (double)(State.ActualOutput ?? 0),
                    getValueText: () => (State.ActualOutput ?? 0).ToString("F1"),
                    min: (double)profile.OutputMin,
                    max: (double)profile.OutputMax,
                    step: (double)profile.OutputStep,
                    primaryColor: primaryColor,
                    textColor: textColor,
                    secondaryTextColor: secondaryTextColor,
                    surfaceColor: surfaceColor,
                    onValueChanged: val => SetState(st => st.ActualOutput = (decimal)val)
                ),
                Label()
                    .Text("t")
                    .FontFamily("coffee-icons")
                    .TextColor(secondaryTextColor)
                    .FontSize(24)
                    .HCenter()
                    .VEnd()
            ).GridColumn(2).GridRow(0),

            // Ratio label (center column, bottom row) - uses lambda binding for reactive updates
            Label(() => State.DoseIn > 0 && State.ActualOutput.HasValue && State.ActualOutput.Value > 0
                ? $"1:{(State.ActualOutput.Value / State.DoseIn):F1}"
                : "1:0")
                .ThemeKey(ThemeKeys.SecondaryText)
                .HCenter()
                .VCenter()
                .GridColumn(1)
                .GridRow(1)
                .Margin(0, 8, 0, 0)
        );
    }

    /// <summary>
    /// Horizontal scrolling row of brew-method chips (Espresso / Pour Over /
    /// Moka / Drip / Aeropress / French Press). Primary user choice on the
    /// drink-logging page — changing the method re-scales the gauges, time
    /// slider, and validation ranges and refreshes the recipe preview.
    /// </summary>
    VisualNode RenderBrewMethodChips()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = AppColors.Light.Primary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;

        var methods = BrewMethodExtensions.All;

        return VStack(spacing: 8,
            Label("Brew method")
                .ThemeKey(ThemeKeys.FormLabel),
            ScrollView(
                HStack(spacing: 8,
                    methods.Select(m =>
                    {
                        var isSelected = State.BrewMethod == m;
                        return (VisualNode)Border(
                            Label(m.DisplayName())
                                .FontSize(14)
                                .FontAttributes(isSelected
                                    ? Microsoft.Maui.Controls.FontAttributes.Bold
                                    : Microsoft.Maui.Controls.FontAttributes.None)
                                .TextColor(isSelected ? Colors.White : textColor)
                                .VCenter()
                                .HCenter()
                                .Margin(14, 0)
                        )
                        .StrokeShape(new RoundRectangle().CornerRadius(18))
                        .StrokeThickness(isSelected ? 0 : 1)
                        .Stroke(new SolidColorBrush(isSelected ? primaryColor : surfaceColor))
                        .BackgroundColor(isSelected ? primaryColor : Colors.Transparent)
                        .HeightRequest(36)
                        .OnTapped(() => OnBrewMethodSelected(m));
                    }).ToArray()
                )
            )
            .Orientation(ScrollOrientation.Horizontal)
            .HorizontalScrollBarVisibility(ScrollBarVisibility.Never)
        );
    }

    /// <summary>
    /// Bag picker plus an adaptive recipe preview card for the selected
    /// bean × brew method. Shows the roaster's official recipe (Onyx scraper
    /// + future roasters) with a one-tap Apply button, or a friendly empty
    /// state + "View all recipes" affordance that jumps to the bean detail.
    /// </summary>
    VisualNode RenderBagAndRecipeSection()
    {
        // Empty-bag state reuses the existing inline-creation CTA.
        if (State.AvailableBags.Count == 0)
        {
            return VStack(spacing: 12,
                Label("No active bags available")
                    .ThemeKey(ThemeKeys.SecondaryText)
                    .FontSize(16)
                    .HCenter(),
                Label("Create a bag to start logging drinks")
                    .ThemeKey(ThemeKeys.MutedText)
                    .FontSize(14)
                    .HCenter(),
                Button("Add New Bag")
                    .OnClicked(async () => await ShowBagCreationPopupWithPicker())
                    .HCenter()
            ).Padding(16);
        }

        return VStack(spacing: 8,
            new FormPickerField()
                .Label("Bag")
                .Title("Select Bag")
                .ItemsSource(State.AvailableBags.Select(b => b.DisplayLabel).ToList())
                .SelectedIndex(State.SelectedBagIndex)
                .OnSelectedIndexChanged(idx =>
                {
                    if (idx >= 0 && idx < State.AvailableBags.Count)
                    {
                        var bag = State.AvailableBags[idx];
                        var bagId = bag.Id;
                        var beanId = bag.BeanId;
                        SetState(s =>
                        {
                            s.SelectedBagIndex = idx;
                            s.SelectedBagId = bagId;
                        });
                        _ = HandleBagSelectionAsync(beanId, bagId);
                        _ = LoadRecipeForSelectionAsync();
                    }
                }),
            RenderRecipePreviewCard()
        );
    }

    /// <summary>
    /// Small card beneath the bag picker that surfaces the roaster-recommended
    /// recipe for the currently selected bean + brew method. Non-blocking —
    /// absence of a recipe is a normal state, not an error.
    /// </summary>
    VisualNode RenderRecipePreviewCard()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = AppColors.Light.Primary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;

        var selectedBag = (State.SelectedBagIndex >= 0 && State.SelectedBagIndex < State.AvailableBags.Count)
            ? State.AvailableBags[State.SelectedBagIndex]
            : null;

        if (selectedBag == null)
        {
            return VStack();
        }

        if (State.IsLoadingRecipe)
        {
            return Border(
                HStack(spacing: 8,
                    ActivityIndicator().IsRunning(true).HeightRequest(16).WidthRequest(16),
                    Label("Looking up recipe…")
                        .FontSize(13)
                        .TextColor(secondaryTextColor)
                        .VCenter()
                ).Padding(12, 10)
            )
            .StrokeShape(new RoundRectangle().CornerRadius(12))
            .StrokeThickness(0)
            .BackgroundColor(surfaceColor);
        }

        var recipe = State.CurrentRecipe;
        if (recipe != null)
        {
            // Build a compact summary (dose → yield, time, grind, temp)
            var parts = new List<string>();
            if (recipe.DoseIn.HasValue && recipe.OutputAmount.HasValue)
                parts.Add($"{recipe.DoseIn.Value:F1}g → {recipe.OutputAmount.Value:F0}g");
            else if (recipe.DoseIn.HasValue)
                parts.Add($"{recipe.DoseIn.Value:F1}g in");
            else if (recipe.OutputAmount.HasValue)
                parts.Add($"{recipe.OutputAmount.Value:F0}g out");
            if (recipe.TotalTimeSeconds.HasValue)
                parts.Add(FormatTime((int)recipe.TotalTimeSeconds.Value));
            if (recipe.BrewTempC.HasValue)
                parts.Add($"{recipe.BrewTempC.Value:F0}°C");
            var summary = parts.Count > 0 ? string.Join(" · ", parts) : "Recipe available";
            var grindLine = string.IsNullOrWhiteSpace(recipe.GrindHint) ? null : $"Grind: {recipe.GrindHint}";
            var sourceLabel = recipe.Source switch
            {
                RecipeSource.RoasterSite => "Roaster",
                RecipeSource.AIGenerated => "AI suggestion",
                RecipeSource.Manual => "You",
                _ => "Recipe"
            };

            return Border(
                VStack(spacing: 6,
                    HStack(spacing: 6,
                        Label(MaterialSymbolsFont.Coffee)
                            .FontFamily(MaterialSymbolsFont.FontFamily)
                            .FontSize(16)
                            .TextColor(primaryColor)
                            .VCenter(),
                        Label($"{sourceLabel} · {State.BrewMethod.DisplayName()}")
                            .FontSize(12)
                            .TextColor(secondaryTextColor)
                            .VCenter()
                    ),
                    Label(summary)
                        .FontSize(14)
                        .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                        .TextColor(textColor),
                    grindLine != null ?
                        Label(grindLine)
                            .FontSize(12)
                            .TextColor(secondaryTextColor)
                        : null,
                    HStack(spacing: 12,
                        Button("Apply recipe")
                            .FontSize(13)
                            .HeightRequest(32)
                            .Padding(12, 0)
                            .OnClicked(() => ApplyCurrentRecipe()),
                        Button("View all")
                            .FontSize(13)
                            .HeightRequest(32)
                            .Padding(12, 0)
                            .BackgroundColor(Colors.Transparent)
                            .TextColor(primaryColor)
                            .OnClicked(async () => await NavigateToBeanRecipesAsync(selectedBag.BeanId))
                    ).Margin(0, 4, 0, 0)
                ).Padding(12, 10)
            )
            .StrokeShape(new RoundRectangle().CornerRadius(12))
            .StrokeThickness(0)
            .BackgroundColor(surfaceColor);
        }

        // No recipe for this bean + method combination.
        return Border(
            HStack(spacing: 8,
                Label(MaterialSymbolsFont.Info)
                    .FontFamily(MaterialSymbolsFont.FontFamily)
                    .FontSize(16)
                    .TextColor(secondaryTextColor)
                    .VCenter(),
                Label($"No {State.BrewMethod.DisplayName()} recipe yet for this bean")
                    .FontSize(13)
                    .TextColor(secondaryTextColor)
                    .VCenter()
                    .HorizontalOptions(LayoutOptions.StartAndExpand),
                Button("Browse")
                    .FontSize(13)
                    .HeightRequest(30)
                    .Padding(10, 0)
                    .BackgroundColor(Colors.Transparent)
                    .TextColor(primaryColor)
                    .OnClicked(async () => await NavigateToBeanRecipesAsync(selectedBag.BeanId))
            ).Padding(12, 10)
        )
        .StrokeShape(new RoundRectangle().CornerRadius(12))
        .StrokeThickness(0)
        .BackgroundColor(surfaceColor);
    }

    /// <summary>
    /// Adaptive time slider — minimum/maximum/step track the selected brew
    /// method's profile so a pour-over can run 15 minutes and an espresso
    /// stays pinned to ~60s.
    /// </summary>
    VisualNode RenderTimeSlider()
    {
        var profile = State.BrewMethod.Profile();
        var currentSeconds = (double)(State.ActualTime ?? 0);
        // Clamp to profile range for display so switching methods doesn't
        // leave the slider pegged past the max.
        var clamped = Math.Max(profile.TimeMin, Math.Min(profile.TimeMax, currentSeconds));
        var label = State.ActualTime.HasValue
            ? $"Time: {FormatTime((int)State.ActualTime.Value)}"
            : $"Time: {FormatTime(0)}";

        return new FormSliderField()
            .Label(label)
            .Minimum(0)
            .Maximum(profile.TimeMax)
            .Value(clamped)
            .OnValueChanged(val => SetState(s => s.ActualTime = (decimal)Math.Round(val)));
    }

    /// <summary>
    /// Format a time in seconds as either "SSs" (< 60s) or "M:SS" (≥ 60s).
    /// </summary>
    static string FormatTime(int totalSeconds)
    {
        if (totalSeconds < 60)
            return $"{totalSeconds}s";
        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return $"{minutes}:{seconds:D2}";
    }

    /// <summary>
    /// Handle brew-method chip tap: update state, re-default gauges if this
    /// is a new (add-mode) drink and the user hasn't meaningfully customized
    /// them yet, and re-query the recipe preview for the new method.
    /// </summary>
    void OnBrewMethodSelected(BrewMethod method)
    {
        if (State.BrewMethod == method) return;

        var oldProfile = State.BrewMethod.Profile();
        var newProfile = method.Profile();
        var isAddMode = !Props.ShotId.HasValue;

        SetState(s =>
        {
            s.BrewMethod = method;
            s.SelectedBrewMethodIndex = BrewMethodExtensions.All.ToList().IndexOf(method);
            if (s.SelectedBrewMethodIndex < 0) s.SelectedBrewMethodIndex = 0;

            // Add mode: snap gauges/time to the new method's defaults so a
            // user switching Espresso → Pour Over sees a sensible 20g / 320g /
            // 3:30 starting point rather than the espresso-scaled values.
            // Edit mode: clamp existing values to the new range without
            // resetting the user's input.
            if (isAddMode)
            {
                s.DoseIn = newProfile.DoseDefault;
                s.ExpectedOutput = newProfile.OutputDefault;
                s.ExpectedTime = newProfile.TimeDefault;
                s.ActualOutput = null;
                s.ActualTime = null;
            }
            else
            {
                s.DoseIn = newProfile.ClampDose(s.DoseIn);
                s.ExpectedOutput = newProfile.ClampOutput(s.ExpectedOutput);
                s.ExpectedTime = newProfile.ClampTime(s.ExpectedTime);
                if (s.ActualOutput.HasValue)
                    s.ActualOutput = newProfile.ClampOutput(s.ActualOutput.Value);
                if (s.ActualTime.HasValue)
                    s.ActualTime = newProfile.ClampTime(s.ActualTime.Value);
            }
        });

        _ = LoadRecipeForSelectionAsync();
    }

    /// <summary>
    /// Refresh <see cref="ShotLoggingState.CurrentRecipe"/> for the currently
    /// selected bag's bean × the currently selected brew method. No-op if no
    /// bag is selected; caches the last lookup so switching state unrelated
    /// to the recipe doesn't re-hit the DB.
    /// </summary>
    async Task LoadRecipeForSelectionAsync()
    {
        var bag = (State.SelectedBagIndex >= 0 && State.SelectedBagIndex < State.AvailableBags.Count)
            ? State.AvailableBags[State.SelectedBagIndex]
            : null;
        if (bag == null)
        {
            SetState(s => { s.CurrentRecipe = null; s.LastRecipeLookup = null; });
            return;
        }

        var key = (bag.BeanId, State.BrewMethod);
        if (State.LastRecipeLookup == key && !State.IsLoadingRecipe)
            return;

        SetState(s => { s.IsLoadingRecipe = true; s.LastRecipeLookup = key; });
        try
        {
            var recipe = await _recipeService.GetRecipeForBeanAndMethodAsync(bag.BeanId, State.BrewMethod);
            SetState(s =>
            {
                s.CurrentRecipe = recipe;
                s.IsLoadingRecipe = false;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load recipe for BeanId={BeanId}, Method={Method}", bag.BeanId, State.BrewMethod);
            SetState(s => { s.CurrentRecipe = null; s.IsLoadingRecipe = false; });
        }
    }

    /// <summary>
    /// Pre-fill the drink form from the current recipe preview. Non-destructive
    /// — only fields the recipe actually specifies are overwritten.
    /// </summary>
    void ApplyCurrentRecipe()
    {
        var recipe = State.CurrentRecipe;
        if (recipe == null) return;
        var profile = State.BrewMethod.Profile();

        SetState(s =>
        {
            if (recipe.DoseIn.HasValue)
                s.DoseIn = profile.ClampDose(recipe.DoseIn.Value);
            if (recipe.OutputAmount.HasValue)
                s.ExpectedOutput = profile.ClampOutput(recipe.OutputAmount.Value);
            if (recipe.TotalTimeSeconds.HasValue)
                s.ExpectedTime = profile.ClampTime(recipe.TotalTimeSeconds.Value);
            if (!string.IsNullOrWhiteSpace(recipe.GrindHint))
                s.GrindSetting = recipe.GrindHint!;
        });
    }

    async Task NavigateToBeanRecipesAsync(int beanId)
    {
        await MauiControls.Shell.Current.GoToAsync<BeanDetailPageProps>(
            "bean-detail",
            props => props.BeanId = beanId);
    }

    VisualNode RenderSingleGauge(
        double value,
        Func<string> getValueText,
        double min,
        double max,
        double step,
        Color primaryColor,
        Color textColor,
        Color secondaryTextColor,
        Color surfaceColor,
        Action<double> onValueChanged)
    {
        // Clamp display value so the gauge doesn't throw / render off-range
        // when switching brew methods (e.g. espresso 36g → pour over still
        // shows 36g but the new axis goes 100–800; clamp to min until user
        // drags).
        var displayValue = Math.Max(min, Math.Min(max, value));
        // Decimal places in the add/subtract buttons: finer for small ranges,
        // integer for coarse ranges (pour-over 100–800g).
        var decimals = step >= 1 ? 0 : 1;
        return Grid(
            new SfRadialGauge()
                .HeightRequest(160)
                .WidthRequest(160)
                .BackgroundColor(Colors.Transparent)
                .WithAxis(
                    new RadialAxis()
                        .Minimum(min)
                        .Maximum(max)
                        .EnableLoadingAnimation(State.ShowAdviceSection == false)
                        .Interval((max - min) / 5)
                        .MinorTicksPerInterval(1)
                        .ShowLabels(true)
                        .ShowTicks(false)
                        .RadiusFactor(0.8)
                        .LabelFormat("0")
                        .AxisLabelStyle(new Syncfusion.Maui.Gauges.GaugeLabelStyle
                        {
                            TextColor = secondaryTextColor,
                            FontSize = 10
                        })
                        .AxisLineStyle(new Syncfusion.Maui.Gauges.RadialLineStyle
                        {
                            Fill = new SolidColorBrush(surfaceColor),
                            Thickness = 20,
                            CornerStyle = Syncfusion.Maui.Gauges.CornerStyle.BothCurve
                        })
                        .WithPointers(
                            new RangePointer()
                                .Value(displayValue)
                                .CornerStyle(Syncfusion.Maui.Gauges.CornerStyle.BothCurve)
                                .PointerWidth(20)
                                .Fill(new SolidColorBrush(primaryColor)),

                            new ShapePointer()
                                .Value(displayValue)
                                .IsInteractive(true)
                                .StepFrequency(step)
                                .ShapeType(Syncfusion.Maui.Gauges.ShapeType.Circle)
                                .ShapeHeight(28)
                                .ShapeWidth(28)
                                .Fill(new SolidColorBrush(primaryColor))
                                .HasShadow(true)
                                .Offset(0)
                                .OnValueChanged((s, e) =>
                                {
                                    if (e is Syncfusion.Maui.Gauges.ValueChangedEventArgs syncArgs)
                                    {
                                        var roundedValue = Math.Round(syncArgs.Value, decimals);
                                        onValueChanged(roundedValue);
                                    }
                                })
                        )
                ),

            // Overlay center labels - uses lambda binding for reactive updates
            VStack(spacing: 0,
                Label(getValueText)
                    .FontSize(20)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                    .TextColor(textColor)
                    .HCenter(),
                Label("g")
                    .FontSize(9)
                    .TextColor(secondaryTextColor)
                    .HCenter()
            ).VCenter().HCenter().TranslationY(10),

            // Add/subtract buttons — step size adapts to the method's scale
            // so pour-over (100–800g) nudges by 5g per tap, espresso (25–50g)
            // stays at 0.1g precision.
            ImageButton().Source(AppIcons.Decrement).HStart().VEnd().TranslationY(10).Aspect(Aspect.Center)
                .OnClicked(() => onValueChanged(Math.Round(Math.Max(min, value - step), decimals))),
            ImageButton().Source(AppIcons.Increment).HEnd().VEnd().TranslationY(10).Aspect(Aspect.Center)
                .OnClicked(() => onValueChanged(Math.Round(Math.Min(max, value + step), decimals)))

        ).HeightRequest(160).WidthRequest(160).HCenter();
    }

    VisualNode RenderUserSelectionRow()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = AppColors.Light.Primary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;

        return Grid("Auto, Auto", "Auto, Auto, Auto",
            // Made By avatar
            RenderUserAvatar(
                user: State.SelectedMaker,
                backgroundColor: surfaceColor,
                iconColor: secondaryTextColor,
                onTapped: () => _ = ShowUserSelectionPopup("Made By", user => SetState(s => s.SelectedMaker = user))
            ).GridRow(0).GridColumn(0),

            // Arrow
            Label(MaterialSymbolsFont.Arrow_forward)
                .FontFamily(MaterialSymbolsFont.FontFamily)
                .FontSize(24)
                .TextColor(secondaryTextColor)
                .VCenter()
                .HCenter()
                .GridRow(0).GridColumn(1),

            // Made For avatar
            RenderUserAvatar(
                user: State.SelectedRecipient,
                backgroundColor: surfaceColor,
                iconColor: secondaryTextColor,
                onTapped: () => _ = ShowUserSelectionPopup("Made For", user => SetState(s => s.SelectedRecipient = user))
            ).GridRow(0).GridColumn(2),

            // Labels row
            Label("Made by")
                .FontSize(12)
                .TextColor(secondaryTextColor)
                .HCenter()
                .GridRow(1).GridColumn(0),

            Label("For")
                .FontSize(12)
                .TextColor(secondaryTextColor)
                .HCenter()
                .GridRow(1).GridColumn(2)
        ).ColumnSpacing(16).RowSpacing(4).HCenter();
    }

    VisualNode RenderRatingSelector()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var primaryColor = isLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary;
        var surfaceColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;
        var mutedColor = isLightTheme ? AppColors.Light.TextMuted : AppColors.Dark.TextMuted;

        return HStack(spacing: 8,
            AppIcons.RatingIcons.Select((icon, index) =>
                Border(
                    Label(icon)
                        .FontFamily(MaterialSymbolsFont.FontFamily)
                        .FontSize(32)
                        .TextColor(State.Rating == index ? primaryColor : mutedColor)
                        .HCenter()
                        .VCenter()
                )
                // .StrokeShape(new RoundRectangle().CornerRadius(8))
                // .BackgroundColor(State.Rating == index ? surfaceColor : Colors.Transparent)
                // .Stroke(State.Rating == index ? primaryColor : Colors.Transparent)
                // .StrokeThickness(State.Rating == index ? 1 : 0)
                .StrokeThickness(0)
                .HeightRequest(48)
                .WidthRequest(48)
                .OnTapped(() => SetState(s => s.Rating = index))
            ).ToArray()
        ).HCenter();
    }

    VisualNode RenderUserAvatar(UserProfileDto? user, Color backgroundColor, Color iconColor, Action onTapped)
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var textColor = isLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary;

        // Show profile image if available, otherwise show default icon
        if (user != null && !string.IsNullOrEmpty(user.AvatarPath))
        {
            // Convert filename to full file path
            var imagePath = System.IO.Path.Combine(FileSystem.AppDataDirectory, user.AvatarPath);

            // User has a profile photo - display it
            return Border(
                Image(imagePath)
                    .Aspect(Aspect.AspectFill)
            )
            .StrokeShape(new RoundRectangle().CornerRadius(30))
            .BackgroundColor(backgroundColor)
            .HeightRequest(60)
            .WidthRequest(60)
            .OnTapped(onTapped);
        }
        else
        {
            // No profile photo - show default person icon
            return Border(
                Grid(
                    Label(MaterialSymbolsFont.Account_circle)
                        .FontFamily(MaterialSymbolsFont.FontFamily)
                        .FontSize(36)
                        .TextColor(user != null ? textColor : iconColor)
                        .HCenter()
                        .VCenter()
                )
            )
            .StrokeShape(new RoundRectangle().CornerRadius(30))
            .BackgroundColor(backgroundColor)
            .HeightRequest(60)
            .WidthRequest(60)
            .OnTapped(onTapped);
        }
    }

    async Task ShowUserSelectionPopup(string title, Action<UserProfileDto> onSelected)
    {
        // Create a simple wrapper class for display
        var userItems = State.AvailableUsers.Select(user => new UserSelectionItem
        {
            User = user,
            Name = user.Name,
            Icon = MaterialSymbolsFont.Account_circle,
            // Convert filename to full path
            AvatarPath = string.IsNullOrEmpty(user.AvatarPath)
                ? null
                : System.IO.Path.Combine(FileSystem.AppDataDirectory, user.AvatarPath)
        }).ToList();

        ListActionPopup? popup = null;
        popup = new ListActionPopup
        {
            Title = title,
            ShowActionButton = false,
            ItemsSource = userItems,
            ItemDataTemplate = new Microsoft.Maui.Controls.DataTemplate(() =>
            {
                var tapGesture = new Microsoft.Maui.Controls.TapGestureRecognizer();
                tapGesture.SetBinding(Microsoft.Maui.Controls.TapGestureRecognizer.CommandParameterProperty, ".");
                tapGesture.Tapped += async (s, e) =>
                {
                    if (e is Microsoft.Maui.Controls.TappedEventArgs args && args.Parameter is UserSelectionItem item)
                    {
                        onSelected(item.User);
                        await IPopupService.Current.PopAsync();
                    }
                };

                var layout = new Microsoft.Maui.Controls.HorizontalStackLayout
                {
                    Spacing = 12,
                    Padding = new Thickness(0, 8)
                };
                layout.GestureRecognizers.Add(tapGesture);

                // Avatar container - conditionally show image or icon
                var avatarContainer = new Microsoft.Maui.Controls.Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                    HeightRequest = 40,
                    WidthRequest = 40,
                    VerticalOptions = LayoutOptions.Center
                };

                // Bind to determine if we show image or icon
                var avatarPathBinding = new Microsoft.Maui.Controls.Binding("AvatarPath");
                avatarContainer.SetBinding(Microsoft.Maui.Controls.BindableObject.BindingContextProperty, ".");

                // Create both image and icon, we'll show one based on AvatarPath
                var avatarImage = new Microsoft.Maui.Controls.Image
                {
                    Aspect = Aspect.AspectFill,
                    HeightRequest = 40,
                    WidthRequest = 40
                };
                avatarImage.SetBinding(Microsoft.Maui.Controls.Image.SourceProperty, "AvatarPath");
                avatarImage.SetBinding(Microsoft.Maui.Controls.Image.IsVisibleProperty, new Microsoft.Maui.Controls.Binding("AvatarPath", converter: new NotNullOrEmptyConverter()));

                var icon = new Microsoft.Maui.Controls.Label
                {
                    FontFamily = MaterialSymbolsFont.FontFamily,
                    FontSize = 32,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.White
                };
                icon.SetBinding(Microsoft.Maui.Controls.Label.TextProperty, "Icon");
                icon.SetBinding(Microsoft.Maui.Controls.Label.IsVisibleProperty, new Microsoft.Maui.Controls.Binding("AvatarPath", converter: new NullOrEmptyConverter()));

                var avatarGrid = new Microsoft.Maui.Controls.Grid();
                avatarGrid.Children.Add(avatarImage);
                avatarGrid.Children.Add(icon);

                avatarContainer.Content = avatarGrid;

                var label = new Microsoft.Maui.Controls.Label
                {
                    FontSize = 16,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.White
                };
                label.SetBinding(Microsoft.Maui.Controls.Label.TextProperty, "Name");

                layout.Children.Add(avatarContainer);
                layout.Children.Add(label);

                return layout;
            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    async Task ShowEquipmentSelectionPopup()
    {
        // Close any existing popup first
        try { await IPopupService.Current.PopAsync(); } catch { }

        // Group equipment by type with display names
        var typeDisplayNames = new Dictionary<EquipmentType, string>
        {
            { EquipmentType.Machine, "Machines" },
            { EquipmentType.Grinder, "Grinders" },
            { EquipmentType.Tamper, "Tampers" },
            { EquipmentType.PuckScreen, "Puck Screens" },
            { EquipmentType.Other, "Other" }
        };

        // Build grouped equipment items for display
        var equipmentItems = State.AvailableEquipment
            .OrderBy(e => e.Type)
            .ThenBy(e => e.Name)
            .Select(e => new EquipmentSelectionItem
            {
                Equipment = e,
                Name = e.Name,
                GroupName = typeDisplayNames.GetValueOrDefault(e.Type, "Other"),
                IsSelected = IsEquipmentSelected(e)
            })
            .ToList();

        if (!equipmentItems.Any())
        {
            await _feedbackService.ShowErrorAsync("No equipment available", "Add equipment in Settings first");
            return;
        }

        var popup = new ListActionPopup
        {
            Title = "Select Equipment",
            ActionButtonText = "Done",
            ShowActionButton = true,
            ItemsSource = equipmentItems,
            ItemDataTemplate = new Microsoft.Maui.Controls.DataTemplate(() =>
            {
                var tapGesture = new Microsoft.Maui.Controls.TapGestureRecognizer();
                tapGesture.SetBinding(Microsoft.Maui.Controls.TapGestureRecognizer.CommandParameterProperty, ".");
                tapGesture.Tapped += (s, e) =>
                {
                    if (e is Microsoft.Maui.Controls.TappedEventArgs args && args.Parameter is EquipmentSelectionItem item)
                    {
                        ToggleEquipmentSelection(item.Equipment);
                    }
                };

                var layout = new Microsoft.Maui.Controls.HorizontalStackLayout
                {
                    Spacing = 12,
                    Padding = new Thickness(0, 8)
                };
                layout.GestureRecognizers.Add(tapGesture);

                // Checkbox icon
                var checkIcon = new Microsoft.Maui.Controls.Label
                {
                    FontFamily = MaterialSymbolsFont.FontFamily,
                    FontSize = 24,
                    VerticalOptions = LayoutOptions.Center
                };
                checkIcon.SetBinding(Microsoft.Maui.Controls.Label.TextProperty,
                    new Microsoft.Maui.Controls.Binding("IsSelected", converter: new BoolToCheckIconConverter()));
                checkIcon.SetBinding(Microsoft.Maui.Controls.Label.TextColorProperty,
                    new Microsoft.Maui.Controls.Binding("IsSelected", converter: new BoolToCheckColorConverter()));

                // Equipment name
                var nameLabel = new Microsoft.Maui.Controls.Label
                {
                    FontSize = 16,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.White
                };
                nameLabel.SetBinding(Microsoft.Maui.Controls.Label.TextProperty, "Name");

                // Group name (smaller, secondary)
                var groupLabel = new Microsoft.Maui.Controls.Label
                {
                    FontSize = 12,
                    VerticalOptions = LayoutOptions.Center,
                    TextColor = Colors.Gray
                };
                groupLabel.SetBinding(Microsoft.Maui.Controls.Label.TextProperty, "GroupName");

                var textStack = new Microsoft.Maui.Controls.VerticalStackLayout { Spacing = 2 };
                textStack.Children.Add(nameLabel);
                textStack.Children.Add(groupLabel);

                layout.Children.Add(checkIcon);
                layout.Children.Add(textStack);

                return layout;
            })
        };

        await IPopupService.Current.PushAsync(popup);
    }

    bool IsEquipmentSelected(EquipmentDto equipment)
    {
        return equipment.Type switch
        {
            EquipmentType.Machine => State.SelectedMachineId == equipment.Id,
            EquipmentType.Grinder => State.SelectedGrinderId == equipment.Id,
            _ => State.SelectedAccessoryIds.Contains(equipment.Id)
        };
    }

    void ToggleEquipmentSelection(EquipmentDto equipment)
    {
        SetState(s =>
        {
            switch (equipment.Type)
            {
                case EquipmentType.Machine:
                    // Toggle: if already selected, deselect; otherwise select this one
                    s.SelectedMachineId = s.SelectedMachineId == equipment.Id ? null : equipment.Id;
                    break;

                case EquipmentType.Grinder:
                    s.SelectedGrinderId = s.SelectedGrinderId == equipment.Id ? null : equipment.Id;
                    break;

                case EquipmentType.Tamper:
                case EquipmentType.PuckScreen:
                case EquipmentType.Other:
                    // For accessories, only one per type
                    var existingOfSameType = s.AvailableEquipment
                        .Where(e => e.Type == equipment.Type && s.SelectedAccessoryIds.Contains(e.Id))
                        .Select(e => e.Id)
                        .ToList();

                    // Remove any existing selection of the same type
                    foreach (var id in existingOfSameType)
                    {
                        s.SelectedAccessoryIds.Remove(id);
                    }

                    // If we didn't just deselect this item, add it
                    if (!existingOfSameType.Contains(equipment.Id))
                    {
                        s.SelectedAccessoryIds.Add(equipment.Id);
                    }
                    break;
            }
        });

        // Re-show the popup with updated selections
        _ = ShowEquipmentSelectionPopup();
    }

    // Helper class for user selection display
    class UserSelectionItem
    {
        public UserProfileDto User { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string? AvatarPath { get; set; }
    }

    // Helper class for equipment selection display
    class EquipmentSelectionItem
    {
        public EquipmentDto Equipment { get; set; } = null!;
        public string Name { get; set; } = string.Empty;
        public string GroupName { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    VisualNode RenderAdviceDisplay()
    {
        if (State.AdviceResponse == null) return null!;

        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var secondaryTextColor = isLightTheme ? AppColors.Light.TextSecondary : AppColors.Dark.TextSecondary;

        // Error state
        if (!State.AdviceResponse.Success)
        {
            return Border(
                VStack(spacing: 12,
                    // Header with dismiss button
                    Grid("Auto", "Auto, *, Auto",
                        Label(MaterialSymbolsFont.Error)
                            .FontFamily(MaterialSymbolsFont.FontFamily)
                            .FontSize(24)
                            .TextColor(Colors.Red)
                            .VCenter()
                            .GridColumn(0),
                        Label("Could not get advice")
                            .ThemeKey(ThemeKeys.CardTitle)
                            .TextColor(Colors.Red)
                            .VCenter()
                            .Margin(8, 0, 0, 0)
                            .GridColumn(1),
                        // Dismiss button
                        Border(
                            Label(MaterialSymbolsFont.Close)
                                .FontFamily(MaterialSymbolsFont.FontFamily)
                                .FontSize(18)
                                .TextColor(secondaryTextColor)
                                .HCenter()
                                .VCenter()
                        )
                        .StrokeThickness(0)
                        .HeightRequest(32)
                        .WidthRequest(32)
                        .OnTapped(() => SetState(s => s.ShowAdviceSection = false))
                        .GridColumn(2)
                    ),

                    Label(State.AdviceResponse.ErrorMessage ?? "An error occurred")
                        .ThemeKey(ThemeKeys.SecondaryText)
                        .LineBreakMode(Microsoft.Maui.LineBreakMode.WordWrap),

                    Button("Try Again")
                        .OnClicked(async () => await RequestAdviceAsync())
                        .HCenter()
                )
                .Padding(16)
            )
            .BackgroundColor(Colors.Red.WithAlpha(0.1f))
            .Stroke(Colors.Red.WithAlpha(0.3f))
            .StrokeThickness(1)
            .Margin(0, AppSpacing.M, 0, AppSpacing.M);
        }

        // Success state
        var primaryColor = isLightTheme ? AppColors.Light.Primary : AppColors.Dark.Primary;

        return Border(
            VStack(spacing: 12,
                // Header with dismiss button
                Grid("Auto", "Auto, *, Auto",
                    Label(MaterialSymbolsFont.Auto_awesome)
                        .FontFamily(MaterialSymbolsFont.FontFamily)
                        .FontSize(20)
                        .TextColor(primaryColor)
                        .VCenter()
                        .GridColumn(0),
                    Label("AI Suggestions")
                        .ThemeKey(ThemeKeys.CardTitle)
                        .VCenter()
                        .Margin(8, 0, 0, 0)
                        .GridColumn(1),
                    // Dismiss button
                    Border(
                        Label(MaterialSymbolsFont.Close)
                            .FontFamily(MaterialSymbolsFont.FontFamily)
                            .FontSize(18)
                            .TextColor(secondaryTextColor)
                            .HCenter()
                            .VCenter()
                    )
                    .StrokeThickness(0)
                    .HeightRequest(32)
                    .WidthRequest(32)
                    .OnTapped(() => SetState(s => s.ShowAdviceSection = false))
                    .GridColumn(2)
                ),

                // Display structured adjustments
                RenderAdviceContent()
            )
            .Padding(16)
        )
        .ThemeKey(ThemeKeys.CardBorder)
        .Margin(0, 0, 0, AppSpacing.M);
    }

    /// <summary>
    /// Renders the structured AI advice content with adjustments and reasoning.
    /// </summary>
    VisualNode RenderAdviceContent()
    {
        var response = State.AdviceResponse;
        if (response == null)
            return Label("").IsVisible(false);

        var adjustments = response.Adjustments ?? [];
        var reasoning = response.Reasoning;
        var source = response.Source;

        var children = new List<VisualNode>();

        // Adjustments list
        if (adjustments.Count > 0)
        {
            foreach (var adj in adjustments)
            {
                children.Add(
                    HStack(spacing: 8,
                        Label("•")
                            .ThemeKey(ThemeKeys.SecondaryText)
                            .FontSize(14),
                        Label($"{FormatAdjustmentDirection(adj.Direction)} {adj.Parameter} by {adj.Amount}")
                            .ThemeKey(ThemeKeys.PrimaryText)
                            .FontSize(14)
                            .LineBreakMode(Microsoft.Maui.LineBreakMode.WordWrap)
                    )
                );
            }
        }
        else
        {
            children.Add(
                Label("No specific adjustments suggested.")
                    .ThemeKey(ThemeKeys.SecondaryText)
                    .FontSize(14)
            );
        }

        // Reasoning
        if (!string.IsNullOrWhiteSpace(reasoning))
        {
            children.Add(
                Label(reasoning)
                    .ThemeKey(ThemeKeys.SecondaryText)
                    .FontSize(13)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Italic)
                    .Margin(0, 4, 0, 0)
                    .LineBreakMode(Microsoft.Maui.LineBreakMode.WordWrap)
            );
        }

        // Source attribution and visibility toggle row
        children.Add(
            HStack(spacing: 8,
                // Visibility toggle button
                ImageButton()
                    .Source(State.ShowPromptDetails ? AppIcons.VisibilityOff : AppIcons.Visibility)
                    .HeightRequest(28)
                    .WidthRequest(28)
                    .Aspect(Aspect.Center)
                    .VCenter()
                    .BackgroundColor(Colors.Transparent)
                    .OnClicked(() => SetState(s => s.ShowPromptDetails = !s.ShowPromptDetails)),

                // Source attribution (pushed to right)
                Label(source ?? "")
                    .ThemeKey(ThemeKeys.MutedText)
                    .HEnd()
                    .VCenter()
                    .HorizontalOptions(Microsoft.Maui.Controls.LayoutOptions.Fill)
            )
            .HEnd()
            .Margin(0, 8, 0, 0)
        );

        // Prompt details (collapsible)
        if (State.ShowPromptDetails && !string.IsNullOrWhiteSpace(response.PromptSent))
        {
            children.Add(RenderPromptDetails(response));
        }

        return VStack(spacing: 8, children.ToArray());
    }

    /// <summary>
    /// Renders the prompt details for transparency, with abbreviated history.
    /// </summary>
    VisualNode RenderPromptDetails(AIAdviceResponseDto response)
    {
        var formattedPrompt = FormatPromptForDisplay(response.PromptSent!, response.HistoricalShotsCount);

        return Border(
            VStack(spacing: 4,
                Label("Information sent to AI:")
                    .ThemeKey(ThemeKeys.MutedText)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                Label(formattedPrompt)
                    .ThemeKey(ThemeKeys.MutedText)
                    .LineBreakMode(Microsoft.Maui.LineBreakMode.WordWrap)
            )
        )
        .ThemeKey(ThemeKeys.PromptDetails)
        .Margin(0, 8, 0, 0);
    }

    /// <summary>
    /// Formats the prompt for display, abbreviating long shot history.
    /// </summary>
    static string FormatPromptForDisplay(string prompt, int totalHistoricalShots)
    {
        var lines = prompt.Split('\n').ToList();
        var result = new List<string>();
        var inHistorySection = false;
        var historyLinesShown = 0;
        const int maxHistoryLines = 3;

        foreach (var line in lines)
        {
            // Detect history section headers
            if (line.Contains("Previous Shots") || line.Contains("Best rated shots") || line.Contains("Most recent shots"))
            {
                inHistorySection = true;
                historyLinesShown = 0;
                result.Add(line);
                continue;
            }

            // Detect section change (new ## header or question)
            if (line.StartsWith("##") || line.StartsWith("Based on"))
            {
                // If we were in history and have more shots, add summary
                if (inHistorySection && totalHistoricalShots > maxHistoryLines)
                {
                    result.Add($"  ... and {totalHistoricalShots - maxHistoryLines} more shots");
                }
                inHistorySection = false;
            }

            // In history section, limit lines shown
            if (inHistorySection && line.TrimStart().StartsWith("-"))
            {
                historyLinesShown++;
                if (historyLinesShown <= maxHistoryLines)
                {
                    result.Add(line);
                }
                continue;
            }

            result.Add(line);
        }

        return string.Join("\n", result);
    }

    /// <summary>
    /// Formats the adjustment direction for display (capitalizes first letter).
    /// </summary>
    static string FormatAdjustmentDirection(string direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
            return string.Empty;
        return char.ToUpper(direction[0]) + direction.Substring(1).ToLower();
    }

    async Task RequestAdviceAsync()
    {
        if (!Props.ShotId.HasValue) return;

        if (!await _aiAdviceService.IsConfiguredAsync())
        {
            await _feedbackService.ShowErrorAsync("AI advice is not available", "Please update the app or contact support.");
            return;
        }

        SetState(s =>
        {
            s.IsLoadingAdvice = true;
            s.ShowAdviceSection = true;
            s.AdviceResponse = null;
        });

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _aiAdviceService.GetAdviceForShotAsync(Props.ShotId.Value, cts.Token);

            // var popup = new SimpleTextPopup
            // {
            //     Title = "AI Advice Retrieved",
            //     Text = response.Advice ?? "No advice was generated."
            // };
            // await IPopupService.Current.PushAsync(popup);

            SetState(s =>
            {
                s.IsLoadingAdvice = false;
                s.AdviceResponse = response;
            });

            if (!response.Success)
            {
                await _feedbackService.ShowErrorAsync("Could not get advice", response.ErrorMessage);
            }
        }
        catch (OperationCanceledException)
        {
            await _feedbackService.ShowErrorAsync("Request timed out", "Please try again.");
            SetState(s => s.IsLoadingAdvice = false);
        }
        catch (Exception ex)
        {
            await _feedbackService.ShowErrorAsync("Failed to get advice", ex.Message);
            SetState(s => s.IsLoadingAdvice = false);
        }
    }
}

// Converter classes
class BoolToCheckIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool isSelected && isSelected
            ? MaterialSymbolsFont.Check_circle
            : MaterialSymbolsFont.Radio_button_unchecked;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Converter to show check color based on selection state
class BoolToCheckColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool isSelected && isSelected
            ? AppColors.Light.Primary
            : Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Converter to check if string is null or empty
class NullOrEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value == null || (value is string str && string.IsNullOrEmpty(str));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

// Converter to check if string is NOT null or empty
class NotNullOrEmptyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value != null && value is string str && !string.IsNullOrEmpty(str);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
