# Quickstart Guide: Espresso Shot Tracking & Management

**Feature**: 001-espresso-tracking  
**Phase**: 1 - Design & Contracts  
**Date**: 2025-12-02  
**Audience**: Developers implementing this feature

## Purpose

This guide provides step-by-step instructions for implementing the BaristaNotes espresso tracking application from data layer through UI, following the research and design specifications.

---

## Prerequisites

- .NET 8 SDK installed
- Visual Studio 2022 or VS Code with C# extensions
- iOS/Android device or emulator for testing
- Git for version control

---

## Step 1: Project Setup

### 1.1 Create .NET MAUI Solution

```bash
cd /Users/davidortinau/work/BaristaNotes/BaristaNotes
dotnet new maui -n BaristaNotes -o BaristaNotes
dotnet new sln -n BaristaNotes
dotnet sln add BaristaNotes/BaristaNotes.csproj
```

### 1.2 Create Test Project

```bash
dotnet new xunit -n BaristaNotes.Tests -o BaristaNotes.Tests
dotnet sln add BaristaNotes.Tests/BaristaNotes.Tests.csproj
dotnet add BaristaNotes.Tests reference BaristaNotes/BaristaNotes.csproj
```

### 1.3 Add NuGet Packages

Edit `BaristaNotes/BaristaNotes.csproj` and add:

```xml
<ItemGroup>
  <!-- Maui Reactor -->
  <PackageReference Include="Reactor.Maui" Version="*-*" />
  
  <!-- Community Toolkit -->
  <PackageReference Include="CommunityToolkit.Maui" Version="7.0.0" />
  
  <!-- Entity Framework Core + SQLite -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="8.0.0" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="8.0.0">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
  
  <!-- CoreSync (for future sync capability) -->
  <PackageReference Include="CoreSync.Sqlite" Version="2.3.0" />
  <PackageReference Include="CoreSync.Http.Client" Version="2.3.0" />
</ItemGroup>
```

Then restore packages:

```bash
dotnet restore
```

---

## Step 2: Data Layer Implementation

### 2.1 Create Models (Domain Entities)

Reference: `specs/001-espresso-tracking/data-model.md`

Create files in `BaristaNotes/Models/`:

**Models/Enums/EquipmentType.cs**:
```csharp
namespace BaristaNotes.Models.Enums;

public enum EquipmentType
{
    Machine = 1,
    Grinder = 2,
    Tamper = 3,
    PuckScreen = 4,
    Other = 99
}
```

**Models/Equipment.cs**, **Models/Bean.cs**, **Models/UserProfile.cs**, **Models/ShotRecord.cs**, **Models/ShotEquipment.cs**:
- Copy entity definitions from data-model.md
- Include all properties, navigation properties, and CoreSync metadata

### 2.2 Create DbContext

**Data/BaristaNotesContext.cs**:
```csharp
using Microsoft.EntityFrameworkCore;
using BaristaNotes.Models;
using BaristaNotes.Models.Enums;

namespace BaristaNotes.Data;

public class BaristaNotesContext : DbContext
{
    public DbSet<Equipment> Equipment { get; set; } = null!;
    public DbSet<Bean> Beans { get; set; } = null!;
    public DbSet<UserProfile> UserProfiles { get; set; } = null!;
    public DbSet<ShotRecord> ShotRecords { get; set; } = null!;
    public DbSet<ShotEquipment> ShotEquipments { get; set; } = null!;
    
    public BaristaNotesContext(DbContextOptions<BaristaNotesContext> options) 
        : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Copy configuration from data-model.md
    }
}
```

### 2.3 Create Initial Migration

```bash
cd BaristaNotes
dotnet ef migrations add InitialCreate
```

Verify generated migration in `Data/Migrations/` looks correct.

### 2.4 Create Repositories

Reference: `specs/001-espresso-tracking/contracts/service-interfaces.md`

Create files in `BaristaNotes/Data/Repositories/`:

**Data/Repositories/IRepository.cs**:
```csharp
namespace BaristaNotes.Data.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task<T> AddAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
```

