# Feature Specification: Voice Commands

**Feature Branch**: `001-voice-commands`  
**Created**: 2026-01-09  
**Status**: Draft  
**Input**: User description: "I want to be able to speak to the app and have it do things, specifically log a new shot, add a new bean, add a bag of beans, rate a shot, and really any of the things the app can already do today via the UI."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Log Shot by Voice (Priority: P1)

As a barista making espresso, I want to speak my shot details while my hands are busy so I can record shots without touching the phone. I say something like "Log a shot with 18 grams in, 36 out, 28 seconds, 4 stars" and the app creates the shot record using my current/default bag and equipment.

**Why this priority**: Shot logging is the most frequent operation and the primary use case where hands-free operation adds the most value (messy hands, time-sensitive workflow).

**Independent Test**: Can be fully tested by speaking a complete shot command and verifying the shot appears in the activity feed with correct values.

**Acceptance Scenarios**:

1. **Given** the app is open and listening for voice input, **When** user says "Log a shot 18 grams in 36 out 28 seconds 4 stars", **Then** a new shot record is created with dose=18g, output=36g, time=28s, rating=4, using the most recent active bag and default equipment.

2. **Given** the app is listening, **When** user says "New shot 17 in 34 out 25 seconds pretty good", **Then** the system interprets "pretty good" as a rating (3-4 stars) and creates the shot record.

3. **Given** the app is listening, **When** user speaks a partial command like "Log shot 18 grams", **Then** the system prompts for missing required fields (output, time) via voice or shows a confirmation screen to complete.

4. **Given** a shot is logged by voice, **When** the shot is created, **Then** the user receives audio and/or visual confirmation that the shot was recorded successfully.

---

### User Story 2 - Add Bean/Bag by Voice (Priority: P2)

As a coffee enthusiast, I want to add new beans or bags to my collection by speaking so I can quickly catalog new purchases without typing. I say something like "Add a new bean called Ethiopia Yirgacheffe from Counter Culture" or "Add a bag of Ethiopia Yirgacheffe roasted today".

**Why this priority**: Adding beans/bags is the second most common operation and benefits from voice when users are unpacking new coffee.

**Independent Test**: Can be tested by speaking a command to add a bean, then verifying it appears in the beans list.

**Acceptance Scenarios**:

1. **Given** the app is listening, **When** user says "Add a new bean Ethiopia Yirgacheffe from Counter Culture", **Then** a new bean is created with name="Ethiopia Yirgacheffe" and roaster="Counter Culture".

2. **Given** a bean "Ethiopia Yirgacheffe" exists, **When** user says "Add a bag of Ethiopia Yirgacheffe roasted January 5th", **Then** a new bag is created linked to that bean with the specified roast date.

3. **Given** the app is listening, **When** user says "Add bag roasted yesterday", **Then** the system uses the most recently used bean and creates a bag with yesterday's date.

4. **Given** no matching bean exists, **When** user says "Add a bag of Colombian Supremo", **Then** the system offers to create a new bean first or asks for clarification.

---

### User Story 3 - Rate and Update Shots by Voice (Priority: P3)

As a user reviewing my espresso, I want to rate or update my most recent shot by voice so I can capture my tasting impressions hands-free. I say "Rate my last shot 5 stars" or "Add tasting notes chocolate and cherry to my last shot".

**Why this priority**: Rating often happens after tasting when hands may still be occupied, making voice valuable but less frequent than initial logging.

**Independent Test**: Can be tested by logging a shot, then speaking a rating command, and verifying the shot's rating is updated.

**Acceptance Scenarios**:

1. **Given** a shot was recently logged, **When** user says "Rate my last shot 5 stars", **Then** the most recent shot's rating is updated to 5.

2. **Given** a shot exists, **When** user says "Add tasting notes dark chocolate and citrus to my last shot", **Then** the tasting notes are appended to the shot record.

3. **Given** multiple shots today, **When** user says "Rate the morning shot 3 stars", **Then** the system attempts to identify the correct shot or asks for clarification.

---

### User Story 4 - Manage Equipment and Profiles by Voice (Priority: P4)

As a user, I want to add or update equipment and people profiles by voice for convenience. I say "Add a new grinder called Niche Zero" or "Add profile for Sarah".

**Why this priority**: Equipment and profile management is infrequent, so voice adds less value but completes the feature set.

**Independent Test**: Can be tested by speaking a command to add equipment, then verifying it appears in the equipment list.

**Acceptance Scenarios**:

1. **Given** the app is listening, **When** user says "Add a new grinder called Niche Zero", **Then** equipment is created with type=Grinder, name="Niche Zero".

2. **Given** the app is listening, **When** user says "Add profile for Sarah", **Then** a new user profile is created with name="Sarah".

---

### User Story 5 - Query and Navigate by Voice (Priority: P5)

As a user, I want to ask the app questions or navigate to specific screens by voice. I say "Show me my shots from last week" or "Go to beans" or "How many shots did I pull today?".

**Why this priority**: Navigation and queries are helpful but users can easily do this manually; voice is a convenience enhancement.

**Independent Test**: Can be tested by speaking a navigation command and verifying the correct screen is displayed.

**Acceptance Scenarios**:

1. **Given** the app is listening, **When** user says "Show my shots from last week", **Then** the activity feed is displayed filtered to the last 7 days.

2. **Given** the app is listening, **When** user says "Go to beans", **Then** the app navigates to the beans management page.

3. **Given** the app is listening, **When** user says "How many shots today?", **Then** the app responds with the count of shots logged today.

---

### Edge Cases

