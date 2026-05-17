namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Result of extracting coffee-bag-label fields from a photo via the vision model.
/// </summary>
public class BeanLabelExtraction
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Name { get; set; }
    public string? Roaster { get; set; }
    public string? Origin { get; set; }
    public DateTime? RoastDate { get; set; }

    /// <summary>Raw model response text, kept for debugging.</summary>
    public string? RawResponse { get; set; }
}
