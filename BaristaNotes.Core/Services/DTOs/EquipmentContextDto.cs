namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Equipment information for AI context.
/// </summary>
public record EquipmentContextDto
{
    /// <summary>
    /// Espresso machine name.
    /// </summary>
    public string? MachineName { get; init; }

    /// <summary>
    /// Grinder name.
    /// </summary>
    public string? GrinderName { get; init; }
}
