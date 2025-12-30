namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Lightweight DTO for bean filter option display.
/// </summary>
public record BeanFilterOptionDto
{
    /// <summary>
    /// Bean unique identifier.
    /// </summary>
    public int Id { get; init; }
    
    /// <summary>
    /// Bean name for display.
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
