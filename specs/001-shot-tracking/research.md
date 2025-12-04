# Research: Shot Maker, Recipient, and Preinfusion Tracking

**Feature**: 001-shot-tracking  
**Date**: 2025-12-03  
**Phase**: 0 - Outline & Research

## Overview

This document consolidates research findings for implementing shot maker, recipient, and preinfusion time tracking in BaristaNotes. All technical unknowns from the plan's Technical Context have been resolved.

## Research Tasks

### 1. .NET MAUI Preferences API for Last-Used Values

**Decision**: Use `Microsoft.Maui.Storage.Preferences` API for storing last-used form values

**Rationale**:
- Built into .NET MAUI - no additional dependencies required
- Synchronous API with fast read/write performance
- Automatically handles platform-specific storage (UserDefaults on iOS, SharedPreferences on Android, ApplicationData on Windows)
- Survives app restarts and updates
- Simple key-value storage perfect for this use case

**Alternatives Considered**:
- **SQLite table**: Rejected - overkill for simple key-value storage, adds query overhead
- **JSON file**: Rejected - requires file I/O management, error handling, no platform optimization
- **SecureStorage**: Rejected - unnecessary encryption overhead for non-sensitive data

**Implementation Pattern**:
```csharp
// Store preferences
Preferences.Set("LastUsedBeanId", beanId);
Preferences.Set("LastUsedMadeById", madeById);
Preferences.Set("LastUsedMadeForId", madeForId);

// Retrieve preferences with defaults
int? lastBeanId = Preferences.ContainsKey("LastUsedBeanId") 
    ? Preferences.Get("LastUsedBeanId", 0) 
    : null;
```

**Best Practices**:
- Use strongly-typed keys (constants or enum) to avoid typos
- Provide sensible defaults when key doesn't exist
- Clear preferences on logout/reset if user-specific
- Keep stored data minimal - IDs only, not full objects

**Documentation**: https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/preferences

---

### 2. User Selection UI Pattern in MauiReactor

**Decision**: Use MauiReactor `Picker` component with data binding to UserProfile list

**Rationale**:
- MauiReactor Picker component maps directly to MAUI Picker control
- Supports ItemsSource binding to ObservableCollection<UserProfile>
- Built-in ItemDisplayBinding for showing user names
- Consistent with existing form controls in ShotLoggingPage
- Platform-native UI behavior (iOS wheel picker, Android dropdown)

**Alternatives Considered**:
- **Custom SearchableListView**: Rejected for MVP - adds complexity, Picker sufficient for <100 users
- **Autocomplete Entry**: Rejected - not part of standard MAUI/MauiReactor, would require custom implementation
- **CollectionView with selection**: Rejected - takes too much screen space in a form

**Implementation Pattern**:
```csharp
Picker()
    .Title("Made By")
    .ItemsSource(State.Users, user => user.Id)
    .ItemDisplayBinding(user => user.Name)
    .SelectedItem(State.SelectedMaker)
    .OnSelectedItemChanged(user => SetState(s => s.SelectedMaker = user))
```

**Best Practices**:
- Load user list once in OnMounted, cache in state
- Sort users alphabetically for easy scanning
- Provide "None" or empty option for optional selections
- Show loading indicator while fetching users
- Handle empty user list gracefully (show message to create users first)

**Documentation**: https://adospace.gitbook.io/mauireactor/components/picker

---

### 3. Database Migration Strategy for New Fields

**Decision**: Add nullable foreign key columns via EF Core migration, maintain referential integrity

**Rationale**:
- Nullable FKs allow existing shots to remain valid (backward compatibility)
- EF Core handles relationship navigation properties automatically
- Soft delete pattern already implemented for UserProfile (IsDeleted flag)
- Cascade delete not appropriate - preserve historical data even if user deleted

