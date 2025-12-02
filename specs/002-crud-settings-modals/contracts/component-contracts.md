# Component Contracts: CRUD Settings with Modal Bottom Sheets

**Feature**: 002-crud-settings-modals  
**Date**: December 2, 2025

## Overview

This document defines the interfaces and contracts for UI components in this feature. Uses **Plugin.Maui.BottomSheet** with MauiReactor's `[Scaffold]` pattern for native bottom sheet modals.

---

## Bottom Sheet Infrastructure

### BottomSheet (Scaffold Wrapper)

**Purpose**: MauiReactor scaffold wrapper for Plugin.Maui.BottomSheet

**Location**: `Components/Modals/BottomSheet.cs`

```csharp
using MauiReactor.Internals;
using Plugin.Maui.BottomSheet;
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

### BottomSheetExtensions

**Purpose**: Extension method for opening bottom sheets with MauiReactor components

**Location**: `Components/Modals/BottomSheetExtensions.cs`

```csharp
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

---

## Page Component Contracts

### SettingsPage

**Purpose**: Hub page for accessing Equipment, Beans, and User Profiles management

**Navigation**:
- Entry: Via toolbar item from ActivityFeedPage
- Exit: Back navigation to ActivityFeedPage

**Interface**:
```csharp
partial class SettingsPage : Component
{
    // No injected services - navigation only
    
    // Rendered content: List of navigation options
    // - Equipment → Shell.Current.GoToAsync("equipment")
    // - Beans → Shell.Current.GoToAsync("beans")  
    // - User Profiles → Shell.Current.GoToAsync("profiles")
}
```

---

### EquipmentManagementPage (Modified)

**Purpose**: Display equipment list with CRUD via bottom sheet modals

**State Contract**:
```csharp
class EquipmentManagementState
{
    List<EquipmentDto> Equipment { get; set; }  // List of active equipment
    bool IsLoading { get; set; }                 // Loading indicator
    string? ErrorMessage { get; set; }           // Error display
}
```

**Injected Dependencies**:
```csharp
[Inject] IEquipmentService _equipmentService;
[Inject] IBottomSheetNavigationService _bottomSheetNavigationService;
```

**Actions**:
| Action | Trigger | Effect |
|--------|---------|--------|
| Load | OnMounted | Fetch equipment list |
| Add | Tap "Add" button | `_bottomSheetNavigationService.OpenBottomSheet(() => new BottomSheet { new EquipmentFormComponent() })` |
| Edit | Tap list item | Open bottom sheet with equipment data |
| Delete | Tap delete in form | Show confirmation bottom sheet |
| Refresh | OnSaved callback | Reload equipment list |

---

### BeanManagementPage (Modified)

**Purpose**: Display beans list with CRUD via bottom sheet modals

**State Contract**:
```csharp
class BeanManagementState
{
    List<BeanDto> Beans { get; set; }           // List of active beans
    bool IsLoading { get; set; }                 // Loading indicator
    string? ErrorMessage { get; set; }           // Error display
}
```

**Injected Dependencies**:
```csharp
[Inject] IBeanService _beanService;
[Inject] IBottomSheetNavigationService _bottomSheetNavigationService;
```

**Actions**:
| Action | Trigger | Effect |
|--------|---------|--------|
| Load | OnMounted | Fetch beans list |
| Add | Tap "Add" button | Open BottomSheet with BeanFormComponent |
| Edit | Tap list item | Open BottomSheet with bean data |
| Delete | Tap delete in form | Show confirmation bottom sheet |
| Refresh | OnSaved callback | Reload beans list |

---

### UserProfileManagementPage (Modified)

**Purpose**: Display profiles list with CRUD via bottom sheet modals

**State Contract**:
```csharp
class UserProfileManagementState
{
    List<UserProfileDto> Profiles { get; set; }  // List of all profiles
    bool IsLoading { get; set; }                  // Loading indicator
    string? ErrorMessage { get; set; }            // Error display
    bool IsLastProfile { get; set; }              // Prevent delete of last profile
}
```

**Injected Dependencies**:
```csharp
[Inject] IUserProfileService _userProfileService;
[Inject] IBottomSheetNavigationService _bottomSheetNavigationService;
```

