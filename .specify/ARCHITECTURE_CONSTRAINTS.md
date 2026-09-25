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

**Available Icons**: See `src/BaristaNotes/Components/MaterialSymbolsFont.cs` for the full list of available icons.

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
- ❌ XAML application screens

XAML resource dictionaries required by MAUI or a dependency are allowed.

---

## 🚫 Feedback and Toasts: UXDivers.Popups.Maui

**RULE**: User feedback and toast-style messages **MUST** use
`IFeedbackService`. UXDivers popups are used for custom modal workflows.
Blocking confirmation questions and platform workflow messages can use
`ContainerPage.DisplayAlertAsync`.

**Package**: `UXDivers.Popups.Maui`

**Why**: This provides:
- Consistent styled popups across the app
- Color-coded feedback (green success, red error, blue info, yellow warning)
- Custom animations and positioning
- Full control over appearance

**Service**: `IFeedbackService` wraps application feedback and provides:
- `ShowSuccessAsync(message)`
- `ShowErrorAsync(message, recoveryAction?)`
- `ShowInfoAsync(message)`
- `ShowWarningAsync(message)`
- `ShowActionToastAsync(message, actionText, onAction)`
- observable feedback and loading-state streams

**❌ DO NOT USE**:
- ❌ `CommunityToolkit.Maui.Alerts.Toast` - Wrong library!
- ❌ `Application.Current.MainPage.DisplayAlert()` - Bypasses the component page
- ❌ Custom popup implementations - Reinventing the wheel
- ❌ Platform-specific toasts - No control over styling

**Examples**:
```csharp
// ✅ CORRECT
await _feedbackService.ShowSuccessAsync("Shot saved successfully");
await _feedbackService.ShowErrorAsync("Failed to save", "Please try again");

// ❌ WRONG
var toast = Toast.Make("Shot saved"); // CommunityToolkit
await Application.Current.MainPage.DisplayAlert("Success", "Shot saved", "OK");
```

For a required confirmation:

```csharp
var confirmed = await ContainerPage.DisplayAlertAsync(
    "Delete bag?",
    "This action cannot be undone.",
    "Delete",
    "Cancel");
```

**Implementation Details**:
- Feedback methods return `Task` and callers await them
- Default durations: Success=2000ms, Error=5000ms, Info=3000ms, Warning=3000ms
- Toasts display at top of screen with slide-in animation
- Do not add arbitrary delays to coordinate navigation and feedback

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

**DO NOT USE**:
- SQLite.Net direct queries
- page-level ADO.NET connections
- direct database writes to create test or sample data

**Versioned startup exception**: `DatabaseInitializer` uses reviewed static SQL
for idempotent, NativeAOT-safe schema creation and upgrades. Data values must
remain parameterized. Normal application data access still uses EF and the
existing repositories.

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
   dotnet ef migrations add AddRatingToShotRecord \
     --project src/BaristaNotes.Core \
     --startup-project src/BaristaNotes.Core \
     --context BaristaNotesContext
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

5. **Update the Startup Path**:
   - Add the same idempotent schema step and migration identifier to
     `DatabaseInitializer`.
   - Add integration tests for a new database, the affected legacy schema, and
     repeated initialization.

6. **Regenerate NativeAOT Artifacts**:
   - Use an EF tool version that matches the project packages.
   - Regenerate the compiled model and precompiled query interceptors.

7. **Test Migration and Rollback**:
   - Verify `Up()` and `Down()` against a backup.
   - Verify application startup through `DatabaseInitializer`.

8. **Production Deployment**: Application startup runs the versioned
   `DatabaseInitializer` path. It does not call `Database.MigrateAsync()`.

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
- **DO**: Inspect the schema and migration history on a backup, then add an
  idempotent corrective migration and initializer check.
- **DON'T**: Delete the database or drop the table as a general repair.

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

## 🚫 Testing Framework: xUnit + Moq

**RULE**: All tests use xUnit syntax. Use Moq for suitable dependency test
doubles and SQLite in-memory connections for EF tests.

**Examples**:
```csharp
// ✅ CORRECT
[Fact]
public void Should_Calculate_Correctly()
{
    var result = calculator.Add(2, 3);
    Assert.Equal(5, result);
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
1. Await asynchronous I/O.
2. A method can return an existing `Task` without the `async` keyword when no
   local await is required.
3. `async void` is allowed only for event handlers and framework lifecycle
   overrides that require it.
4. Do not use `.Result` or `.Wait()`.
5. Propagate `CancellationToken` for work that can outlive its caller.

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
