using System.Text;
using BaristaNotes.Core.Models.Enums;
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
        sb.AppendLine($"- Brew method: {context.CurrentShot.BrewMethod.DisplayName()}");
        if (!string.IsNullOrWhiteSpace(context.CurrentShot.DrinkType))
            sb.AppendLine($"- Drink type: {context.CurrentShot.DrinkType}");
        sb.AppendLine($"- Dose: {context.CurrentShot.DoseIn}g in");
        if (context.CurrentShot.ActualOutput.HasValue)
            sb.AppendLine($"- Yield: {context.CurrentShot.ActualOutput}g out");
        if (context.CurrentShot.ActualTime.HasValue)
            sb.AppendLine($"- Time: {context.CurrentShot.ActualTime}s");
        if (context.CurrentShot.GrindMicrons.HasValue)
            sb.AppendLine($"- Grind: {context.CurrentShot.GrindMicrons}µm");
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

        // Persona this shot was made for (if any)
        if (context.MadeFor != null && !string.IsNullOrWhiteSpace(context.MadeFor.Context))
        {
            sb.AppendLine();
            sb.AppendLine($"## Made For: {context.MadeFor.Name}");
            sb.AppendLine("Persona context (their stated coffee preferences — weight advice accordingly):");
            sb.AppendLine(context.MadeFor.Context);
        }
        else if (context.MadeFor != null)
        {
            sb.AppendLine();
            sb.AppendLine($"## Made For: {context.MadeFor.Name}");
            sb.AppendLine("(No persona preferences recorded yet.)");
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
                    if (shot.GrindMicrons.HasValue) details.Add($"grind {shot.GrindMicrons}µm");
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
        sb.AppendLine($"Based on this {context.CurrentShot.BrewMethod.DisplayName()} brew, the drink type, and my history, what adjustments would you suggest to improve my next one? Keep advice grounded in the brewing method above — do not import assumptions from other methods.");

        return sb.ToString();
    }

    /// <summary>
    /// Builds the system prompt for full shot advice, tailored to the brew
    /// method. Espresso (pressure-extracted) needs very different guidance from
    /// immersion (French Press), pour-over, or cold brew. The model should
    /// never apply espresso-only assumptions (1:2 ratio, 25–35s) to a
    /// non-espresso brew.
    /// </summary>
    public static string BuildAdviceSystemPrompt(BrewMethod method)
    {
        var profile = MethodAdvice(method);
        return
            $"You are an expert barista assistant helping improve {profile.Subject} brews.\n" +
            $"Analyse the brew data and provide 1–3 specific parameter adjustments.\n" +
            $"{profile.Targets}\n" +
            $"Adjust using these levers: {profile.Levers}.\n" +
            $"Be practical and specific with amounts (e.g. '0.5g', '2 clicks finer', '15s longer steep', '+1°C').\n" +
            $"Provide brief reasoning in one sentence.\n" +
            $"This is a {method.DisplayName()} brew — do NOT apply assumptions from other brewing methods. Espresso ratios and timings, for example, do not apply to pour over, French press, cold brew, etc.";
    }

    /// <summary>
    /// Brief system prompt for passive insights, tailored to the brew method
    /// so the one-line tip uses the right vocabulary (e.g. "steep longer" for
    /// immersion, "grind finer" for espresso, "slow the pour" for pour over).
    /// </summary>
    public static string BuildPassiveAdviceSystemPrompt(BrewMethod method)
    {
        var profile = MethodAdvice(method);
        return
            $"You are a brief {profile.Subject} advisor. Give ONE short sentence of advice (max 15 words). " +
            $"This is a {method.DisplayName()} brew — use only levers appropriate to that method ({profile.Levers}).";
    }

    /// <summary>
    /// Builds the system prompt for bean-extraction recommendations, tailored
    /// to the brew method the user plans to brew with. Without this, every
    /// recommendation defaults to espresso ratios even for pour over / French
    /// press / cold brew beans.
    /// </summary>
    public static string BuildRecommendationSystemPrompt(BrewMethod method)
    {
        var profile = MethodAdvice(method);
        return
            $"You are an expert barista assistant.\n" +
            $"Recommend {method.DisplayName()} extraction parameters based on bean characteristics.\n" +
            $"{profile.Targets}\n" +
            $"Adjust for roast level: darker roasts generally need coarser grind / shorter contact, lighter roasts the opposite.\n" +
            $"This is a {method.DisplayName()} recipe — do NOT return espresso-style parameters for non-espresso methods.";
    }

    /// <summary>
    /// Per-method advisor profile. Captures the brew subject, the parameter
    /// targets the AI should anchor to, and the levers it is allowed to
    /// suggest changing. Centralised here so every prompt (advice, passive,
    /// recommendation) stays consistent with the brew method.
    /// </summary>
    internal static (string Subject, string Targets, string Levers) MethodAdvice(BrewMethod method) => method switch
    {
        BrewMethod.Espresso => (
            Subject: "espresso",
            Targets: "Consider extraction ratio (target 1:2 to 1:2.5), shot time (25–35s including preinfusion), grind size, dose, and basket fit.",
            Levers: "grind size, dose, yield (output), shot time, preinfusion"),
        BrewMethod.PourOver => (
            Subject: "pour-over coffee",
            Targets: "Consider brew ratio (1:15 to 1:17), total drawdown time (2:30–4:00 for a single cup), bloom (30–45s with ~2x dose water), pour structure, and water temperature (92–96°C).",
            Levers: "grind size, dose, water ratio, bloom time, pour rate / pulse count, water temperature"),
        BrewMethod.V60 => (
            Subject: "V60 pour-over coffee",
            Targets: "Consider brew ratio (1:15 to 1:17), total drawdown 2:30–3:30, bloom (30–45s, ~2x dose), pour pulses, and water temperature (92–96°C).",
            Levers: "grind size, dose, water ratio, bloom time, number/size of pours, water temperature"),
        BrewMethod.Moka => (
            Subject: "Moka pot",
            Targets: "Consider dose-to-water ratio (typically 1:7 to 1:10), heat level (low–medium), and total time on heat (4–6 min). Pull off heat as soon as you hear sputtering to avoid scorching.",
            Levers: "grind size (espresso-fine to medium-fine), dose, water amount, heat level, time on heat"),
        BrewMethod.Drip => (
            Subject: "batch drip coffee",
            Targets: "Consider brew ratio (1:16 to 1:18), total brew time (4–6 min for a typical batch), and even bed wetting.",
            Levers: "grind size (medium), dose, water volume, brew time, water temperature"),
        BrewMethod.Aeropress => (
            Subject: "AeroPress",
            Targets: "Consider brew ratio (1:12 to 1:16 standard, or concentrate then dilute), steep time (1–2 min), press time (~30s), and orientation (inverted vs standard).",
            Levers: "grind size (medium-fine), dose, water ratio, steep time, water temperature, press speed, inverted vs standard"),
        BrewMethod.FrenchPress => (
            Subject: "French press",
            Targets: "Consider brew ratio (1:12 to 1:17), steep time (4–5 min), grind coarseness, and crust handling (break + skim or leave). Decant promptly after plunging.",
            Levers: "grind size (coarse), dose, water ratio, steep time, water temperature, crust handling"),
        BrewMethod.Turkish => (
            Subject: "Turkish (ibrik / cezve)",
            Targets: "Consider 1:10 ratio, very fine (powder) grind, low heat for ~3–4 min, foam-raising technique (heat → lift → reheat). Do not stir after foam forms.",
            Levers: "grind fineness (powder), dose, water amount, heat level, total time, foam technique, sweetness level"),
        BrewMethod.Siphon => (
            Subject: "siphon (vacuum) coffee",
            Targets: "Consider brew ratio (1:14 to 1:17), total brew time 2–3 min after full draw-up, medium grind, and stir technique on draw-up and draw-down.",
            Levers: "grind size (medium), dose, water ratio, draw-up time, brew time, stir pattern, heat level"),
        BrewMethod.Cupping => (
            Subject: "SCA-style cupping",
            Targets: "SCA standard is 8.25g per 150ml water (~1:18), 4-minute steep, then break the crust and skim. Use 93°C water.",
            Levers: "grind size (medium-coarse, consistent), dose-to-water ratio, steep time, crust break timing, water temperature"),
        BrewMethod.ColdBrew => (
            Subject: "cold brew",
            Targets: "Consider concentrate ratio (1:5 to 1:8 for ready-to-drink, or stronger to dilute later), steep time (12–24 h at room or fridge temp), and filtration. Coarse grind only.",
            Levers: "grind size (coarse), dose, water ratio, steep time, steep temperature (room vs fridge), dilution ratio"),
        BrewMethod.ColdDrip => (
            Subject: "cold drip / Kyoto",
            Targets: "Consider brew ratio (1:8 to 1:10), total drip time (4–12 h), drip rate (~1 drop/sec), and bed even-wetting.",
            Levers: "grind size (medium-coarse to coarse), dose, water ratio, drip rate, total drip time"),
        BrewMethod.SteepAndRelease => (
            Subject: "steep-and-release (Clever / Hario Switch)",
            Targets: "Consider brew ratio (1:15 to 1:17), steep time (2–3 min before release), bloom (30s), and water temperature (92–96°C).",
            Levers: "grind size (medium), dose, water ratio, bloom time, steep time, release timing, water temperature"),
        _ => (
            Subject: method.DisplayName(),
            Targets: "Use brewing parameters appropriate for this method (ratio, time, grind, temperature).",
            Levers: "grind size, dose, water ratio, brew time, water temperature")
    };

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
        if (context.CurrentShot.GrindMicrons.HasValue)
            sb.Append($", grind {context.CurrentShot.GrindMicrons}µm");

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

    /// <summary>
    /// Builds a prompt for new bean recommendations (no shot history).
    /// Uses <paramref name="method"/> to scope the recommendation (and the
    /// suggested-range hints in the JSON template) to the brew method the user
    /// plans to use. Defaults to Espresso for backward compatibility.
    /// </summary>
    public static string BuildNewBeanPrompt(BeanRecommendationContextDto context, BrewMethod method = BrewMethod.Espresso)
    {
        var sb = new StringBuilder();
        var profile = method.Profile();
        var grindRange = method.GrindMicronRange();

        sb.AppendLine($"## Brew Method: {method.DisplayName()}");
        sb.AppendLine();

        sb.AppendLine("## Bean Information");
        sb.AppendLine($"- Name: {context.BeanName}");
        if (!string.IsNullOrWhiteSpace(context.Roaster))
            sb.AppendLine($"- Roaster: {context.Roaster}");
        if (!string.IsNullOrWhiteSpace(context.Origin))
            sb.AppendLine($"- Origin: {context.Origin}");
        if (context.DaysFromRoast.HasValue)
            sb.AppendLine($"- Days since roast: {context.DaysFromRoast}");
        if (!string.IsNullOrWhiteSpace(context.Notes))
            sb.AppendLine($"- Flavor notes: {context.Notes}");

        if (context.Equipment != null)
        {
            sb.AppendLine();
            sb.AppendLine("## Equipment");
            if (!string.IsNullOrWhiteSpace(context.Equipment.MachineName))
                sb.AppendLine($"- Machine: {context.Equipment.MachineName}");
            if (!string.IsNullOrWhiteSpace(context.Equipment.GrinderName))
                sb.AppendLine($"- Grinder: {context.Equipment.GrinderName}");
        }

        sb.AppendLine();
        sb.AppendLine($"I have no previous brews with this bean. Based on the bean characteristics above, recommend starting {method.DisplayName()} extraction parameters.");
        sb.AppendLine();
        sb.AppendLine("Respond ONLY with a JSON object in this exact format (no other text):");
        sb.AppendLine("{");
        sb.AppendLine($"  \"dose\": <number in grams, typical {method.DisplayName()} range {profile.DoseMin}–{profile.DoseMax}>,");
        sb.AppendLine($"  \"grind\": \"<grinder setting as string; target micron range for {method.DisplayName()} is {grindRange.Min}–{grindRange.Max}µm>\",");
        sb.AppendLine($"  \"output\": <number in grams, typical {method.DisplayName()} range {profile.OutputMin}–{profile.OutputMax}>,");
        sb.AppendLine($"  \"duration\": <number in seconds, typical {method.DisplayName()} range {profile.TimeMin}–{profile.TimeMax}>");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Builds a prompt for returning bean recommendations (with shot history).
    /// Uses <paramref name="method"/> to scope the recommendation (and the
    /// suggested-range hints in the JSON template) to the brew method the user
    /// plans to use. Defaults to Espresso for backward compatibility.
    /// </summary>
    public static string BuildReturningBeanPrompt(BeanRecommendationContextDto context, BrewMethod method = BrewMethod.Espresso)
    {
        var sb = new StringBuilder();
        var profile = method.Profile();
        var grindRange = method.GrindMicronRange();

        sb.AppendLine($"## Brew Method: {method.DisplayName()}");
        sb.AppendLine();

        sb.AppendLine("## Bean Information");
        sb.AppendLine($"- Name: {context.BeanName}");
        if (!string.IsNullOrWhiteSpace(context.Roaster))
            sb.AppendLine($"- Roaster: {context.Roaster}");
        if (!string.IsNullOrWhiteSpace(context.Origin))
            sb.AppendLine($"- Origin: {context.Origin}");
        if (context.DaysFromRoast.HasValue)
            sb.AppendLine($"- Days since roast: {context.DaysFromRoast}");
        if (!string.IsNullOrWhiteSpace(context.Notes))
            sb.AppendLine($"- Flavor notes: {context.Notes}");

        if (context.Equipment != null)
        {
            sb.AppendLine();
            sb.AppendLine("## Equipment");
            if (!string.IsNullOrWhiteSpace(context.Equipment.MachineName))
                sb.AppendLine($"- Machine: {context.Equipment.MachineName}");
            if (!string.IsNullOrWhiteSpace(context.Equipment.GrinderName))
                sb.AppendLine($"- Grinder: {context.Equipment.GrinderName}");
        }

        if (context.HistoricalShots?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Previous Brews (sorted by rating)");
            foreach (var shot in context.HistoricalShots)
            {
                var details = new List<string>();
                details.Add($"{shot.BrewMethod.DisplayName()}");
                details.Add($"{shot.DoseIn}g in");
                if (shot.ActualOutput.HasValue) details.Add($"{shot.ActualOutput}g out");
                if (shot.ActualTime.HasValue) details.Add($"{shot.ActualTime}s");
                if (shot.GrindMicrons.HasValue) details.Add($"grind {shot.GrindMicrons}µm");
                if (shot.Rating.HasValue) details.Add($"rated {shot.Rating}/4");
                sb.AppendLine($"- {string.Join(", ", details)}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Based on my brew history with this bean, recommend optimal {method.DisplayName()} extraction parameters. Prefer history entries that match this brew method; treat other-method entries as weak context only.");
        sb.AppendLine();
        sb.AppendLine("Respond ONLY with a JSON object in this exact format (no other text):");
        sb.AppendLine("{");
        sb.AppendLine($"  \"dose\": <number in grams, typical {method.DisplayName()} range {profile.DoseMin}–{profile.DoseMax}>,");
        sb.AppendLine($"  \"grind\": \"<grinder setting as string; target micron range for {method.DisplayName()} is {grindRange.Min}–{grindRange.Max}µm>\",");
        sb.AppendLine($"  \"output\": <number in grams, typical {method.DisplayName()} range {profile.OutputMin}–{profile.OutputMax}>,");
        sb.AppendLine($"  \"duration\": <number in seconds, typical {method.DisplayName()} range {profile.TimeMin}–{profile.TimeMax}>");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
