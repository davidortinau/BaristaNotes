# Feature Specification: Espresso Shot Tracking & Management

**Feature Branch**: `001-espresso-tracking`  
**Created**: 2025-12-02  
**Status**: Draft  
**Input**: User description: "Build an application to help me track and monitor my espresso making. I will need to track my equipment (machine, grinder, tamper, puck screens, etc.) as using them in combination will impact my results. Also I want to track my beans so I can reference a history of recipes. On a daily basis I will open the app and create a new shot based on the previous shot recipe: bean, grind setting, grams in, and expected shot time and grams out. After taking the shot I will either just confirm my results matched, or I will update the time and output. I then want to complete the shot note with a taste rating (5 point scale from dislike to like) and save it. I should probably also note what drink I'm making: espresso shot, americano, latte, etc. The previous choice should be remembered so I don't have to keep changing it. I want to also have user profiles so I can track who the shot was made for and who it was made by. In an activity feed I want to see shot notes in a timeline with this info."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Quick Daily Shot Logging (Priority: P1)

As a home barista, I open the app each morning and quickly log my espresso shot by copying yesterday's recipe, adjusting parameters as needed, recording actual results, rating the taste, and saving the shot note. This helps me iterate and improve my technique day by day.

**Why this priority**: This is the core daily workflow that delivers immediate value. Users need to quickly log shots without friction to maintain consistent tracking habits. Without this, the app provides no value.

**Independent Test**: Can be fully tested by creating a shot record with preset recipe values, recording actual results (time, output), adding a taste rating, and verifying the shot is saved and appears in the activity feed. Delivers immediate value of tracking daily shots.

**Acceptance Scenarios**:

1. **Given** I open the app for the first time today, **When** I navigate to create a new shot, **Then** the form is pre-populated with my most recent shot's recipe (bean, grind setting, dose in, expected time, expected output, drink type)
2. **Given** I have a pre-populated shot form, **When** I pull the shot and record the actual time and output weight, **Then** I can quickly update only those fields without re-entering other recipe details
3. **Given** I have recorded shot results, **When** I add a taste rating on a 5-point scale (1=dislike to 5=like), **Then** the rating is saved with the shot note
4. **Given** I have completed all shot details, **When** I save the shot note, **Then** it appears immediately in my activity feed with timestamp, recipe details, results, and rating
5. **Given** I made a latte yesterday, **When** I create a new shot today, **Then** the drink type defaults to "latte" (my previous selection)

---

### User Story 2 - Equipment & Bean Management (Priority: P2)

As a home barista, I need to manage my equipment inventory (espresso machines, grinders, tampers, puck screens) and bean collection so I can select them when creating shot recipes and understand how different combinations affect my results.

**Why this priority**: Equipment and beans are foundational data that shots depend on. Users need to establish their inventory before shot tracking becomes truly useful for comparison and optimization. This enables the "equipment combination impact" goal stated in the requirements.

**Independent Test**: Can be tested by adding equipment items (machine, grinder, accessories), adding bean records (origin, roast date, roaster), selecting equipment/bean combinations when creating a shot, and viewing historical shots filtered by equipment or bean to see patterns.

**Acceptance Scenarios**:

1. **Given** I navigate to equipment management, **When** I add a new piece of equipment with name, type (machine/grinder/tamper/puck screen/other), and optional notes, **Then** it is saved and available for selection in shot recipes
2. **Given** I navigate to bean management, **When** I add a new bean record with name, roaster, roast date, origin, and optional notes, **Then** it is saved and available for selection in shot recipes
3. **Given** I am creating a shot recipe, **When** I select my equipment (machine, grinder, and optional accessories) and bean, **Then** these selections are saved with the shot note
4. **Given** I have multiple shots logged with different equipment/bean combinations, **When** I view my shot history, **Then** I can see which equipment and beans were used for each shot
5. **Given** I have saved an equipment combination with a shot, **When** I create a new shot, **Then** the previous equipment combination is pre-selected

---

### User Story 3 - User Profiles & Activity Feed (Priority: P3)

As a household with multiple coffee drinkers, I want to track who made each shot and who it was made for, and view all shots in a chronological activity feed showing complete shot information including profiles.

