using Android.Content;
using Android.Views;
using Android.Views.Accessibility;
using Android.Widget;
using Action = System.Action;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class SelectorActionRow(Context context) : LinearLayout(context)
{
    public Action? Activate { get; set; }

    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        Clickable = Focusable = true;
        ImportantForAccessibility = ImportantForAccessibility.Yes;
    }

    public override bool PerformClick()
    {
        var action = Activate;
        if (!Enabled || action is null)
            return false;
        base.PerformClick();
        action();
        return true;
    }

    public override void OnInitializeAccessibilityNodeInfo(AccessibilityNodeInfo? info)
    {
        base.OnInitializeAccessibilityNodeInfo(info);
        if (info is null)
            return;
        info.ClassName = "android.widget.Button";
        info.Clickable = Activate is not null;
        info.Selected = Selected;
    }
}
