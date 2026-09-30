using Android.Views;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class BeanListAdapter(NativeStyle style, Action<int> edit) : RecyclerView.Adapter
{
    private IReadOnlyList<BeanDto> _beans = [];
    private Action<int>? _edit = edit;
    private readonly List<Holder> _holders = [];
    public override int ItemCount => _beans.Count;

    public void SetItems(IReadOnlyList<BeanDto> beans) { _beans = beans; NotifyDataSetChanged(); }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var tile = new RangeTile(style);
        tile.Caption.SetTextSize(Android.Util.ComplexUnitType.Sp, 10);
        tile.Caption.LetterSpacing = 2 * .0624f;
        tile.Value.SetTextSize(Android.Util.ComplexUnitType.Sp, 20);
        tile.Value.Typeface = style.Bold;
        tile.Glyph.Text = "\ue5cc";
        tile.Glyph.SetTextSize(Android.Util.ComplexUnitType.Dip, 24);
        var holder = new Holder(tile, style);
        _holders.Add(holder);
        return holder;
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var cell = (Holder)holder;
        var bean = _beans[position];
        cell.Activate = () => _edit?.Invoke(bean.Id);
        cell.Tile.Caption.Text = BeanDisplay.Subtitle(bean);
        cell.Tile.Value.Text = bean.Name;
        cell.Tile.SetInteraction(true, $"{cell.Tile.Caption.Text}: {bean.Name}");
        cell.RefreshFocus();
        cell.Tile.LayoutParameters = new RecyclerView.LayoutParams(-1, -2) { BottomMargin = style.Dp(1) };
        NativeStyle.Identify(cell.Tile.ActionButton, $"Bean_{bean.Id}");
    }

    public override void OnViewRecycled(Java.Lang.Object holder)
    {
        if (holder is Holder cell) cell.Activate = null;
        base.OnViewRecycled(holder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var holder in _holders) holder.Release();
            _holders.Clear();
            _beans = [];
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
        public void Release()
        {
            Activate = null;
            Tile.ActionButton.Click -= _click;
            Tile.ActionButton.FocusChange -= _focus;
        }
    }
}