**Data/Repositories/Repository.cs** (base implementation):
```csharp
using Microsoft.EntityFrameworkCore;

namespace BaristaNotes.Data.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly BaristaNotesContext _context;
    protected readonly DbSet<T> _dbSet;
    
    public Repository(BaristaNotesContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }
    
    public virtual async Task<T?> GetByIdAsync(int id)
        => await _dbSet.FindAsync(id);
    
    public virtual async Task<List<T>> GetAllAsync()
        => await _dbSet.ToListAsync();
    
    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }
    
    public virtual async Task<T> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }
    
    public virtual async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }
}
```

Create specific repositories: **EquipmentRepository.cs**, **BeanRepository.cs**, **UserProfileRepository.cs**, **ShotRecordRepository.cs** that extend `Repository<T>` and add specialized queries (e.g., `GetMostRecentShotAsync` in ShotRecordRepository).

---

## Step 3: Services Layer Implementation

Reference: `specs/001-espresso-tracking/contracts/service-interfaces.md`

### 3.1 Create DTOs

Create files in `BaristaNotes/Services/DTOs/`:
- Copy all DTO definitions from service-interfaces.md
- Use `record` types for immutability

### 3.2 Create Service Interfaces

Create files in `BaristaNotes/Services/`:
- Copy all service interfaces from service-interfaces.md

### 3.3 Implement Services

**Services/ShotService.cs**:
```csharp
using BaristaNotes.Data.Repositories;
using BaristaNotes.Models;
using BaristaNotes.Services.DTOs;

namespace BaristaNotes.Services;

public class ShotService : IShotService
{
    private readonly IShotRecordRepository _shotRepository;
    private readonly IPreferencesService _preferences;
    
    public ShotService(
        IShotRecordRepository shotRepository,
        IPreferencesService preferences)
    {
        _shotRepository = shotRepository;
        _preferences = preferences;
    }
    
    public async Task<ShotRecordDto?> GetMostRecentShotAsync()
    {
        var shot = await _shotRepository.GetMostRecentAsync();
        return shot == null ? null : MapToDto(shot);
    }
    
    public async Task<ShotRecordDto> CreateShotAsync(CreateShotDto dto)
    {
        // Validation
        ValidateCreateShot(dto);
        
        // Map to entity
        var shot = new ShotRecord
        {
            Timestamp = dto.Timestamp ?? DateTimeOffset.Now,
            BeanId = dto.BeanId,
            MachineId = dto.MachineId,
            GrinderId = dto.GrinderId,
            MadeById = dto.MadeById,
            MadeForId = dto.MadeForId,
            DoseIn = dto.DoseIn,
            GrindSetting = dto.GrindSetting,
            ExpectedTime = dto.ExpectedTime,
            ExpectedOutput = dto.ExpectedOutput,
            DrinkType = dto.DrinkType,
            ActualTime = dto.ActualTime,
            ActualOutput = dto.ActualOutput,
            Rating = dto.Rating,
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        // Save
        var created = await _shotRepository.AddAsync(shot);
        
        // Remember selections
        _preferences.SetLastDrinkType(dto.DrinkType);
        _preferences.SetLastBeanId(dto.BeanId);
        _preferences.SetLastMachineId(dto.MachineId);
        _preferences.SetLastGrinderId(dto.GrinderId);
        _preferences.SetLastMadeById(dto.MadeById);
        _preferences.SetLastMadeForId(dto.MadeForId);
        
        return MapToDto(created);
    }
    
    // Implement other methods...
    
    private ShotRecordDto MapToDto(ShotRecord shot) => new()
    {
        Id = shot.Id,
        Timestamp = shot.Timestamp,
        Bean = shot.Bean == null ? null : new BeanDto { /* map */ },
        // ... map all properties
    };
    
    private void ValidateCreateShot(CreateShotDto dto)
    {
        var errors = new Dictionary<string, List<string>>();
        
        if (dto.DoseIn < 5 || dto.DoseIn > 30)
            errors.Add(nameof(dto.DoseIn), new List<string> { "Dose must be between 5 and 30 grams" });
        
        // ... other validations
        
        if (errors.Any())
            throw new ValidationException(errors);
    }
}
```

Implement: **EquipmentService.cs**, **BeanService.cs**, **UserProfileService.cs**, **PreferencesService.cs** similarly.

---

## Step 4: ViewModels Implementation

### 4.1 Create Base ViewModel

