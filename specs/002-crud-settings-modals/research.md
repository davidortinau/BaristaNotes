# Research: CRUD Settings with Modal Bottom Sheets

**Feature**: 002-crud-settings-modals  
**Date**: December 2, 2025

## Research Tasks

This document consolidates research findings for all unknowns identified during planning.

---

## 1. Bottom Sheet Modal Implementation in MauiReactor

### Decision
Use **Plugin.Maui.BottomSheet** with MauiReactor scaffold pattern. This is the recommended approach from the official MauiReactor integration samples.

### Rationale
- Official MauiReactor integration sample exists at `adospace/mauireactor-integration/BottomSheet`
- Uses `[Scaffold]` attribute to generate MauiReactor-compatible wrapper
- Integrates with DI via `IBottomSheetNavigationService`
- Supports proper MauiReactor component lifecycle (OnMounted, OnWillUnmount)
- Native bottom sheet appearance on iOS/Android

### Alternatives Considered
1. **CommunityToolkit.Maui Popup**: Less native feel, not a true bottom sheet
2. **The49.Maui.BottomSheet**: Also works but Plugin.Maui.BottomSheet has better MauiReactor integration patterns
3. **Custom overlay component**: Would require manual animation handling

### Required Package
```xml
<PackageReference Include="Plugin.Maui.BottomSheet" Version="x.x.x" />
```

### Implementation Pattern

**1. Create scaffolded BottomSheet component:**
```csharp
using MauiReactor.Internals;
using Plugin.Maui.BottomSheet.Navigation;

namespace BaristaNotes.Components.Modals;

[Scaffold(typeof(Plugin.Maui.BottomSheet.BottomSheet))]
public partial class BottomSheet
{
    protected override void OnAddChild(VisualNode widget, MauiControls.BindableObject childControl)
    {
        if (childControl is MauiControls.View childView)
        {
            Validate.EnsureNotNull(NativeControl);
            NativeControl.Content = new Plugin.Maui.BottomSheet.BottomSheetContent { Content = childView };
        }
        else
        {
            base.OnAddChild(widget, childControl);
        }
    }
}

public static class BottomSheetExtensions
{
    public static async Task OpenBottomSheet(this IBottomSheetNavigationService service, Func<BottomSheet> contentRender)
    {
        var templateHost = TemplateHost.Create(contentRender());
        var bottomSheet = (Plugin.Maui.BottomSheet.IBottomSheet)templateHost.NativeElement.EnsureNotNull();
        bottomSheet.Closed += (s, e) => (templateHost as IHostElement)?.Stop();
        await service.NavigateToAsync(bottomSheet);
    }
}
```

**2. Register in MauiProgram.cs:**
```csharp
builder
    .UseMauiReactorApp<App>()
    .UseBottomSheet()  // Add this
    // ...
```

**3. Use from MauiReactor component:**
```csharp
partial class EquipmentManagementPage : Component<EquipmentManagementState>
{
    [Inject]
    IBottomSheetNavigationService _bottomSheetNavigationService;
    
    private async Task OpenAddEquipmentSheet()
    {
        await _bottomSheetNavigationService.OpenBottomSheet(() => new BottomSheet
        {
            new EquipmentFormComponent()
                .OnSaved(OnEquipmentSaved)
        });
    }
}
```

**4. Create form component (pure MauiReactor):**
```csharp
class EquipmentFormState
{
    public string Name { get; set; } = "";
    public EquipmentType Type { get; set; }
    public string Notes { get; set; } = "";
}

partial class EquipmentFormComponent : Component<EquipmentFormState>
{
    private Action<EquipmentDto>? _onSaved;
    
    public EquipmentFormComponent OnSaved(Action<EquipmentDto> action)
    {
        _onSaved = action;
        return this;
    }
    
    public override VisualNode Render()
        => VStack(spacing: 16,
            Label("Add Equipment").FontSize(20).FontAttributes(FontAttributes.Bold),
            
            Entry()
                .Placeholder("Equipment Name")
                .Text(State.Name)
                .OnTextChanged(text => SetState(s => s.Name = text)),
            
            Picker()
                .Title("Type")
                .ItemsSource(Enum.GetValues<EquipmentType>())
                .SelectedItem(State.Type)
                .OnSelectedIndexChanged(idx => SetState(s => s.Type = (EquipmentType)idx)),
            
            Editor()
                .Placeholder("Notes (optional)")
                .Text(State.Notes)
                .OnTextChanged(text => SetState(s => s.Notes = text))
                .HeightRequest(100),
            
            Button("Save")
                .OnClicked(OnSave)
        )
        .Padding(20);
    
    private void OnSave()
    {
        // Validate and invoke callback
        _onSaved?.Invoke(new EquipmentDto { ... });
    }
}
```

