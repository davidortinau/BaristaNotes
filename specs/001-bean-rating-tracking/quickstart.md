# Quickstart: Bean Rating Tracking and Bag Management

**Feature**: 001-bean-rating-tracking  
**Audience**: Developers implementing this feature  
**Prerequisites**: .NET 10.0 SDK, Visual Studio 2022+ or VS Code with C# extension

## Overview

This feature adds a two-level rating system:
1. **Bag entity**: Separates physical coffee bags from bean varieties
2. **Rating aggregates**: Bean-level (all bags) and bag-level (single bag) statistics
3. **UI enhancements**: Rating display, bag selection, bag management

**Key concepts**:
- **Bean**: Coffee variety (e.g., "Ethiopian Yirgacheffe")
- **Bag**: Physical bag with roast date (many bags can reference same bean)
- **Shot**: Now links to Bag (was Bean), includes optional 1-5 star rating
- **Aggregates**: Computed on-demand from ShotRecord queries (not stored)

## Architecture Overview

```
┌────────────────────────────────────────────────────────────┐
│  BaristaNotes (MAUI App)                                   │
│  ├─ Pages/                                                 │
│  │  ├─ BeanDetailPage.cs       (modified: add ratings)    │
│  │  ├─ BagDetailPage.cs        (new: bag-specific view)   │
│  │  ├─ BagFormPage.cs          (new: add bag for bean)    │
│  │  └─ ShotLoggingPage.cs      (modified: bag selection)  │
│  └─ Components/                                            │
│     ├─ RatingDisplayComponent.cs  (new: stars + bars)     │
│     └─ BagSelectorComponent.cs    (new: bag picker)       │
└────────────────────────────────────────────────────────────┘
                          ▼ Uses
┌────────────────────────────────────────────────────────────┐
│  BaristaNotes.Core (Business Logic)                       │
│  ├─ Models/                                                │
│  │  ├─ Bean.cs          (modified: remove RoastDate)      │
│  │  ├─ Bag.cs           (new: physical bag entity)        │
│  │  └─ ShotRecord.cs    (modified: BagId FK)              │
│  ├─ Services/                                              │
│  │  ├─ IBagService.cs / BagService.cs       (new)         │
│  │  ├─ IRatingService.cs / RatingService.cs (new)         │
│  │  ├─ IBeanService.cs / BeanService.cs     (modified)    │
│  │  └─ IShotService.cs / ShotService.cs     (modified)    │
│  ├─ Data/                                                  │
│  │  ├─ BaristaNotesContext.cs  (modified: add Bag DbSet)  │
│  │  └─ Repositories/                                       │
│  │     ├─ IBagRepository.cs / BagRepository.cs (new)      │
│  │     └─ IBeanRepository.cs / BeanRepository.cs (mod)    │
│  └─ Migrations/                                            │
│     └─ [timestamp]_AddBagEntity.cs  (new: EF migration)   │
└────────────────────────────────────────────────────────────┘
                          ▼ Reads/Writes
┌────────────────────────────────────────────────────────────┐
│  SQLite Database (barista_notes.db)                       │
│  ├─ Beans         (RoastDate removed)                     │
│  ├─ Bags          (new table)                             │
│  └─ ShotRecords   (BagId FK, indexed on BagId+Rating)     │
└────────────────────────────────────────────────────────────┘
```

## Development Workflow (Test-First)

**Constitution Requirement**: Test-Driven Development is mandatory.

### Phase 1: Write Tests (Before Implementation)

1. **Unit Tests** (BaristaNotes.Tests/Unit/):
   ```bash
   # Create test files FIRST
   touch BaristaNotes.Tests/Unit/RatingServiceTests.cs
   touch BaristaNotes.Tests/Unit/BagServiceTests.cs
   ```

   Example test structure (write BEFORE implementation):
   ```csharp
   public class RatingServiceTests
   {
       [Fact]
       public async Task GetBeanRating_WithMultipleBags_ReturnsAggregateAcrossAllBags()
       {
           // Arrange: Create in-memory DB, seed bean + 2 bags + shots
           // Act: Call _ratingService.GetBeanRatingAsync(beanId)
           // Assert: AverageRating == expected, Distribution correct
       }
       
       [Fact]
       public async Task GetBagRating_WithNoShots_ReturnsEmptyAggregate()
       {
           // Arrange: Create bag with no shots
           // Act: Call _ratingService.GetBagRatingAsync(bagId)
           // Assert: AverageRating == 0, TotalShots == 0
       }
   }
   ```

