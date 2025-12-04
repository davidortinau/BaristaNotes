# Research: Edit and Delete Shots Feature

**Feature**: Edit and Delete Shots from Activity Page  
**Date**: 2025-12-03  
**Status**: Complete

## Research Tasks

### 1. MVU Pattern for Edit Forms in Maui Reactor

**Question**: How should edit forms be structured in MVU pattern with Maui Reactor?

**Decision**: Edit forms use local component state with explicit update messages

**Rationale**:
- MVU pattern uses Messages to trigger state updates
- Edit forms need local state for form fields (not global app state)
- Use `State<T>` property for each editable field
- Create Message enum for each field change and save/cancel actions
- Example pattern:
  ```csharp
  enum Message
  {
      ActualTimeChanged,
      ActualOutputChanged,
      RatingChanged,
      DrinkTypeChanged,
      Save,
      Cancel
  }
  
  class State
  {
      public int ShotId { get; set; }
      public decimal ActualTime { get; set; }
      public decimal ActualOutput { get; set; }
      public int? Rating { get; set; }
      public string DrinkType { get; set; }
      public bool IsSaving { get; set; }
  }
  ```

**Alternatives Considered**:
- Global app state: Rejected - edit form state is temporary and doesn't need to be shared
- Direct property binding: Rejected - breaks MVU pattern, harder to test
- MVVM ViewModels: Rejected - not compatible with Reactor's MVU architecture

**References**:
- Maui Reactor Samples: https://github.com/adospace/mauireactor-samples
- Existing ShotLoggingPage.cs in codebase follows this pattern

---

### 2. Delete Confirmation with UXDivers.Popups.Maui

**Question**: How to implement delete confirmation using UXDivers.Popups.Maui in Maui Reactor?

**Decision**: Use scaffolded RxPopupPage component with confirmation content

**Rationale**:
- UXDivers.Popups requires scaffolding to work with Maui Reactor (wraps native controls)
- App already has UXDivers integrated (Toast component exists)
- Confirmation pattern: Create RxPopupPage with two buttons (Confirm/Cancel)
- Use PopupService.ShowPopupAsync() to display modal
- Return result via Task<bool> or Message dispatch
- Example structure:
  ```csharp
  new RxPopupPage()
  {
      new VStack
      {
          new Label("Delete this shot?"),
          new Label("This action cannot be undone"),
          new HStack
          {
              new Button("Cancel").OnClicked(() => PopupService.PopAsync()),
              new Button("Delete").OnClicked(() => {
                  PopupService.PopAsync();
                  // Dispatch delete message
              })
          }
      }
  }
  .BackgroundColor(Colors.DarkBackground)
  ```

**Alternatives Considered**:
- DisplayAlert: Rejected - not styled, doesn't match app theme
- The49.Maui.BottomSheet: Currently used but user explicitly wants UXDivers
- Custom modal overlay: Rejected - reinventing wheel, UXDivers already integrated

**References**:
- UXDivers docs: https://github.com/UXDivers/uxd-popups/blob/main/docs/Home.md
- Existing FeedbackService uses UXDivers Toast
- Existing popup scaffolding in Integrations/UXDivers.Popups/

---

### 3. Swipe Actions vs Context Menu for Edit/Delete Access

**Question**: What's the best UI pattern for accessing edit/delete actions on shot cards?

**Decision**: Use SwipeView with platform-specific swipe directions (iOS: right-to-left, Android: left-to-right)

**Rationale**:
- MAUI provides built-in SwipeView control
- Platform conventions: iOS users expect swipe-to-delete, Android expects swipe menus
- SwipeView supports both swipe-to-execute and swipe-to-reveal patterns
- Can show both edit and delete actions simultaneously
- Touch targets automatically sized correctly
- Accessible via screen readers
- Example structure:
  ```csharp
  new SwipeView()
  {
      new SwipeItems()
          .Mode(SwipeMode.Reveal)
          .Add(new SwipeItem("Edit")
              .BackgroundColor(Colors.Blue)
              .OnInvoked(() => SetState(s => s.NavigateToEdit = true)))
          .Add(new SwipeItem("Delete")
              .BackgroundColor(Colors.Red)
              .OnInvoked(() => SetState(s => s.ShowDeleteConfirmation = true))),
      
      // Existing shot card content
      new ShotCardContent()
  }
  ```

