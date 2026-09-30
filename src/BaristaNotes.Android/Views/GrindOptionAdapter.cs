using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.Core.Services.Workflows;
using System.Globalization;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class GrindOptionAdapter(NativeStyle style) : RecyclerView.Adapter
{
    private GrindPickerState? _state;
    private Action<int>? _select;
    private readonly List<WeakReference<SelectorActionRow>> _rows = [];
    public override int ItemCount => _state?.Values.Count ?? 0;

    public void SetState(GrindPickerState state, Action<int> select)
    {
        _state = state;
        _select = select;
        NotifyDataSetChanged();
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var row = new SelectorActionRow(style.Context);
        row.SetGravity(GravityFlags.Center);
        row.SetBackgroundColor(Color.Transparent);
        var label = style.Label("", 32);
        label.Gravity = GravityFlags.Center;
        label.ImportantForAccessibility = ImportantForAccessibility.No;
        row.AddView(label, new LinearLayout.LayoutParams(-1, -1));
        _rows.Add(new(row));
        return new Holder(row, label);
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var state = _state ?? throw new InvalidOperationException("Grind selector state is unavailable.");
        var cell = (Holder)holder;
        var value = state.Values[position];
        var selected = position == state.SelectedIndex;
        var preferred = state.IsPreferred(value);
        cell.Row.Activate = () => _select?.Invoke(value);
        cell.Row.Selected = selected;
        cell.Label.Text = $"{value} µm";
        cell.Label.Typeface = selected ? style.Bold : style.Regular;
        cell.Label.SetTextSize(Android.Util.ComplexUnitType.Sp, selected ? 56 : preferred ? 32 : 20);
        cell.Label.SetTextColor(selected ? style.Primary : preferred ? style.Text
            : Color.Argb(128, style.Secondary.R, style.Secondary.G, style.Secondary.B));
        cell.Row.ContentDescription = cell.Label.Text;
        cell.Row.LayoutParameters = new RecyclerView.LayoutParams(-1, style.Dp(selected ? 96 : preferred ? 72 : 48));
        NativeStyle.Identify(cell.Row, "GrindValue_" + value.ToString(CultureInfo.InvariantCulture));
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
            _select = null;
            _state = null;
        }
        base.Dispose(disposing);
    }

    private sealed class Holder(SelectorActionRow row, TextView label) : RecyclerView.ViewHolder(row)
    {
        public SelectorActionRow Row { get; } = row;
        public TextView Label { get; } = label;
    }
}
