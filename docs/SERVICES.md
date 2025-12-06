# Service Architecture

This document describes the service layer in BaristaNotes, including dependency injection configuration, service interfaces, and implementation patterns.

## Table of Contents

- [Overview](#overview)
- [Dependency Injection](#dependency-injection)
- [Core Services](#core-services)
- [Platform Services](#platform-services)
- [Service Patterns](#service-patterns)

## Overview

The service layer encapsulates business logic and coordinates between the UI and data layers. Services are registered in the dependency injection container and injected into components as needed.

### Architecture Layers

```
┌──────────────────────────────┐
│        UI Layer              │
│  (MauiReactor Components)    │
└──────────┬───────────────────┘
           │ Inject Services
           ▼
┌──────────────────────────────┐
│      Service Layer           │
│  (Business Logic)            │
└──────────┬───────────────────┘
           │ Use Repositories
           ▼
┌──────────────────────────────┐
│       Data Layer             │
│  (Entity Framework Core)     │
└──────────────────────────────┘
```

## Dependency Injection

Services are registered in `MauiProgram.cs` during app startup.

### Service Registration

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        // Core Services (Singleton - app lifetime)
        builder.Services.AddSingleton<BaristasDbContext>();
        builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
        
        // Business Services (Transient - per use)
        builder.Services.AddTransient<IShotService, ShotService>();
        builder.Services.AddTransient<IBeanService, BeanService>();
        builder.Services.AddTransient<IEquipmentService, EquipmentService>();
        builder.Services.AddTransient<IUserProfileService, UserProfileService>();
        
        // Platform Services (Transient)
        builder.Services.AddTransient<IImagePickerService, ImagePickerService>();
        builder.Services.AddTransient<IImageProcessingService, ImageProcessingService>();
        builder.Services.AddSingleton<IFeedbackService, FeedbackService>();
        
        // MAUI Platform Services
        builder.Services.AddSingleton(MediaPicker.Default);
        
        return builder.Build();
    }
}
```

### Service Lifetimes

| Lifetime | Description | Use Cases |
|----------|-------------|-----------|
| **Singleton** | One instance for app lifetime | DbContext, preferences, app state |
| **Transient** | New instance each time | Stateless services, data operations |
| **Scoped** | One instance per scope | Not typically used in MAUI apps |

## Core Services

Core services handle business logic and data operations.

### IShotService

Manages espresso shot records.

```csharp
public interface IShotService
{
    // Query
    Task<ShotDto?> GetShotByIdAsync(int id);
    Task<List<ShotDto>> GetAllShotsAsync();
    Task<List<ShotDto>> GetShotsByBeanIdAsync(int beanId);
    Task<List<ShotDto>> GetShotsByDateRangeAsync(DateTime start, DateTime end);
    Task<List<ShotDto>> GetRecentShotsAsync(int count = 10);
    
    // Command
    Task<ShotDto> CreateShotAsync(CreateShotRequest request);
    Task<ShotDto> UpdateShotAsync(int id, UpdateShotRequest request);
    Task DeleteShotAsync(int id);
    
    // Statistics
    Task<ShotStatistics> GetStatisticsAsync(int? beanId = null);
}
```

**Implementation Example**:

```csharp
public class ShotService : IShotService
{
    private readonly BaristasDbContext _context;
    
    public ShotService(BaristasDbContext context)
    {
        _context = context;
    }
    
    public async Task<ShotDto?> GetShotByIdAsync(int id)
    {
        var shot = await _context.ShotRecords
            .Include(s => s.Bean)
            .Include(s => s.MadeBy)
            .Include(s => s.MadeFor)
            .Include(s => s.Machine)
            .Include(s => s.Grinder)
            .FirstOrDefaultAsync(s => s.Id == id);
            
        return shot == null ? null : ShotMapper.ToDto(shot);
    }
    
    public async Task<ShotDto> CreateShotAsync(CreateShotRequest request)
    {
        var shot = new ShotRecord
        {
            BeanId = request.BeanId,
            MadeById = request.MadeById,
            MadeForId = request.MadeForId,
            MachineId = request.MachineId,
            GrinderId = request.GrinderId,
            Dose = request.Dose,
            GrindSetting = request.GrindSetting,
            OutputWeight = request.OutputWeight,
            ExtractionTime = request.ExtractionTime,
            WaterTemperature = request.WaterTemperature,
            Rating = request.Rating,
            Notes = request.Notes,
            LoggedAt = DateTime.UtcNow
        };
        
        _context.ShotRecords.Add(shot);
        await _context.SaveChangesAsync();
        
        // Reload with navigation properties
        return (await GetShotByIdAsync(shot.Id))!;
    }
    
    public async Task<ShotStatistics> GetStatisticsAsync(int? beanId = null)
    {
        var query = _context.ShotRecords.AsQueryable();
        
        if (beanId.HasValue)
            query = query.Where(s => s.BeanId == beanId.Value);
        
        var shots = await query.ToListAsync();
        
        return new ShotStatistics
        {
            TotalShots = shots.Count,
            AverageDose = shots.Average(s => s.Dose),
            AverageOutputWeight = shots.Average(s => s.OutputWeight),
            AverageExtractionTime = shots.Average(s => s.ExtractionTime),
            AverageRating = shots.Average(s => s.Rating)
        };
    }
}
```

### IBeanService

Manages coffee bean inventory.

```csharp
public interface IBeanService
{
    Task<BeanDto?> GetBeanByIdAsync(int id);
    Task<List<BeanDto>> GetAllBeansAsync();
    Task<List<BeanDto>> GetActiveBeansAsync();
    Task<BeanDto> CreateBeanAsync(CreateBeanRequest request);
    Task<BeanDto> UpdateBeanAsync(int id, UpdateBeanRequest request);
    Task DeleteBeanAsync(int id);
    Task UpdateStockAsync(int id, double amount);
}
```

### IEquipmentService

Manages espresso equipment.

```csharp
public interface IEquipmentService
{
    Task<EquipmentDto?> GetEquipmentByIdAsync(int id);
    Task<List<EquipmentDto>> GetAllEquipmentAsync();
    Task<List<EquipmentDto>> GetEquipmentByTypeAsync(EquipmentType type);
    Task<EquipmentDto> CreateEquipmentAsync(CreateEquipmentRequest request);
    Task<EquipmentDto> UpdateEquipmentAsync(int id, UpdateEquipmentRequest request);
    Task DeleteEquipmentAsync(int id);
}
```

### IUserProfileService

Manages user profiles.

```csharp
public interface IUserProfileService
{
    Task<UserProfileDto?> GetProfileByIdAsync(int id);
    Task<List<UserProfileDto>> GetAllProfilesAsync();
    Task<List<UserProfileDto>> GetActiveProfilesAsync();
    Task<UserProfileDto> CreateProfileAsync(CreateUserProfileRequest request);
    Task<UserProfileDto> UpdateProfileAsync(int id, UpdateUserProfileRequest request);
    Task DeleteProfileAsync(int id);
    Task<string?> GetProfileAvatarPathAsync(int id);
}
```

### IPreferencesService

Manages app preferences and settings.

```csharp
public interface IPreferencesService
{
    // Theme
    AppTheme Theme { get; set; }
    
    // Default Values
    int? DefaultMachineId { get; set; }
    int? DefaultGrinderId { get; set; }
    int? DefaultMakerId { get; set; }
    
    // Units
    TemperatureUnit TemperatureUnit { get; set; }
    
    // App State
    bool IsFirstLaunch { get; set; }
    DateTime? LastSyncTime { get; set; }
}
```

**Implementation Example**:

```csharp
public class PreferencesService : IPreferencesService
{
    public AppTheme Theme
    {
        get => Enum.Parse<AppTheme>(
            Preferences.Get(nameof(Theme), AppTheme.System.ToString())
        );
        set => Preferences.Set(nameof(Theme), value.ToString());
    }
    
    public int? DefaultMachineId
    {
        get
        {
            var value = Preferences.Get(nameof(DefaultMachineId), -1);
            return value == -1 ? null : value;
        }
        set => Preferences.Set(nameof(DefaultMachineId), value ?? -1);
    }
    
    // ... other properties
}
```

## Platform Services

Platform services abstract platform-specific functionality.

### IImagePickerService

Wraps MAUI's `IMediaPicker` for photo selection.

```csharp
public interface IImagePickerService
{
    Task<Stream?> PickImageAsync();
    Task<bool> HasPermissionAsync();
}
```

**Implementation**:

```csharp
public class ImagePickerService : IImagePickerService
{
    private readonly IMediaPicker _mediaPicker;
    
    public ImagePickerService(IMediaPicker mediaPicker)
    {
        _mediaPicker = mediaPicker;
    }
    
    public async Task<Stream?> PickImageAsync()
    {
        try
        {
            var result = await _mediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Select a photo"
            });
            
            if (result == null)
                return null;
                
            return await result.OpenReadAsync();
        }
        catch (PermissionException)
        {
            return null;
        }
    }
    
    public async Task<bool> HasPermissionAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Photos>();
        
        if (status == PermissionStatus.Granted)
            return true;
            
        status = await Permissions.RequestAsync<Permissions.Photos>();
        return status == PermissionStatus.Granted;
    }
}
```

### IImageProcessingService

Handles image resizing and optimization.

```csharp
public interface IImageProcessingService
{
    Task<ImageValidationResult> ValidateImageAsync(Stream imageStream);
    Task<Stream> ProcessProfileImageAsync(Stream imageStream);
    Task<string> SaveProfileImageAsync(Stream imageStream, int profileId);
}
```

### IFeedbackService

Provides user feedback (toasts, alerts, etc.).

```csharp
public interface IFeedbackService
{
    Task ShowToastAsync(string message);
    Task ShowAlertAsync(string title, string message);
    Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No");
}
```

**Implementation** (using Community Toolkit):

```csharp
public class FeedbackService : IFeedbackService
{
    public async Task ShowToastAsync(string message)
    {
        var toast = Toast.Make(message, ToastDuration.Short);
        await toast.Show();
    }
    
