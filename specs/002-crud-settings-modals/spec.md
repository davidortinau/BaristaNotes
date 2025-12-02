# Feature Specification: CRUD Settings with Modal Bottom Sheets

**Feature Branch**: `002-crud-settings-modals`  
**Created**: December 2, 2025  
**Status**: Draft  
**Input**: User description: "I need to be able to perform CRUD operations for equipment, beans, and user profiles. I would prefer to use modals with a bottom sheet if possible. Really all the CRUD stuff doesn't need to be top level navigation as it is now. A settings page via a toolbar item would be fine. the primary page should be the activity feed."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Primary Activity Feed Navigation (Priority: P1)

As a barista, I want the Activity Feed to be my primary landing page so I can immediately see my espresso shot history and recent activity when I open the app.

**Why this priority**: The Activity Feed is the most frequently accessed feature - users want to quickly review their shot history and track their espresso journey. Making it the primary page reduces navigation friction for the most common use case.

**Independent Test**: Can be fully tested by launching the app and verifying the Activity Feed displays as the primary/home page, and can be demonstrated as a standalone MVP experience.

**Acceptance Scenarios**:

1. **Given** I launch the app, **When** the app finishes loading, **Then** I see the Activity Feed as the primary view
2. **Given** I am on any other screen, **When** I navigate to the home/primary tab, **Then** I see the Activity Feed
3. **Given** I am viewing the Activity Feed, **When** I scroll through my history, **Then** I see my recent espresso shots in chronological order

---

### User Story 2 - Access Settings via Toolbar (Priority: P1)

As a user, I want to access settings through a toolbar item so that I can manage my equipment, beans, and profiles without cluttering the main navigation.

**Why this priority**: Moving CRUD operations out of top-level navigation simplifies the app's primary interface and focuses it on the core espresso tracking experience. The toolbar provides easy access without visual clutter.

**Independent Test**: Can be fully tested by tapping the settings toolbar item and verifying the settings page opens with all management options visible.

**Acceptance Scenarios**:

1. **Given** I am on the Activity Feed, **When** I look at the toolbar, **Then** I see a settings icon/button
2. **Given** I tap the settings toolbar item, **When** the navigation completes, **Then** I see the Settings page with options for Equipment, Beans, and User Profiles
3. **Given** I am on the Settings page, **When** I want to return to the main view, **Then** I can easily navigate back to the Activity Feed

---

### User Story 3 - Manage Equipment via Bottom Sheet Modal (Priority: P2)

As a barista, I want to add, view, edit, and delete my equipment using bottom sheet modals so that I can quickly manage my espresso machines and grinders without leaving the settings context.

**Why this priority**: Equipment management is essential for tracking shots accurately. Bottom sheet modals provide a modern, non-intrusive UX that keeps users oriented within the settings flow.

**Independent Test**: Can be fully tested by navigating to Settings, selecting Equipment, and performing all CRUD operations through the modal interface.

**Acceptance Scenarios**:

1. **Given** I am on the Settings page, **When** I tap "Equipment", **Then** I see a list of my existing equipment
2. **Given** I am viewing my equipment list, **When** I tap "Add Equipment", **Then** a bottom sheet modal appears with a form to enter equipment details (name, type, notes)
3. **Given** I am adding equipment, **When** I fill in the required fields and tap Save, **Then** the new equipment is saved and appears in my equipment list
4. **Given** I am viewing my equipment list, **When** I tap on an existing equipment item, **Then** a bottom sheet modal appears showing the equipment details with Edit and Delete options
5. **Given** I am editing equipment, **When** I modify fields and tap Save, **Then** the changes are persisted and reflected in the list
6. **Given** I am viewing equipment details, **When** I tap Delete and confirm, **Then** the equipment is removed from my list

---

### User Story 4 - Manage Beans via Bottom Sheet Modal (Priority: P2)

As a barista, I want to add, view, edit, and delete my coffee beans using bottom sheet modals so that I can track the beans I use for my espresso shots.

**Why this priority**: Bean tracking is critical for espresso experimentation. Users need to record roaster, origin, roast date, and other details to correlate with shot quality.

