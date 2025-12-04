# Quickstart Guide: Coffee-Themed Color System

**Feature**: 002-coffee-theme  
**For**: Developers and Designers  
**Last Updated**: 2025-12-04

## Overview

This guide helps you get started with BaristaNotes' coffee-themed color system and theme management. Whether you're adding new UI pages or updating existing colors, this document provides practical guidance and code examples.

---

## For Developers

### Using Semantic Color Tokens in Pages

**✅ DO THIS** - Use semantic color tokens from `AppColors`:
```csharp
using BaristaNotes.Styles;

// In your MauiReactor page component
Label("Shot Details")
    .TextColor(AppColors.Light.TextPrimary) // For light mode
    .BackgroundColor(AppColors.Light.Surface);

// Better: Use ApplicationTheme helper (handles theme switching automatically)
Label("Shot Details")
    .TextColor(ApplicationTheme.IsLightTheme ? AppColors.Light.TextPrimary : AppColors.Dark.TextPrimary)
    .BackgroundColor(ApplicationTheme.IsLightTheme ? AppColors.Light.Surface : AppColors.Dark.Surface);
```

**❌ DON'T DO THIS** - Avoid hardcoded hex values:
```csharp
// BAD: Hardcoded colors won't change with theme
Label("Shot Details")
    .TextColor(Color.FromArgb("#352B23"))
    .BackgroundColor(Color.FromArgb("#FCEFE1"));
```

### Semantic Token Reference

Quick reference for common use cases:

| Use Case | Light Mode Token | Dark Mode Token |
|----------|------------------|-----------------|
| Page background | `AppColors.Light.Background` | `AppColors.Dark.Background` |
| Card background | `AppColors.Light.Surface` | `AppColors.Dark.Surface` |
| Secondary card/chip | `AppColors.Light.SurfaceVariant` | `AppColors.Dark.SurfaceVariant` |
| Button background | `AppColors.Light.Primary` | `AppColors.Dark.Primary` |
| Button text | `AppColors.Light.OnPrimary` | `AppColors.Dark.OnPrimary` |
| Heading text | `AppColors.Light.TextPrimary` | `AppColors.Dark.TextPrimary` |
| Body text | `AppColors.Light.TextSecondary` | `AppColors.Dark.TextSecondary` |
| Helper text | `AppColors.Light.TextMuted` | `AppColors.Dark.TextMuted` |
| Divider lines | `AppColors.Light.Outline` | `AppColors.Dark.Outline` |
| Success message | `AppColors.Success` | `AppColors.Success` (same) |
| Warning message | `AppColors.Warning` | `AppColors.Warning` (same) |
| Error message | `AppColors.Error` | `AppColors.Error` (same) |

### Adding Theme-Aware Styling to New Pages

**Example: Creating a new page with coffee theme colors**

```csharp
using MauiReactor;
using BaristaNotes.Styles;

namespace BaristaNotes.Pages;

class MyNewPageState
{
    // Your state here
}

partial class MyNewPage : Component<MyNewPageState>
{
    public override VisualNode Render()
    {
        return ContentPage("My New Page",
            ScrollView(
                VStack(spacing: 16,
                    // Card with theme-aware styling
                    Border(
                        VStack(spacing: 12,
                            Label("Card Title")
                                .FontSize(18)
                                .FontAttributes(FontAttributes.Bold)
                                .TextColor(ApplicationTheme.IsLightTheme 
                                    ? AppColors.Light.TextPrimary 
                                    : AppColors.Dark.TextPrimary),
                            
                            Label("Card description text")
                                .FontSize(14)
                                .TextColor(ApplicationTheme.IsLightTheme 
                                    ? AppColors.Light.TextSecondary 
                                    : AppColors.Dark.TextSecondary)
                        )
                        .Padding(16)
                    )
                    .BackgroundColor(ApplicationTheme.IsLightTheme 
                        ? AppColors.Light.Surface 
                        : AppColors.Dark.Surface)
                    .StrokeThickness(0)
                    .StrokeShape(new RoundRectangle().CornerRadius(12))
                )
                .Padding(16)
            )
        )
        .BackgroundColor(ApplicationTheme.IsLightTheme 
            ? AppColors.Light.Background 
            : AppColors.Dark.Background);
    }
}
```

### Testing Theme Changes Locally

**Step 1**: Run app in simulator/device  
**Step 2**: Navigate to Settings page  
**Step 3**: Tap theme selector (Light/Dark/System)  
**Step 4**: Observe colors update immediately across all pages  
**Step 5**: Restart app—verify theme persists

**Testing System Mode**:
1. Select "System" theme in app settings
2. Change device theme in OS settings (iOS: Settings > Display & Brightness, Android: Settings > Display > Dark theme)
3. Verify app theme updates automatically

