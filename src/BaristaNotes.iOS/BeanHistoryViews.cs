using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Foundation;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class BeanRatingView : UIView
{
    private readonly UILabel _average;
    private readonly UILabel _count;
    private readonly List<(UILabel Icon, UIView Background, UIView Fill, UILabel Count, double Percentage)> _bars = [];
    public BeanRatingView(RatingAggregateDto rating, UITraitCollection traits)
    {
        _average = BeanBagUi.Text(rating.FormattedAverage, 36, false, NativeTheme.TextPrimary, traits);
        _count = BeanBagUi.Text($"{rating.TotalShots} shots", 14, false, NativeTheme.Secondary, traits);
        _average.TextAlignment = _count.TextAlignment = UITextAlignment.Center;
        AddSubviews(_average, _count);
        for (var value = 4; value >= 0; value--)
        {
            var icon = new UILabel { Text = BeanDisplay.RatingGlyph(value), TextColor = NativeTheme.Secondary,
                Font = UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.Icons(20), traits), TextAlignment = UITextAlignment.Center };
            var background = new UIView { BackgroundColor = EquipmentColors.SurfaceVariant };
            var fill = new UIView { BackgroundColor = NativeTheme.Primary };
            var count = BeanBagUi.Text(rating.GetCountForRating(value).ToString(), 14, false, NativeTheme.Secondary, traits);
            count.TextAlignment = UITextAlignment.Right;
            _bars.Add((icon, background, fill, count, rating.GetPercentageForRating(value)));
            AddSubviews(icon, background, fill, count);
        }
        AccessibilityLabel = $"{rating.FormattedAverage}, {rating.TotalShots} shots";
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width,
        SliceUi.Measure(_average, size.Width).Height + 4 + SliceUi.Measure(_count, size.Width).Height + 16
        + _bars.Sum(bar => (double)Math.Max(20, Math.Max(bar.Icon.Font.LineHeight, bar.Count.Font.LineHeight))) + 32);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var average = SliceUi.Measure(_average, Bounds.Width).Height;
        var count = SliceUi.Measure(_count, Bounds.Width).Height;
        _average.Frame = new CGRect(0, 0, Bounds.Width, average);
        _count.Frame = new CGRect(0, average + 4, Bounds.Width, count);
        nfloat y = average + 4 + count + 16;
        foreach (var bar in _bars)
        {
            var height = (nfloat)Math.Max(20, Math.Max(bar.Icon.Font.LineHeight, bar.Count.Font.LineHeight));
            bar.Icon.Frame = new CGRect(0, y, 30, height);
            bar.Background.Frame = new CGRect(38, y + (height - 20) / 2, 200, 20);
            bar.Fill.Frame = new CGRect(38, y + (height - 20) / 2, (nfloat)(bar.Percentage * 2), 20);
            bar.Count.Frame = new CGRect(246, y, 30, height);
            y += height + 8;
        }
    }
}

internal sealed class BeanShotCard : UIView
{
    private readonly UIView _card = new();
    private readonly UILabel _icon = new();
    private readonly UILabel _drink = new();
    private readonly UILabel _rating = new();
    private readonly UILabel _method = new();
    private readonly UILabel _bean = new() { Lines = 0 };
    private readonly UILabel _details = new() { Lines = 0 };
    private readonly UILabel _people = new() { Lines = 0 };
    private readonly UIButton _action = new(UIButtonType.Custom);
    private Action? _activate;