**Alternatives Considered**:
- **Denormalize user names**: Rejected - data duplication, name changes wouldn't reflect, violates normalization
- **Required FKs**: Rejected - breaks existing data, forces retroactive data entry
- **Junction table**: Rejected - overkill for 1:N relationship

**Migration Steps**:
```csharp
// Add columns
migrationBuilder.AddColumn<int?>(
    name: "MadeById",
    table: "ShotRecords",
    nullable: true);

migrationBuilder.AddColumn<int?>(
    name: "MadeForId", 
    table: "ShotRecords",
    nullable: true);

migrationBuilder.AddColumn<decimal?>(
    name: "PreinfusionTime",
    table: "ShotRecords",
    type: "decimal(5,2)",
    nullable: true);

// Add foreign keys
migrationBuilder.CreateIndex(
    name: "IX_ShotRecords_MadeById",
    table: "ShotRecords",
    column: "MadeById");

migrationBuilder.CreateIndex(
    name: "IX_ShotRecords_MadeForId",
    table: "ShotRecords", 
    column: "MadeForId");

migrationBuilder.AddForeignKey(
    name: "FK_ShotRecords_UserProfiles_MadeById",
    table: "ShotRecords",
    column: "MadeById",
    principalTable: "UserProfiles",
    principalColumn: "Id",
    onDelete: ReferentialAction.Restrict); // IMPORTANT: No cascade delete
```

**Best Practices**:
- Always add indexes for foreign keys (query performance)
- Use `ReferentialAction.Restrict` to prevent accidental data loss
- Test migration on copy of production database first
- Include rollback migration in deployment plan
- Validate existing data integrity after migration

**Documentation**: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/

---

### 4. Form Pre-Population Strategy

**Decision**: Load preferences in `OnMounted`, apply before rendering form controls

**Rationale**:
- OnMounted executes once when component initializes - ideal for setup
- Preferences.Get is synchronous and fast (<1ms)
- State update triggers re-render with pre-populated values
- Separates concerns: preferences loading vs. form rendering

**Implementation Pattern**:
```csharp
protected override void OnMounted()
{
    var state = State;
    
    // Load last-used values
    if (Preferences.ContainsKey("LastUsedBeanId"))
        state.SelectedBeanId = Preferences.Get("LastUsedBeanId", 0);
    
    if (Preferences.ContainsKey("LastUsedMadeById"))
        state.SelectedMakerId = Preferences.Get("LastUsedMadeById", 0);
    
    // ... load other preferences ...
    
    SetState(state);
    
    base.OnMounted();
}

private async Task SaveShot()
{
    // Save shot via service...
    
    // Persist preferences for next time
    Preferences.Set("LastUsedBeanId", State.SelectedBeanId);
    Preferences.Set("LastUsedMadeById", State.SelectedMakerId);
    // ... save other preferences ...
}
```

**Best Practices**:
- Only load preferences on new shot (not when editing existing shot)
- Save preferences immediately after successful save (not on every field change)
- Use defensive coding - validate preference values before applying
- Consider adding "Clear Defaults" option in settings

---

### 5. Preinfusion Time Validation

**Decision**: Validate as non-negative decimal with range 0-60 seconds, client-side and service-side

**Rationale**:
- Preinfusion >60s is unrealistic for espresso (prevents data entry errors)
- Decimal type (5,2) allows precision to 0.01s while preventing extreme values
- Client-side validation provides immediate feedback
- Service-side validation prevents bypassing client validation

**Validation Rules**:
```csharp
// CreateShotDto/UpdateShotDto validation
[Range(0, 60, ErrorMessage = "Preinfusion time must be between 0 and 60 seconds")]
public decimal? PreinfusionTime { get; set; }

// UI validation in ShotLoggingPage
private bool ValidatePreinfusionTime(decimal? value)
{
    if (!value.HasValue) return true; // Optional field
    if (value < 0 || value > 60)
    {
        await FeedbackService.ShowError("Preinfusion time must be between 0 and 60 seconds");
        return false;
    }
    return true;
}
```

