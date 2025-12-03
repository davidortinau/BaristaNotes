# Quickstart Guide: Edit and Delete Shots

**Feature**: Edit and Delete Shots from Activity Page  
**Date**: 2025-12-03  
**For**: Developers implementing this feature

## Overview

This guide walks you through implementing edit and delete functionality for shot records in the BaristaNotes app. The implementation follows the MVU (Model-View-Update) pattern using Maui Reactor.

**Estimated Time**: 4-6 hours  
**Difficulty**: Intermediate  
**Prerequisites**: Familiarity with Maui Reactor, Entity Framework Core, MVU pattern

---

## Implementation Checklist

### Phase 1: Service Layer (Core Business Logic)

- [ ] Create `UpdateShotDto.cs` in `BaristaNotes.Core/Services/DTOs/`
- [ ] Add `UpdateShotAsync` method signature to `IShotService.cs`
- [ ] Add `DeleteShotAsync` method signature to `IShotService.cs`
- [ ] Implement `UpdateShotAsync` in `ShotService.cs` with validation
- [ ] Implement `DeleteShotAsync` in `ShotService.cs` (soft delete)
- [ ] Write unit tests for both methods in `ShotServiceTests.cs`
- [ ] Write integration tests in `ShotDatabaseTests.cs`
- [ ] Run tests and verify all pass

### Phase 2: UI Components (Maui Reactor MVU)

- [ ] Create `EditShotPage.cs` in `BaristaNotes/Pages/`
- [ ] Implement EditShotPage state and messages
- [ ] Add form fields for editable properties
- [ ] Add validation UI and error display
- [ ] Create delete confirmation popup using UXDivers
- [ ] Update `ActivityFeedPage.cs` to add SwipeView actions
- [ ] Wire up navigation to EditShotPage
- [ ] Wire up delete confirmation flow

### Phase 3: Integration & Testing

- [ ] Test edit flow end-to-end (Activity → Edit → Save → Back)
- [ ] Test delete flow end-to-end (Activity → Swipe → Confirm → Removed)
- [ ] Test validation errors display correctly
- [ ] Test cancel operations (edit and delete)
- [ ] Test visual feedback (toasts) on success/error
- [ ] Test accessibility (touch targets, screen reader)
- [ ] Test performance (edit load <500ms, save <2s, delete <1s)

---

## Step-by-Step Implementation

### Step 1: Create UpdateShotDto

**File**: `BaristaNotes.Core/Services/DTOs/UpdateShotDto.cs`

```csharp
namespace BaristaNotes.Core.Services.DTOs;

public class UpdateShotDto
{
    public decimal? ActualTime { get; set; }
    public decimal? ActualOutput { get; set; }
    public int? Rating { get; set; }
    public string DrinkType { get; set; } = string.Empty;
}
```

**Why**: Encapsulates only editable fields for update operations. Immutable fields (timestamp, bean, grind settings) intentionally excluded.

---

### Step 2: Extend IShotService Interface

**File**: `BaristaNotes.Core/Services/IShotService.cs`

Add these method signatures:

```csharp
Task<ShotRecordDto> UpdateShotAsync(int id, UpdateShotDto dto);
Task DeleteShotAsync(int id);
```

**Why**: Defines service contract. Implementation comes next.

---

### Step 3: Implement UpdateShotAsync

**File**: `BaristaNotes.Core/Services/ShotService.cs`

