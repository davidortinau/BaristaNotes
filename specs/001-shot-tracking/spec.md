# Feature Specification: Shot Maker, Recipient, and Preinfusion Tracking

**Feature Branch**: `001-shot-tracking`  
**Created**: 2025-12-03  
**Status**: Draft  
**Input**: User description: "Track shot makers, recipients, and preinfusion time"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record Shot with Maker and Recipient (Priority: P1)

As a barista, I want to record who pulled the shot and who it was made for, so I can track which barista made each drink and maintain accountability for quality.

**Why this priority**: Core functionality that enables tracking responsibility and customer service. Without this, we cannot attribute shots to specific baristas or customers, which is essential for quality tracking and customer relationship management.

**Independent Test**: Can be fully tested by logging a shot, selecting a maker from the user list, selecting a recipient from the user list, saving the shot, and verifying both users are displayed on the shot record in the activity feed.

**Acceptance Scenarios**:

1. **Given** I am logging a new shot, **When** I select a barista from the "Made By" field, **Then** that barista is associated with the shot record
2. **Given** I am logging a new shot, **When** I select a customer from the "Made For" field, **Then** that customer is associated with the shot record
3. **Given** I am editing an existing shot, **When** I change the "Made By" or "Made For" fields, **Then** the updated associations are saved
4. **Given** I am viewing the activity feed, **When** I look at a shot record, **Then** I can see both the maker and recipient names displayed
5. **Given** I am logging a shot for myself, **When** I select the same person for both "Made By" and "Made For", **Then** the system accepts this valid scenario

---

### User Story 2 - Track Preinfusion Time (Priority: P2)

As a barista experimenting with extraction techniques, I want to record preinfusion time for each shot, so I can correlate preinfusion duration with taste outcomes and optimize my espresso recipes.

**Why this priority**: Important for advanced baristas who dial in recipes, but not critical for basic shot logging. Preinfusion is a key variable in espresso extraction that affects taste and quality.

**Independent Test**: Can be fully tested by logging a shot, entering a preinfusion time value, saving the shot, and verifying the preinfusion time is displayed on the shot record and can be edited later.

**Acceptance Scenarios**:

1. **Given** I am logging a new shot, **When** I enter a preinfusion time in seconds, **Then** that value is saved with the shot record
2. **Given** I am editing an existing shot, **When** I update the preinfusion time, **Then** the new value is saved
3. **Given** I am viewing a shot record, **When** preinfusion time was recorded, **Then** I can see the preinfusion duration displayed
4. **Given** I am logging a shot without preinfusion, **When** I leave the preinfusion field empty, **Then** the system accepts this as optional data

---

### User Story 3 - Filter and Analyze by Maker and Recipient (Priority: P3)

As a cafe owner, I want to filter shots by barista and customer, so I can analyze individual barista performance and customer preferences over time.

**Why this priority**: Valuable for analytics and reporting, but the feature is still useful without filtering. This enables business insights and quality improvement.

**Independent Test**: Can be fully tested by logging multiple shots with different makers and recipients, applying filters to view shots by specific barista or customer, and verifying the filtered results show only matching shots.

**Acceptance Scenarios**:

1. **Given** I have shots logged by multiple baristas, **When** I filter by a specific barista, **Then** I see only shots made by that barista
2. **Given** I have shots logged for multiple customers, **When** I filter by a specific customer, **Then** I see only shots made for that customer
3. **Given** I am viewing filtered results, **When** I clear the filter, **Then** I see all shots again

---

### Edge Cases

