# Quickstart: Shot Maker, Recipient, and Preinfusion Tracking

**Feature**: 001-shot-tracking  
**Date**: 2025-12-03  
**Phase**: 1 - Design & Contracts  
**Audience**: Developers implementing this feature

## Overview

This quickstart guide provides step-by-step instructions for implementing shot maker, recipient, and preinfusion tracking in BaristaNotes. It covers database migrations, service layer updates, UI modifications, and testing.

---

## Prerequisites

- BaristaNotes repository cloned and building successfully
- .NET 9.0 SDK installed
- Understanding of Entity Framework Core migrations
- Understanding of MauiReactor reactive UI patterns
- Understanding of UXDivers.Popups for user feedback

---

## Implementation Checklist

### Phase 1: Database & Model Updates

- [ ] **1.1**: Add new fields to `ShotRecord` entity
  - [ ] Add `MadeById` (int?, FK to UserProfile)
  - [ ] Add `MadeForId` (int?, FK to UserProfile)
  - [ ] Add `PreinfusionTime` (decimal?)
  - [ ] Add navigation properties: `MadeBy`, `MadeFor`

- [ ] **1.2**: Update `BaristaNotesContext` configuration
  - [ ] Configure FK relationship for `MadeById` with `ReferentialAction.Restrict`
  - [ ] Configure FK relationship for `MadeForId` with `ReferentialAction.Restrict`
  - [ ] Add indexes for `MadeById` and `MadeForId`

- [ ] **1.3**: Create and apply EF Core migration
  - [ ] Run: `dotnet ef migrations add AddShotMakerRecipientPreinfusion`
  - [ ] Review generated migration SQL
  - [ ] Run: `dotnet ef database update`
  - [ ] Verify columns added to SQLite database

### Phase 2: DTO & Service Updates

- [ ] **2.1**: Update `CreateShotDto`
  - [ ] Add `MadeById` property (int?)
  - [ ] Add `MadeForId` property (int?)
  - [ ] Add `PreinfusionTime` property with `[Range(0, 60)]` validation

- [ ] **2.2**: Update `UpdateShotDto`
  - [ ] Add same three properties as CreateShotDto

- [ ] **2.3**: Update `ShotRecordDto`
  - [ ] Add `MadeBy` property (SimpleUserDto?)
  - [ ] Add `MadeFor` property (SimpleUserDto?)
  - [ ] Add `PreinfusionTime` property (decimal?)

- [ ] **2.4**: Create `SimpleUserDto`
  - [ ] Properties: `Id`, `Name`, `AvatarPath`

- [ ] **2.5**: Update `ShotService`
  - [ ] Modify `CreateShotAsync` to map and save new fields
  - [ ] Modify `UpdateShotAsync` to map and update new fields
  - [ ] Update `GetShotByIdAsync` to include `.Include(s => s.MadeBy)` and `.Include(s => s.MadeFor)`
  - [ ] Update `GetAllShotsAsync` to include maker/recipient in query
  - [ ] Implement (future) `GetShotsByMakerAsync` method
  - [ ] Implement (future) `GetShotsByRecipientAsync` method

- [ ] **2.6**: Create `PreferencesService`
  - [ ] Implement `IPreferencesService` interface
  - [ ] Define constants for preference keys
  - [ ] Implement `SaveLastUsedShotValues` method
  - [ ] Implement `LoadLastUsedShotValues` method
  - [ ] Register service in DI container (`MauiProgram.cs`)

### Phase 3: UI Updates

- [ ] **3.1**: Update `ShotLoggingPage` state
  - [ ] Add `List<SimpleUserDto> Users` to state
  - [ ] Add `SimpleUserDto? SelectedMaker` to state
  - [ ] Add `SimpleUserDto? SelectedRecipient` to state
  - [ ] Add `decimal? PreinfusionTime` to state

- [ ] **3.2**: Update `ShotLoggingPage.OnMounted`
  - [ ] Load users via `UserService.GetAllUsersAsync()`
  - [ ] Check if editing existing shot (Props.ShotId present)
  - [ ] If new shot: load preferences via `PreferencesService.LoadLastUsedShotValues()`
  - [ ] If editing: load shot data (skip preferences)

