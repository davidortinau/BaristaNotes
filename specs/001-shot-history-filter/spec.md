# Feature Specification: Shot History Filter

**Feature Branch**: `001-shot-history-filter`  
**Created**: 2025-12-30  
**Status**: Draft  
**Input**: User description: "When viewing shot history activity I need to be able to filter by different criteria such as by Bean, by who the shot was made for, and by rating. I should be able to choose one or more of those parameters, and be able to clear/reset the filtering. When I apply the filtering I should be able to see a list of shots that meet that criterion in the same view. The filtering button should be a ToolbarItem and the options should be set in a UXD popup of some kind."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Filter Shot History by Bean (Priority: P1)

As a barista tracking my espresso shots, I want to filter my shot history by a specific coffee bean so that I can analyze my performance and results with different beans over time.

**Why this priority**: Filtering by bean is the most common use case—baristas frequently want to see all shots made with a particular bean to compare extraction parameters, identify trends, and optimize their technique for that specific coffee.

**Independent Test**: Can be fully tested by selecting a bean filter and verifying only shots made with that bean appear in the list. Delivers immediate value by allowing focused analysis of bean-specific shot data.

**Acceptance Scenarios**:

1. **Given** I am viewing the shot history with multiple shots from different beans, **When** I tap the filter toolbar button and select a specific bean, **Then** only shots made with that bean are displayed in the list.
2. **Given** I have applied a bean filter, **When** I view the shot list, **Then** each displayed shot shows the bean information matching my filter selection.
3. **Given** I have applied a bean filter, **When** no shots exist for that bean, **Then** I see an empty state message indicating no shots match the filter.

---

### User Story 2 - Filter Shot History by Made For (Priority: P2)

As a home barista who makes coffee for family members, I want to filter my shot history by who the shot was made for so that I can track preferences and results for each person.

**Why this priority**: Filtering by "made for" enables personalized tracking when making coffee for multiple people. This is the second most valuable filter as it helps users understand individual preferences.

**Independent Test**: Can be fully tested by selecting a person filter and verifying only shots made for that person appear. Delivers value by enabling person-specific analysis.

**Acceptance Scenarios**:

1. **Given** I am viewing shot history with shots made for different people, **When** I tap the filter toolbar button and select a specific person from the "made for" options, **Then** only shots made for that person are displayed.
2. **Given** I have applied a "made for" filter, **When** I view shot details, **Then** each shot confirms it was made for the selected person.

---

### User Story 3 - Filter Shot History by Rating (Priority: P2)

As a barista improving my technique, I want to filter my shot history by rating so that I can study my best shots and identify what made them successful.

**Why this priority**: Rating-based filtering helps users learn from their successes and failures. Equal priority to "made for" as both provide valuable analytical perspectives.

**Independent Test**: Can be fully tested by selecting a rating filter and verifying only shots with that rating appear. Delivers value by enabling quality-focused analysis.

**Acceptance Scenarios**:

1. **Given** I am viewing shot history with shots of various ratings, **When** I tap the filter toolbar button and select a rating value, **Then** only shots with that rating are displayed.
2. **Given** I want to see my best shots, **When** I filter by 5-star rating, **Then** only my top-rated shots appear in the list.

---

### User Story 4 - Apply Multiple Filters (Priority: P3)

As a barista doing detailed analysis, I want to combine multiple filters (bean + made for + rating) so that I can narrow down my shot history to very specific criteria.

**Why this priority**: Combined filtering provides advanced analytical capability but builds upon the single-filter stories. Most users will start with single filters.

**Independent Test**: Can be fully tested by applying multiple filter criteria and verifying only shots matching ALL criteria appear.

**Acceptance Scenarios**:

1. **Given** I have selected a bean filter, **When** I also select a "made for" filter, **Then** only shots matching BOTH criteria are displayed.
2. **Given** I have applied bean + made for + rating filters, **When** I view the results, **Then** only shots matching all three criteria appear.
3. **Given** I have multiple filters applied, **When** I view the filter popup, **Then** I can see which filters are currently active.

---

### User Story 5 - Clear/Reset Filters (Priority: P3)

As a user who has applied filters, I want to quickly clear all filters so that I can return to viewing my complete shot history.

**Why this priority**: Essential usability feature but dependent on filter functionality being implemented first.