2. **Integration Tests** (BaristaNotes.Tests/Integration/):
   ```csharp
   public class BagRepositoryTests
   {
       [Fact]
       public async Task GetBagSummariesForBean_OrdersByRoastDateDescending()
       {
           // Arrange: Seed bean with 3 bags (different roast dates)
           // Act: Call _bagRepository.GetBagSummariesForBeanAsync(beanId)
           // Assert: First bag has newest RoastDate
       }
   }
   ```

3. **Run tests** (they should FAIL - Red phase):
   ```bash
   cd BaristaNotes.Tests
   dotnet test --filter "FullyQualifiedName~RatingService"
   # Expected: All tests fail (services don't exist yet)
   ```

### Phase 2: Database Migration

1. **Update Entity Models** (BaristaNotes.Core/Models/):
   ```bash
   # Edit Bean.cs: Remove RoastDate, add Bags navigation property
   # Create Bag.cs: New entity with schema from data-model.md
   # Edit ShotRecord.cs: Change BeanId → BagId
   ```

2. **Update DbContext** (BaristaNotes.Core/Data/BaristaNotesContext.cs):
   ```csharp
   public DbSet<Bag> Bags { get; set; } = null!;
   
   protected override void OnModelCreating(ModelBuilder modelBuilder)
   {
       // Add Bag configuration
       modelBuilder.Entity<Bag>(entity =>
       {
           entity.HasKey(e => e.Id);
           entity.Property(e => e.RoastDate).IsRequired();
           entity.HasOne(e => e.Bean)
                 .WithMany(b => b.Bags)
                 .HasForeignKey(e => e.BeanId);
           // ... (see data-model.md for full config)
       });
       
       // Update ShotRecord configuration
       modelBuilder.Entity<ShotRecord>(entity =>
       {
           entity.HasOne(e => e.Bag)
                 .WithMany(b => b.ShotRecords)
                 .HasForeignKey(e => e.BagId);
           // ... (remove old Bean relationship)
       });
   }
   ```

3. **Generate Migration** (CRITICAL: Use EF Core, never manual SQL):
   ```bash
   cd BaristaNotes.Core
   dotnet ef migrations add AddBagEntity
   # Review generated migration in Migrations/ folder
   ```

4. **Review Migration**:
   - Open `Migrations/[timestamp]_AddBagEntity.cs`
   - Verify Up() method includes data seeding (see research.md for SQL)
   - Verify Down() method can rollback
   - **If data migration not auto-generated**: Manually add SQL in Up() method

5. **Test Migration**:
   ```bash
   # Apply migration to dev database
   dotnet ef database update
   
   # Verify schema changes
   sqlite3 ../barista_notes.db ".schema Bags"
   
   # Test rollback
   dotnet ef database update [previous_migration_name]
   dotnet ef database update AddBagEntity
   ```

### Phase 3: Implement Services (Make Tests Pass - Green Phase)

1. **Create Service Interfaces** (copy from specs/001-bean-rating-tracking/contracts/):
   ```bash
   cp specs/001-bean-rating-tracking/contracts/*.cs BaristaNotes.Core/Services/
   cp specs/001-bean-rating-tracking/contracts/DTOs/*.cs BaristaNotes.Core/Services/DTOs/
   ```

2. **Implement RatingService**:
   ```csharp
   public class RatingService : IRatingService
   {
       private readonly BaristaNotesContext _context;
       
       public async Task<RatingAggregateDto> GetBeanRatingAsync(int beanId)
       {
           var shots = await _context.ShotRecords
               .Where(s => s.Bag.BeanId == beanId && !s.IsDeleted && s.Rating != null)
               .Select(s => s.Rating!.Value)
               .ToListAsync();
           
           return new RatingAggregateDto
           {
               TotalShots = shots.Count,
               RatedShots = shots.Count,
               AverageRating = shots.Any() ? shots.Average() : 0,
               Distribution = shots.GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count())
           };
       }
       // ... (implement other methods)
   }
   ```

3. **Implement BagService** (follow existing BeanService pattern):
   ```csharp
   public class BagService : IBagService
   {
       private readonly BaristaNotesContext _context;
       
       public async Task<OperationResult<Bag>> CreateBagAsync(Bag bag)
       {
           // Validation: BeanId exists, RoastDate not future, etc.
           // Save to DB
           // Return OperationResult
       }
       // ... (implement other methods)
   }
   ```

