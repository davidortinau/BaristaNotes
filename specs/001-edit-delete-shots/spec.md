# Feature Specification: Edit and Delete Shots from Activity Page

**Feature Branch**: `001-edit-delete-shots`  
**Created**: 2025-12-03  
**Status**: Draft  
**Input**: User description: "Add the ability to delete and edit a shot from the activity page so I can remove and correct mistakes."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Delete Shot from Activity (Priority: P1)

As a barista, I want to delete a shot note from the activity page so I can remove mistakes or duplicate entries that clutter my history.

**Why this priority**: Core error correction capability - users need to fix mistakes immediately when they notice them. Without this, the activity feed becomes cluttered with incorrect data that cannot be removed.

**Independent Test**: Can be fully tested by creating a shot note, navigating to the activity page, deleting the shot, and verifying it no longer appears in the list or database.

**Acceptance Scenarios**:

1. **Given** I am viewing the activity page with multiple shot notes, **When** I select delete on a specific shot, **Then** a confirmation prompt appears asking me to confirm deletion
2. **Given** I see a delete confirmation prompt, **When** I confirm the deletion, **Then** the shot is removed from the activity list and I see success feedback
3. **Given** I see a delete confirmation prompt, **When** I cancel the deletion, **Then** the shot remains in the activity list and no changes are made
4. **Given** I have deleted a shot, **When** I refresh or reopen the activity page, **Then** the deleted shot does not reappear

---

### User Story 2 - Edit Shot from Activity (Priority: P2)

As a barista, I want to edit a shot note from the activity page so I can correct mistakes in the recorded data (time, output weight, rating, drink type) without having to delete and re-create the entry.

**Why this priority**: Enhances error correction by allowing in-place updates. Users can fix typos, adjust measurements, or update ratings without losing the timestamp and other metadata.

**Independent Test**: Can be fully tested by creating a shot note with specific values, navigating to the activity page, editing the shot details, and verifying the updated values persist correctly.

**Acceptance Scenarios**:

1. **Given** I am viewing the activity page with shot notes, **When** I select edit on a specific shot, **Then** I am taken to an edit form pre-filled with the current shot details
2. **Given** I am editing a shot, **When** I modify the shot time, output weight, rating, or drink type, **Then** I can save the changes and see success feedback
3. **Given** I am editing a shot, **When** I cancel without saving, **Then** I return to the activity page and no changes are applied
4. **Given** I have edited a shot, **When** I return to the activity page, **Then** the updated values are displayed correctly
5. **Given** I am editing a shot, **When** I attempt to save with invalid data (e.g., negative values, empty required fields), **Then** I see validation errors and cannot save until corrected

---

### User Story 3 - Access Edit/Delete Actions Quickly (Priority: P3)

As a barista, I want quick and intuitive access to edit and delete actions on each shot in the activity list so I can efficiently manage my entries without excessive navigation.

**Why this priority**: Improves usability and workflow efficiency. Users should not need multiple taps or complex gestures to access common management actions.

**Independent Test**: Can be fully tested by interacting with shot entries in the activity list and verifying edit/delete actions are accessible within 1-2 taps using standard mobile UI patterns.

**Acceptance Scenarios**:

1. **Given** I am viewing the activity page, **When** I look at each shot entry, **Then** I can clearly identify how to access edit and delete actions (e.g., swipe gesture, menu button, long press)
2. **Given** I am viewing a shot entry, **When** I perform the edit action gesture, **Then** I immediately navigate to the edit form
3. **Given** I am viewing a shot entry, **When** I perform the delete action gesture, **Then** I immediately see the delete confirmation prompt
4. **Given** I use a touch target for edit or delete, **When** I tap the target, **Then** it provides visual feedback (e.g., highlight, animation) to confirm my interaction

---

### Edge Cases

