using Android.Text;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class ShotAdapter : RecyclerView.Adapter
{
    private readonly NativeStyle _style;
    private readonly Action<int> _openShot;
    private IReadOnlyList<ShotRecordDto> _items = [];
    private readonly List<WeakReference<ShotCell>> _cells = [];
    public override int ItemCount => _items.Count;
    public override long GetItemId(int position) => _items[position].Id;

    public ShotAdapter(NativeStyle style, Action<int> openShot)
    {
        _style = style;
        _openShot = openShot;
        HasStableIds = true;
    }

    public void SetItems(IReadOnlyList<ShotRecordDto> items)
    {
        _items = items;
        NotifyDataSetChanged();
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var cell = new ShotCell(_style);
        cell.LayoutParameters = new RecyclerView.LayoutParams(-1, -2)
        {
            BottomMargin = _style.Dp(1)
        };
        _cells.Add(new(cell));
        return new ShotHolder(cell);
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var cell = (ShotCell)holder.ItemView;
        var shot = _items[position];
        var text = DrinkDisplay.ActivityRow(shot);
        cell.Title.Text = text.Title;
        cell.Ratio.Text = text.Ratio;
        cell.Subtitle.Text = text.Subtitle;
        cell.Result.Text = text.Result;
        cell.Open = () => _openShot(shot.Id);
        cell.ContentDescription = $"{text.Title}, {text.Ratio}, {text.Subtitle}, {text.Result}";
        NativeStyle.Identify(cell, $"Shot_{shot.Id}");
    }

    public override void OnViewRecycled(Java.Lang.Object holder)
    {
        if (holder is ShotHolder row)
            ((ShotCell)row.ItemView).Open = null;
        base.OnViewRecycled(holder);
    }

    private sealed class ShotHolder(View view) : RecyclerView.ViewHolder(view);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var reference in _cells)
                if (reference.TryGetTarget(out var cell))
                    cell.Open = null;
            _cells.Clear();
            _items = [];
        }
        base.Dispose(disposing);
    }

    private sealed class ShotCell : LinearLayout
    {
        public TextView Title { get; }
        public TextView Ratio { get; }
        public TextView Subtitle { get; }
        public TextView Result { get; }
        public Action? Open { get; set; }

        public ShotCell(NativeStyle style) : base(style.Context)
        {
            Orientation = Orientation.Vertical;
            Clickable = Focusable = true;
            SetBackgroundColor(style.Surface);
            SetPadding(style.Dp(16), style.Dp(14), style.Dp(16), style.Dp(14));
            Title = style.Label("", 22, true);
            Ratio = style.Label("", 18, true);
            Subtitle = style.Label("", 13, color: style.Secondary);
            Result = style.Label("", 13, color: style.Secondary);
            foreach (var label in new[] { Title, Subtitle })
            {
                label.SetSingleLine(true);
                label.Ellipsize = TextUtils.TruncateAt.End;
            }
            var top = style.Row();
            top.AddView(Title, new LinearLayout.LayoutParams(0, -2, 1));
            top.AddView(Ratio);
            var bottom = style.Row();
            bottom.SetPadding(0, style.Dp(4), 0, 0);
            bottom.AddView(Subtitle, new LinearLayout.LayoutParams(0, -2, 1));
            bottom.AddView(Result);
            AddView(top);
            AddView(bottom);
        }

        public override bool PerformClick()
        {
            base.PerformClick();
            Open?.Invoke();
            return true;
        }
    }
}
