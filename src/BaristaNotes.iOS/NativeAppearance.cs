using BaristaNotes.Core.Models.Enums;
using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal static class NativeAppearance
{
    public static void Apply(UIWindow window, ThemeMode mode)
    {
        // This changes only this app window; UIKit keeps every controller and draft.
        window.OverrideUserInterfaceStyle = mode switch
        {
            ThemeMode.Light => UIUserInterfaceStyle.Light,
            ThemeMode.Dark => UIUserInterfaceStyle.Dark,
            _ => UIUserInterfaceStyle.Unspecified
        };
        Invalidate(window);
    }

    public static void Invalidate(UIView view)
    {
        // Each layer owner reapplies its named color role in LayoutSubviews.
        // Do not infer roles by comparing flattened CGColor values.
        view.SetNeedsLayout();
        view.SetNeedsDisplay();
        foreach (var child in view.Subviews) Invalidate(child);
    }
}

internal sealed class AppearanceWindow : UIWindow
{
    public AppearanceWindow(UIWindowScene scene) : base(scene)
    {
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitUserInterfaceStyle>(static (environment, _) =>
                NativeAppearance.Invalidate((AppearanceWindow)environment));
    }
#pragma warning disable CS0672, CA1422
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        if (!OperatingSystem.IsIOSVersionAtLeast(17) && previousTraitCollection?.UserInterfaceStyle != TraitCollection.UserInterfaceStyle)
            NativeAppearance.Invalidate(this);
    }
#pragma warning restore CS0672, CA1422
}

internal sealed class SettingsAboutTile : UIView
{
    private readonly UILabel _caption = new();
    private readonly UILabel _version = new() { Text = "Version 1.0", Lines = 0 };
    private readonly UILabel _description = new() { Text = "Track your espresso journey", Lines = 0 };

    public SettingsAboutTile()
    {
        AccessibilityIdentifier = "settings.about";
        BackgroundColor = NativeTheme.Surface;
        AddSubviews(_caption, _version, _description);
        UpdateFonts(TraitCollection);
    }
    public void UpdateFonts(UITraitCollection traits)
    {
        SourceScaledText.Tracked(_caption, "BARISTANOTES", 10, 2, NativeTheme.Secondary, traits);
        _version.Font = SourceScaledText.Font(18, true, traits);
        _version.TextColor = NativeTheme.TextPrimary;
        _description.Font = SourceScaledText.Font(13, false, traits);
        _description.TextColor = NativeTheme.Secondary;
        SetNeedsLayout();
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, (nfloat)Math.Max(96,
        32 + SliceUi.Measure(_caption, size.Width - 32).Height
        + SliceUi.Measure(_version, size.Width - 32).Height + SliceUi.Measure(_description, size.Width - 32).Height));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        nfloat y = 14;
        foreach (var label in new[] { _caption, _version, _description })
        {
            var height = SliceUi.Measure(label, Bounds.Width - 32).Height;
            label.Frame = new CGRect(16, y, Bounds.Width - 32, height);
            y += height;
        }
    }
}
