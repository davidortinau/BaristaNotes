namespace BaristaNotes.Models;

public class FeedbackMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public FeedbackType Type { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? RecoveryAction { get; init; }
    public int DurationMs { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTime.UtcNow;
    public bool IsVisible { get; set; } = true;
}
