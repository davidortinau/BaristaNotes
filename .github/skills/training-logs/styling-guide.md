# Training Log: styling-guide

## Session: 2026-08-27 - Fixed-theme popup chrome

**Trainer:** SkillTrainer | **Skill:** styling-guide | **Trigger:** A UXDivers popup used fixed dark chrome while theme-aware content changed to light-theme colors.

### Assessment

**Issues found (ranked):**
1. ❌ Theme-aware text could lose contrast on fixed-theme third-party chrome.
2. ❌ Early examples allowed token-based inline styles while a later rule prohibited all inline styles.
3. ⚠️ The guide did not require styling the popup host and inner content as separate surfaces.

### Cycle 1: Define third-party chrome styling

**Hypothesis:** A fixed-theme chrome rule and a narrow ThemeKey exception will remove model disagreement because the required palette follows the rendered surface, not the app theme.

**Edit:** `.github/skills/styling-guide/skill.md` - clarified the ThemeKey hierarchy and added fixed-theme host, content, contrast, and verification rules.

| Model | Before | After | Delta Tool Calls |
|-------|--------|-------|------------------|
| Claude Sonnet 5 | 4/5, chose dark tokens but found a rule conflict | 4/5, chose dark tokens under an explicit exception | 0 |
| GPT-5.4 | 2/5, chose light tokens for dark chrome | 5/5, chose fixed dark tokens and both surfaces | 0 |
| Gemini 3.1 Pro | 2/5, tried to make the fixed chrome theme-aware | 5/5, kept dark tokens in both app themes | 0 |

**Outcome:** ✅ | **Decision:** kept

### Patterns Learned

- Contrast tokens must follow the surface that is rendered, not the global app theme.
- Third-party host chrome and MauiReactor content need separate styling decisions.

### Open Items

- UXDivers-specific host property names remain implementation details that agents must discover from the installed package or existing code.
