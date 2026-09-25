# MauiReactor Patterns

BaristaNotes builds its UI with MauiReactor components. XAML is used only where
a dependency or platform resource requires it; application screens are C#.

## Component Shape

A page can have state, navigation props, or both:

```csharp
class ExamplePageProps
{
    public int ItemId { get; set; }
}

class ExamplePageState
{
    public bool IsLoading { get; set; }
    public string? ErrorMessage { get; set; }
}

partial class ExamplePage
    : Component<ExamplePageState, ExamplePageProps>
{
    [Inject] IShotService _shotService;

    public override VisualNode Render() =>
        ContentPage(
            State.IsLoading
                ? ActivityIndicator().IsRunning(true)
                : Label($"Item {Props.ItemId}"));
}
```

Use a `partial` component when MauiReactor source generation supplies injected
members or other generated behavior.

## State

State contains values that change the rendered output.

```csharp
SetState(s =>
{
    s.IsLoading = false;
    s.ErrorMessage = null;
});
```

Rules:

- Keep state local and small.
- Derive simple display values during rendering.
- Update related fields in one `SetState` call.
- Do not change state directly without `SetState`.
- Do not use `INotifyPropertyChanged` for component-local state.

Subscribe to events in `OnMounted` and unsubscribe in `OnWillUnmount`:

```csharp
protected override void OnMounted()
{
    base.OnMounted();
    _rangeService.SettingsChanged += OnSettingsChanged;
}

protected override void OnWillUnmount()
{
    _rangeService.SettingsChanged -= OnSettingsChanged;
    base.OnWillUnmount();
}
```

Event callbacks that can run off the UI thread must dispatch UI state changes
through `MainThread.BeginInvokeOnMainThread`.

## Dependency Injection

Register services in the matching file under `Hosting/`. Inject them with
`[Inject]`:

```csharp
[Inject] IDrinkValueRangeService _rangeService;
[Inject] IFeedbackService _feedbackService;
```

Do not create domain services in a page and do not use a global service
locator.

## Navigation

Register routes in `Hosting/RouteRegistration.cs`:

```csharp
MauiReactor.Routing.RegisterRoute<ValueRangeEditorPage>(
    "value-range-editor");
```

Use typed props for route data:

```csharp
await Shell.Current.GoToAsync<ValueRangeEditorPageProps>(
    "value-range-editor",
    props =>
    {
        props.Metric = metric;
        props.Method = method;
    });
```

Use absolute Shell routes only for the three root destinations:

```text
//shots
//history
//settings
```

Do not add string query parameters or `QueryProperty` attributes when typed
props can carry the value.

## Application Shell

`AppShell` renders a `TabBar` with three `ShellContent` nodes. The wrapper is
required for correct route resolution in published iOS builds. Individual
pages hide the visual tab bar when the design requires a custom bottom
navigation row.

Database initialization is an explicit UI state:

- loading shows "Preparing your data";
- success renders the main Shell; and
- failure shows the error and a retry action.

Do not create a second application window to represent startup state.

## Styling

Use the shared style system:

- `ThemeKeys`
- `AppColors`
- `AppFontSizes`
- `AppSpacing`
- `AppIcons`
- `MaterialSymbolsFont`

```csharp
Label("Dose")
    .ThemeKey(ThemeKeys.FormLabel);

VStack(content)
    .Spacing(AppSpacing.S)
    .Padding(AppSpacing.M);
```

Prefer a theme key when a semantic style already exists. Direct values are
acceptable for component-specific geometry, such as a measured column width or
a small icon size, when no shared semantic token applies.

Use Material Symbols or an image asset for icons. Do not use emoji as UI icons.

## Shared Rows and Forms

Use `AdaptiveTwoLineTile` for a two-line row that can have:

- a leading icon;
- a primary label;
- supporting text;
- trailing status; and
- a chevron or other action.

The component keeps the leading and trailing content vertically centered and
uses flexible middle space. Do not copy its grid and padding into each page.

Use components under `Components/FormFields/` for repeated form controls.
Keep validation, labels, focus behavior, and accessibility consistent.

## Adaptive Layout

- Use `Grid` star columns for content that must adapt to width.
- Use `Auto` only for content with an intrinsic size.
- Use a minimum height rather than a fixed row height when text can wrap.
- Keep touch targets at least 44 by 44 device-independent units.
- Check long text, large font scaling, narrow phones, tablets, and landscape.
- Set safe-area behavior on the layout that reaches the screen edge. Do not
  assume that a page-level setting applies to nested edge layouts.

Use `Border` with a `RoundRectangle` for rounded containers. Do not use
deprecated `Frame`, `ListView`, or `TableView` controls.

## Async Work

Do not perform database or network work in `Render()`.

Start loading from lifecycle or user actions and expose loading, success, and
error states. Catch only errors that the component can handle. Log technical
details and show a clear recovery action through the feedback service.

Use `CancellationToken` for operations that can outlive the page.

## Lists

Use `CollectionView` for long or virtualized lists. A `ScrollView` with a
generated stack is acceptable only for a small bounded set, such as the fixed
list of brew methods.

Use stable item identifiers and avoid loading related rows in a loop.

## Accessibility

- Give interactive elements a clear accessible name.
- Exclude decorative glyphs from the accessibility tree.
- Keep keyboard focus visible.
- Do not encode status only by color.
- Make the complete tile actionable when the complete tile appears actionable.
- Add automation IDs to controls used by DevFlow scenarios.

## UI Verification

For a UI change:

1. run a Debug build;
2. wait for MAUI DevFlow;
3. inspect the visual tree;
4. capture the initial screen;
5. exercise every changed state and transition;
6. inspect the screen after each transition; and
7. check top and bottom edges, focus, scrolling, and blocked input.

```bash
dotnet build src/BaristaNotes -t:Run -f net11.0-ios
maui devflow wait
maui devflow ui tree --depth 4
```

A successful build alone does not verify a UI change.
