# Training Log: maui-devflow-debug

## Session: 2026-08-27 - Behavioral UI verification

**Trainer:** SkillTrainer | **Skill:** maui-devflow-debug | **Trigger:** Photo workflow regressions were missed when verification stopped at a visible state.

### Assessment

**Issues found (ranked):**
1. ❌ The core loop did not require agents to exercise every changed state and transition.
2. ⚠️ A screenshot could be treated as proof that a processing overlay blocked input.
3. ⚠️ Target selection could start a second simulator instead of using the user-named or connected target.

### Cycle 1: Require behavioral transition verification

**Hypothesis:** Adding a state inventory and interaction-based checks will prevent visual-only verification because agents will have explicit success criteria for each transition.

**Edit:** `.github/skills/maui-devflow-debug/SKILL.md` - expanded target selection, limited platform tools to target management, and added checks for every changed transition, safe areas, overlay input blocking, and resumed interaction.

| Model | Before | After | Delta Tool Calls |
|-------|--------|-------|------------------|
| Claude Sonnet 5 | 4/5, inferred behavioral checks | 5/5, cited the required blocked-input check | 0 |
| GPT-5.4 | 4/5, supplied checks not present in the skill | 5/5, followed the ordered verification sequence | 0 |
| Gemini 3.1 Pro | 4/5, identified the missing rule | 5/5, treated every transition as mandatory | 0 |

**Outcome:** ✅ | **Decision:** kept

### Patterns Learned

- A screenshot proves appearance, not input behavior. Blocking UI needs a negative interaction test.
- Device selection, data state, and UI verification must be one ordered workflow.

### Open Items

- Release and NativeAOT physical-device publishing remains outside this debug skill.
