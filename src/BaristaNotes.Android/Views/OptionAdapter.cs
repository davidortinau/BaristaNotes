using Android.Graphics;
using Android.Views;
using Android.Views.Accessibility;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Action = System.Action;

namespace BaristaNotes.AndroidApp.Views;

internal sealed record NativeChoice(string Id, string Text, bool Selected, Action Select);

internal sealed class OptionAdapter(NativeStyle style, bool numeric = false) : RecyclerView.Adapter
{
    private IReadOnlyList<NativeChoice> _items = [];
    private readonly List<WeakReference<ChoiceRow>> _rows = [];
    private string? _selectedId;
    public override int ItemCount => _items.Count;

    public void SetItems(IReadOnlyList<NativeChoice> items)
    {
        _items = items;
        NotifyDataSetChanged();
    }

    public void SetSelection(string id)
    {
        if (_selectedId == id)
            return;
        var previous = IndexOf(_selectedId);
        _selectedId = id;
        var current = IndexOf(id);
        if (previous >= 0)
            NotifyItemChanged(previous);
        if (current >= 0)
            NotifyItemChanged(current);
    }

    private int IndexOf(string? id)
    {
        for (var i = 0; i < _items.Count; i++)
            if (_items[i].Id == id)
                return i;
        return -1;
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var holder = new ChoiceHolder(style, numeric);
        // The native hierarchy can retain a row after its holder is recycled.
        // Track the object that actually owns the managed callback.
        _rows.Add(new(holder.Root));
        return holder;
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var cell = (ChoiceHolder)holder;
        var item = _items[position];
        var selected = _selectedId is null ? item.Selected : item.Id == _selectedId;
        cell.Root.Action = item.Select;
        cell.Label.Text = item.Text;
        cell.Label.Typeface = selected ? style.Bold : style.Regular;
        cell.Label.SetTextColor(selected ? style.Primary : style.Text);
        cell.Label.SetTextSize(Android.Util.ComplexUnitType.Sp,
            numeric ? (selected ? 56 : 32) : (selected ? 28 : 22));
        cell.Dot.Visibility = !numeric && selected ? ViewStates.Visible : ViewStates.Gone;
        cell.Root.Selected = selected;
        cell.Root.ContentDescription = item.Text;
        cell.Root.LayoutParameters = new RecyclerView.LayoutParams(-1,
            numeric ? style.Dp(selected ? 96 : 72) : ViewGroup.LayoutParams.WrapContent);
        NativeStyle.Identify(cell.Root, item.Id);
    }

    public override void OnViewRecycled(Java.Lang.Object holder)
    {
        if (holder is ChoiceHolder cell)
            cell.Root.Action = null;
        base.OnViewRecycled(holder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var reference in _rows)
                if (reference.TryGetTarget(out var row))
                    row.Action = null;
            _rows.Clear();
            _items = [];
            _selectedId = null;
        }
        base.Dispose(disposing);
    }

    private sealed class ChoiceHolder : RecyclerView.ViewHolder
    {
        public ChoiceRow Root { get; }
        public TextView Label { get; }
        public TextView Dot { get; }

        public ChoiceHolder(NativeStyle style, bool numeric) : base(new ChoiceRow(style.Context))
        {
            Root = (ChoiceRow)ItemView;
            Root.Orientation = Orientation.Horizontal;
            Root.SetGravity(GravityFlags.CenterVertical);
            Root.SetBackgroundColor(Color.Transparent);
            Root.SetPadding(style.Dp(numeric ? 0 : 24), style.Dp(numeric ? 0 : 18),
                style.Dp(numeric ? 0 : 24), style.Dp(numeric ? 0 : 18));
            Label = style.Label("", 22);
            Label.Gravity = numeric ? GravityFlags.Center : GravityFlags.CenterVertical | GravityFlags.Start;
            Label.ImportantForAccessibility = ImportantForAccessibility.No;
            Root.AddView(Label, new LinearLayout.LayoutParams(0, numeric ? -1 : -2, 1));
            Dot = style.Label("●", 14, color: style.Primary);
            Dot.ImportantForAccessibility = ImportantForAccessibility.No;
            Dot.Gravity = GravityFlags.Center;
            Root.AddView(Dot, new LinearLayout.LayoutParams(-2, -2));
        }
    }

    private sealed class ChoiceRow(Android.Content.Context context) : LinearLayout(context)
    {
        public Action? Action { get; set; }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();
            Clickable = Focusable = true;
            ImportantForAccessibility = ImportantForAccessibility.Yes;
        }

        public override bool PerformClick()
        {
            base.PerformClick();
            if (!Enabled)
                return false;
            Action?.Invoke();
            return true;
        }

        public override void OnInitializeAccessibilityNodeInfo(AccessibilityNodeInfo? info)
        {
            base.OnInitializeAccessibilityNodeInfo(info);
            if (info is null)
                return;
            info.ClassName = "android.widget.Button";
            info.Clickable = true;
            info.Selected = Selected;
        }
    }
}
