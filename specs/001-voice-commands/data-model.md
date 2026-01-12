# Data Model: Voice Commands Feature

**Date**: 2026-01-09  
**Feature Branch**: `001-voice-commands`

## Overview

The voice commands feature introduces conceptual entities for voice interaction but does **not** require database schema changes. Voice commands are transient - they are processed in real-time and result in operations on existing entities (Shot, Bean, Bag, Equipment, UserProfile).

## New Entities (In-Memory Only)

### VoiceCommand

Represents a parsed voice input with extracted intent and parameters.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Unique identifier for the command |
| `Transcript` | `string` | Raw text from speech recognition |
| `Intent` | `CommandIntent` | Identified action type |
| `Parameters` | `Dictionary<string, object>` | Extracted values (dose, rating, bean name, etc.) |
| `Confidence` | `double` | Recognition confidence (0.0-1.0) |
| `Timestamp` | `DateTime` | When command was received |
| `Status` | `CommandStatus` | Processing status |

### CommandIntent (Enum)

```csharp
public enum CommandIntent
{
    Unknown,
    LogShot,
    AddBean,
    AddBag,
    RateShot,
    AddTastingNotes,
    AddEquipment,
    AddProfile,
    Navigate,
    Query,
    Cancel,
    Help
}
```

### CommandStatus (Enum)

```csharp
public enum CommandStatus
{
    Listening,
    Processing,
    AwaitingConfirmation,
    Executing,
    Completed,
    Failed,
    Cancelled
}
```

### VoiceSession

Tracks an active voice interaction session.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Session identifier |
| `StartedAt` | `DateTime` | Session start time |
| `EndedAt` | `DateTime?` | Session end time (null if active) |
| `Commands` | `List<VoiceCommand>` | Commands processed in session |
| `IsActive` | `bool` | Whether session is currently active |

## State Transitions

```
                    ┌─────────────┐
                    │   Idle      │
                    └──────┬──────┘
                           │ User taps mic
                           ▼
                    ┌─────────────┐
        ┌───────────│  Listening  │───────────┐
        │ Timeout   └──────┬──────┘  Cancel   │
        │                  │ Speech detected  │
        ▼                  ▼                  ▼
  ┌───────────┐     ┌─────────────┐    ┌───────────┐
  │  Failed   │     │ Processing  │    │ Cancelled │
  └───────────┘     └──────┬──────┘    └───────────┘
                           │ Intent parsed
                           ▼
                    ┌─────────────────┐
        ┌───────────│AwaitingConfirm │───────────┐
        │ User      └───────┬────────┘  Cancel   │
        │ declines          │ User confirms      │
        ▼                   ▼                    ▼
  ┌───────────┐     ┌─────────────┐      ┌───────────┐
  │ Cancelled │     │  Executing  │      │ Cancelled │
  └───────────┘     └──────┬──────┘      └───────────┘
                           │ Operation complete
                           ▼
                    ┌─────────────┐
                    │  Completed  │
                    └─────────────┘
```

## Relationship to Existing Entities

Voice commands interact with existing entities but don't modify their schema:

```
┌─────────────────┐
│  VoiceCommand   │
│    (transient)  │
└────────┬────────┘
         │ executes operations on
         ▼
┌────────────────────────────────────────────────────┐
│                  Existing Entities                  │
├─────────────┬──────────────┬─────────────┬─────────┤
│ ShotRecord  │    Bean      │     Bag     │Equipment│
│ UserProfile │              │             │         │
└─────────────┴──────────────┴─────────────┴─────────┘
```

## DTOs

### VoiceCommandRequestDto

Input to the voice command service after speech recognition.

```csharp
public record VoiceCommandRequestDto(
    string Transcript,
    double Confidence,
    Guid? ActiveBagId = null,
    Guid? ActiveEquipmentId = null,
    Guid? ActiveUserId = null
);
```

### VoiceCommandResponseDto

Output from AI interpretation.

```csharp
public record VoiceCommandResponseDto(
    CommandIntent Intent,
    Dictionary<string, object> Parameters,
    string ConfirmationMessage,
    bool RequiresConfirmation,
    string? ErrorMessage = null
);
```

### VoiceToolResultDto

Result after executing a tool call.

```csharp
public record VoiceToolResultDto(
    bool Success,
    string Message,
    object? CreatedEntity = null,
    Guid? EntityId = null
);
```

## Tool Parameter Definitions

These map to AI function parameters:

### LogShotParameters

```csharp
public record LogShotParameters(
    double DoseGrams,
    double OutputGrams,
    int TimeSeconds,
    int? Rating = null,
    string? TastingNotes = null,
    int? PreinfusionSeconds = null,
    Guid? BagId = null,
    Guid? MachineId = null,
    Guid? GrinderId = null
);
```

### AddBeanParameters

```csharp
public record AddBeanParameters(
    string Name,
    string? Roaster = null,
    string? Origin = null,
    string? TastingNotes = null
);
```

