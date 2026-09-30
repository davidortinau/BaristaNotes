using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class AccessoryOptionAdapter(NativeStyle style) : RecyclerView.Adapter
{
    private IReadOnlyList<EquipmentDto> _items = [];
    private IReadOnlyCollection<int> _selected = Array.Empty<int>();
    private Action<int>? _toggle;
    private readonly List<WeakReference<SelectorActionRow>> _rows = [];
    public override int ItemCount => _items.Count;

    public void SetItems(IReadOnlyList<EquipmentDto> items, IReadOnlyCollection<int> selected, Action<int> toggle)
    {
        _items = items;
        _selected = selected;
        _toggle = toggle;
        NotifyDataSetChanged();
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var row = new SelectorActionRow(style.Context) { Orientation = Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        row.SetPadding(style.Dp(24), style.Dp(16), style.Dp(24), style.Dp(16));
        var indicator = style.Label("", 24);
        indicator.Gravity = GravityFlags.CenterVertical;
        indicator.ImportantForAccessibility = ImportantForAccessibility.No;
        row.AddView(indicator, new LinearLayout.LayoutParams(-2, -2) { RightMargin = style.Dp(16) });
        var label = style.Label("", 22);
        label.ImportantForAccessibility = ImportantForAccessibility.No;
        row.AddView(label, new LinearLayout.LayoutParams(0, -2, 1));
        _rows.Add(new(row));
        return new Holder(row, indicator, label);
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var cell = (Holder)holder;
        var item = _items[position];
        var selected = _selected.Contains(item.Id);
        cell.Row.Activate = () => _toggle?.Invoke(item.Id);
        cell.Row.Selected = selected;
        cell.Row.LayoutParameters = new RecyclerView.LayoutParams(-1, -2);
        cell.Indicator.Text = selected ? "■" : "□";
        cell.Indicator.SetTextColor(selected ? style.Primary : style.Text);
        cell.Label.Text = item.Name;
        cell.Row.ContentDescription = $"{item.Name}, {(selected ? "selected" : "not selected")}";
        NativeStyle.Identify(cell.Row, $"Accessory_{item.Id}");
    }

    public override void OnViewRecycled(Java.Lang.Object holder)
    {
        if (holder is Holder cell)
            cell.Row.Activate = null;
        base.OnViewRecycled(holder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var reference in _rows)
                if (reference.TryGetTarget(out var row))
                    row.Activate = null;
            _rows.Clear();
            _items = [];
            _selected = Array.Empty<int>();
            _toggle = null;
        }
        base.Dispose(disposing);
    }

    private sealed class Holder(SelectorActionRow row, TextView indicator, TextView label) : RecyclerView.ViewHolder(row)
    {
        public SelectorActionRow Row { get; } = row;
        public TextView Indicator { get; } = indicator;
        public TextView Label { get; } = label;
    }
}
