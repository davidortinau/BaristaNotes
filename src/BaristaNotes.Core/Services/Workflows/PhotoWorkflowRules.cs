using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services.Workflows;

public static class PhotoWorkflowRules
{
    public static PhotoIntentChoice? AutomaticChoice(PhotoWorkflowAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (!analysis.Success || !analysis.IsObvious || analysis.Intent == PhotoWorkflowIntent.Unknown)
            return null;

        return analysis.Intent switch
        {
            PhotoWorkflowIntent.Coffee => PhotoIntentChoice.Coffee,
            PhotoWorkflowIntent.Profile => PhotoIntentChoice.Profile,
            PhotoWorkflowIntent.Room => PhotoIntentChoice.Room,
            _ => PhotoIntentChoice.Cancel
        };
    }

    public static bool NeedsCoffeeExtraction(BeanLabelExtraction? details) =>
        details is null || (string.IsNullOrWhiteSpace(details.Name)
            && string.IsNullOrWhiteSpace(details.Roaster)
            && string.IsNullOrWhiteSpace(details.Origin)
            && !details.RoastDate.HasValue
            && string.IsNullOrWhiteSpace(details.Notes));

    public static BeanLabelExtraction CoffeePrefill(BeanLabelExtraction? details) =>
        details?.Success == true ? details : new BeanLabelExtraction { Success = true };
}