4. **Register Services** (BaristaNotes/MauiProgram.cs):
   ```csharp
   builder.Services.AddScoped<IBagService, BagService>();
   builder.Services.AddScoped<IRatingService, RatingService>();
   ```

5. **Run Tests Again** (should PASS - Green phase):
   ```bash
   dotnet test --filter "FullyQualifiedName~RatingService"
   dotnet test --filter "FullyQualifiedName~BagService"
   # Expected: All tests pass
   ```

### Phase 4: UI Implementation (Reactor.Maui)

1. **Create Rating Display Component**:
   ```csharp
   class RatingDisplayComponent : Component<RatingDisplayState>
   {
       [Prop] public RatingAggregateDto Aggregate { get; set; }
       
       public override VisualNode Render()
           => new VStack(spacing: 12)
           {
               // Star rating average
               new HStack(spacing: 8)
               {
                   new Label($"★ {Aggregate.FormattedAverage}")
                       .FontSize(24),
                   new Label($"({Aggregate.RatedShots} ratings)")
                       .Opacity(0.7)
               },
               
               // Distribution bars (5 stars → 1 star)
               RenderDistributionBars()
           };
       
       VisualNode RenderDistributionBars()
           => new VStack(spacing: 4)
           {
               For(5, 1, -1, rating => 
                   new HStack(spacing: 8)
                   {
                       new Label($"{rating}★").Width(40),
                       new ProgressBar()
                           .Progress(Aggregate.GetPercentageForRating(rating) / 100.0)
                           .FlexGrow(1),
                       new Label(Aggregate.GetCountForRating(rating).ToString())
                           .Width(40)
                   }
               )
           };
   }
   ```

2. **Modify BeanDetailPage** (add rating section):
   ```csharp
   [Inject] private IRatingService _ratingService;
   
   protected override async Task OnMountedAsync()
   {
       var beanRating = await _ratingService.GetBeanRatingAsync(BeanId);
       SetState(s => s.BeanRating = beanRating);
   }
   
   public override VisualNode Render()
       => new VStack
       {
           // Existing bean details...
           
           // NEW: Rating section
           new Label("Overall Rating").FontSize(20).FontWeight(FontWeights.Bold),
           new RatingDisplayComponent().Aggregate(State.BeanRating),
           
           // NEW: Bags section
           new Label("Bags").FontSize(20).FontWeight(FontWeights.Bold),
           RenderBagsList()
       };
   ```

3. **Create BagFormPage** (for adding new bags):
   ```csharp
   class BagFormPage : Component<BagFormState>
   {
       [Inject] private IBagService _bagService;
       [Param] public int BeanId { get; set; }
       
       public override VisualNode Render()
           => new VStack(spacing: 16).Padding(16)
           {
               new Label("Add New Bag").FontSize(24),
               
               new DatePicker()
                   .Date(State.RoastDate)
                   .OnDateSelected(date => SetState(s => s.RoastDate = date)),
               
               new Entry()
                   .Placeholder("Notes (optional)")
                   .Text(State.Notes)
                   .OnTextChanged(text => SetState(s => s.Notes = text)),
               
               new Button("Save Bag")
                   .OnClicked(SaveBag)
           };
       
       async Task SaveBag()
       {
           var bag = new Bag
           {
               BeanId = BeanId,
               RoastDate = State.RoastDate,
               Notes = State.Notes,
               CreatedAt = DateTimeOffset.Now,
               SyncId = Guid.NewGuid(),
               LastModifiedAt = DateTimeOffset.Now
           };
           
           var result = await _bagService.CreateBagAsync(bag);
           if (result.Success)
               await Navigation.PopAsync();
       }
   }
   ```

4. **Modify ShotLoggingPage** (add bag selection):
   ```csharp
   // After bean selection, load bags
   async Task OnBeanSelected(Bean bean)
   {
       var bags = await _bagService.GetBagsForBeanAsync(bean.Id);
       SetState(s => 
       {
           s.SelectedBean = bean;
           s.AvailableBags = bags;
           s.SelectedBag = bags.OrderByDescending(b => b.RoastDate).FirstOrDefault();
       });
   }
   
   // In Render(), add bag picker after bean picker
   RenderIf(State.SelectedBean != null && State.AvailableBags.Count > 1,
       new Label("Which bag?").FontWeight(FontWeights.SemiBold),
       new Picker()
           .ItemsSource(State.AvailableBags, bag => FormatBagLabel(bag))
           .SelectedItem(State.SelectedBag)
           .OnSelectedItemChanged(bag => SetState(s => s.SelectedBag = bag))
   )
   ```

