# Implementation Tasks: User Profile Image Picker

**Feature**: 003-profile-image-picker  
**Branch**: `003-profile-image-picker`  
**Date**: 2025-12-05

## Overview

This document provides actionable implementation tasks organized by user story to enable independent, test-first development. The feature allows users to select, display, persist, and remove profile images with automatic processing.

---

## Task Summary

- **Total Tasks**: 42
- **Setup Phase**: 3 tasks
- **Foundational Phase**: 5 tasks
- **User Story 1** (Select photo): 10 tasks
- **User Story 2** (Auto-processing display): 6 tasks
- **User Story 3** (Persistence): 4 tasks
- **User Story 4** (Remove photo): 6 tasks
- **Polish Phase**: 8 tasks

---

## Implementation Strategy

### MVP Scope (User Story 1 Only)
For minimum viable product, implement only **User Story 1**:
- User can select photo from device
- Photo displays as circular avatar
- Basic validation and error handling

This provides immediate user value and validates the technical approach before building additional features.

### Incremental Delivery
Each user story is independently testable and can be deployed separately:
1. **US1**: Photo selection and display
2. **US2**: Auto-processing and quality
3. **US3**: Persistence across sessions
4. **US4**: Photo removal capability

---

## Dependencies Between User Stories

```
Setup Phase (T001-T003)
   ↓
Foundational Phase (T004-T008) ← Must complete before any user story
   ↓
   ├─→ US1: Select Photo (T009-T018) ← MVP - No dependencies
   │
   ├─→ US2: Auto-processing (T019-T024) ← Depends on US1
   │
   ├─→ US3: Persistence (T025-T028) ← Depends on US1
   │
   └─→ US4: Remove Photo (T029-T034) ← Depends on US1
   
All User Stories Complete
   ↓
Polish Phase (T035-T042)
```

**Critical Path**: Setup → Foundational → US1 → (US2 || US3 || US4) → Polish

**Parallel Opportunities**:
- US2, US3, and US4 can be developed in parallel after US1 completes
- Within each phase, tasks marked [P] can be executed in parallel

---

## Phase 1: Setup

**Goal**: Initialize project structure and service interfaces

**Independent Test**: Verify all new files exist and compile without errors

### Tasks

- [ ] T001 Create ImageValidationResult enum in BaristaNotes.Core/Services/ImageValidationResult.cs
- [ ] T002 Create ProfileImageUpdateResult DTO in BaristaNotes.Core/Services/DTOs/ProfileImageUpdateResult.cs
- [ ] T003 [P] Add default avatar placeholder image to BaristaNotes/Resources/Images/default_avatar.png

---

## Phase 2: Foundational

**Goal**: Implement core service interfaces and contracts required by all user stories

**Independent Test**: All interface definitions compile and can be mocked in tests

### Tasks

- [ ] T004 Create IImagePickerService interface in BaristaNotes.Core/Services/IImagePickerService.cs
- [ ] T005 [P] Create IImageProcessingService interface in BaristaNotes.Core/Services/IImageProcessingService.cs
- [ ] T006 [P] Extend IUserProfileService interface with image methods in BaristaNotes.Core/Services/IUserProfileService.cs
- [ ] T007 Register IMediaPicker singleton in BaristaNotes/MauiProgram.cs
- [ ] T008 [P] Configure iOS photo library permission in BaristaNotes/Platforms/iOS/Info.plist

---

## Phase 3: User Story 1 - Select Photo from Device

**User Story**: "As a user, I want to select a photo from my device so I can personalize my profile"

**Acceptance Criteria**:
- Tapping profile area opens native photo picker
- Selected image appears as circular avatar
- User cancellation handled gracefully

**Independent Test**: 
1. Mock IMediaPicker to return test image
2. Verify ProfileImagePicker component renders with image
3. Verify circular crop applied
4. Verify cancellation returns to original state

### Tasks

#### Test Setup (TDD)

- [ ] T009 [US1] Create ImagePickerServiceTests.cs in BaristaNotes.Tests/Unit/Services/ with test for PickImageAsync returning stream
- [ ] T010 [P] [US1] Add test in ImagePickerServiceTests.cs for user cancellation returning null
- [ ] T011 [P] [US1] Add test in ImagePickerServiceTests.cs verifying MediaPickerOptions (MaxWidth=400, MaxHeight=400, Quality=85)

#### Service Implementation

