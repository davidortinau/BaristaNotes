# Feature Specification: AI Shot Improvement Advice

**Feature Branch**: `001-ai-shot-advice`  
**Created**: 2025-12-09  
**Status**: Draft  
**Input**: User description: "I want to be able to get advice from AI on how I might adjust to improve my shots."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Request Deep Advice from Shot Detail (Priority: P1)

As a home barista viewing a logged shot in my shot history, I want to explicitly request detailed AI advice on how to adjust my next shot, so I can get specific, actionable guidance when I need help dialing in.

**Why this priority**: This is the core value proposition - users explicitly seeking help when they're struggling. The shot detail page is the natural place to reflect and ask for guidance.

**Independent Test**: Can be fully tested by viewing any logged shot, tapping "Get Advice", and receiving detailed adjustment suggestions. Delivers immediate value to struggling baristas.

**Acceptance Scenarios**:

1. **Given** I am viewing a shot in the shot log detail page, **When** I tap "Get Advice", **Then** I see detailed AI-generated suggestions for adjustments (dose, grind, yield, time)
2. **Given** I am viewing AI advice, **When** I read the suggestions, **Then** the advice references my specific shot parameters, my rating history, and explains the reasoning
3. **Given** AI advice is loading, **When** I wait for the response, **Then** I see a loading indicator and the request completes within a reasonable time
4. **Given** I have added optional tasting notes to a shot, **When** I request advice, **Then** the AI incorporates those notes into its recommendations

---

### User Story 2 - Passive AI Assessment After Logging (Priority: P2)

As a home barista who just logged a shot, I want the system to passively analyze my shot parameters against my history and surface a brief insight if something looks off, so I can be aware of potential issues without extra effort.

**Why this priority**: Provides value automatically without requiring user action. Historical context and shot facts (without requiring rating) enable proactive guidance.

**Independent Test**: Can be tested by logging a shot with parameters that deviate from successful historical shots and seeing a brief AI insight appear.

**Acceptance Scenarios**:

1. **Given** I have logged a new shot with parameters that differ significantly from my best-rated shots, **When** the shot is saved, **Then** I see a brief AI insight (e.g., "This shot ran 8 seconds faster than your best shots with this bean")
2. **Given** I have logged a shot with typical parameters, **When** the shot is saved, **Then** no insight is shown (non-intrusive)
3. **Given** the passive insight appears, **When** I tap on it, **Then** I am taken to the shot detail where I can request full advice

---

### User Story 3 - AI Advice Informed by Rating History (Priority: P2)

As a home barista with a history of rated shots, I want AI advice to learn from my ratings to understand what parameters correlate with good vs. poor shots for me personally.

**Why this priority**: Ratings are the key signal for what works - leveraging this data makes advice personalized and more accurate.

**Independent Test**: Can be tested by having shots with varying ratings, requesting advice, and verifying the AI references rating patterns.

**Acceptance Scenarios**:

1. **Given** I have 5+ rated shots for a bag with a mix of high and low ratings, **When** I request advice, **Then** the AI identifies what parameters correlate with my higher-rated shots
2. **Given** my highest-rated shots share common parameters (e.g., 18g in, 36g out, 28s), **When** I request advice for a lower-rated shot, **Then** the AI suggests adjusting toward those successful parameters

---

### User Story 4 - Optional Tasting Notes Field (Priority: P3)

As a home barista, I want to optionally add tasting notes (free text) when logging a shot, so I can provide additional context that AI can use if I choose to fill it out.

**Why this priority**: Enhances advice quality but should not be required. Some users will want to capture flavor notes; others won't.

**Independent Test**: Can be tested by logging a shot with tasting notes, then requesting advice and seeing the notes reflected in recommendations.

**Acceptance Scenarios**:

1. **Given** I am logging a new shot, **When** I view the form, **Then** I see an optional "Tasting Notes" text field
2. **Given** I leave tasting notes empty, **When** I save the shot, **Then** the shot saves successfully without notes
3. **Given** I enter "sour, thin body" in tasting notes, **When** I later request AI advice, **Then** the AI references these notes in its suggestions

---

### Edge Cases

- What happens when the user has no shot history at all? → Passive assessment disabled; explicit advice gives general espresso guidance
- How does the system handle API failures or network errors? → Show friendly error message with retry option
- What if the user's equipment isn't logged? → Provide advice based on available data, noting equipment details could help refine suggestions
- What if a shot has no rating? → AI can still provide advice based on parameters and history, just without rating context for that specific shot

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a "Get Advice" action on the shot log detail page for explicit deep advice requests
- **FR-002**: System MUST send relevant shot parameters (dose, grind, time, yield) to the AI service
- **FR-003**: System MUST include the shot's rating and historical rating data when analyzing shots
- **FR-004**: System MUST display AI-generated advice in a readable, actionable format
- **FR-005**: Users MUST be able to request detailed advice for any logged shot from the shot detail page
- **FR-006**: System MUST include historical shots for the same bag when generating advice
- **FR-007**: System MUST display loading state while AI request is in progress
- **FR-008**: System MUST handle errors gracefully with user-friendly messages
- **FR-009**: System MUST provide advice that is specific to espresso extraction principles
- **FR-010**: System SHOULD provide passive AI insights after shot logging when parameters deviate significantly from successful history
- **FR-011**: System SHOULD allow an optional "Tasting Notes" free-text field when logging shots (not required)
- **FR-012**: System MUST incorporate tasting notes into AI analysis when provided

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: AI advice request MUST complete within 10 seconds (accounting for external API latency)
- **NFR-P2**: Loading indicator MUST appear within 100ms of request initiation
- **NFR-P3**: UI MUST remain responsive during AI request (non-blocking)

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: All advice text MUST be selectable/readable by screen readers
- **NFR-A2**: Action buttons MUST have minimum 44x44px touch targets
- **NFR-A3**: Advice content MUST have sufficient color contrast

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: Advice display MUST follow existing app styling conventions
- **NFR-UX2**: Error messages MUST be user-friendly with clear recovery steps
- **NFR-UX3**: Loading states MUST be shown during AI processing
- **NFR-UX4**: Feature MUST work on mobile, tablet, and desktop

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: Code coverage MUST meet 80% minimum
- **NFR-Q2**: Static analysis MUST pass without warnings
- **NFR-Q3**: All code MUST pass peer review

### Key Entities *(include if feature involves data)*

- **ShotRecord**: Existing entity containing shot parameters (dose, grind, time, yield, rating) - adding optional TastingNotes field
- **Bag/Bean**: Provides context about the coffee being used (roast date, origin, roaster)
- **Equipment**: Optional context about machine and grinder used
- **Rating History**: Collection of rated shots for the same bag used to identify successful parameter patterns
- **AIAdviceRequest**: The prompt/context sent to AI service (shot facts + history + optional notes)
- **AIAdviceResponse**: The structured advice returned from AI service

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can receive actionable AI advice within 10 seconds of requesting it from shot detail page (matches NFR-P1 timeout)
- **SC-002**: 80% of AI advice responses include specific, numbered adjustment suggestions
- **SC-003**: Users who receive advice and log a follow-up shot show improved ratings 60% of the time
- **SC-004**: Feature maintains less than 2% error rate for AI advice requests
- **SC-005**: Users can understand and act on advice without additional explanation (validated via user testing)
- **SC-006**: AI advice references user's rating history patterns when 5+ rated shots exist for a bag
