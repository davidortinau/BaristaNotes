<!--
SYNC IMPACT REPORT
==================
Version Change: 1.1.0 → 1.2.0
Constitution Type: Minor Amendment

Core Principles Added:
  VI. Technology Stack Consistency - New principle establishing architectural constraint authority

Document Relationship Clarified:
  - Constitution (this file): Governance principles and "WHY"
  - ARCHITECTURE_CONSTRAINTS.md: Technical implementation rules and "HOW"
  - Cross-references established between documents
  
Sections Refactored:
  - EF Core implementation details moved to ARCHITECTURE_CONSTRAINTS.md
  - Data Preservation principles retained at governance level
  - Development Standards section streamlined (removed redundant EF migration details)

Templates Status:
  ✅ plan-template.md - Constitution Check references all 6 principles
  ✅ spec-template.md - Requirements sections align with all standards
  ✅ tasks-template.md - References both constitution and architecture constraints
  ⚠️  ARCHITECTURE_CONSTRAINTS.md - Updated with EF Core details from constitution
  
Follow-up Actions:
  - Update ARCHITECTURE_CONSTRAINTS.md with consolidated EF Core migration patterns
  - Verify plan-template.md includes architecture constraint compliance check

Rationale for Version 1.2.0 (MINOR bump):
  - Added new Principle VI (Technology Stack Consistency)
  - Clarified document separation of concerns (governance vs implementation)
  - Streamlined constitution by moving technical details to appropriate document
  - No breaking changes to existing principles
-->

# BaristaNotes Constitution

## Core Principles

### I. Code Quality Standards

All code MUST meet these non-negotiable quality requirements:

- **Readability First**: Code is written for humans first, machines second. Self-documenting code with clear variable/function names is mandatory.
- **Single Responsibility**: Every class, function, and module has exactly one reason to change. Complexity must be justified in writing.
- **DRY Principle**: Duplication of logic is prohibited beyond three instances. Extract shared code into reusable components.
- **Code Review Required**: All changes require peer review before merge. Reviewer must verify adherence to all constitution principles.
- **Static Analysis**: Linting and static analysis tools MUST pass without warnings. Suppressions require documented justification.

**Rationale**: High code quality reduces bugs, improves maintainability, and accelerates onboarding of new team members. Poor code quality creates technical debt that compounds exponentially.

### II. Test-First Development (NON-NEGOTIABLE)

Test-Driven Development is the mandatory development methodology:

- **Red-Green-Refactor Cycle**: Tests are written BEFORE implementation, tests must FAIL first, then implement to pass, then refactor.
- **Test Coverage Minimum**: 80% code coverage for business logic. 100% coverage for critical paths (data loss, security, payments).
- **Test Types Required**:
  - **Unit Tests**: All services, utilities, and business logic
  - **Integration Tests**: All API endpoints, data persistence, external service interactions
  - **Contract Tests**: All public APIs and interfaces between components
- **Test Quality**: Tests must be deterministic, fast (<5s per test), isolated, and self-contained. No shared state between tests.
- **Approval Gate**: Product owner/stakeholder MUST approve test scenarios before implementation begins.

**Rationale**: Test-first development catches defects early when they are cheapest to fix, provides living documentation, enables confident refactoring, and ensures requirements are testable.

### III. User Experience Consistency

User-facing functionality MUST provide a consistent, high-quality experience:

- **Design System Adherence**: All UI components MUST follow the established design system. Custom components require design review.
- **Icon Usage (NON-NEGOTIABLE)**: NEVER use emojis (☕, ⭐, etc.) in UI. Always use font icons (MaterialSymbolsFont, custom font icons) unless a specific PNG or SVG file is explicitly specified. Emojis render inconsistently across platforms and break accessibility.
- **Accessibility Standards**: WCAG 2.1 Level AA compliance is mandatory. All interactive elements must be keyboard navigable and screen-reader compatible.
- **Error Handling**: User-facing errors MUST be clear, actionable, and never expose technical details. Provide recovery steps when possible.
- **Responsive Design**: All interfaces MUST function on mobile, tablet, and desktop form factors. Touch targets minimum 44x44px.
- **Loading States**: All async operations MUST provide immediate feedback. No action should appear unresponsive.
- **Consistent Patterns**: Navigation, forms, validation, and feedback mechanisms use consistent patterns across the entire application.

