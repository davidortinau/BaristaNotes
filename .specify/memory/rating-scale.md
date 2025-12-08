# Rating Scale Reference

## Coffee Shot Rating System

**Scale**: 0-4 (5 levels)
**Icons**: MaterialSymbolsFont sentiment faces (NO EMOJIS - per Constitution)

### Rating Values and Icons
- 0 = Terrible → `MaterialSymbolsFont.Sentiment_very_dissatisfied`
- 1 = Bad → `MaterialSymbolsFont.Sentiment_dissatisfied`
- 2 = Average → `MaterialSymbolsFont.Sentiment_neutral`
- 3 = Good → `MaterialSymbolsFont.Sentiment_satisfied`
- 4 = Excellent → `MaterialSymbolsFont.Sentiment_very_satisfied`

### Icon Array Location
**Centralized in**: `BaristaNotes/Resources/Styles/AppIcons.cs`
```csharp
public static readonly string[] RatingIcons = new[]
{
    MaterialSymbolsFont.Sentiment_very_dissatisfied, // 0
    MaterialSymbolsFont.Sentiment_dissatisfied,      // 1
    MaterialSymbolsFont.Sentiment_neutral,           // 2
    MaterialSymbolsFont.Sentiment_satisfied,         // 3
    MaterialSymbolsFont.Sentiment_very_satisfied     // 4
};
```

**Used by**:
- `ShotLoggingPage.cs` - Rating input
- `ShotRecordCard.cs` - Rating display on shot cards
- `RatingDisplayComponent.cs` - Aggregate rating display

### Important Rules
- The scale is **0-4**, NOT 1-5
- Always use 0 as the minimum value
- Always use 4 as the maximum value
- **NEVER use emojis (☕, ⭐, etc.)** - Always use MaterialSymbolsFont icons
- Database stores values as integers from 0 to 4
- Display format: "2.9 / 4" (1 decimal place, showing max value of 4)

### Usage Examples

**Input Component (ShotLoggingPage)**:
- Shows 5 sentiment face icons (representing 0, 1, 2, 3, 4)
- User taps an icon to select that rating level
- Filled icons up to selected value

**Display Component (RatingDisplayComponent)**:
- Shows aggregate ratings by level (0, 1, 2, 3, 4) with distribution bars
- Shows average rating: "2.9 / 4" (1 decimal place)
- Shows total shot count: "8 shots" (NOT "8 rated shots (8 total)" - all shots are rated)
- Each distribution bar shows the sentiment icon on the left, progress bar in middle, count on right

**Bag Summary Display (BeanDetailPage)**:
- Shows bag rating with icon: "2.9" + sentiment face icon
- Icon matches the rounded average (e.g., 2.9 rounds to 3, shows satisfied face)

### Database Schema
- Rating column in Shots table is INTEGER with CHECK constraint (Rating >= 0 AND Rating <= 4)
- All shots MUST have a rating (no NULL values)
- Original beans had DateTimeOffset roast dates but SQLite requires DateTime conversion
