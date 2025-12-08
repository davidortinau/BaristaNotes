# Feature Specification: Bean Rating Tracking and Bag Management

**Feature Branch**: `001-bean-rating-tracking`  
**Created**: 2025-12-07  
**Status**: Draft  
**Input**: User description: "I want to be able to see how many shots have been logged for a bean when I view it, by rating. I'm envisioning similar to a product review with a list of the rating levels and a count next to them. I'd also like an aggregate rating. The goal is to help me know if we liked a bean or not so we can decide if we are ordering more of them. Which brings up another concern - as I get future bags of the same beans, how do I keep track of them together? The only value that will change is the roasting date. So when I get a new bag of a bean that I've already logged, I will want to add the new bag by roasting date and start associating shots to that bag. I want to see my ratings and aggregate ratings by bag and by bean."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Aggregate Bean Ratings (Priority: P1)

As a barista, I want to see an aggregate rating for a bean (averaged across all bags) so I can quickly decide if I should reorder this bean.

**Why this priority**: This is the core decision-making feature - helping the user determine if they should buy more of a specific bean variety. Without this, the entire feature loses its primary value.

**Independent Test**: Can be fully tested by viewing any bean that has logged shots and seeing the aggregate rating and rating distribution. Delivers immediate value by answering "Should I reorder this bean?"

**Acceptance Scenarios**:

1. **Given** a bean has multiple logged shots across one or more bags, **When** I view the bean details, **Then** I see an aggregate rating (average of all shots for this bean)
2. **Given** a bean has logged shots with various ratings (e.g., 2 five-star shots, 1 four-star shot, 1 three-star shot), **When** I view the bean details, **Then** I see a rating distribution showing count for each rating level (5 stars: 2, 4 stars: 1, 3 stars: 1, etc.)
3. **Given** a bean has no logged shots yet, **When** I view the bean details, **Then** I see an indicator that no ratings are available yet

---

### User Story 2 - Bag-Based Shot Logging (Priority: P2)

As a barista, when I log a new shot, I want to select a bag (not a bean) so that shots are properly associated with the specific physical bag I'm using, and I want to mark bags as complete when finished so they don't clutter my selection list.

**Why this priority**: Essential for daily workflow - users log shots multiple times per day. The bag selection must be the primary interface, not bean selection. Bag completion prevents confusion and keeps the UI clean.

**Independent Test**: Can be tested by logging shots while selecting from active bags, then marking a bag complete and verifying it no longer appears in the shot logging bag picker. Delivers value by streamlining the most frequent user action.

**Acceptance Scenarios**:

1. **Given** I am logging a new shot, **When** I open the shot logging page, **Then** I see a bag picker showing only active (incomplete) bags, ordered by most recent roast date first
2. **Given** I have multiple bags for the same bean, **When** I select a bag in the shot logger, **Then** the bean information is displayed automatically (derived from the selected bag)
3. **Given** I have finished using a bag, **When** I mark it as complete, **Then** it no longer appears in the shot logging bag picker but remains visible in bag history views
4. **Given** I want to add a new bag, **When** I navigate to bean details and add a bag, **Then** that bag immediately becomes available in the shot logging bag picker
5. **Given** I have an existing bean with a finished bag, **When** I receive a new bag of the same bean with a different roasting date, **Then** I can add a new bag entry and start logging shots to it

---

### User Story 3 - View Ratings by Individual Bag (Priority: P3)

As a barista, I want to see ratings for each individual bag of a bean so I can identify if certain roasting dates produce better results than others.

**Why this priority**: This provides deeper insights and helps identify quality variations between roasting batches. It's valuable but not essential for the core reordering decision.

**Independent Test**: Can be tested by viewing a bean with multiple bags and seeing separate rating summaries for each bag. Delivers analytical value for quality-conscious users.

**Acceptance Scenarios**:

1. **Given** a bean has multiple bags with logged shots, **When** I view the bean details, **Then** I can see rating distribution and aggregate rating for each bag separately
2. **Given** I'm viewing a specific bag's ratings, **When** I compare it to other bags of the same bean, **Then** I can identify which roasting dates performed best
3. **Given** a bag has no logged shots yet, **When** I view that bag's details, **Then** I see an indicator that no ratings are available for this bag

---

### Edge Cases

