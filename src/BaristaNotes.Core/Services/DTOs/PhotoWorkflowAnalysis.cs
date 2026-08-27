namespace BaristaNotes.Core.Services.DTOs;

public enum PhotoWorkflowIntent
{
    Unknown,
    Coffee,
    Profile,
    Room
}

public sealed class PhotoWorkflowAnalysis
{
    public bool Success { get; init; }
    public PhotoWorkflowIntent Intent { get; init; }
    public bool IsObvious { get; init; }
    public string? Rationale { get; init; }
    public BeanLabelExtraction? CoffeeDetails { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RawResponse { get; init; }

    public static PhotoWorkflowAnalysis Error(string message, string? rawResponse = null) => new()
    {
        Intent = PhotoWorkflowIntent.Unknown,
        ErrorMessage = message,
        RawResponse = rawResponse
    };
}