**ViewModels/BaseViewModel.cs**:
```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BaristaNotes.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
            return false;
        
        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

### 4.2 Create ViewModels

**ViewModels/ShotLoggingViewModel.cs**:
```csharp
using System.Collections.ObjectModel;
using BaristaNotes.Services;
using BaristaNotes.Services.DTOs;

namespace BaristaNotes.ViewModels;

public class ShotLoggingViewModel : BaseViewModel
{
    private readonly IShotService _shotService;
    private readonly IEquipmentService _equipmentService;
    private readonly IBeanService _beanService;
    private readonly IUserProfileService _profileService;
    
    public ShotLoggingViewModel(
        IShotService shotService,
        IEquipmentService equipmentService,
        IBeanService beanService,
        IUserProfileService profileService)
    {
        _shotService = shotService;
        _equipmentService = equipmentService;
        _beanService = beanService;
        _profileService = profileService;
    }
    
    // Properties
    private decimal _doseIn;
    public decimal DoseIn
    {
        get => _doseIn;
        set => SetProperty(ref _doseIn, value);
    }
    
    // ... other properties
    
    public ObservableCollection<BeanDto> Beans { get; } = new();
    public ObservableCollection<EquipmentDto> Machines { get; } = new();
    public ObservableCollection<EquipmentDto> Grinders { get; } = new();
    
    // Commands
    public async Task LoadDataAsync()
    {
        var beans = await _beanService.GetAllActiveBeansAsync();
        Beans.Clear();
        foreach (var bean in beans) Beans.Add(bean);
        
        // Load equipment...
        
        // Pre-populate from last shot
        var lastShot = await _shotService.GetMostRecentShotAsync();
        if (lastShot != null)
        {
            DoseIn = lastShot.DoseIn;
            // ... copy other fields
        }
    }
    
    public async Task SaveShotAsync()
    {
        var dto = new CreateShotDto
        {
            DoseIn = DoseIn,
            // ... map all properties
        };
        
        await _shotService.CreateShotAsync(dto);
    }
}
```

Create: **ActivityFeedViewModel.cs**, **EquipmentManagementViewModel.cs**, **BeanManagementViewModel.cs**, **UserProfileManagementViewModel.cs**.

---

## Step 5: UI Implementation with Maui Reactor

### 5.1 Setup Maui Reactor App

**App.cs** (replace default):
```csharp
using MauiReactor;

namespace BaristaNotes;

public class App : Component
{
    public override VisualNode Render()
    {
        return new AppShell();
    }
}
```

**AppShell.cs**:
```csharp
using MauiReactor;

namespace BaristaNotes;

public class AppShell : Component
{
    public override VisualNode Render()
    {
        return new Shell
        {
            new TabBar
            {
                new Tab("Shots")
                    .Icon("coffee.png")
                    .Content(() => new ShellContent()
                        .ContentTemplate(() => new ShotLoggingPage())),
                
                new Tab("History")
                    .Icon("history.png")
                    .Content(() => new ShellContent()
                        .ContentTemplate(() => new ActivityFeedPage())),
                
                new Tab("Equipment")
                    .Icon("tools.png")
                    .Content(() => new ShellContent()
                        .ContentTemplate(() => new EquipmentManagementPage())),
                
                new Tab("Beans")
                    .Icon("beans.png")
                    .Content(() => new ShellContent()
                        .ContentTemplate(() => new BeanManagementPage()))
            }
        };
    }
}
```

### 5.2 Create Theme Resources

**Resources/Styles/Colors.xaml**:
```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    
    <!-- Primary Colors -->
    <Color x:Key="ColorPrimary">#8B4513</Color> <!-- Coffee brown -->
    <Color x:Key="ColorPrimaryDark">#5C2E0A</Color>
    <Color x:Key="ColorAccent">#D2691E</Color> <!-- Light brown -->
    
    <!-- Surface Colors -->
    <Color x:Key="ColorSurface">#FFFFFF</Color>
    <Color x:Key="ColorBackground">#F5F5F5</Color>
    <Color x:Key="ColorOnSurface">#212121</Color>
    <Color x:Key="ColorOnBackground">#424242</Color>
    
    <!-- Semantic Colors -->
    <Color x:Key="ColorSuccess">#4CAF50</Color>
    <Color x:Key="ColorError">#F44336</Color>
    <Color x:Key="ColorWarning">#FF9800</Color>
    <Color x:Key="ColorInfo">#2196F3</Color>