### AddBagParameters

```csharp
public record AddBagParameters(
    string BeanName,
    DateOnly? RoastDate = null
);
```

### RateShotParameters

```csharp
public record RateShotParameters(
    int Rating,
    string? ShotReference = null // "last", "morning", etc.
);
```

### AddEquipmentParameters

```csharp
public record AddEquipmentParameters(
    string Name,
    EquipmentType Type,
    string? Notes = null
);
```

### AddProfileParameters

```csharp
public record AddProfileParameters(
    string Name
);
```

### NavigateParameters

```csharp
public record NavigateParameters(
    string Destination // "beans", "equipment", "profiles", "activity", "settings"
);
```

### QueryParameters

```csharp
public record QueryParameters(
    string QueryType, // "count", "average", "list"
    string Period = "today", // "today", "week", "month", "all"
    string? Filter = null
);
```

## Validation Rules

| Entity | Rule | Error Message |
|--------|------|---------------|
| LogShotParameters | DoseGrams > 0 | "Dose must be positive" |
| LogShotParameters | OutputGrams > 0 | "Output must be positive" |
| LogShotParameters | TimeSeconds > 0 | "Time must be positive" |
| LogShotParameters | Rating 0-4 if provided | "Rating must be 0-4" |
| AddBeanParameters | Name not empty | "Bean name is required" |
| AddBagParameters | BeanName not empty | "Bean name is required" |
| RateShotParameters | Rating 0-4 | "Rating must be 0-4" |
| AddEquipmentParameters | Name not empty | "Equipment name is required" |
| AddProfileParameters | Name not empty | "Profile name is required" |

## No Database Changes Required

This feature:
- ✅ Uses existing entity schemas (Shot, Bean, Bag, Equipment, UserProfile)
- ✅ All voice-related data is transient (in-memory only)
- ✅ No new tables or columns needed
- ✅ No migrations required

## Cross-Page Data Change Notification

Voice commands can modify data from any page. The affected pages must refresh to reflect changes.

### IDataChangeNotifier

```csharp
public interface IDataChangeNotifier
{
    /// <summary>
    /// Event raised when data changes occur (from voice commands or other sources).
    /// </summary>
    event EventHandler<DataChangedEventArgs>? DataChanged;
    
    /// <summary>
    /// Notifies subscribers that data has changed.
    /// </summary>
    void NotifyDataChanged(DataChangeType changeType, object? entity = null);
}

public class DataChangedEventArgs : EventArgs
{
    public DataChangeType ChangeType { get; }
    public object? Entity { get; }
    
    public DataChangedEventArgs(DataChangeType changeType, object? entity = null)
    {
        ChangeType = changeType;
        Entity = entity;
    }
}

public enum DataChangeType
{
    BeanCreated,
    BeanUpdated,
    BagCreated,
    BagUpdated,
    ShotCreated,
    ShotUpdated,
    ShotDeleted,
    EquipmentCreated,
    EquipmentUpdated,
    ProfileCreated,
    ProfileUpdated
}
```

### Page Refresh Pattern

Pages subscribe to `IDataChangeNotifier.DataChanged` and refresh relevant data:

| Page | Listens For | Refreshes |
|------|-------------|-----------|
| ShotLoggingPage | BeanCreated, BagCreated, EquipmentCreated, ProfileCreated | Bags picker, Equipment picker, Users picker |
| BeanManagementPage | BeanCreated, BeanUpdated | Beans list |
| EquipmentManagementPage | EquipmentCreated, EquipmentUpdated | Equipment list |
| ActivityFeedPage | ShotCreated, ShotUpdated, ShotDeleted | Shots list |
| UserProfileManagementPage | ProfileCreated, ProfileUpdated | Profiles list |

### Voice Command → UI Refresh Flow

```
┌─────────────────┐     ┌──────────────────────┐
│ User on         │     │ VoiceCommandService  │
│ ShotLoggingPage │     │                      │
└────────┬────────┘     └──────────┬───────────┘
         │                         │
         │ "Add bean Ethiopia      │
         │  from Counter Culture"  │
         │─────────────────────────▶
         │                         │
         │                         │ 1. AI interprets → AddBean tool
         │                         │ 2. Tool calls IBeanService.CreateAsync()
         │                         │ 3. Bean created in database
         │                         │
         │                         │ 4. NotifyDataChanged(BeanCreated, bean)
         │                         │─────────────────────────────────────┐
         │                         │                                     │
         │                         │    ┌────────────────────────┐       │
         │                         │    │ IDataChangeNotifier    │◀──────┘
         │◀────────────────────────│    │ broadcasts to all      │
         │ 5. OnDataChanged event  │    │ subscribed pages       │
         │                         │    └────────────────────────┘
         │
         │ 6. RefreshBagsAndBeansAsync()
         │    → Bags picker now shows new bean
         │
         │ 7. Feedback: "Added bean Ethiopia from Counter Culture"
         ▼
```
