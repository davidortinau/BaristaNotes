using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Service for processing voice commands using AI interpretation.
/// </summary>
public interface IVoiceCommandService
{
    /// <summary>
    /// Interprets a voice command transcript and returns the parsed intent and parameters.
    /// </summary>
    /// <param name="request">The voice command request with transcript.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The interpreted command with intent and parameters.</returns>
    Task<VoiceCommandResponseDto> InterpretCommandAsync(
        VoiceCommandRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a voice command after interpretation.
    /// </summary>
    /// <param name="response">The interpreted command response.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of executing the command.</returns>
    Task<VoiceToolResultDto> ExecuteCommandAsync(
        VoiceCommandResponseDto response,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Interprets and executes a voice command in one step (for non-confirmation flows).
    /// </summary>
    /// <param name="request">The voice command request with transcript.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of executing the command.</returns>
    Task<VoiceToolResultDto> ProcessCommandAsync(
        VoiceCommandRequestDto request,
        CancellationToken cancellationToken = default);
}