</ResourceDictionary>
```

**Resources/Styles/Styles.xaml**:
```xml
<?xml version="1.0" encoding="UTF-8" ?>
<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">
    
    <!-- Spacing Scale -->
    <x:Double x:Key="SpaceXS">4</x:Double>
    <x:Double x:Key="SpaceS">8</x:Double>
    <x:Double x:Key="SpaceM">16</x:Double>
    <x:Double x:Key="SpaceL">24</x:Double>
    <x:Double x:Key="SpaceXL">32</x:Double>
    
    <!-- Typography -->
    <Style x:Key="HeadlineStyle" TargetType="Label">
        <Setter Property="FontSize" Value="32" />
        <Setter Property="FontAttributes" Value="Bold" />
    </Style>
    
    <Style x:Key="TitleStyle" TargetType="Label">
        <Setter Property="FontSize" Value="20" />
        <Setter Property="FontAttributes" Value="Bold" />
    </Style>
    
    <Style x:Key="BodyStyle" TargetType="Label">
        <Setter Property="FontSize" Value="16" />
    </Style>
    
    <!-- Button Styles -->
    <Style x:Key="PrimaryButtonStyle" TargetType="Button">
        <Setter Property="BackgroundColor" Value="{StaticResource ColorPrimary}" />
        <Setter Property="TextColor" Value="White" />
        <Setter Property="CornerRadius" Value="8" />
        <Setter Property="HeightRequest" Value="48" />
    </Style>
</ResourceDictionary>
```

### 5.3 Create Pages

**Pages/ShotLoggingPage.cs**:
```csharp
using MauiReactor;
using BaristaNotes.ViewModels;

namespace BaristaNotes.Pages;

public class ShotLoggingPage : Component<ShotLoggingViewModel>
{
    protected override void OnMounted()
    {
        base.OnMounted();
        State!.LoadDataAsync().ConfigureAwait(false);
    }
    
    public override VisualNode Render()
    {
        return new ContentPage
        {
            new ScrollView
            {
                new VStack(spacing: 16)
                {
                    new Label("Quick Shot Logging")
                        .Style("TitleStyle"),
                    
                    new Entry()
                        .Placeholder("Dose In (g)")
                        .Keyboard(Keyboard.Numeric)
                        .Text(State!.DoseIn.ToString())
                        .OnTextChanged(text => State.DoseIn = decimal.Parse(text)),
                    
                    // ... more form fields
                    
                    new Button("Save Shot")
                        .Style("PrimaryButtonStyle")
                        .OnClicked(async () => await State!.SaveShotAsync())
                }
                .Padding(16)
            }
        };
    }
}
```

Create: **ActivityFeedPage.cs**, **EquipmentManagementPage.cs**, **BeanManagementPage.cs**.

---

## Step 6: Dependency Injection Setup

**MauiProgram.cs**:
```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MauiReactor;
using CommunityToolkit.Maui;
using BaristaNotes.Data;
using BaristaNotes.Services;
using BaristaNotes.ViewModels;

namespace BaristaNotes;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiReactorApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("MaterialIcons-Regular.ttf", "MaterialIcons");
            });
        
        // Database
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "barista_notes.db");
        builder.Services.AddDbContext<BaristaNotesContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));
        
        // Repositories
        builder.Services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        builder.Services.AddScoped<IBeanRepository, BeanRepository>();
        builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        builder.Services.AddScoped<IShotRecordRepository, ShotRecordRepository>();
        
        // Services
        builder.Services.AddScoped<IShotService, ShotService>();
        builder.Services.AddScoped<IEquipmentService, EquipmentService>();
        builder.Services.AddScoped<IBeanService, BeanService>();
        builder.Services.AddScoped<IUserProfileService, UserProfileService>();
        builder.Services.AddSingleton<IPreferencesService, PreferencesService>();
        
        // ViewModels
        builder.Services.AddTransient<ShotLoggingViewModel>();
        builder.Services.AddTransient<ActivityFeedViewModel>();
        builder.Services.AddTransient<EquipmentManagementViewModel>();
        builder.Services.AddTransient<BeanManagementViewModel>();
        builder.Services.AddTransient<UserProfileManagementViewModel>();
        
        #if DEBUG
        builder.Logging.AddDebug();
        #endif
        
        var app = builder.Build();
        
        // Run migrations on startup
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<BaristaNotesContext>();
            context.Database.Migrate();
        }
        
        return app;
    }
}
```

---

## Step 7: Testing

### 7.1 Unit Tests

**BaristaNotes.Tests/Unit/Services/ShotServiceTests.cs**:
```csharp
using Xunit;
using Moq;
using BaristaNotes.Services;
using BaristaNotes.Data.Repositories;