**Independent Test**: Can be fully tested by applying filters, then clearing them, and verifying all shots reappear.

**Acceptance Scenarios**:

1. **Given** I have one or more filters applied, **When** I tap the clear/reset option in the filter popup, **Then** all filters are removed and the full shot history is displayed.
2. **Given** I have filters applied, **When** I clear them, **Then** the filter toolbar button returns to its default (unfiltered) state.
3. **Given** no filters are applied, **When** I open the filter popup, **Then** the clear/reset option is disabled or hidden.

---

### Edge Cases

- What happens when the selected bean has been deleted? Display shots with a fallback indicator showing the bean is no longer available.
- What happens when the selected person has been deleted? Display shots with a fallback indicator showing the person is no longer available.
- What happens when filtering returns zero results? Display an empty state with a clear message and option to adjust or clear filters.
- What happens when the user has no shots recorded yet? The filter button should be visible but filtering has no effect; empty state message explains no shots exist.
- What happens if a filter dropdown has many items (50+ beans)? List should be scrollable with adequate touch targets.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a filter button as a ToolbarItem on the shot history page
- **FR-002**: System MUST display a popup when the filter toolbar button is tapped
- **FR-003**: System MUST allow users to filter shots by selecting one or more beans from available beans
- **FR-004**: System MUST allow users to filter shots by selecting one or more people from the "made for" list
- **FR-005**: System MUST allow users to filter shots by selecting one or more rating values (1-5 stars)
- **FR-006**: System MUST apply filters as AND conditions (shots must match ALL selected criteria)
- **FR-007**: System MUST provide a clear/reset option to remove all active filters
- **FR-008**: System MUST visually indicate when filters are active (e.g., badge on toolbar button, highlighted state)
- **FR-009**: System MUST update the shot list immediately when filters are applied or cleared
- **FR-010**: System MUST preserve filter state when navigating away and returning to the shot history page within the same session
- **FR-011**: System MUST display an appropriate empty state when no shots match the filter criteria
- **FR-012**: System MUST show a count of filtered results (e.g., "Showing 12 of 45 shots")

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: Filter popup MUST open within 300ms of tapping the toolbar button
- **NFR-P2**: Shot list MUST update within 500ms after applying filters
- **NFR-P3**: User interactions MUST provide feedback within 100ms
- **NFR-P4**: Filtering MUST handle 1000+ shots without noticeable lag

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: All filter options MUST be accessible via screen reader
- **NFR-A2**: Filter popup MUST support VoiceOver/TalkBack navigation
- **NFR-A3**: Touch targets for filter options MUST be minimum 44x44px
- **NFR-A4**: Screen readers MUST announce filter changes and result counts

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: Filter popup MUST use UXDivers popup component consistent with existing app patterns
- **NFR-UX2**: Empty state MUST include helpful message explaining no matches found
- **NFR-UX3**: Loading indicator MUST be shown if filter application takes longer than 200ms
- **NFR-UX4**: Filter UI MUST work correctly in both portrait and landscape orientations

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: Filter logic MUST have unit test coverage for all filter combinations
- **NFR-Q2**: Static analysis MUST pass without warnings
- **NFR-Q3**: All code MUST pass peer review

### Key Entities

- **ShotRecord**: Individual espresso shot with timestamp, rating (1-5), MadeBy (user who made it), MadeFor (person it was made for), and relationship to Bag→Bean
- **Bean**: Coffee bean type with name and other attributes; shots are linked via Bag
- **UserProfile**: Represents people in the system; used for both MadeBy and MadeFor relationships
- **FilterCriteria**: Transient filter state containing selected beans, selected people (made for), and selected ratings

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can apply a filter and see filtered results within 2 seconds of opening the filter popup
- **SC-002**: Users can clear all filters and return to full list with a single action
- **SC-003**: 90% of users successfully apply their first filter without assistance or errors
- **SC-004**: Filter state is accurately reflected in both the popup and the toolbar button indicator
- **SC-005**: Zero data loss—filtering is read-only and does not modify any shot records

## Assumptions

- Bean list will be populated from existing beans associated with bags that have shot records
- "Made For" list will be populated from existing UserProfile entities
- Rating values are 1-5 integer scale (already established in existing ShotRecord model)
- Filter popup follows existing UXDivers popup patterns used elsewhere in the app
- Filter state does not need to persist across app restarts (session-only)
