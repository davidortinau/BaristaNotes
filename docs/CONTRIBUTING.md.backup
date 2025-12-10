# Contributing to BaristaNotes

Thank you for your interest in contributing to BaristaNotes! This document provides guidelines and best practices for contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Process](#pull-request-process)
- [Educational Focus](#educational-focus)

## Code of Conduct

This project is intended as an educational resource. Contributors should:

- Be respectful and constructive in discussions
- Focus on educational value and code clarity
- Help newcomers understand .NET MAUI and MauiReactor patterns
- Provide clear explanations for technical decisions

## Getting Started

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork:
   ```bash
   git clone https://github.com/yourusername/BaristaNotes.git
   cd BaristaNotes
   ```
3. Add upstream remote:
   ```bash
   git remote add upstream https://github.com/originalowner/BaristaNotes.git
   ```

### Set Up Development Environment

Follow the [Getting Started Guide](GETTING_STARTED.md) to:
- Install required software (.NET 10 SDK, IDE)
- Install platform-specific tools (Xcode, Android SDK)
- Build and run the application

### Create a Branch

Create a feature branch for your work:

```bash
git checkout -b feature/your-feature-name
```

Branch naming conventions:
- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation improvements
- `refactor/` - Code refactoring
- `test/` - Test additions or improvements

## Development Workflow

### 1. Sync with Upstream

Before starting work, sync with the upstream repository:

```bash
git fetch upstream
git checkout main
git merge upstream/main
git push origin main
```

### 2. Make Changes

- Follow the [Coding Standards](#coding-standards)
- Write tests for new functionality
- Update documentation as needed
- Test your changes on at least one platform

### 3. Commit Changes

Write clear, descriptive commit messages:

```bash
git add .
git commit -m "Add profile image picker functionality"
```

Commit message guidelines:
- Use present tense ("Add feature" not "Added feature")
- Use imperative mood ("Move cursor to..." not "Moves cursor to...")
- Limit first line to 72 characters
- Reference issues and pull requests where appropriate

### 4. Push Changes

```bash
git push origin feature/your-feature-name
```

### 5. Create Pull Request

Open a pull request on GitHub with:
- Clear title describing the change
- Detailed description of what and why
- Reference to any related issues
- Screenshots/videos for UI changes
- Test results

## Coding Standards

### C# Style Guide

Follow Microsoft's C# coding conventions with these specifics:

#### Naming Conventions

```csharp
// Pascal case for classes, methods, properties
public class ShotService { }
public void CreateShot() { }
public string BeanName { get; set; }

// Camel case for local variables, parameters
int shotId = 1;
public void UpdateShot(int shotId) { }

// Prefix interfaces with 'I'
public interface IShotService { }

// Suffix async methods with 'Async'
public async Task<ShotDto> GetShotAsync(int id) { }

// Private fields with underscore prefix
private readonly IShotService _shotService;
```

#### File Organization

```csharp
// 1. Usings
using System;
using Microsoft.Maui.Controls;

// 2. Namespace
namespace BaristaNotes.Pages;

// 3. Class with members in this order:
public class ShotLoggingPage : Component
{
    // Constants
    private const int MaxRating = 5;
    
    // Fields
    private readonly IShotService _shotService;
    
    // Constructors
    public ShotLoggingPage() { }
    
    // Properties
    public int ShotId { get; set; }
    
    // Public methods
    public override VisualNode Render() { }
    
    // Protected methods
    protected override void OnMounted() { }
    
    // Private methods
    private async Task SaveShot() { }
}
```

#### Code Formatting

- Use 4 spaces for indentation (no tabs)
- Place opening braces on new line
- One statement per line
- Always use braces for if/else blocks

```csharp
// Good
if (condition)
{
    DoSomething();
}

// Avoid
if (condition) DoSomething();
```

### MauiReactor Patterns

Follow established patterns in the codebase:

#### Component Structure

```csharp
// State class
class MyPageState
{
    public string Text { get; set; } = "";
    public bool IsLoading { get; set; }
}

// Props class (if needed)
class MyPageProps
{
    public int ItemId { get; set; }
}

// Component
partial class MyPage : Component<MyPageState, MyPageProps>
{
    [Inject]
    IMyService _myService;
    
    public override VisualNode Render()
    {
        return ContentPage(
            VStack(
                Label(State.Text)
            )
        );
    }
    
    protected override async void OnMounted()
    {
        base.OnMounted();
        await LoadData();
    }
}
```

#### State Updates

```csharp
// Good: Single SetState call
SetState(s => 
{
    s.IsLoading = false;
    s.Data = result;
    s.Error = null;
});

// Avoid: Multiple SetState calls
SetState(s => s.IsLoading = false);
SetState(s => s.Data = result);
SetState(s => s.Error = null);
```

#### Navigation

```csharp
// Use typed props navigation
await Shell.Current.GoToAsync<ShotLoggingPageProps>(
    "shot-logging",
    props => props.ShotId = shotId
);

// Avoid query parameters
await Shell.Current.GoToAsync($"shot-logging?id={shotId}");
```

### Service Layer

#### Service Interfaces

```csharp
public interface IShotService
{
    // Async methods with Async suffix
    Task<ShotDto?> GetShotByIdAsync(int id);
    Task<List<ShotDto>> GetAllShotsAsync();
    Task<ShotDto> CreateShotAsync(CreateShotRequest request);
    
    // Sync properties without Async
    int TotalShots { get; }
}
```

#### DTOs and Requests

```csharp
// Use records for DTOs (immutable)
public record ShotDto
{
    public int Id { get; init; }
    public string? BeanName { get; init; }
    public double Dose { get; init; }
}

// Use records for requests
public record CreateShotRequest
{
    public int? BeanId { get; init; }
    public double Dose { get; init; }
    public int Rating { get; init; }
}
```

### Comments and Documentation

#### When to Comment

```csharp
// Good: Explain WHY, not WHAT
// Calculate ratio to ensure we're in the 1:2 to 1:3 range for espresso
var ratio = outputWeight / dose;

// Avoid: Stating the obvious
// Set the dose to 18
var dose = 18;
```

#### XML Documentation

Add XML docs for public APIs:

```csharp
/// <summary>
/// Creates a new espresso shot record with the provided parameters.
/// </summary>
/// <param name="request">Shot creation parameters</param>
/// <returns>The created shot DTO with generated ID</returns>
/// <exception cref="ValidationException">Thrown when parameters are invalid</exception>
public async Task<ShotDto> CreateShotAsync(CreateShotRequest request)
{
    // ...
}
```

## Testing Guidelines

### Test Structure

```csharp
public class ShotServiceTests
{
    [Fact]
    public async Task CreateShotAsync_ValidData_CreatesShot()
    {
        // Arrange
        var service = CreateService();
        var request = new CreateShotRequest { Dose = 18.0 };
        
        // Act
        var result = await service.CreateShotAsync(request);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(18.0, result.Dose);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateShotAsync_InvalidDose_ThrowsException(double dose)
    {
        // Arrange
        var service = CreateService();
        var request = new CreateShotRequest { Dose = dose };
        
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => service.CreateShotAsync(request)
        );
    }
}
```

### Test Coverage

- Write tests for all service layer methods
- Test both success and failure scenarios
- Use theory tests for multiple input scenarios
- Aim for high coverage but prioritize critical paths

### Running Tests

```bash
# All tests
dotnet test

# With coverage
dotnet test --collect:"XPlat Code Coverage"

# Specific test
dotnet test --filter "FullyQualifiedName~ShotServiceTests"
```

## Pull Request Process

### Before Submitting

- [ ] Code follows style guidelines
- [ ] Tests added/updated and passing
- [ ] Documentation updated if needed
- [ ] Commit messages are clear
- [ ] Branch is up to date with main
- [ ] Build succeeds on at least one platform

### PR Description

Include in your PR description:

1. **Summary**: What does this PR do?
2. **Motivation**: Why is this change needed?
3. **Changes**: What changed? (code, docs, tests)
4. **Testing**: How was this tested?
5. **Screenshots**: For UI changes
6. **Breaking Changes**: Any breaking changes?

Example:

```markdown
## Summary
Adds profile image picker functionality allowing users to select and save profile photos.

## Motivation
Users requested the ability to add profile pictures to distinguish between multiple users.

## Changes
- Added IImagePickerService and implementation
- Added IImageProcessingService for image resizing
- Created ProfileImagePicker component
- Updated UserProfileService with image methods
- Added unit tests for services

## Testing
- Tested on iOS Simulator (iPhone 15)
- Tested on Android Emulator (Pixel 6)
- Unit tests pass locally
- Verified image persistence across app restarts

## Screenshots
[Include before/after screenshots]

## Breaking Changes
None
```

### Review Process

1. Automated checks run (build, tests)
2. Maintainers review code
3. Address feedback by pushing new commits
4. Once approved, PR will be merged

## Educational Focus

Since this is an educational project, contributions should enhance learning value:

### Good Contributions

- Clear, well-commented code examples
- Documentation improvements
- Additional tests demonstrating patterns
- Simplified implementations that are easier to understand
- Performance improvements with explanations

### Avoid

- Over-engineered solutions
- Magic/clever code without explanation
- Breaking changes without clear benefit
- Removing educational comments
- Complex patterns without justification

## Questions?

If you have questions:

1. Check existing [documentation](README.md)
2. Search [GitHub Issues](https://github.com/yourusername/BaristaNotes/issues)
3. Open a new issue with "Question" label

## Additional Resources

- [C# Coding Conventions](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- [.NET MAUI Docs](https://learn.microsoft.com/dotnet/maui/)
- [MauiReactor Patterns](MAUIREACTOR_PATTERNS.md)
- [Entity Framework Best Practices](https://learn.microsoft.com/ef/core/miscellaneous/nullable-reference-types)
