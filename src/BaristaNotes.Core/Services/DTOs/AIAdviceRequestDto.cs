namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Used to gather context for AI advice request.
/// </summary>
public record AIAdviceRequestDto
{
    /// <summary>
    /// The shot ID to get advice for.
    /// </summary>
    public int ShotId { get; init; }

    /// <summary>
    /// Current shot details.
    /// </summary>
    public required ShotContextDto CurrentShot { get; init; }

    /// <summary>
    /// Previous shots for same bag (up to 10, sorted by rating desc).
    /// </summary>
    public List<ShotContextDto> HistoricalShots { get; init; } = new();

    /// <summary>
    /// Bean and roast information.
    /// </summary>
    public required BeanContextDto BeanInfo { get; init; }

    /// <summary>
    /// Machine and grinder if logged.
    /// </summary>
    public EquipmentContextDto? Equipment { get; init; }

    /// <summary>
    /// Persona the shot was made for, with their learned Context.
    /// Lets the model tailor advice to a person's stated preferences.
    /// </summary>
    public UserProfileDto? MadeFor { get; init; }
}
