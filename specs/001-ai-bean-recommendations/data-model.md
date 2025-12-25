# Data Model: AI Bean Recommendations

**Feature**: 001-ai-bean-recommendations  
**Date**: 2024-12-24

## Overview

This feature extends existing entities with no schema changes. New DTOs support AI recommendation requests and responses.

## Existing Entities (No Changes Required)

### Bean

| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| Name | string | Bean name |
| Roaster | string? | Roaster name |
| Origin | string? | Country/region of origin |
| Notes | string? | Flavor profile notes |
| IsActive | bool | Soft delete flag |

**Usage**: Source bean characteristics for new-bean recommendations.

### Bag

| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| BeanId | int | Foreign key to Bean |
| RoastDate | DateTime | Date beans were roasted |
| Notes | string? | Bag-specific notes |
| IsActive | bool | Soft delete flag |

**Usage**: Links beans to shot records; provides roast date for freshness context.

### ShotRecord

| Field | Type | Description |
|-------|------|-------------|
| Id | int | Primary key |
| BagId | int | Foreign key to Bag |
| DoseIn | decimal | Input dose in grams |
| GrindSetting | string | Grinder setting |
| ExpectedOutput | decimal | Target output in grams |
| ExpectedTime | decimal | Target extraction time in seconds |
| ActualOutput | decimal? | Actual output in grams |
| ActualTime | decimal? | Actual extraction time in seconds |
| Rating | int? | 0-4 rating scale |
| Timestamp | DateTime | When shot was logged |

**Usage**: Historical data source for returning-bean recommendations.

## New DTOs

### AIRecommendationDto (Response)

| Field | Type | Description |
|-------|------|-------------|
| Success | bool | Whether recommendation was generated |
| Dose | decimal | Recommended dose in grams |
| GrindSetting | string | Recommended grinder setting |
| Output | decimal | Recommended output in grams |
| Duration | decimal | Recommended extraction time in seconds |
| RecommendationType | RecommendationType | NewBean or ReturningBean |
| Confidence | string? | Optional confidence indicator |
| ErrorMessage | string? | Error details if Success is false |
| Source | string? | "via Apple Intelligence" or "via OpenAI" |

### RecommendationType (Enum)

| Value | Description |
|-------|-------------|
| NewBean | Bean has no shot history; recommendation based on bean characteristics |
| ReturningBean | Bean has history; recommendation based on user's past shots |

### BeanRecommendationContextDto (Internal)

| Field | Type | Description |
|-------|------|-------------|
| BeanId | int | Bean identifier |
| BeanName | string | Bean name |
| Roaster | string? | Roaster name |
| Origin | string? | Origin country/region |
| Notes | string? | Flavor profile |
| RoastDate | DateTime? | Most recent bag's roast date |
| DaysFromRoast | int? | Days since roast |
| HasHistory | bool | Whether shots exist for this bean |
| HistoricalShots | List<ShotContextDto>? | Up to 10 best-rated shots (if HasHistory) |
| Equipment | EquipmentContextDto? | Current machine/grinder |

## Entity Relationships

```
Bean (1) ─────┬───── (*) Bag (1) ───── (*) ShotRecord
              │
              │  [Query: Get all shots for a bean across all bags]
              │
              └── BeanRecommendationContextDto (computed)
                  ├── Bean characteristics (direct)
                  ├── RoastDate (from most recent Bag)
                  └── HistoricalShots (from ShotRecord via Bag)
```

## Query Patterns

### Check if Bean Has History

```text
SELECT COUNT(*) FROM ShotRecords sr
JOIN Bags b ON sr.BagId = b.Id
WHERE b.BeanId = @beanId AND sr.IsDeleted = 0
```

### Get Most Recent Bean

```text
SELECT b.BeanId FROM ShotRecords sr
JOIN Bags b ON sr.BagId = b.Id
WHERE sr.IsDeleted = 0
ORDER BY sr.Timestamp DESC
LIMIT 1
```

### Get Historical Shots for Bean

```text
SELECT sr.* FROM ShotRecords sr
JOIN Bags b ON sr.BagId = b.Id
WHERE b.BeanId = @beanId AND sr.IsDeleted = 0
ORDER BY sr.Rating DESC, sr.Timestamp DESC
LIMIT 10
```

## State Transitions

### Recommendation Request Lifecycle

```
[Bean Selected] → [Check History] → [Build Context] → [Call AI] → [Populate Fields]
       │                │                  │               │              │
       ▼                ▼                  ▼               ▼              ▼
  Cancel Previous   NewBean or      BeanContext +     Loading Bar    Toast Message
     Request       ReturningBean    HistoricalShots    Visible        Displayed
```

### Error States

| State | Trigger | Recovery |
|-------|---------|----------|
| AI Unavailable | Service timeout/error | Error toast, manual entry |
| Partial Response | AI returns incomplete data | Populate available values, default others |
| Cancelled | User switched beans | Discard response, start new request |

## Validation Rules

| Field | Rule | Enforcement |
|-------|------|-------------|
| Dose | 0.1 - 30.0g | AI prompt constraints + UI validation |
| GrindSetting | Non-empty string | AI prompt constraints |
| Output | 0.1 - 100.0g | AI prompt constraints + UI validation |
| Duration | 1 - 120 seconds | AI prompt constraints + UI validation |

## Data Volume Assumptions

- Historical shots per bean: 1-100 (typical: 10-30)
- Beans per user: 5-50 (typical: 10-20)
- Query performance: <50ms for history check and context building
