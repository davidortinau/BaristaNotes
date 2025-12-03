# Architecture Constraints

## NON-NEGOTIABLE ARCHITECTURAL DECISIONS

These constraints are **MANDATORY** and **CANNOT** be changed without explicit user approval.

---

## 🚫 UI Framework: MauiReactor

**RULE**: All UI must use **MauiReactor** components, NOT standard MAUI XAML or C# UI patterns.

**Why**: This is the chosen UI framework for the entire application.

**Examples**:
- ✅ `Button("Click Me").OnClicked(async () => ...)`
- ✅ `Entry().Text(state.Name).OnTextChanged(t => ...)`
- ❌ `new Button { Text = "Click Me" }`
- ❌ XAML files

---

## 🚫 Popups, Toasts, Alerts: UXDivers.Popups.Maui

**RULE**: ALL popups, toasts, modals, and alert-style UI **MUST** use **UXDivers.Popups.Maui** library.

**Package**: `UXDivers.Grial`

**Why**: This provides:
- Consistent styled popups across the app
- Color-coded feedback (green success, red error, blue info, yellow warning)
- Custom animations and positioning
- Full control over appearance

**Service**: `IFeedbackService` wraps UXDivers and provides:
- `ShowSuccess(message)` - Green toast with ✓
- `ShowError(message, recoveryAction?)` - Red toast with ✕
- `ShowInfo(message)` - Blue toast with ℹ
- `ShowWarning(message)` - Yellow toast with ⚠
- `ShowLoading(message)` / `HideLoading()` - Loading spinner overlay

**❌ DO NOT USE**:
- ❌ `CommunityToolkit.Maui.Alerts.Toast` - Wrong library!
- ❌ `Application.Current.MainPage.DisplayAlert()` - Blocking, inconsistent styling
- ❌ Custom popup implementations - Reinventing the wheel
- ❌ Platform-specific toasts - No control over styling

**Examples**:
```csharp
// ✅ CORRECT
_feedbackService.ShowSuccess("Shot saved successfully");
_feedbackService.ShowError("Failed to save", "Please try again");

// ❌ WRONG
var toast = Toast.Make("Shot saved"); // CommunityToolkit
await Application.Current.MainPage.DisplayAlert("Success", "Shot saved", "OK");
```

**Implementation Details**:
- UXDivers toasts are `async void` (fire-and-forget)
- Default durations: Success=2000ms, Error=5000ms, Info=3000ms, Warning=3000ms
- Toasts display at top of screen with slide-in animation
- **If navigating after showing toast**: Add `await Task.Delay(2000)` to allow toast to display

---

## 🚫 Navigation: Shell-Based with MauiReactor Extensions

**RULE**: Use Shell navigation with MauiReactor's `GoToAsync` extensions.

**Pattern for passing parameters**:
```csharp
// Register route
Routing.RegisterRoute<MyPage>("my-page");

// Navigate with props
await Shell.Current.GoToAsync<MyPageProps>("my-page", props => props.Id = 123);

// Page must inherit Component<TState, TProps>
class MyPage : Component<MyPageState, MyPageProps>
{
    // Access via Props.Id
}
```

**❌ DO NOT USE**:
- ❌ `Navigation.PushAsync()` - Not Shell-based
- ❌ QueryProperty attributes - Not compatible with MauiReactor Props pattern
- ❌ Passing parameters via query strings - Use typed Props

---

## 🚫 Dependency Injection: Microsoft.Extensions.DependencyInjection

**RULE**: All services **MUST** be registered in DI and injected via `[Inject]` attribute.

**Examples**:
```csharp
// ✅ CORRECT
partial class MyPage : Component<MyPageState>
{
    [Inject]
    IShotService _shotService;
    
    [Inject]
    IFeedbackService _feedbackService;
}

// ❌ WRONG
var shotService = new ShotService(); // Manual instantiation
var shotService = ServiceLocator.Get<IShotService>(); // Service locator pattern
```

---

## 🚫 Data Layer: Entity Framework Core

**RULE**: All database operations **MUST** go through Entity Framework Core DbContext.

**Why**: Centralized schema management, migrations, change tracking, LINQ queries.

**❌ DO NOT USE**:
- ❌ Raw SQL strings
- ❌ SQLite.Net direct queries
- ❌ Manual ADO.NET connections

**Exception**: Raw SQL is allowed ONLY for:
- Complex reporting queries that EF can't optimize
- Bulk operations where performance is critical
- Must use `dbContext.Database.ExecuteSqlRaw()` with parameterized queries

---

## 🚫 Testing Framework: xUnit + FluentAssertions

**RULE**: All tests use xUnit syntax with FluentAssertions for assertions.

**Examples**:
```csharp
// ✅ CORRECT
[Fact]
public void Should_Calculate_Correctly()
{
    var result = calculator.Add(2, 3);
    result.Should().Be(5);
}

// ❌ WRONG
[Test] // NUnit
public void TestCalculation()
{
    Assert.AreEqual(5, result); // MSTest/NUnit syntax
}
```

---

## 🚫 Async/Await Patterns

**RULE**: 
1. Methods returning `Task` or `Task<T>` **MUST** be `async`
2. `async void` is **ONLY** allowed for event handlers
3. Always `await` async operations - don't use `.Result` or `.Wait()`

**Navigation Timing with Toasts**:
When showing a toast before navigation:
```csharp
// ✅ CORRECT - Wait for toast to display
_feedbackService.ShowSuccess("Operation complete");
await Task.Delay(2000); // Match toast duration
await Navigation.PopAsync();

// ❌ WRONG - Toast gets interrupted
_feedbackService.ShowSuccess("Operation complete");
await Navigation.PopAsync(); // Immediate navigation kills toast
```

---

## 🚫 State Management: Component State Pattern

**RULE**: MauiReactor components manage state via `Component<TState>` base class.

**Pattern**:
```csharp
class MyState
{
    public string Name { get; set; } = "";
    public bool IsLoading { get; set; }
}

class MyPage : Component<MyState>
{
    protected override void OnMounted()
    {
        SetState(s => s.IsLoading = true);
    }
}
```

**❌ DO NOT USE**:
- ❌ `INotifyPropertyChanged` - MauiReactor handles this
- ❌ Observable collections - Use `List<T>` in state, create new instances on updates
- ❌ Manual property change notifications

---

## When In Doubt

1. **Check existing code** in the same project
2. **Ask the user** before introducing new libraries or patterns
3. **Document your reasoning** if you think a constraint should be changed

**Remember**: These constraints exist for **consistency**, **maintainability**, and **team productivity**. Violating them creates technical debt.

