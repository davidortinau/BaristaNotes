using Microsoft.Extensions.Logging;
using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Services.Grind;

/// <summary>
/// Orchestrates the grind-translation resolution chain. See
/// <see cref="IGrindTranslationService"/> for the priority order.
/// </summary>
public class GrindTranslationService : IGrindTranslationService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromDays(90);

    private readonly IEquipmentRepository _equipment;
    private readonly IGrinderProfileRepository _profiles;
    private readonly IGrindTranslationCacheRepository _cache;
    private readonly IShotRecordRepository _shots;
    private readonly IGrindTranslationAI? _ai;
    private readonly ILogger<GrindTranslationService> _logger;

    public GrindTranslationService(
        IEquipmentRepository equipment,
        IGrinderProfileRepository profiles,
        IGrindTranslationCacheRepository cache,
        IShotRecordRepository shots,
        ILogger<GrindTranslationService> logger,
        IGrindTranslationAI? ai = null)
    {
        _equipment = equipment;
        _profiles = profiles;
        _cache = cache;
        _shots = shots;
        _logger = logger;
        _ai = ai;
    }

    public async Task<GrindTranslationResult> TranslateAsync(
        GrindTranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        var parsed = GrindHintParser.Parse(request.GrindHint);
        var modelNorm = NormalizeModel(request.GrinderModel);

        _logger.LogDebug(
            "Grind translate: model={Model} kind={Kind} hint={Hint} method={Method}",
            modelNorm, parsed.Kind, request.GrindHint, request.Method);

        GrinderProfile? profile = null;
        if (request.EquipmentId.HasValue)
        {
            profile = await _profiles.GetByEquipmentIdAsync(request.EquipmentId.Value);
        }

        // 1) User history lookup. We use the prior shot's GrindMicrons
        // (canonical) and interpolate against this grinder's anchors to
        // give a grinder-native suggestion.
        if (request.EquipmentId.HasValue)
        {
            var recent = await _shots.GetMostRecentWithGrindAsync(
                request.EquipmentId.Value, request.BeanId, request.Method);
            if (recent != null && recent.GrindMicrons.HasValue)
            {
                var historyParsed = parsed with { Microns = recent.GrindMicrons.Value };
                var deterministic = TryDeterministic(historyParsed, profile);
                return BuildResult(
                    min: deterministic?.Min,
                    max: deterministic?.Max,
                    suggested: deterministic?.Suggested,
                    parsed: parsed,
                    source: GrindTranslationSource.UserHistory,
                    confidence: deterministic != null ? "high" : "low",
                    explanation: $"From your last {FormatMethod(request.Method)} on this grinder.",
                    request: request);
            }
        }

        // 2) Deterministic
        var interp = TryDeterministic(parsed, profile);
        if (interp != null)
        {
            return BuildResult(
                min: interp.Min,
                max: interp.Max,
                suggested: interp.Suggested,
                parsed: parsed,
                source: GrindTranslationSource.Deterministic,
                confidence: interp.AnchorsUsed >= 3 ? "medium" : "low",
                explanation: $"Calculated from calibration anchors for {request.GrinderModel}.",
                request: request);
        }

        // 3) Cache
        var cached = await _cache.FindAsync(modelNorm, parsed.Normalized, request.Method);
        if (cached != null)
        {
            return new GrindTranslationResult(
                MinSetting: cached.MinSetting,
                MaxSetting: cached.MaxSetting,
                SuggestedSetting: cached.SuggestedSetting,
                ParsedKind: parsed.Kind,
                Source: GrindTranslationSource.Cache,
                ConfidenceLabel: cached.Confidence,
                Explanation: cached.Explanation,
                GrinderModel: request.GrinderModel);
        }

        // 4) AI
        if (_ai != null)
        {
            try
            {
                var ai = await _ai.TranslateAsync(request.GrinderModel, request.Method, request.GrindHint, cancellationToken);
                if (ai != null && (ai.SuggestedSetting.HasValue || ai.MinSetting.HasValue))
                {
                    await PersistAiResultAsync(request, parsed, modelNorm, profile, ai);

                    return new GrindTranslationResult(
                        MinSetting: ai.MinSetting,
                        MaxSetting: ai.MaxSetting,
                        SuggestedSetting: ai.SuggestedSetting ?? AvgOrNull(ai.MinSetting, ai.MaxSetting),
                        ParsedKind: parsed.Kind,
                        Source: GrindTranslationSource.AI,
                        ConfidenceLabel: NormalizeConfidence(ai.Confidence),
                        Explanation: ai.Explanation,
                        GrinderModel: request.GrinderModel);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Grind translation AI request failed for model {Model}", modelNorm);
            }
        }

        // 5) Default — return whatever parsed info we have so the UI can still render
        return new GrindTranslationResult(
            MinSetting: null,
            MaxSetting: null,
            SuggestedSetting: null,
            ParsedKind: parsed.Kind,
            Source: GrindTranslationSource.Default,
            ConfidenceLabel: "low",
            Explanation: parsed.Kind == GrindHintKind.Unknown
                ? "Recipe grind couldn't be interpreted; try adjusting manually."
                : "Register your grinder to get a personalized setting.",
            GrinderModel: request.GrinderModel);
    }

    private InterpolationResult? TryDeterministic(ParsedGrindHint parsed, GrinderProfile? profile)
    {
        if (!parsed.Microns.HasValue) return null;

        var anchors = DeterministicGrindInterpolator.ParseAnchors(profile?.AnchorsJson);
        if (anchors.Count < 2)
        {
            // Fall back to seed anchors when the profile is empty but the
            // grinder model matches a known seeded family.
            var seeded = KnownGrinderSeeds.TryGet(profile?.Equipment?.Name);
            if (seeded != null)
            {
                anchors = seeded;
            }
            else
            {
                return null;
            }
        }

        return DeterministicGrindInterpolator.Interpolate(
            anchors,
            parsed.Microns.Value,
            parsed.MicronRange,
            profile?.MinSetting,
            profile?.MaxSetting);
    }

    private async Task PersistAiResultAsync(
        GrindTranslationRequest request,
        ParsedGrindHint parsed,
        string modelNorm,
        GrinderProfile? profile,
        GrindTranslationAIResponse ai)
    {
        var now = DateTime.UtcNow;

        await _cache.UpsertAsync(new GrindTranslationCache
        {
            GrinderModelNormalized = modelNorm,
            GrindHintNormalized = parsed.Normalized,
            BrewMethod = request.Method,
            MinSetting = ai.MinSetting,
            MaxSetting = ai.MaxSetting,
            SuggestedSetting = ai.SuggestedSetting,
            Confidence = NormalizeConfidence(ai.Confidence),
            Source = "AI",
            Explanation = ai.Explanation,
            CreatedAt = now,
            ExpiresAt = now.Add(CacheTtl),
        });

        // If AI provided clean anchors AND we have a registered grinder,
        // merge them into the profile so future deterministic calls improve.
        if (profile != null && ai.MicronAnchors is { Count: > 0 })
        {
            var existing = DeterministicGrindInterpolator.ParseAnchors(profile.AnchorsJson).ToList();
            foreach (var a in ai.MicronAnchors)
            {
                if (a.Micron <= 0) continue;
                existing.RemoveAll(e => Math.Abs(e.Micron - a.Micron) < 10m);
                existing.Add(new GrindAnchor(a.Micron, a.Setting, "ai", now));
            }
            profile.AnchorsJson = DeterministicGrindInterpolator.SerializeAnchors(
                existing.OrderBy(a => a.Micron));
            profile.LastModifiedAt = now;
            await _profiles.UpdateAsync(profile);
        }
    }

    private GrindTranslationResult BuildResult(
        decimal? min, decimal? max, decimal? suggested,
        ParsedGrindHint parsed,
        GrindTranslationSource source,
        string confidence,
        string? explanation,
        GrindTranslationRequest request)
    {
        return new GrindTranslationResult(
            MinSetting: min,
            MaxSetting: max,
            SuggestedSetting: suggested,
            ParsedKind: parsed.Kind,
            Source: source,
            ConfidenceLabel: confidence,
            Explanation: explanation,
            GrinderModel: request.GrinderModel);
    }

    private static string NormalizeModel(string model) =>
        (model ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeConfidence(string? c)
    {
        var v = (c ?? "low").Trim().ToLowerInvariant();
        return v is "low" or "medium" or "high" ? v : "low";
    }

    private static decimal? AvgOrNull(decimal? a, decimal? b)
    {
        if (a.HasValue && b.HasValue) return Math.Round((a.Value + b.Value) / 2m, 2);
        return a ?? b;
    }

    private static string FormatMethod(BrewMethod m) => m switch
    {
        BrewMethod.Espresso => "espresso",
        BrewMethod.PourOver => "pour-over",
        BrewMethod.V60 => "V60",
        BrewMethod.Moka => "moka",
        BrewMethod.Drip => "drip",
        BrewMethod.Aeropress => "Aeropress",
        BrewMethod.FrenchPress => "French press",
        BrewMethod.Turkish => "Turkish",
        BrewMethod.Siphon => "siphon",
        BrewMethod.Cupping => "cupping",
        BrewMethod.ColdBrew => "cold brew",
        BrewMethod.ColdDrip => "cold drip",
        BrewMethod.SteepAndRelease => "steep-and-release",
        _ => "drink",
    };
}
