# Research: AI Bean Recommendations

**Feature**: 001-ai-bean-recommendations  
**Date**: 2024-12-24

## Research Summary

No major unknowns identified. This feature extends existing, well-established patterns in the codebase.

## Decisions

### 1. AI Service Extension Pattern

**Decision**: Extend existing `IAIAdviceService` with new method `GetRecommendationsForBeanAsync(int beanId, bool hasHistory)`

**Rationale**: 
- Maintains single service for all AI interactions
- Reuses existing fallback logic (Apple Intelligence → OpenAI)
- Reuses existing timeout and error handling patterns
- No new abstractions or services required

**Alternatives considered**:
- Create separate `IAIRecommendationService` - Rejected: Duplicates fallback logic, increases maintenance burden
- Add to `IShotService` - Rejected: Violates single responsibility, AI logic belongs in AI service

### 2. Trigger Point for Recommendations

**Decision**: Trigger in `ShotLoggingPage` bag picker `OnSelectedIndexChanged` handler, after determining bean status (new vs. returning)

**Rationale**:
- User expectation is immediate feedback on bean selection (per clarification)
- Aligns with existing `LoadBestShotSettingsAsync` pattern that already triggers on bag selection
- Single entry point for recommendation logic

**Alternatives considered**:
- Trigger on page load - Rejected: Doesn't handle mid-session bean switching
- Add explicit button - Rejected: User wants automatic recommendations (per spec)

### 3. New Bean Detection Logic

**Decision**: Query `ShotRecord` table for any shots linked to bags with matching `BeanId`. Zero results = new bean.

**Rationale**:
- Simple, single database query
- Accurate: considers all historical bags of the same bean type
- No new indexes required (BeanId already indexed via Bag relationship)

**Alternatives considered**:
- Check only current bag - Rejected: Misses history from previous bags of same bean
- Add flag to Bean entity - Rejected: Redundant data, sync complexity

### 4. "Most Recent Bean" Detection

**Decision**: Query most recent `ShotRecord` by timestamp, get its `Bag.BeanId`. Compare to selected bean.

**Rationale**:
- Aligns with spec assumption: "Most recent bean is determined by most recently logged shot"
- Single query with ordering
- No state to maintain

**Alternatives considered**:
- Track in user preferences - Rejected: Adds state management, doesn't match natural user expectation

### 5. Recommendation DTO Structure

**Decision**: Create `AIRecommendationDto` with: `Dose (decimal)`, `GrindSetting (string)`, `Output (decimal)`, `Duration (decimal)`, `RecommendationType (enum: NewBean, ReturningBean)`, `Confidence (string?)`

**Rationale**:
- Matches the four parameters specified in requirements
- RecommendationType enables correct toast message selection
- Confidence enables edge case messaging (low history)

**Alternatives considered**:
- Reuse `AIAdviceResponseDto` - Rejected: Different purpose (advice text vs. parameters), would require breaking changes

### 6. Toast Message Format

**Decision**: Use `FeedbackService.ShowInfoAsync()` with formatted message:
- New bean: "We didn't have any shots for this bean, so we've created a recommended starting point: {dose}g dose, {grind} grind, {output}g output, {duration}s."
- Returning bean: "I see you're switching beans, so here's a recommended starting point: {dose}g dose, {grind} grind, {output}g output, {duration}s."

**Rationale**:
- Matches user's exact wording from spec
- Info toast (blue) appropriate for informational message
- Existing pattern handles accessibility, duration

**Alternatives considered**:
- Success toast - Rejected: Green implies user action succeeded; these are AI suggestions
- Custom toast duration - Rejected: Default 3s sufficient for reading message

### 7. AI Prompt Strategy

**Decision**: Two distinct prompts based on scenario:
1. **New bean prompt**: Include bean characteristics (origin, roast level, processing), request starting parameters based on espresso extraction principles
2. **Returning bean prompt**: Include historical shot data (ratings, parameters, patterns), request optimal parameters based on user's best results

**Rationale**:
- Different data available in each scenario
- AI can provide better recommendations with scenario-specific context
- Maintains existing `AIPromptBuilder` pattern

**Alternatives considered**:
- Single unified prompt - Rejected: New beans have no history, would generate hallucinated data

### 8. Cancellation Handling

**Decision**: Use `CancellationTokenSource` per recommendation request. Cancel previous on new bean selection.

**Rationale**:
- Matches existing AI advice cancellation pattern
- Prevents stale recommendations populating fields
- Graceful handling of rapid switching

**Alternatives considered**:
- Debounce bean selection - Rejected: Adds artificial delay, user expects immediate response

## Dependencies Verified

| Dependency | Current Version | Status |
|------------|-----------------|--------|
| Microsoft.Extensions.AI | Installed | Ready |
| Microsoft.Extensions.AI.OpenAI | Installed | Ready |
| UXDivers.Popups.Maui | Installed | Ready |
| Entity Framework Core | 8.0 | Ready |

## Integration Points

| Integration | Pattern | Failure Mode |
|-------------|---------|--------------|
| OpenAI API | Existing fallback (local → OpenAI) | Error toast + manual entry |
| Apple Intelligence | Existing fallback | Disabled for session, use OpenAI |
| SQLite Database | EF Core async queries | Graceful degradation |
| FeedbackService | ShowInfoAsync/ShowErrorAsync | N/A (local) |

## Outstanding Questions

None - all clarifications resolved in spec.
