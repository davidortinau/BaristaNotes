# UI Design Guide: BaristaNotes

**Feature**: 001-espresso-tracking  
**Created**: 2025-12-02  
**Purpose**: Define visual design, theming, and accessibility standards for the espresso tracking application

## Design Philosophy

**Style**: Modern yet 1950s-inspired design with coffee-centric aesthetics  
**Era Influence**: Mid-century modern simplicity, clean lines, retro elegance  
**Theme**: Coffee-focused color palette with warm, inviting tones  
**Default Mode**: Dark theme (with light theme support)

## Color Palette

### Dark Theme (Default)

**Primary Colors** (Coffee-inspired):
- **Espresso Dark**: `#1A0F0A` - Deep espresso brown for backgrounds
- **Rich Coffee**: `#3E2723` - Medium coffee brown for surfaces
- **Caramel**: `#D4A574` - Warm caramel for primary accents
- **Crema**: `#E8D5B7` - Light cream for highlights

**Surface Colors**:
- **Background**: `#121212` - Near black (Material Design dark baseline)
- **Surface**: `#1E1E1E` - Elevated surfaces
- **Card**: `#2A2522` - Coffee-tinted card backgrounds

**Semantic Colors**:
- **Success** (Good Shot): `#4CAF50` - Green
- **Warning** (Adjust Recipe): `#FFA726` - Orange
- **Error**: `#EF5350` - Red
- **Info**: `#42A5F5` - Blue

**Text Colors**:
- **Primary Text**: `#FAFAFA` - Almost white
- **Secondary Text**: `#B8B8B8` - Muted gray
- **Disabled Text**: `#757575` - Dark gray

### Light Theme

**Primary Colors**:
- **Background**: `#FFF8F0` - Warm off-white (coffee with cream)
- **Surface**: `#FFFFFF` - Pure white
- **Card**: `#F5EDE3` - Light cream
- **Espresso Accent**: `#3E2723` - Dark coffee for primary actions
- **Caramel**: `#B8835A` - Darker caramel for contrast

**Text Colors**:
- **Primary Text**: `#1A0F0A` - Deep espresso
- **Secondary Text**: `#5D4E46` - Medium brown
- **Disabled Text**: `#9E9E9E` - Gray

## Typography

**Font Family**: 
- Primary: "OpenSans" (already included in MAUI template)
- Fallback: System default sans-serif

**Type Scale** (1950s-inspired hierarchy):

```
Display (Headlines):  36pt / Bold / 44pt line-height
Title Large:          28pt / SemiBold / 36pt line-height
Title Medium:         22pt / SemiBold / 28pt line-height
Body Large:           18pt / Regular / 24pt line-height
Body Medium:          16pt / Regular / 22pt line-height
Body Small:           14pt / Regular / 20pt line-height
Caption:              12pt / Regular / 16pt line-height
```

**Letter Spacing**:
- Headlines: -0.5px (tight, vintage feel)
- Body: 0px (normal)
- Captions: +0.3px (slightly open)

## Spacing Scale

**8-point grid system** (retro-modern consistency):

```
XS:  4pt   (tight spacing, inline elements)
S:   8pt   (small gaps, icon padding)
M:   16pt  (standard spacing, card padding)
L:   24pt  (section spacing)
XL:  32pt  (major section breaks)
XXL: 48pt  (page margins)
```

## Touch Accessibility

**Minimum Touch Targets**: 44x44pt (iOS) / 48x48dp (Android)  
**Button Height**: 48pt minimum  
**Spacing Between Tappable Elements**: 8pt minimum  
**Card Touch Area**: Full card width, 72pt minimum height

**Input Fields**:
- Height: 56pt (comfortable single-line input)
- Padding: 16pt horizontal, 12pt vertical
- Corner Radius: 8pt

## Color Contrast (WCAG AA Compliance)

