# Feature Specification: Bean Detail Page

**Feature Branch**: `004-bean-detail-page`  
**Created**: December 5, 2025  
**Status**: Draft  
**Input**: User description: "create a bean detail page for adding and editing beans, replacing the current bottom sheet form. When I am viewing an existing bean profile, i want to see a list of shots associated to that bean from the most recent to the oldest. Display that below the profile form."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View and Edit Bean Details with Shot History (Priority: P1)

As a barista, I want to view a bean's full details on a dedicated page where I can edit the bean information and see all shots made with that bean, so I can track bean performance and make adjustments to my brewing.

**Why this priority**: This is the core value proposition - users need to see the relationship between beans and their shot results to improve their espresso quality over time. The shot history provides essential context for evaluating bean quality and freshness.

**Independent Test**: Navigate to an existing bean from the bean list, verify the bean details are displayed in an editable form, verify the shot history list shows shots using that bean in reverse chronological order, edit bean details and save, confirm changes persist.

**Acceptance Scenarios**:

1. **Given** I am on the bean management page with existing beans, **When** I tap on a bean item, **Then** I navigate to the bean detail page showing the bean's editable form and a list of associated shots below
2. **Given** I am viewing a bean detail page for a bean with shot history, **When** the page loads, **Then** I see shots displayed from most recent to oldest with key shot information (date, rating, actual time, actual output)
3. **Given** I am viewing a bean detail page with many shots, **When** I scroll down, **Then** additional shots are loaded (pagination) to maintain performance
4. **Given** I am on the bean detail page, **When** I modify the bean name, roaster, origin, roast date, or notes and tap Save, **Then** the bean is updated and I see a success message
5. **Given** I am on the bean detail page, **When** I tap the back button or Cancel, **Then** I navigate back to the bean management page without saving unsaved changes

---

### User Story 2 - Add New Bean via Detail Page (Priority: P1)

As a barista, I want to add new beans using a full-page form instead of a bottom sheet, so I have more space to enter bean information comfortably.

**Why this priority**: Adding beans is a core CRUD operation and replacing the bottom sheet with a full page improves usability, especially on smaller devices where bottom sheets feel cramped.

**Independent Test**: Tap "Add" on the bean management page, verify navigation to empty bean detail page, enter bean details, save and confirm bean appears in the list.

**Acceptance Scenarios**:

1. **Given** I am on the bean management page, **When** I tap the "+ Add" toolbar button, **Then** I navigate to the bean detail page with an empty form (no shot history section shown)
2. **Given** I am on the bean detail page adding a new bean, **When** I enter a valid name and tap Save, **Then** the bean is created and I navigate back to the bean management page with the new bean visible
3. **Given** I am on the bean detail page adding a new bean, **When** I tap Save without entering a name, **Then** I see a validation error "Bean name is required" and remain on the page
4. **Given** I am on the bean detail page adding a new bean, **When** I select a roast date in the future and tap Save, **Then** I see a validation error "Roast date cannot be in the future"

---

### User Story 3 - Delete Bean from Detail Page (Priority: P2)

As a barista, I want to delete a bean from its detail page with confirmation, so I can remove beans I no longer use.

**Why this priority**: Delete functionality is important but secondary to the core view/edit/add flows. Users need to be able to clean up their bean list.

**Independent Test**: Navigate to an existing bean, tap Delete, confirm in the dialog, verify bean is removed from the list.

**Acceptance Scenarios**:

1. **Given** I am on the bean detail page for an existing bean, **When** I tap the Delete button, **Then** I see a confirmation dialog asking "Delete [bean name]? This action cannot be undone."
2. **Given** the delete confirmation dialog is showing, **When** I tap "Delete", **Then** the bean is deleted, I navigate back to the bean management page, and see a success message
3. **Given** the delete confirmation dialog is showing, **When** I tap "Cancel", **Then** the dialog closes and I remain on the bean detail page

---

