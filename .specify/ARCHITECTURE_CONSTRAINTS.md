# Architecture Constraints

**Document Purpose**: This file defines the mandatory technical implementation rules for the BaristaNotes project. These constraints are NON-NEGOTIABLE without stakeholder approval.

**Relationship to Constitution**: The project [Constitution](memory/constitution.md) establishes governance principles and rationale (WHY/WHAT). This document provides technical implementation rules (HOW). Both are mandatory.

---

## NON-NEGOTIABLE ARCHITECTURAL DECISIONS

These constraints are **MANDATORY** and **CANNOT** be changed without explicit user approval.

---

## 🚫 Icons: MaterialSymbolsFont Only (NO EMOJIS)

**RULE**: ALL icons in the UI **MUST** use `MaterialSymbolsFont`. Emojis are **ABSOLUTELY PROHIBITED**.

**Why**: Emojis render inconsistently across platforms (iOS, Android, Windows, Mac), break accessibility (screen readers handle them poorly), and give the application an unprofessional appearance.

**Examples**:
```csharp
// ✅ CORRECT - Use MaterialSymbolsFont
Label(MaterialSymbolsFont.Coffee)
    .FontFamily(MaterialSymbolsFont.FontFamily)
    .FontSize(48)

// ✅ CORRECT - Use MaterialSymbolsFont with color
Label(MaterialSymbolsFont.Warning)
    .FontFamily(MaterialSymbolsFont.FontFamily)
    .TextColor(Colors.Orange)

// ❌ WRONG - NEVER use emoji characters
Label("☕")  // BANNED
Label("⭐")  // BANNED
Label("⚠️")  // BANNED
Label("✓")   // BANNED
Label("❌")  // BANNED
```

**Available Icons**: See `Resources/Fonts/MaterialSymbolsFont.cs` for the full list of available icons.

**Common Icon Mappings**:
| Intent | Correct | WRONG |
|--------|---------|-------|
| Coffee | `MaterialSymbolsFont.Coffee` | ☕ |
| Warning | `MaterialSymbolsFont.Warning` | ⚠️ |
| Star/Rating | `MaterialSymbolsFont.Star` | ⭐ |
| Check | `MaterialSymbolsFont.Check` | ✓ ✔ |
| Error | `MaterialSymbolsFont.Error` | ❌ ✕ |
| Person | `MaterialSymbolsFont.Person` | 👤 |
| Add | `MaterialSymbolsFont.Add` | ➕ |

**NO EXCEPTIONS**: If an icon is needed that doesn't exist in MaterialSymbolsFont, request it be added to the font or use a PNG/SVG asset. NEVER fall back to emoji.

---

## 🚫 UI Framework: MauiReactor

**RULE**: All UI must use **MauiReactor** components, NOT standard MAUI XAML or C# UI patterns.

**Why**: This is the chosen UI framework for the entire application.

**Examples**:
- ✅ `Button("Click Me").OnClicked(async () => ...)`
- ✅ `Entry().Text(state.Name).OnTextChanged(t => ...)`
- ❌ `new Button { Text = "Click Me" }`
- ❌ XAML files

---

## 🚫 Popups, Toasts, Alerts: UXDivers.Popups.Maui

**RULE**: ALL popups, toasts, modals, and alert-style UI **MUST** use **UXDivers.Popups.Maui** library.

**Package**: `UXDivers.Grial`

**Why**: This provides:
- Consistent styled popups across the app
- Color-coded feedback (green success, red error, blue info, yellow warning)
- Custom animations and positioning
- Full control over appearance

**Service**: `IFeedbackService` wraps UXDivers and provides:
- `ShowSuccess(message)` - Green toast with ✓
- `ShowError(message, recoveryAction?)` - Red toast with ✕
- `ShowInfo(message)` - Blue toast with ℹ
- `ShowWarning(message)` - Yellow toast with ⚠
- `ShowLoading(message)` / `HideLoading()` - Loading spinner overlay

**❌ DO NOT USE**:
- ❌ `CommunityToolkit.Maui.Alerts.Toast` - Wrong library!
- ❌ `Application.Current.MainPage.DisplayAlert()` - Blocking, inconsistent styling
- ❌ Custom popup implementations - Reinventing the wheel
- ❌ Platform-specific toasts - No control over styling

**Examples**:
```csharp
// ✅ CORRECT
_feedbackService.ShowSuccess("Shot saved successfully");
_feedbackService.ShowError("Failed to save", "Please try again");

// ❌ WRONG
var toast = Toast.Make("Shot saved"); // CommunityToolkit
await Application.Current.MainPage.DisplayAlert("Success", "Shot saved", "OK");
```

