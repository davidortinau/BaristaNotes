using Android.Views;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class EquipmentListAdapter(NativeStyle style, Action<int> edit) : RecyclerView.Adapter
{
    private IReadOnlyList<EquipmentDto> _items = [];
    private readonly List<Holder> _holders = [];
    private Action<int>? _edit = edit;
    public override int ItemCount => _items.Count;

    public void SetItems(IReadOnlyList<EquipmentDto> items)
    {
        _items = items;
        NotifyDataSetChanged();
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var tile = new RangeTile(style);
        tile.Caption.SetTextSize(Android.Util.ComplexUnitType.Sp, 10);
        tile.Caption.LetterSpacing = 2 * .0624f;
        tile.Value.SetTextSize(Android.Util.ComplexUnitType.Sp, 20);
        tile.Value.Typeface = style.Bold;
        tile.Glyph.Text = "\ue5cc";
        // AdaptiveTwoLineTile opts only its decorative glyph out of font scaling.
        tile.Glyph.SetTextSize(Android.Util.ComplexUnitType.Dip, 24);
        var holder = new Holder(tile, style);
        _holders.Add(holder);
        return holder;
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var cell = (Holder)holder;
        var item = _items[position];
        cell.Activate = () => _edit?.Invoke(item.Id);
        cell.Tile.Caption.Text = item.Type.ToString().ToUpperInvariant();
        cell.Tile.Value.Text = item.Name;
        cell.Tile.LayoutParameters = new RecyclerView.LayoutParams(-1, -2) { BottomMargin = style.Dp(1) };
        cell.Tile.SetInteraction(true, $"{item.Type}: {item.Name}");
        cell.RefreshFocus();
        NativeStyle.Identify(cell.Tile, $"Equipment_{item.Id}Row");
        NativeStyle.Identify(cell.Tile.ActionButton, $"Equipment_{item.Id}");
    }

    public override void OnViewRecycled(Java.Lang.Object holder)
    {
        if (holder is Holder cell)
            cell.Activate = null;
        base.OnViewRecycled(holder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var holder in _holders)
                holder.ReleaseCallbacks();
            _holders.Clear();
            _items = [];
            _edit = null;
        }
        base.Dispose(disposing);
    }

    private sealed class Holder : RecyclerView.ViewHolder
    {
        private readonly EventHandler _click;
        private readonly EventHandler<View.FocusChangeEventArgs> _focus;
        private readonly NativeStyle _style;
        public RangeTile Tile { get; }
        public Action? Activate { get; set; }

        public Holder(RangeTile tile, NativeStyle style) : base(tile)
        {
            Tile = tile;
            _style = style;
            _click = (_, _) => Activate?.Invoke();
            _focus = (_, _) => RefreshFocus();
            tile.ActionButton.Click += _click;
            tile.ActionButton.FocusChange += _focus;
        }

        public void RefreshFocus() => Tile.Background = _style.Rounded(_style.Surface, 0,
            Tile.ActionButton.HasFocus ? _style.IsDark ? _style.Text : _style.Primary : _style.Surface);

        public void ReleaseCallbacks()
        {
            Activate = null;
            Tile.ActionButton.Click -= _click;
            Tile.ActionButton.FocusChange -= _focus;
        }
    }
}