### User Story 4 - Navigate to Shot Detail from Bean Page (Priority: P3)

As a barista, I want to tap on a shot in the bean's shot history to view that shot's details, so I can review and potentially edit past shots.

**Why this priority**: This provides useful navigation but is an enhancement to the core bean detail functionality.

**Independent Test**: On a bean detail page with shot history, tap a shot card, verify navigation to the shot detail/edit view.

**Acceptance Scenarios**:

1. **Given** I am on the bean detail page viewing shot history, **When** I tap on a shot item, **Then** I navigate to the shot logging page for that shot in edit mode

---

### Edge Cases

- What happens when a bean has no associated shots? Display an empty state message "No shots recorded with this bean yet"
- What happens when the bean being viewed is deleted by another user/device? Show an error and navigate back to the bean list
- How does the system handle saving when offline? Show error message "Unable to save. Please check your connection and try again."
- What happens when loading shots fails? Show an error message in the shot history section with a "Retry" button, but keep the bean form functional

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a dedicated full-page view for bean details replacing the current bottom sheet form
- **FR-002**: System MUST display all editable bean fields (Name, Roaster, Origin, Roast Date, Notes) in the form section
- **FR-003**: System MUST display shot history below the form when viewing an existing bean
- **FR-004**: System MUST sort shot history from most recent to oldest (descending by timestamp)
- **FR-005**: System MUST paginate shot history to prevent performance issues with beans that have many shots
- **FR-006**: System MUST validate bean name is required before saving
- **FR-007**: System MUST validate roast date is not in the future before saving
- **FR-008**: System MUST navigate back to bean management page after successful save or delete
- **FR-009**: System MUST show a confirmation dialog before deleting a bean
- **FR-010**: System MUST hide the shot history section when creating a new bean (no ID yet)
- **FR-011**: System MUST provide navigation from shot items to the shot logging page for editing
- **FR-012**: System MUST display key shot information in each shot item: timestamp, rating (if available), actual time, actual output, drink type

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: Page load time MUST be <2 seconds (p95)
- **NFR-P2**: Initial shot history load (first page) MUST complete in <500ms (p95)
- **NFR-P3**: Save operation MUST provide feedback within 100ms (show loading state)
- **NFR-P4**: Shot history pagination MUST load next page in <300ms

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: All form fields MUST have accessible labels
- **NFR-A2**: Touch targets MUST be minimum 44x44px
- **NFR-A3**: Screen readers MUST announce form field validation errors
- **NFR-A4**: Delete confirmation dialog MUST be keyboard navigable

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: Page layout MUST follow existing app patterns (see ProfileFormPage for reference)
- **NFR-UX2**: Error messages MUST be user-friendly with recovery guidance
- **NFR-UX3**: Loading states MUST be shown during save and shot history loading
- **NFR-UX4**: Form validation errors MUST display inline near the relevant field

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: Code coverage MUST meet 80% minimum for new code
- **NFR-Q2**: Must use existing services (IBeanService, IShotService) without modification
- **NFR-Q3**: Must follow MauiReactor component patterns established in the codebase

### Key Entities *(include if feature involves data)*

- **Bean**: Core entity being viewed/edited. Fields: Id, Name, Roaster, Origin, RoastDate, Notes, IsActive, CreatedAt
- **ShotRecord**: Related entity displayed in history. Linked to Bean via BeanId. Key display fields: Timestamp, Rating, ActualTime, ActualOutput, DrinkType

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can complete adding a new bean in under 30 seconds (from tap "Add" to successful save)
- **SC-002**: Users can view a bean with 50 shots and have the first page of shot history displayed within 2 seconds total page load
- **SC-003**: 100% of bean CRUD operations currently supported by the bottom sheet remain functional in the new detail page
- **SC-004**: Shot history displays correct association - shots shown MUST have the viewed bean's ID as their BeanId
- **SC-005**: Navigation flow is intuitive - users can navigate to bean detail, view/edit, and return to list without getting lost
