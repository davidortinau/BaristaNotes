using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class BeanHistoryAdapter(NativeStyle style, Action<int> edit) : RecyclerView.Adapter
{
    private IReadOnlyList<ShotRecordDto> _shots = [];
    private Action<int>? _edit = edit;
    private readonly List<WeakReference<SelectorActionRow>> _rows = [];
    public override int ItemCount => _shots.Count;
    public void SetItems(IReadOnlyList<ShotRecordDto> shots) { _shots = shots; NotifyDataSetChanged(); }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var root = new SelectorActionRow(style.Context) { Orientation = Orientation.Vertical };
        root.SetBackgroundColor(style.SurfaceVariant);
        var card = style.Column();
        card.Background = style.Rounded(style.Surface, 8, style.Outline);
        card.SetPadding(style.Dp(12), style.Dp(12), style.Dp(12), style.Dp(12));
        card.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        root.AddView(card, new LinearLayout.LayoutParams(-1, -2));
        var header = new FrameLayout(style.Context);
        var headline = style.Row();
        headline.SetGravity(GravityFlags.CenterVertical);
        var coffee = style.Label("\uefef", 18);
        coffee.Typeface = style.Symbols;
        headline.AddView(coffee, new LinearLayout.LayoutParams(-2, -2) { RightMargin = style.Dp(4) });
        var drink = style.Label("", 18, true);
        headline.AddView(drink);
        var method = style.Label("", 10, color: style.Primary);
        method.SetPadding(style.Dp(6), style.Dp(2), style.Dp(6), style.Dp(2));
        method.Background = style.Rounded(Color.Transparent, 8, style.Primary);
        headline.AddView(method, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = style.Dp(10) });
        header.AddView(headline, new FrameLayout.LayoutParams(-1, -2));
        var rating = style.Label("", 24, color: style.Primary);
        rating.Typeface = style.Symbols;
        header.AddView(rating, new FrameLayout.LayoutParams(-2, -2, GravityFlags.End));
        card.AddView(header);
        var bean = style.Label("", 16);
        card.AddView(bean, new LinearLayout.LayoutParams(-1, -2) { TopMargin = style.Dp(8) });
        var recipe = style.Label("", 14, color: Color.Gray);
        card.AddView(recipe, new LinearLayout.LayoutParams(-1, -2) { TopMargin = style.Dp(8) });
        var people = style.Label("", 12, color: Color.Gray);
        card.AddView(people, new LinearLayout.LayoutParams(-1, -2) { TopMargin = style.Dp(8) });
        _rows.Add(new(root));
        return new Holder(root, drink, method, rating, bean, recipe, people);
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var cell = (Holder)holder;
        var shot = _shots[position];
        cell.Root.Activate = () => _edit?.Invoke(shot.Id);
        cell.Root.LayoutParameters = new RecyclerView.LayoutParams(-1, -2)
        {
            TopMargin = style.Dp(4), BottomMargin = style.Dp(4)
        };
        cell.Drink.Text = shot.DrinkType;
        cell.Method.Text = shot.BrewMethod.DisplayName();
        cell.Method.Visibility = shot.BrewMethod == BrewMethod.Espresso ? ViewStates.Gone : ViewStates.Visible;
        cell.Rating.Text = BeanDisplay.RatingGlyph(shot.Rating ?? 0);
        cell.Bean.Text = shot.Bean?.Name ?? "Unknown Bean";
        cell.Recipe.Text = BeanDisplay.ShotRecipe(shot);
        cell.People.Text = BeanDisplay.ShotPeople(shot, DateTimeOffset.Now) ?? "";
        cell.People.Visibility = string.IsNullOrEmpty(cell.People.Text) ? ViewStates.Gone : ViewStates.Visible;
        cell.Root.ContentDescription = BeanHistoryAccessibility.Describe(
            cell.Drink.Text ?? "",
            cell.Method.Visibility == ViewStates.Visible ? cell.Method.Text : null,
            shot.Rating ?? 0, cell.Bean.Text ?? "", cell.Recipe.Text ?? "",
            cell.People.Visibility == ViewStates.Visible ? cell.People.Text : null);
        NativeStyle.Identify(cell.Root, $"BeanShot_{shot.Id}");
    }

    public override void OnViewRecycled(Java.Lang.Object holder)
    {
        if (holder is Holder cell) cell.Root.Activate = null;
        base.OnViewRecycled(holder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var reference in _rows)
                if (reference.TryGetTarget(out var row)) row.Activate = null;
            _rows.Clear();
            _shots = [];
            _edit = null;
        }
        base.Dispose(disposing);
    }

    private sealed class Holder(SelectorActionRow root, TextView drink, TextView method, TextView rating,
        TextView bean, TextView recipe, TextView people) : RecyclerView.ViewHolder(root)
    {
        public SelectorActionRow Root { get; } = root;
        public TextView Drink { get; } = drink;
        public TextView Method { get; } = method;
        public TextView Rating { get; } = rating;
        public TextView Bean { get; } = bean;
        public TextView Recipe { get; } = recipe;
        public TextView People { get; } = people;
    }
}
