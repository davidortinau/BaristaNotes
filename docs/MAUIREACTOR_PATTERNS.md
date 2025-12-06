# MauiReactor Patterns

This document explains the MauiReactor MVU (Model-View-Update) architecture and component patterns used in BaristaNotes.

## Table of Contents

- [MVU Architecture Overview](#mvu-architecture-overview)
- [Component Anatomy](#component-anatomy)
- [State Management](#state-management)
- [Props and Data Flow](#props-and-data-flow)
- [Navigation](#navigation)
- [Service Injection](#service-injection)
- [Common Patterns](#common-patterns)

## MVU Architecture Overview

MauiReactor implements the MVU (Model-View-Update) pattern, inspired by Elm and similar to React's component model. This architecture promotes:

- **Immutable State**: State changes create new state objects rather than mutating existing ones
- **Unidirectional Data Flow**: Data flows down through props, events flow up through callbacks
- **Declarative UI**: UI is declared as a function of state
- **Predictable Updates**: All state changes go through a single update mechanism

### MVU Flow

```
┌─────────────┐
│    State    │
│  (Model)    │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│    View     │ ──────> User Interaction
│   (Render)  │
└──────┬──────┘
       │
       ▼
┌─────────────┐
│   Update    │
│  (SetState) │
└──────┬──────┘
       │
       └─────> New State ──> Re-render
```

## Component Anatomy

A MauiReactor component consists of three main parts:

### 1. State Class

Holds the component's local data. All fields should be mutable for the SetState mechanism to work.

```csharp
class ShotLoggingState
{
    public double Dose { get; set; } = 18.0;
    public double GrindSetting { get; set; } = 3.0;
    public double OutputWeight { get; set; } = 36.0;
    public int ExtractionTime { get; set; } = 28;
    public int Rating { get; set; } = 3;
    public bool IsLoading { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
```

### 2. Props Class

Receives data from the parent component. Props are immutable from the component's perspective.

```csharp
class ShotLoggingPageProps
{
    public int? ShotId { get; set; }  // null = create mode, value = edit mode
}
```

### 3. Component Class

Extends `Component<TState>` or `Component<TState, TProps>` and implements the `Render()` method.

```csharp
partial class ShotLoggingPage : Component<ShotLoggingState, ShotLoggingPageProps>
{
    [Inject]
    IShotService _shotService;
    
    public override VisualNode Render()
    {
        return ContentPage(
            VStack(spacing: 16,
                Label("Dose (g)"),
                Entry($"{State.Dose:F1}")
                    .OnTextChanged(text => 
                    {
                        if (double.TryParse(text, out var value))
                            SetState(s => s.Dose = value);
                    }),
                
                Button("Save")
                    .IsEnabled(!State.IsLoading)
                    .OnClicked(SaveShot)
            )
        );
    }
    
    async Task SaveShot()
    {
        SetState(s => s.IsLoading = true);
        
        try
        {
            await _shotService.CreateShotAsync(/* ... */);
            await Navigation.GoBackAsync();
        }
        catch (Exception ex)
        {
            SetState(s => 
            {
                s.IsLoading = false;
                s.ErrorMessage = ex.Message;
            });
        }
    }
}
```

## State Management

### Updating State

Use `SetState()` to update component state. This triggers a re-render with the new state.

```csharp
// Single property update
SetState(s => s.Count = 5);

// Multiple property updates
SetState(s => 
{
    s.IsLoading = false;
    s.Data = result;
    s.ErrorMessage = string.Empty;
});
```

### State Initialization

Override `OnMounted()` to initialize state when the component first mounts:

```csharp
protected override async void OnMounted()
{
    base.OnMounted();
    
    // Load initial data
    if (Props.ShotId.HasValue)
    {
        SetState(s => s.IsLoading = true);
        var shot = await _shotService.GetShotByIdAsync(Props.ShotId.Value);
        SetState(s => 
        {
            s.Dose = shot.Dose;
            s.GrindSetting = shot.GrindSetting;
            s.IsLoading = false;
        });
    }
}
```

### State Best Practices

1. **Keep state minimal**: Only store what's needed for rendering
2. **Derive computed values**: Calculate derived data in Render() rather than storing it
3. **Avoid nested objects**: Flattened state is easier to update
4. **Use nullable types**: For optional data or loading states

```csharp
// Good: Flat, minimal state
class MyState
{
    public string SearchText { get; set; } = "";
    public List<int> ResultIds { get; set; } = new();
    public bool IsSearching { get; set; }
}

// Avoid: Nested, redundant state
class MyState
{
    public SearchRequest Request { get; set; }  // Nested object
    public SearchResult Result { get; set; }    // Contains full objects
    public int ResultCount { get; set; }        // Redundant (can be derived)
}
```

## Props and Data Flow

### Passing Props

Pass data to child components through props during navigation or when rendering:

```csharp
// Navigation with props
await Shell.Current.GoToAsync<ShotLoggingPageProps>(
    "shot-logging",
    props => props.ShotId = shotId
);

// Rendering child components with props (if you create reusable components)
new MyChildComponent()
{
    ItemId = State.SelectedId,
    OnItemSelected = HandleSelection
}
```

### Props Validation

Check props in `OnMounted()` or early in `Render()`:

```csharp
protected override async void OnMounted()
{
    base.OnMounted();
    
    if (Props.ShotId == null)
    {
        // Invalid props, navigate away
        await Navigation.GoBackAsync();
        return;
    }
    
    // Load data with valid props
    await LoadShot(Props.ShotId.Value);
}
```

### Props vs State

| Aspect | Props | State |
|--------|-------|-------|
| Source | Parent component / navigation | Component itself |
| Mutability | Read-only | Mutable via SetState |
| Lifetime | Fixed for component instance | Can change during lifetime |
| Purpose | Input/configuration | Local UI state |

## Navigation

### Shell Navigation

BaristaNotes uses .NET MAUI Shell for navigation. Register routes in `AppShell.cs`:

```csharp
Routing.RegisterRoute("shot-logging", typeof(ShotLoggingPage));
Routing.RegisterRoute("bean-detail", typeof(BeanDetailPage));
```

### Type-Safe Navigation with Props

Navigate using generic GoToAsync with props initialization:

```csharp
// Navigate with props
await Shell.Current.GoToAsync<ShotLoggingPageProps>(
    "shot-logging",
    props => props.ShotId = shotId
);

// Navigate without props (create mode)
await Shell.Current.GoToAsync("shot-logging");

// Go back
await Shell.Current.GoToAsync("..");
```

### Navigation Parameters

Props are the preferred method for passing data. Avoid query parameters:

```csharp
// Good: Type-safe props
await Shell.Current.GoToAsync<BeanDetailPageProps>(
    "bean-detail",
    props => props.BeanId = beanId
);

// Avoid: String-based query parameters (error-prone)
await Shell.Current.GoToAsync($"bean-detail?id={beanId}");
```

## Service Injection

### Declaring Injected Services

Use the `[Inject]` attribute to inject services registered in the DI container:

```csharp
partial class ShotLoggingPage : Component<ShotLoggingState, ShotLoggingPageProps>
{
    [Inject]
    IShotService _shotService;
    
    [Inject]
    IBeanService _beanService;
    
    [Inject]
    IFeedbackService _feedbackService;
}
```

Note: The component class must be `partial` for injection to work.

### Service Lifetimes

Services are registered in `MauiProgram.cs` with appropriate lifetimes:

```csharp
// Singleton: One instance for app lifetime (DbContext, preferences)
builder.Services.AddSingleton<BaristasDbContext>();
builder.Services.AddSingleton<IPreferencesService, PreferencesService>();

// Transient: New instance each time (services with no state)
builder.Services.AddTransient<IShotService, ShotService>();
builder.Services.AddTransient<IBeanService, BeanService>();
```

## Common Patterns

### Loading States

Handle async data loading with loading indicators:

```csharp
class MyPageState
{
    public bool IsLoading { get; set; }
    public List<ShotDto> Shots { get; set; } = new();
    public string ErrorMessage { get; set; } = string.Empty;
}

public override VisualNode Render()
{
    return ContentPage(
        State.IsLoading
            ? ActivityIndicator().IsRunning(true)
            : State.ErrorMessage != string.Empty
                ? Label(State.ErrorMessage).TextColor(Colors.Red)
                : VStack(
                    // Render loaded data
                    State.Shots.Select(shot => 
                        new ShotRecordCard { Shot = shot }
                    ).ToArray()
                )
    );
}
```

### Form Validation

Validate form inputs before submission:

```csharp
class FormState
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsValid => 
        !string.IsNullOrWhiteSpace(Name) && 
        Email.Contains('@');
}

public override VisualNode Render()
{
    return VStack(
        Entry(State.Name)
            .OnTextChanged(text => SetState(s => s.Name = text)),
        Entry(State.Email)
            .OnTextChanged(text => SetState(s => s.Email = text)),
        Button("Submit")
            .IsEnabled(State.IsValid)
            .OnClicked(SubmitForm)
    );
}
```

### Conditional Rendering

Show/hide UI elements based on state:

```csharp
public override VisualNode Render()
{
    return VStack(
        Label("Options"),
        
        // Conditional single element
        State.ShowAdvanced
            ? VStack(
                Label("Advanced Settings"),
                Slider()
            )
            : null,
        
        // Conditional with alternative
        State.IsEditMode
            ? Button("Save").OnClicked(Save)
            : Button("Edit").OnClicked(EnableEdit)
    );
}
```

### List Rendering

Render lists with Select and ToArray:

```csharp
public override VisualNode Render()
{
    return ScrollView(
        VStack(spacing: 8,
            State.Items
                .Select(item => 
                    HStack(
                        Label(item.Name),
                        Button("Delete")
                            .OnClicked(() => DeleteItem(item.Id))
                    )
                )
                .ToArray()
        )
    );
}
```

### Bottom Sheets

Use bottom sheets for forms and confirmations:

```csharp
BottomSheet()
    .IsPresented(State.ShowForm)
    .OnDismissing(() => SetState(s => s.ShowForm = false))
    .Content(
        VStack(
            Label("Enter Details"),
            Entry()
                .Placeholder("Name"),
            Button("Confirm")
                .OnClicked(ConfirmAction)
        )
    )
```

### Component Lifecycle

Override lifecycle methods for initialization and cleanup:

```csharp
protected override async void OnMounted()
{
    base.OnMounted();
    // Called once when component first mounts
    await LoadInitialData();
}

protected override void OnPropsChanged()
{
    base.OnPropsChanged();
    // Called when props change (e.g., navigation to same route with different params)
    if (Props.ShotId.HasValue)
        await LoadShot(Props.ShotId.Value);
}

protected override void OnWillUnmount()
{
    base.OnWillUnmount();
    // Called before component unmounts
    // Clean up subscriptions, timers, etc.
}
```

## Performance Tips

1. **Minimize Render() complexity**: Extract helper methods for complex UI sections
2. **Avoid expensive operations in Render()**: Cache computed values in state
3. **Use SetState efficiently**: Batch multiple state changes in one SetState call
4. **Lazy load data**: Load data only when needed, not all upfront
5. **Optimize lists**: Consider virtualization for long lists (use CollectionView)

## Additional Resources

- [MauiReactor Official Documentation](https://github.com/adospace/reactorui-maui)
- [MVU Pattern Explained](https://guide.elm-lang.org/architecture/)
- [React Docs (similar concepts)](https://react.dev/learn)
