using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Services.Grind;

/// <summary>
/// Abstraction over the AI provider used to translate a recipe grind hint
/// into a grinder-specific setting. Implementations are responsible for
/// client selection (on-device vs cloud), timeouts, and fallback. Returns
/// <c>null</c> when no provider is available or every attempt failed — the
/// orchestrator treats that as a non-fatal miss.
/// </summary>
public interface IGrindTranslationAI
{
    Task<GrindTranslationAIResponse?> TranslateAsync(
        string grinderModel,
        BrewMethod method,
        string grindHint,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates grind-hint translation using the resolution chain:
/// (1) user history → (2) deterministic interpolation → (3) cache →
/// (4) AI → (5) default. Persists AI results into the translation cache
/// and, when anchors are provided, back into the grinder profile so future
/// deterministic lookups self-improve.
/// </summary>
public interface IGrindTranslationService
{
    Task<GrindTranslationResult> TranslateAsync(
        GrindTranslationRequest request,
        CancellationToken cancellationToken = default);
}