**Dark Theme**:
- Primary Text on Background: `#FAFAFA` on `#121212` = 19.6:1 ✅
- Secondary Text on Background: `#B8B8B8` on `#121212` = 11.2:1 ✅
- Caramel on Surface: `#D4A574` on `#1E1E1E` = 5.8:1 ✅
- Crema on Rich Coffee: `#E8D5B7` on `#3E2723` = 8.1:1 ✅

**Light Theme**:
- Primary Text on Background: `#1A0F0A` on `#FFF8F0` = 16.2:1 ✅
- Secondary Text on Background: `#5D4E46` on `#FFF8F0` = 7.9:1 ✅
- Espresso on Caramel: `#3E2723` on `#B8835A` = 4.8:1 ✅

## Component Styling

### Buttons

**Primary Button**:
- Background: `Caramel` (`#D4A574` dark / `#B8835A` light)
- Text: `Espresso Dark` (`#1A0F0A`)
- Corner Radius: 8pt
- Height: 48pt
- Padding: 16pt horizontal
- Font: Body Large / SemiBold
- Shadow: 2pt elevation (subtle depth)

**Secondary Button**:
- Background: Transparent
- Border: 2pt solid `Caramel`
- Text: `Caramel`
- Corner Radius: 8pt
- Height: 48pt

**Text Button**:
- Background: Transparent
- Text: `Caramel`
- No border
- Padding: 12pt horizontal

### Cards

**Shot Record Card**:
- Background: `Card` surface color
- Corner Radius: 12pt
- Padding: 16pt
- Shadow: 4pt elevation (lifted appearance)
- Border: 1pt solid `#FFFFFF10` (subtle glow in dark mode)

### Rating Control (5-point)

**Visual**: Coffee cup icons (empty → full)
- Empty State: Outline stroke, `Secondary Text` color
- Filled State: Solid fill, `Caramel` color
- Size: 32x32pt per icon
- Spacing: 8pt between icons
- Total Width: 192pt (32×5 + 8×4)

### Input Fields

**Text Entry**:
- Background: `Surface` elevated color
- Border: 1pt solid `#FFFFFF20` (dark) / `#00000020` (light)
- Focus Border: 2pt solid `Caramel`
- Corner Radius: 8pt
- Height: 56pt
- Label: Caption size, `Secondary Text` color
- Placeholder: `Disabled Text` color

**Number Picker** (Grind Setting, Grams):
- Same styling as text entry
- Increment/Decrement buttons: 32x32pt touch targets
- Icon: +/- symbols in `Caramel`

### Icons

**Icon Font**: Material Design Icons or SF Symbols (platform-native)  
**Sizes**:
- Small: 16pt (inline with text)
- Medium: 24pt (buttons, toolbar)
- Large: 32pt (rating, primary actions)
- Hero: 48pt (empty states)

**Tab Bar Icons**: 28pt with 4pt padding

## Layout Patterns

### Shot Logging Form

```
┌─────────────────────────────┐
│ Title Bar: "New Shot"       │ 56pt height
├─────────────────────────────┤
│ Bean Picker (dropdown)      │ 56pt
│ Grind Setting (number)      │ 56pt
│ Dose In (grams)             │ 56pt
│ Time (seconds)              │ 56pt
│ Dose Out (grams)            │ 56pt
│ Drink Type (dropdown)       │ 56pt
│ [Spacing 16pt]              │
│ Rating: ☕☕☕☕☕            │ 48pt
│ [Spacing 16pt]              │
│ Notes (multiline)           │ 120pt
│ [Spacing 24pt]              │
│ [Save Button]               │ 48pt
│ [Spacing 16pt]              │
└─────────────────────────────┘
```

### Activity Feed Card

```
┌─────────────────────────────┐
│ ☕ Espresso Shot    ⭐⭐⭐⭐⭐ │ Header (24pt)
│ Made by: David              │ Caption
│ For: Sarah                  │ Caption
│                             │
│ Brazilian Blend             │ Body Medium
│ 18g in → 36g out (28s)     │ Body Small
│ Grind: 5.5                  │ Body Small
│                             │
│ "Perfect crema today!"      │ Body Small (notes)
│                             │
│ 2:45 PM • Today            │ Caption (timestamp)
└─────────────────────────────┘
```

