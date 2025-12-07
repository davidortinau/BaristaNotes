# Research: Bean Rating Tracking and Bag Management

**Feature**: 001-bean-rating-tracking  
**Phase**: 0 - Research & Decision Documentation  
**Date**: 2025-12-07

## Purpose

Resolve all "NEEDS CLARIFICATION" items from Technical Context to enable Phase 1 design with confidence.

## Research Areas

### 1. Bean-Bag Data Model Migration Strategy

**Question**: How to migrate existing Beans (with RoastDate) to new Bag entity while preserving existing shot associations?

**Research**:
- Current state: Bean.RoastDate exists, ShotRecord.BeanId FK exists
- Target state: Bean (no RoastDate) → Bag (has RoastDate) → ShotRecord.BagId FK
- EF Core migration capabilities: Can generate data migrations, not just schema migrations
- Constitution requirement: "EF Core Migrations Only - ALL database schema changes MUST be made using dotnet ef migrations add"

**Decision**: Multi-step EF Core migration with automatic data seeding

**Rationale**:
1. **Zero-downtime**: Migration runs on app startup via `MigrateAsync()` (already in use per constitution)
2. **Data preservation**: EF migration's `Up()` method creates Bag for each existing Bean, copies RoastDate, updates ShotRecord.BagId
3. **Backward compatibility**: Keep Bean.RoastDate temporarily as nullable during migration window, mark [Obsolete] in code
4. **Rollback safety**: `Down()` migration reverses: copies Bag.RoastDate back to Bean, restores BeanId FK

**Migration Steps** (in EF Migration Up() method):
```csharp
// 1. Add Bag table
migrationBuilder.CreateTable(name: "Bags", ...);

// 2. Add BagId to ShotRecords (nullable initially)
migrationBuilder.AddColumn<int>("BagId", "ShotRecords", nullable: true);

// 3. Seed Bags: For each Bean with RoastDate, create corresponding Bag
migrationBuilder.Sql(@"
    INSERT INTO Bags (BeanId, RoastDate, IsActive, CreatedAt, SyncId, LastModifiedAt, IsDeleted)
    SELECT Id, RoastDate, IsActive, CreatedAt, 
           lower(hex(randomblob(16))), LastModifiedAt, IsDeleted
    FROM Beans WHERE RoastDate IS NOT NULL
");

// 4. Update ShotRecords: Set BagId where Bean has a Bag (via RoastDate match)
migrationBuilder.Sql(@"
    UPDATE ShotRecords 
    SET BagId = (SELECT Id FROM Bags WHERE Bags.BeanId = ShotRecords.BeanId LIMIT 1)
    WHERE BeanId IN (SELECT Id FROM Beans WHERE RoastDate IS NOT NULL)
");

// 5. Make BagId required, add FK constraint
migrationBuilder.AlterColumn<int>("BagId", "ShotRecords", nullable: false);
migrationBuilder.CreateIndex("IX_ShotRecords_BagId", "ShotRecords", "BagId");
migrationBuilder.AddForeignKey("FK_ShotRecords_Bags_BagId", "ShotRecords", "BagId", "Bags", "Id");

// 6. Drop old BeanId FK from ShotRecords
migrationBuilder.DropForeignKey("FK_ShotRecords_Beans_BeanId", "ShotRecords");
migrationBuilder.DropColumn("BeanId", "ShotRecords");

// 7. Drop RoastDate from Beans
migrationBuilder.DropColumn("RoastDate", "Beans");
```

**Alternatives Considered**:
- Manual SQL scripts: Rejected per constitution "Never Manually Modify Schema"
- Two separate migrations: Rejected - atomicity lost, rollback complexity
- Keep both BeanId and BagId: Rejected - data duplication, consistency issues

**Risks & Mitigation**:
- **Risk**: Large datasets may timeout during migration
  - **Mitigation**: Test with production-scale data (1000 beans, 10000 shots) in dev
- **Risk**: Beans without RoastDate (edge case) have no Bag
  - **Mitigation**: Create default Bag with current date for orphaned Beans, log warning

---

### 2. Rating Calculation Strategy

**Question**: Should aggregate ratings be calculated on-demand (LINQ queries) or denormalized/cached (stored columns)?

