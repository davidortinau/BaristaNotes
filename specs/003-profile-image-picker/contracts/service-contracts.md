# Service Contracts: User Profile Image Picker

**Date**: 2025-12-05  
**Feature**: 003-profile-image-picker

## Overview

This document defines the service interfaces and method contracts for the profile image picker feature. All services follow the existing repository/service pattern in BaristaNotes.Core.

---

## 1. IImagePickerService

**Purpose**: Abstraction over MediaPicker for photo selection, enabling testability.

**Namespace**: `BaristaNotes.Core.Services`

### Interface Definition

```csharp
namespace BaristaNotes.Core.Services;

public interface IImagePickerService
{
    /// <summary>
    /// Opens native photo picker and returns selected image as stream.
    /// </summary>
    /// <returns>
    /// Image stream if user selected photo, null if cancelled or error.
    /// </returns>
    /// <exception cref="PermissionException">
    /// Thrown when photo library permission denied.
    /// </exception>
    Task<Stream?> PickImageAsync();
    
    /// <summary>
    /// Checks if photo picker is available on current platform.
    /// </summary>
    bool IsPickerSupported { get; }
}
```

### Method Details

#### PickImageAsync()

**Behavior**:
- Opens native photo library picker
- Returns immediately if user cancels (returns null)
- Applies MediaPickerOptions: MaxWidth=400, MaxHeight=400, Quality=85
- Auto-rotates based on EXIF orientation
- Strips EXIF metadata for privacy

**Return Values**:
- `Stream`: Successfully selected image, ready for processing
- `null`: User cancelled or picker unavailable

**Exceptions**:
- `PermissionException`: Photo library permission denied
- `PlatformNotSupportedException`: Platform doesn't support photo picking
- `InvalidOperationException`: Picker already in use

**Usage Example**:
```csharp
var imageStream = await _imagePickerService.PickImageAsync();
if (imageStream != null)
{
    // Process image
}
```

#### IsPickerSupported

**Behavior**:
- Returns `true` if current platform supports photo picking
- Always `true` for iOS/Android
- May be `false` for desktop platforms or emulators

---

## 2. IImageProcessingService

**Purpose**: Validate and process images according to requirements.

**Namespace**: `BaristaNotes.Core.Services`

### Interface Definition

```csharp
namespace BaristaNotes.Core.Services;

public interface IImageProcessingService
{
    /// <summary>
    /// Validates image dimensions and file size.
    /// </summary>
    /// <param name="imageStream">Image stream to validate (will be read and reset).</param>
    /// <returns>Validation result indicating success or specific failure.</returns>
    Task<ImageValidationResult> ValidateImageAsync(Stream imageStream);
    
    /// <summary>
    /// Saves image stream to app data directory with specified filename.
    /// </summary>
    /// <param name="imageStream">Image stream to save.</param>
    /// <param name="filename">Target filename (e.g., "profile_avatar_123.jpg").</param>
    /// <returns>Full path to saved file.</returns>
    /// <exception cref="IOException">Failed to save file.</exception>
    Task<string> SaveImageAsync(Stream imageStream, string filename);
    
    /// <summary>
    /// Deletes image file from app data directory.
    /// </summary>
    /// <param name="filename">Filename to delete (not full path).</param>
    /// <returns>True if deleted, false if file doesn't exist.</returns>
    Task<bool> DeleteImageAsync(string filename);
    
    /// <summary>
    /// Constructs full path to image file in app data directory.
    /// </summary>
    /// <param name="filename">Filename (e.g., "profile_avatar_123.jpg").</param>
    /// <returns>Full path to file.</returns>
    string GetImagePath(string filename);
    
    /// <summary>
    /// Checks if image file exists in app data directory.
    /// </summary>
    /// <param name="filename">Filename to check.</param>
    /// <returns>True if file exists.</returns>
    bool ImageExists(string filename);
}
```

### Method Details

#### ValidateImageAsync(Stream imageStream)

**Validation Rules**:
- **File Size**: ≤1MB (max), ~100-500KB (typical)
- **Dimensions**: 100x100 (min) to 400x400 (max)
- **Format**: JPEG (implied by MediaPicker output)

