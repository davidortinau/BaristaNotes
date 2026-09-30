using Android.Content;
using Android.Graphics;
using Android.Views;
using AndroidX.RecyclerView.Widget;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class SelectorRecyclerView : RecyclerView
{
    private readonly LinearLayoutManager _manager;
    private readonly EndSpacing _spacing;
    private int _pendingCenter = NoPosition;
    private bool _centered;

    public SelectorRecyclerView(Context context) : base(context)
    {
        _manager = new LinearLayoutManager(context);
        SetLayoutManager(_manager);
        SetItemAnimator(null);
        _spacing = new EndSpacing(this);
        AddItemDecoration(_spacing);
        VerticalScrollBarEnabled = true;
    }

    public void CenterOn(int position)
    {
        if (position < 0)
            return;
        _centered = true;
        _pendingCenter = position;
        InvalidateItemDecorations();
        _manager.ScrollToPositionWithOffset(position, 0);
        RequestLayout();
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);
        if (_pendingCenter == NoPosition || Height == 0)
            return;
        var row = _manager.FindViewByPosition(_pendingCenter);
        if (row is null || row.MeasuredHeight == 0)
            return;
        // Use the actual content row, not decorated height: endpoint decoration
        // is just scroll space and must not become part of the selected row.
        var delta = row.Top + row.Height / 2 - Height / 2;
        _pendingCenter = NoPosition;
        if (delta != 0)
            ScrollBy(0, delta);
    }

    protected override void OnSizeChanged(int width, int height, int oldWidth, int oldHeight)
    {
        base.OnSizeChanged(width, height, oldWidth, oldHeight);
        if (height != oldHeight)
            InvalidateItemDecorations();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pendingCenter = NoPosition;
            SetAdapter(null);
            SetLayoutManager(null);
            RemoveItemDecoration(_spacing);
            _manager.Dispose();
            _spacing.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class EndSpacing(SelectorRecyclerView owner) : ItemDecoration
    {
        public override void GetItemOffsets(Rect outRect, View view, RecyclerView parent, State state)
        {
            outRect.Set(0, 0, 0, 0);
            if (!owner._centered)
                return;
            var position = parent.GetChildAdapterPosition(view);
            if (position == 0)
                outRect.Top = parent.Height / 2;
            if (position == state.ItemCount - 1)
                outRect.Bottom = parent.Height / 2;
        }
    }
}
