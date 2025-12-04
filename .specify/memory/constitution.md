<!--
SYNC IMPACT REPORT
==================
Version Change: INITIAL → 1.0.0
Constitution Type: Initial Ratification

Core Principles Added:
  1. Code Quality Standards
  2. Test-First Development (NON-NEGOTIABLE)
  3. User Experience Consistency
  4. Performance Requirements

Sections Added:
  - Performance Standards (specific metrics and requirements)
  - Quality Gates (enforcement mechanisms)
  - Governance (amendment and compliance procedures)

Templates Status:
  ✅ plan-template.md - Constitution Check section will reference all 4 principles
  ✅ spec-template.md - Requirements sections align with UX consistency & quality standards
  ✅ tasks-template.md - Test-first emphasis maintained, quality gates noted
  
Follow-up Actions: None - all principles fully defined

Rationale for Version 1.0.0:
  - Initial constitution ratification for BaristaNotes project
  - Establishes foundational governance framework
  - All principles defined and ready for enforcement
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

- **EF Core Migrations Only**: ALL database schema changes MUST be made using `dotnet ef migrations add` command. Manual SQL scripts or direct database modifications are PROHIBITED.
- **Never Manually Modify Schema**: Do not add columns, tables, indexes, or constraints directly to the database or via manual SQL. This creates inconsistent state between the EF model and actual database.
- **Migration Workflow**:
  1. Update entity models in `*.Core/Models/`
  2. Update `DbContext.OnModelCreating()` if needed
  3. Run `dotnet ef migrations add MigrationName` from the Core project directory
  4. Review the generated migration to ensure it only contains intended changes
  5. Test migration with `dotnet ef database update` in development
  6. Application startup calls `await db.Database.MigrateAsync()` to apply migrations automatically
- **Migration Rollback**: Always test Down() migration in development to ensure it can be safely rolled back.
- **Never Skip Migrations**: If database and model are out of sync, fix by creating proper migration, never by manually altering database.

**Rationale**: Manual database changes bypass EF Core's change tracking, creating inconsistent state that causes runtime errors ("table already exists", "column not found"). EF migrations provide versioned, testable, rollback-able schema changes with full audit trail.

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

**Version**: 1.0.0 | **Ratified**: 2025-12-02 | **Last Amended**: 2025-12-02
