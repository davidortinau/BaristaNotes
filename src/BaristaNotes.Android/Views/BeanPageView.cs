using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class BeanPageView : FrameLayout
{
    private readonly NativeStyle _style;
    private readonly BeanMapPanel _map;
    private readonly FrameLayout _mapHost;
    private readonly View _title;
    private readonly View _status;
    private readonly FrameLayout _titleRow;
    private readonly FrameLayout _tail;
    private readonly FrameLayout _pinned;
    private readonly PageScroll _scroll;
    private readonly Action<bool> _pinChanged;
    private (int Width, int Height, int Hero, int Title, int Beans, int State, int Top) _metrics;
    private int _safeTop;
    private bool _isPinned;
    private int _beanCount;
    private bool _disposed;
    public SelectorRecyclerView List { get; }
    public BeanListAdapter Adapter { get; }

    public BeanPageView(NativeStyle style, BeanMapPanel map, View title, View status, Action<int> edit,
        Action<bool> pinChanged) : base(style.Context)
    {
        _style = style;
        _map = map;
        _title = title;
        _status = status;
        _pinChanged = pinChanged;
        SetBackgroundColor(style.Surface);
        SetClipChildren(true);
        _mapHost = new FrameLayout(style.Context);
        _mapHost.SetClipChildren(true);
        _mapHost.AddView(map, new LayoutParams(-1, -1));
        _titleRow = new FrameLayout(style.Context);
        _titleRow.AddView(title, new LayoutParams(-1, -1));
        _tail = new FrameLayout(style.Context);
        _tail.AddView(status, new LayoutParams(-1, -1));
        _pinned = new FrameLayout(style.Context) { Visibility = ViewStates.Invisible };
        _pinned.SetBackgroundColor(style.Surface);
        List = new SelectorRecyclerView(style.Context);
        List.SetBackgroundColor(style.Outline);
        NativeStyle.Identify(List, "BeanList");
        Adapter = new BeanListAdapter(style, edit, _titleRow, _tail);
        Adapter.HasStableIds = true;
        List.SetAdapter(Adapter);
        AddView(List, new LayoutParams(-1, -1));
        AddView(_mapHost, new LayoutParams(-1, 0));
        AddView(_pinned, new LayoutParams(-1, -2));
        _mapHost.Touch += ForwardMapTouch;
        _pinned.Touch += ForwardTitleTouch;
        _scroll = new PageScroll(this);
        List.AddOnScrollListener(_scroll);
    }

    public void SetItems(IReadOnlyList<BeanDto> beans)
    {
        _beanCount = beans.Count;
        Adapter.SetItems(beans);
        RequestLayout();
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        var width = right - left;
        var height = bottom - top;
        _title.Measure(MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly),
            MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
        var titleHeight = _title.MeasuredHeight;
        _status.Measure(MeasureSpec.MakeMeasureSpec(width, MeasureSpecMode.Exactly),
            MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified));
        var stateHeight = _status.Visibility == ViewStates.Visible ? _status.MeasuredHeight : 0;
        var root = RootView ?? this;
        _safeTop = WindowEdgeInsets.Read(root).Top;
        var previousTitle = Math.Max(_style.Dp(120), titleHeight - _style.Dp(BeanPageGeometry.SeparatorHeight));
        var previousHero = previousTitle - _safeTop + Math.Min(_style.Dp(240), (int)(root.Height * .38));
        var heroHeight = (int)BeanPageGeometry.ExpandedMapHeight(previousHero,
            _safeTop + _style.Dp(1), _map.MeasureFormerFooter(width));
        var metrics = (width, height, heroHeight, titleHeight, _beanCount, stateHeight, _safeTop);
        if (width > 0 && height > 0 && _metrics != metrics)
        {
            _metrics = metrics;
            Adapter.SetViewport(width, height, heroHeight, titleHeight, stateHeight);
            _mapHost.LayoutParameters = new LayoutParams(-1, heroHeight);
            _pinned.LayoutParameters = new LayoutParams(-1, titleHeight + _safeTop);
        }
        base.OnLayout(changed, left, top, right, bottom);
        UpdateScroll();
    }

    private void UpdateScroll()
    {
        if (List.GetLayoutManager() is not LinearLayoutManager manager) return;
        var hero = manager.FindViewByPosition(0);
        var offset = hero == null ? Adapter.HeroHeight : Math.Clamp(-hero.Top, 0, Adapter.HeroHeight);
        // Keep the hardware surface attached; only the recycler's empty hero is recycled.
        _mapHost.TranslationY = -offset;
        _map.SetParallax(offset);
        var title = manager.FindViewByPosition(1);
        var pin = title == null ? manager.FindFirstVisibleItemPosition() > 1 :
            BeanPageGeometry.TitleTop(title.Top, 0, _safeTop) == _safeTop;
        // Reserve only the overlap with the pinned safe area, not an expanded-header gap.
        Adapter.SetTitleInset(title == null ? pin ? _safeTop : 0 :
            (int)BeanPageGeometry.PinnedTitleInset(title.Top, _safeTop));
        var parent = pin ? _pinned : _titleRow;
        if (!ReferenceEquals(_title.Parent, parent))
        {
            (_title.Parent as ViewGroup)?.RemoveView(_title);
            parent.AddView(_title, new LayoutParams(-1, _metrics.Title) { TopMargin = pin ? _safeTop : 0 });
        }
        else if (_title.LayoutParameters is LayoutParams parameters &&
            (parameters.TopMargin != (pin ? _safeTop : 0) || parameters.Height != _metrics.Title))
        {
            parameters.TopMargin = pin ? _safeTop : 0;
            parameters.Height = _metrics.Title;
            _title.LayoutParameters = parameters;
        }
        _pinned.Visibility = pin ? ViewStates.Visible : ViewStates.Invisible;
        if (_isPinned != pin)
        {
            _isPinned = pin;
            _pinChanged(pin);
        }
    }

    private void ForwardTitleTouch(object? sender, View.TouchEventArgs args)
    {
        if (args.Event == null) return;
        List.DispatchTouchEvent(args.Event);
        args.Handled = true;
    }

    private void ForwardMapTouch(object? sender, View.TouchEventArgs args)
    {
        if (args.Event == null) return;
        using var touch = MotionEvent.Obtain(args.Event);
        touch.OffsetLocation(0, _mapHost.TranslationY);
        List.DispatchTouchEvent(touch);
        touch.Recycle();
        args.Handled = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _mapHost.Touch -= ForwardMapTouch;
            _pinned.Touch -= ForwardTitleTouch;
            List.RemoveOnScrollListener(_scroll);
            _scroll.Dispose();
            List.Dispose();
            Adapter.Dispose();
            _map.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class PageScroll(BeanPageView owner) : RecyclerView.OnScrollListener
    {
        public override void OnScrolled(RecyclerView recyclerView, int dx, int dy) => owner.UpdateScroll();
    }
}
