# Research: Espresso Shot Tracking & Management

**Feature**: 001-espresso-tracking  
**Phase**: 0 - Research & Technical Decisions  
**Date**: 2025-12-02

## Purpose

This document captures technical research and decisions for building the BaristaNotes espresso tracking application. All decisions are based on the specified technology stack and best practices for .NET MAUI, Maui Reactor, and offline-first mobile applications.

## Technology Stack Decisions

### 1. .NET MAUI with Maui Reactor

**Decision**: Use .NET MAUI as the cross-platform framework with Maui Reactor for fluent UI composition.

**Rationale**:
- **Cross-platform**: Single codebase for iOS and Android reduces development and maintenance effort
- **Native Performance**: .NET MAUI compiles to native platform code, meeting <2s launch time requirement
- **Maui Reactor Benefits**:
  - Fluent, declarative UI syntax improves code readability (Constitution Principle I)
  - Component-based architecture promotes reusability and single responsibility
  - Similar to React/SwiftUI mental model, familiar to modern developers
  - Hot reload for rapid UI iteration
- **Ecosystem**: Rich NuGet ecosystem, active community, Microsoft backing ensures long-term support

**Alternatives Considered**:
- **Xamarin.Forms**: Deprecated by Microsoft, migration path to MAUI clear
- **Native iOS/Android**: Would require duplicate codebases, violates DRY principle
- **Flutter**: Different language (Dart), team expertise in C#/.NET ecosystem
- **React Native**: JavaScript/TypeScript stack, team expertise in C#/.NET ecosystem

