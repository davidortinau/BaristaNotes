# Data Model: AI Shot Improvement Advice

**Feature**: 001-ai-shot-advice  
**Date**: 2025-12-09

## Entity Changes

### ShotRecord (Modified)

**Existing Entity** - Adding one new field

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Id | int | Yes | Primary key (existing) |
| Timestamp | DateTime | Yes | When shot was logged (existing) |
| BagId | int | Yes | FK to Bag (existing) |
| MachineId | int? | No | FK to Equipment (existing) |
| GrinderId | int? | No | FK to Equipment (existing) |
| MadeById | int? | No | FK to UserProfile (existing) |
| MadeForId | int? | No | FK to UserProfile (existing) |
| DoseIn | decimal | Yes | Grams of coffee in (existing) |
| GrindSetting | string | Yes | Grinder setting (existing) |
| ExpectedTime | decimal | Yes | Target extraction time (existing) |
| ExpectedOutput | decimal | Yes | Target yield (existing) |
| DrinkType | string | Yes | Espresso, Latte, etc. (existing) |
| ActualTime | decimal? | No | Actual extraction time (existing) |
| ActualOutput | decimal? | No | Actual yield (existing) |
| PreinfusionTime | decimal? | No | Pre-infusion duration (existing) |
| Rating | int? | No | 0-4 rating scale (existing) |
| **TastingNotes** | **string?** | **No** | **NEW: Optional free-text tasting notes** |
| SyncId | Guid | Yes | CoreSync metadata (existing) |
| LastModifiedAt | DateTime | Yes | CoreSync metadata (existing) |
| IsDeleted | bool | Yes | Soft delete flag (existing) |

**Migration Required**: Yes - Add nullable TastingNotes column

---

## New DTOs

### AIAdviceRequestDto

Used to gather context for AI advice request.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| ShotId | int | Yes | The shot to get advice for |
| CurrentShot | ShotContextDto | Yes | Current shot details |
| HistoricalShots | List&lt;ShotContextDto&gt; | No | Previous shots for same bag |
| BeanInfo | BeanContextDto | Yes | Bean and roast information |
| Equipment | EquipmentContextDto? | No | Machine and grinder if logged |

### ShotContextDto

Simplified shot data for AI context.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| DoseIn | decimal | Yes | Grams in |
| ActualOutput | decimal? | No | Grams out |
| ActualTime | decimal? | No | Extraction time in seconds |
| GrindSetting | string | Yes | Grinder setting |
| Rating | int? | No | 0-4 rating |
| TastingNotes | string? | No | User's tasting notes |
| Timestamp | DateTime | Yes | When shot was logged |

### BeanContextDto

Bean information for AI context.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Name | string | Yes | Bean name |
| Roaster | string? | No | Roaster name |
| Origin | string? | No | Coffee origin |
| RoastDate | DateTime | Yes | When beans were roasted |
| DaysFromRoast | int | Yes | Calculated: days since roast |
| Notes | string? | No | Bean notes |

### EquipmentContextDto

Equipment information for AI context.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| MachineName | string? | No | Espresso machine name |
| GrinderName | string? | No | Grinder name |

### AIAdviceResponseDto

Response from AI service.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| Success | bool | Yes | Whether advice was generated |
| Advice | string? | No | The AI-generated advice text |
| ErrorMessage | string? | No | Error message if failed |
| GeneratedAt | DateTime | Yes | Timestamp of response |

---

## Relationships

```
ShotRecord (modified)
    └── TastingNotes (new nullable string field)

No new relationships - using existing:
    ShotRecord → Bag → Bean
    ShotRecord → Equipment (Machine)
    ShotRecord → Equipment (Grinder)
```

---

## Validation Rules

### TastingNotes
- Maximum length: 500 characters
- No required format (free text)
- Nullable (optional field)
- No profanity filter (user's private notes)

### AIAdviceRequestDto
- ShotId must reference existing shot
- CurrentShot must have valid shot data
- HistoricalShots limited to most recent 10 for context

---

## State Transitions

N/A - No state machine for this feature. AI advice is a stateless request/response.

---

## Migration Plan

### Migration: AddTastingNotesToShotRecord

```csharp
public partial class AddTastingNotesToShotRecord : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "TastingNotes",
            table: "ShotRecords",
            type: "TEXT",
            maxLength: 500,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "TastingNotes",
            table: "ShotRecords");
    }
}
```

**Data Preservation**: No data transformation needed. All existing records will have NULL for TastingNotes.
