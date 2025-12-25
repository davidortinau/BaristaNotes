# Feature Specification: AI Bean Recommendations

**Feature Branch**: `001-ai-bean-recommendations`  
**Created**: 2024-12-24  
**Status**: Draft  
**Input**: User description: "When I start a new bag of beans for a bean of which I have no logged shots, I want AI to recommend a dose, grind, output, and duration for me. I want it to do that by adjusting the values on the view and showing a toast that 'we didn't have any shots for this bean, so we've created a recommended starting point of _____' and tell me those values also. Use the same loading UI bar we use for the AI recommendations of an existing shot. If I'm starting a new bag of beans for which I have a history, but which isn't the most recent bean I used I want the app to load up an AI recommended profile for me just like above but based on the history of what I've logged. The toast message should be something like 'I see you're switching beans, so here's a recommended starting point: ______'."

## Clarifications

### Session 2024-12-24

- Q: When exactly does the AI recommendation trigger? → A: When user selects a new bean from the picker on the shot logging page.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - New Bean AI Recommendations (Priority: P1)

As a barista starting a new bag of beans that I've never logged shots for, I want the app to automatically recommend starting parameters (dose, grind, output, duration) so that I have a reasonable starting point without guessing.

**Why this priority**: This addresses the cold-start problem for new beans. Without historical data, users have no guidance and must experiment blindly. AI-generated recommendations based on bean characteristics (origin, roast level, processing method) provide immediate value.

**Independent Test**: Can be fully tested by creating a new bag for a bean with zero shot history and verifying AI recommendations appear with appropriate toast message. Delivers immediate value by eliminating guesswork.

**Acceptance Scenarios**:

1. **Given** a user is on the shot logging page and selects a bean with no prior shot history from the picker, **When** the selection is made, **Then** the animated loading bar appears while AI generates recommendations.
2. **Given** AI has generated recommendations for a new bean, **When** loading completes, **Then** the dose, grind, output, and duration fields are populated with the recommended values.
3. **Given** AI recommendations have been applied for a new bean, **When** the values are set, **Then** a toast notification appears with message: "We didn't have any shots for this bean, so we've created a recommended starting point: [dose]g dose, [grind] grind, [output]g output, [duration]s."
4. **Given** a user is viewing AI-recommended values for a new bean, **When** they want to adjust values, **Then** they can modify any field before logging their shot.

---

### User Story 2 - Bean Switching AI Recommendations (Priority: P1)

