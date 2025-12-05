# Research: User Profile Image Picker

**Date**: 2025-12-05  
**Feature**: 003-profile-image-picker

## Overview

This document consolidates research findings for implementing a user profile image picker in .NET MAUI, resolving all "NEEDS CLARIFICATION" items from the Technical Context.

---

## 1. MediaPicker API Usage

### Decision
Use `MediaPicker.PickPhotosAsync()` with `SelectionLimit = 1` from `Microsoft.Maui.Media` namespace.

### Rationale
- **Official API**: Built into .NET MAUI 10.0, no external dependencies
- **Modern Method**: `PickPhotoAsync()` is obsolete; `PickPhotosAsync()` is recommended even for single selection
- **Automatic Processing**: Supports `MaximumWidth` and `MaximumHeight` parameters for built-in resize
- **Returns**: `List<FileResult>` where `FileResult` provides `OpenReadAsync()` for stream access
- **Platform Native**: Uses native photo picker UI on iOS/Android

### Alternatives Considered
- **Deprecated `PickPhotoAsync()`**: Marked obsolete, lacks multi-select capability
- **CapturePhotoAsync()**: Only for camera capture, not gallery selection
- **Community Toolkit FilePicker**: More generic, less optimized for images

### Implementation Pattern
```csharp
var results = await MediaPicker.PickPhotosAsync(new MediaPickerOptions
{
    SelectionLimit = 1,
    MaximumWidth = 400,
    MaximumHeight = 400,
    CompressionQuality = 85,
    RotateImage = true,
    PreserveMetaData = false
});

if (results?.Count > 0)
{
    var file = results.First();
    using var stream = await file.OpenReadAsync();
    // Process stream
}
```

### References
- [Media picker for photos and videos - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device-media/picker?view=net-maui-10.0)
- Namespace: `Microsoft.Maui.Media`
- Assembly: `Microsoft.Maui.Essentials.dll` (included in MAUI)

---

## 2. Image Processing (Resize & Compress)

### Decision
Use `MediaPickerOptions` built-in resizing for initial processing, supplemented by `Microsoft.Maui.Graphics.IImage` for additional manipulation if needed.

### Rationale
- **Simplified Approach**: `MediaPickerOptions.MaximumWidth/Height` handles resize automatically
- **Compression Control**: `CompressionQuality` (0-100) provides JPEG compression
- **Rotation Handling**: `RotateImage = true` auto-corrects EXIF orientation
- **Metadata Stripping**: `PreserveMetaData = false` reduces file size
- **No External Dependencies**: Uses built-in MAUI capabilities

### Alternatives Considered
- **SkiaSharp**: Fast, but adds external dependency (~2MB per platform)
- **ImageSharp**: More features, but slower and larger dependency
- **Microsoft.Maui.Graphics.IImage**: More control, but requires manual implementation
  - Only needed if built-in processing insufficient
  - Pattern: `PlatformImage.FromStream(stream).Resize(400, 400).SaveAsync(outputStream, ImageFormat.Jpeg, 0.85f)`

### Image Format Decision
**JPEG with quality 85**:
- No transparency needed (profile images)
- Better compression than PNG (~70% smaller files)
- Quality 85 balances size vs quality
- Target: <500KB for 400x400 image

### Processing Pipeline
1. **MediaPicker**: User selects image with built-in resize to 400x400
2. **Validation**: Check dimensions and file size
3. **Save**: Write stream to `FileSystem.AppDataDirectory`
4. **Database**: Store relative path in `UserProfile.AvatarPath`

