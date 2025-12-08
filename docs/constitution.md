# BaristaNotes Development Constitution

## Non-Negotiable Principles

### I. Data Preservation is Sacred

**NEVER DELETE THE DATABASE**

- User data is valuable and must be preserved at all costs
- Database changes MUST use EF Core migrations
- Migrations MUST include data transformation logic where needed
- Test migrations with production-scale data before deployment
- Always provide rollback capability in Down() methods
- Database deletion is only acceptable for:
  - Empty databases with no user data
  - Explicit user request with confirmation
  - Test databases in automated testing environments

**Violation of this principle is a critical failure.**

### II. Test-First Development (TDD)

- Write tests BEFORE implementation
- Tests must FAIL before writing implementation code
- Minimum 80% code coverage
- 100% coverage for critical calculations (e.g., rating aggregations)
- All tests must pass before merge

### III. Quality Gates

All code changes must pass:
- ✅ All tests passing
- ✅ Static analysis clean (zero warnings)
- ✅ Code review approved
- ✅ Performance baselines met
- ✅ Documentation updated

### IV. Performance Standards

- Bean detail page load: <2s p95
- Rating calculations: <500ms p95  
- Shot logging workflow: <100ms perceived response time
- Rating distribution rendering: <1s

### V. Accessibility Requirements (WCAG 2.1 AA)

- Keyboard navigation support
- Screen reader compatibility
- Touch targets ≥44x44px
- Color contrast compliance

### VI. MauiReactor UI Standards

- **ALWAYS use ThemeKey** for styling (never inline styles)
- Reference ApplicationTheme.cs for theme definitions
- Use proper MauiReactor component patterns
- Follow established component hierarchy

## Migration Strategy

When modifying database schema:

1. **Generate migration**: `dotnet ef migrations add [Name]`
2. **Review generated code**: Check Up() and Down() methods
3. **Add data transformation**: Manually add SQL for data preservation
4. **Test migration**: Run on copy of production database
5. **Test rollback**: Verify Down() restores data correctly
6. **Document changes**: Update data-model.md

### Example: Bean to Bag Migration

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 1. Create new Bags table
    migrationBuilder.CreateTable(name: "Bags", ...);
    
    // 2. Migrate existing data
    migrationBuilder.Sql(@"
        INSERT INTO Bags (BeanId, RoastDate, IsComplete, IsActive, CreatedAt, SyncId, LastModifiedAt, IsDeleted)
        SELECT Id, RoastDate, 0, IsActive, CreatedAt, lower(hex(randomblob(16))), LastModifiedAt, IsDeleted
        FROM Beans;
    ");
    
    // 3. Update foreign keys in ShotRecords
    migrationBuilder.Sql(@"
        UPDATE ShotRecords 
        SET BagId = (SELECT Id FROM Bags WHERE Bags.BeanId = ShotRecords.BeanId LIMIT 1);
    ");
    
    // 4. Remove old column
    migrationBuilder.DropColumn(name: "RoastDate", table: "Beans");
}
```

## Code Review Checklist

- [ ] Data preservation verified
- [ ] Tests written first and passing
- [ ] ThemeKey used for all UI styling
- [ ] Performance benchmarks met
- [ ] Accessibility requirements satisfied
- [ ] Documentation updated
- [ ] Migration tested (if applicable)

---

*This constitution is living documentation. Update as the project evolves, but NEVER compromise on data preservation.*