**Implementation Details**:
- UXDivers toasts are `async void` (fire-and-forget)
- Default durations: Success=2000ms, Error=5000ms, Info=3000ms, Warning=3000ms
- Toasts display at top of screen with slide-in animation
- **If navigating after showing toast**: Add `await Task.Delay(2000)` to allow toast to display

---

## 🚫 Navigation: Shell-Based with MauiReactor Extensions

**RULE**: Use Shell navigation with MauiReactor's `GoToAsync` extensions.

**Pattern for passing parameters**:
```csharp
// Register route
Routing.RegisterRoute<MyPage>("my-page");

// Navigate with props
await Shell.Current.GoToAsync<MyPageProps>("my-page", props => props.Id = 123);

// Page must inherit Component<TState, TProps>
class MyPage : Component<MyPageState, MyPageProps>
{
    // Access via Props.Id
}
```

**❌ DO NOT USE**:
- ❌ `Navigation.PushAsync()` - Not Shell-based
- ❌ QueryProperty attributes - Not compatible with MauiReactor Props pattern
- ❌ Passing parameters via query strings - Use typed Props

---

## 🚫 Dependency Injection: Microsoft.Extensions.DependencyInjection

**RULE**: All services **MUST** be registered in DI and injected via `[Inject]` attribute.

**Examples**:
```csharp
// ✅ CORRECT
partial class MyPage : Component<MyPageState>
{
    [Inject]
    IShotService _shotService;
    
    [Inject]
    IFeedbackService _feedbackService;
}

// ❌ WRONG
var shotService = new ShotService(); // Manual instantiation
var shotService = ServiceLocator.Get<IShotService>(); // Service locator pattern
```

---

## 🚫 Data Layer: Entity Framework Core

**RULE**: All database operations **MUST** go through Entity Framework Core DbContext.

**Why**: Centralized schema management, migrations, change tracking, LINQ queries.

**❌ DO NOT USE**:
- ❌ Raw SQL strings
- ❌ SQLite.Net direct queries
- ❌ Manual ADO.NET connections

**Exception**: Raw SQL is allowed ONLY for:
- Complex reporting queries that EF can't optimize
- Bulk operations where performance is critical
- Must use `dbContext.Database.ExecuteSqlRaw()` with parameterized queries

### EF Core Migration Workflow (MANDATORY)

**ALL** database schema changes MUST follow this exact workflow:

1. **Update Entity Models**: Modify entity classes in `*.Core/Models/`
   ```csharp
   // Example: Adding a new property
   public class ShotRecord
   {
       public int Id { get; set; }
       public string BeanName { get; set; }
       public DateTime Timestamp { get; set; }
       public int Rating { get; set; } // NEW property
   }
   ```

2. **Update DbContext Configuration** (if needed):
   ```csharp
   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
       modelBuilder.Entity<ShotRecord>()
           .Property(s => s.Rating)
           .HasDefaultValue(0); // Optional: add constraints
   }
   ```

3. **Generate Migration**:
   ```bash
   cd BaristaNotes.Core
   dotnet ef migrations add AddRatingToShotRecord
   ```

4. **Review Generated Migration**:
   - Open `Migrations/[timestamp]_AddRatingToShotRecord.cs`
   - Verify `Up()` contains only intended changes
   - Add custom SQL for data preservation if needed:
     ```csharp
     protected override void Up(MigrationBuilder migrationBuilder)
     {
         migrationBuilder.AddColumn<int>(
             name: "Rating",
             table: "ShotRecords",
             nullable: false,
             defaultValue: 0);
         
         // Custom SQL for data transformation
         migrationBuilder.Sql(
             "UPDATE ShotRecords SET Rating = 2 WHERE Rating = 0");
     }
     ```

5. **Test Migration Locally**:
   ```bash
   dotnet ef database update
   ```

6. **Test Rollback** (verify Down() works):
   ```bash
   dotnet ef database update [PreviousMigrationName]
   dotnet ef database update  # Re-apply
   ```

7. **Production Deployment**: Application startup automatically applies migrations:
   ```csharp
   // In App.xaml.cs or Startup
   await dbContext.Database.MigrateAsync();
   ```

### Data-Preserving Migration Patterns

