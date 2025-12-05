# Feature Specification: Coffee-Themed Color System with Theme Selection

**Feature Branch**: `002-coffee-theme`  
**Created**: 2025-12-04  
**Status**: Draft  
**Input**: User description: "Style the application with coffee-themed colors and implement user-selectable theme modes (light/dark/system)"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Apply Coffee Color Palette to All UI Elements (Priority: P1) 🎯 MVP

Baristas open the app and immediately see a warm, inviting coffee-themed interface that uses a semantic color palette aligned with the coffee shop aesthetic. All screens, cards, text, buttons, and navigation elements use the coffee-themed colors consistently across light and dark modes.

**Why this priority**: This establishes the visual identity of the app and creates an immersive coffee-focused experience. Without this, the app lacks brand consistency and emotional connection to the coffee domain.

**Independent Test**: Launch the app in light mode and verify all screens (shot logging, activity feed, settings, equipment, beans, user profiles) display the coffee-themed color palette. Switch device to dark mode and verify all screens adapt to the dark coffee theme. Check that backgrounds, surfaces, text, buttons, and cards all use the defined coffee palette.

**Acceptance Scenarios**:

1. **Given** the app is launched in light mode, **When** the user views any screen, **Then** the background displays warm cream tones (#D2BCA5), surfaces use soft beige (#FCEFE1), and text is dark espresso (#352B23)
2. **Given** the app is launched in dark mode, **When** the user views any screen, **Then** the background displays rich brown (#48362E), surfaces maintain coffee tones, and text is light cream (#F8F6F4)
3. **Given** the user navigates between screens, **When** viewing cards and lists, **Then** card backgrounds use `SurfaceVariant` (#ECDAC4 light / #7D5A45 dark) and maintain consistent spacing and borders
4. **Given** the user interacts with buttons and controls, **When** tapping primary actions, **Then** buttons display coffee accent (#86543F) with cream text (#F8F6F4)
5. **Given** the user views navigation tabs, **When** selecting different tabs, **Then** active tabs highlight with coffee accent color and inactive tabs use muted tones

---

### User Story 2 - User-Controlled Theme Selection (Priority: P2)

Baristas access theme settings and choose their preferred theme mode (Light, Dark, or System) from a settings interface. The app immediately updates the theme and persists the selection across app restarts. When "System" is selected, the app automatically follows the device's system theme setting.

**Why this priority**: This provides user autonomy and accessibility. Some baristas work in bright environments and prefer light mode, others in dim environments prefer dark mode, and some want automatic switching based on time of day via system settings.

**Independent Test**: Open Settings, select "Light" theme, verify app updates immediately. Restart app, verify light theme persists. Select "Dark" theme, verify app updates. Select "System" theme, change device theme setting, verify app follows device theme.

**Acceptance Scenarios**:

1. **Given** the user is in Settings, **When** they tap "Theme" option, **Then** a theme selection interface appears showing "Light", "Dark", and "System" options with the current selection highlighted
2. **Given** the user selects "Light" theme, **When** the selection is made, **Then** the app immediately switches to light mode across all screens without requiring restart
3. **Given** the user selects "Dark" theme, **When** the selection is made, **Then** the app immediately switches to dark mode across all screens without requiring restart
4. **Given** the user selects "System" theme, **When** the device theme is light, **Then** the app displays light mode; when device theme is dark, app displays dark mode
5. **Given** the user has selected a theme preference, **When** the app is closed and reopened, **Then** the app displays the previously selected theme mode
6. **Given** "System" theme is active, **When** the device theme changes, **Then** the app automatically updates to match without requiring manual intervention

---

### User Story 3 - Smooth Theme Transitions (Priority: P3)

When users switch themes (manually or automatically via system theme), the app smoothly transitions between color schemes without jarring flashes or visual glitches. All UI elements animate their color changes in a coordinated, polished manner.

**Why this priority**: This enhances the premium feel of the app and prevents disorienting experiences when themes change. While not critical for functionality, it significantly improves user experience and app polish.

**Independent Test**: Select different themes in Settings and observe transitions. Change device theme while app is open (with System theme selected) and observe automatic transition. Verify no white flashes, no controls appearing unstyled momentarily, and colors change smoothly.

**Acceptance Scenarios**:

1. **Given** the user changes theme in Settings, **When** the new theme applies, **Then** colors transition smoothly over 200-300ms without white flashes or jarring jumps
2. **Given** "System" theme is active, **When** the device theme changes, **Then** the app transitions to the new theme smoothly without requiring user to navigate away and back
3. **Given** the user is viewing any screen during theme change, **When** the transition occurs, **Then** all elements (backgrounds, surfaces, text, buttons, icons) update in a coordinated manner
4. **Given** modals or overlays are open during theme change, **When** the transition occurs, **Then** the overlay and underlying screens both transition appropriately

---

### Edge Cases

- What happens when the app is in the middle of an animation (e.g., loading overlay, toast message) and the theme changes?
- How does the app handle theme changes while modals (shot logging, equipment editing) are open?
- What if user preferences are corrupted or missing—does the app default to system theme?
- How do custom semantic colors (Success, Warning, Error) appear in both light and dark modes?
- What happens to images with transparent backgrounds when theme changes?
- How do existing ApplicationTheme overrides interact with the new coffee color palette?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Application MUST replace all hardcoded colors with coffee-themed semantic color tokens defined in a centralized color system
- **FR-002**: Application MUST support three theme modes: Light, Dark, and System (follows device theme)
- **FR-003**: Application MUST provide a theme selection control in the Settings page with visual indicators for current selection
- **FR-004**: Application MUST persist user's theme preference across app restarts
- **FR-005**: Application MUST immediately apply theme changes when user selects a new theme mode
- **FR-006**: Application MUST automatically update theme when "System" mode is active and device theme changes
- **FR-007**: Application MUST apply coffee color palette to all UI elements including: backgrounds, surfaces, cards, text, buttons, icons, navigation, borders, shadows, and overlays
- **FR-008**: Application MUST maintain WCAG AA contrast ratios between text and backgrounds in both light and dark modes
- **FR-009**: Application MUST use semantic color tokens (Background, Surface, SurfaceVariant, Primary, TextPrimary, etc.) rather than direct color values in UI components
- **FR-010**: Application MUST reconcile new coffee palette with existing ApplicationTheme.cs MauiReactor theme implementation
- **FR-011**: Application MUST define light and dark variants for all semantic color tokens as specified in the design guidance
- **FR-012**: Application MUST update ApplicationTheme.cs to use coffee color palette while preserving MauiReactor style definitions

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: Theme switching MUST complete within 300ms (p95)
- **NFR-P2**: Theme persistence (save to preferences) MUST complete within 100ms
- **NFR-P3**: Initial theme load on app startup MUST complete within 200ms
- **NFR-P4**: System theme change detection MUST respond within 500ms when app is active

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: Text on backgrounds MUST maintain minimum 4.5:1 contrast ratio (WCAG AA)
- **NFR-A2**: Large text (18pt+) MUST maintain minimum 3:1 contrast ratio (WCAG AA)
- **NFR-A3**: Theme selection controls MUST be keyboard navigable on desktop platforms
- **NFR-A4**: Theme selection controls MUST announce current selection to screen readers

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: All screens MUST use coffee color palette semantic tokens consistently
- **NFR-UX2**: Theme changes MUST animate smoothly without white flashes or visual glitches
- **NFR-UX3**: Theme selection UI MUST clearly indicate current active theme
- **NFR-UX4**: Coffee color palette MUST align with provided design guidance from UI designer

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: Coffee color theme code MUST achieve 80% minimum test coverage
- **NFR-Q2**: Color token definitions MUST be centralized in a single location
- **NFR-Q3**: All color references in UI code MUST be reviewed to use semantic tokens

### Key Entities

- **ThemeMode**: Represents user's theme preference (Light, Dark, System) with persistence across sessions
- **CoffeeColorPalette**: Centralized definition of all semantic color tokens with light/dark variants including Background, Surface, SurfaceVariant, SurfaceElevated, Primary, OnPrimary, TextPrimary, TextSecondary, TextMuted, Outline
- **ThemePreference**: User's stored theme selection that persists in application preferences

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of UI screens display coffee-themed colors in both light and dark modes with no remaining default/placeholder colors visible
- **SC-002**: Theme switching occurs within 300ms of user selection with smooth visual transitions
- **SC-003**: Theme preference persists correctly across app restarts in 100% of test cases
- **SC-004**: All text-on-background combinations meet WCAG AA contrast requirements (4.5:1 for normal text, 3:1 for large text)
- **SC-005**: System theme mode automatically follows device theme changes within 500ms when app is active
- **SC-006**: Coffee color palette matches designer-provided specifications with no more than 2% RGB deviation
