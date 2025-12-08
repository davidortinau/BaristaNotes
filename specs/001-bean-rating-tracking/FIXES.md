# Fixes Applied - 2025-12-07

## Issue: SQLite DateTimeOffset Compatibility

**Problem**: Runtime error "sqlite doesn't support types DateTimeOffset"

**Root Cause**: 
- Data model specification used `DateTimeOffset` 
- SQLite only supports `DateTime` natively
- Entities (Bean, Bag, ShotRecord) were using `DateTime` correctly
- Migration was already correct (using `DateTime`)

**Resolution**: 
- Confirmed entities use `DateTime` (not `DateTimeOffset`)
- Database schema uses TEXT for date storage (SQLite standard)
- No changes needed - schema already correct

## Issue: Migration "Beans table already exists"

**Problem**: Runtime error "the table beans already exists"

**Root Cause**:
- InitialCreate migration already includes Beans and Bags tables
- No separate AddBagEntity migration needed (fresh project start)
- Database file had stale schema

**Resolution**:
- Deleted existing database file: `rm -rf BaristaNotes.Core/barista.db*`
- Updated tasks.md to reflect that InitialCreate includes Bags
- No data migration needed (fresh start)

## Build Status

✅ Core project builds successfully (0 errors, 0 warnings)
✅ MAUI app builds successfully (0 errors, 0 warnings)
✅ Database schema ready for runtime testing

## Next Steps

Continue with Phase 4 UI implementation:
- Fix BagPickerComponent using proper MauiReactor ThemeKey patterns
- Test bag-based shot logging workflow
- Verify bag completion toggle
