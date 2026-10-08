using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class BeanListViewController : SliceViewController
{
    private readonly EquipmentHeader _header = new("beans.header", compact: true);
    private readonly EquipmentRows _list = new(EquipmentRowStyle.Management, "beans.list");
    private readonly UIView _bodySurface = new() { BackgroundColor = NativeTheme.Surface };
    private readonly BeanListState _state = new() { BackgroundColor = NativeTheme.Surface };
    private readonly UILabel _stateTitle = new() { TextAlignment = UITextAlignment.Center };
    private readonly UILabel _stateMessage = new() { Lines = 0, TextAlignment = UITextAlignment.Center };
    private readonly UIActivityIndicatorView _spinner = new(UIActivityIndicatorViewStyle.Medium);
    private readonly List<UIButton> _navigation = [];
    private readonly UIView _navigationTopBorder = SliceUi.NavigationTopBorder();
    private List<BeanDto> _beans = [];
    private UIButton? _retry;
    private bool _visible;
    private bool _loading;
    private string? _loadError;
    private int _version;
    private BeanMapView? _map;
    private bool _mapOwnerDisposed;
    private nfloat _heroHeight;
    private nfloat _safeTop;
    private bool _titlePinned;

    public BeanListViewController(SliceNavigationController host) : base(host) { }
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = NativeTheme.Outline;
        _spinner.Color = NativeTheme.Primary;
        _retry = SliceUi.PickerAction("Retry", "beans.retry", WeakUiCallback.Create(this, static owner => _ = owner.LoadAsync()));
        _retry.BackgroundColor = NativeTheme.Primary;
        _retry.SetTitleColor(NativeTheme.Surface, UIControlState.Normal);
        _retry.Layer.CornerRadius = 8;
        _retry.Hidden = true;
        _navigation.Add(SliceUi.Icon("\uefef", "New Drink", "nav.drink",
            WeakUiCallback.Create(this, static owner => owner.Host.NewDrink())));
        _navigation.Add(SliceUi.Icon("\uf009", "Activity", "nav.activity",
            WeakUiCallback.Create(this, static owner => owner.Host.Activity())));
        _navigation.Add(SliceUi.Icon("\ue8b8", "Settings", "beans.back",
            WeakUiCallback.Create(this, static owner => owner.Host.RequestBack())));
        var add = SliceUi.Icon("\ue145", "Add bean", "beans.add", WeakUiCallback.Create(this, static owner =>
        {
            if (owner.Host.TopViewController == owner && owner.Host.PresentedViewController == null)
                owner.Host.PushHierarchy(new BeanDetailViewController(owner.Host));
        }));
        add.TitleLabel.Font = NativeTheme.Icons(24);
        add.BackgroundColor = NativeTheme.TextPrimary;
        add.SetTitleColor(NativeTheme.Surface, UIControlState.Normal);
        _navigation.Add(add);
        Root.AddSubviews(_list, _bodySurface);
        _state.AddSubviews(_stateTitle, _stateMessage, _spinner, _retry);
        _state.LayoutContent = WeakUiCallback.Create(this, static owner => owner.LayoutState());
        Root.AddSubviews(_navigation.ToArray());
        Root.AddSubview(_navigationTopBorder);
        _map = new BeanMapView(Logger, Services.Singleton<IOriginGeocoder>(),
            WeakUiCallback.Create(this, static owner => owner.SelectMapCountries()),
            WeakUiCallback.Create(this, static owner => owner.RenderOriginHeader()));
        _list.ConfigurePage(_map, _header, _state, PageScrolled, _map.BlocksPageScrollAt);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
        {
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((BeanListViewController)environment).Render());
            RegisterForTraitChanges<UITraitUserInterfaceStyle>(static (environment, _) =>
                ((BeanListViewController)environment).SetNeedsStatusBarAppearanceUpdate());
        }
        Render();
    }
    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        _visible = true;
        _ = LoadAsync();
    }
    public override void ViewDidDisappear(bool animated)
    {
        _visible = false;
        _version++;
        _map?.CancelOriginResolution();
        _spinner.StopAnimating();
        base.ViewDidDisappear(animated);
    }
    public override void ViewWillDisappear(bool animated)
    {
        base.ViewWillDisappear(animated);
        if (IsMovingFromParentViewController) RetireMap();
    }
    private async Task LoadAsync()
    {
        var version = ++_version;
        _loading = true;
        _loadError = null;
        Render();
        _ = LoadOriginsAsync(version);
        try
        {
            var beans = await Services.RunAsync(provider => provider.GetRequiredService<IBeanService>().GetAllActiveBeansAsync());
            if (!_visible || version != _version) return;
            _beans = beans.ToList();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Native bean list read failed");
            if (_visible && version == _version)
            {
                _loadError = error.Message;
            }
        }
        finally
        {
            if (_visible && version == _version)
            {
                _loading = false;
                Render();
            }
        }
    }

    private async Task LoadOriginsAsync(int version)
    {
        try
        {
            var beans = await Services.RunAsync(provider =>
                provider.GetRequiredService<IBeanService>().GetAllSavedBeansAsync());
            if (!_visible || version != _version) return;
            _map?.SetBeans(beans);
            Render();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Native saved bean origins read failed");
            if (_visible && version == _version) _map?.ReportOriginError();
        }
    }

    private void SelectMapCountries()
    {
        if (_mapOwnerDisposed) return;
        Render();
        _list.LayoutIfNeeded();
        // Keep the hero visible when filtering; a pinned page starts at its first bean.
        var offset = (nfloat)Math.Min(_list.ContentOffset.Y, _map?.Bounds.Height ?? 0);
        _list.SetContentOffset(new CGPoint(0, offset), false);
    }

    private void Render()
    {
        var beans = _map?.HasSelection == true ? _map.SelectedBeans : _beans;
        UpdateHeader(beans);
        if (_loading) _spinner.StartAnimating(); else _spinner.StopAnimating();
        _list.BackgroundColor = beans.Count == 0 ? NativeTheme.Surface : NativeTheme.Outline;
        var offset = _list.ContentOffset;
        var rows = (_loadError != null ? [] : beans).Select(bean => new EquipmentRow($"beans.item.{bean.Id}", bean.Name,
            WeakUiCallback.Create(this, bean.Id, static (owner, id) =>
            {
                if (owner.Host.TopViewController == owner)
                    owner.Host.PushHierarchy(new BeanDetailViewController(owner.Host, id));
            }), Caption: (bean.IsActive ? "" : "ARCHIVED - ") +
                (!string.IsNullOrWhiteSpace(bean.Roaster) ? bean.Roaster.ToUpperInvariant()
                : !string.IsNullOrWhiteSpace(bean.Origin) ? bean.Origin.ToUpperInvariant() : "BEAN"))).ToArray();
        _stateTitle.Hidden = _stateMessage.Hidden = _loading || (_loadError == null && beans.Count > 0);
        SourceScaledText.Tracked(_stateTitle, _loadError == null ? "NO BEANS" : "ERROR", 10, 2, NativeTheme.Secondary, TraitCollection);
        _stateMessage.Text = _loadError ?? "Add your favorite coffee beans";
        _stateMessage.Font = SourceScaledText.Font(16, false, TraitCollection);
        _stateMessage.TextColor = NativeTheme.TextPrimary;
        if (_retry != null)
        {
            _retry.Hidden = _loading || _loadError == null;
            _retry.TitleLabel.Font = SourceScaledText.Font(14, false, TraitCollection);
        }
        _state.Hidden = _loadError == null && beans.Count > 0;
        UpdateStateHeight(_list.Bounds.Width);
        _list.Update(rows, TraitCollection);
        if (_list.Bounds.Height > 0)
        {
            _list.LayoutIfNeeded();
            var maximum = (nfloat)Math.Max(0, _list.ContentSize.Height - _list.Bounds.Height);
            _list.SetContentOffset(new CGPoint(0, (nfloat)Math.Clamp(offset.Y, 0, maximum)), false);
        }
        _state.SetNeedsLayout();
        Root.SetNeedsLayout();
        SetNeedsStatusBarAppearanceUpdate();
    }

    private void UpdateHeader(IReadOnlyList<BeanDto> beans)
    {
        var count = beans.Count == 1 ? "1 bean" : $"{beans.Count} beans";
        _header.Update("BEANS", _map?.HasSelection == true ? _map.SelectionLabel : count);
        _header.UpdateFonts(TraitCollection);
    }

    private void RenderOriginHeader()
    {
        if (_mapOwnerDisposed || _map?.HasSelection != true) return;
        UpdateHeader(_map.SelectedBeans);
        Root.SetNeedsLayout();
    }