**Research**:
- Calculation frequency: Every time Bean/Bag detail page loads
- Data volume: Typical user ~50 beans, ~500 shots/year, ~5 shots per bag average
- Performance target: <500ms per NFR-P2
- EF Core capabilities: LINQ translates to SQL efficiently for aggregates (AVG, COUNT, GROUP BY)
- Constitution: "Database Queries: All queries MUST use appropriate indexes. N+1 queries are prohibited."

**Decision**: On-demand calculation with optimized LINQ queries and strategic indexing

**Rationale**:
1. **Data consistency**: Always accurate, no stale cache issues
2. **Simplicity**: No triggers, no denormalization maintenance, follows constitution "Single Responsibility"
3. **Performance**: Indexed queries on ShotRecord.BagId + ShotRecord.Rating meet <500ms target
   - Bean-level: `SELECT BagId, AVG(Rating), COUNT(*) FROM ShotRecords WHERE BagId IN (SELECT Id FROM Bags WHERE BeanId = ?) GROUP BY BagId`
   - Single query with JOIN, indexed on BagId and Rating columns
4. **Scalability**: 100 shots * 5 grouping levels (1-5 stars) = trivial aggregation workload for SQLite

**Query Pattern**:
```csharp
// Bean-level aggregate (via repository)
var beanRating = await _context.ShotRecords
    .Where(s => s.Bag.BeanId == beanId && s.Rating != null)
    .GroupBy(s => s.Rating)
    .Select(g => new { Rating = g.Key, Count = g.Count() })
    .ToListAsync();

var average = await _context.ShotRecords
    .Where(s => s.Bag.BeanId == beanId && s.Rating != null)
    .AverageAsync(s => s.Rating.Value);
```

**Indexes Required**:
- `CREATE INDEX IX_ShotRecords_BagId_Rating ON ShotRecords (BagId, Rating)` (composite)
- `CREATE INDEX IX_Bags_BeanId ON Bags (BeanId)`

**Alternatives Considered**:
- **Cached columns** (e.g., Bean.AverageRating, Bean.TotalShots): 
  - Rejected: Requires triggers or service-layer updates on every shot CRUD
  - Rejected: Violates "Single Responsibility" - Bean shouldn't store computed data
- **Materialized view**: 
  - Rejected: SQLite doesn't support materialized views natively
  - Rejected: Manual refresh adds complexity
- **Separate RatingAggregate table**:
  - Rejected: Over-engineering for small dataset
  - Rejected: Would need event-driven updates (complexity not justified)

**Performance Validation**:
- Benchmark with 100 beans, 500 shots in dev environment
- Monitor with instrumentation (EF Core query logging)
- Load test: 10 concurrent bean detail views <2s (includes network/rendering)

---

### 3. Rating Distribution Storage

**Question**: Store distribution as JSON column, separate RatingDistribution table, or compute on-the-fly?

**Research**:
- Data structure: Dictionary<int, int> (rating → count), e.g., {5: 10, 4: 5, 3: 2, 2: 1, 1: 0}
- Update frequency: On every shot create/update/delete
- Query frequency: On every bean/bag detail view
- EF Core 10.0: Supports JSON columns in SQLite via `HasConversion<string>()`

**Decision**: Compute on-the-fly (same query as average rating calculation)

**Rationale**:
1. **Consistency with Decision #2**: Already computing aggregate rating on-demand
2. **Single query**: GROUP BY clause provides both average AND distribution
   ```csharp
   var distribution = await _context.ShotRecords
       .Where(s => s.Bag.BeanId == beanId && s.Rating != null)
       .GroupBy(s => s.Rating.Value)
       .Select(g => new RatingDistributionItem { Rating = g.Key, Count = g.Count() })
       .ToListAsync();
   ```
3. **No additional storage**: DTO constructed from query result
4. **Performance**: Same indexed query, O(n) scan with GROUP BY (acceptable for <100 shots per bean)

**DTO Pattern**:
```csharp
public class RatingAggregateDto
{
    public double AverageRating { get; set; }
    public int TotalShots { get; set; }
    public Dictionary<int, int> Distribution { get; set; } // 1-5 → count
}
```

**Alternatives Considered**:
- **JSON column in Bean/Bag table**: 
  - Rejected: Must update on every shot change (event-driven complexity)
  - Rejected: JSON querying in SQLite less performant than indexed columns
- **Separate RatingDistribution table** (BeanId, Rating, Count):
  - Rejected: CRUD overhead (insert/update on every shot change)
  - Rejected: Additional table complicates schema for minimal benefit
