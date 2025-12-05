# Tasks: Coffee-Themed Color System with Theme Selection

**Feature Branch**: `002-coffee-theme`  
**Date**: 2025-12-04  
**Input**: Design documents from `/specs/002-coffee-theme/`

**Prerequisites**: 
- spec.md (user stories with priorities)
- plan.md (tech stack and structure)
- research.md (technology decisions)
- data-model.md (ThemeMode enum and color palette)
- contracts/theme-contracts.md (IThemeService interface)

**Tests**: Tests are MANDATORY per Constitution Principle II (Test-First Development). Tests must be written FIRST, must FAIL before implementation, and require 80% minimum coverage (100% for critical paths).

**Quality Gates**: All tasks must pass constitution-mandated quality gates before merge: tests pass, static analysis clean, code review approved, performance baseline met, documentation updated.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

BaristaNotes uses a mobile + API architecture:
- **Core**: `BaristaNotes.Core/` - Shared models, interfaces, DTOs
- **Main**: `BaristaNotes/` - MauiReactor UI pages and components
- **Tests**: `BaristaNotes.Tests/` - Unit and integration tests

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project structure validation and dependency verification

- [X] T001 Verify BaristaNotes, BaristaNotes.Core, and BaristaNotes.Tests projects build successfully
- [X] T002 Verify MauiReactor, Microsoft.Maui.Graphics, and Preferences API dependencies are available
- [X] T003 [P] Review existing ApplicationTheme.cs in BaristaNotes/Resources/Styles/ to understand MauiReactor Theme pattern

**Checkpoint**: Environment validated - ready for foundational work

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### ThemeMode Enum and Contracts

- [X] T004 Create ThemeMode enum in BaristaNotes/Services/ThemeMode.cs with values: Light, Dark, System
- [X] T005 Create IThemeService interface in BaristaNotes/Services/IThemeService.cs with CurrentMode, CurrentTheme properties and GetThemeModeAsync, SetThemeModeAsync, ApplyTheme methods

### Color Contrast Validation Utility

- [ ] T006 [P] Create ContrastCalculator utility class in BaristaNotes.Tests/Utilities/ContrastCalculator.cs with CalculateContrast method implementing WCAG 2.1 relative luminance formula
- [ ] T007 [P] Write unit tests for ContrastCalculator in BaristaNotes.Tests/Unit/Utilities/ContrastCalculatorTests.cs verifying 4.5:1 and 3:1 ratio calculations

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Apply Coffee Color Palette to All UI Elements (Priority: P1) 🎯 MVP

**Goal**: Replace generic color palette with coffee-themed semantic colors that work in both light and dark modes

**Independent Test**: Launch app in light mode, verify all 6 pages display coffee colors (warm cream backgrounds, dark espresso text). Switch device to dark mode, verify all pages adapt to dark coffee theme (rich brown backgrounds, light cream text). Check buttons use coffee accent #86543F.

### Color Palette Definition (Tests First)