**Return Values** (ImageValidationResult enum):
- `Valid`: Image meets all requirements
- `TooLarge`: File size exceeds 1MB
- `DimensionsTooLarge`: Width or height > 400px
- `DimensionsTooSmall`: Width or height < 100px
- `ProcessingFailed`: Unexpected error during validation

**Stream Handling**:
- Stream position will be reset to 0 after validation
- Stream remains open (caller responsible for disposal)

#### SaveImageAsync(Stream imageStream, string filename)

**Behavior**:
- Saves stream to `FileSystem.AppDataDirectory/{filename}`
- Overwrites if file already exists
- Creates directory if doesn't exist
- Closes stream after saving

**Parameters**:
- `imageStream`: Source image data
- `filename`: Just filename, not full path (e.g., "profile_avatar_123.jpg")

**Returns**: Full path to saved file

**Exceptions**:
- `IOException`: Disk full, permission denied, etc.
- `ArgumentException`: Invalid filename

#### DeleteImageAsync(string filename)

**Behavior**:
- Deletes file from `FileSystem.AppDataDirectory/{filename}`
- Returns `false` if file doesn't exist (not an error)
- Returns `true` if file deleted successfully

**Thread Safety**: Safe to call even if file already deleted

#### GetImagePath(string filename)

**Behavior**:
- Constructs full path: `Path.Combine(FileSystem.AppDataDirectory, filename)`
- Does not check if file exists
- Pure function, no I/O

#### ImageExists(string filename)

**Behavior**:
- Checks if file exists at `GetImagePath(filename)`
- Returns `false` if path invalid or file missing
- Synchronous operation

---

## 3. IUserProfileService Extensions

**Purpose**: Extend existing service with image-specific operations.

**Namespace**: `BaristaNotes.Core.Services`

### New Methods

```csharp
namespace BaristaNotes.Core.Services;

public partial interface IUserProfileService
{
    /// <summary>
    /// Updates profile avatar from image stream.
    /// Validates, saves image, updates database.
    /// </summary>
    /// <param name="profileId">Profile to update.</param>
    /// <param name="imageStream">Image stream from picker.</param>
    /// <returns>Result indicating success or specific failure.</returns>
    Task<ProfileImageUpdateResult> UpdateProfileImageAsync(int profileId, Stream imageStream);
    
    /// <summary>
    /// Removes avatar from profile and deletes image file.
    /// </summary>
    /// <param name="profileId">Profile to update.</param>
    /// <returns>True if removed, false if profile had no avatar.</returns>
    Task<bool> RemoveProfileImageAsync(int profileId);
    
    /// <summary>
    /// Gets full path to profile's avatar image, or null if no avatar.
    /// </summary>
    /// <param name="profileId">Profile to query.</param>
    /// <returns>Full path to image, or null if no avatar set.</returns>
    Task<string?> GetProfileImagePathAsync(int profileId);
}
```

### Method Details

#### UpdateProfileImageAsync(int profileId, Stream imageStream)

**Workflow**:
1. Validate image using `IImageProcessingService.ValidateImageAsync()`
2. If invalid, return failure result with specific error
3. Generate filename: `profile_avatar_{profileId}.jpg`
4. Delete old image if exists
5. Save new image using `IImageProcessingService.SaveImageAsync()`
6. Update `UserProfile.AvatarPath` in database
7. Update `UserProfile.LastModifiedAt`
8. Save changes to database
9. Return success result with new path

**Return Values**:
- `Success = true, NewAvatarPath = "profile_avatar_123.jpg"`: Updated successfully
- `Success = false, ErrorMessage = "..."`: Failed with specific error

**Error Messages**:
- "Image is too large (max 1MB)"
- "Image dimensions too large (max 400x400)"
- "Image dimensions too small (min 100x100)"
- "Failed to save image"
- "Profile not found"

**Transactional Behavior**:
- If image saves but DB update fails, orphan file remains (acceptable)
- If DB update succeeds but old file delete fails, log warning (acceptable)

#### RemoveProfileImageAsync(int profileId)

**Workflow**:
1. Load UserProfile by ID
2. If `AvatarPath` is null, return `false` (nothing to remove)
3. Delete image file using `IImageProcessingService.DeleteImageAsync()`
4. Set `UserProfile.AvatarPath = null`
5. Update `UserProfile.LastModifiedAt`
6. Save changes to database
7. Return `true`