**Independent Test**: Can be fully tested by navigating to Settings, selecting Beans, and performing all CRUD operations through the modal interface.

**Acceptance Scenarios**:

1. **Given** I am on the Settings page, **When** I tap "Beans", **Then** I see a list of my coffee beans
2. **Given** I am viewing my beans list, **When** I tap "Add Bean", **Then** a bottom sheet modal appears with a form to enter bean details (name, roaster, origin, roast date, notes)
3. **Given** I am adding a bean, **When** I fill in the required fields and tap Save, **Then** the new bean is saved and appears in my beans list
4. **Given** I am viewing my beans list, **When** I tap on an existing bean, **Then** a bottom sheet modal appears showing the bean details with Edit and Delete options
5. **Given** I am editing a bean, **When** I modify fields and tap Save, **Then** the changes are persisted and reflected in the list
6. **Given** I am viewing bean details, **When** I tap Delete and confirm, **Then** the bean is removed from my list

---

### User Story 5 - Manage User Profiles via Bottom Sheet Modal (Priority: P2)

As a user, I want to add, view, edit, and delete user profiles using bottom sheet modals so that multiple people can track their espresso shots separately.

**Why this priority**: User profiles enable multi-user scenarios (e.g., shared home espresso setup). This supports household use cases where different baristas want to track their own shots.

**Independent Test**: Can be fully tested by navigating to Settings, selecting User Profiles, and performing all CRUD operations through the modal interface.

**Acceptance Scenarios**:

1. **Given** I am on the Settings page, **When** I tap "User Profiles", **Then** I see a list of existing profiles
2. **Given** I am viewing my profiles list, **When** I tap "Add Profile", **Then** a bottom sheet modal appears with a form to enter profile details (name)
3. **Given** I am adding a profile, **When** I fill in the required fields and tap Save, **Then** the new profile is saved and appears in my profiles list
4. **Given** I am viewing my profiles list, **When** I tap on an existing profile, **Then** a bottom sheet modal appears showing profile details with Edit and Delete options
5. **Given** I am editing a profile, **When** I modify fields and tap Save, **Then** the changes are persisted and reflected in the list
6. **Given** I am viewing profile details, **When** I tap Delete and confirm, **Then** the profile is removed from my list

---

### User Story 6 - Simplified Tab Navigation (Priority: P3)

As a user, I want a cleaner tab bar with only the essential navigation items so that the app feels focused and uncluttered.

**Why this priority**: Reducing tab bar items to Shot Log and Activity Feed (with settings in toolbar) creates a more focused, professional user experience.

**Independent Test**: Can be fully tested by viewing the tab bar and confirming only Shot Log and Activity Feed (History) tabs are present.

**Acceptance Scenarios**:

1. **Given** I am using the app, **When** I look at the tab bar, **Then** I see only "Shot Log" and "History" (Activity Feed) tabs
2. **Given** the tab bar is simplified, **When** I look for Equipment, Beans, or Profiles, **Then** I understand they are accessible via the Settings toolbar item

---

### Edge Cases