**Alternatives Considered**:
- Long press context menu: Rejected - less discoverable, requires holding
- Explicit menu buttons: Rejected - clutters UI, takes more space
- Slide-out action panel: Rejected - custom implementation, more complex
- Tap-to-expand: Rejected - requires multiple interactions

**References**:
- MAUI SwipeView docs: https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/swipeview
- Platform design guidelines for swipe gestures
- Existing app uses native controls per user requirement

---

### 4. Data Validation for Edit Operations

**Question**: What validation is needed for editing shot records?

**Decision**: Client-side validation with service-level enforcement

**Rationale**:
- Validate before allowing save to prevent database errors
- Show inline validation errors immediately (better UX)
- Validation rules:
  - ActualTime: Must be > 0 seconds, < 999 seconds (reasonable espresso range)
  - ActualOutput: Must be > 0 grams, < 200 grams (reasonable espresso range)
  - Rating: Must be 1-5 (if provided, nullable allowed)
  - DrinkType: Cannot be empty string
- Immutable fields (timestamp, bean, grind setting, dose in) readonly in edit form
- Example validation:
  ```csharp
  private bool ValidateFields(State state, out List<string> errors)
  {
      errors = new List<string>();
      
      if (state.ActualTime <= 0 || state.ActualTime > 999)
          errors.Add("Shot time must be between 0 and 999 seconds");
          
      if (state.ActualOutput <= 0 || state.ActualOutput > 200)
          errors.Add("Output weight must be between 0 and 200 grams");
          
      if (state.Rating.HasValue && (state.Rating < 1 || state.Rating > 5))
          errors.Add("Rating must be between 1 and 5");
          
      if (string.IsNullOrWhiteSpace(state.DrinkType))
          errors.Add("Drink type is required");
          
      return errors.Count == 0;
  }
  ```

**Alternatives Considered**:
- Database constraints only: Rejected - poor UX, cryptic error messages
- FluentValidation library: Rejected - adds dependency, overkill for simple rules
- No validation: Rejected - violates constitution quality standards

**References**:
- Existing validation in CreateShotDto validation (ShotService.cs)
- MAUI data validation patterns

---

### 5. Best Practices for Delete Operations with Sync

**Question**: How should deletes work with CoreSync.Sqlite for future sync capability?

**Decision**: Soft delete with IsDeleted flag (already implemented in ShotRecord model)

**Rationale**:
- ShotRecord model already has `IsDeleted` flag for CoreSync
- Soft delete preserves sync history and prevents conflicts
- DeleteShotAsync should set IsDeleted=true, update LastModifiedAt
- UI filters out IsDeleted=true records
- Actual database cleanup happens during sync reconciliation
- Example implementation:
  ```csharp
  public async Task DeleteShotAsync(int id)
  {
      var shot = await _shotRepository.GetByIdAsync(id);
      if (shot == null)
          throw new NotFoundException($"Shot with ID {id} not found");
          
      shot.IsDeleted = true;
      shot.LastModifiedAt = DateTimeOffset.Now;
      await _shotRepository.UpdateAsync(shot);
  }
  ```

**Alternatives Considered**:
- Hard delete (DELETE FROM): Rejected - breaks sync, can't track deletions across devices
- Archive table: Rejected - more complex, soft delete sufficient
- Cascade delete: Already handled by EF Core relationships

**References**:
- CoreSync.Sqlite documentation (soft delete pattern)
- Existing ShotRecord.IsDeleted field
- Existing repository patterns in codebase

---

## Summary

All research tasks complete. Key decisions:

1. **MVU Edit Forms**: Use local component state with Message enum for updates
2. **Delete Confirmation**: UXDivers RxPopupPage with styled confirmation dialog
3. **UI Access Pattern**: SwipeView with platform-specific swipe directions
4. **Validation**: Client-side with inline errors, service-level enforcement
5. **Delete Strategy**: Soft delete using existing IsDeleted flag for sync compatibility

No additional research or unknowns remain. Ready to proceed to Phase 1: Design & Contracts.