```csharp
public async Task<ShotRecordDto> UpdateShotAsync(int id, UpdateShotDto dto)
{
    // Validate DTO
    ValidateUpdateShot(dto);
    
    // Get existing shot
    var shot = await _shotRepository.GetByIdAsync(id);
    if (shot == null || shot.IsDeleted)
        throw new NotFoundException($"Shot with ID {id} not found");
    
    // Update only editable fields
    if (dto.ActualTime.HasValue)
        shot.ActualTime = dto.ActualTime.Value;
    
    if (dto.ActualOutput.HasValue)
        shot.ActualOutput = dto.ActualOutput.Value;
    
    shot.Rating = dto.Rating;  // Can be null
    shot.DrinkType = dto.DrinkType;
    shot.LastModifiedAt = DateTimeOffset.Now;
    
    // Persist
    await _shotRepository.UpdateAsync(shot);
    
    // Return updated DTO
    return MapToDto(shot);
}

private void ValidateUpdateShot(UpdateShotDto dto)
{
    var errors = new List<string>();
    
    if (dto.ActualTime.HasValue && (dto.ActualTime <= 0 || dto.ActualTime > 999))
        errors.Add("Shot time must be between 0 and 999 seconds");
    
    if (dto.ActualOutput.HasValue && (dto.ActualOutput <= 0 || dto.ActualOutput > 200))
        errors.Add("Output weight must be between 0 and 200 grams");
    
    if (dto.Rating.HasValue && (dto.Rating < 1 || dto.Rating > 5))
        errors.Add("Rating must be between 1 and 5 stars");
    
    if (string.IsNullOrWhiteSpace(dto.DrinkType))
        errors.Add("Drink type is required");
    
    if (errors.Any())
        throw new ValidationException(errors);
}
```

**Why**: Updates only mutable fields, validates input, maintains sync metadata.

---

### Step 4: Implement DeleteShotAsync

**File**: `BaristaNotes.Core/Services/ShotService.cs`

```csharp
public async Task DeleteShotAsync(int id)
{
    var shot = await _shotRepository.GetByIdAsync(id);
    if (shot == null || shot.IsDeleted)
        throw new NotFoundException($"Shot with ID {id} not found");
    
    shot.IsDeleted = true;
    shot.LastModifiedAt = DateTimeOffset.Now;
    
    await _shotRepository.UpdateAsync(shot);
}
```

**Why**: Soft delete preserves sync history. Repository filters out IsDeleted shots automatically.

---

### Step 5: Write Unit Tests

**File**: `BaristaNotes.Tests/Services/ShotServiceTests.cs`

```csharp
[Fact]
public async Task UpdateShotAsync_ValidDto_UpdatesFields()
{
    // Arrange
    var mockRepo = new Mock<IShotRecordRepository>();
    var existingShot = new ShotRecord { Id = 1, DrinkType = "Espresso" };
    mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingShot);
    
    var service = new ShotService(mockRepo.Object, Mock.Of<IPreferencesService>());
    var dto = new UpdateShotDto 
    { 
        ActualTime = 28.5m,
        ActualOutput = 42.0m,
        Rating = 4,
        DrinkType = "Latte"
    };
    
    // Act
    var result = await service.UpdateShotAsync(1, dto);
    
    // Assert
    Assert.Equal(28.5m, existingShot.ActualTime);
    Assert.Equal(42.0m, existingShot.ActualOutput);
    Assert.Equal(4, existingShot.Rating);
    Assert.Equal("Latte", existingShot.DrinkType);
    mockRepo.Verify(r => r.UpdateAsync(existingShot), Times.Once);
}

[Fact]
public async Task UpdateShotAsync_InvalidActualTime_ThrowsValidationException()
{
    // Arrange
    var service = new ShotService(Mock.Of<IShotRecordRepository>(), Mock.Of<IPreferencesService>());
    var dto = new UpdateShotDto 
    { 
        ActualTime = 1000m,  // Invalid: too high
        DrinkType = "Espresso"
    };
    
    // Act & Assert
    await Assert.ThrowsAsync<ValidationException>(() => service.UpdateShotAsync(1, dto));
}

[Fact]
public async Task DeleteShotAsync_ValidId_SoftDeletes()
{
    // Arrange
    var mockRepo = new Mock<IShotRecordRepository>();
    var existingShot = new ShotRecord { Id = 1, IsDeleted = false };
    mockRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existingShot);
    
    var service = new ShotService(mockRepo.Object, Mock.Of<IPreferencesService>());
    
    // Act
    await service.DeleteShotAsync(1);
    
    // Assert
    Assert.True(existingShot.IsDeleted);
    mockRepo.Verify(r => r.UpdateAsync(existingShot), Times.Once);
}
```

**Why**: Verifies business logic correctness before touching UI.

---

### Step 6: Create EditShotPage Component

**File**: `BaristaNotes/Pages/EditShotPage.cs`