- What happens when a user profile is deleted but they are associated with historical shots? (System should maintain the association but mark the user as inactive/deleted)
- What happens when I log a shot without selecting a maker or recipient? (System should allow optional maker/recipient - defaults to empty/unassigned)
- What happens when I enter an invalid preinfusion time (negative or extremely large)? (System should validate input and show appropriate error message)
- What happens when I try to log a shot with only maker or only recipient selected? (System should allow this - both fields are independent and optional)
- How does the system handle shots made by multiple baristas? (Out of scope - single maker per shot for MVP)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow users to select a barista from the existing user list as the shot maker
- **FR-002**: System MUST allow users to select a customer from the existing user list as the shot recipient
- **FR-003**: System MUST allow users to enter preinfusion time in seconds as a decimal value (e.g., 5.5 seconds)
- **FR-004**: System MUST persist maker, recipient, and preinfusion time with the shot record
- **FR-005**: System MUST display maker and recipient names on shot records in the activity feed
- **FR-006**: System MUST allow maker and recipient fields to be optional (shots can be logged without this information)
- **FR-007**: System MUST allow editing of maker, recipient, and preinfusion time on existing shot records
- **FR-008**: System MUST validate preinfusion time input to ensure non-negative values
- **FR-009**: System MUST display preinfusion time on shot records when present
- **FR-014**: System MUST display the preinfusion time field in the shot logging form positioned below the extraction time field with consistent styling
- **FR-010**: System MUST maintain maker and recipient associations even if the user profile is later deleted or deactivated
- **FR-011**: System MUST remember the last-used maker, recipient, bean, machine, grinder, and other shot parameters using .NET MAUI Preferences API
- **FR-012**: System MUST pre-populate the shot logging form with the last-used values on subsequent uses
- **FR-013**: System MUST persist last-used preferences across app restarts

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: Page load time MUST be <2 seconds (p95)
- **NFR-P2**: API responses MUST complete in <500ms (p95)
- **NFR-P3**: User interactions (field updates) MUST provide feedback within 100ms
- **NFR-P4**: User selection pickers MUST respond to input within 100ms

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: All user selection fields MUST be keyboard navigable
- **NFR-A2**: WCAG 2.1 Level AA compliance MUST be verified for new form fields
- **NFR-A3**: Touch targets for picker/selection controls MUST be minimum 44x44px
- **NFR-A4**: Screen readers MUST announce field labels and selected values

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: User selection controls MUST follow existing design system patterns used elsewhere in the app
- **NFR-UX2**: Error messages for invalid preinfusion values MUST be user-friendly with recovery steps
- **NFR-UX3**: Loading states MUST be shown when fetching user lists
- **NFR-UX4**: Form fields MUST be responsive and work on mobile, tablet, and desktop

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: Code coverage MUST meet 80% minimum (100% for validation logic)
- **NFR-Q2**: Static analysis MUST pass without warnings
- **NFR-Q3**: All code MUST pass peer review

### Key Entities

- **ShotRecord** (existing entity - enhanced): 
  - Maker: Reference to UserProfile (who pulled the shot)
  - Recipient: Reference to UserProfile (who the shot was made for)
  - PreinfusionTime: Decimal value in seconds (optional)
  
- **UserProfile** (existing entity): 
  - Referenced by ShotRecord for maker and recipient associations
  - Can be associated with multiple shots as either maker or recipient

## Clarifications

### Session 2025-12-03

- Q: How should the system remember last-used values for maker, recipient, bean, machine, etc. between sessions? → A: Use .NET MAUI Preferences API - no table or database needed
- Q: What unit and data type should be used for preinfusion time to maintain consistency with other time-based metrics (ActualTime, ExpectedTime)? → A: Decimal seconds, same as other time fields
- Q: Should preinfusion time be required or optional when logging a shot? → A: Optional, defaults to null/0
- Q: Where should the preinfusion time field be displayed in the shot logging form? → A: Below extraction time, with same styling as other time fields

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can add maker and recipient information to a shot in under 10 seconds
- **SC-002**: 100% of shot records correctly persist maker, recipient, and preinfusion time data without data loss
- **SC-003**: Users can successfully edit maker, recipient, or preinfusion time on 100% of existing shots
- **SC-004**: System displays maker and recipient names on all shot records in the activity feed where this information was provided
- **SC-005**: Form validation prevents invalid preinfusion time values (negative numbers) in 100% of cases
- **SC-006**: User selection controls load and respond to interaction within 100ms for lists up to 100 users
