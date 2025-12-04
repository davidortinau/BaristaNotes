# Quickstart: CRUD Settings with Modal Bottom Sheets

**Feature**: 002-crud-settings-modals  
**Date**: December 2, 2025

## Overview

This guide provides a quick reference for implementing the CRUD Settings feature using **Plugin.Maui.BottomSheet** with MauiReactor's scaffold pattern.

---

## Key Changes Summary

| Area | Current State | Target State |
|------|---------------|--------------|
| Primary Page | Shot Log | Activity Feed |
| Tab Bar Items | 5 tabs | 2 tabs (History, Shot Log) |
| Settings Access | Tab bar items | Toolbar button → Settings page |
| CRUD UI | Basic list pages | Bottom sheet modal forms |

---

## Required Package

Add to `BaristaNotes.csproj`:
```xml
<PackageReference Include="Plugin.Maui.BottomSheet" Version="1.0.0" />
```

---

## Implementation Order

### Phase 1: Bottom Sheet Infrastructure
1. Add `Plugin.Maui.BottomSheet` NuGet package
2. Register `.UseBottomSheet()` in `MauiProgram.cs`
3. Create `Components/Modals/BottomSheet.cs` scaffold wrapper
4. Create `Components/Modals/BottomSheetExtensions.cs`

### Phase 2: Navigation Restructure
1. Modify `AppShell.cs` - reduce to 2 tabs
2. Add toolbar item to `ActivityFeedPage.cs`
3. Create `SettingsPage.cs` with navigation options
4. Register new routes in Shell

### Phase 3: Equipment CRUD
1. Create `EquipmentFormComponent.cs` (pure MauiReactor)
2. Modify `EquipmentManagementPage.cs` to use bottom sheet
3. Add form validation
4. Implement delete with confirmation

### Phase 4: Bean CRUD
1. Create `BeanFormComponent.cs`
2. Modify `BeanManagementPage.cs` to use bottom sheet
3. Add date picker for roast date
4. Implement delete with confirmation

### Phase 5: User Profile CRUD
1. Create `UserProfileFormComponent.cs`
2. Modify `UserProfileManagementPage.cs` to use bottom sheet
3. Implement "last profile" protection
4. Add delete confirmation

---

## Code Patterns

### 1. MauiProgram.cs Setup

```csharp
using Plugin.Maui.BottomSheet.Hosting;

public static MauiApp CreateMauiApp()
{
    var builder = MauiApp.CreateBuilder();
    builder
        .UseMauiReactorApp<App>()
        .UseBottomSheet()  // Add this
        .UseMauiCommunityToolkit()
        // ... rest of configuration
}
```

### 2. BottomSheet Scaffold Component

```csharp
// Components/Modals/BottomSheet.cs
using MauiReactor.Internals;
using Plugin.Maui.BottomSheet;
using Plugin.Maui.BottomSheet.Navigation;
using MauiControls = Microsoft.Maui.Controls;

namespace BaristaNotes.Components.Modals;

[Scaffold(typeof(Plugin.Maui.BottomSheet.BottomSheet))]
public partial class BottomSheet
{
    protected override void OnAddChild(VisualNode widget, MauiControls.BindableObject childControl)
    {
        if (childControl is MauiControls.View childView)
        {
            Validate.EnsureNotNull(NativeControl);
            NativeControl.Content = new BottomSheetContent { Content = childView };
        }
        else
        {
            base.OnAddChild(widget, childControl);
        }
    }
}
```

### 3. BottomSheet Extension Method

```csharp
// Components/Modals/BottomSheetExtensions.cs
using MauiReactor;
using MauiReactor.Internals;
using Plugin.Maui.BottomSheet;
using Plugin.Maui.BottomSheet.Navigation;

namespace BaristaNotes.Components.Modals;

public static class BottomSheetExtensions
{
    public static async Task OpenBottomSheet(
        this IBottomSheetNavigationService service,
        Func<BottomSheet> contentRender)
    {
        var templateHost = TemplateHost.Create(contentRender());
        var bottomSheet = (IBottomSheet)templateHost.NativeElement.EnsureNotNull();
        bottomSheet.Closed += (s, e) => (templateHost as IHostElement)?.Stop();
        await service.NavigateToAsync(bottomSheet);
    }
}
```

### 4. Using BottomSheet from a Page

