using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class BeanListAdapter(NativeStyle style, Action<int> edit, FrameLayout title,
    FrameLayout tail) : RecyclerView.Adapter
{
    private IReadOnlyList<BeanDto> _beans = [];
    private Action<int>? _edit = edit;
    private readonly List<Holder> _holders = [];
    private readonly List<PageHolder> _pageHolders = [];
    public override int ItemCount => _beans.Count + 3;
    public int HeroHeight { get; private set; }
    private int _rowHeight;
    private int _titleHeight;
    private int _titleInset;
    private int _viewportHeight;
    private int _stateHeight;
    private int _tailHeight;
    private readonly RangeTile _measure = CreateTile(style);

    public void SetViewport(int width, int height, int heroHeight, int titleHeight, int stateHeight)
    {
        HeroHeight = heroHeight;
        _measure.Measure(View.MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly),
            View.MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
        _rowHeight = _measure.MeasuredHeight + style.Dp(1);
        _titleHeight = titleHeight;
        _viewportHeight = height;
        _stateHeight = stateHeight;
        UpdatePageHeights();
    }

    public void SetTitleInset(int inset)
    {
        if (_titleInset == inset) return;
        _titleInset = inset;
        UpdatePageHeights();
    }

    private void UpdatePageHeights()
    {
        _tailHeight = Math.Max(Math.Max(1, _stateHeight),
            _viewportHeight - _titleHeight - _titleInset - _beans.Count * _rowHeight);
        foreach (var holder in _pageHolders) SetPageHeight(holder);
    }

    public void SetItems(IReadOnlyList<BeanDto> beans)
    {
        _beans = beans;
        NotifyDataSetChanged();
    }

    public override int GetItemViewType(int position) => position == 0 ? 1 : position == 1 ? 2 :
        position == ItemCount - 1 ? 3 : 0;
    public override long GetItemId(int position) => position == 0 ? long.MinValue :
        position == 1 ? long.MinValue + 1 : position == ItemCount - 1 ? long.MinValue + 2 : _beans[position - 2].Id;

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        if (viewType != 0)
        {
            var page = new PageHolder(new FrameLayout(style.Context), viewType);
            _pageHolders.Add(page);
            SetPageHeight(page);
            return page;
        }
        var tile = CreateTile(style);
        tile.LayoutParameters = new RecyclerView.LayoutParams(-1, -2) { BottomMargin = style.Dp(1) };
        var holder = new Holder(tile, style);
        _holders.Add(holder);
        return holder;
    }

    private void SetPageHeight(PageHolder holder)
    {
        var height = holder.PageType == 1 ? HeroHeight :
            holder.PageType == 2 ? _titleHeight + _titleInset : _tailHeight;
        var parameters = holder.Row.LayoutParameters as RecyclerView.LayoutParams;
        if (parameters == null)
        {
            holder.Row.LayoutParameters = new RecyclerView.LayoutParams(-1, height);
        }
        else if (parameters.Width != -1 || parameters.Height != height)
        {
            // RecyclerView stores the owning holder in these parameters; retain the instance.
            parameters.Width = -1;
            parameters.Height = height;
            holder.Row.LayoutParameters = parameters;
        }
    }

    private static RangeTile CreateTile(NativeStyle style)
    {
        var tile = new RangeTile(style);
        tile.Caption.SetTextSize(Android.Util.ComplexUnitType.Sp, 10);
        tile.Caption.LetterSpacing = 2 * .0624f;
        tile.Value.SetTextSize(Android.Util.ComplexUnitType.Sp, 20);
        tile.Value.Typeface = style.Bold;
        tile.Glyph.Text = "\ue5cc";
        tile.Glyph.SetTextSize(Android.Util.ComplexUnitType.Dip, 24);
        tile.Caption.Text = "BEAN";
        tile.Value.Text = "Bean";
        return tile;
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        if (holder is PageHolder page)
        {
            if (page.PageType != 1) page.Bind(page.PageType == 2 ? title : tail);
            return;
        }
        if (holder is not Holder cell) return;
        var bean = _beans[position - 2];
        cell.Activate = () => _edit?.Invoke(bean.Id);
        cell.Tile.Caption.Text = bean.IsActive ? BeanDisplay.Subtitle(bean) : $"ARCHIVED - {BeanDisplay.Subtitle(bean)}";
        cell.Tile.Value.Text = bean.Name;
        cell.Tile.SetInteraction(true, $"{cell.Tile.Caption.Text}: {bean.Name}");
        cell.RefreshFocus();
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
            _pageHolders.Clear();
            _beans = [];
            _edit = null;
            _measure.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class PageHolder(FrameLayout row, int pageType) : RecyclerView.ViewHolder(row)
    {
        public FrameLayout Row { get; } = row;
        public int PageType { get; } = pageType;

        public void Bind(View content)
        {
            if (ReferenceEquals(content.Parent, Row)) return;
            (content.Parent as FrameLayout)?.RemoveView(content);
            Row.AddView(content, new FrameLayout.LayoutParams(-1, -1));
        }
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