- **Cached in-memory** (app-level cache):
  - Rejected: Offline-first app, multi-device sync invalidation issues
  - Rejected: Premature optimization

---

### 4. Bag Selection UX Integration

**Question**: How does user select which bag when logging a shot? Must integrate with existing ShotLoggingPage.cs (44KB, complex Reactor component).

**Updated Requirement** (from user clarification): Shot logging should be **bag-first**, not bean-first. Users select a bag directly, and the bean information is derived from the selected bag. Completed bags should not appear in the picker.

**Research**:
- Current ShotLoggingPage: Uses Reactor.Maui reactive state management
- Existing patterns: Picker/dropdown components (check BeanService, EquipmentService usage)
- Reactor.Maui patterns: Component composition, state flows via RxUI observables
- Constitution: "UX Consistency: Design system components MUST be used"

**Decision**: Single bag picker showing active (incomplete) bags only, with bean name included in display label

**Rationale**:
1. **User-centric workflow**: Users think in terms of "which bag am I using" not "which bean + which bag"
2. **Simplified UX**: One picker instead of two-stage selection reduces cognitive load
3. **Completion filtering**: Only show bags marked as incomplete (IsComplete=false), keeping UI clean
4. **Bean context automatic**: Bean name displayed in bag picker label (e.g., "Ethiopian Yirgacheffe - Roasted Dec 05, 2025")
5. **Default selection**: Pre-select most recent active bag by RoastDate DESC

**UX Flow**:
```
1. User taps "Log Shot"
2. Bag picker loads active bags (IsComplete=false, IsDeleted=false, IsActive=true)
   → Bags displayed with format: "{BeanName} - Roasted {Date} [- Notes]"
   → Sorted by RoastDate DESC (newest first)
   → Pre-select most recent bag
3. IF no active bags exist:
      Show "No active bags" message with "Add New Bag" button
   ELSE:
      Display selected bag's bean info (name, roaster, origin) below picker
4. Continue with shot parameters (grind, time, etc.)
5. Save shot with BagId (not BeanId)
```

**Component Pattern** (Reactor.Maui):
```csharp
new VStack
{
    new Label("Select Bag").FontWeight(FontWeights.SemiBold),
    
    RenderIf(state.ActiveBags.Count > 0,
        new Picker()
            .ItemsSource(state.ActiveBags, bag => bag.DisplayLabel) // "Bean - Roasted Date - Notes"
            .SelectedItem(state.SelectedBag)
            .OnSelectedItemChanged(async bag => {
                SetState(s => s.SelectedBag = bag);
                // Bean info auto-displayed from bag.BeanName, bag.Bean properties
            })
    ),
    
    RenderIf(state.ActiveBags.Count == 0,
        new VStack(spacing: 8)
        {
            new Label("No active bags available").Opacity(0.7),
            new Button("Add New Bag")
                .OnClicked(async () => await NavigateTo<BagFormPage>())
        }
    ),
    
    // Auto-display bean info from selected bag
    RenderIf(state.SelectedBag != null,
        new VStack(spacing: 4).Padding(8).BackgroundColor(Colors.LightGray.WithAlpha(0.1f))
        {
            new Label($"Bean: {state.SelectedBag.BeanName}").FontSize(14),
            new Label($"{state.SelectedBag.ShotCount} shots logged, avg {state.SelectedBag.FormattedRating}").FontSize(12).Opacity(0.7)
        }
    )
}
```

**BagSummaryDto.DisplayLabel format**:
```csharp
public string DisplayLabel => 
    $"{BeanName} - Roasted {FormattedRoastDate}" + 
    (Notes != null ? $" - {Notes}" : "");

// Example outputs:
// "Ethiopian Yirgacheffe - Roasted Dec 05, 2025"
// "Colombian Supremo - Roasted Nov 28, 2025 - From Trader Joe's"
```

**Completion Workflow**:
```
1. User finishes a bag
2. Navigate to Bag Detail page (from bean detail or shot history)
3. Tap "Mark as Complete" button
4. Bag.IsComplete = true
5. Bag no longer appears in shot logging picker
6. Bag remains visible in bean detail "Bags" history section (with "Complete" badge)
7. User can "Reactivate" bag if needed (IsComplete = false)
```

**Alternatives Considered**:
- **Two-stage: Bean → Bag selection** (original design): 
  - Rejected: User feedback clarified bag-first workflow is more intuitive
  - Rejected: Extra step slows down most frequent operation (logging shots)