```csharp
using MauiReactor;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Pages;

class EditShotPageState
{
    public int ShotId { get; set; }
    public bool IsLoading { get; set; } = true;
    public bool IsSaving { get; set; }
    
    // Original (readonly) fields
    public DateTimeOffset Timestamp { get; set; }
    public string BeanName { get; set; } = string.Empty;
    public string GrindSetting { get; set; } = string.Empty;
    public decimal DoseIn { get; set; }
    
    // Editable fields
    public decimal ActualTime { get; set; }
    public decimal ActualOutput { get; set; }
    public int? Rating { get; set; }
    public string DrinkType { get; set; } = string.Empty;
    
    public List<string> ValidationErrors { get; set; } = new();
}

enum EditShotMessage
{
    Load,
    Loaded,
    ActualTimeChanged,
    ActualOutputChanged,
    RatingChanged,
    DrinkTypeChanged,
    Save,
    Saved,
    Cancel,
    ValidationFailed
}

partial class EditShotPage : Component<EditShotPageState>
{
    [Prop] int ShotId { get; set; }
    
    [Inject] IShotService _shotService;
    [Inject] IFeedbackService _feedbackService;
    
    protected override void OnMounted()
    {
        State.ShotId = ShotId;
        SetState(s => s.IsLoading = true, EditShotMessage.Load);
        base.OnMounted();
    }
    
    public override VisualNode Render()
    {
        return new ContentPage
        {
            new ScrollView
            {
                new VStack(spacing: 16)
                {
                    // Readonly fields
                    new VStack(spacing: 8)
                    {
                        new Label($"Shot from {State.Timestamp:MMM d, yyyy h:mm tt}")
                            .StyleClass("Headline"),
                        new Label($"Bean: {State.BeanName}"),
                        new Label($"Grind: {State.GrindSetting}"),
                        new Label($"Dose: {State.DoseIn}g")
                    },
                    
                    // Editable fields
                    new Entry(State.ActualTime.ToString())
                        .Placeholder("Actual Time (seconds)")
                        .Keyboard(Keyboard.Numeric)
                        .OnTextChanged((sender, args) => 
                        {
                            if (decimal.TryParse(args.NewTextValue, out var val))
                                SetState(s => s.ActualTime = val, EditShotMessage.ActualTimeChanged);
                        }),
                    
                    new Entry(State.ActualOutput.ToString())
                        .Placeholder("Actual Output (grams)")
                        .Keyboard(Keyboard.Numeric)
                        .OnTextChanged((sender, args) =>
                        {
                            if (decimal.TryParse(args.NewTextValue, out var val))
                                SetState(s => s.ActualOutput = val, EditShotMessage.ActualOutputChanged);
                        }),
                    
                    // Rating picker (1-5 stars)
                    new Picker()
                        .Title("Rating")
                        .ItemsSource(new[] { 1, 2, 3, 4, 5 })
                        .SelectedItem(State.Rating ?? 3)
                        .OnSelectedIndexChanged((sender, args) =>
                        {
                            if (args.SelectedIndex >= 0)
                                SetState(s => s.Rating = args.SelectedIndex + 1, EditShotMessage.RatingChanged);
                        }),
                    
                    new Entry(State.DrinkType)
                        .Placeholder("Drink Type")
                        .OnTextChanged((sender, args) =>
                            SetState(s => s.DrinkType = args.NewTextValue, EditShotMessage.DrinkTypeChanged)),
                    
                    // Validation errors
                    RenderValidationErrors(),
                    
                    // Action buttons
                    new HStack(spacing: 16)
                    {
                        new Button("Cancel")
                            .OnClicked(() => SetState(s => s, EditShotMessage.Cancel)),
                        
                        new Button("Save")
                            .IsEnabled(!State.IsSaving)
                            .OnClicked(() => SetState(s => s.IsSaving = true, EditShotMessage.Save))
                    }
                }
                .Padding(16)
            }
        }
        .Title("Edit Shot")
        .OnAppearing(OnPageAppearing);
    }
    
    VisualNode RenderValidationErrors()
    {
        if (!State.ValidationErrors.Any())
            return null;
        
        return new VStack(spacing: 4)
        {
            State.ValidationErrors.Select(err => new Label(err).TextColor(Colors.Red))
        };
    }
    
    async void OnPageAppearing()
    {
        if (State.IsLoading)
            await LoadShotData();
    }
    
    async Task LoadShotData()
    {
        try
        {
            var shot = await _shotService.GetShotByIdAsync(State.ShotId);
            if (shot == null)
            {
                await _feedbackService.ShowErrorAsync("Shot not found");
                return;
            }
            
            SetState(s =>
            {
                s.IsLoading = false;
                s.Timestamp = shot.Timestamp;
                s.BeanName = shot.BeanName ?? "Unknown";
                s.GrindSetting = shot.GrindSetting;
                s.DoseIn = shot.DoseIn;
                s.ActualTime = shot.ActualTime ?? shot.ExpectedTime;
                s.ActualOutput = shot.ActualOutput ?? shot.ExpectedOutput;
                s.Rating = shot.Rating;
                s.DrinkType = shot.DrinkType;
            }, EditShotMessage.Loaded);
        }
        catch (Exception ex)
        {
            await _feedbackService.ShowErrorAsync($"Error loading shot: {ex.Message}");
        }
    }
    
    protected override async Task OnMessageAsync(EditShotMessage message)
    {
        switch (message)
        {
            case EditShotMessage.Save:
                await SaveChanges();
                break;
            
            case EditShotMessage.Cancel:
                await Navigation.PopAsync();
                break;
        }
    }
    
    async Task SaveChanges()
    {
        try
        {
            var dto = new UpdateShotDto
            {
                ActualTime = State.ActualTime,
                ActualOutput = State.ActualOutput,
                Rating = State.Rating,
                DrinkType = State.DrinkType
            };
            
            await _shotService.UpdateShotAsync(State.ShotId, dto);
            await _feedbackService.ShowSuccessAsync("Shot updated successfully");
            await Navigation.PopAsync();
        }
        catch (ValidationException ex)
        {
            SetState(s =>
            {
                s.IsSaving = false;
                s.ValidationErrors = ex.Errors;
            }, EditShotMessage.ValidationFailed);
        }
        catch (Exception ex)
        {
            SetState(s => s.IsSaving = false);
            await _feedbackService.ShowErrorAsync($"Error saving: {ex.Message}");
        }
    }
}
```

