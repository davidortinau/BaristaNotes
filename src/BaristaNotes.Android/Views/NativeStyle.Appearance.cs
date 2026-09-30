using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Text.Style;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using System.Runtime.CompilerServices;

namespace BaristaNotes.AndroidApp.Views;

internal sealed partial class NativeStyle
{
    private readonly WeakAppearanceRegistry<View> _appearanceViews = new();
    private readonly ConditionalWeakTable<View, object> _fixedThemeViews = new();

    private T Track<T>(T view) where T : View
    {
        _appearanceViews.Add(view);
        return view;
    }

    public void FixTheme(View root)
    {
        _fixedThemeViews.GetValue(root, _ => new object());
        if (root is ViewGroup group)
            for (var index = 0; index < group.ChildCount; index++)
                if (group.GetChildAt(index) is { } child) FixTheme(child);
    }

    public void RefreshAppearance(bool dark, params View?[] roots)
    {
        if (dark == IsDark) return;
        var before = IsDark ? NativePalette.Dark : NativePalette.Light;
        var after = dark ? NativePalette.Dark : NativePalette.Light;
        IsDark = dark;
        var visited = new HashSet<View>();
        var lists = new HashSet<RecyclerView>();
        foreach (var root in roots) Refresh(root);
        foreach (var view in _appearanceViews.Targets)
        {
            if (view.Handle == IntPtr.Zero)
            {
                _appearanceViews.Remove(view);
                continue;
            }
            Refresh(view);
            // A detached RecyclerView holder can retain a custom parent that
            // wasn't created by Label/Row/Column. Recolor it without rebinding
            // or reconstructing any form/draft.
            for (var parent = view.Parent as View; parent is not null && parent.Handle != IntPtr.Zero;
                 parent = parent.Parent as View)
                Refresh(parent);
        }
        foreach (var list in lists)
        {
            var manager = list.GetLayoutManager();
            using var position = manager?.OnSaveInstanceState();
            list.GetAdapter()?.NotifyDataSetChanged();
            if (position is not null) manager?.OnRestoreInstanceState(position);
        }

        void Refresh(View? view)
        {
            if (view is null || view.Handle == IntPtr.Zero || !visited.Add(view)
                || _fixedThemeViews.TryGetValue(view, out _)) return;
            Recolor(view.Background);
            Recolor(view.Foreground);
            if (view is TextView label)
            {
                var textColor = before.TranslateTo(label.CurrentTextColor, after);
                if (textColor != label.CurrentTextColor) label.SetTextColor(new Color(textColor));
                var hintColor = before.TranslateTo(label.CurrentHintTextColor, after);
                if (hintColor != label.CurrentHintTextColor) label.SetHintTextColor(new Color(hintColor));
                // Never replace an editable buffer: text, selection, composing
                // region and its source-bound draft must survive a theme change.
                if (label is not EditText) RefreshSpans(label, before, after);
            }
            if (view is RecyclerView recycler) lists.Add(recycler);
            if (view is ViewGroup group)
                for (var index = 0; index < group.ChildCount; index++) Refresh(group.GetChildAt(index));
            view.Invalidate();
        }
        void Recolor(Drawable? drawable)
        {
            switch (drawable)
            {
                case PaletteDrawable palette:
                    palette.Refresh(before, after);
                    break;
                case ColorDrawable solid:
                    solid.Color = new Color(before.TranslateTo(solid.Color.ToArgb(), after));
                    break;
            }
        }
    }

    private static void RefreshSpans(TextView label, NativePalette before, NativePalette after)
    {
        if (label.TextFormatted is not ISpanned original) return;
        using var spanClass = Java.Lang.Class.FromType(typeof(ForegroundColorSpan));
        var foregrounds = original.GetSpans(0, original.Length(), spanClass);
        if (foregrounds is null || foregrounds.Length == 0) return;
        using var copy = new SpannableString(original);
        var changed = false;
        foreach (var span in foregrounds.OfType<ForegroundColorSpan>())
        {
            var color = before.TranslateTo(span.ForegroundColor, after);
            if (color == span.ForegroundColor) continue;
            var start = original.GetSpanStart(span);
            var end = original.GetSpanEnd(span);
            var flags = original.GetSpanFlags(span);
            copy.RemoveSpan(span);
            using var replacement = new ForegroundColorSpan(new Color(color));
            copy.SetSpan(replacement, start, end, flags);
            changed = true;
        }
        if (changed) label.TextFormatted = copy;
    }
}