**Actions**:
| Action | Trigger | Effect |
|--------|---------|--------|
| Load | OnMounted | Fetch profiles list, check if single profile |
| Add | Tap "Add" button | Open BottomSheet with UserProfileFormComponent |
| Edit | Tap list item | Open BottomSheet with profile data |
| Delete | Tap delete in form | Show confirmation (blocked if last) |
| Refresh | OnSaved callback | Reload profiles list |

---

## Form Component Contracts

### EquipmentFormComponent

**Purpose**: Pure MauiReactor form component for creating/editing equipment (displayed inside BottomSheet)

**Location**: `Components/Forms/EquipmentFormComponent.cs`

**State Contract**:
```csharp
class EquipmentFormState
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public EquipmentType Type { get; set; } = EquipmentType.GrinderBurrFlat;
    public string Notes { get; set; } = "";
    public Dictionary<string, string> Errors { get; set; } = new();
    public bool IsSaving { get; set; }
}
```

**Fluent API**:
```csharp
partial class EquipmentFormComponent : Component<EquipmentFormState>
{
    [Inject] IEquipmentService _equipmentService;
    
    private EquipmentDto? _equipment;
    private Action<EquipmentDto>? _onSaved;
    private Action? _onCancelled;
    private Action? _onDeleteRequested;
    
    public EquipmentFormComponent Equipment(EquipmentDto? equipment);
    public EquipmentFormComponent OnSaved(Action<EquipmentDto> action);
    public EquipmentFormComponent OnCancelled(Action action);
    public EquipmentFormComponent OnDeleteRequested(Action action);
}
```

**Form Fields**:
| Field | Type | Required | Input Control |
|-------|------|----------|---------------|
| Name | string | Yes | Entry (text) |
| Type | EquipmentType | Yes | Picker (enum) |
| Notes | string | No | Editor (multiline) |

---

### BeanFormComponent

**Purpose**: Pure MauiReactor form component for creating/editing beans

**Location**: `Components/Forms/BeanFormComponent.cs`

**State Contract**:
```csharp
class BeanFormState
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public string Roaster { get; set; } = "";
    public string Origin { get; set; } = "";
    public DateTimeOffset? RoastDate { get; set; }
    public string Notes { get; set; } = "";
    public Dictionary<string, string> Errors { get; set; } = new();
    public bool IsSaving { get; set; }
}
```

**Fluent API**:
```csharp
partial class BeanFormComponent : Component<BeanFormState>
{
    [Inject] IBeanService _beanService;
    
    private BeanDto? _bean;
    private Action<BeanDto>? _onSaved;
    private Action? _onCancelled;
    private Action? _onDeleteRequested;
    
    public BeanFormComponent Bean(BeanDto? bean);
    public BeanFormComponent OnSaved(Action<BeanDto> action);
    public BeanFormComponent OnCancelled(Action action);
    public BeanFormComponent OnDeleteRequested(Action action);
}
```

**Form Fields**:
| Field | Type | Required | Input Control |
|-------|------|----------|---------------|
| Name | string | Yes | Entry (text) |
| Roaster | string | No | Entry (text) |
| Origin | string | No | Entry (text) |
| RoastDate | DateTimeOffset? | No | DatePicker |
| Notes | string | No | Editor (multiline) |

---

### UserProfileFormComponent

**Purpose**: Pure MauiReactor form component for creating/editing profiles

**Location**: `Components/Forms/UserProfileFormComponent.cs`

**State Contract**:
```csharp
class UserProfileFormState
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public Dictionary<string, string> Errors { get; set; } = new();
    public bool IsSaving { get; set; }
}
```

**Fluent API**:
```csharp
partial class UserProfileFormComponent : Component<UserProfileFormState>
{
    [Inject] IUserProfileService _userProfileService;
    
    private UserProfileDto? _profile;
    private Action<UserProfileDto>? _onSaved;
    private Action? _onCancelled;
    private Action? _onDeleteRequested;
    private bool _canDelete = true;
    
    public UserProfileFormComponent Profile(UserProfileDto? profile);
    public UserProfileFormComponent OnSaved(Action<UserProfileDto> action);
    public UserProfileFormComponent OnCancelled(Action action);
    public UserProfileFormComponent OnDeleteRequested(Action action);
    public UserProfileFormComponent CanDelete(bool canDelete);
}
```

