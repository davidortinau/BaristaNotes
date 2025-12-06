# Project Structure

This document explains the organization of the BaristaNotes codebase and the purpose of each directory and file.

## Solution Overview

```
BaristaNotes/
├── BaristaNotes/              # Main MAUI application project
├── BaristaNotes.Core/         # Shared business logic and data
├── BaristaNotes.Tests/        # Unit and integration tests
├── docs/                      # Documentation
├── specs/                     # Feature specifications
├── BaristaNotes.sln           # Solution file
└── README.md                  # Project overview
```

## BaristaNotes Project (Main App)

The main MAUI application project contains UI components and platform-specific code.

```
BaristaNotes/
├── Components/                # Reusable UI components
│   ├── CircularAvatar.cs
│   ├── FormFields.cs
│   ├── ProfileImagePicker.cs
│   └── ShotRecordCard.cs
│
├── Pages/                     # Application screens
│   ├── ActivityFeedPage.cs
│   ├── BeanDetailPage.cs
│   ├── BeanManagementPage.cs
│   ├── EquipmentDetailPage.cs
│   ├── EquipmentManagementPage.cs
│   ├── ProfileFormPage.cs
│   ├── SettingsPage.cs
│   ├── ShotLoggingPage.cs
│   └── UserProfileManagementPage.cs
│
├── Platforms/                 # Platform-specific code
│   ├── Android/
│   │   └── MainActivity.cs
│   ├── iOS/
│   │   └── AppDelegate.cs
│   └── Windows/
│
├── Resources/                 # Application resources
│   ├── AppIcon/              # App icon assets
│   ├── Fonts/                # Custom fonts
│   ├── Images/               # Image assets
│   ├── Raw/                  # Raw assets
│   ├── Splash/               # Splash screen
│   └── Styles/
│       ├── Colors.xaml       # Color definitions
│       └── Styles.xaml       # Global styles
│
├── Services/                  # Platform services
│   ├── FeedbackService.cs
│   ├── ImagePickerService.cs
│   └── ImageProcessingService.cs
│
├── App.cs                     # Application lifecycle
├── AppShell.cs               # Shell navigation structure
└── MauiProgram.cs            # App builder and DI setup
```

### Key Files

#### MauiProgram.cs

Entry point for the application. Configures:
- MAUI app builder
- MauiReactor integration
- Dependency injection container
- Services registration
- Fonts and resources

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        
        builder
            .UseMauiReactorApp<App>()
            .ConfigureFonts(fonts => { /* ... */ });
            
        // Register services
        builder.Services.AddSingleton<BaristasDbContext>();
        builder.Services.AddTransient<IShotService, ShotService>();
        
        return builder.Build();
    }
}
```

#### AppShell.cs

Defines navigation structure and routes:

```csharp
public partial class AppShell : Shell
{
    public AppShell()
    {
        Routing.RegisterRoute("shot-logging", typeof(ShotLoggingPage));
        Routing.RegisterRoute("bean-detail", typeof(BeanDetailPage));
        // ...
    }
}
```

#### App.cs

Application lifecycle management:

```csharp
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new AppShell();
    }
}
```

### Pages

Each page is a MauiReactor component representing a screen in the app:

- **ActivityFeedPage**: Lists recent shot records with filtering
- **ShotLoggingPage**: Create/edit shot records
- **BeanManagementPage**: Lists coffee beans
- **BeanDetailPage**: View/edit bean details and shot history
- **EquipmentManagementPage**: Lists espresso equipment
- **EquipmentDetailPage**: View/edit equipment
- **UserProfileManagementPage**: Lists user profiles
- **ProfileFormPage**: Create/edit user profiles
- **SettingsPage**: App preferences and settings

### Components

Reusable UI components:

- **ShotRecordCard**: Displays shot summary in list views
- **CircularAvatar**: User profile image with circular crop
- **ProfileImagePicker**: Image selection with preview
- **FormFields**: Reusable form input components (Entry, Picker, Slider)

### Services

Platform-specific implementations:

- **FeedbackService**: User notifications (toasts, alerts)
- **ImagePickerService**: Photo selection from device
- **ImageProcessingService**: Image resizing and optimization

## BaristaNotes.Core Project

Shared business logic, data access, and domain models.

```
BaristaNotes.Core/
├── Data/                      # Entity Framework Core
│   ├── BaristasDbContext.cs
│   └── Migrations/           # EF Core migrations
│
├── Models/                    # Domain models
│   ├── Entities/             # Database entities
│   │   ├── Bean.cs
│   │   ├── Equipment.cs
│   │   ├── ShotRecord.cs
│   │   └── UserProfile.cs
│   │
│   ├── DTOs/                 # Data transfer objects
│   │   ├── BeanDto.cs
│   │   ├── EquipmentDto.cs
│   │   ├── ShotDto.cs
│   │   └── UserProfileDto.cs
│   │
│   ├── Requests/             # Service request models
│   │   ├── CreateShotRequest.cs
│   │   ├── UpdateShotRequest.cs
│   │   └── ...
│   │
│   └── Enums/                # Enumerations
│       ├── EquipmentType.cs
│       ├── RoastLevel.cs
│       └── TemperatureUnit.cs
│
└── Services/                  # Business services
    ├── Interfaces/
    │   ├── IBeanService.cs
    │   ├── IEquipmentService.cs
    │   ├── IPreferencesService.cs
    │   ├── IShotService.cs
    │   └── IUserProfileService.cs
    │
    └── Implementations/
        ├── BeanService.cs
        ├── EquipmentService.cs
        ├── PreferencesService.cs
        ├── ShotService.cs
        └── UserProfileService.cs