**References**:
- [.NET MAUI Documentation](https://learn.microsoft.com/en-us/dotnet/maui/)
- [Maui Reactor GitHub](https://github.com/adospace/reactorui-maui)
- [Reactor.Maui NuGet](https://www.nuget.org/packages/Reactor.Maui/)

---

### 2. SQLite with Entity Framework Core

**Decision**: Use SQLite for local data storage with Entity Framework Core as the ORM.

**Rationale**:
- **Offline-First**: SQLite is embedded, no network dependency (requirement FR-015)
- **Performance**: Fast local queries meet <500ms form pre-population requirement
- **EF Core Benefits**:
  - Type-safe LINQ queries reduce runtime errors
  - Automatic migrations handle schema evolution
  - Repository pattern abstraction enables testability
  - Built-in change tracking simplifies CRUD operations
- **Mobile-Optimized**: SQLite is battle-tested on mobile platforms, low memory footprint
- **Cross-Platform**: Same database file format works on iOS and Android

**Alternatives Considered**:
- **Realm**: Good mobile performance but adds dependency; EF Core sufficient for scale (1000+ records)
- **LiteDB**: .NET-native NoSQL, but relational model fits shot tracking data structure better
- **Direct SQLite**: Would require manual query writing, violates DRY and reduces type safety

**Implementation Notes**:
- Use indexed columns for frequently queried fields (timestamp, user profiles)
- Implement cascade deletes carefully (archiving vs. deletion)
- Configure connection pooling for multi-threaded access
- Enable Write-Ahead Logging (WAL) for better concurrency

**References**:
- [EF Core SQLite Provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)
- [SQLite for .NET MAUI](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/database-sqlite)

---

### 3. CoreSync for Future Sync Capabilities

**Decision**: Include CoreSync.Sqlite and CoreSync.Http.Client for future cloud sync, but not implement sync in MVP.

**Rationale**:
- **Future-Proof**: Architecture supports sync without major refactoring
- **Data Model Impact**: CoreSync requires specific schema design (sync columns)
- **MVP Scope**: Local-only satisfies all FR-001 through FR-015 requirements
- **Architecture**: Adding sync columns now prevents breaking migration later

**Alternatives Considered**:
- **No Sync Planning**: Would require breaking changes to data model later
- **Implement Sync Now**: Out of scope for MVP, adds complexity
- **Custom Sync**: CoreSync provides battle-tested conflict resolution

**Implementation Notes**:
- Add CoreSync metadata columns to all entities (SyncId, LastModified, IsDeleted)
- Use soft deletes (IsDeleted flag) instead of hard deletes for sync compatibility
- Design data model with sync in mind but don't implement sync endpoints

**References**:
- [CoreSync GitHub](https://github.com/aldycool/CoreSync)
- [CoreSync Documentation](https://github.com/aldycool/CoreSync/wiki)

---

### 4. CommunityToolkit.Maui for UI Components

**Decision**: Use CommunityToolkit.Maui for standardized controls and behaviors.

**Rationale**:
- **Standardization**: Provides battle-tested components (popup, toast, behaviors)
- **Accessibility**: Toolkit components include accessibility semantics by default
- **Performance**: Optimized for mobile, meets 60fps requirement
- **Community**: Microsoft-backed community project, regular updates

**Key Components for This Project**:
- **Popup**: For quick equipment/bean selection dialogs
- **Toast**: For save confirmations and validation messages
- **Behaviors**: For form validation, touch feedback
- **Converters**: For data binding (rating to stars, timestamp to relative time)

**References**:
- [CommunityToolkit.Maui Documentation](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/)
- [CommunityToolkit.Maui GitHub](https://github.com/CommunityToolkit/Maui)

---

### 5. MauiReactor Integration Libraries

**Decision**: Use pre-scaffolded libraries from adospace/mauireactor-integration for additional controls.

**Rationale**:
- **Reactor-Compatible**: Controls wrapped for use with Maui Reactor fluent syntax
- **Vetted**: Integration maintained by Reactor author
- **Reduces Boilerplate**: Pre-built wrappers save integration effort

**Potential Components**:
- Icon font integration
- Custom picker controls
- Advanced list virtualization if needed

**References**:
- [MauiReactor Integration GitHub](https://github.com/adospace/mauireactor-integration)

---

### 6. Icon Font for Iconography

**Decision**: Use a single icon font (e.g., Material Icons, Font Awesome) for all iconography.

**Rationale**:
- **Performance**: Vector-based, single file download, scales without quality loss
- **Consistency**: Same icon style throughout app (Constitution Principle III)
- **Theme Integration**: Icons colored via theme, no separate asset management
- **Accessibility**: Text-based, works with screen readers

**Recommended Icon Font**:
- **Material Symbols**: Google's latest icon font, 2500+ icons, variable font weights
- **Font Awesome Free**: Popular choice, 2000+ icons, well-documented

**Implementation Notes**:
- Embed font in Resources/Fonts/
- Register font in MauiProgram.cs
- Create icon constant class for type-safe icon references
- Use semantic naming (IconNames.Add, IconNames.Coffee, etc.)

**References**:
- [Material Symbols](https://fonts.google.com/icons)
- [MAUI Fonts Documentation](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts)

---

### 7. Theme-Based Styling Architecture

**Decision**: Centralize all styles in Resources/Styles/Styles.xaml with theme keys, avoid inline styling.

**Rationale**:
- **Consistency**: Single source of truth for colors, fonts, spacing (Constitution Principle III)
- **Maintainability**: Theme changes propagate automatically, no scattered style updates
- **Dark Mode**: Easy to implement alternate theme with same keys
- **Testability**: Visual regression testing easier with consistent styling

**Theme Structure**:
```
Resources/Styles/
├── Colors.xaml          # Color palette (Primary, Secondary, Success, Error, etc.)
└── Styles.xaml          # Component styles (Button, Entry, Label, Card, etc.)
```

**Implementation Pattern**:
- Define semantic color keys (e.g., "ColorPrimary", "ColorSurface", "ColorOnSurface")
- Define typography scale (Headline, Title, Body, Caption)
- Define spacing scale (SpaceXS, SpaceS, SpaceM, SpaceL, SpaceXL)
- Apply via StaticResource in Reactor components

**References**:
- [MAUI Styling Documentation](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/styles/xaml)
- [Material Design Color System](https://m3.material.io/styles/color/system/overview)

---

### 8. MVVM Architecture with Services Layer

**Decision**: Use MVVM pattern with separation: ViewModels → Services → Repositories → EF Core.

**Rationale**:
- **Separation of Concerns**: Each layer has single responsibility (Constitution Principle I)
- **Testability**: Services and ViewModels testable in isolation with mocks
- **Maintainability**: Business logic in Services, not ViewModels or UI
- **Reactor Integration**: ViewModels hold state, Reactor components consume ViewModels

**Layer Responsibilities**:
- **ViewModels**: UI state, user input validation, command handling
- **Services**: Business logic, data transformation, cross-cutting concerns
- **Repositories**: Data access abstraction, CRUD operations, query optimization
- **Models**: Domain entities, EF Core configuration, validation attributes

**Dependency Injection**:
- Register all services and repositories in MauiProgram.cs
- Use constructor injection throughout
- Scoped lifetime for DbContext
- Singleton lifetime for stateless services

**References**:
- [MAUI Dependency Injection](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/dependency-injection)
- [MVVM Pattern](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm)

---

### 9. Testing Strategy

**Decision**: Use xUnit for unit/integration tests with InMemory SQLite for database tests.

**Rationale**:
- **xUnit**: Modern, widely adopted, excellent async support
- **InMemory SQLite**: Fast test execution without file I/O
- **Test Coverage**: 80% minimum (100% for data persistence)

**Test Categories**:

**Unit Tests**:
- Services: Business logic, data transformations, validation
- ViewModels: State management, command handling, property changes
- Repositories: Query logic (with InMemory database)

**Integration Tests**:
- Database operations: Migrations, relationships, cascade behavior
- CoreSync integration: Sync metadata handling (preparation for future)

**UI Tests** (Future):
- .NET MAUI testing framework for critical flows
- Focus on shot logging and activity feed workflows

**Test Organization**:
```
BaristaNotes.Tests/
├── Unit/
│   ├── Services/
│   ├── ViewModels/
│   └── Repositories/
├── Integration/
│   ├── DatabaseTests.cs
│   └── CoreSyncTests.cs
└── Helpers/
    ├── TestDbContextFactory.cs
    └── MockDataBuilder.cs
```

**References**:
- [xUnit Documentation](https://xunit.net/)
- [EF Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)

---

### 10. Performance Optimization Strategies

**Decision**: Follow performance best practices to meet constitutional requirements.

**Key Optimizations**:

**App Launch (<2s)**:
- Minimize MauiProgram.cs initialization
- Lazy-load services where possible
- Use compiled XAML for theme resources
- Defer non-critical initializations to post-launch

**Form Pre-Population (<500ms)**:
- Index ShotRecord.Timestamp for "most recent" query
- Use AsNoTracking() for read-only queries
- Cache last selections in IPreferencesService (Preferences API)

**60fps Rendering**:
- Use native controls (avoid custom drawing)
- Virtualize activity feed (CollectionView with data virtualization)
- Keep visual tree shallow (max 3-4 levels deep)
- Avoid transparency/blur effects
- Use CachedImage for profile avatars if implemented later

**Memory Management (<200MB)**:
- Dispose ViewModels and subscriptions properly
- Use WeakEventManager for event subscriptions
- Profile with Xamarin Profiler/dotMemory during development
- Implement image caching if photos added later

**References**:
- [MAUI Performance Tips](https://learn.microsoft.com/en-us/dotnet/maui/deployment/performance)
- [CollectionView Performance](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/controls/collectionview/populate-data)

---

## Open Questions for Phase 1 Design

1. **Grind Setting Data Type**: String (flexible for named/numeric) or separate fields for stepped/stepless grinders?
   - **Recommendation**: String field with optional numeric validation in UI
   
2. **Equipment Accessories Relationship**: Many-to-many or serialized list?
   - **Recommendation**: Many-to-many with junction table for query flexibility

3. **Rating Storage**: Integer 1-5 or enum?
   - **Recommendation**: Integer with range validation (simpler, no enum overhead)

4. **Timestamp Precision**: DateTime or DateTimeOffset?
   - **Recommendation**: DateTimeOffset for timezone-aware logging (future cloud sync)

5. **Soft Deletes**: Flag on all entities for CoreSync compatibility?
   - **Recommendation**: Yes, add IsDeleted to all entities now

---

## Summary

All technical decisions documented with rationale, alternatives considered, and implementation notes. Technology stack fully specified:
- **.NET 8 / C# 12** with **.NET MAUI** for cross-platform mobile
- **Maui Reactor** for fluent UI composition
- **SQLite + EF Core** for offline-first data persistence
- **CoreSync** metadata prepared (sync not implemented in MVP)
- **CommunityToolkit.Maui** for standardized controls
- **Icon font** for consistent iconography
- **Theme-based styling** for maintainability and consistency
- **MVVM + Services + Repositories** architecture for separation of concerns
- **xUnit** with InMemory SQLite for comprehensive testing

All decisions align with constitutional principles and meet performance requirements. Ready to proceed to Phase 1: Data Model & Contracts.