#pragma warning disable CS0672, CA1422
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        if (!OperatingSystem.IsIOSVersionAtLeast(17) && IsViewLoaded) Render();
    }
#pragma warning restore CS0672, CA1422
    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = Root.SafeAreaInsets;
        var left = (nfloat)Math.Max(1, safe.Left);
        var right = (nfloat)Math.Max(1, safe.Right);
        var top = (nfloat)Math.Max(1, safe.Top);
        var width = Root.Bounds.Width - left - right;
        _safeTop = safe.Top;
        var oldHeader = _header.LegacyHeightThatFits(new CGSize(width, nfloat.MaxValue),
            (nfloat)Math.Max(0, top - 1));
        var header = _header.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;
        var bottom = SliceUi.LayoutNavigation(Root, _navigation, _navigationTopBorder);
        var oldMap = (nfloat)Math.Min(240, Math.Max(0, (Root.Bounds.Height - oldHeader - bottom - 4) * .4));
        var previousHero = (nfloat)Math.Max(1, oldHeader + 2 + oldMap - top);
        _heroHeight = (nfloat)BeanPageGeometry.ExpandedMapHeight(previousHero, top, _map?.MeasureFormerFooter(width) ?? 0);
        _list.Frame = new CGRect(left, 0, width, (nfloat)Math.Max(0, Root.Bounds.Height - bottom - 2));
        _list.ScrollIndicatorInsets = new UIEdgeInsets(_safeTop, 0, 0, 0);
        _bodySurface.Frame = new CGRect(0, 0, Root.Bounds.Width, _safeTop);
        if (UpdateStateHeight(width)) _list.CollectionViewLayout.InvalidateLayout();
        _list.SetPageHeights(_heroHeight, header, _safeTop);
        _list.LayoutIfNeeded();
        _state.SetNeedsLayout();
        PageScrolled(_list.ContentOffset.Y);
    }

    public override UIStatusBarStyle PreferredStatusBarStyle() =>
        _titlePinned && TraitCollection.UserInterfaceStyle == UIUserInterfaceStyle.Dark
            ? UIStatusBarStyle.LightContent : UIStatusBarStyle.DarkContent;

    private void PageScrolled(nfloat offset)
    {
        _map?.SetParallax(offset);
        var pinned = _heroHeight > 0 && BeanPageGeometry.TitleTop(_heroHeight, offset, _safeTop) == _safeTop;
        _bodySurface.Hidden = !pinned;
        if (_titlePinned == pinned) return;
        _titlePinned = pinned;
        SetNeedsStatusBarAppearanceUpdate();
    }

    private bool UpdateStateHeight(nfloat width)
    {
        var inset = _loadError == null ? 32 : 24;
        var retryText = _retry == null ? CGSize.Empty : SliceUi.Measure(_retry.TitleLabel, (nfloat)Math.Max(1, width - 48));
        var height = _state.Hidden ? 0 : _loading ? 44 :
            SliceUi.Measure(_stateTitle, (nfloat)Math.Max(1, width - 2 * inset)).Height + 12 +
            SliceUi.Measure(_stateMessage, (nfloat)Math.Max(1, width - 2 * inset)).Height +
            (_loadError == null ? 0 : 12 + (nfloat)Math.Max(44, retryText.Height + 20));
        if (_state.MinimumContentHeight == height) return false;
        _state.MinimumContentHeight = height;
        return true;
    }

    private void LayoutState()
    {
        var width = _state.Bounds.Width;
        var middle = _state.Bounds.Height / 2;
        var inset = _loadError == null ? 32 : 24;
        var title = SliceUi.Measure(_stateTitle, width - 2 * inset).Height;
        var message = SliceUi.Measure(_stateMessage, width - 2 * inset).Height;
        var retryText = _retry == null ? CGSize.Empty : SliceUi.Measure(_retry.TitleLabel, width - 48);
        var retryWidth = (nfloat)Math.Max(44, retryText.Width + 28);
        var retryHeight = (nfloat)Math.Max(44, retryText.Height + 20);
        var groupHeight = title + 12 + message + (_loadError == null ? 0 : 12 + retryHeight);
        _stateTitle.Frame = new CGRect(inset, middle - groupHeight / 2, width - 2 * inset, title);
        _stateMessage.Frame = new CGRect(inset, _stateTitle.Frame.Bottom + 12, width - 2 * inset, message);
        if (_retry != null) _retry.Frame = new CGRect((width - retryWidth) / 2, _stateMessage.Frame.Bottom + 12, retryWidth, retryHeight);
        _spinner.Center = new CGPoint(width / 2, middle);
    }

    internal sealed class BeanListState : UIView
    {
        public Action? LayoutContent { get; set; }
        public nfloat MinimumContentHeight { get; set; }
        public override CGSize SizeThatFits(CGSize size) => new(size.Width, MinimumContentHeight);
        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            LayoutContent?.Invoke();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _visible = false;
            _version++;
            RetireMap();
        }
        base.Dispose(disposing);
    }

    internal void RetireMap()
    {
        if (_mapOwnerDisposed) return;
        _mapOwnerDisposed = true;
        _map?.RemoveFromSuperview();
        _map?.Dispose();
        _map = null;
    }
}