**Form Fields**:
| Field | Type | Required | Input Control |
|-------|------|----------|---------------|
| Name | string | Yes | Entry (text) |

---

### ConfirmDeleteComponent

**Purpose**: Generic confirmation component for destructive actions (displayed inside BottomSheet)

**Location**: `Components/Modals/ConfirmDeleteComponent.cs`

**Fluent API**:
```csharp
partial class ConfirmDeleteComponent : Component
{
    private string _title = "Confirm Delete";
    private string _message = "Are you sure?";
    private Action? _onConfirm;
    private Action? _onCancel;
    
    public ConfirmDeleteComponent Title(string title);
    public ConfirmDeleteComponent Message(string message);
    public ConfirmDeleteComponent OnConfirm(Action action);
    public ConfirmDeleteComponent OnCancel(Action action);
}
```

---

## Service Method Contracts (Existing - No Changes)

### IEquipmentService

```csharp
interface IEquipmentService
{
    Task<List<EquipmentDto>> GetAllActiveEquipmentAsync();
    Task<List<EquipmentDto>> GetEquipmentByTypeAsync(EquipmentType type);
    Task<EquipmentDto?> GetEquipmentByIdAsync(int id);
    Task<EquipmentDto> CreateEquipmentAsync(CreateEquipmentDto dto);
    Task<EquipmentDto> UpdateEquipmentAsync(int id, UpdateEquipmentDto dto);
    Task ArchiveEquipmentAsync(int id);  // Soft delete
    Task DeleteEquipmentAsync(int id);   // Hard delete
}
```

### IBeanService

```csharp
interface IBeanService
{
    Task<List<BeanDto>> GetAllActiveBeansAsync();
    Task<BeanDto?> GetBeanByIdAsync(int id);
    Task<BeanDto> CreateBeanAsync(CreateBeanDto dto);
    Task<BeanDto> UpdateBeanAsync(int id, UpdateBeanDto dto);
    Task ArchiveBeanAsync(int id);       // Soft delete
    Task DeleteBeanAsync(int id);        // Hard delete
}
```

### IUserProfileService

```csharp
interface IUserProfileService
{
    Task<List<UserProfileDto>> GetAllProfilesAsync();
    Task<UserProfileDto?> GetProfileByIdAsync(int id);
    Task<UserProfileDto> CreateProfileAsync(CreateUserProfileDto dto);
    Task<UserProfileDto> UpdateProfileAsync(int id, UpdateUserProfileDto dto);
    Task DeleteProfileAsync(int id);
}
```

---

## Navigation Routes

| Route | Page | Entry Point |
|-------|------|-------------|
| `history` | ActivityFeedPage | Tab bar (primary) |
| `shots` | ShotLoggingPage | Tab bar |
| `settings` | SettingsPage | Toolbar item on ActivityFeedPage |
| `equipment` | EquipmentManagementPage | Settings page navigation |
| `beans` | BeanManagementPage | Settings page navigation |
| `profiles` | UserProfileManagementPage | Settings page navigation |

---

## Usage Example

Opening an equipment form in a bottom sheet:

```csharp
// In EquipmentManagementPage
private async Task OnAddEquipmentTapped()
{
    await _bottomSheetNavigationService.OpenBottomSheet(() => 
        new BottomSheet
        {
            new EquipmentFormComponent()
                .OnSaved(async equipment => 
                {
                    await LoadDataAsync();
                    // Bottom sheet closes automatically when component unmounts
                })
                .OnCancelled(() => { /* Bottom sheet closes */ })
        });
}

private async Task OnEditEquipmentTapped(EquipmentDto equipment)
{
    await _bottomSheetNavigationService.OpenBottomSheet(() => 
        new BottomSheet
        {
            new EquipmentFormComponent()
                .Equipment(equipment)
                .OnSaved(async _ => await LoadDataAsync())
                .OnDeleteRequested(() => ShowDeleteConfirmation(equipment))
        });
}
```