- [ ] **3.3**: Add user picker controls to `ShotLoggingPage.Render`
  - [ ] Add "Made By" Picker below bean selection
  - [ ] Add "Made For" Picker below "Made By"
  - [ ] Bind to `State.Users` as ItemsSource
  - [ ] Set ItemDisplayBinding to `user => user.Name`
  - [ ] Handle SelectedItemChanged to update state

- [ ] **3.4**: Add preinfusion time field to `ShotLoggingPage.Render`
  - [ ] Add Entry control below "Actual Time" field
  - [ ] Set Keyboard to `Keyboard.Numeric`
  - [ ] Set Placeholder to "e.g., 5.5"
  - [ ] Bind Text to `State.PreinfusionTime?.ToString()`
  - [ ] Handle TextChanged to update state (with decimal parsing)

- [ ] **3.5**: Update save logic in `ShotLoggingPage`
  - [ ] Map `SelectedMaker.Id` to `CreateShotDto.MadeById`
  - [ ] Map `SelectedRecipient.Id` to `CreateShotDto.MadeForId`
  - [ ] Map `PreinfusionTime` to DTO
  - [ ] Call `PreferencesService.SaveLastUsedShotValues(dto)` after successful save
  - [ ] Show success toast via `FeedbackService.ShowToast`

- [ ] **3.6**: Update `ShotRecordCard` component
  - [ ] Add maker display: Avatar + "By: {name}" (when present)
  - [ ] Add recipient display: Avatar + "For: {name}" (when present)
  - [ ] Add preinfusion display: "Preinfusion: {time}s" (when present)
  - [ ] Use `.When()` to conditionally render sections

### Phase 4: Testing

- [ ] **4.1**: Unit test `PreferencesService`
  - [ ] Test saving preferences stores correct values
  - [ ] Test loading preferences returns saved values
  - [ ] Test loading with no saved preferences returns defaults
  - [ ] Test clearing preferences removes all keys

- [ ] **4.2**: Unit test `ShotService` updates
  - [ ] Test `CreateShotAsync` with maker/recipient/preinfusion saves correctly
  - [ ] Test `CreateShotAsync` validates preinfusion range (0-60)
  - [ ] Test `CreateShotAsync` throws EntityNotFoundException for invalid user IDs
  - [ ] Test `UpdateShotAsync` updates new fields
  - [ ] Test `GetShotByIdAsync` includes maker/recipient in result

- [ ] **4.3**: Integration test shot creation flow
  - [ ] Create users in test database
  - [ ] Create shot with maker, recipient, and preinfusion time
  - [ ] Verify shot saved with all fields
  - [ ] Verify preferences saved
  - [ ] Create second shot, verify preferences loaded
  - [ ] Verify activity feed displays maker/recipient/preinfusion

- [ ] **4.4**: Manual UI testing
  - [ ] Test user picker displays all users alphabetically
  - [ ] Test selecting maker and recipient
  - [ ] Test selecting same user for both (valid scenario)
  - [ ] Test entering valid preinfusion time (e.g., 5.5)
  - [ ] Test entering invalid preinfusion time (e.g., -5, 70)
  - [ ] Test leaving maker/recipient/preinfusion empty (valid)
  - [ ] Test saving shot and verifying preferences persist
  - [ ] Test app restart and verify preferences loaded
  - [ ] Test editing shot changes maker/recipient/preinfusion
  - [ ] Test activity feed displays new information correctly

---

## Code Examples

### 1. ShotRecord Entity Update

```csharp
// File: BaristaNotes/Models/ShotRecord.cs

public class ShotRecord
{
    // Existing properties...
    
    // NEW properties
    public int? MadeById { get; set; }
    public int? MadeForId { get; set; }
    public decimal? PreinfusionTime { get; set; }
    
    // NEW navigation properties
    public UserProfile? MadeBy { get; set; }
    public UserProfile? MadeFor { get; set; }
}
```

### 2. DbContext Configuration