    public BeanShotCard()
    {
        BackgroundColor = EquipmentColors.SurfaceVariant;
        _card.BackgroundColor = NativeTheme.Surface;
        _card.Layer.BorderWidth = 1;
        _card.Layer.BorderColor = NativeTheme.Outline.CGColor;
        _card.Layer.CornerRadius = 8;
        _method.Layer.BorderColor = NativeTheme.Primary.CGColor;
        _method.Layer.BorderWidth = 1;
        _method.Layer.CornerRadius = 8;
        _method.TextAlignment = UITextAlignment.Center;
        _method.ClipsToBounds = true;
        var tap = WeakUiCallback.Create(this, static card => card._activate?.Invoke());
        _action.TouchUpInside += (_, _) => tap();
        _card.AddSubviews(_icon, _drink, _rating, _method, _bean, _details, _people);
        AddSubviews(_card, _action);
    }
    public void Bind(ShotRecordDto shot, UITraitCollection traits, Action? activate)
    {
        _activate = activate;
        _action.AccessibilityIdentifier = $"bean.shot.{shot.Id}";
        _action.AccessibilityLabel = $"{shot.DrinkType}, {shot.Bean?.Name ?? "Unknown Bean"}, {shot.DoseIn:F1}g in";
        _icon.Text = "\uefef";
        _icon.Font = UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.Icons(18), traits);
        _icon.TextColor = NativeTheme.TextPrimary;
        _drink.Text = shot.DrinkType;
        _drink.Font = SourceScaledText.Font(18, true, traits);
        _drink.TextColor = NativeTheme.TextPrimary;
        _rating.Text = BeanDisplay.RatingGlyph(shot.Rating ?? 0);
        _rating.Font = UIFontMetrics.DefaultMetrics.GetScaledFont(NativeTheme.Icons(24), traits);
        _rating.TextColor = NativeTheme.Primary;
        _method.Hidden = shot.BrewMethod == BrewMethod.Espresso;
        _method.Text = shot.BrewMethod.DisplayName();
        _method.Font = SourceScaledText.Font(10, false, traits);
        _method.TextColor = NativeTheme.Primary;
        _bean.Text = shot.Bean?.Name ?? "Unknown Bean";
        _bean.Font = SourceScaledText.Font(16, false, traits);
        _bean.TextColor = NativeTheme.TextPrimary;
        _details.Text = BeanDisplay.ShotRecipe(shot);
        _details.Font = SourceScaledText.Font(14, false, traits);
        _details.TextColor = UIColor.FromRGB(128, 128, 128);
        _people.Text = BeanDisplay.ShotPeople(shot, DateTimeOffset.Now) ?? "";
        _people.Hidden = string.IsNullOrEmpty(_people.Text);
        _people.Font = SourceScaledText.Font(12, false, traits);
        _people.TextColor = UIColor.FromRGB(128, 128, 128);
        SetNeedsLayout();
    }
    private nfloat HeaderHeight => (nfloat)Math.Max(_icon.Font.LineHeight, Math.Max(_drink.Font.LineHeight, _rating.Font.LineHeight));
    public override CGSize SizeThatFits(CGSize size)
    {
        var width = size.Width - 26;
        return new CGSize(size.Width, 26 + HeaderHeight + 8 + SliceUi.Measure(_bean, width).Height + 8
            + SliceUi.Measure(_details, width).Height + (_people.Hidden ? 0 : 8 + SliceUi.Measure(_people, width).Height));
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _card.Frame = Bounds;
        _card.Layer.BorderColor = NativeTheme.Outline.GetResolvedColor(TraitCollection).CGColor;
        _action.Frame = Bounds;
        var width = Bounds.Width - 26;
        var icon = SliceUi.Measure(_icon, width);
        var rating = SliceUi.Measure(_rating, width);
        var drink = SliceUi.Measure(_drink, width - icon.Width - rating.Width - 8);
        _icon.Frame = new CGRect(13, 13, icon.Width, HeaderHeight);
        _drink.Frame = new CGRect(17 + icon.Width, 13, drink.Width, HeaderHeight);
        _rating.Frame = new CGRect(Bounds.Width - 13 - rating.Width, 13, rating.Width, HeaderHeight);
        if (!_method.Hidden)
        {
            var method = SliceUi.Measure(_method, width);
            // Source Label padding is inside a one-point Border on each edge.
            _method.Frame = new CGRect(_drink.Frame.Right + 10, 13 + (HeaderHeight - method.Height - 6) / 2, method.Width + 14, method.Height + 6);
        }
        var y = 13 + HeaderHeight + 8;
        foreach (var label in new[] { _bean, _details, _people }.Where(label => !label.Hidden))
        {
            var height = SliceUi.Measure(label, width).Height;
            label.Frame = new CGRect(13, y, width, height);
            y += height + 8;
        }
    }
}