**Why**: MVU component manages edit form state, validation, and save logic.

---

### Step 7: Add SwipeView to ActivityFeedPage

**File**: `BaristaNotes/Pages/ActivityFeedPage.cs`

Update the shot card rendering to add SwipeView:

```csharp
VisualNode RenderShotCard(ShotRecordDto shot)
{
    return new SwipeView
    {
        new SwipeItems()
            .Mode(SwipeMode.Reveal)
            .Add(new SwipeItem("Edit")
                .BackgroundColor(Colors.Blue)
                .OnInvoked(() => NavigateToEdit(shot.Id)))
            .Add(new SwipeItem("Delete")
                .BackgroundColor(Colors.Red)
                .OnInvoked(() => ShowDeleteConfirmation(shot.Id))),
        
        // Existing shot card content
        new Border
        {
            new VStack(spacing: 8)
            {
                new Label($"{shot.Timestamp:MMM d, h:mm tt}"),
                new Label($"{shot.BeanName} - {shot.DrinkType}"),
                new Label($"Time: {shot.ActualTime ?? shot.ExpectedTime}s"),
                new Label($"Output: {shot.ActualOutput ?? shot.ExpectedOutput}g"),
                RenderRating(shot.Rating)
            }
            .Padding(16)
        }
        .StyleClass("ShotCard")
    };
}

void NavigateToEdit(int shotId)
{
    Navigation.PushAsync<EditShotPage>(r => r.ShotId = shotId);
}

void ShowDeleteConfirmation(int shotId)
{
    SetState(s =>
    {
        s.ShotToDelete = shotId;
        s.ShowDeleteConfirmation = true;
    });
    
    // Show UXDivers popup (implementation in next step)
}
```

**Why**: Provides intuitive swipe gesture access to edit/delete actions.

