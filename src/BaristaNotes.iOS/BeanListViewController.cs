using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class BeanListViewController : SliceViewController
{
    private readonly EquipmentHeader _header = new("beans.header");
    private readonly EquipmentRows _list = new(EquipmentRowStyle.Management, "beans.list");
    private readonly UIView _bodySurface = new() { BackgroundColor = NativeTheme.Surface };
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
        Root.AddSubviews(_header, _bodySurface, _list, _stateTitle, _stateMessage, _spinner, _retry);
        Root.AddSubviews(_navigation.ToArray());
        Root.AddSubview(_navigationTopBorder);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((BeanListViewController)environment).Render());
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
        _spinner.StopAnimating();
        base.ViewDidDisappear(animated);
    }
    private async Task LoadAsync()
    {
        var version = ++_version;
        _loading = true;
        _loadError = null;
        Render();
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
    private void Render()
    {
        _header.Update("BEANS", _beans.Count == 1 ? "1 bean" : $"{_beans.Count} beans");
        _header.UpdateFonts(TraitCollection);
        if (_loading) _spinner.StartAnimating(); else _spinner.StopAnimating();
        _list.Hidden = _loading || _loadError != null;
        _list.BackgroundColor = _beans.Count == 0 ? NativeTheme.Surface : NativeTheme.Outline;
        if (!_list.Hidden) _list.Update(_beans.Select(bean => new EquipmentRow($"beans.item.{bean.Id}", bean.Name,
            WeakUiCallback.Create(this, bean.Id, static (owner, id) =>
            {
                if (owner.Host.TopViewController == owner)
                    owner.Host.PushHierarchy(new BeanDetailViewController(owner.Host, id));
            }), Caption: !string.IsNullOrWhiteSpace(bean.Roaster) ? bean.Roaster.ToUpperInvariant()
                : !string.IsNullOrWhiteSpace(bean.Origin) ? bean.Origin.ToUpperInvariant() : "BEAN")).ToArray(), TraitCollection);
        _stateTitle.Hidden = _stateMessage.Hidden = _loading || (_loadError == null && _beans.Count > 0);
        SourceScaledText.Tracked(_stateTitle, _loadError == null ? "NO BEANS" : "ERROR", 10, 2, NativeTheme.Secondary, TraitCollection);
        _stateMessage.Text = _loadError ?? "Add your favorite coffee beans";
        _stateMessage.Font = SourceScaledText.Font(16, false, TraitCollection);
        _stateMessage.TextColor = NativeTheme.TextPrimary;
        if (_retry != null)
        {
            _retry.Hidden = _loading || _loadError == null;
            _retry.TitleLabel.Font = SourceScaledText.Font(14, false, TraitCollection);
        }
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
        var width = Root.Bounds.Width - 2;
        _header.TopInset = (nfloat)Math.Max(0, Root.SafeAreaInsets.Top - 1);
        var header = _header.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;
        var bottom = SliceUi.LayoutNavigation(Root, _navigation, _navigationTopBorder);
        _header.Frame = new CGRect(1, 1, width, header);
        _list.Frame = new CGRect(1, header + 2, width, Root.Bounds.Height - header - bottom - 4);
        _bodySurface.Frame = _list.Frame;
        var middle = _list.Frame.Y + _list.Frame.Height / 2;
        var inset = _loadError == null ? 32 : 24;
        var title = SliceUi.Measure(_stateTitle, width - 2 * inset).Height;
        var message = SliceUi.Measure(_stateMessage, width - 2 * inset).Height;
        var retryText = _retry == null ? CGSize.Empty : SliceUi.Measure(_retry.TitleLabel, width - 48);
        var retryWidth = (nfloat)Math.Max(44, retryText.Width + 28);
        var retryHeight = (nfloat)Math.Max(44, retryText.Height + 20);
        var groupHeight = title + 12 + message + (_loadError == null ? 0 : 12 + retryHeight);
        _stateTitle.Frame = new CGRect(1 + inset, middle - groupHeight / 2, width - 2 * inset, title);
        _stateMessage.Frame = new CGRect(1 + inset, _stateTitle.Frame.Bottom + 12, width - 2 * inset, message);
        if (_retry != null) _retry.Frame = new CGRect((Root.Bounds.Width - retryWidth) / 2, _stateMessage.Frame.Bottom + 12, retryWidth, retryHeight);
        _spinner.Center = new CGPoint(Root.Bounds.Width / 2, middle);
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