- [ ] T012 [US1] Implement ImagePickerService class in BaristaNotes.Core/Services/ImagePickerService.cs using IMediaPicker
- [ ] T013 [US1] Register IImagePickerService as singleton in BaristaNotes/MauiProgram.cs

#### UI Components

- [ ] T014 [P] [US1] Create CircularAvatar component in BaristaNotes/Components/CircularAvatar.cs with Frame and Image controls
- [ ] T015 [US1] Create ProfileImagePicker component in BaristaNotes/Components/ProfileImagePicker.cs with state management
- [ ] T016 [US1] Add "Change Photo" button to ProfileImagePicker component with PickImageAsync handler
- [ ] T017 [US1] Implement image display logic in ProfileImagePicker to show selected image or default placeholder
- [ ] T018 [US1] Integrate ProfileImagePicker into UserProfileManagementPage.cs in BaristaNotes/Pages/

---

## Phase 4: User Story 2 - Auto-processing and Display Quality

**User Story**: "As a user, I want my profile photo to look good without manually cropping or resizing it"

**Acceptance Criteria**:
- Image automatically resized to 400x400px max
- Image quality maintained (JPEG quality 85)
- EXIF orientation corrected
- Circular crop appears clean and centered

**Independent Test**:
1. Provide oversized test image (e.g., 2000x2000)
2. Verify validation passes/fails based on dimensions
3. Verify saved file is ≤400x400
4. Verify circular display handles non-square images

### Tasks

#### Test Setup (TDD)

- [ ] T019 [US2] Create ImageProcessingServiceTests.cs in BaristaNotes.Tests/Unit/Services/ with validation tests
- [ ] T020 [P] [US2] Add test for ValidateImageAsync with valid image (300x300, 200KB) returning Valid
- [ ] T021 [P] [US2] Add test for ValidateImageAsync with oversized image (500x500) returning DimensionsTooLarge
- [ ] T022 [P] [US2] Add test for ValidateImageAsync with large file (2MB) returning TooLarge

#### Service Implementation

- [ ] T023 [US2] Implement ImageProcessingService class in BaristaNotes.Core/Services/ImageProcessingService.cs with ValidateImageAsync
- [ ] T024 [US2] Register IImageProcessingService as singleton in BaristaNotes/MauiProgram.cs

---

## Phase 5: User Story 3 - Persistence Across Sessions

**User Story**: "As a user, I want my profile photo to persist so I don't have to select it every time"

**Acceptance Criteria**:
- Selected image persists after app restart
- Image file stored in FileSystem.AppDataDirectory
- Database stores filename reference
- File exists check before display

**Independent Test**:
1. Mock file system to simulate app restart
2. Verify UserProfile.AvatarPath saved to database
3. Verify GetProfileImagePathAsync returns correct path
4. Verify LoadProfileImage retrieves persisted image

### Tasks

#### Test Setup (TDD)

- [ ] T025 [US3] Create UserProfileServiceImageTests.cs in BaristaNotes.Tests/Unit/Services/ for UpdateProfileImageAsync
- [ ] T026 [P] [US3] Add test for UpdateProfileImageAsync with valid image updating database

#### Service Implementation

- [ ] T027 [US3] Implement SaveImageAsync and GetImagePath in ImageProcessingService in BaristaNotes.Core/Services/ImageProcessingService.cs
- [ ] T028 [US3] Implement UpdateProfileImageAsync in UserProfileService in BaristaNotes.Core/Services/UserProfileService.cs

---

## Phase 6: User Story 4 - Remove Profile Photo

**User Story**: "As a user, I want to remove my profile photo if I change my mind"

**Acceptance Criteria**:
- "Remove" button appears when avatar set
- Tapping remove deletes image file
- Database updated to null AvatarPath
- Default placeholder displayed after removal

**Independent Test**:
1. Mock profile with existing avatar
2. Call RemoveProfileImageAsync
3. Verify file delete called
4. Verify AvatarPath set to null
5. Verify UI shows default placeholder

### Tasks

#### Test Setup (TDD)

- [ ] T029 [US4] Add test in ImageProcessingServiceTests.cs for DeleteImageAsync with existing file
- [ ] T030 [P] [US4] Add test in ImageProcessingServiceTests.cs for DeleteImageAsync with non-existing file
- [ ] T031 [P] [US4] Add test in UserProfileServiceImageTests.cs for RemoveProfileImageAsync

#### Service Implementation