- What happens when a bean has only one logged shot? (Show single rating without statistical significance warning)
- What happens when multiple bags have the same roasting date? (Allow duplicate dates, distinguish by entry timestamp or allow user to add notes)
- What happens when all bags for a bean are marked complete? (Show "No active bags" message with option to add new bag or reactivate a completed bag)
- What happens if a user tries to log a shot but no active bags exist? (Prompt to add a new bag first)
- What happens if a user deletes a shot? (Recalculate aggregate and bag-specific ratings immediately)
- What if a bean has 50+ bags logged over time (including completed)? (Display active bags first, then completed bags, consider pagination for large lists)
- Can a user un-complete a bag? (Yes, via bag detail page - reactivate by unmarking complete status)

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display an aggregate rating for each bean, calculated as the average of all logged shots across all bags of that bean
- **FR-002**: System MUST display a rating distribution breakdown showing the count of shots at each rating level (e.g., 5 stars: 3, 4 stars: 5, 3 stars: 1)
- **FR-003**: Users MUST be able to create multiple bag entries for the same bean, with each bag distinguished by its roasting date
- **FR-004**: Shot logging interface MUST present bag selection as the primary picker (not bean selection), showing only active (incomplete) bags
- **FR-005**: System MUST allow users to mark a bag as "complete" to remove it from the shot logging bag picker while preserving it in history views
- **FR-006**: System MUST display ratings at two levels: aggregate (all bags of a bean) and per-bag (individual bag ratings)
- **FR-007**: System MUST maintain the relationship between beans and their bags such that deleting or modifying a shot updates both bag-level and bean-level ratings
- **FR-008**: Users MUST be able to view a list of all bags for a given bean, ordered by roasting date (most recent first), with visual distinction between active and completed bags
- **FR-009**: System MUST display the roasting date and bean information for each bag when viewing bag or shot logging interfaces
- **FR-010**: System MUST recalculate aggregate and bag-specific ratings immediately when shots are added, modified, or deleted
- **FR-011**: Users MUST be able to reactivate a completed bag (unmark as complete) if needed

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: Bean details view (including ratings) MUST load in <2 seconds (p95)
- **NFR-P2**: Rating calculations MUST complete in <500ms (p95)
- **NFR-P3**: User interactions for adding a new bag MUST provide feedback within 100ms
- **NFR-P4**: Rating distribution display MUST render within 1 second for beans with up to 100 logged shots

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: All interactive elements (bag selection, rating views) MUST be keyboard navigable
- **NFR-A2**: WCAG 2.1 Level AA compliance MUST be verified for all rating display components
- **NFR-A3**: Touch targets for bag selection and rating interactions MUST be minimum 44x44px
- **NFR-A4**: Screen readers MUST announce rating values and distribution counts

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: Rating display components MUST use existing design system patterns (similar to product review displays)
- **NFR-UX2**: Error messages for bag creation conflicts MUST be user-friendly with recovery steps
- **NFR-UX3**: Loading states MUST be shown when calculating or fetching ratings
- **NFR-UX4**: Responsive design MUST support viewing ratings on mobile, tablet, and desktop

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: Code coverage MUST meet 80% minimum (100% for rating calculation logic)
- **NFR-Q2**: Static analysis MUST pass without warnings
- **NFR-Q3**: All code MUST pass peer review

### Key Entities

- **Bean**: Represents a coffee bean variety (e.g., "Ethiopian Yirgacheffe"). Has a name, origin, roaster, and other descriptive attributes. A bean can have multiple bags over time.
- **Bag**: Represents a physical bag of a specific bean, distinguished by its roasting date. Linked to exactly one bean. Contains roasting date and tracks which shots were made from this bag.
- **Shot**: Represents a single espresso shot logged by the user. Contains a rating (typically 1-5 stars), timestamp, and reference to the specific bag it came from. May include additional notes or parameters (grind setting, extraction time, etc.).
- **Rating Aggregate (Bean-level)**: A calculated view showing the average rating and rating distribution for all shots across all bags of a bean.
- **Rating Aggregate (Bag-level)**: A calculated view showing the average rating and rating distribution for all shots from a specific bag.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can view aggregate bean ratings (average and distribution) within 2 seconds of navigating to a bean detail view
- **SC-002**: Users can successfully add a new bag for an existing bean and associate shots with it in under 1 minute
- **SC-003**: 95% of users can correctly identify their highest-rated bean when viewing their bean list
- **SC-004**: Rating calculations remain accurate (within 0.01 of mathematically correct value) for beans with up to 100 logged shots
- **SC-005**: Users can distinguish between bag-level and bean-level ratings within 10 seconds of viewing bean details