internal sealed class BeanSettingsLink : UIControl
{
    private readonly UILabel _caption = new();
    private readonly UILabel _title = new() { Text = "Coffee beans and roasters", Lines = 1 };
    private readonly UILabel _chevron = new() { Text = "\ue5cc", Font = NativeTheme.Icons(24) };
    public BeanSettingsLink(Action action)
    {
        AccessibilityIdentifier = "settings.beans";
        AccessibilityLabel = "BEANS: Coffee beans and roasters";
        AccessibilityTraits = UIAccessibilityTrait.Button;
        IsAccessibilityElement = true;
        BackgroundColor = NativeTheme.Surface;
        AddSubviews(_caption, _title, _chevron);
        TouchUpInside += (_, _) => action();
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
            {
                var view = (BeanSettingsLink)environment;
                view.UpdateFonts();
                view.Superview?.SetNeedsLayout();
            });
        UpdateFonts();
    }
    private void UpdateFonts()
    {
        SourceScaledText.Tracked(_caption, "BEANS", 10, 2, NativeTheme.Secondary, TraitCollection);
        _title.Font = SourceScaledText.Font(20, true, TraitCollection);
        _title.TextColor = _chevron.TextColor = NativeTheme.TextPrimary;
    }
    public override CGSize SizeThatFits(CGSize size)
    {
        UpdateFonts();
        return new CGSize(size.Width, (nfloat)Math.Max(80, 32 + SliceUi.Measure(_caption, size.Width - 64).Height + SliceUi.Measure(_title, size.Width - 64).Height));
    }
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var caption = SliceUi.Measure(_caption, Bounds.Width - 64).Height;
        var title = SliceUi.Measure(_title, Bounds.Width - 64).Height;
        var y = (Bounds.Height - caption - title) / 2;
        _caption.Frame = new CGRect(16, y, Bounds.Width - 64, caption);
        _title.Frame = new CGRect(16, y + caption, Bounds.Width - 64, title);
        _chevron.Frame = new CGRect(Bounds.Width - 40, (Bounds.Height - 32) / 2, 24, 32);
    }
}