**When Renaming Columns/Tables**:
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.RenameColumn(
        name: "OldName",
        table: "ShotRecords",
        newName: "NewName");
}
```

**When Restructuring Data**:
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 1. Add new column
    migrationBuilder.AddColumn<string>(
        name: "FullName",
        table: "Users");
    
    // 2. Copy/transform existing data
    migrationBuilder.Sql(@"
        UPDATE Users 
        SET FullName = FirstName || ' ' || LastName 
        WHERE FirstName IS NOT NULL");
    
    // 3. Remove old columns (only after data copied)
    migrationBuilder.DropColumn(name: "FirstName", table: "Users");
    migrationBuilder.DropColumn(name: "LastName", table: "Users");
}
```

**When Changing Types**:
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Add temporary column with new type
    migrationBuilder.AddColumn<DateTime>(
        name: "CreatedAt_New",
        table: "ShotRecords");
    
    // Convert data
    migrationBuilder.Sql(@"
        UPDATE ShotRecords 
        SET CreatedAt_New = datetime(CreatedAt_Old, 'unixepoch')");
    
    // Drop old, rename new
    migrationBuilder.DropColumn(name: "CreatedAt_Old", table: "ShotRecords");
    migrationBuilder.RenameColumn(
        name: "CreatedAt_New",
        table: "ShotRecords",
        newName: "CreatedAt");
}
```

### Migration Troubleshooting

**Problem**: "Table already exists" error
- **DO**: Create migration to add `DropTable()` or use `migrationBuilder.Sql("DROP TABLE IF EXISTS...")` in Up()
- **DON'T**: Delete database or manually drop table

**Problem**: Migration and database out of sync
- **DO**: Check `__EFMigrationsHistory` table, create corrective migration
- **DON'T**: Delete migrations or database

**Problem**: Accidentally deleted migration files
- **DO**: Restore from git history: `git checkout HEAD~1 -- Migrations/`
- **DON'T**: Recreate migration with `migrations add` (creates duplicate)

### Migration Best Practices

✅ **DO**:
- Keep migrations small and focused (one logical change per migration)
- Name migrations descriptively: `AddRatingColumn`, `RenameUserTable`
- Test migrations with production-like data volumes
- Document complex migrations with comments
- Include rollback SQL in Down() method

❌ **DON'T**:
- Modify existing migration files after they've been applied
- Delete migration files (breaks deployment)
- Create migrations manually (use `dotnet ef migrations add`)
- Skip testing Down() migrations
- Apply migrations directly in production database (let app apply them)

---

## 🚫 Testing Framework: xUnit + FluentAssertions

**RULE**: All tests use xUnit syntax with FluentAssertions for assertions.

**Examples**:
```csharp
// ✅ CORRECT
[Fact]
public void Should_Calculate_Correctly()
{
    var result = calculator.Add(2, 3);
    result.Should().Be(5);
}

// ❌ WRONG
[Test] // NUnit
public void TestCalculation()
{
    Assert.AreEqual(5, result); // MSTest/NUnit syntax
}
```

---

## 🚫 Async/Await Patterns

**RULE**: 
1. Methods returning `Task` or `Task<T>` **MUST** be `async`
2. `async void` is **ONLY** allowed for event handlers
3. Always `await` async operations - don't use `.Result` or `.Wait()`

**Navigation Timing with Toasts**:
When showing a toast before navigation:
```csharp
// ✅ CORRECT - Wait for toast to display
_feedbackService.ShowSuccess("Operation complete");
await Task.Delay(2000); // Match toast duration
await Navigation.PopAsync();

// ❌ WRONG - Toast gets interrupted
_feedbackService.ShowSuccess("Operation complete");
await Navigation.PopAsync(); // Immediate navigation kills toast
```

---

## 🚫 State Management: Component State Pattern

**RULE**: MauiReactor components manage state via `Component<TState>` base class.

**Pattern**:
```csharp
class MyState
{
    public string Name { get; set; } = "";
    public bool IsLoading { get; set; }
}

class MyPage : Component<MyState>
{
    protected override void OnMounted()
    {
        SetState(s => s.IsLoading = true);
    }
}
```

**❌ DO NOT USE**:
- ❌ `INotifyPropertyChanged` - MauiReactor handles this
- ❌ Observable collections - Use `List<T>` in state, create new instances on updates
- ❌ Manual property change notifications

---

## When In Doubt

1. **Check existing code** in the same project
2. **Ask the user** before introducing new libraries or patterns
3. **Document your reasoning** if you think a constraint should be changed

**Remember**: These constraints exist for **consistency**, **maintainability**, and **team productivity**. Violating them creates technical debt.

