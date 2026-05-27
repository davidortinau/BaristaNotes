using System.ComponentModel;
using System.Text;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Maui.AI.Attributes;

namespace BaristaNotes.Services.AI;

/// <summary>
/// AI tool surface for reading and writing learned persona context on a
/// <c>UserProfile</c>. The Context field is free-form text the model uses
/// to tailor brewing advice to a person's stated preferences.
/// </summary>
public class ProfileContextTools
{
    private const int MaxContextLength = 2000;

    private readonly IUserProfileService _userProfileService;
    private readonly IShotService _shotService;
    private readonly ILogger<ProfileContextTools> _logger;

    public ProfileContextTools(
        IUserProfileService userProfileService,
        IShotService shotService,
        ILogger<ProfileContextTools> logger)
    {
        _userProfileService = userProfileService;
        _shotService = shotService;
        _logger = logger;
    }

    [Description("Reads the learned persona context (stated coffee preferences) for a user profile. Call before making a recommendation for someone, or before deciding to update their context.")]
    [ExportAIFunction("get_profile_context")]
    public async Task<string> GetProfileContextAsync(
        [Description("The profile ID (integer) whose context to read")] int profileId)
    {
        _logger.LogInformation("GetProfileContext tool called: {ProfileId}", profileId);

        try
        {
            var profile = await _userProfileService.GetProfileByIdAsync(profileId);
            if (profile is null)
                return $"No profile found with ID {profileId}.";

            if (string.IsNullOrWhiteSpace(profile.Context))
                return $"{profile.Name} (ID:{profileId}) has no recorded context yet.";

            return $"{profile.Name} (ID:{profileId}) context:\n{profile.Context}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading profile context for {ProfileId}", profileId);
            return "Sorry, I couldn't read that profile's context.";
        }
    }

    [Description("Appends a new learned preference or note to a user profile's persona context. Existing context is preserved. Use short factual sentences (e.g. 'Prefers single-origin pour overs in the morning.'). Cap is 2000 chars total.")]
    [ExportAIFunction("append_profile_context")]
    public async Task<string> AppendProfileContextAsync(
        [Description("The profile ID (integer) to update")] int profileId,
        [Description("A short factual sentence to add to their context. One observation at a time.")] string note)
    {
        _logger.LogInformation("AppendProfileContext tool called: {ProfileId}", profileId);

        if (string.IsNullOrWhiteSpace(note))
            return "Nothing to append (note was empty).";

        try
        {
            var profile = await _userProfileService.GetProfileByIdAsync(profileId);
            if (profile is null)
                return $"No profile found with ID {profileId}.";

            var trimmedNote = note.Trim();
            var existing = profile.Context ?? string.Empty;
            var merged = string.IsNullOrWhiteSpace(existing)
                ? trimmedNote
                : $"{existing.TrimEnd()}\n{trimmedNote}";

            if (merged.Length > MaxContextLength)
            {
                _logger.LogWarning("AppendProfileContext would exceed cap ({Len}/{Max}) for {ProfileId}", merged.Length, MaxContextLength, profileId);
                return $"Cannot append: would exceed {MaxContextLength}-character cap (currently {existing.Length}, attempted add of {trimmedNote.Length}). Consider summarizing existing context first.";
            }

            await _userProfileService.UpdateProfileAsync(profileId, new UpdateUserProfileDto
            {
                Context = merged
            });

            return $"Added to {profile.Name}'s context. Now at {merged.Length}/{MaxContextLength} chars.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending profile context for {ProfileId}", profileId);
            return "Sorry, I couldn't update that profile's context.";
        }
    }

    [Description("Replaces a user profile's persona context with new text (e.g. a summary). Use this when consolidating or rewriting context. Cap is 2000 chars.")]
    [ExportAIFunction("set_profile_context")]
    public async Task<string> SetProfileContextAsync(
        [Description("The profile ID (integer) to update")] int profileId,
        [Description("The full replacement context text (max 2000 chars). Pass empty string to clear.")] string context)
    {
        _logger.LogInformation("SetProfileContext tool called: {ProfileId}", profileId);

        try
        {
            var profile = await _userProfileService.GetProfileByIdAsync(profileId);
            if (profile is null)
                return $"No profile found with ID {profileId}.";

            var trimmed = (context ?? string.Empty).Trim();
            if (trimmed.Length > MaxContextLength)
                return $"Cannot set: text is {trimmed.Length} chars, cap is {MaxContextLength}.";

            await _userProfileService.UpdateProfileAsync(profileId, new UpdateUserProfileDto
            {
                // UpdateProfileAsync treats empty string as explicit clear; null = no change.
                Context = trimmed.Length == 0 ? string.Empty : trimmed
            });

            return trimmed.Length == 0
                ? $"Cleared {profile.Name}'s context."
                : $"Replaced {profile.Name}'s context ({trimmed.Length}/{MaxContextLength} chars).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting profile context for {ProfileId}", profileId);
            return "Sorry, I couldn't update that profile's context.";
        }
    }

    [Description("Summarizes a person's shot history (brew methods, beans, ratings, tasting notes) into a brief draft suitable for their profile context. Returns proposed text — DOES NOT save. Call set_profile_context or append_profile_context to persist after user confirms.")]
    [ExportAIFunction("summarize_preferences_from_history")]
    public async Task<string> SummarizePreferencesFromHistoryAsync(
        [Description("The profile ID (integer) whose shots to summarize")] int profileId)
    {
        _logger.LogInformation("SummarizePreferencesFromHistory tool called: {ProfileId}", profileId);

        try
        {
            var profile = await _userProfileService.GetProfileByIdAsync(profileId);
            if (profile is null)
                return $"No profile found with ID {profileId}.";

            // Pull a reasonable window of shots made for this person.
            var page = await _shotService.GetShotHistoryByUserAsync(profileId, pageIndex: 0, pageSize: 50);
            var shots = page.Items;
            if (shots.Count == 0)
                return $"{profile.Name} has no logged shots yet — nothing to summarize.";

            // Aggregate facts the model can reason over. We deliberately do NOT
            // generate prose here; we hand the model structured facts and let it
            // compose the summary (it can then call set/append_profile_context).
            var byBrewMethod = shots
                .GroupBy(s => s.BrewMethod.DisplayName())
                .Select(g => new { Method = g.Key, Count = g.Count(), AvgRating = g.Where(s => s.Rating.HasValue).DefaultIfEmpty().Average(s => s?.Rating ?? 0) })
                .OrderByDescending(x => x.Count)
                .ToList();

            var topBeans = shots
                .Where(s => s.Bag != null && !string.IsNullOrWhiteSpace(s.Bag.BeanName))
                .GroupBy(s => s.Bag!.BeanName)
                .Select(g => new { Bean = g.Key, Count = g.Count(), AvgRating = g.Where(s => s.Rating.HasValue).DefaultIfEmpty().Average(s => s?.Rating ?? 0) })
                .OrderByDescending(x => x.AvgRating)
                .ThenByDescending(x => x.Count)
                .Take(5)
                .ToList();

            var tastingSamples = shots
                .Where(s => !string.IsNullOrWhiteSpace(s.TastingNotes))
                .OrderByDescending(s => s.Rating ?? -1)
                .Take(5)
                .Select(s => s.TastingNotes!.Trim())
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Facts for {profile.Name} (ID:{profileId}) drawn from {shots.Count} logged shot(s):");
            sb.AppendLine();
            sb.AppendLine("Brew methods used:");
            foreach (var m in byBrewMethod)
                sb.AppendLine($"- {m.Method}: {m.Count} shot(s), avg rating {m.AvgRating:F1}/4");

            if (topBeans.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Top-rated beans:");
                foreach (var b in topBeans)
                    sb.AppendLine($"- {b.Bean}: {b.Count} shot(s), avg rating {b.AvgRating:F1}/4");
            }

            if (tastingSamples.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Sample tasting notes:");
                foreach (var t in tastingSamples)
                    sb.AppendLine($"- \"{t}\"");
            }

            if (!string.IsNullOrWhiteSpace(profile.Context))
            {
                sb.AppendLine();
                sb.AppendLine("Existing context (do not duplicate):");
                sb.AppendLine(profile.Context);
            }

            sb.AppendLine();
            sb.AppendLine("Draft 2-4 short factual preference sentences from these facts, then call set_profile_context (replace) or append_profile_context (add) to save. Do not invent details not supported by the facts above.");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing preferences for {ProfileId}", profileId);
            return "Sorry, I couldn't summarize their preferences.";
        }
    }
}
