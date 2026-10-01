using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class ActivityViewController : SliceViewController
{
    private readonly DrinkTile _header = new("ACTIVITY", "activity.header", () => { }, valueFontSize: 28);
    private readonly UICollectionView _table = new(CGRect.Empty, new UICollectionViewFlowLayout
    {
        MinimumLineSpacing = 0, MinimumInteritemSpacing = 0, ScrollDirection = UICollectionViewScrollDirection.Vertical
    });
    private readonly UILabel _state = SliceUi.Label("Loading…", 18);
    private readonly UIView _empty = new();
    private readonly UILabel _emptyTitle = new();
    private readonly UILabel _emptyBody = SliceUi.Label("", 18);
    private readonly List<UIButton> _navigation = [];
    private readonly UIView _navigationTopBorder = SliceUi.NavigationTopBorder();
    private readonly ShotSource _source;
    private readonly ShotLayoutDelegate _layoutDelegate;
    private UIButton? _retry;
    private UIButton? _clearFilters;
    private ShotFilterCriteria _filters = new();
    private long _refreshVersion;
    private Task? _refreshTask;
    public ActivityViewController(SliceNavigationController host) : base(host)
    {
        _source = new ShotSource(id => { if (PresentedViewController == null) host.Edit(id); });
        _layoutDelegate = new ShotLayoutDelegate(_source);
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = NativeTheme.Outline;
        _header.Enabled = false;
        _header.SetValue("0 shots");
        _table.BackgroundColor = NativeTheme.Surface;
        _table.AccessibilityIdentifier = "activity.list";
        _table.DataSource = _source;
        _table.Delegate = _layoutDelegate;
        _table.RegisterClassForCell(typeof(ShotCell), "shot");
        _table.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
        _state.AccessibilityIdentifier = "activity.state";
        _state.TextAlignment = UITextAlignment.Center;
        _state.BackgroundColor = NativeTheme.Surface;
        _retry = SliceUi.Button("Retry", "activity.retry", () => _ = GuardAsync(RefreshAsync));
        _retry.Hidden = true;
        _empty.BackgroundColor = NativeTheme.Surface;
        _empty.Hidden = true;
        _empty.AccessibilityIdentifier = "activity.empty";
        _emptyTitle.TextAlignment = _emptyBody.TextAlignment = UITextAlignment.Center;
        _clearFilters = SliceUi.Button("Clear Filters", "activity.clear-filters", () =>
        {
            _filters.Clear();
            _ = RefreshAsync();
        });
        _clearFilters.BackgroundColor = NativeTheme.Primary;
        _clearFilters.SetTitleColor(NativeTheme.Surface, UIControlState.Normal);
        _empty.AddSubviews(_emptyTitle, _emptyBody, _clearFilters);
        _navigation.Add(SliceUi.Icon("\uefef", "New Drink", "nav.drink", () => { if (PresentedViewController == null) Host.NewDrink(); }));
        _navigation.Add(SliceUi.Icon("\ue8b8", "Settings", "nav.settings", () => { if (PresentedViewController == null) Host.Settings(); }));
        _navigation.Add(SliceUi.Icon("\ue152", "Filter Shots", "nav.filter", () => _ = GuardAsync(OpenFilterAsync)));
        _navigation.Add(SliceUi.Icon("\ue029", "Voice", "nav.voice",
            WeakUiCallback.Create(this, static owner => _ = owner.Host.ToggleVoiceAsync(navigateToDrink: true))));
        Root.AddSubviews(_header, _table, _state, _empty, _retry);
        Root.AddSubviews(_navigation.ToArray());
        Root.AddSubview(_navigationTopBorder);
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        _ = GuardAsync(RefreshAsync);
    }

    private Task RefreshAsync()
    {
        _refreshVersion++;
        return _refreshTask ??= RefreshLatestAsync();
    }

    private async Task RefreshLatestAsync()
    {
        // Publish the shared task before a fast query can complete and clear it.
        await Task.Yield();
        _state.Text = "Loading…";
        _state.Hidden = false;
        _empty.Hidden = true;
        _table.Hidden = true;
        if (_retry != null) _retry.Hidden = true;
        try
        {
            while (true)
            {
                var version = _refreshVersion;
                var filters = _filters.Clone();
                try
                {
                    var results = await Services.RunAsync(async provider =>
                    {
                        var shots = provider.GetRequiredService<IShotService>();
                        var all = await shots.GetShotHistoryAsync(0, 50);
                        var filtered = filters.HasFilters
                            ? await shots.GetFilteredShotHistoryAsync(filters.ToDto(), 0, 50) : all;
#if DEBUG
                        await NativeReadFaults.AfterActivityReadAsync(Logger);
#endif
                        return (all.TotalCount, Filtered: filtered);
                    });
                    if (version != _refreshVersion)
                    {
                        Logger.LogDebug("Discarded Activity result {Version}; latest request is {LatestVersion}", version, _refreshVersion);
                        continue;
                    }
                    _source.Shots = results.Filtered.Items.ToList();
                    _table.ReloadData();
                    _header.SetValue(filters.HasFilters
                        ? $"{results.Filtered.TotalCount} of {results.TotalCount} shots"
                        : results.TotalCount == 1 ? "1 shot" : $"{results.TotalCount} shots");
                    SliceUi.TrackedText(_emptyTitle, filters.HasFilters ? "NO MATCHES" : "NO SHOTS YET", 12, 3, NativeTheme.Secondary);
                    _emptyBody.Text = filters.HasFilters ? "Adjust or clear filters to see results." : "Log a drink to see it here.";
                    if (_clearFilters != null) _clearFilters.Hidden = !filters.HasFilters;
                    _empty.Hidden = _source.Shots.Count > 0;
                    _state.Hidden = true;
                    _table.Hidden = _source.Shots.Count == 0;
                    _navigation[2].SetTitleColor(filters.HasFilters ? NativeTheme.Primary : NativeTheme.TextPrimary, UIControlState.Normal);
                    Root.SetNeedsLayout();
                    Host.Performance.ActivityReady(Host, results.TotalCount, _source.Shots.Count);
                    break;
                }
                catch (Exception exception)
                {
                    Logger.LogError(exception, "Activity query {Version} failed", version);
                    if (version != _refreshVersion) continue;
                    _state.Text = "Could not load Activity.";
                    _state.Hidden = false;
                    if (_retry != null) _retry.Hidden = false;
                    ShowFeedback(exception.Message, isError: true);
                    break;
                }
            }
        }
        finally { _refreshTask = null; }
    }

    private async Task OpenFilterAsync()
    {
        if (PresentedViewController != null) return;
        var options = await Services.RunAsync(async provider =>
        {
            var shots = provider.GetRequiredService<IShotService>();
            return (Beans: await shots.GetBeansWithShotsAsync(), People: await shots.GetPeopleWithShotsAsync());
        });
        var popup = new FilterViewController(Host, _filters, options.Beans, options.People, filters =>
        {
            _filters = filters;
            _ = GuardAsync(RefreshAsync);
        });
        PresentViewController(popup, false, null);
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var width = Root.Bounds.Width;
        var topInset = (nfloat)Math.Max(0, Root.SafeAreaLayoutGuide.LayoutFrame.Top - 1);
        var header = _header.MeasureHeight(width - 2, topInset);
        var bottom = SliceUi.LayoutNavigation(Root, _navigation, _navigationTopBorder);
        _header.TopInset = topInset;
        _header.Frame = new CGRect(1, 1, width - 2, header);
        _table.Frame = new CGRect(1, header + 2, width - 2, Root.Bounds.Height - header - bottom - 4);
        _state.Frame = _table.Frame;
        _empty.Frame = _table.Frame;
        var emptyWidth = _empty.Bounds.Width - 48;
        var titleHeight = SliceUi.Measure(_emptyTitle, emptyWidth).Height;
        var bodyHeight = SliceUi.Measure(_emptyBody, emptyWidth).Height;
        var hasClear = _clearFilters is { Hidden: false };
        var emptyHeight = titleHeight + 12 + bodyHeight + (hasClear ? 64 : 0);
        var emptyTop = (_empty.Bounds.Height - emptyHeight) / 2;
        _emptyTitle.Frame = new CGRect(24, emptyTop, emptyWidth, titleHeight);
        _emptyBody.Frame = new CGRect(24, emptyTop + titleHeight + 12, emptyWidth, bodyHeight);
        if (_clearFilters != null)
        {
            var clearWidth = _clearFilters.SizeThatFits(new CGSize(emptyWidth, 44)).Width + 32;
            _clearFilters.Frame = new CGRect((_empty.Bounds.Width - clearWidth) / 2,
                emptyTop + titleHeight + bodyHeight + 32, clearWidth, 44);
        }
        if (_retry != null) _retry.Frame = new CGRect(24, header + 140, width - 48, 48);
    }

    private sealed class ShotSource(Action<int> selected) : UICollectionViewDataSource
    {
        public List<ShotRecordDto> Shots { get; set; } = [];
        public override nint GetItemsCount(UICollectionView collectionView, nint section) => Shots.Count;
        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (ShotCell)collectionView.DequeueReusableCell("shot", indexPath);
            cell.Bind(Shots[(int)indexPath.Item], selected);
            return cell;
        }
        public void Select(nint index) => selected(Shots[(int)index].Id);
    }

    private sealed class ShotLayoutDelegate(ShotSource source) : UICollectionViewDelegateFlowLayout
    {
        private readonly UILabel _heading = SliceUi.Label("Mg", 22, true);
        private readonly UILabel _detail = SliceUi.Label("Mg", 13);
        public override CGSize GetSizeForItem(UICollectionView collectionView, UICollectionViewLayout layout, NSIndexPath indexPath)
            => new(collectionView.Bounds.Width,
                SliceUi.Measure(_heading, collectionView.Bounds.Width).Height + SliceUi.Measure(_detail, collectionView.Bounds.Width).Height + 33);

        public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
        {
            collectionView.DeselectItem(indexPath, false);
            source.Select(indexPath.Item);
        }
    }

    [Register("NativeShotCell")]
    private sealed class ShotCell : UICollectionViewCell
    {
        private readonly UILabel _method = SliceUi.Label("", 22, true);
        private readonly UILabel _ratio = SliceUi.Label("", 18, true);
        private readonly UILabel _bean = SliceUi.Label("", 13, secondary: true);
        private readonly UILabel _result = SliceUi.Label("", 13, secondary: true);
        private readonly UIView _divider = new() { BackgroundColor = NativeTheme.Outline };
        private Action<int>? _activate;
        private int _shotId;
        public ShotCell(ObjCRuntime.NativeHandle handle) : base(handle)
        {
            BackgroundColor = NativeTheme.Surface;
            IsAccessibilityElement = true;
            AccessibilityTraits = UIAccessibilityTrait.Button;
            _ratio.TextAlignment = _result.TextAlignment = UITextAlignment.Right;
            _bean.Lines = _method.Lines = _ratio.Lines = _result.Lines = 1;
            _bean.LineBreakMode = _method.LineBreakMode = UILineBreakMode.TailTruncation;
            ContentView.AddSubviews(_method, _ratio, _bean, _result, _divider);
        }
        public void Bind(ShotRecordDto shot, Action<int> activate)
        {
            AccessibilityIdentifier = $"shot.{shot.Id}";
            _shotId = shot.Id;
            _activate = activate;
            var text = DrinkDisplay.ActivityRow(shot);
            _method.Text = text.Title;
            _ratio.Text = text.Ratio;
            _bean.Text = text.Subtitle;
            _result.Text = text.Result;
            AccessibilityLabel = $"{text.Title}, {text.Ratio}, {text.Subtitle}, {text.Result}";
            SetNeedsLayout();
        }
        public override bool AccessibilityActivate()
        {
            if (_activate == null) return false;
            _activate(_shotId);
            return true;
        }
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var width = ContentView.Bounds.Width;
            var ratio = SliceUi.Measure(_ratio, width - 32);
            var result = SliceUi.Measure(_result, width - 32);
            var rightWidth = (nfloat)Math.Max(ratio.Width, result.Width);
            var headingHeight = (nfloat)Math.Max(SliceUi.Measure(_method, width).Height, ratio.Height);
            var detailHeight = (nfloat)Math.Max(SliceUi.Measure(_bean, width).Height, result.Height);
            _method.Frame = new CGRect(16, 14, width - rightWidth - 32, headingHeight);
            _ratio.Frame = new CGRect(width - ratio.Width - 16, 14, ratio.Width, headingHeight);
            _bean.Frame = new CGRect(16, 18 + headingHeight, width - rightWidth - 32, detailHeight);
            _result.Frame = new CGRect(width - result.Width - 16, 18 + headingHeight, result.Width, detailHeight);
            _divider.Frame = new CGRect(0, ContentView.Bounds.Height - 1, width, 1);
        }
    }
}