- [ ] T008 [US1] Write color contrast validation tests in BaristaNotes.Tests/Unit/Services/ColorContrastTests.cs for Light mode: TextPrimary on Background (≥4.5:1), TextSecondary on Background (≥4.5:1), OnPrimary on Primary (≥4.5:1)
- [ ] T009 [US1] Write color contrast validation tests in BaristaNotes.Tests/Unit/Services/ColorContrastTests.cs for Dark mode: TextPrimary on Background (≥4.5:1), TextSecondary on Background (≥4.5:1), OnPrimary on Primary (≥4.5:1)
- [X] T010 [US1] Replace existing AppColors class in BaristaNotes/Resources/Styles/AppColors.cs with coffee-themed semantic tokens: Light.Background (#D2BCA5), Light.Surface (#FCEFE1), Light.SurfaceVariant (#ECDAC4), Light.SurfaceElevated (#FFF7EC), Light.Primary (#86543F), Light.OnPrimary (#F8F6F4), Light.TextPrimary (#352B23), Light.TextSecondary (#7C7067), Light.TextMuted (#A38F7D), Light.Outline (#D7C5B2)
- [X] T011 [US1] Add Dark mode nested class in BaristaNotes/Resources/Styles/AppColors.cs with tokens: Dark.Background (#48362E), Dark.Surface (#48362E), Dark.SurfaceVariant (#7D5A45), Dark.SurfaceElevated (#B3A291), Dark.Primary (#86543F), Dark.OnPrimary (#F8F6F4), Dark.TextPrimary (#F8F6F4), Dark.TextSecondary (#C5BFBB), Dark.TextMuted (#A19085), Dark.Outline (#5A463B)
- [X] T012 [US1] Keep existing semantic colors in AppColors.cs (Success #4CAF50, Warning #FFA726, Error #EF5350, Info #42A5F5) as they work in both themes
- [ ] T013 [US1] Run ColorContrastTests to verify all text/background combinations meet WCAG AA requirements (should pass if colors match spec)

### ApplicationTheme Update (Apply Coffee Colors)

- [X] T014 [US1] Update ApplicationTheme.OnApply() in BaristaNotes/Resources/Styles/ApplicationTheme.cs to replace all hardcoded purple/gray colors with conditional coffee colors: `IsLightTheme ? AppColors.Light.X : AppColors.Dark.X`
- [X] T015 [US1] Update ButtonStyles.Default in ApplicationTheme.cs to use Primary for background and OnPrimary for text color
- [X] T016 [US1] Update LabelStyles.Default in ApplicationTheme.cs to use TextPrimary for color
- [X] T017 [US1] Update PageStyles.Default in ApplicationTheme.cs to use Background for background color
- [X] T018 [US1] Update BorderStyles.Default in ApplicationTheme.cs to use Outline for stroke color
- [X] T019 [US1] Update EntryStyles.Default, EditorStyles.Default, SearchBarStyles.Default in ApplicationTheme.cs to use TextPrimary for text and TextMuted for placeholder
- [X] T020 [US1] Update ShellStyles.Default in ApplicationTheme.cs to use Background, Primary, and TextPrimary colors
- [X] T021 [US1] Create cached Brush properties in ApplicationTheme.cs: PrimaryBrush, BackgroundBrush, SurfaceBrush (initialized in OnApply to avoid allocations)

### Visual Verification on All Pages

- [ ] T022 [US1] Launch app in light mode, verify ShotLoggingPage displays coffee colors (background #D2BCA5, cards #FCEFE1, text #352B23, buttons #86543F)
- [ ] T023 [US1] Launch app in light mode, verify ActivityFeedPage displays coffee colors (background #D2BCA5, shot cards #ECDAC4, text #352B23)
- [ ] T024 [US1] Launch app in light mode, verify SettingsPage displays coffee colors (background #D2BCA5, list items #FCEFE1, text #352B23)
- [ ] T025 [US1] Launch app in light mode, verify EquipmentManagementPage displays coffee colors
- [ ] T026 [US1] Launch app in light mode, verify BeanManagementPage displays coffee colors
- [ ] T027 [US1] Launch app in light mode, verify UserProfileManagementPage displays coffee colors
- [ ] T028 [US1] Switch device to dark mode (iOS: Settings > Display & Brightness, Android: Settings > Display), verify all 6 pages adapt to dark coffee theme (background #48362E, text #F8F6F4)
- [ ] T029 [US1] Verify no white flashes or visual glitches when switching between light and dark modes on device

**Checkpoint**: US1 complete - App displays coffee-themed colors in both light and dark modes, all pages verified

---

## Phase 4: User Story 2 - User-Controlled Theme Selection (Priority: P2)

**Goal**: Implement theme selection UI in Settings page with immediate application and persistence across app restarts

**Independent Test**: Open Settings, select "Light" theme, verify app updates immediately. Restart app, verify light theme persists. Select "Dark" theme, verify app updates. Select "System" theme, change device theme setting, verify app follows device theme.

### ThemeService Implementation (Tests First)

- [ ] T030 [US2] Write ThemeService tests in BaristaNotes.Tests/Unit/Services/ThemeServiceTests.cs: GetThemeModeAsync returns default System when no preference saved
- [ ] T031 [US2] Write ThemeService test: GetThemeModeAsync returns saved preference value (Light/Dark/System)
- [ ] T032 [US2] Write ThemeService test: SetThemeModeAsync persists mode to preferences with key "AppThemeMode"
- [ ] T033 [US2] Write ThemeService test: SetThemeModeAsync updates CurrentMode property
- [ ] T034 [US2] Write ThemeService test: SetThemeModeAsync calls ApplyTheme to immediately reflect change
- [ ] T035 [US2] Write ThemeService test: ApplyTheme resolves correct AppTheme for each ThemeMode (Light→Light, Dark→Dark, System→Application.Current.RequestedTheme)
- [ ] T036 [US2] Write ThemeService test: System theme change event triggers ApplyTheme only when CurrentMode is System
- [X] T037 [US2] Implement ThemeService class in BaristaNotes/Services/ThemeService.cs with constructor injecting IPreferencesStore
- [X] T038 [US2] Implement ThemeService.GetThemeModeAsync reading "AppThemeMode" from Preferences API, defaulting to System if missing
- [X] T039 [US2] Implement ThemeService.SetThemeModeAsync saving mode as string to preferences, updating CurrentMode property, and calling ApplyTheme
- [X] T040 [US2] Implement ThemeService.ApplyTheme resolving effective AppTheme and setting Application.Current.UserAppTheme
- [X] T041 [US2] Implement ThemeService constructor subscribing to Application.Current.RequestedThemeChanged event
- [X] T042 [US2] Implement OnSystemThemeChanged event handler in ThemeService checking if CurrentMode is System before calling ApplyTheme
- [ ] T043 [US2] Run ThemeServiceTests to verify all 7 test cases pass

### Dependency Injection Registration

- [X] T044 [US2] Register ThemeService as singleton in MauiProgram.cs: `builder.Services.AddSingleton<IThemeService, ThemeService>()`
- [X] T045 [US2] Initialize ThemeService on app startup in MauiProgram.cs to load saved theme preference

### Theme Selection UI in Settings Page

- [X] T046 [US2] Add theme selection section to SettingsPage.cs with Label("Appearance") header
- [X] T047 [US2] Add VStack with 3 theme option buttons in SettingsPage.cs: "Light", "Dark", "System" with visual indicators showing current selection
- [X] T048 [US2] Inject IThemeService into SettingsPage.cs using `[Inject]` attribute
- [X] T049 [US2] Implement OnThemeSelected handler in SettingsPage.cs calling `await _themeService.SetThemeModeAsync(selectedMode)`
- [X] T050 [US2] Update SettingsPage state to highlight current theme option based on `_themeService.CurrentMode`
- [X] T051 [US2] Add visual feedback (check mark or highlighted background) to selected theme option in SettingsPage.cs

### Integration Testing

- [ ] T052 [US2] Write integration test in BaristaNotes.Tests/Integration/ThemeIntegrationTests.cs: Set theme to Light, restart app (simulate), verify theme persists as Light
- [ ] T053 [US2] Write integration test: Set theme to Dark, restart app, verify theme persists as Dark
- [ ] T054 [US2] Write integration test: Set theme to System with OS in light mode, verify app displays light theme
- [ ] T055 [US2] Write integration test: Set theme to System with OS in dark mode, verify app displays dark theme
- [ ] T056 [US2] Run ThemeIntegrationTests to verify persistence and system theme tracking work end-to-end

**Checkpoint**: US2 complete - Users can select theme in Settings, preference persists, System mode tracks device theme

---

## Phase 5: User Story 3 - Smooth Theme Transitions (Priority: P3)

**Goal**: Ensure theme changes animate smoothly without white flashes or visual glitches

**Independent Test**: Select different themes in Settings and observe transitions. Change device theme while app is open (with System theme selected) and observe automatic transition. Verify no white flashes, no controls appearing unstyled momentarily, and colors change smoothly.

### Performance Optimization

- [ ] T057 [US3] Profile theme switching time using Xcode Instruments or Android Profiler, verify <300ms from SetThemeModeAsync call to visual update
- [ ] T058 [US3] Ensure ApplicationTheme.OnApply() updates all control styles in single render pass (no async operations)
- [ ] T059 [US3] Verify brush objects are cached in ApplicationTheme static properties and reused across theme switches (no allocations)

### Visual Smoothness Testing

- [ ] T060 [US3] Test theme transition on physical iOS device: tap Light→Dark→System in quick succession, verify smooth transitions with no white flash
- [ ] T061 [US3] Test theme transition on physical Android device: verify smooth color changes without jarring jumps
- [ ] T062 [US3] Test theme change while shot logging modal is open, verify both modal and underlying page transition smoothly
- [ ] T063 [US3] Test theme change while toast message is displayed, verify toast updates color without visual glitches
- [ ] T064 [US3] Test System mode auto-switching: set to System, change device theme in OS settings, verify app transitions smoothly within 500ms

### Performance Validation

- [ ] T065 [US3] Run performance profiler during theme switch, verify no GC spikes (brush/color object reuse working)
- [ ] T066 [US3] Measure theme switch time with stopwatch: from button tap to last pixel update, verify <300ms (NFR-P1)
- [ ] T067 [US3] Measure preference persistence time, verify <100ms (NFR-P2)

**Checkpoint**: US3 complete - Theme transitions are smooth, performant, and visually polished

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final quality assurance, documentation, and production readiness

### Accessibility Testing

- [ ] T068 Enable iOS VoiceOver, navigate to Settings > Theme, verify theme options are announced ("Light theme", "Dark theme", "System theme")
- [ ] T069 Enable iOS VoiceOver, select a theme, verify selection change is announced ("Light theme selected")
- [ ] T070 Enable Android TalkBack, perform same theme selection accessibility tests
- [ ] T071 Verify all text is readable with screen reader active, no low-contrast warnings

### Documentation Updates

- [ ] T072 Update README.md with coffee theme feature description and theme selection instructions
- [ ] T073 Verify quickstart.md accurately reflects implemented theme system (semantic tokens, theme service usage)
- [ ] T074 Add code comments to IThemeService interface documenting performance requirements and thread safety

### Code Review Preparation

- [ ] T075 Run static analysis (dotnet build with warnings as errors), verify no warnings in theme-related code
- [ ] T076 Verify all test tasks (T008-T009, T013, T030-T036, T052-T055) pass with ≥80% coverage for ThemeService
- [ ] T077 Verify ColorContrastTests pass for all light and dark mode token combinations
- [ ] T078 Create PR with title "feat: Coffee-themed color system with user-selectable theme modes" and link to spec.md

### Final Manual Testing Checklist

- [ ] T079 Test app launch with no saved preference, verify defaults to System theme matching device
- [ ] T080 Test app launch with corrupted preference value (manually set to "Invalid"), verify defaults to System without crash
- [ ] T081 Test rapid theme changes (Light→Dark→Light→System in <2 seconds), verify no UI corruption or crashes
- [ ] T082 Test theme persistence across app kill and relaunch on iOS device
- [ ] T083 Test theme persistence across app kill and relaunch on Android device
- [ ] T084 Test all 6 pages in both light and dark themes, verify visual consistency and readability

---

## Implementation Strategy

### MVP Delivery

**User Story 1 (P1)** is the MVP - delivers immediate visual value:
- Coffee color palette applied to all screens
- Works with device theme (light/dark)
- No user controls yet, just responsive to system theme

### Incremental Delivery

1. **Week 1**: US1 (Coffee palette) → Deploy, gather feedback on colors
2. **Week 2**: US2 (Theme selection) → Users can override system theme
3. **Week 3**: US3 (Smooth transitions) → Polish and performance tuning

### Parallel Execution Opportunities

**During US1 (Phase 3)**:
- T008-T009 (contrast tests) can run parallel to T010-T012 (color definition)
- T022-T027 (page verification) can be split among multiple testers

**During US2 (Phase 4)**:
- T030-T036 (writing tests) can run parallel to T046-T051 (UI implementation)
- T037-T043 (ThemeService impl) must complete before T044-T045 (DI registration)

**During US3 (Phase 5)**:
- T057-T059 (performance) can run parallel to T060-T064 (visual testing)

### Dependencies

```
Phase 1 (Setup) → Phase 2 (Foundation)
                       ↓
       Phase 3 (US1: Coffee Palette) 🎯 MVP
                       ↓
       Phase 4 (US2: Theme Selection)
                       ↓
       Phase 5 (US3: Smooth Transitions)
                       ↓
       Phase 6 (Polish)
```

**Story Independence**:
- US1 is standalone MVP (no dependencies on US2/US3)
- US2 requires US1 complete (needs color palette to switch between)
- US3 requires US2 complete (needs theme switching to optimize)

---

## Summary

**Total Tasks**: 84
- **Setup**: 3 tasks
- **Foundational**: 4 tasks
- **US1 (MVP)**: 22 tasks (T008-T029)
- **US2**: 27 tasks (T030-T056)
- **US3**: 11 tasks (T057-T067)
- **Polish**: 17 tasks (T068-T084)

**Parallel Opportunities**: 15 tasks marked with [P] across all phases

**Independent Test Criteria**:
- **US1**: Launch app, verify coffee colors in light mode (6 pages), switch to dark mode, verify all pages adapt
- **US2**: Select theme in Settings, verify immediate change, restart app, verify persistence
- **US3**: Switch themes rapidly, verify smooth transitions <300ms with no white flashes

**Suggested MVP Scope**: Phase 3 (US1 only) - 22 tasks delivers coffee-themed interface

**Performance Targets**: Theme switch <300ms, persistence <100ms, system detection <500ms (all validated in Phase 5)

**Quality Gates**: All tests pass (30 test tasks), WCAG AA contrast validated, static analysis clean, 80% coverage

---

## Phase 7: Bug Fixes (Visual Theme Issues)

**Purpose**: Fix discovered theme reactivity and styling issues

### System Theme Tracking Issue

- [X] T085 [BUG] Investigate why app stops responding to system theme changes after user selects "System" theme following Light or Dark selection
- [X] T086 [BUG] Verify Application.Current.RequestedThemeChanged event handler is still subscribed after theme mode changes
- [X] T087 [BUG] Test if ThemeService.OnSystemThemeChanged is being called when device theme changes while in System mode
- [X] T088 [BUG] Add logging to ThemeService to track event subscription lifecycle and theme change triggers
- [X] T089 [BUG] Fix System theme tracking bug (changed ApplyTheme to use AppTheme.Unspecified for System mode to allow RequestedThemeChanged events)

### Bottom Sheet Theme Colors

- [X] T090 [BUG] Verify EquipmentFormBottomSheet component applies theme colors using ThemeKey() instead of hardcoded colors
- [X] T091 [BUG] Verify UserProfileFormBottomSheet component applies theme colors using ThemeKey() instead of hardcoded colors
- [X] T092 [BUG] Test all bottom sheets (Equipment, Beans, UserProfile) in both light and dark modes to verify proper theme application
- [X] T093 [BUG] Ensure all bottom sheet components recreate on theme change or use reactive theme bindings

### AppIcons Theme Reactivity

- [X] T094 [BUG] Investigate why AppIcons (edit, delete) on EquipmentManagementPage don't change color between light and dark modes
- [X] T095 [BUG] Review AppIcons implementation to determine if colors are hardcoded or using theme keys
- [X] T096 [BUG] Update AppIcons to use theme-aware colors (e.g., ThemeKey for icon color or ApplicationTheme color properties)
- [X] T097 [BUG] Verify AppIcons update color immediately when theme changes across all pages using them
- [X] T098 [BUG] Test AppIcons in light mode (should use TextPrimary #352B23) and dark mode (should use TextPrimary #F8F6F4)

**Checkpoint**: All Phase 7 bug fixes implemented - Bottom sheets use proper theme colors, AppIcons react to theme changes, system theme tracking has debug logging

**Checkpoint**: All theme reactivity bugs fixed - System theme tracking works, bottom sheets use correct colors, AppIcons react to theme changes
