namespace BaristaNotes.Core.Services.DTOs;

public record ShotRecordDto
{
    public int Id { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    
    public BeanDto? Bean { get; init; }
    public EquipmentDto? Machine { get; init; }
    public EquipmentDto? Grinder { get; init; }
    public List<EquipmentDto> Accessories { get; init; } = new();
    public UserProfileDto? MadeBy { get; init; }
    public UserProfileDto? MadeFor { get; init; }
    
    public decimal DoseIn { get; init; }
    public string GrindSetting { get; init; } = string.Empty;
    public decimal ExpectedTime { get; init; }
    public decimal ExpectedOutput { get; init; }
    public string DrinkType { get; init; } = string.Empty;
    
    public decimal? ActualTime { get; init; }
    public decimal? ActualOutput { get; init; }
    public decimal? PreinfusionTime { get; init; }
    
    public int? Rating { get; init; }
}

public record CreateShotDto
{
    public DateTimeOffset? Timestamp { get; init; }
    
    public int? BeanId { get; init; }
    public int? MachineId { get; init; }
    public int? GrinderId { get; init; }
    public List<int> AccessoryIds { get; init; } = new();
    public int? MadeById { get; init; }
    public int? MadeForId { get; init; }
    
    public decimal DoseIn { get; init; }
    public string GrindSetting { get; init; } = string.Empty;
    public decimal ExpectedTime { get; init; }
    public decimal ExpectedOutput { get; init; }
    public string DrinkType { get; init; } = string.Empty;
    
    public decimal? ActualTime { get; init; }
    public decimal? ActualOutput { get; init; }
    public decimal? PreinfusionTime { get; init; }
    
    public int? Rating { get; init; }
}

/// <summary>
/// DTO for updating editable fields of a shot record.
/// Includes result fields (actual time/output, rating) and correctable setup fields (bean).
/// </summary>
public record UpdateShotDto
{
    /// <summary>
    /// Bean used for the shot.
    /// Optional - null means no change to existing value.
    /// Allows correcting bean selection mistakes.
    /// </summary>
    public int? BeanId { get; init; }
    
    /// <summary>
    /// User who pulled the shot (barista).
    /// Optional - null means no change to existing value.
    /// </summary>
    public int? MadeById { get; init; }
    
    /// <summary>
    /// User the shot was made for (customer).
    /// Optional - null means no change to existing value.
    /// </summary>
    public int? MadeForId { get; init; }
    
    /// <summary>
    /// Actual shot extraction time in seconds.
    /// Optional - null means no change to existing value.
    /// If provided, must be greater than 0 and less than 999.
    /// </summary>
    public decimal? ActualTime { get; init; }
    
    /// <summary>
    /// Actual output weight in grams.
    /// Optional - null means no change to existing value.
    /// If provided, must be greater than 0 and less than 200.
    /// </summary>
    public decimal? ActualOutput { get; init; }
    
    /// <summary>
    /// Preinfusion time in seconds.
    /// Optional - null means no change to existing value.
    /// If provided, must be between 0 and 60.
    /// </summary>
    public decimal? PreinfusionTime { get; init; }
    
    /// <summary>
    /// Taste rating on 1-5 scale (1=dislike, 5=excellent).
    /// Optional - null means no change to existing value.
    /// If provided, must be between 1 and 5 inclusive.
    /// </summary>
    public int? Rating { get; init; }
    
    /// <summary>
    /// Type of drink made (e.g., "Espresso", "Latte", "Americano").
    /// Required - cannot be null or empty.
    /// </summary>
    public string DrinkType { get; init; } = string.Empty;
}

public record EquipmentDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Models.Enums.EquipmentType Type { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public record CreateEquipmentDto
{
    public string Name { get; init; } = string.Empty;
    public Models.Enums.EquipmentType Type { get; init; }
    public string? Notes { get; init; }
}

public record UpdateEquipmentDto
{
    public string? Name { get; init; }
    public Models.Enums.EquipmentType? Type { get; init; }
    public string? Notes { get; init; }
    public bool? IsActive { get; init; }
}

public record BeanDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Roaster { get; init; }
    public DateTimeOffset? RoastDate { get; init; }
    public string? Origin { get; init; }
    public string? Notes { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public record CreateBeanDto
{
    public string Name { get; init; } = string.Empty;
    public string? Roaster { get; init; }
    public DateTimeOffset? RoastDate { get; init; }
    public string? Origin { get; init; }
    public string? Notes { get; init; }
}

public record UpdateBeanDto
{
    public string? Name { get; init; }
    public string? Roaster { get; init; }
    public DateTimeOffset? RoastDate { get; init; }
    public string? Origin { get; init; }
    public string? Notes { get; init; }
    public bool? IsActive { get; init; }
}

public record UserProfileDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? AvatarPath { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public record CreateUserProfileDto
{
    public string Name { get; init; } = string.Empty;
    public string? AvatarPath { get; init; }
}

public record UpdateUserProfileDto
{
    public string? Name { get; init; }
    public string? AvatarPath { get; init; }
}

public record PagedResult<T>
{
    public List<T> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int PageIndex { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 0;
    public bool HasNextPage => PageIndex < TotalPages - 1;
}
