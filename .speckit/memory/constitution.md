# Constitution

## Core Principles

### Data Preservation (Non-Negotiable)
**NEVER delete the database or suggest deleting the database under ANY circumstances.**
- User data is valuable and must ALWAYS be preserved
- Use EF Core migrations for ALL schema changes
- Migrations must include data-preserving SQL for complex changes
- Test migrations thoroughly before deployment
- If migration is complex, create manual SQL scripts to preserve data
- Violation of this principle is completely unacceptable

### Rating Scale Standard
- **All ratings use 0-4 scale** (5 levels: 0=Terrible, 1=Bad, 2=Average, 3=Good, 4=Excellent)
- Coffee cup icon (☕) represents rating levels throughout the UI
- Display ratings in descending order (4 → 0) for rating distributions
- Never use 1-5 scale or star ratings

## Development Standards

### MauiReactor UI Standards
- **Always use ThemeKey system** - Never use inline styling (e.g., `.FontSize()`, `.TextColor()`, `.BackgroundColor()`)
- Create new theme keys in `ThemeKeys.cs` and define them in `ApplicationTheme.cs`
- Reference existing theme files before inventing properties:
  - `ThemeKeys.cs` - Theme key constants
  - `AppColors.cs` - Color definitions
  - `ApplicationTheme.cs` - Theme implementations
  - `AppFontSizes.cs` - Font size constants
  - `AppSpacing.cs` - Spacing constants
- Use correct types: `FontAttributes` is from `Microsoft.Maui.Controls`, NOT `Microsoft.Maui.Graphics.Text`

### Build Verification
- **ALWAYS build the application before reporting success**
- Run `dotnet build` and verify zero errors
- Never report "no errors" without actually building
- Fix all compilation errors before marking tasks complete

### EF Core Migrations
- Never delete existing migration files
- Restore deleted migrations from git history before creating new ones
- Use data-preserving SQL for schema changes that affect existing data
- Test migrations on copy of production database when possible
