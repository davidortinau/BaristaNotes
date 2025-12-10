using System.Text;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Utility class for building AI prompts from shot context.
/// Extracted for testability from the MAUI-dependent AIAdviceService.
/// </summary>
public static class AIPromptBuilder
{
    /// <summary>
    /// Builds the user prompt from shot context.
    /// </summary>
    public static string BuildPrompt(AIAdviceRequestDto context)
    {
        var sb = new StringBuilder();

        // Current shot info
        sb.AppendLine("## Current Shot");
        sb.AppendLine($"- Dose: {context.CurrentShot.DoseIn}g in");
        if (context.CurrentShot.ActualOutput.HasValue)
            sb.AppendLine($"- Yield: {context.CurrentShot.ActualOutput}g out");
        if (context.CurrentShot.ActualTime.HasValue)
            sb.AppendLine($"- Time: {context.CurrentShot.ActualTime}s");
        if (!string.IsNullOrWhiteSpace(context.CurrentShot.GrindSetting))
            sb.AppendLine($"- Grind: {context.CurrentShot.GrindSetting}");
        if (context.CurrentShot.Rating.HasValue)
            sb.AppendLine($"- Rating: {context.CurrentShot.Rating}/4");
        if (!string.IsNullOrWhiteSpace(context.CurrentShot.TastingNotes))
            sb.AppendLine($"- Tasting notes: {context.CurrentShot.TastingNotes}");

        // Bean info
        sb.AppendLine();
        sb.AppendLine("## Bean Information");
        sb.AppendLine($"- Name: {context.BeanInfo.Name}");
        if (!string.IsNullOrWhiteSpace(context.BeanInfo.Roaster))
            sb.AppendLine($"- Roaster: {context.BeanInfo.Roaster}");
        if (!string.IsNullOrWhiteSpace(context.BeanInfo.Origin))
            sb.AppendLine($"- Origin: {context.BeanInfo.Origin}");
        sb.AppendLine($"- Days since roast: {context.BeanInfo.DaysFromRoast}");
        if (!string.IsNullOrWhiteSpace(context.BeanInfo.Notes))
            sb.AppendLine($"- Flavor notes: {context.BeanInfo.Notes}");

        // Equipment if available
        if (context.Equipment != null)
        {
            sb.AppendLine();
            sb.AppendLine("## Equipment");
            if (!string.IsNullOrWhiteSpace(context.Equipment.MachineName))
                sb.AppendLine($"- Machine: {context.Equipment.MachineName}");
            if (!string.IsNullOrWhiteSpace(context.Equipment.GrinderName))
                sb.AppendLine($"- Grinder: {context.Equipment.GrinderName}");
        }

        // Historical shots (best rated first)
        if (context.HistoricalShots.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Previous Shots (same beans, sorted by rating)");

            var bestShots = context.HistoricalShots
                .Where(s => s.Rating.HasValue && s.Rating >= 3)
                .Take(5)
                .ToList();

            if (bestShots.Count > 0)
            {
                sb.AppendLine("Best rated shots:");
                foreach (var shot in bestShots)
                {
                    var details = new List<string>();
                    details.Add($"{shot.DoseIn}g in");
                    if (shot.ActualOutput.HasValue) details.Add($"{shot.ActualOutput}g out");
                    if (shot.ActualTime.HasValue) details.Add($"{shot.ActualTime}s");
                    if (!string.IsNullOrWhiteSpace(shot.GrindSetting)) details.Add($"grind {shot.GrindSetting}");
                    details.Add($"rated {shot.Rating}/4");
                    sb.AppendLine($"- {string.Join(", ", details)}");
                }
            }

            var recentShots = context.HistoricalShots
                .OrderByDescending(s => s.Timestamp)
                .Take(3)
                .ToList();

            if (recentShots.Count > 0)
            {
                sb.AppendLine("Most recent shots:");
                foreach (var shot in recentShots)
                {
                    var details = new List<string>();
                    details.Add($"{shot.DoseIn}g in");
                    if (shot.ActualOutput.HasValue) details.Add($"{shot.ActualOutput}g out");
                    if (shot.ActualTime.HasValue) details.Add($"{shot.ActualTime}s");
                    if (shot.Rating.HasValue) details.Add($"rated {shot.Rating}/4");
                    sb.AppendLine($"- {string.Join(", ", details)}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine("Based on this shot and my history, what adjustments would you suggest to improve my next shot?");

        return sb.ToString();
    }

    /// <summary>
    /// Builds a brief prompt for passive insights.
    /// </summary>
    public static string BuildPassivePrompt(AIAdviceRequestDto context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Shot: {context.CurrentShot.DoseIn}g in");
        if (context.CurrentShot.ActualOutput.HasValue)
            sb.Append($", {context.CurrentShot.ActualOutput}g out");
        if (context.CurrentShot.ActualTime.HasValue)
            sb.Append($", {context.CurrentShot.ActualTime}s");
        if (!string.IsNullOrWhiteSpace(context.CurrentShot.GrindSetting))
            sb.Append($", grind {context.CurrentShot.GrindSetting}");

        if (context.HistoricalShots.Any(s => s.Rating >= 3))
        {
            var best = context.HistoricalShots.First(s => s.Rating >= 3);
            sb.AppendLine();
            sb.Append($"Best shot was: {best.DoseIn}g in");
            if (best.ActualOutput.HasValue)
                sb.Append($", {best.ActualOutput}g out");
            if (best.ActualTime.HasValue)
                sb.Append($", {best.ActualTime}s");
        }

        sb.AppendLine();
        sb.AppendLine("Quick tip?");

        return sb.ToString();
    }
}
