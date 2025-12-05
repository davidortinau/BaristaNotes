# Implementation Plan: User Profile Image Picker

**Branch**: `003-profile-image-picker` | **Date**: 2025-12-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/003-profile-image-picker/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Implement a user profile image picker that allows users to select an image from their device photo library. The selected image will be automatically resized to 400x400px maximum, stored locally, and displayed as a circle-cropped avatar throughout the app. Technical approach will use .NET MAUI's Media Picker API for cross-platform image selection, IImageService for processing, and local file storage for persistence.

## Technical Context

**Language/Version**: C# 12 / .NET 10.0  
**Primary Dependencies**: 
- .NET MAUI 10.0.11 (cross-platform UI framework)
- Microsoft.Maui.Media (MediaPicker.PickPhotosAsync for image selection)
- Microsoft.Maui.Graphics (IImage for validation - optional fallback)
- Entity Framework Core 10.0.0 + SQLite (existing data layer)
- Reactor.Maui 4.0.3-beta (MVU state management)

**Storage**: 
- SQLite database (existing UserProfile.AvatarPath field - no migration needed)
- Local file system: `FileSystem.AppDataDirectory` for processed images
- File naming: `profile_avatar_{profileId}.jpg`
- Cleanup: Delete on profile removal or avatar update

**Testing**: 
- xUnit (existing test framework)
- Moq for mocking IMediaPicker and service interfaces
- Unit tests: Service layer validation, file operations
- Integration tests: Database persistence, end-to-end image flow
- Manual device tests: Native UI picker, circular display

**Target Platform**: iOS 15+, Android 21+, Mac Catalyst 15+ (.NET MAUI mobile)

**Project Type**: Mobile application (MAUI multi-project: BaristaNotes.Core, BaristaNotes, BaristaNotes.Tests)

**Performance Goals**: 
- Image selection to display: <2 seconds
- Image processing (automatic via MediaPickerOptions): <1 second
- UI remains responsive during processing (async operations)

**Constraints**: 
- Image file size: <1MB (enforced), target <500KB
- Image dimensions: 100x100 (min) to 400x400 (max)
- Local storage only (no cloud dependencies)
- Maintain existing MVU architecture patterns
- JPEG format only (quality 85)

**Scale/Scope**: 
- Single user device (no multi-user concerns)
- ~10-20 user profiles per device
- Single image per profile
- Minimal storage impact (<10MB total for all profiles)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

All features must demonstrate alignment with constitutional principles:

- [x] **Code Quality Standards**: Design will enable single-responsibility components: ImagePickerService for selection, ImageProcessingService for resize/compress, updated UserProfileService for persistence. Clear separation of concerns between UI (Reactor component), service layer, and data layer.

- [x] **Test-First Development**: Test scenarios will be written and approved before implementation:
  - Unit tests: Image processing (resize, compress, validate dimensions)
  - Unit tests: File storage operations (save, load, delete, cleanup)
  - Integration tests: UserProfile with image path persistence
  - UI tests: MediaPicker integration (with mocking strategy)
  - Target: 80% coverage for services, 100% for image processing logic

- [x] **User Experience Consistency**: 
  - Uses native MediaPicker UI (platform consistency)
  - Circle avatar display follows existing design patterns
  - Loading indicator during image processing
  - Error messages for failures (permissions, file access, processing errors)
  - Accessible: tappable profile image with proper semantic labels

- [x] **Performance Requirements**: 
  - Image selection + processing target: <2 seconds
  - Async processing to avoid UI blocking
  - Instrumentation: measure image processing time, file I/O operations
  - Performance test: validate 400x400 resize completes within target

**Violations requiring justification**: None. All constitutional principles can be met with this design.

## Project Structure

### Documentation (this feature)

```text
specs/003-profile-image-picker/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
BaristaNotes.Core/
├── Models/
│   └── UserProfile.cs               # Existing: AvatarPath property
├── Services/
│   ├── IUserProfileService.cs       # Existing: extend with image methods
│   ├── UserProfileService.cs        # Existing: extend with image methods
│   ├── IImagePickerService.cs       # NEW: abstraction for MediaPicker
│   ├── ImagePickerService.cs        # NEW: platform-agnostic picker
│   ├── IImageProcessingService.cs   # NEW: resize/compress abstraction
│   └── ImageProcessingService.cs    # NEW: image manipulation
├── Data/
│   ├── BaristaNotesContext.cs       # Existing: no changes needed
│   └── Repositories/
│       ├── IUserProfileRepository.cs # Existing: may need image cleanup
│       └── UserProfileRepository.cs  # Existing: may need image cleanup
└── Migrations/                      # NEW: migration if AvatarPath needs changes

BaristaNotes/
├── Components/
│   ├── ProfileImagePicker.cs        # NEW: Reactor component for image selection
│   └── CircularAvatar.cs            # NEW: Reactor component for display
├── Pages/
│   └── UserProfileManagementPage.cs # MODIFY: integrate image picker
└── Platforms/
    ├── Android/
    │   └── Permissions.cs           # MODIFY: add READ_EXTERNAL_STORAGE if needed
    └── iOS/
        └── Info.plist               # MODIFY: add photo library usage description

BaristaNotes.Tests/
├── Unit/
│   ├── Services/
│   │   ├── ImageProcessingServiceTests.cs  # NEW
│   │   ├── ImagePickerServiceTests.cs      # NEW
│   │   └── UserProfileServiceTests.cs      # EXTEND: image operations
│   └── Components/
│       └── ProfileImagePickerTests.cs      # NEW
└── Integration/
    └── UserProfileImageTests.cs            # NEW: end-to-end image flow
```

**Structure Decision**: Using existing mobile application structure with BaristaNotes.Core for business logic/services, BaristaNotes for UI components, and BaristaNotes.Tests for test coverage. New services follow repository pattern established in codebase.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
