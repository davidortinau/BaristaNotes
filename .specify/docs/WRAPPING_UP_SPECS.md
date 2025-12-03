# How to Wrap Up a Specification

This guide explains how to properly complete a specification and prepare for the next one.

---

## 📋 Completion Checklist

### 1. Verify Implementation ✅

```bash
# Run all tests
dotnet test

# Build the project
dotnet build

# Manual testing complete
```

### 2. Create COMPLETION_STATUS.md 📚

In your spec directory (`specs/XXX-feature-name/`), create:

**COMPLETION_STATUS.md**
- Status: ✅ COMPLETED
- Date completed
- Objectives achieved
- Files modified
- Issues resolved  
- Quality checklist
- Lessons learned

See `specs/001-edit-delete-shots/COMPLETION_STATUS.md` for example.

### 3. Final Commits 💾

```bash
git add specs/XXX-feature-name/COMPLETION_STATUS.md
git commit -m "docs: add completion status for spec XXX"

git add -A
git commit -m "feat: complete spec XXX - feature name

Status: ✅ PRODUCTION READY"

git push origin XXX-feature-name
```

### 4. Merge to Main 🔀

```bash
git checkout main
git pull origin main
git merge --no-ff XXX-feature-name
git push origin main
```

### 5. Archive ✅

Spec remains in `specs/XXX-feature-name/` as permanent archive.

---

## 🚀 Starting Next Spec

### 1. Create Directory

```bash
mkdir -p specs/002-new-feature
cd specs/002-new-feature
touch spec.md plan.md tasks.md
mkdir -p checklists contracts
```

### 2. Create Branch

```bash
git checkout main
git pull
git checkout -b 002-new-feature
```

### 3. Write Spec Files

- `spec.md` - What to build
- `plan.md` - How to build it
- `tasks.md` - Step-by-step breakdown

### 4. Implement

Use SpecKit commands to implement the feature.

---

## 🎯 Spec Lifecycle

```
Plan → Implement → Test → Document → Merge → Archive
```

**Spec directories are permanent archives** - never delete them!

---

## ✅ Quality Gates

Before completing:
- [ ] All objectives met
- [ ] Tests passing
- [ ] Manual testing done
- [ ] Documentation complete
- [ ] No regressions

---

## 💡 Tips

- Review previous specs for patterns
- Check ARCHITECTURE_CONSTRAINTS.md for project rules
- Capture lessons learned while fresh
- Document any new constraints discovered

---

**See**: `specs/001-edit-delete-shots/COMPLETION_STATUS.md` for full example