```

### Models

#### Entities

Database entities mapped to tables via Entity Framework:

```csharp
// Bean.cs - Represents coffee bean records
public class Bean
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string? Roaster { get; set; }
    public DateTime? RoastDate { get; set; }
    public RoastLevel RoastLevel { get; set; }
    
    public ICollection<ShotRecord> ShotRecords { get; set; }
}
```

#### DTOs

Data transfer objects for API surfaces:

```csharp
// BeanDto.cs - Bean data for UI layer
public record BeanDto
{
    public int Id { get; init; }
    public string Name { get; init; }
    public string? Roaster { get; init; }
    public int TotalShots { get; init; }  // Computed property
}
```

#### Requests

Request objects for service methods:

```csharp
// CreateBeanRequest.cs
public record CreateBeanRequest
{
    public string Name { get; init; }
    public string? Roaster { get; init; }
    public DateTime? RoastDate { get; init; }
    public RoastLevel RoastLevel { get; init; }
}
```

### Services

Business logic implementations:

```csharp
// ShotService.cs
public class ShotService : IShotService
{
    private readonly BaristasDbContext _context;
    
    public ShotService(BaristasDbContext context)
    {
        _context = context;
    }
    
    public async Task<ShotDto> CreateShotAsync(CreateShotRequest request)
    {
        // Business logic
    }
}
```

## BaristaNotes.Tests Project

Unit and integration tests.

```
BaristaNotes.Tests/
├── Unit/
│   ├── Services/
│   │   ├── BeanServiceTests.cs
│   │   ├── EquipmentServiceTests.cs
│   │   ├── ShotServiceTests.cs
│   │   └── UserProfileServiceTests.cs
│   │
│   └── Helpers/
│       └── TestDbContextFactory.cs
│
└── Integration/
    └── DatabaseTests.cs
```

### Test Structure

```csharp
public class ShotServiceTests
{
    private readonly BaristasDbContext _context;
    
    public ShotServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<BaristasDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDb")
            .Options;
            
        _context = new BaristasDbContext(options);
    }
    
    [Fact]
    public async Task CreateShotAsync_ValidData_CreatesShot()
    {
        // Test implementation
    }
}
```

## Documentation (docs/)

Comprehensive documentation:

- **MAUIREACTOR_PATTERNS.md**: MVU architecture and component patterns
- **DATA_LAYER.md**: Entity Framework and database design
- **SERVICES.md**: Service layer and dependency injection
- **GETTING_STARTED.md**: Setup and build instructions
- **CONTRIBUTING.md**: Development workflow and standards
- **PROJECT_STRUCTURE.md**: This file

## Specifications (specs/)

Feature specifications created during development:

```
specs/
├── 1-profile-image-picker/
│   ├── spec.md
│   ├── plan.md
│   ├── tasks.md
│   └── research.md
│
└── 2-bean-detail-page/
    ├── spec.md
    └── ...
```

## Configuration Files

### BaristaNotes.csproj

Main project file defining:
- Target frameworks (net10.0-ios, net10.0-android, etc.)
- NuGet package references
- Build configurations
- Platform-specific settings

Key sections:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <UseMaui>true</UseMaui>
    <SingleProject>true</SingleProject>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.Maui.Controls" Version="10.0.0" />
    <PackageReference Include="Reactor.Maui" Version="4.0.3-beta" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
  </ItemGroup>
</Project>
```

### BaristaNotes.Core.csproj

Core library project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
  </ItemGroup>
</Project>
```

### BaristaNotes.Tests.csproj

Test project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
  </ItemGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\BaristaNotes.Core\BaristaNotes.Core.csproj" />
  </ItemGroup>
</Project>
```

## Build Artifacts

Generated during build (gitignored):

```
bin/                  # Compiled binaries
obj/                  # Intermediate build files
*.binlog              # Binary build logs
```

## Resource Organization

### Images

```
Resources/Images/
├── dotnet_bot.png           # Sample image
└── (user-uploaded images stored in app data directory)
```

### Fonts

```
Resources/Fonts/
├── OpenSans-Regular.ttf
└── OpenSans-Semibold.ttf
```

### Styles

Global styles defined in XAML:

```
Resources/Styles/
├── Colors.xaml       # Color palette
└── Styles.xaml       # Control styles
```

## Platform-Specific Code

### iOS (Platforms/iOS/)

- **Info.plist**: App permissions, capabilities
- **Entitlements.plist**: App entitlements
- **AppDelegate.cs**: iOS lifecycle hooks

### Android (Platforms/Android/)

- **AndroidManifest.xml**: App permissions, metadata
- **MainActivity.cs**: Android lifecycle hooks

### Windows (Platforms/Windows/)

- **Package.appxmanifest**: Windows app manifest
- **app.manifest**: Windows-specific settings

## Naming Conventions

### Files

- **PascalCase** for class files: `ShotLoggingPage.cs`
- **camelCase** for resource files: `dotnet_bot.png`
- **kebab-case** for documentation: `getting-started.md`

### Directories

- **PascalCase** for code directories: `Pages/`, `Services/`
- **lowercase** for non-code: `docs/`, `specs/`

## Dependencies

### NuGet Packages

Key dependencies tracked in project files:

- **Microsoft.Maui.Controls** - MAUI framework
- **Reactor.Maui** - MauiReactor MVU library
- **Microsoft.EntityFrameworkCore.Sqlite** - EF Core SQLite provider
- **CommunityToolkit.Maui** - Community extensions
- **xunit** - Testing framework

### Project References

```
BaristaNotes → BaristaNotes.Core
BaristaNotes.Tests → BaristaNotes.Core
```

## Additional Resources

- [MAUI Project Structure](https://learn.microsoft.com/dotnet/maui/fundamentals/single-project)
- [Entity Framework Conventions](https://learn.microsoft.com/ef/core/modeling/)
- [xUnit Documentation](https://xunit.net/)