- What happens when the microphone permission is denied? → System shows a friendly message explaining why voice features need microphone access and how to enable it.
- What happens when speech is not recognized or is unclear? → System asks user to repeat or shows what it heard for correction.
- What happens when the app is in background? → Voice commands only work when app is in foreground (no background listening for privacy).
- What happens when there's no network and voice recognition requires internet? → System uses on-device recognition if available, or informs user that voice features require connectivity.
- What happens when ambiguous entity names are spoken (e.g., two beans with similar names)? → System presents options for user to choose from.
- What happens when required data is missing from a voice command? → System either uses smart defaults or prompts for the missing information.

## Requirements *(mandatory)*

### Functional Requirements

**Voice Activation & Recognition**
- **FR-001**: System MUST provide a voice input button accessible from any screen in the app
- **FR-002**: System MUST convert spoken words to text with support for coffee terminology (espresso, grind, dose, yield, etc.)
- **FR-003**: System MUST parse natural language commands to identify intent (log shot, add bean, rate, navigate, etc.)
- **FR-004**: System MUST handle variations in phrasing (e.g., "18 grams", "18g", "eighteen grams" all mean the same)

**Shot Logging by Voice**
- **FR-005**: Users MUST be able to log a complete shot by voice with dose, output, time, and rating
- **FR-006**: System MUST use smart defaults when values are omitted (current active bag, default equipment, current user profile)
- **FR-007**: System MUST support partial commands and prompt for missing required fields
- **FR-008**: System MUST confirm successful shot creation via audio feedback and/or visual confirmation

**Bean and Bag Management by Voice**
- **FR-009**: Users MUST be able to create new beans by voice with name and roaster
- **FR-010**: Users MUST be able to create new bags by voice with bean reference and roast date
- **FR-011**: System MUST interpret relative dates ("today", "yesterday", "last Tuesday") for roast dates
- **FR-012**: System MUST handle ambiguous bean references by asking for clarification

**Rating and Updates by Voice**
- **FR-013**: Users MUST be able to rate the most recent shot by voice
- **FR-014**: Users MUST be able to add or update tasting notes on shots by voice
- **FR-015**: System MUST support "last shot" and "most recent" as references to the latest shot record

**Equipment and Profile Management by Voice**
- **FR-016**: Users MUST be able to add equipment (machines, grinders) by voice with name and type
- **FR-017**: Users MUST be able to add user profiles by voice with name

**Navigation and Queries by Voice**
- **FR-018**: Users MUST be able to navigate to major screens by voice (beans, equipment, profiles, activity, settings)
- **FR-019**: Users MUST be able to filter activity feed by voice (by date range, bean, equipment, person)
- **FR-020**: Users MUST be able to ask simple queries ("How many shots today?", "What's my average rating this week?")

**Feedback and Error Handling**
- **FR-021**: System MUST provide audio confirmation for successful commands when voice mode is active
- **FR-022**: System MUST display what it heard/understood so users can verify or correct
- **FR-023**: System MUST gracefully handle unrecognized commands with helpful suggestions
- **FR-024**: System MUST request microphone permission before first use and explain why it's needed

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: Voice recognition MUST begin processing within 500ms of user stopping speech
- **NFR-P2**: Command interpretation and execution MUST complete within 2 seconds of recognition
- **NFR-P3**: Audio feedback MUST begin within 200ms of command completion
- **NFR-P4**: Voice input button MUST respond to tap within 100ms

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: Voice features MUST work alongside existing UI (not replace it)
- **NFR-A2**: Visual feedback MUST accompany all audio feedback for hearing-impaired users
- **NFR-A3**: Voice button MUST be minimum 44x44px and clearly labeled
- **NFR-A4**: Screen readers MUST announce voice mode state changes

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: Voice interface MUST use consistent terminology with the existing UI
- **NFR-UX2**: Error messages MUST clearly explain what went wrong and how to retry
- **NFR-UX3**: Visual listening indicator MUST be shown while processing voice input
- **NFR-UX4**: Users MUST be able to cancel voice input at any time

**Privacy & Security**:
- **NFR-S1**: Voice data MUST NOT be stored beyond the current session unless explicitly opted-in
- **NFR-S2**: Voice processing MUST only occur when explicitly activated by user (no always-on listening)
- **NFR-S3**: Microphone access MUST be clearly indicated while active

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: Code coverage MUST meet 80% minimum (100% for command parsing logic)
- **NFR-Q2**: Static analysis MUST pass without warnings
- **NFR-Q3**: All code MUST pass peer review

### Key Entities

- **VoiceCommand**: Represents a parsed voice input with intent (action type), entities (extracted values like dose, bean name), confidence level, and raw transcript
- **CommandIntent**: The identified action type (LogShot, AddBean, AddBag, RateShot, AddEquipment, AddProfile, Navigate, Query)
- **VoiceSession**: A voice interaction session tracking activation time, commands processed, and user feedback

### Assumptions

- Device has a working microphone
- User speaks English (initial release - other languages can be added later)
- Platform speech recognition services are available (iOS Speech, Android Speech)
- Users will primarily use voice for shot logging during brewing (the hands-free use case)
- Internet connectivity may be required for cloud-based speech recognition on some platforms

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can log a complete shot by voice in under 10 seconds (vs. 30+ seconds via UI)
- **SC-002**: Voice commands are correctly interpreted 90% of the time on first attempt in quiet environments
- **SC-003**: 80% of users who enable voice features use them at least once per week
- **SC-004**: Voice-logged shots require manual correction less than 20% of the time
- **SC-005**: Users can complete all core operations (log shot, add bean, add bag, rate shot) entirely by voice
- **SC-006**: Voice feedback confirms command completion within 3 seconds of speaking
- **SC-007**: Users report voice features save time during their brewing workflow (measured via in-app survey)