**Rationale**: Inconsistent UX increases cognitive load, training costs, and user frustration. Consistency builds user trust and reduces support burden.

### IV. Performance Requirements

Application performance directly impacts user satisfaction and operational costs:

- **Response Time Targets**:
  - Page loads: <2 seconds (p95)
  - API responses: <500ms (p95)
  - User interactions: <100ms perceived response time
- **Resource Constraints**:
  - Mobile memory footprint: <200MB under normal operation
  - Battery impact: Background operations must be justifiable and efficient
- **Data Efficiency**:
  - Pagination required for lists >100 items
  - Image optimization mandatory (WebP/AVIF with fallbacks)
  - Lazy loading for off-screen content
- **Performance Monitoring**: All critical paths MUST have instrumentation. Performance regressions >20% block deployment.
- **Scalability**: Architecture must support 10x current user load without redesign.

**Rationale**: Performance is a feature. Slow applications drive user abandonment, increase infrastructure costs, and damage brand reputation.

### V. Rating Scale Standard

All rating systems MUST follow project-wide consistency standards:

- **0-4 Scale (NON-NEGOTIABLE)**: All ratings use 0-4 scale with 5 levels:
  - 0 = Terrible
  - 1 = Bad
  - 2 = Average
  - 3 = Good
  - 4 = Excellent
- **Coffee Cup Icon Standard**: Coffee cup icon (☕) represents rating levels throughout UI. Use font icons (MaterialSymbolsFont) for consistency.
- **Rating Display Order**: Display ratings in descending order (4 → 0) for rating distributions and summary views.
- **No Alternative Scales**: Never use 1-5 scale, star ratings, or other rating systems. Consistency across all features is mandatory.

**Rationale**: Inconsistent rating scales confuse users and make data comparison impossible. The 0-4 scale provides clear differentiation between levels and aligns with user mental models (zero = none/terrible, four = maximum/excellent).

### VI. Technology Stack Consistency

All technical implementation MUST adhere to the established architectural constraints:

- **Mandatory Compliance**: All code MUST follow the architectural decisions documented in `.specify/ARCHITECTURE_CONSTRAINTS.md`. These constraints are non-negotiable without explicit stakeholder approval.
- **Technology Stack Adherence**: Use only approved technologies and libraries:
  - **UI Framework**: MauiReactor (NOT standard MAUI XAML)
  - **Feedback/Popups**: UXDivers.Popups.Maui via IFeedbackService
  - **Navigation**: Shell-based with MauiReactor extensions
  - **Data Layer**: Entity Framework Core with migrations
  - **Testing**: xUnit with FluentAssertions
  - **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Pattern Consistency**: Follow established patterns for state management, async/await, and navigation as defined in architecture constraints.
- **No Pattern Deviation**: Do not introduce alternative libraries, frameworks, or patterns without documented justification and approval.
- **Implementation Examples Required**: Architecture constraints provide code examples showing correct and incorrect usage. Follow them precisely.

**Rationale**: Consistency in technology choices and implementation patterns reduces cognitive load, simplifies onboarding, enables code reuse, and prevents framework fragmentation. Mixed architectural approaches create maintenance nightmares and technical debt.

## Performance Standards

### Measurement & Monitoring

- **Continuous Monitoring**: Production performance metrics collected via APM (Application Performance Monitoring) tooling.
- **Synthetic Monitoring**: Automated performance tests run on every deployment to catch regressions.
- **Real User Monitoring**: Track actual user experience metrics (First Contentful Paint, Time to Interactive, Cumulative Layout Shift).
- **Performance Budgets**: Each page/feature has a defined performance budget. Exceeding budget requires architectural review.

### Optimization Requirements