### Common Patterns

**Pattern 1: Theme-aware helper method**
```csharp
private Color GetThemedColor(Color lightColor, Color darkColor)
{
    return ApplicationTheme.IsLightTheme ? lightColor : darkColor;
}

// Usage
Label("Text")
    .TextColor(GetThemedColor(AppColors.Light.TextPrimary, AppColors.Dark.TextPrimary));
```

**Pattern 2: Conditional styling in loops**
```csharp
items.Select(item =>
    Border(
        Label(item.Name)
            .TextColor(GetThemedColor(AppColors.Light.TextPrimary, AppColors.Dark.TextPrimary))
    )
    .BackgroundColor(GetThemedColor(AppColors.Light.Surface, AppColors.Dark.Surface))
)
```

### Accessing Theme Service

If you need programmatic control over theme:

```csharp
using BaristaNotes.Services;

// In your page or component
[Inject] IThemeService _themeService;

// Get current theme mode
var mode = await _themeService.GetThemeModeAsync(); // Returns: Light, Dark, or System

// Set theme mode
await _themeService.SetThemeModeAsync(ThemeMode.Dark); // Forces dark theme

// Get resolved theme (Light or Dark, never System)
var effectiveTheme = _themeService.CurrentTheme; // AppTheme.Light or AppTheme.Dark
```

---

## For Designers

### Coffee Color Palette Specifications

All colors defined in `BaristaNotes/Resources/Styles/AppColors.cs`.

**Light Mode Palette**:
- **Background**: `#D2BCA5` (warm cream) - App body, behind cards
- **Surface**: `#FCEFE1` (soft beige) - Cards, modals
- **SurfaceVariant**: `#ECDAC4` (light tan) - Secondary cards, chips
- **Primary**: `#86543F` (rich coffee) - Accent, buttons, active states
- **OnPrimary**: `#F8F6F4` (cream) - Text on primary buttons
- **TextPrimary**: `#352B23` (dark espresso) - Headings, important text
- **TextSecondary**: `#7C7067` (medium brown) - Body text
- **TextMuted**: `#A38F7D` (light brown) - Helper text
- **Outline**: `#D7C5B2` (tan) - Borders, dividers

**Dark Mode Palette**:
- **Background**: `#48362E` (dark brown) - App body, behind cards
- **Surface**: `#48362E` (dark brown) - Cards, modals
- **SurfaceVariant**: `#7D5A45` (medium brown) - Secondary cards, chips
- **Primary**: `#86543F` (rich coffee) - Same as light mode
- **OnPrimary**: `#F8F6F4` (cream) - Same as light mode
- **TextPrimary**: `#F8F6F4` (cream) - Headings, important text
- **TextSecondary**: `#C5BFBB` (light tan) - Body text
- **TextMuted**: `#A19085` (tan) - Helper text
- **Outline**: `#5A463B` (dark tan) - Borders, dividers

**Semantic Colors** (same in both themes):
- **Success**: `#4CAF50` (green)
- **Warning**: `#FFA726` (orange)
- **Error**: `#EF5350` (red)
- **Info**: `#42A5F5` (blue)

### Updating Colors

To change colors in the palette:

1. **Open file**: `BaristaNotes/Resources/Styles/AppColors.cs`
2. **Locate token**: Find the semantic token you want to change (e.g., `Primary`)
3. **Update hex value**: Change the hex code in `Color.FromArgb()`
4. **Verify contrast**: Run unit tests to ensure WCAG AA compliance (see below)
5. **Test visually**: Launch app, switch themes, verify appearance

**Example**:
```csharp
// Before
public static Color Primary { get; } = Color.FromArgb("#86543F");

// After (changing to darker coffee)
public static Color Primary { get; } = Color.FromArgb("#6B3F2F");
```

### Accessibility Requirements

All text/background combinations MUST meet WCAG AA contrast requirements:
- **Normal text** (< 18pt): Minimum 4.5:1 contrast ratio
- **Large text** (≥ 18pt): Minimum 3:1 contrast ratio

**Verifying Contrast**:
1. Run unit tests: `dotnet test --filter FullyQualifiedName~ColorContrastTests`
2. Tests calculate contrast ratios for all token pairs
3. Tests fail if any combination violates WCAG AA