```csharp
// Pages/EquipmentManagementPage.cs
partial class EquipmentManagementPage : Component<EquipmentManagementState>
{
    [Inject]
    IBottomSheetNavigationService _bottomSheetNavigationService;
    
    [Inject]
    IEquipmentService _equipmentService;
    
    private async Task OnAddEquipmentTapped()
    {
        await _bottomSheetNavigationService.OpenBottomSheet(() => 
            new BottomSheet
            {
                new EquipmentFormComponent()
                    .OnSaved(OnEquipmentSaved)
                    .OnCancelled(OnModalCancelled)
            });
    }
    
    private async Task OnEditEquipmentTapped(EquipmentDto equipment)
    {
        await _bottomSheetNavigationService.OpenBottomSheet(() => 
            new BottomSheet
            {
                new EquipmentFormComponent()
                    .Equipment(equipment)
                    .OnSaved(OnEquipmentSaved)
                    .OnCancelled(OnModalCancelled)
            });
    }
    
    private async void OnEquipmentSaved(EquipmentDto result)
    {
        await LoadDataAsync();
    }
}
```

### 5. Form Component (Pure MauiReactor)

```csharp
// Components/Forms/EquipmentFormComponent.cs
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using MauiReactor;

namespace BaristaNotes.Components.Forms;

class EquipmentFormState
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public EquipmentType Type { get; set; } = EquipmentType.GrinderBurrFlat;
    public string Notes { get; set; } = "";
    public Dictionary<string, string> Errors { get; set; } = new();
    public bool IsSaving { get; set; }
}

partial class EquipmentFormComponent : Component<EquipmentFormState>
{
    [Inject] IEquipmentService _equipmentService;
    
    private EquipmentDto? _equipment;
    private Action<EquipmentDto>? _onSaved;
    private Action? _onCancelled;
    
    public EquipmentFormComponent Equipment(EquipmentDto? equipment)
    {
        _equipment = equipment;
        return this;
    }
    
    public EquipmentFormComponent OnSaved(Action<EquipmentDto> action)
    {
        _onSaved = action;
        return this;
    }
    
    public EquipmentFormComponent OnCancelled(Action action)
    {
        _onCancelled = action;
        return this;
    }
    
    protected override void OnMounted()
    {
        if (_equipment != null)
        {
            SetState(s => {
                s.Id = _equipment.Id;
                s.Name = _equipment.Name;
                s.Type = _equipment.Type;
                s.Notes = _equipment.Notes ?? "";
            });
        }
    }
    
    public override VisualNode Render()
        => VStack(spacing: 16,
            // Header
            Label(State.Id.HasValue ? "Edit Equipment" : "Add Equipment")
                .FontSize(20)
                .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
            
            // Name field
            VStack(spacing: 4,
                Label("Name *").FontSize(14),
                Entry()
                    .Placeholder("Equipment name")
                    .Text(State.Name)
                    .OnTextChanged(text => SetState(s => s.Name = text)),
                State.Errors.TryGetValue("Name", out var nameError)
                    ? Label(nameError).TextColor(Colors.Red).FontSize(12)
                    : null
            ),
            
            // Type picker
            VStack(spacing: 4,
                Label("Type *").FontSize(14),
                Picker()
                    .Title("Select type")
                    .ItemsSource(Enum.GetValues<EquipmentType>())
                    .SelectedItem(State.Type)
                    .OnSelectedIndexChanged(idx => 
                        SetState(s => s.Type = (EquipmentType)idx))
            ),
            
            // Notes field
            VStack(spacing: 4,
                Label("Notes").FontSize(14),
                Editor()
                    .Placeholder("Optional notes")
                    .Text(State.Notes)
                    .OnTextChanged(text => SetState(s => s.Notes = text))
                    .HeightRequest(100)
            ),
            
            // Buttons
            HStack(spacing: 12,
                Button("Cancel")
                    .OnClicked(() => _onCancelled?.Invoke()),
                Button("Save")
                    .BackgroundColor(Colors.Blue)
                    .TextColor(Colors.White)
                    .IsEnabled(!State.IsSaving)
                    .OnClicked(OnSave)
            )
            .HorizontalOptions(Microsoft.Maui.Layouts.LayoutOptions.End)
        )
        .Padding(20)
        .BackgroundColor(Colors.White);
    
    private async void OnSave()
    {
        // Validate
        var errors = ValidateForm();
        if (errors.Count > 0)
        {
            SetState(s => s.Errors = errors);
            return;
        }
        
        SetState(s => { s.IsSaving = true; s.Errors = new(); });
        
        try
        {
            EquipmentDto result;
            if (State.Id.HasValue)
            {
                result = await _equipmentService.UpdateEquipmentAsync(
                    State.Id.Value,
                    new UpdateEquipmentDto
                    {
                        Name = State.Name,
                        Type = State.Type,
                        Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                    });
            }
            else
            {
                result = await _equipmentService.CreateEquipmentAsync(
                    new CreateEquipmentDto
                    {
                        Name = State.Name,
                        Type = State.Type,
                        Notes = string.IsNullOrWhiteSpace(State.Notes) ? null : State.Notes
                    });
            }
            
            _onSaved?.Invoke(result);
        }
        catch (Exception ex)
        {
            SetState(s => {
                s.IsSaving = false;
                s.Errors = new Dictionary<string, string> { ["General"] = ex.Message };
            });
        }
    }
    
    private Dictionary<string, string> ValidateForm()
    {
        var errors = new Dictionary<string, string>();
        
        if (string.IsNullOrWhiteSpace(State.Name))
            errors["Name"] = "Equipment name is required";
        else if (State.Name.Length > 100)
            errors["Name"] = "Name must be 100 characters or less";
        
        return errors;
    }
}
```