```csharp
// File: BaristaNotes/Data/BaristaNotesContext.cs

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    // Existing configurations...
    
    // NEW: Configure maker relationship
    modelBuilder.Entity<ShotRecord>()
        .HasOne(s => s.MadeBy)
        .WithMany()
        .HasForeignKey(s => s.MadeById)
        .OnDelete(DeleteBehavior.Restrict);
    
    // NEW: Configure recipient relationship
    modelBuilder.Entity<ShotRecord>()
        .HasOne(s => s.MadeFor)
        .WithMany()
        .HasForeignKey(s => s.MadeForId)
        .OnDelete(DeleteBehavior.Restrict);
    
    // NEW: Add indexes
    modelBuilder.Entity<ShotRecord>()
        .HasIndex(s => s.MadeById);
    
    modelBuilder.Entity<ShotRecord>()
        .HasIndex(s => s.MadeForId);
}
```

### 3. PreferencesService Implementation

```csharp
// File: BaristaNotes/Services/PreferencesService.cs

public class PreferencesService : IPreferencesService
{
    private const string KEY_LAST_BEAN_ID = "LastUsedBeanId";
    private const string KEY_LAST_MADE_BY_ID = "LastUsedMadeById";
    private const string KEY_LAST_MADE_FOR_ID = "LastUsedMadeForId";
    private const string KEY_LAST_PREINFUSION_TIME = "LastUsedPreinfusionTime";
    // ... other constants
    
    public void SaveLastUsedShotValues(CreateShotDto dto)
    {
        if (dto.BeanId.HasValue)
            Preferences.Set(KEY_LAST_BEAN_ID, dto.BeanId.Value);
        
        if (dto.MadeById.HasValue)
            Preferences.Set(KEY_LAST_MADE_BY_ID, dto.MadeById.Value);
        
        if (dto.MadeForId.HasValue)
            Preferences.Set(KEY_LAST_MADE_FOR_ID, dto.MadeForId.Value);
        
        if (dto.PreinfusionTime.HasValue)
            Preferences.Set(KEY_LAST_PREINFUSION_TIME, (double)dto.PreinfusionTime.Value);
        
        // ... save other values
    }
    
    public CreateShotDto LoadLastUsedShotValues()
    {
        return new CreateShotDto
        {
            BeanId = Preferences.ContainsKey(KEY_LAST_BEAN_ID) 
                ? Preferences.Get(KEY_LAST_BEAN_ID, 0) : null,
            
            MadeById = Preferences.ContainsKey(KEY_LAST_MADE_BY_ID)
                ? Preferences.Get(KEY_LAST_MADE_BY_ID, 0) : null,
            
            MadeForId = Preferences.ContainsKey(KEY_LAST_MADE_FOR_ID)
                ? Preferences.Get(KEY_LAST_MADE_FOR_ID, 0) : null,
            
            PreinfusionTime = Preferences.ContainsKey(KEY_LAST_PREINFUSION_TIME)
                ? (decimal)Preferences.Get(KEY_LAST_PREINFUSION_TIME, 0.0) : null,
            
            // ... load other values
            Timestamp = DateTimeOffset.Now
        };
    }
    
    public void ClearAllPreferences()
    {
        Preferences.Clear();
    }
    
    public bool HasPreference(string key)
    {
        return Preferences.ContainsKey(key);
    }
}
```

### 4. ShotLoggingPage - User Pickers

```csharp
// File: BaristaNotes/Pages/ShotLoggingPage.cs

public override VisualNode Render()
{
    return ContentPage("Log Shot",
        VStack(spacing: 20,
            // Existing fields...
            
            // NEW: Made By picker
            VStack(spacing: 5,
                Label("Made By")
                    .FontSize(14)
                    .FontAttributes(Microsoft.Maui.FontAttributes.Bold),
                
                Picker()
                    .Title("Select barista")
                    .ItemsSource(State.Users, user => user.Id)
                    .ItemDisplayBinding(user => user.Name)
                    .SelectedItem(State.SelectedMaker)
                    .OnSelectedItemChanged(user => SetState(s => s.SelectedMaker = user))
                    .HeightRequest(44)
            ),
            
            // NEW: Made For picker
            VStack(spacing: 5,
                Label("Made For")
                    .FontSize(14)
                    .FontAttributes(Microsoft.Maui.FontAttributes.Bold),
                
                Picker()
                    .Title("Select recipient")
                    .ItemsSource(State.Users, user => user.Id)
                    .ItemDisplayBinding(user => user.Name)
                    .SelectedItem(State.SelectedRecipient)
                    .OnSelectedItemChanged(user => SetState(s => s.SelectedRecipient = user))
                    .HeightRequest(44)
            ),
            
            // Extraction time field...
            
            // NEW: Preinfusion time field (below extraction time)
            VStack(spacing: 5,
                Label("Preinfusion Time (seconds)")
                    .FontSize(14)
                    .FontAttributes(Microsoft.Maui.FontAttributes.Bold),
                
                Entry()
                    .Keyboard(Keyboard.Numeric)
                    .Placeholder("e.g., 5.5")
                    .Text(State.PreinfusionTime?.ToString() ?? "")
                    .OnTextChanged(text => 
                    {
                        if (decimal.TryParse(text, out var value))
                            SetState(s => s.PreinfusionTime = value);
                        else
                            SetState(s => s.PreinfusionTime = null);
                    })
                    .HeightRequest(44)
            ),
            
            // Save button...
        )
    );
}
```