    public async Task ShowAlertAsync(string title, string message)
    {
        await Shell.Current.DisplayAlert(title, message, "OK");
    }
    
    public async Task<bool> ShowConfirmAsync(string title, string message, string accept, string cancel)
    {
        return await Shell.Current.DisplayAlert(title, message, accept, cancel);
    }
}
```

## Service Patterns

### 1. Request/Response Pattern

Use dedicated request and response objects for service methods:

```csharp
// Request
public record CreateShotRequest
{
    public int? BeanId { get; init; }
    public int? MadeById { get; init; }
    public double Dose { get; init; }
    public double GrindSetting { get; init; }
    public double OutputWeight { get; init; }
    public int ExtractionTime { get; init; }
    public int Rating { get; init; }
    public string? Notes { get; init; }
}

// Response (DTO)
public record ShotDto
{
    public int Id { get; init; }
    public string? BeanName { get; init; }
    public string? MadeByName { get; init; }
    public double Dose { get; init; }
    // ...
}

// Usage
var request = new CreateShotRequest 
{ 
    Dose = 18.0, 
    Rating = 4 
};
var result = await _shotService.CreateShotAsync(request);
```

### 2. Validation in Services

Validate business rules in the service layer:

```csharp
public async Task<BeanDto> CreateBeanAsync(CreateBeanRequest request)
{
    // Validation
    if (string.IsNullOrWhiteSpace(request.Name))
        throw new ValidationException("Bean name is required");
        
    if (request.CurrentStock < 0)
        throw new ValidationException("Stock cannot be negative");
        
    // Business logic
    var bean = new Bean
    {
        Name = request.Name,
        CurrentStock = request.CurrentStock,
        // ...
    };
    
    _context.Beans.Add(bean);
    await _context.SaveChangesAsync();
    
    return BeanMapper.ToDto(bean);
}
```

### 3. Error Handling

Use custom exceptions for different error scenarios:

```csharp
// Custom exceptions
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}

