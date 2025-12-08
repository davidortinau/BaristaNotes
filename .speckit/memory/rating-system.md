# BaristaNotes Rating System

## Critical Information

**RATING SCALE: 0-4 (NOT 1-5)**

The app uses a **0-4 integer rating scale** with 5 levels:
- 0 = Terrible ☕
- 1 = Bad ☕
- 2 = Average ☕
- 3 = Good ☕
- 4 = Excellent ☕

## Icon Usage

- Coffee cup emoji: ☕
- Used in both rating input (ShotLoggingPage) and display (RatingDisplayComponent)
- Number of cups matches the rating value (0-4 cups)

## Database

- `Shots.Rating` column: INTEGER, nullable
- Stored as 0, 1, 2, 3, or 4
- NULL = no rating provided

## Display Format

- Average ratings: Displayed as decimal (e.g., "3.2")
- Individual ratings: Displayed as integers with coffee cups
- Distribution bars: Show count per rating level (4 down to 0)

## Remember

ALWAYS use 0-4 scale when:
- Creating rating inputs
- Displaying ratings
- Calculating aggregates
- Writing tests
- Generating sample data