---

### Step 8: Create Delete Confirmation Popup

**File**: `BaristaNotes/Pages/ActivityFeedPage.cs` (add to existing component)

```csharp
async void ShowDeleteConfirmation(int shotId)
{
    var confirmPopup = new RxPopupPage
    {
        new VStack(spacing: 16)
        {
            new Label("Delete Shot?")
                .StyleClass("Headline")
                .TextColor(Colors.White),
            
            new Label("This action cannot be undone.")
                .TextColor(Colors.Gray),
            
            new HStack(spacing: 16)
            {
                new Button("Cancel")
                    .OnClicked(async () => await PopupService.PopAsync()),
                
                new Button("Delete")
                    .BackgroundColor(Colors.Red)
                    .OnClicked(async () =>
                    {
                        await PopupService.PopAsync();
                        await DeleteShot(shotId);
                    })
            }
        }
        .Padding(24)
    }
    .BackgroundColor(Colors.DarkBackground);
    
    await PopupService.ShowPopupAsync(confirmPopup);
}

async Task DeleteShot(int shotId)
{
    try
    {
        await _shotService.DeleteShotAsync(shotId);
        await _feedbackService.ShowSuccessAsync("Shot deleted");
        
        // Refresh list
        await LoadShots();
    }
    catch (NotFoundException ex)
    {
        await _feedbackService.ShowErrorAsync("Shot not found");
    }
    catch (Exception ex)
    {
        await _feedbackService.ShowErrorAsync($"Error deleting shot: {ex.Message}");
    }
}
```

**Why**: Styled confirmation prevents accidental deletion, uses UXDivers for consistency.

---

## Testing Guide

### Manual Testing Steps

1. **Edit Flow**:
   - Open Activity Feed
   - Swipe left on a shot card
   - Tap "Edit"
   - Verify form loads with current values
   - Change values and tap Save
   - Verify toast appears: "Shot updated successfully"
   - Verify updated values shown in activity feed

2. **Delete Flow**:
   - Open Activity Feed
   - Swipe left on a shot card
   - Tap "Delete"
   - Verify confirmation popup appears
   - Tap "Delete"
   - Verify toast appears: "Shot deleted"
   - Verify shot removed from activity feed

3. **Validation**:
   - Edit a shot
   - Enter invalid time (e.g., 1000)
   - Tap Save
   - Verify error message appears
   - Correct value and save
   - Verify success

### Performance Testing

Run these commands and verify timing:

```bash
# Time edit form load
Stopwatch.Start(); await GetShotByIdAsync(id); Stopwatch.Stop();
# Should be <500ms

# Time save operation
Stopwatch.Start(); await UpdateShotAsync(id, dto); Stopwatch.Stop();
# Should be <2s

# Time delete operation
Stopwatch.Start(); await DeleteShotAsync(id); Stopwatch.Stop();
# Should be <1s
```

---

## Troubleshooting

### "Shot not found" error on edit
- **Cause**: Shot has IsDeleted=true
- **Fix**: Ensure repository filters IsDeleted in GetByIdAsync

### Swipe actions not working
- **Cause**: SwipeView not properly configured
- **Fix**: Check SwipeMode is set to Reveal and SwipeItems are added

### Validation not showing
- **Cause**: ValidationException not caught in UI
- **Fix**: Wrap SaveChanges in try-catch with ValidationException handler

### Delete confirmation doesn't appear
- **Cause**: UXDivers popup not scaffolded correctly
- **Fix**: Verify RxPopupPage is properly initialized and PopupService registered

---

## Next Steps

After completing this feature:

1. Write comprehensive tests (unit, integration, UI)
2. Test on both iOS and Android devices
3. Verify accessibility with screen reader
4. Test performance on low-end devices
5. Update user documentation
6. Merge feature branch after PR review

---

## Resources

- [Maui Reactor Docs](https://adospace.gitbook.io/mauireactor/)
- [UXDivers Popups](https://github.com/UXDivers/uxd-popups)
- [MVU Pattern Guide](research.md)
- [Service Contracts](contracts/IShotService.md)
- [Data Model](data-model.md)
