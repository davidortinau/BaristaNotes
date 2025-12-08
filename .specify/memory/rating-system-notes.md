# Rating System Implementation Notes

## Rating Scale
**CRITICAL**: The application uses a **0-4 rating scale** (NOT 1-5):
- 0 = Terrible (Sentiment_very_dissatisfied)
- 1 = Bad (Sentiment_dissatisfied)
- 2 = Average (Sentiment_neutral)
- 3 = Good (Sentiment_satisfied)
- 4 = Excellent (Sentiment_very_satisfied)

## Rating Icons
All rating displays MUST use sentiment icons from MaterialSymbolsFont, stored centrally in:
- `BaristaNotes/Resources/Styles/AppIcons.cs::RatingIcons[]`
- Access via: `AppIcons.RatingIcons[index]` or `AppIcons.GetRatingIcon(rating)`

## Icon Usage Policy
**NEVER use emojis (☕, ⭐, etc.) in UI code.**
- Always use font icons from MaterialSymbolsFont
- Use custom font icons (coffee-icons font family)
- Only use PNG/SVG if explicitly specified by user

**Rationale**: Emojis render inconsistently across platforms (iOS, Android, Windows) and break screen reader accessibility.

## Locations Using Rating Icons
1. `BaristaNotes/Pages/ShotLoggingPage.cs` - Rating input selector
2. `BaristaNotes/Components/ShotRecordCard.cs` - Shot record display
3. `BaristaNotes/Components/RatingDisplayComponent.cs` - Aggregate rating distribution

## Data Model
- `ShotRecord.Rating` property: int (0-4)
- Database column: INTEGER NOT NULL
- Validation: Must be 0, 1, 2, 3, or 4

## Display Pattern
When showing rating aggregates:
- Show icon on the left (no numeric rating value)
- Show horizontal bar graph in middle
- Show shot count on the right
- Order from 4 (Excellent) down to 0 (Terrible)