- **Show completed bags with disabled state**:
  - Rejected: Visual clutter, users don't want to see finished bags in picker
  - Kept in history views: Completed bags visible in bean detail for historical reference
- **Bean-only selection (no bag concept)**:
  - Rejected: Core requirement is tracking multiple bags per bean
- **Modal/BottomSheet for bag selection**: 
  - Rejected: Adds navigation step, standard Picker is sufficient

**Accessibility**:
- Picker has implicit keyboard navigation (MAUI standard control)
- Label text announces picker purpose for screen readers
- 44px touch target per MAUI Picker default height

---

### 5. Empty State Handling

**Question**: UI patterns for beans with no bags, bags with no shots, etc. Must align with existing design system.

**Research**:
- Existing patterns: Check BeanManagementPage.cs, EquipmentManagementPage.cs for empty state handling
- Reactor.Maui: Conditional rendering via `RenderIf()` or ternary in component tree
- Design system: Likely uses CommunityToolkit.Maui components (check Dependencies)

**Decision**: Inline empty state messages with call-to-action buttons (consistent with assumed existing pattern)

**Rationale**:
1. **User guidance**: Clear messaging + actionable next step
2. **Component consistency**: Reuse VStack/Label/Button patterns from existing pages
3. **No modal blocking**: Inline messages don't interrupt navigation flow

**Empty State Patterns**:

**A. Bean with no bags** (BeanDetailPage):
```csharp
RenderIf(bags.Count == 0,
    new VStack(spacing: 16)
        .Padding(24)
        .VCenter()
    {
        new Label("No bags logged yet")
            .FontSize(18)
            .FontWeight(FontWeights.Medium)
            .HCenter(),
        new Label("Add a bag to start tracking shots for this bean")
            .FontSize(14)
            .Opacity(0.7)
            .HCenter()
            .TextAlignment(TextAlignment.Center),
        new Button("Add First Bag")
            .OnClicked(async () => await NavigateTo<BagFormPage>(beanId))
    }
)
```

**B. Bag with no shots** (BagDetailPage, rating section):
```csharp
RenderIf(shots.Count == 0,
    new VStack(spacing: 8).Padding(16)
    {
        new Label("No ratings yet")
            .FontSize(16)
            .FontWeight(FontWeights.Medium),
        new Label("Log your first shot to see ratings here")
            .FontSize(14)
            .Opacity(0.7)
    }
)
```

**C. Bean list with no beans** (BeanManagementPage - likely already exists):
```csharp
// Assume existing pattern, ensure consistency
RenderIf(beans.Count == 0,
    new Label("No beans added yet. Tap + to add your first bean.")
        .HCenter()
        .VCenter()
)
```

**Alternatives Considered**:
- **Placeholder graphics/icons**: 
  - Deferred: Design system unclear, focus on functional MVP
  - Future enhancement: Add coffee bean icon SVG
- **Toast notifications**:
  - Rejected: Empty state is not an error, doesn't require dismissal
- **Wizard flow**:
  - Rejected: Over-engineering, users understand "add" buttons

**Accessibility**:
- Empty state labels have semantic text (screen reader announces)
- Buttons have accessible labels ("Add First Bag" vs. generic "Add")
- Sufficient color contrast (check against WCAG 2.1 AA in implementation)

---

## Technology Best Practices

### EF Core 10.0 + SQLite

**Best Practices Applied**:
1. **Migrations**: Use `dotnet ef migrations add` exclusively (per constitution)
2. **Indexes**: Composite index on (BagId, Rating) for aggregate queries
3. **Lazy loading**: Disabled (avoid N+1), use explicit `.Include()` for navigation properties
4. **Connection pooling**: SQLite doesn't pool, single connection per app instance (mobile pattern)
5. **Async queries**: Always use `ToListAsync()`, `FirstOrDefaultAsync()` for UI thread safety

**Performance Patterns**:
```csharp
// GOOD: Single query with Include
var bean = await _context.Beans
    .Include(b => b.Bags)
        .ThenInclude(bag => bag.ShotRecords)
    .FirstOrDefaultAsync(b => b.Id == beanId);

// BAD: N+1 query (lazy loading)
var bean = await _context.Beans.FirstAsync(b => b.Id == beanId);
var bags = bean.Bags; // Triggers separate query per bag
```

### Reactor.Maui 4.0.3-beta