- **Database Queries**: All queries MUST use appropriate indexes. N+1 queries are prohibited.
- **Caching Strategy**: Frequently accessed, infrequently changing data MUST be cached with appropriate invalidation.
- **Asset Optimization**: All static assets minified, compressed, and served via CDN.
- **Background Processing**: Long-running operations (>3s) MUST be async with progress indication.

### Database Schema Management (NON-NEGOTIABLE)

- **EF Core Migrations Only**: ALL database schema changes MUST be made through Entity Framework Core migrations. Manual schema modifications are PROHIBITED.
- **Data Preservation First**: Migrations MUST preserve existing user data. Use data-preserving SQL for complex transformations.
- **Migration Discipline**: See `.specify/ARCHITECTURE_CONSTRAINTS.md` for detailed EF Core migration workflow and implementation patterns.

**Rationale**: Database migrations provide versioned, testable, rollback-able schema changes with full audit trail. Manual changes bypass change tracking and create deployment risks.

### Data Preservation (NON-NEGOTIABLE)

- **NEVER DELETE THE DATABASE**: Under NO circumstances is it acceptable to delete the database, regardless of migration issues, schema conflicts, or development challenges.
- **User Data is Sacred**: All user data MUST be preserved during schema changes, feature development, debugging, or any other development activity.
- **Migration-Based Solutions Only**: Any database issues MUST be resolved through proper EF Core migrations that preserve existing data.
- **Data-Preserving Migration Strategy**:
  1. When renaming columns/tables: Use `RenameColumn()` and `RenameTable()` operations
  2. When restructuring data: Include SQL in migration to copy/transform existing data to new schema
  3. When splitting tables: Migrate data to new tables before removing old structure
  4. When changing types: Use SQL CAST/CONVERT to transform existing data
  5. Always test migrations with actual production-like data before deployment
- **Zero Data Loss**: Migrations must be designed to preserve 100% of existing data. Data loss is NEVER an acceptable solution.
- **Development Database = Production Database**: Treat your development database with the same respect as production. Deleting it to "fix" issues is prohibited.

**Rationale**: The database contains valuable user data that represents hours of user effort, trust, and commitment to the application. Losing this data destroys user trust, wastes user time, and makes continuous development impossible. EF Core migrations exist specifically to enable schema evolution without data loss. Any suggestion to delete the database demonstrates a fundamental misunderstanding of database migration principles and is completely unacceptable.

## Quality Gates

### Pre-Merge Requirements

All pull requests MUST satisfy these gates before merge approval:

1. **All Tests Pass**: Unit, integration, and contract tests pass with required coverage.
2. **Static Analysis Clean**: No linting errors, type errors, or security warnings.
3. **Performance Baseline**: Automated performance tests show no regression >20%.
4. **Code Review Approved**: At least one peer review with constitution compliance verification.
5. **Documentation Updated**: README, API docs, or user docs updated if user-facing changes.
6. **Accessibility Check**: Automated and manual accessibility verification for UI changes.

### Pre-Deployment Requirements

Before deploying to production:

1. **Integration Tests Pass**: Full integration test suite passes in staging environment.
2. **Performance Validation**: Synthetic tests confirm performance targets met.
3. **Security Scan**: Dependency vulnerabilities resolved or documented/accepted.
4. **Database Migrations**: All migrations tested with rollback procedure documented.
5. **Feature Flags**: New features deployed behind flags for gradual rollout when appropriate.

### Constitution Compliance Review

All features MUST pass constitution check in implementation plan (plan.md):

- [ ] **Code Quality**: Does the design enable readable, maintainable, single-responsibility code?
- [ ] **Test-First**: Are test scenarios defined, approved, and written before implementation?
- [ ] **UX Consistency**: Does the feature follow design system and accessibility standards?
- [ ] **Performance**: Have performance targets been defined and validated?
- [ ] **Rating Standards**: If applicable, does feature use 0-4 rating scale consistently?
- [ ] **Technology Stack**: Does implementation follow architectural constraints (MauiReactor, EF Core, etc.)?

## Governance

### Authority & Precedence

This Constitution supersedes all other development practices, conventions, or documentation. In case of conflict, Constitution principles take precedence.

### Amendment Process