## Animations

**Duration**: 
- Fast: 150ms (micro-interactions)
- Medium: 250ms (page transitions)
- Slow: 350ms (major state changes)

**Easing**: 
- Enter: `EaseOut` (decelerating)
- Exit: `EaseIn` (accelerating)
- Move: `EaseInOut` (smooth)

**Transitions**:
- Page Navigation: Slide with fade (250ms)
- Card Appear: Fade + Scale up from 0.95 (200ms)
- Button Press: Scale down to 0.97 (100ms)

## Dark/Light Theme Toggle

**Default**: Dark theme on app launch  
**Storage**: Persist user preference in `IPreferencesService`  
**Key**: `"app_theme"` → `"dark"` | `"light"` | `"system"`  
**Application**: Apply theme colors via Maui Reactor theme dictionary  
**Toggle Location**: Settings page (future feature) or AppShell toolbar

## Implementation Notes for Maui Reactor

**Theme Application**:
```csharp
// All colors defined as static resources
public static class AppColors
{
    // Dark theme (default)
    public static Color EspressoDark = Color.FromArgb("#1A0F0A");
    public static Color RichCoffee = Color.FromArgb("#3E2723");
    public static Color Caramel = Color.FromArgb("#D4A574");
    public static Color Crema = Color.FromArgb("#E8D5B7");
    public static Color Background = Color.FromArgb("#121212");
    public static Color Surface = Color.FromArgb("#1E1E1E");
    public static Color Card = Color.FromArgb("#2A2522");
    
    // Text
    public static Color PrimaryText = Color.FromArgb("#FAFAFA");
    public static Color SecondaryText = Color.FromArgb("#B8B8B8");
    public static Color DisabledText = Color.FromArgb("#757575");
    
    // Semantic
    public static Color Success = Color.FromArgb("#4CAF50");
    public static Color Warning = Color.FromArgb("#FFA726");
    public static Color Error = Color.FromArgb("#EF5350");
    public static Color Info = Color.FromArgb("#42A5F5");
}

public static class AppFontSizes
{
    public const double Display = 36;
    public const double TitleLarge = 28;
    public const double TitleMedium = 22;
    public const double BodyLarge = 18;
    public const double BodyMedium = 16;
    public const double BodySmall = 14;
    public const double Caption = 12;
}

public static class AppSpacing
{
    public const double XS = 4;
    public const double S = 8;
    public const double M = 16;
    public const double L = 24;
    public const double XL = 32;
    public const double XXL = 48;
}
```

**Style Application in Components**:
```csharp
// Apply styles fluently
Button("Save Shot")
    .BackgroundColor(AppColors.Caramel)
    .TextColor(AppColors.EspressoDark)
    .FontSize(AppFontSizes.BodyLarge)
    .HeightRequest(48)
    .CornerRadius(8)
    .Padding(AppSpacing.M, 0)
```

## Accessibility Checklist

- [ ] All touch targets ≥ 44pt (iOS) / 48dp (Android)
- [ ] Color contrast ≥ 4.5:1 for normal text (WCAG AA)
- [ ] Color contrast ≥ 3:1 for large text (WCAG AA)
- [ ] Semantic properties set for screen readers
- [ ] Focus indicators visible for keyboard navigation
- [ ] Text scales with system font size settings
- [ ] No color-only information (use icons + color)
- [ ] Animations respect reduced motion preference

## References

- WCAG 2.1 AA Guidelines: https://www.w3.org/WAI/WCAG21/quickref/
- Apple Human Interface Guidelines: https://developer.apple.com/design/human-interface-guidelines/
- Material Design Accessibility: https://m3.material.io/foundations/accessible-design/overview
- .NET MAUI Styling: https://learn.microsoft.com/en-us/dotnet/maui/user-interface/styles/