---

## 2. Navigation Architecture Change

### Decision
Restructure `AppShell` to use 2 tabs (Activity Feed as primary, Shot Log) with settings accessible via toolbar item on Activity Feed page.

### Rationale
- Follows user's requirement for cleaner navigation
- Activity Feed is most frequently accessed - should be primary
- Settings operations are secondary, don't need tab-level visibility
- Shell navigation supports toolbar items via `ContentPage.ToolbarItems`

### Implementation Pattern
```csharp
// AppShell.cs - simplified
public override VisualNode Render()
{
    return Shell(
        TabBar(
            ShellContent("History")
                .Icon("list.png")
                .Route("history")
                .RenderContent(() => new ActivityFeedPage()),
            
            ShellContent("Shot Log")
                .Icon("coffee.png")
                .Route("shots")
                .RenderContent(() => new ShotLoggingPage())
        )
    );
}

// ActivityFeedPage.cs - with toolbar
public override VisualNode Render()
{
    return ContentPage("Shot History",
        // ... content
    )
    .ToolbarItems(
        new ToolbarItem("Settings")
            .IconImageSource("settings.png")
            .OnClicked(NavigateToSettings)
    );
}
```

---

## 3. Settings Page Design

### Decision
Create a dedicated `SettingsPage` with navigation options to Equipment, Beans, and User Profiles management lists.

### Rationale
- Single entry point for all configuration
- Clear visual hierarchy
- Each management section loads its own list with CRUD modals
- Back navigation returns to Activity Feed

### Layout Pattern
```
Settings Page
├── Equipment Section → EquipmentManagementPage
├── Beans Section → BeanManagementPage  
└── User Profiles Section → UserProfileManagementPage
```

---

## 4. Delete Confirmation UX

### Decision
Use a smaller BottomSheet confirmation dialog before delete operations, with clear warning text for items with existing shot record references.

### Rationale
- Consistent with other modal patterns
- Constitution requires confirmation for destructive actions
- Native bottom sheet feel

### Implementation
```csharp
partial class ConfirmDeleteComponent : Component
{
    private string _title = "Delete?";
    private string _message = "Are you sure?";
    private Action? _onConfirm;
    private Action? _onCancel;
    
    public override VisualNode Render()
        => VStack(spacing: 16,
            Label(_title).FontSize(18).FontAttributes(FontAttributes.Bold),
            Label(_message),
            HStack(spacing: 12,
                Button("Cancel").OnClicked(() => _onCancel?.Invoke()),
                Button("Delete").BackgroundColor(Colors.Red).TextColor(Colors.White)
                    .OnClicked(() => _onConfirm?.Invoke())
            )
        )
        .Padding(20);
}
```

---

## 5. Form Validation Approach

### Decision
Client-side validation with inline error display. Required fields: Equipment (name, type), Bean (name), Profile (name).

### Validation Rules
| Entity | Field | Rule |
|--------|-------|------|
| Equipment | Name | Required, max 100 chars |
| Equipment | Type | Required (enum selection) |
| Bean | Name | Required, max 100 chars |
| Bean | RoastDate | Optional, must be past or today |
| Profile | Name | Required, max 50 chars |

---

## 6. Testing Strategy for Modal Components

### Decision
Unit test service interactions via mocks. Integration tests for full CRUD flows using in-memory database.

### Rationale
- Modal UI components are difficult to unit test directly
- Service layer already has good test coverage patterns
- Integration tests verify end-to-end behavior

### Test Categories
1. **Unit Tests**: Service method calls, validation logic, state management
2. **Integration Tests**: Full CRUD cycle with real (in-memory) database
3. **Manual Tests**: Modal animations, gestures, visual appearance

---

## Summary

All technical unknowns resolved. Key decisions:
- **Plugin.Maui.BottomSheet** with MauiReactor `[Scaffold]` pattern for native bottom sheets
- `IBottomSheetNavigationService` injected via DI for sheet management
- Pure MauiReactor components inside bottom sheets (form components)
- 2-tab navigation with toolbar settings access
- Client-side form validation
- Confirmation dialogs via bottom sheet for destructive actions