1. **Proposal**: Any team member may propose an amendment with written rationale.
2. **Review**: Team reviews proposal for impact on existing features and development velocity.
3. **Approval**: Amendments require consensus (or majority vote if consensus fails).
4. **Migration Plan**: Breaking amendments require migration plan for existing code.
5. **Version Increment**: Version bumped per semantic versioning:
   - **MAJOR**: Principle removed or fundamentally redefined
   - **MINOR**: New principle added or existing principle materially expanded
   - **PATCH**: Clarifications, wording improvements, non-semantic refinements
6. **Template Updates**: All dependent templates (.specify/templates/*.md) updated for consistency.
7. **Communication**: Amendment communicated to all team members with effective date.

### Compliance & Enforcement

- All code reviews MUST verify constitutional compliance.
- Performance metrics monitored continuously with alerts for threshold violations.
- Quarterly constitution review to ensure principles remain relevant and achievable.
- Complexity violations require written justification in plan.md Complexity Tracking section.

### Exceptions

Exception to constitutional principles require:

1. **Written Justification**: Document why principle cannot be met.
2. **Simpler Alternatives**: Explain why simpler approaches were rejected.
3. **Technical Debt Tracking**: Create technical debt item with remediation plan.
4. **Stakeholder Approval**: Product owner or technical lead approval required.

## Development Standards & Enforcement Guidelines

### MauiReactor UI Standards

All MauiReactor UI code MUST follow these standards:

- **ThemeKey System (MANDATORY)**: Never use inline styling methods (`.FontSize()`, `.TextColor()`, `.BackgroundColor()`, etc.). All styling MUST use the ThemeKey system.
- **Theme File References**: Before creating new theme keys, reference existing theme files:
  - `ThemeKeys.cs` - Theme key constants
  - `AppColors.cs` - Color definitions  
  - `ApplicationTheme.cs` - Theme implementations
  - `AppFontSizes.cs` - Font size constants
  - `AppSpacing.cs` - Spacing constants
- **Correct Type Usage**: Use `FontAttributes` from `Microsoft.Maui.Controls`, NOT `Microsoft.Maui.Graphics.Text`. Verify namespace imports.
- **New Theme Keys**: When new styling is required, create theme keys in `ThemeKeys.cs` and define values in `ApplicationTheme.cs`.

**Rationale**: Inline styling creates unmaintainable code, makes theme switching impossible, and violates the single source of truth principle. The ThemeKey system ensures consistency and enables runtime theme changes.

### Build Verification Standard

**ALWAYS build the application before reporting completion**:

1. Run `dotnet build` after ALL code changes
2. Verify zero compilation errors before marking tasks complete
3. Never report "no errors" without actually building
4. Fix all compilation errors as part of the task (not deferred)

**Rationale**: Unbuildable code breaks the development pipeline and wastes team time. Build verification is a basic quality gate that must never be skipped.

### EF Core Migration Standards

Entity Framework Core migration discipline:

- **Migration File Preservation**: Never delete existing migration files. They are the historical record of schema evolution.
- **Restore Before Adding**: If migrations are accidentally deleted, restore from git history before creating new ones.
- **Data-Preserving SQL**: Include custom SQL in migrations for complex schema changes affecting existing data.
- **Production Testing**: Test migrations on production-like data before deployment.
- **Rollback Procedures**: Document rollback plans for complex migrations.

**Implementation Details**: See `.specify/ARCHITECTURE_CONSTRAINTS.md` for complete EF Core workflow, migration commands, and code examples.

**Rationale**: Migrations are deployment scripts, not just development artifacts. Deleting them breaks production deployment and loses schema history.

## Document Relationship

This Constitution establishes **governance principles and rationale** (the "WHY" and "WHAT").

For **technical implementation rules** (the "HOW"), see:
- `.specify/ARCHITECTURE_CONSTRAINTS.md` - Technology stack mandates, implementation patterns, code examples

Both documents are mandatory. Constitution violations require amendment process; Architecture Constraint violations require stakeholder approval.

**Version**: 1.2.0 | **Ratified**: 2025-12-02 | **Last Amended**: 2025-12-08
