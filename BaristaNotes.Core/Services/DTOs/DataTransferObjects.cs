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
    
    public int? Rating { get; init; }
}

public record UpdateShotDto
{
    public decimal? ActualTime { get; init; }
    public decimal? ActualOutput { get; init; }
    public int? Rating { get; init; }
    
    public decimal? DoseIn { get; init; }
    public string? GrindSetting { get; init; }
    public decimal? ExpectedTime { get; init; }
    public decimal? ExpectedOutput { get; init; }
    public string? DrinkType { get; init; }
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