### 6. Delete Confirmation Component

```csharp
// Components/Modals/ConfirmDeleteComponent.cs
partial class ConfirmDeleteComponent : Component
{
    private string _title = "Confirm Delete";
    private string _message = "Are you sure?";
    private Action? _onConfirm;
    private Action? _onCancel;
    
    public ConfirmDeleteComponent Title(string title) { _title = title; return this; }
    public ConfirmDeleteComponent Message(string message) { _message = message; return this; }
    public ConfirmDeleteComponent OnConfirm(Action action) { _onConfirm = action; return this; }
    public ConfirmDeleteComponent OnCancel(Action action) { _onCancel = action; return this; }
    
    public override VisualNode Render()
        => VStack(spacing: 16,
            Label(_title)
                .FontSize(18)
                .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
            Label(_message)
                .TextColor(Colors.Gray),
            HStack(spacing: 12,
                Button("Cancel")
                    .OnClicked(() => _onCancel?.Invoke()),
                Button("Delete")
                    .BackgroundColor(Colors.Red)
                    .TextColor(Colors.White)
                    .OnClicked(() => _onConfirm?.Invoke())
            )
            .HorizontalOptions(Microsoft.Maui.Layouts.LayoutOptions.End)
        )
        .Padding(20)
        .BackgroundColor(Colors.White);
}
```

---

## File Checklist

### New Files
- [ ] `BaristaNotes/Components/Modals/BottomSheet.cs`
- [ ] `BaristaNotes/Components/Modals/BottomSheetExtensions.cs`
- [ ] `BaristaNotes/Components/Forms/EquipmentFormComponent.cs`
- [ ] `BaristaNotes/Components/Forms/BeanFormComponent.cs`
- [ ] `BaristaNotes/Components/Forms/UserProfileFormComponent.cs`
- [ ] `BaristaNotes/Components/Modals/ConfirmDeleteComponent.cs`
- [ ] `BaristaNotes/Pages/SettingsPage.cs`

### Modified Files
- [ ] `BaristaNotes/MauiProgram.cs` - Add `.UseBottomSheet()`
- [ ] `BaristaNotes/BaristaNotes.csproj` - Add Plugin.Maui.BottomSheet package
- [ ] `BaristaNotes/AppShell.cs` - Reduce to 2 tabs
- [ ] `BaristaNotes/Pages/ActivityFeedPage.cs` - Add toolbar item
- [ ] `BaristaNotes/Pages/EquipmentManagementPage.cs` - Use bottom sheets
- [ ] `BaristaNotes/Pages/BeanManagementPage.cs` - Use bottom sheets
- [ ] `BaristaNotes/Pages/UserProfileManagementPage.cs` - Use bottom sheets

### Test Files
- [ ] `BaristaNotes.Tests/Integration/EquipmentCrudTests.cs`
- [ ] `BaristaNotes.Tests/Integration/BeanCrudTests.cs`
- [ ] `BaristaNotes.Tests/Integration/UserProfileCrudTests.cs`

---

## Dependencies

**New dependency to add:**
- `Plugin.Maui.BottomSheet` - Native bottom sheet support

**Already installed:**
- `CommunityToolkit.Maui` 9.1.1 - Other UI components
- `Reactor.Maui` 4.0.3-beta - MVU framework
- `Microsoft.EntityFrameworkCore.Sqlite` 8.0.0 - Data persistence

---

## Testing Commands

```bash
# Run all tests
dotnet test BaristaNotes.Tests

# Run specific test category
dotnet test BaristaNotes.Tests --filter "Category=Integration"

# Build and run app
dotnet build BaristaNotes -f net10.0-ios
dotnet build BaristaNotes -f net10.0-android
```

---

## Common Pitfalls

1. **Bottom sheet not showing**: Ensure `.UseBottomSheet()` is called in `MauiProgram.cs`

2. **Service injection not working**: Use `[Inject]` attribute on component fields for MauiReactor DI

3. **Bottom sheet not closing**: The `Closed` event handler in the extension method automatically cleans up the template host

4. **State not refreshing after save**: Use callback pattern (`OnSaved`) to trigger parent page refresh

5. **Delete last profile**: Check profile count before allowing delete operation

6. **Bottom sheet content layout**: Wrap content in proper layout containers with padding