namespace BaristaNotes.Tests.Unit.Services;

public class ShotServiceTests
{
    [Fact]
    public async Task CreateShot_ValidDto_CreatesAndReturnsShot()
    {
        // Arrange
        var mockRepo = new Mock<IShotRecordRepository>();
        var mockPrefs = new Mock<IPreferencesService>();
        var service = new ShotService(mockRepo.Object, mockPrefs.Object);
        
        var dto = new CreateShotDto
        {
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 28,
            ExpectedOutput = 36,
            DrinkType = "Espresso"
        };
        
        // Act
        var result = await service.CreateShotAsync(dto);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(18, result.DoseIn);
        mockRepo.Verify(r => r.AddAsync(It.IsAny<ShotRecord>()), Times.Once);
    }
    
    [Fact]
    public async Task CreateShot_InvalidDose_ThrowsValidationException()
    {
        // Test validation logic
    }
}
```

### 7.2 Integration Tests

**BaristaNotes.Tests/Integration/DatabaseTests.cs**:
```csharp
using Microsoft.EntityFrameworkCore;
using Xunit;
using BaristaNotes.Data;
using BaristaNotes.Models;

namespace BaristaNotes.Tests.Integration;

public class DatabaseTests
{
    private BaristaNotesContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BaristaNotesContext>()
            .UseInMemorySqlite("Data Source=:memory:")
            .Options;
        
        var context = new BaristaNotesContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        
        return context;
    }
    
    [Fact]
    public async Task CanAddAndRetrieveShot()
    {
        using var context = CreateInMemoryContext();
        
        var shot = new ShotRecord
        {
            Timestamp = DateTimeOffset.Now,
            DoseIn = 18,
            GrindSetting = "5",
            ExpectedTime = 28,
            ExpectedOutput = 36,
            DrinkType = "Espresso",
            SyncId = Guid.NewGuid(),
            LastModifiedAt = DateTimeOffset.Now
        };
        
        context.ShotRecords.Add(shot);
        await context.SaveChangesAsync();
        
        var retrieved = await context.ShotRecords.FirstOrDefaultAsync();
        Assert.NotNull(retrieved);
        Assert.Equal(18, retrieved.DoseIn);
    }
}
```

---

## Step 8: Run and Validate

### 8.1 Build and Run

```bash
dotnet build
dotnet run --project BaristaNotes --framework net8.0-android
# or
dotnet run --project BaristaNotes --framework net8.0-ios
```

### 8.2 Validate Constitution Compliance

- [ ] **Code Quality**: Services follow single responsibility, ViewModels separate from business logic
- [ ] **Test-First**: Unit and integration tests written and passing (80%+ coverage)
- [ ] **UX Consistency**: Theme-based styling applied, native controls used, 44x44px touch targets
- [ ] **Performance**: App launches <2s, form pre-population <500ms, 60fps scrolling

---

## Troubleshooting

### Migration Issues
```bash
# Drop database and recreate
dotnet ef database drop --project BaristaNotes
dotnet ef database update --project BaristaNotes
```

### NuGet Package Issues
```bash
# Clear cache and restore
dotnet nuget locals all --clear
dotnet restore
```

### Reactor Issues
- Ensure latest preview version installed
- Check MauiReactor GitHub issues for known problems

---

## Next Steps

After implementing core functionality:
1. Add photo attachments (future iteration)
2. Implement cloud sync using CoreSync
3. Add data export (CSV, JSON)
4. Add shot analytics/graphs
5. Implement dark mode theme

---

## Summary

This quickstart provides complete implementation path from data layer through UI. Follow steps sequentially for Test-First Development compliance. All design decisions documented in research.md and data-model.md. Service contracts defined in service-interfaces.md. Ready for implementation!
