using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services.Workflows;

public static class AdvicePresentation
{
    public const string Title = "AI Suggestions";
    public const string EmptyAdjustments = "No specific adjustments suggested.";
    public const string Unavailable = "AI advice is not available";
    public const string UnavailableDetail = "Please update the app or contact support.";
    public const string Unsuccessful = "Could not get advice";
    public const string UnsuccessfulDetail = "The AI did not return a usable response.";
    public const string TimedOut = "Request timed out";
    public const string TimedOutDetail = "Please try again.";
    public const string Failed = "Failed to get advice";

    public static string Adjustment(ShotAdjustment adjustment) =>
        $"{Capitalize(adjustment.Direction)} {adjustment.Parameter} by {adjustment.Amount}";

    public static string Capitalize(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : char.ToUpper(value[0]) + value[1..].ToLowerInvariant();

    public static string Prompt(string prompt, int historyCount) => historyCount <= 3
        ? prompt
        : prompt + Environment.NewLine + Environment.NewLine
            + $"(prompt includes {historyCount} historical shots, abbreviated)";

    public static double MaximumBodyHeight(double logicalScreenHeight) =>
        Math.Max(240, logicalScreenHeight * .6);

    public static string Error(string title, string detail) => title;
}