**Best Practices**:
- Use data annotations for service-level validation
- Provide clear error messages with valid ranges
- Keyboard type: numeric with decimal point on mobile
- Show example value as placeholder (e.g., "5.5")

---

### 6. Activity Feed Display Strategy

**Decision**: Extend ShotRecordCard component to display maker/recipient with avatar icons

**Rationale**:
- Existing ShotRecordCard already displays shot metadata in compact format
- UserProfile includes AvatarPath - leverage for visual identification
- Consistent with existing design patterns in the app
- Maintains card-based list UI paradigm

**Display Pattern**:
```csharp
// In ShotRecordCard.Render()
HStack(
    // Existing shot data...
    
    // New: Maker/Recipient section
    VStack(
        HStack(
            Image()
                .Source(Props.Shot.MadeBy?.AvatarPath ?? "default_avatar.png")
                .WidthRequest(24)
                .HeightRequest(24)
                .Aspect(Aspect.AspectFill)
                .Clip(new RoundRectangle { CornerRadius = 12 }),
            Label($"By: {Props.Shot.MadeBy?.Name ?? "Unknown"}")
                .FontSize(12)
                .TextColor(Colors.Gray)
        ).When(() => Props.Shot.MadeBy != null),
        
        HStack(
            Image()
                .Source(Props.Shot.MadeFor?.AvatarPath ?? "default_avatar.png")
                .WidthRequest(24)
                .HeightRequest(24)
                .Aspect(Aspect.AspectFill)
                .Clip(new RoundRectangle { CornerRadius = 12 }),
            Label($"For: {Props.Shot.MadeFor?.Name ?? "Unknown"}")
                .FontSize(12)
                .TextColor(Colors.Gray)
        ).When(() => Props.Shot.MadeFor != null)
    ),
    
    // Preinfusion time
    Label($"Preinfusion: {Props.Shot.PreinfusionTime:F1}s")
        .FontSize(12)
        .TextColor(Colors.Gray)
        .When(() => Props.Shot.PreinfusionTime.HasValue)
)
```

**Best Practices**:
- Use `.When()` to conditionally render sections only when data present
- Keep text labels concise for compact display
- Use icon + text pattern for visual hierarchy
- Maintain consistent spacing/alignment with existing card content

---

## Technology Stack Summary

| Component | Technology | Version | Purpose |
|-----------|-----------|---------|---------|
| Preferences Storage | Microsoft.Maui.Storage.Preferences | Built-in | Last-used values persistence |
| Database | SQLite via EF Core | 9.x | Shot/user data storage |
| ORM | Entity Framework Core | 9.x | Data access and migrations |
| UI Framework | MauiReactor | 4.x | Reactive UI components |
| Feedback/Toasts | UXDivers.Popups | 1.x | User feedback (NON-NEGOTIABLE) |
| Testing | xUnit, FluentAssertions, Moq | Latest | Unit/integration tests |

## Performance Considerations

- **Preferences Access**: O(1) synchronous reads/writes, <1ms latency
- **User List Loading**: Single query with 100 users ~50ms, acceptable for form load
- **Form Pre-Population**: Synchronous preference reads sequential, total <10ms for all fields
- **Database Indexes**: New FK indexes maintain query performance for joins

## Accessibility Notes

- Picker controls are inherently keyboard navigable and screen-reader friendly
- Add `.SemanticProperties()` for "Made By" and "Made For" pickers
- Ensure avatar images have alt text via `.SemanticProperties()`
- Maintain 44x44px minimum touch target for picker controls

## Security & Privacy

- No sensitive data in preferences (only IDs)
- User data already subject to existing privacy policies
- Historical shots maintain user associations even if user deleted (audit trail)

---

**Phase 0 Complete**: All technical unknowns resolved. Ready for Phase 1 (data model and contracts design).