- What happens when I attempt to delete the only shot in the activity list?
- What happens when I try to edit a shot but lose network connectivity before saving?
- What happens if I navigate away from the edit form without saving - is there unsaved changes warning?
- What happens when I delete a shot that is currently being synced?
- What happens if the database operation fails during delete or edit?
- What happens when I attempt to delete a shot that has already been deleted by another device (sync conflict)?
- What happens when multiple users edit the same shot simultaneously on different devices?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a delete action for each shot note displayed in the activity list
- **FR-002**: System MUST display a confirmation prompt before deleting a shot note to prevent accidental deletion
- **FR-003**: System MUST permanently remove the shot note from the database when deletion is confirmed
- **FR-004**: System MUST provide visual feedback (toast notification) confirming successful deletion
- **FR-005**: System MUST provide an edit action for each shot note displayed in the activity list
- **FR-006**: System MUST display an edit form pre-populated with the current shot details when edit is selected
- **FR-007**: System MUST allow editing of shot time, output weight (grams out), taste rating, and drink type
- **FR-008**: System MUST preserve the original timestamp, bean, grind setting, and grams in values (read-only in edit mode)
- **FR-009**: System MUST validate edited values before allowing save (positive numbers for time/weight, valid rating scale)
- **FR-010**: System MUST persist edited values to the database when save is confirmed
- **FR-011**: System MUST provide visual feedback (toast notification) confirming successful edit save
- **FR-012**: System MUST allow canceling edit or delete operations without making changes
- **FR-013**: System MUST refresh the activity list to reflect deleted or edited shots immediately
- **FR-014**: System MUST handle database operation failures gracefully with user-friendly error messages

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: Delete operation MUST complete within 1 second including UI feedback
- **NFR-P2**: Edit form MUST load pre-filled data within 500ms
- **NFR-P3**: Save operation MUST complete within 2 seconds including validation and UI feedback
- **NFR-P4**: Activity list refresh after delete/edit MUST complete within 1 second

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: Edit and delete actions MUST be accessible via both touch gestures and visible buttons
- **NFR-A2**: Confirmation dialogs MUST be keyboard navigable with clear focus indicators
- **NFR-A3**: Touch targets for edit/delete MUST be minimum 44x44px
- **NFR-A4**: Screen readers MUST announce action availability and confirmation prompts

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: Delete confirmation MUST use UXDivers.Popups.Maui components consistent with app styling
- **NFR-UX2**: Edit form MUST use the same styling and validation patterns as the create shot form
- **NFR-UX3**: Success/error feedback MUST use consistent toast notification styling
- **NFR-UX4**: Swipe actions (if used) MUST follow platform conventions (iOS/Android)
- **NFR-UX5**: Loading states MUST be shown during save/delete operations

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: Unit tests MUST cover delete and edit service methods with 100% coverage
- **NFR-Q2**: Integration tests MUST verify database operations for delete and edit
- **NFR-Q3**: UI tests MUST verify user flows for both delete and edit scenarios
- **NFR-Q4**: All code MUST follow MVU pattern using Maui Reactor

### Key Entities *(include if feature involves data)*

- **ShotNote**: Represents an espresso shot record with attributes including timestamp, bean reference, equipment references, grind setting, input/output measurements, shot time, taste rating, drink type, and user profiles (made by/made for). Edit operations modify mutable fields (time, output weight, rating, drink type) while preserving immutable creation metadata.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can delete a shot note from the activity page within 3 taps (action → confirm → complete)
- **SC-002**: Users can edit a shot note and save changes within 30 seconds
- **SC-003**: 100% of delete operations either succeed with confirmation feedback or fail with clear error message
- **SC-004**: 100% of edit operations either succeed with confirmation feedback or fail with validation/error guidance
- **SC-005**: Users receive immediate visual feedback (within 1 second) after completing delete or edit actions
- **SC-006**: Activity list accurately reflects all deletes and edits without requiring manual refresh
- **SC-007**: Zero data loss or corruption when editing or deleting shots across multiple devices (sync integrity maintained)