**Online Tools**:
- [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/)
- [Contrast Ratio Calculator](https://contrast-ratio.com/)

### Design-to-Code Workflow

**When adding new colors**:
1. Create mockup with new color
2. Identify semantic purpose (e.g., "accent for success states")
3. Choose appropriate token name (`Success`, `Primary`, etc.)
4. Add to `AppColors.cs` in both `Light` and `Dark` nested classes
5. Verify contrast ratios with unit tests
6. Update this quickstart guide with new token documentation

**When updating existing colors**:
1. Identify which semantic token to update (e.g., `Primary`)
2. Update hex value in `AppColors.cs`
3. Run unit tests to verify contrast ratios still pass
4. Test app visually in both light and dark modes

---

## For QA / Testers

### Manual Testing Checklist

**Theme Selection**:
- [ ] Open Settings page
- [ ] Tap "Light" theme—app updates to light mode immediately
- [ ] Tap "Dark" theme—app updates to dark mode immediately
- [ ] Tap "System" theme—app follows OS theme
- [ ] Close app, reopen—theme persists (same mode as before close)

**System Theme Tracking** (System mode only):
- [ ] Set app to "System" theme
- [ ] Change device theme in OS settings
- [ ] Verify app updates automatically within 1 second
- [ ] Switch back and forth multiple times—app follows each time

**Visual Verification** (Both light and dark):
- [ ] Check all 6 pages: Shot Logging, Activity Feed, Settings, Equipment, Beans, User Profiles
- [ ] Verify backgrounds are coffee-themed (warm tones, not gray/blue)
- [ ] Verify text is readable (high contrast)
- [ ] Verify buttons use coffee accent color (#86543F)
- [ ] Verify no white flashes during theme transitions
- [ ] Verify modals/overlays update theme correctly

**Performance**:
- [ ] Theme switch feels instant (< 300ms)
- [ ] No lag when changing themes rapidly
- [ ] No memory spikes (use Xcode Instruments or Android Profiler)

**Accessibility**:
- [ ] Enable iOS VoiceOver or Android TalkBack
- [ ] Navigate to Settings > Theme
- [ ] Verify theme options are announced ("Light theme", "Dark theme", "System theme")
- [ ] Change theme—verify change is announced
- [ ] Verify all text is readable (no low-contrast combinations)

---

## Troubleshooting

### Issue: Colors don't update when theme changes

**Possible Causes**:
1. Page uses hardcoded colors instead of semantic tokens
2. Page doesn't respond to theme change events

**Solution**:
```csharp
// Replace hardcoded colors
.TextColor(Color.FromArgb("#352B23")) // ❌ Bad

// With semantic tokens
.TextColor(ApplicationTheme.IsLightTheme 
    ? AppColors.Light.TextPrimary 
    : AppColors.Dark.TextPrimary) // ✅ Good
```

### Issue: Theme doesn't persist after app restart

**Possible Causes**:
1. `ThemeService.SetThemeModeAsync` not being called
2. Preferences API not saving correctly

**Solution**:
- Verify `SetThemeModeAsync` is awaited: `await _themeService.SetThemeModeAsync(mode);`
- Check app permissions for file system access (iOS: no special permissions needed)

### Issue: White flash during theme change

**Possible Causes**:
1. `OnApply()` method takes too long (async operations)
2. Partial state where some controls styled, others not

**Solution**:
- Avoid async operations in `ApplicationTheme.OnApply()`
- Pre-compute all colors before calling `OnApply()`
- Use static readonly color objects (no allocations)

### Issue: Contrast ratio test fails

**Possible Causes**:
1. New color doesn't meet WCAG AA requirements (4.5:1 for normal text, 3:1 for large text)

**Solution**:
- Use [WebAIM Contrast Checker](https://webaim.org/resources/contrastchecker/) to verify ratio
- Adjust foreground or background color until ratio >= 4.5:1
- Update color in `AppColors.cs`, re-run tests

---

## Additional Resources

- **Spec Document**: [spec.md](./spec.md) - User stories and requirements
- **Research**: [research.md](./research.md) - Technology decisions and best practices
- **Data Model**: [data-model.md](./data-model.md) - Theme preference and color palette structure
- **Contracts**: [contracts/theme-contracts.md](./contracts/theme-contracts.md) - Service interfaces and API contracts
- **Constitution**: `.specify/memory/constitution.md` - Code quality and accessibility standards

**External Resources**:
- [WCAG 2.1 Contrast Guidelines](https://www.w3.org/WAI/WCAG21/Understanding/contrast-minimum.html)
- [MauiReactor Theme Documentation](https://github.com/adospace/MauiReactor) (if available)
- [.NET MAUI AppTheme Documentation](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/system-theme-changes)

---

## Getting Help

**Questions?**
- Check existing pages for usage examples (e.g., `SettingsPage.cs`)
- Review contracts for API signatures
- Reach out to team for design guidance

**Found a Bug?**
- Verify unit tests pass: `dotnet test`
- Check console for errors during theme switch
- Report issue with repro steps

---

**Last Updated**: 2025-12-04  
**Version**: 1.0.0  
**Maintainer**: BaristaNotes Development Team