As a barista switching to a different bag of beans (one I have history with but isn't my most recent), I want the app to recommend a starting profile based on my previous shots with that bean, so I can pick up where I left off.

**Why this priority**: Equal priority to US1 as it addresses the common scenario of returning to a previously-used bean. The AI can leverage historical shot data to suggest optimal parameters based on the user's past results.

**Independent Test**: Can be fully tested by switching from a current bean to a different bean that has shot history. Verifying AI recommendations appear based on historical data with appropriate toast message.

**Acceptance Scenarios**:

1. **Given** a user has shot history with Bean A, is currently using Bean B, **When** they switch back to Bean A, **Then** the animated loading bar appears while AI analyzes historical shot data.
2. **Given** AI has analyzed historical shots for a returning bean, **When** loading completes, **Then** the dose, grind, output, and duration fields are populated with AI-recommended values based on the user's best-performing past shots.
3. **Given** AI recommendations have been applied when switching beans, **When** the values are set, **Then** a toast notification appears with message: "I see you're switching beans, so here's a recommended starting point: [dose]g dose, [grind] grind, [output]g output, [duration]s."
4. **Given** a user is viewing history-based AI recommendations, **When** they want to adjust values, **Then** they can modify any field before logging their shot.

---

### User Story 3 - Loading State Consistency (Priority: P2)

As a user, I want the AI recommendation loading experience to be consistent with existing AI features, so the app feels cohesive and I understand what's happening.

**Why this priority**: UX consistency builds user trust and reduces confusion. Reusing the established animated loading bar pattern ensures users recognize the AI is working.

**Independent Test**: Can be tested by triggering AI recommendations and verifying the same animated loading bar appears as when requesting AI advice on an existing shot.

**Acceptance Scenarios**:

1. **Given** AI recommendations are being generated, **When** the process starts, **Then** the same animated loading bar used for existing AI advice appears at the top of the view.
2. **Given** the loading bar is visible, **When** AI processing completes, **Then** the loading bar disappears and values are updated simultaneously.
3. **Given** AI recommendation loading has started, **When** the user interacts with other parts of the page, **Then** the loading bar remains visible until processing completes.

---

### Edge Cases

- What happens when AI service is unavailable or times out? The app displays an error toast and allows manual entry without pre-populated values.
- What happens when AI returns partial recommendations (some values but not all)? The app populates available values and leaves others at defaults, with toast indicating partial recommendations.
- What happens when the bean has very few historical shots (e.g., 1-2 shots)? The AI still attempts recommendations but may indicate lower confidence in the toast message.
- What happens when the user rapidly switches between beans? Each switch cancels any pending AI request and starts a new one for the current selection.
- What happens when the bean information is incomplete (no roast level, origin, etc.)? The AI uses industry-standard defaults for espresso extraction and indicates this in the recommendation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST detect when a user selects a bean from the picker on the shot logging page that has no prior shot history and trigger AI recommendations automatically.
- **FR-002**: System MUST detect when a user selects a bean from the picker on the shot logging page that is not their most recently used bean and trigger AI recommendations automatically.
- **FR-003**: System MUST display the animated loading bar during AI recommendation generation, consistent with existing AI advice feature.
- **FR-004**: System MUST populate dose, grind setting, output weight, and extraction duration fields with AI-recommended values.
- **FR-005**: System MUST display a toast notification with context-appropriate message and the recommended values.
- **FR-006**: System MUST allow users to modify AI-recommended values before saving their shot.
- **FR-007**: System MUST handle AI service failures gracefully by showing an error toast and allowing manual entry.
- **FR-008**: System MUST cancel pending AI requests when the user switches to a different bean before completion.
- **FR-009**: For beans with history, System MUST analyze the user's previous shots to inform recommendations (considering ratings, consistency, extraction patterns).
- **FR-010**: For beans without history, System MUST generate recommendations based on bean characteristics (roast level, origin, processing method) using industry-standard extraction principles.

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: AI recommendation requests MUST complete within 10 seconds (matching existing AI advice timeout).
- **NFR-P2**: Loading bar animation MUST start within 100ms of triggering the AI request.
- **NFR-P3**: Toast notification MUST appear within 200ms of receiving AI response.

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: Toast notifications MUST be announced by screen readers.
- **NFR-A2**: Loading state MUST be announced to assistive technologies.
- **NFR-A3**: Recommended values MUST be navigable and adjustable via keyboard/assistive controls.

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: Loading bar MUST use the same visual design as existing AI advice loading bar.
- **NFR-UX2**: Toast notifications MUST use the existing FeedbackService info/success toast pattern.
- **NFR-UX3**: Error states MUST provide clear recovery guidance (e.g., "Couldn't get AI recommendations. Enter values manually or try again.").

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: AI recommendation logic MUST be covered by unit tests (minimum 80%).
- **NFR-Q2**: Integration tests MUST verify toast message content matches expected format.

### Key Entities

- **Bean**: Coffee bean record containing origin, roast level, processing method, and other characteristics used for generating recommendations when no history exists.
- **Shot Record**: Historical shot data including dose, grind, output, duration, and rating; used to inform history-based recommendations.
- **AI Recommendation**: Temporary recommendation object containing suggested dose (grams), grind setting, output (grams), and duration (seconds) along with recommendation type (new bean vs. returning bean).

## Assumptions

- The existing AI advice service (AIAdviceService) can be extended to support new recommendation scenarios.
- Bean entities contain sufficient metadata (roast level, origin, processing) for new-bean recommendations; if not, sensible defaults will be used.
- The existing animated loading bar component can be reused without modification.
- Toast messages using FeedbackService.ShowInfoAsync() will suffice for both recommendation scenarios.
- "Most recent bean" is determined by the most recently logged shot, not bag creation date.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users logging their first shot with a new bean receive AI recommendations within 10 seconds, 95% of the time.
- **SC-002**: Users switching beans receive history-based AI recommendations within 10 seconds, 95% of the time.
- **SC-003**: Toast notifications display complete recommended values (all four parameters: dose, grind, output, duration) 100% of successful recommendation requests.
- **SC-004**: Users can begin logging shots within 15 seconds of selecting a new or returning bean (including AI recommendation time).
- **SC-005**: AI recommendation feature maintains 95% availability during normal operation.