### Phase 5: Testing & Validation

1. **Run Full Test Suite**:
   ```bash
   cd BaristaNotes.Tests
   dotnet test --collect:"XPlat Code Coverage"
   # Target: 80% overall, 100% for RatingService
   ```

2. **Manual Testing Checklist** (follow spec acceptance scenarios):
   - [ ] P1-1: Bean with multiple shots shows aggregate rating
   - [ ] P1-2: Bean shows rating distribution (5★: X, 4★: Y, etc.)
   - [ ] P1-3: Bean with no shots shows "No ratings yet"
   - [ ] P2-1: Can add new bag for existing bean
   - [ ] P2-2: Can select bag when logging shot
   - [ ] P2-3: Bean detail shows list of all bags
   - [ ] P3-1: Can view individual bag ratings
   - [ ] P3-2: Can compare ratings between bags

3. **Performance Validation**:
   ```bash
   # Seed database with 50 beans, 500 shots
   # Measure bean detail load time (target: <2s)
   # Measure rating calculation time (target: <500ms)
   ```

4. **Accessibility Check**:
   - [ ] All buttons keyboard navigable
   - [ ] Screen reader announces ratings ("Average 4.5 stars")
   - [ ] Touch targets >= 44px
   - [ ] Color contrast meets WCAG 2.1 AA

## Key Files Reference

| File | Purpose | Status |
|------|---------|--------|
| `BaristaNotes.Core/Models/Bag.cs` | New entity | Create |
| `BaristaNotes.Core/Models/Bean.cs` | Remove RoastDate | Modify |
| `BaristaNotes.Core/Models/ShotRecord.cs` | BagId FK | Modify |
| `BaristaNotes.Core/Data/BaristaNotesContext.cs` | Add Bag DbSet | Modify |
| `BaristaNotes.Core/Services/BagService.cs` | Bag CRUD logic | Create |
| `BaristaNotes.Core/Services/RatingService.cs` | Rating calculations | Create |
| `BaristaNotes.Core/Migrations/[timestamp]_AddBagEntity.cs` | Schema migration | Generate |
| `BaristaNotes/Pages/BeanDetailPage.cs` | Add rating display | Modify |
| `BaristaNotes/Pages/BagDetailPage.cs` | Bag-specific view | Create |
| `BaristaNotes/Pages/BagFormPage.cs` | Add bag form | Create |
| `BaristaNotes/Pages/ShotLoggingPage.cs` | Add bag selection | Modify |
| `BaristaNotes/Components/RatingDisplayComponent.cs` | Rating UI | Create |

## Common Issues & Solutions

### Issue: Migration doesn't seed Bags from existing Beans
**Solution**: Manually add SQL data migration in Up() method (see research.md section 1)

### Issue: Rating queries slow (>500ms)
**Solution**: Verify indexes exist via `sqlite3 barista_notes.db ".indices ShotRecords"`

### Issue: Bag picker doesn't show after bean selection
**Solution**: Check `OnBeanSelected` loads bags and sets state correctly

### Issue: Tests fail with "table Bags not found"
**Solution**: In-memory test DB needs migration: `await _context.Database.MigrateAsync();`

## Performance Targets (from NFR)

- **NFR-P1**: Bean detail view load < 2s (p95)
- **NFR-P2**: Rating calculations < 500ms (p95)
- **NFR-P3**: UI interactions < 100ms feedback
- **NFR-P4**: Rating display render < 1s (up to 100 shots)

**Monitor via**: EF Core query logging, Stopwatch profiling, app performance instrumentation

## Next Steps

1. **Phase 2 (NOT part of /speckit.plan)**: Run `/speckit.tasks` to generate task breakdown
2. Follow TDD workflow: Write tests → Implement → Refactor
3. Commit frequently with descriptive messages (reference feature number: "001: Add Bag entity")
4. Open PR when P1 acceptance scenarios pass (P2/P3 can be separate PRs)

## Questions?

Refer to:
- **Spec**: `specs/001-bean-rating-tracking/spec.md` (user requirements)
- **Research**: `specs/001-bean-rating-tracking/research.md` (technical decisions)
- **Data Model**: `specs/001-bean-rating-tracking/data-model.md` (schema details)
- **Contracts**: `specs/001-bean-rating-tracking/contracts/` (service interfaces)