- [ ] T032 [US4] Implement DeleteImageAsync and ImageExists in ImageProcessingService in BaristaNotes.Core/Services/ImageProcessingService.cs
- [ ] T033 [US4] Implement RemoveProfileImageAsync and GetProfileImagePathAsync in UserProfileService in BaristaNotes.Core/Services/UserProfileService.cs

#### UI Implementation

- [ ] T034 [US4] Add "Remove" button to ProfileImagePicker component with RemoveImageAsync handler in BaristaNotes/Components/ProfileImagePicker.cs

---

## Phase 7: Polish & Cross-Cutting Concerns

**Goal**: Complete testing, error handling, accessibility, and platform-specific configurations

**Independent Test**: Full end-to-end flow works on iOS and Android devices with proper error handling

### Tasks

#### Integration Testing

- [ ] T035 Create UserProfileImageTests.cs in BaristaNotes.Tests/Integration/ for end-to-end image flow
- [ ] T036 [P] Add integration test for image selection, save, and database persistence
- [ ] T037 [P] Add integration test for image removal and cleanup

#### Error Handling & UX

- [ ] T038 Add error message display to ProfileImagePicker for validation failures in BaristaNotes/Components/ProfileImagePicker.cs
- [ ] T039 [P] Add loading indicator (ActivityIndicator) during image processing in ProfileImagePicker component
- [ ] T040 [P] Add permission denied error handling with user-friendly message in ProfileImagePicker component

#### Platform Configuration

- [ ] T041 Configure Android READ_EXTERNAL_STORAGE permission (if needed for Android <13) in BaristaNotes/Platforms/Android/AndroidManifest.xml
- [ ] T042 [P] Add semantic descriptions for accessibility to CircularAvatar and ProfileImagePicker components in BaristaNotes/Components/

---

## Parallel Execution Examples

### Within Setup Phase
All T001-T003 can execute in parallel (different files, no dependencies)

### Within Foundational Phase
- T005 and T006 can execute in parallel (different services)
- T008 can execute in parallel with T004-T007

### Within User Story 1
- T010 and T011 can execute in parallel (independent test cases)
- T014 can execute in parallel with T012-T013 (UI vs service layer)

### Within User Story 2
- T020, T021, T022 can execute in parallel (independent test cases)

### Within User Story 3
- T026 can execute in parallel with T025 (additional test case)

### Within User Story 4
- T029, T030, T031 can execute in parallel (independent test cases)

### Within Polish Phase
- T036 and T037 can execute in parallel (independent integration tests)
- T039, T040 can execute in parallel (independent UI enhancements)
- T042 can execute in parallel with T038-T041

---

## Validation Checklist

Before marking feature complete, verify:

- [ ] All 42 tasks completed
- [ ] All unit tests passing (80%+ coverage target achieved)
- [ ] Integration tests passing on iOS and Android
- [ ] Manual testing completed:
  - [ ] Image selection opens native picker
  - [ ] Selected image displays as circular avatar
  - [ ] Image persists after app restart
  - [ ] Remove button functions correctly
  - [ ] Default placeholder shows when no image
  - [ ] Error messages clear and actionable
  - [ ] Performance <2 seconds selection to display
  - [ ] Works on both iOS and Android devices
- [ ] Accessibility verified with screen reader
- [ ] Code review completed
- [ ] Documentation updated (if user-facing changes)

---

## Notes

**Test-First Development**: This feature follows TDD as mandated by the constitution. Tests are created before implementation for each user story.

**File Paths**: All file paths are absolute from repository root. Use exact paths specified in task descriptions.

**Constitution Compliance**: 
- ✅ Code Quality: Services follow single-responsibility principle
- ✅ Test-First: Tests defined before implementation
- ✅ UX Consistency: Native picker, circular display, accessible
- ✅ Performance: <2 second target, async operations

**Moq Required**: Add Moq package reference to BaristaNotes.Tests.csproj if not already present for mocking IMediaPicker and services.

---

## Quick Reference

**Service Interfaces**:
- `IImagePickerService`: Photo selection abstraction
- `IImageProcessingService`: Validation and file operations
- `IUserProfileService`: Business logic and persistence

**Key Components**:
- `CircularAvatar`: Reusable avatar display
- `ProfileImagePicker`: User interaction component

**File Naming Convention**: `profile_avatar_{profileId}.jpg`

**Storage Location**: `FileSystem.AppDataDirectory`

**Validation Rules**: 100x100 (min) to 400x400 (max), <1MB, JPEG only