**Why this priority**: This adds valuable context for multi-user households and enables social/sharing aspects of the app. However, single users can still get full value from P1 and P2. This enhances rather than enables core functionality.

**Independent Test**: Can be tested by creating user profiles, selecting "made by" and "made for" profiles when logging shots, and viewing the activity feed filtered by user to see personalized shot histories.

**Acceptance Scenarios**:

1. **Given** I navigate to profile management, **When** I create user profiles with names and optional avatars, **Then** they are available for selection when logging shots
2. **Given** I am logging a shot, **When** I select who made the shot (barista) and who it was made for (consumer), **Then** both profiles are saved with the shot note
3. **Given** I have multiple shots logged with different user profiles, **When** I view the activity feed, **Then** shots are displayed in reverse chronological order showing: timestamp, barista, consumer, drink type, recipe details, results, and rating
4. **Given** I am viewing the activity feed, **When** I filter by a specific user (as barista or consumer), **Then** only shots involving that user are displayed
5. **Given** I made yesterday's shot as "David" for "Sarah", **When** I create today's shot, **Then** the barista and consumer default to the previous selections

---

### Edge Cases

- What happens when a user tries to create a shot without any beans or equipment defined? System should prompt to add equipment/beans first or allow proceeding with "unspecified" placeholders
- What happens when a user accidentally saves a shot with incorrect data? System should allow editing or deleting recent shot notes
- What happens when a bean runs out or equipment breaks? Users should be able to mark items as inactive/archived while preserving historical shot data
- What happens if actual shot results significantly deviate from expected (e.g., 18g in, 60g out in 15s - likely a channeling issue)? System should allow recording any values without validation errors, as outliers are learning opportunities
- What happens when a user forgets to record actual results immediately after pulling the shot? System should allow saving shots with only expected values and editing later to add actuals

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow users to create and manage equipment records with name, type (machine/grinder/tamper/puck screen/other), and optional notes
- **FR-002**: System MUST allow users to create and manage bean records with name, roaster, roast date, origin, and optional notes
- **FR-003**: System MUST allow users to create and manage user profiles with name and optional avatar
- **FR-004**: System MUST allow users to create shot records including: selected equipment (machine, grinder, optional accessories), selected bean, dose in (grams), grind setting, expected shot time (seconds), expected output (grams), drink type, barista profile, and consumer profile
- **FR-005**: System MUST allow users to record actual shot results: actual shot time (seconds) and actual output weight (grams)
- **FR-006**: System MUST allow users to rate shots on a 5-point scale (1=dislike to 5=like)
- **FR-007**: System MUST persist the user's most recent selections for: drink type, equipment combination, bean, barista profile, and consumer profile
- **FR-008**: System MUST pre-populate the shot creation form with the most recent shot's recipe values (bean, grind setting, dose in, expected time, expected output, drink type, equipment, profiles)
- **FR-009**: System MUST display all shot notes in an activity feed in reverse chronological order
- **FR-010**: System MUST display complete shot information in the activity feed: timestamp, barista, consumer, drink type, equipment used, bean used, recipe parameters, actual results, and taste rating
- **FR-011**: System MUST allow users to filter the activity feed by user profile (as barista or consumer)
- **FR-012**: System MUST allow users to edit or delete shot records
- **FR-013**: System MUST allow users to mark equipment or beans as inactive/archived without deleting historical shot data
- **FR-014**: System MUST support common drink types: espresso shot, americano, latte, cappuccino, cortado, macchiato, flat white, and allow custom drink type entry
- **FR-015**: System MUST persist all shot data locally on the user's device for offline access

### Non-Functional Requirements (Constitution-Mandated)

**Performance** *(Per Principle IV: Performance Requirements)*:
- **NFR-P1**: App launch time MUST be <2 seconds (p95)
- **NFR-P2**: Shot creation form pre-population MUST complete in <500ms (p95)
- **NFR-P3**: User interactions (button taps, field updates) MUST provide feedback within 100ms
- **NFR-P4**: Activity feed MUST load initial 50 records in <1 second
- **NFR-P5**: Scrolling through activity feed MUST maintain 60fps with no janky frames

