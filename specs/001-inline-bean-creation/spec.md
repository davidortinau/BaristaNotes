# Feature Specification: Inline Bean Creation During Shot Logging

**Feature Branch**: `001-inline-bean-creation`  
**Created**: 2025-12-08  
**Status**: Draft  
**Input**: User description: "when logging a shot if i have no bags of beans and no beans, then i need to be able to first create a bean before i create a bag of that bean"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create Bean When No Beans Exist (Priority: P1)

As a new user logging my first shot, when I have no beans or bags in the system, I want to be guided through creating a bean first, then a bag of that bean, so that I can log my shot without leaving the shot logging flow.

**Why this priority**: This is the core scenario that unblocks first-time users. Without this, new users cannot log shots at all if they haven't pre-configured beans and bags.

**Independent Test**: Can be fully tested by launching the app fresh with empty database, navigating to shot logging, and verifying the user can create a bean and bag inline.

**Acceptance Scenarios**:

1. **Given** I have no beans in the system, **When** I try to log a shot, **Then** I see a prompt indicating I need to create a bean first with a clear call-to-action
2. **Given** I am on the shot logging page with no beans, **When** I tap the "Create Bean" action, **Then** I can create a new bean without navigating away from the shot logging context
3. **Given** I just created a bean inline, **When** the bean is saved, **Then** I am automatically prompted to create a bag for that bean

---

### User Story 2 - Create Bag When Beans Exist But No Active Bags (Priority: P2)

As a user who has beans configured but no active bags (all bags are completed/inactive), I want to quickly add a new bag for an existing bean directly from the shot logging page, so that I can continue logging shots efficiently.

**Why this priority**: Common scenario for returning users who have finished their previous bag. Less critical than P1 because the existing "Add New Bag" button partially addresses this.

**Independent Test**: Can be tested by having beans in the system but marking all bags as complete, then attempting to log a shot.

**Acceptance Scenarios**:

1. **Given** I have beans but no active bags, **When** I am on the shot logging page, **Then** I see the "Add New Bag" button (existing behavior)
2. **Given** I tap "Add New Bag" when beans exist, **When** the bag creation starts, **Then** I can select from my existing beans
3. **Given** I create a bag for an existing bean, **When** the bag is saved, **Then** it appears in the bag picker and can be selected for my shot

---

### User Story 3 - Seamless Flow Continuation After Creation (Priority: P3)

As a user who just created a bean and/or bag inline, I want the shot logging form to be pre-populated with my new selections, so that I can immediately continue logging my shot.

**Why this priority**: Enhances user experience by eliminating extra selection steps, but the core functionality works without this polish.

**Independent Test**: Create a new bean and bag inline, then verify the shot logging form automatically selects the newly created items.

**Acceptance Scenarios**:

1. **Given** I created a new bean and bag inline, **When** I return to the shot logging form, **Then** the newly created bag is automatically selected
2. **Given** I created a new bag for an existing bean, **When** I return to the shot logging form, **Then** the bag picker shows and selects my new bag

---

### Edge Cases

- What happens when the user cancels bean creation mid-flow? They should return to the shot logging empty state with the original prompt.
- How does the system handle validation errors during inline bean creation? Errors should be shown inline without dismissing the creation flow.
- What if the user has beans but they are all inactive/deleted? System should treat this as "no beans" and show the create bean prompt.
- What happens if bag creation fails after successful bean creation? The bean should persist, and user should be able to retry bag creation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST detect when no active beans exist and display an appropriate empty state on the shot logging page
- **FR-002**: System MUST provide a "Create Bean" action when no beans are available
- **FR-003**: Users MUST be able to create a bean via a modal form that overlays the shot logging page (not full-page navigation)
- **FR-004**: System MUST automatically prompt for bag creation after successful inline bean creation
- **FR-005**: Users MUST be able to create a bag via a modal form after creating a bean
- **FR-006**: System MUST auto-select newly created bag after inline creation flow completes and modal dismisses
- **FR-007**: System MUST allow cancellation at any point in the modal creation flow, returning user to shot logging
- **FR-008**: System MUST validate bean name is not empty before allowing creation
- **FR-009**: System MUST validate roast date is not in the future during bag creation
- **FR-010**: System MUST refresh the bag picker list after modal creation without requiring page refresh

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: Inline creation forms MUST appear within 100ms of user action
- **NFR-P2**: Bean and bag save operations MUST complete within 500ms
- **NFR-P3**: UI MUST provide immediate feedback (loading indicator) for save operations

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: All inline creation form elements MUST be keyboard navigable
- **NFR-A2**: Screen readers MUST announce state changes during inline creation flow
- **NFR-A3**: Touch targets for action buttons MUST be minimum 44x44px

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: Inline creation UI MUST use consistent styling with existing form fields
- **NFR-UX2**: Error messages MUST be user-friendly with clear recovery steps
- **NFR-UX3**: Loading states MUST be shown during save operations
- **NFR-UX4**: Cancel/back actions MUST be clearly visible and accessible

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: New code MUST have unit test coverage for creation logic
- **NFR-Q2**: Integration tests MUST cover the full inline creation flow
- **NFR-Q3**: All code MUST follow existing MauiReactor patterns in the codebase

### Key Entities *(include if feature involves data)*

- **Bean**: Represents a coffee bean type with name, roaster, origin, and notes. Required before creating a bag.
- **Bag**: Represents a physical bag of beans with roast date and notes. Links to a Bean. Required before logging a shot.
- **Shot/ShotRecord**: The espresso shot being logged. Links to a Bag, which provides bean information.

## Clarifications

### Session 2025-12-09

- Q: What UI pattern should be used for "inline" creation (expandable form, bottom sheet/modal, or full-screen modal)? → A: Bottom sheet/modal form that overlays the shot logging page and dismisses on completion

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: New users can complete first shot log (including bean and bag creation) in under 2 minutes
- **SC-002**: 95% of inline creation attempts complete successfully without errors
- **SC-003**: Users report they can log shots without pre-configuring data (qualitative via user feedback)
- **SC-004**: Zero navigation back-button confusion during inline creation flow (users complete flow or explicitly cancel)
- **SC-005**: Inline creation flow has same or better completion rate compared to standalone bean/bag management pages