### 5. ShotRecordCard - Display Maker/Recipient

```csharp
// File: BaristaNotes/Components/ShotRecordCard.cs

public override VisualNode Render()
{
    return Border(
        VStack(spacing: 10,
            // Existing shot info...
            
            // NEW: Maker and recipient section
            VStack(spacing: 5,
                HStack(spacing: 8,
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
                
                HStack(spacing: 8,
                    Image()
                        .Source(Props.Shot.MadeFor?.AvatarPath ?? "default_avatar.png")
                        .WidthRequest(24)
                        .HeightRequest(24)
                        .Aspect(Aspect.AspectFill)
                        .Clip(new RoundRectangle { CornerRadius = 12 }),
                    
                    Label($"For: {Props.Shot.MadeFor?.Name ?? "Unknown"}")
                        .FontSize(12)
                        .TextColor(Colors.Gray)
                ).When(() => Props.Shot.MadeFor != null),
                
                Label($"Preinfusion: {Props.Shot.PreinfusionTime:F1}s")
                    .FontSize(12)
                    .TextColor(Colors.Gray)
                    .When(() => Props.Shot.PreinfusionTime.HasValue)
            )
        )
    );
}
```

---

## Common Issues & Solutions

### Issue: Migration fails with foreign key constraint error

**Solution**: Ensure existing shot records have NULL values for new FKs. Migration should add nullable columns first, then add constraints.

### Issue: Preferences not persisting after app restart

**Solution**: Verify `Preferences.Set()` is called AFTER successful save, not before. Check platform-specific storage permissions.

### Issue: User picker shows deleted users

**Solution**: Filter users in service layer: `.Where(u => !u.IsDeleted)` before returning to UI.

### Issue: Preinfusion time validation not working

**Solution**: Validate BOTH client-side (immediate feedback) and service-side (data integrity). Use `[Range(0, 60)]` on DTO.

### Issue: Toast not appearing after save

**Solution**: Ensure using `UXDivers.Popups` (NON-NEGOTIABLE per constitution). Call `await FeedbackService.ShowToast()` and wait for completion before navigating.

---

## Performance Tips

1. **Load users once**: Cache user list in state, don't reload on every render
2. **Index foreign keys**: Ensure migration creates indexes for MadeById/MadeForId
3. **Lazy load preferences**: Only load on new shot, not every page render
4. **Batch preference writes**: Save all preferences in single transaction after shot saved

---

## Next Steps

After completing this implementation:

1. Run full test suite to ensure no regressions
2. Test on physical devices (iOS and Android)
3. Verify accessibility with screen reader
4. Profile memory usage with large user lists
5. Document any constitutional exceptions in plan.md
6. Create pull request with reference to spec and plan

---

## References

- [.NET MAUI Preferences API](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/preferences)
- [Entity Framework Core Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [MauiReactor Picker Component](https://adospace.gitbook.io/mauireactor/components/picker)
- [UXDivers.Popups Documentation](https://github.com/UXDivers/Popups)
- Feature Spec: `/specs/001-shot-tracking/spec.md`
- Implementation Plan: `/specs/001-shot-tracking/plan.md`
- Data Model: `/specs/001-shot-tracking/data-model.md`
- Service Contracts: `/specs/001-shot-tracking/contracts/service-contracts.md`

---

**Quickstart Complete**: Developers have step-by-step implementation guide. Ready for task breakdown in Phase 2.