**Best Practices Applied**:
1. **State management**: Use component-level state for UI-specific state, services for shared state
2. **Component composition**: Extract reusable components (BagSelectorComponent, RatingDisplayComponent)
3. **Reactivity**: Leverage RxUI observables for bean→bags cascading updates
4. **Hot reload**: Structure components for fast iteration (small, focused components)

**Component Pattern**:
```csharp
class BagSelectorComponent : Component<BagSelectorState>
{
    public override VisualNode Render()
        => new VStack
        {
            new Picker()
                .ItemsSource(State.Bags, FormatBagLabel)
                .SelectedItem(State.SelectedBag)
                .OnSelectedItemChanged(bag => SetState(s => s.SelectedBag = bag))
        };
}
```

### .NET MAUI Cross-Platform

**Best Practices Applied**:
1. **Responsive layout**: Use VStack/HStack with spacing, avoid absolute positioning
2. **Platform-specific**: Defer iOS/Android customization to Platforms/ folder if needed
3. **Touch targets**: Ensure 44px minimum for all interactive elements (Button, Picker)
4. **Offline-first**: All data operations via local SQLite, no network dependencies for this feature

---

## Integration Points

### 1. Existing Services

**BeanService** (`BaristaNotes.Core/Services/BeanService.cs`):
- Current: CRUD operations for Bean entity
- Modification: Add `GetWithRatingsAsync(int beanId)` method
- New dependency: Inject `IRatingService` for aggregate calculation

**ShotService** (`BaristaNotes.Core/Services/ShotService.cs`):
- Current: CRUD operations for ShotRecord, expects `BeanId` parameter
- **Major Change**: Shot logging now uses `BagId` as primary parameter (not BeanId)
- Modification: Change all shot creation/update signatures to accept `BagId`
- Validation: Bag exists, is active (IsComplete=false), and not deleted
- Cascade: On shot delete, ratings recalculated on-demand (no explicit trigger needed)

### 2. Existing UI Pages

**BeanDetailPage** (`BaristaNotes/Pages/BeanDetailPage.cs`):
- Current: Displays bean details (name, roaster, origin, notes), lists shot records
- Modification: 
  - Add rating display component (aggregate for bean)
  - Add "Bags" section with expandable list (roast date, shot count, bag-level rating)
  - Add "Add Bag" button

**ShotLoggingPage** (`BaristaNotes/Pages/ShotLoggingPage.cs`):
- Current: Multi-step form for shot parameters (bean, machine, grinder, dose, time, output, rating)
- **Major Change**: Replace bean selection with bag selection as primary picker
- Modification:
  - **Remove** bean picker (or make it secondary/optional for filtering)
  - **Add** bag picker showing active bags only (via `GetActiveBagsForShotLoggingAsync()`)
  - Bag picker displays: "{BeanName} - Roasted {Date} [- Notes]"
  - Auto-display bean info from selected bag (bean name, roaster shown below picker)
  - Update service call to use `BagId` instead of `BeanId`
  - Handle empty state: "No active bags" → prompt to add bag

### 3. Database Context

**BaristaNotesContext** (`BaristaNotes.Core/Data/BaristaNotesContext.cs`):
- Current: Configures Equipment, Bean, ShotRecord, UserProfile entities
- Modification:
  - Add `DbSet<Bag> Bags`
  - Add `OnModelCreating` configuration for Bag entity (relationships, indexes)
  - Update ShotRecord configuration (change FK from BeanId to BagId)

---

## Summary of Decisions

| Research Area | Decision | Rationale |
|---------------|----------|-----------|
| Data Migration | Multi-step EF Core migration with data seeding | Constitution compliance, zero-downtime, rollback safety |
| Rating Calculation | On-demand LINQ queries with indexing | Data consistency, simplicity, meets <500ms target |
| Distribution Storage | Compute on-the-fly (GROUP BY query) | Same query as average, no denormalization complexity |
| Bag Selection UX | **Single bag picker** (bag-first, not bean-first) with completion filtering | User clarification: bag is primary selector, bean derived. Only active (incomplete) bags shown. Simpler workflow. |
| Bag Completion | IsComplete boolean flag (separate from IsActive/IsDeleted) | Clear semantics: Complete=finished/empty, Active=soft-delete, Deleted=permanent. Allows reactivation. |
| Empty States | Inline messages with CTA buttons | User guidance, existing pattern consistency |

**All NEEDS CLARIFICATION items resolved. Ready for Phase 1: Design & Contracts.**