- What happens when a user tries to delete the last/only user profile? The system should prevent deletion and show a message explaining at least one profile is required.
- What happens when a user tries to delete equipment or beans that are associated with existing shot records? The system should warn the user and either prevent deletion or handle the orphaned references gracefully (soft delete/archive).
- How does the bottom sheet modal behave on different screen sizes? The modal should adapt to available screen height, scrolling internally if content exceeds visible area.
- What happens if the user dismisses the modal without saving? Unsaved changes are discarded; consider prompting for confirmation if changes were made.
- What happens during a save operation if there's a network/database error? Show a user-friendly error message and allow retry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display the Activity Feed as the primary/home page when the app launches
- **FR-002**: System MUST provide a toolbar item (settings icon) on the Activity Feed page for accessing settings
- **FR-003**: System MUST display a Settings page with navigation options for Equipment, Beans, and User Profiles
- **FR-004**: System MUST remove Equipment, Beans, and User Profiles from the main tab bar navigation
- **FR-005**: System MUST display only Shot Log and Activity Feed (History) in the tab bar
- **FR-006**: System MUST present CRUD forms for Equipment as bottom sheet modals
- **FR-007**: System MUST present CRUD forms for Beans as bottom sheet modals
- **FR-008**: System MUST present CRUD forms for User Profiles as bottom sheet modals
- **FR-009**: System MUST allow users to create new equipment with name (required), type (required), and notes (optional)
- **FR-010**: System MUST allow users to create new beans with name (required), roaster (optional), origin (optional), roast date (optional), and notes (optional)
- **FR-011**: System MUST allow users to create new profiles with name (required)
- **FR-012**: System MUST allow users to view, edit, and delete existing equipment
- **FR-013**: System MUST allow users to view, edit, and delete existing beans
- **FR-014**: System MUST allow users to view, edit, and delete existing user profiles
- **FR-015**: System MUST prevent deletion of the last remaining user profile
- **FR-016**: System MUST warn users before deleting equipment or beans that are referenced by existing shot records
- **FR-017**: System MUST persist all changes to the local database
- **FR-018**: System MUST provide visual confirmation when items are successfully created, updated, or deleted
- **FR-019**: System MUST allow users to dismiss bottom sheet modals by swiping down or tapping outside

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: Bottom sheet modals MUST animate smoothly and appear within 300ms
- **NFR-P2**: List views MUST load and display items within 500ms
- **NFR-P3**: Save/Delete operations MUST complete and show feedback within 1 second
- **NFR-P4**: Settings page navigation MUST complete within 200ms

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: All interactive elements MUST be keyboard navigable
- **NFR-A2**: Bottom sheet modals MUST be accessible via screen readers with proper announcements
- **NFR-A3**: Touch targets MUST be minimum 44x44px
- **NFR-A4**: Form fields MUST have visible labels and support assistive technologies
- **NFR-A5**: Settings toolbar item MUST have an accessible label (e.g., "Settings")

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: Bottom sheet modals MUST follow platform-native patterns (iOS/Android)
- **NFR-UX2**: Error messages MUST be user-friendly with clear recovery steps
- **NFR-UX3**: Loading states MUST be shown during save/delete operations
- **NFR-UX4**: Consistent visual styling MUST be applied across all bottom sheet modals
- **NFR-UX5**: Confirmation dialogs MUST be used for destructive actions (delete)

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: Code coverage MUST meet 80% minimum for new CRUD operations
- **NFR-Q2**: Static analysis MUST pass without warnings
- **NFR-Q3**: All code MUST pass peer review

### Key Entities

- **Equipment**: Represents espresso-making equipment (machines, grinders). Key attributes: name, type, notes, active status. Related to shot records.
- **Bean**: Represents coffee beans used for espresso. Key attributes: name, roaster, origin, roast date, notes, active status. Related to shot records.
- **User Profile**: Represents individual users of the app. Key attributes: name, creation date. Associated with shot records for multi-user tracking.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can access settings within 2 taps from any screen
- **SC-002**: Users can complete a full CRUD cycle (create, view, edit, delete) for any entity type within 60 seconds
- **SC-003**: Bottom sheet modals open and close within 300ms with smooth animations
- **SC-004**: Tab bar displays only 2 items (Shot Log, History) instead of previous 5
- **SC-005**: 100% of CRUD operations for Equipment, Beans, and Profiles are accessible via the Settings page
- **SC-006**: Users successfully complete equipment/bean/profile creation on first attempt 90% of the time
- **SC-007**: App launch displays Activity Feed as primary view 100% of the time

## Assumptions

- The existing Equipment, Bean, and UserProfile services (IEquipmentService, IBeanService, IUserProfileService) provide all necessary CRUD operations
- The app uses MauiReactor which supports bottom sheet modal implementations
- The existing data models (EquipmentDto, BeanDto, UserProfileDto) contain all fields needed for the CRUD forms
- Soft delete (marking as inactive) is preferred over hard delete for data integrity with shot records
- The app is single-device (no cloud sync), so offline/conflict handling is not required
