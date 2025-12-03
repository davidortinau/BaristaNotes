using MauiReactor;
using MauiReactor.Internals;
using UXDivers.Popups.Maui;

namespace BaristaNotes.MauiControls;

[Scaffold(typeof(UXDivers.Popups.Maui.PopupPage))]
partial class PopupPage
{
    protected override void OnAddChild(VisualNode widget, Microsoft.Maui.Controls.BindableObject childNativeControl)
    {
        if (childNativeControl is Microsoft.Maui.Controls.View content)
        {
            Validate.EnsureNotNull(NativeControl);
            NativeControl.Content = content;
        }

        base.OnAddChild(widget, childNativeControl);
    }

    protected override void OnRemoveChild(VisualNode widget, Microsoft.Maui.Controls.BindableObject childNativeControl)
    {
        Validate.EnsureNotNull(NativeControl);

        if (childNativeControl is Microsoft.Maui.Controls.View content &&
            NativeControl.Content == content)
        {
            NativeControl.Content = null;
        }
        base.OnRemoveChild(widget, childNativeControl);
    }
}