// Service usage
public async Task<ShotDto> UpdateShotAsync(int id, UpdateShotRequest request)
{
    var shot = await _context.ShotRecords.FindAsync(id);
    if (shot == null)
        throw new NotFoundException($"Shot {id} not found");
        
    // Update logic
    shot.Rating = request.Rating ?? shot.Rating;
    shot.Notes = request.Notes ?? shot.Notes;
    
    await _context.SaveChangesAsync();
    return ShotMapper.ToDto(shot);
}

// UI error handling
try
{
    await _shotService.UpdateShotAsync(id, request);
    await _feedbackService.ShowToastAsync("Shot updated successfully");
}
catch (NotFoundException ex)
{
    await _feedbackService.ShowAlertAsync("Error", ex.Message);
}
catch (Exception ex)
{
    await _feedbackService.ShowAlertAsync("Error", "An unexpected error occurred");
}
```

### 4. Async/Await Best Practices

Always use async/await for I/O operations:

```csharp
// Good
public async Task<List<ShotDto>> GetAllShotsAsync()
{
    var shots = await _context.ShotRecords
        .Include(s => s.Bean)
        .ToListAsync();
        
    return shots.Select(ShotMapper.ToDto).ToList();
}

// Avoid - blocks thread
public List<ShotDto> GetAllShots()
{
    var shots = _context.ShotRecords
        .Include(s => s.Bean)
        .ToList();
        
    return shots.Select(ShotMapper.ToDto).ToList();
}
```

### 5. Mapper Pattern

Use static mapper classes to convert between entities and DTOs:

```csharp
public static class ShotMapper
{
    public static ShotDto ToDto(ShotRecord shot)
    {
        return new ShotDto
        {
            Id = shot.Id,
            BeanId = shot.BeanId,
            BeanName = shot.Bean?.Name,
            MadeById = shot.MadeById,
            MadeByName = shot.MadeBy?.Name,
            MadeForId = shot.MadeForId,
            MadeForName = shot.MadeFor?.Name,
            Dose = shot.Dose,
            GrindSetting = shot.GrindSetting,
            OutputWeight = shot.OutputWeight,
            ExtractionTime = shot.ExtractionTime,
            Rating = shot.Rating,
            Notes = shot.Notes,
            LoggedAt = shot.LoggedAt,
            Ratio = shot.Dose > 0 ? shot.OutputWeight / shot.Dose : 0
        };
    }
}
```

## Testing Services

Services should be unit tested with mock dependencies:

```csharp
public class ShotServiceTests
{
    private readonly DbContextOptions<BaristasDbContext> _options;
    
    public ShotServiceTests()
    {
        _options = new DbContextOptionsBuilder<BaristasDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb")
            .Options;
    }
    
    [Fact]
    public async Task CreateShotAsync_ValidData_CreatesShot()
    {
        // Arrange
        using var context = new BaristasDbContext(_options);
        var service = new ShotService(context);
        
        var request = new CreateShotRequest
        {
            Dose = 18.0,
            GrindSetting = 3.5,
            OutputWeight = 36.0,
            ExtractionTime = 28,
            Rating = 4
        };
        
        // Act
        var result = await service.CreateShotAsync(request);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(18.0, result.Dose);
        Assert.Equal(4, result.Rating);
    }
}
```

## Additional Resources

- [Dependency Injection in .NET](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection)
- [Service Lifetimes](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection#service-lifetimes)
- [Testing with EF Core](https://learn.microsoft.com/ef/core/testing/)