internal sealed class BeanHistoryList : UICollectionView
{
    private readonly HistorySource _source;
    private readonly HistoryLayout _layout;
    private readonly Action _more;
    private nfloat _width;
    public BeanHistoryList(Action<int> open, Action more)
        : base(CGRect.Empty, new UICollectionViewFlowLayout { MinimumLineSpacing = 0, MinimumInteritemSpacing = 0 })
    {
        AccessibilityIdentifier = "bean.history";
        _more = more;
        BackgroundColor = NativeTheme.Surface;
        ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
        _source = new HistorySource(open);
        _layout = new HistoryLayout(_source, more);
        DataSource = _source;
        Delegate = _layout;
        RegisterClassForCell(typeof(HistoryCell), "bean-history");
    }
    public void Update(IReadOnlyList<ShotRecordDto> shots, UITraitCollection traits, bool append)
    {
        var oldCount = _source.Shots.Count;
        _source.Shots = shots;
        _source.Traits = traits;
        if (append && shots.Count > oldCount)
            InsertItems(Enumerable.Range(oldCount, shots.Count - oldCount).Select(index => NSIndexPath.FromItemSection(index, 0)).ToArray());
        else ReloadData();
        CollectionViewLayout.InvalidateLayout();
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, 420);
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        if (_width != Bounds.Width)
        {
            _width = Bounds.Width;
            CollectionViewLayout.InvalidateLayout();
        }
        // Unlike a drag's transient bounce, an idle out-of-domain offset hides
        // all rows. Keep programmatic/restored offsets within actual content.
        if (!Dragging && !Decelerating && Bounds.Height > 0)
        {
            var maximum = (nfloat)Math.Max(0, ContentSize.Height - Bounds.Height);
            var y = (nfloat)Math.Clamp(ContentOffset.Y, 0, maximum);
            if (y != ContentOffset.Y) SetContentOffset(new CGPoint(ContentOffset.X, y), false);
        }
        var visible = IndexPathsForVisibleItems;
        if (visible.Length > 0 && visible.Max(path => path.Item) >= _source.Shots.Count - 5)
            _more();
    }
    private sealed class HistorySource(Action<int> open) : UICollectionViewDataSource
    {
        public IReadOnlyList<ShotRecordDto> Shots { get; set; } = [];
        public UITraitCollection Traits { get; set; } = new();
        public override nint GetItemsCount(UICollectionView collectionView, nint section) => Shots.Count;
        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (HistoryCell)collectionView.DequeueReusableCell("bean-history", indexPath);
            var shot = Shots[(int)indexPath.Item];
            cell.Card.Bind(shot, Traits, () => open(shot.Id));
            return cell;
        }
    }
    private sealed class HistoryLayout(HistorySource source, Action more) : UICollectionViewDelegateFlowLayout
    {
        private readonly BeanShotCard _measure = new();
        public override CGSize GetSizeForItem(UICollectionView collectionView, UICollectionViewLayout layout, NSIndexPath indexPath)
        {
            _measure.Bind(source.Shots[(int)indexPath.Item], source.Traits, null);
            var size = _measure.SizeThatFits(new CGSize(collectionView.Bounds.Width, nfloat.MaxValue));
            return new CGSize(size.Width, size.Height + 8);
        }
        public override void WillDisplayCell(UICollectionView collectionView, UICollectionViewCell cell, NSIndexPath indexPath)
        {
            if (indexPath.Item >= source.Shots.Count - 5) more();
        }
    }
    [Register("NativeBeanHistoryCell")]
    private sealed class HistoryCell : UICollectionViewCell
    {
        public BeanShotCard Card { get; } = new();
        public HistoryCell(ObjCRuntime.NativeHandle handle) : base(handle) => ContentView.AddSubview(Card);
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            Card.Frame = new CGRect(0, 4, Bounds.Width, Bounds.Height - 8);
        }
    }
}