**Accessibility** *(Per Principle III: User Experience Consistency)*:
- **NFR-A1**: All interactive elements MUST be keyboard navigable (web) or support screen reader navigation (mobile)
- **NFR-A2**: WCAG 2.1 Level AA compliance MUST be verified for all user interfaces
- **NFR-A3**: Touch targets MUST be minimum 44x44px on mobile platforms
- **NFR-A4**: Screen readers MUST announce all state changes (shot saved, validation errors, filter applied)

**UX Consistency** *(Per Principle III: User Experience Consistency)*:
- **NFR-UX1**: Design system components MUST be used throughout the app (buttons, forms, lists, navigation patterns)
- **NFR-UX2**: Error messages MUST be user-friendly with recovery steps (e.g., "Please add at least one bean before creating a shot. Tap here to add beans.")
- **NFR-UX3**: Loading states MUST be shown for all async operations (saving shot, loading feed)
- **NFR-UX4**: Responsive design MUST support mobile phones (primary platform) and tablets

**Code Quality** *(Per Principle I: Code Quality Standards)*:
- **NFR-Q1**: Code coverage MUST meet 80% minimum (100% for data persistence and shot calculation logic)
- **NFR-Q2**: Static analysis MUST pass without warnings
- **NFR-Q3**: All code MUST pass peer review

### Key Entities

- **Equipment**: Represents espresso-making tools (machines, grinders, accessories). Attributes: name, type (enum: machine/grinder/tamper/puck screen/other), notes, active status, creation date
- **Bean**: Represents coffee beans used for shots. Attributes: name, roaster, roast date, origin, notes, active status, creation date
- **UserProfile**: Represents people in the household. Attributes: name, avatar (optional), creation date
- **ShotRecord**: Represents a logged espresso shot. Attributes: timestamp, equipment references (machine, grinder, accessories array), bean reference, barista profile reference, consumer profile reference, drink type, recipe (dose in, grind setting, expected time, expected output), actuals (actual time, actual output), taste rating (1-5 scale)
- **ShotRecipe**: Embedded within ShotRecord. Attributes: dose in (grams), grind setting (string/number), expected shot time (seconds), expected output (grams)
- **ShotActuals**: Embedded within ShotRecord. Attributes: actual shot time (seconds), actual output weight (grams)

**Relationships**:
- ShotRecord → Equipment (many-to-one for machine and grinder, many-to-many for accessories)
- ShotRecord → Bean (many-to-one)
- ShotRecord → UserProfile (many-to-one for barista, many-to-one for consumer)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can create a new shot record based on their previous recipe in under 30 seconds
- **SC-002**: Users can complete the full daily workflow (open app, adjust recipe, pull shot, record results, rate, save) in under 2 minutes
- **SC-003**: 90% of daily users successfully log at least one shot per day after first week of use (indicates habit formation)
- **SC-004**: Users can view their complete shot history for the past 30 days in under 3 seconds
- **SC-005**: Activity feed displays 100+ shot records without performance degradation (60fps scrolling maintained)
- **SC-006**: Users successfully identify patterns in their shots (e.g., "I get better taste ratings with bean X on grinder Y at setting Z") within 2 weeks of regular use
- **SC-007**: Multi-user households can distinguish between different users' shots 100% of the time in the activity feed
- **SC-008**: App functions completely offline with no data loss (all features available without internet connection)

## Assumptions

- **Platform**: Application will be built for mobile platforms (iOS/Android) as primary interface, with mobile-first design principles
- **Data Storage**: All data stored locally on device; cloud sync is out of scope for initial release
- **Authentication**: No login required for single-device use; data is device-specific
- **Grind Setting Format**: Grind settings can be entered as text or numbers to support various grinder types (stepped/stepless, numeric/named settings)
- **Measurement Units**: Weight measurements in grams, time in seconds (standard espresso units)
- **Equipment Complexity**: Each shot uses exactly one machine and one grinder, plus optional accessories (puck screens, tampers, etc.)
- **Rating Scale**: 5-point scale is sufficient for subjective taste evaluation (more granularity not needed)
- **Activity Feed Scope**: Feed shows all shots without date-based filtering initially; filtering by user profile is sufficient for MVP
- **Photo Attachments**: Shot photos are out of scope for initial release (text-based notes only)