### References
- [Media picker options - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/device-media/picker?view=net-maui-10.0#using-media-picker)
- [Images - Graphics - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/graphics/images?view=net-maui-10.0)

---

## 3. Local File Storage Best Practices

### Decision
Store processed images in `FileSystem.AppDataDirectory` with standardized naming convention.

### Rationale
- **Platform Abstraction**: `FileSystem.AppDataDirectory` maps to platform-specific locations
  - iOS: `Documents` folder (backed up by iCloud)
  - Android: `data/data/[package]/files/` (backed up by Google)
  - Mac Catalyst: `~/Library/Application Support/[bundle]`
- **Persistence**: Files survive app updates/restarts
- **Automatic Cleanup**: OS manages cleanup when app uninstalled
- **No Permissions**: Reading/writing to app data directory requires no special permissions

### File Naming Convention
**Pattern**: `profile_avatar_{profileId}.jpg`
- Example: `profile_avatar_123.jpg`
- Predictable for cleanup operations
- Easy to query by profile ID
- Single extension (.jpg) for consistency

### Storage Location Examples
```csharp
// Get directory
string appDataPath = FileSystem.AppDataDirectory;
// iOS: /var/mobile/Containers/Data/Application/{guid}/Documents/
// Android: /data/data/com.companyname.baristanotes/files/

// Full path example
string filePath = Path.Combine(appDataPath, $"profile_avatar_{profileId}.jpg");
```

### Cleanup Strategy
- **On Profile Delete**: Remove associated image file
- **On Image Update**: Delete old file before saving new
- **Orphan Detection**: Periodic cleanup of files not referenced in database

### Alternatives Considered
- **Cache Directory** (`FileSystem.CacheDirectory`): Not backed up, can be cleared by OS
- **Temp Directory** (`TemporaryFolder`): Automatically cleaned up, not persistent
- **External Storage**: Requires permissions, not portable across platforms

### References
- [File system helpers - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/file-system-helpers?view=net-maui-10.0)
- Code sample: Notes app using `FileSystem.AppDataDirectory`

---

## 4. Testing Strategy for MediaPicker

### Decision
Use interface abstraction with Moq for unit testing; integration tests on device/emulator.

### Rationale
- **Platform Dependency**: `MediaPicker` requires native UI, cannot run in unit tests
- **Interface Available**: `IMediaPicker` provided by framework for DI
- **Moq Support**: Standard mocking library works well with `IMediaPicker`
- **Separation**: Business logic testable independently of UI

### Unit Testing Pattern

**Service Abstraction**:
```csharp
public interface IImagePickerService
{
    Task<Stream?> PickImageAsync();
}

public class ImagePickerService : IImagePickerService
{
    private readonly IMediaPicker _mediaPicker;
    
    public ImagePickerService(IMediaPicker mediaPicker)
    {
        _mediaPicker = mediaPicker;
    }
    
    public async Task<Stream?> PickImageAsync()
    {
        var results = await _mediaPicker.PickPhotosAsync(new MediaPickerOptions
        {
            SelectionLimit = 1,
            MaximumWidth = 400,
            MaximumHeight = 400
        });
        
        if (results?.Count > 0)
        {
            return await results.First().OpenReadAsync();
        }
        return null;
    }
}
```

**Unit Test with Moq**:
```csharp
[Fact]
public async Task UpdateProfileImage_WithValidImage_SavesImagePath()
{
    // Arrange
    var mockMediaPicker = new Mock<IMediaPicker>();
    var mockFileResult = new Mock<FileResult>("test.jpg");
    mockFileResult.Setup(f => f.OpenReadAsync()).ReturnsAsync(new MemoryStream());
    
    mockMediaPicker
        .Setup(m => m.PickPhotosAsync(It.IsAny<MediaPickerOptions>()))
        .ReturnsAsync(new List<FileResult> { mockFileResult.Object });
    
    var service = new ImagePickerService(mockMediaPicker.Object);
    
    // Act
    var stream = await service.PickImageAsync();
    
    // Assert
    Assert.NotNull(stream);
}
```

### Test Coverage Strategy

**Unit Tests** (80% target):
- Image picker service: Selection, cancellation, errors
- Image processing service: Resize validation, compression, format
- UserProfile service: Update avatar path, delete old image
- File storage operations: Save, load, delete, path generation

**Integration Tests** (critical paths):
- End-to-end: Pick image → process → save → load → display
- Database: Profile with avatar path persistence
- File system: Image file exists after save

**Manual/Device Tests** (UI validation):
- MediaPicker native UI appearance
- Image selection and cancellation flows
- Circular avatar display quality
- Performance on actual devices

### Mocking Frameworks
- **Primary**: Moq (already familiar in .NET ecosystem)
- **Alternative**: NSubstitute (if preferred)
- **Helper**: MauiMocks library (for MAUI-specific mocks)

### Alternatives Considered
- **Direct MediaPicker.Default in tests**: Fails, requires native UI
- **Device-only tests**: Slow, hard to automate, CI/CD challenges
- **Custom FileResult implementation**: Complex, fragile

### References
- [Unit testing - .NET MAUI - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/maui/deployment/unit-testing?view=net-maui-10.0)
- [MauiMocks library](https://github.com/thomasgalliker/MauiMocks)
- Community examples: Moq with IMediaPicker

---

## 5. Image Processing Validation

### Decision
Validate images using dimension checks and file size limits after processing.

### Rationale
- **Trust but Verify**: `MediaPickerOptions` processing is usually reliable, but validation ensures quality
- **User Feedback**: Provide clear errors if image processing fails
- **Resource Protection**: Prevent oversized images from impacting app performance

### Validation Rules

**Dimensions**:
- Maximum: 400x400 pixels
- Minimum: 100x100 pixels (ensure quality for display)
- Aspect ratio: Any (will be circle-cropped anyway)

**File Size**:
- Target: <500KB
- Maximum: 1MB (reject if exceeded)
- Typical JPEG @ quality 85: 100-300KB for 400x400

**Format**:
- Accept: JPEG only from picker
- Store as: `.jpg` extension
- Reject: Animated GIFs, videos, unsupported formats

### Validation Implementation
```csharp
public async Task<ImageValidationResult> ValidateImageAsync(Stream imageStream)
{
    using var ms = new MemoryStream();
    await imageStream.CopyToAsync(ms);
    ms.Position = 0;
    
    // Check file size
    if (ms.Length > 1_048_576) // 1MB
    {
        return ImageValidationResult.TooLarge;
    }
    
    // Load image to check dimensions
    var image = PlatformImage.FromStream(ms);
    if (image.Width > 400 || image.Height > 400)
    {
        return ImageValidationResult.DimensionsTooLarge;
    }
    if (image.Width < 100 || image.Height < 100)
    {
        return ImageValidationResult.DimensionsTooSmall;
    }
    
    return ImageValidationResult.Valid;
}
```

### Error Handling
- **User Cancellation**: No error, return to previous state
- **Permission Denied**: Show alert explaining photo library permission needed
- **Processing Failed**: Show generic error, log details for debugging
- **Validation Failed**: Show specific message (e.g., "Image too large")

### Alternatives Considered
- **No Validation**: Trust MediaPicker completely (risky)
- **Client-Side Only**: Skip server validation (acceptable for local-only app)
- **Strict Format Check**: Magic number validation (overkill for MAUI MediaPicker)

---

## Summary of Decisions

| Clarification | Decision | Key Benefit |
|---------------|----------|-------------|
| **MediaPicker API** | `MediaPicker.PickPhotosAsync()` with options | Built-in resize, modern API |
| **Image Processing** | `MediaPickerOptions` + validation | No external dependencies |
| **Storage Location** | `FileSystem.AppDataDirectory` | Cross-platform, backed up |
| **File Naming** | `profile_avatar_{id}.jpg` | Predictable, easy cleanup |
| **Testing Strategy** | Mock `IMediaPicker` with Moq | Unit testable business logic |
| **Validation** | Dimensions + size checks | Quality assurance |
| **Image Format** | JPEG @ quality 85 | Balance size/quality |

---

## Implementation Readiness

All "NEEDS CLARIFICATION" items resolved:
- ✅ MediaPicker namespace and usage pattern defined
- ✅ Image processing approach selected (built-in options)
- ✅ Storage location and file management strategy established
- ✅ Testing approach with mocking strategy documented
- ✅ Validation and error handling patterns defined

**Ready to proceed to Phase 1: Design & Contracts**