**Return Values**:
- `true`: Avatar removed (or didn't exist)
- Throws exception if profile not found

#### GetProfileImagePathAsync(int profileId)

**Workflow**:
1. Load UserProfile by ID
2. If `AvatarPath` is null, return `null`
3. Construct full path using `IImageProcessingService.GetImagePath()`
4. Verify file exists using `IImageProcessingService.ImageExists()`
5. If file missing, log warning and return `null`
6. Return full path

**Return Values**:
- `string`: Full path to existing image file
- `null`: No avatar set or file missing

---

## 4. Repository Layer

**Note**: No changes required to `IUserProfileRepository` or `UserProfileRepository`. The `AvatarPath` field is already part of the UserProfile entity and will be automatically persisted.

---

## 5. Error Handling Strategy

### Service Layer Exceptions

**IImagePickerService**:
- `PermissionException`: User denied photo library access
  - UI: Show permission explanation dialog
- `PlatformNotSupportedException`: Platform doesn't support picker
  - UI: Disable feature or show "Not available" message
- `InvalidOperationException`: Picker already active
  - UI: Ignore (shouldn't happen with proper UI state management)

**IImageProcessingService**:
- `IOException`: Disk full, permission denied
  - UI: Show "Failed to save image, check storage" message
- `ArgumentException`: Invalid filename
  - UI: Should never reach user (internal error, log and recover)

**IUserProfileService**:
- Returns `ProfileImageUpdateResult` instead of throwing
  - `Success = false` with `ErrorMessage` for user display
- Only throws for unexpected errors (DB connection lost, etc.)

### UI Layer Handling

```csharp
try
{
    var stream = await _imagePickerService.PickImageAsync();
    if (stream == null) return; // User cancelled
    
    var result = await _userProfileService.UpdateProfileImageAsync(profileId, stream);
    if (!result.Success)
    {
        await DisplayAlert("Error", result.ErrorMessage, "OK");
        return;
    }
    
    // Success: refresh UI
    await RefreshProfileImage();
}
catch (PermissionException)
{
    await DisplayAlert("Permission Required", 
        "Please allow photo library access in Settings", "OK");
}
catch (Exception ex)
{
    _logger.LogError(ex, "Unexpected error updating profile image");
    await DisplayAlert("Error", "An unexpected error occurred", "OK");
}
```

---

## 6. Dependency Injection Registration

**Location**: `BaristaNotes/MauiProgram.cs`

### Service Registration

```csharp
// Image picker service
builder.Services.AddSingleton<IMediaPicker>(MediaPicker.Default);
builder.Services.AddSingleton<IImagePickerService, ImagePickerService>();

// Image processing service
builder.Services.AddSingleton<IImageProcessingService, ImageProcessingService>();

// UserProfileService already registered, no changes needed
```

### Lifetime Justification
- **IMediaPicker**: Singleton (stateless platform API)
- **IImagePickerService**: Singleton (thin wrapper, no state)
- **IImageProcessingService**: Singleton (stateless utilities)
- **IUserProfileService**: Existing registration (likely scoped)

---

## 7. Testing Contracts

### Unit Test Requirements

**IImagePickerService**:
- Mock `IMediaPicker` returns various FileResult scenarios
- Verify correct MediaPickerOptions passed
- Test null return on cancellation
- Test exception propagation

**IImageProcessingService**:
- Test validation with various image sizes/dimensions
- Test save with valid/invalid filenames
- Test delete existing/non-existing files
- Test path construction

**IUserProfileService**:
- Mock `IImagePickerService` and `IImageProcessingService`
- Test successful image update workflow
- Test validation failure handling
- Test removal of existing/non-existing avatars
- Test database update failures

---

## Summary

### Service Responsibilities

| Service | Responsibility |
|---------|---------------|
| **IImagePickerService** | Platform abstraction for photo picking |
| **IImageProcessingService** | File system operations and validation |
| **IUserProfileService** | Business logic and database persistence |

### Key Design Principles
1. **Single Responsibility**: Each service has clear, focused purpose
2. **Testability**: All services mockable via interfaces
3. **Error Handling**: Structured errors (DTOs) vs exceptions
4. **Platform Abstraction**: Platform-specific code isolated
5. **Separation of Concerns**: UI ← Service ← Repository layers

**Ready to generate quickstart.md**
