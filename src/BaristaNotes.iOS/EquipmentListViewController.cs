using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class EquipmentListViewController : SliceViewController
{
    private readonly EquipmentHeader _header = new("equipment.list.header");
    private readonly EquipmentRows _rows = new(EquipmentRowStyle.Management, "equipment.list");
    private readonly UILabel _emptyTitle = new();
    private readonly UILabel _emptyText = new() { Lines = 0, TextAlignment = UITextAlignment.Center };
    private readonly UILabel _error = new() { Lines = 0, TextAlignment = UITextAlignment.Center };
    private readonly UIActivityIndicatorView _spinner = new(UIActivityIndicatorViewStyle.Medium);
    private readonly List<UIButton> _navigation = [];
    private readonly UIView _navigationTopBorder = SliceUi.NavigationTopBorder();
    private UIButton? _retry;
    private List<EquipmentDto> _items = [];
    private int _readVersion;
    private bool _visible;

    public EquipmentListViewController(SliceNavigationController host) : base(host) { }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = NativeTheme.Outline;
        _emptyTitle.TextAlignment = UITextAlignment.Center;
        _emptyText.Text = "Add machines, grinders, and accessories";
        _error.Hidden = true;
        _retry = SliceUi.Button("Retry", "equipment.retry",
            WeakUiCallback.Create(this, static owner => _ = owner.RefreshAsync()));
        _retry.Hidden = true;
        _navigation.Add(SliceUi.Icon("\uefef", "New Drink", "nav.drink", Host.NewDrink));
        _navigation.Add(SliceUi.Icon("\uf009", "Activity", "nav.activity", Host.Activity));
        _navigation.Add(SliceUi.Icon("\ue8b8", "Settings", "equipment.back",
            WeakUiCallback.Create(this, static owner => owner.Host.RequestBack())));
        var add = SliceUi.Icon("\ue145", "Add equipment", "equipment.add",
            WeakUiCallback.Create(this, static owner =>
            {
                if (owner.Host.TopViewController == owner && owner.Host.PresentedViewController == null)
                    owner.Host.PushHierarchy(new EquipmentFormViewController(owner.Host));
            }));
        add.TitleLabel.Font = NativeTheme.Icons(24);
        add.BackgroundColor = NativeTheme.TextPrimary;
        add.SetTitleColor(NativeTheme.Surface, UIControlState.Normal);
        _navigation.Add(add);
        Root.AddSubviews(_header, _rows, _emptyTitle, _emptyText, _error, _retry, _spinner);
        Root.AddSubviews(_navigation.ToArray());
        Root.AddSubview(_navigationTopBorder);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((EquipmentListViewController)environment).Render());
        Render();
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        _visible = true;
        _ = RefreshAsync();
    }

    public override void ViewDidDisappear(bool animated)
    {
        _visible = false;
        _readVersion++;
        base.ViewDidDisappear(animated);
    }

    private async Task RefreshAsync()
    {
        var version = ++_readVersion;
        _spinner.StartAnimating();
        _error.Hidden = true;
        if (_retry != null) _retry.Hidden = true;
        try
        {
            var items = await Services.RunAsync(provider => provider.GetRequiredService<IEquipmentService>().GetAllActiveEquipmentAsync());
            if (!_visible || version != _readVersion) return;
            _items = items.ToList();
            Render();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Native equipment list refresh failed");
            if (_visible && version == _readVersion)
            {
                _error.Text = error.Message;
                _error.Hidden = false;
                _rows.Hidden = _emptyText.Hidden = _emptyTitle.Hidden = true;
                if (_retry != null) _retry.Hidden = false;
                Root.SetNeedsLayout();
            }
        }
        finally { if (version == _readVersion) _spinner.StopAnimating(); }
    }

    private void Render()
    {
        _rows.Hidden = false;
        _rows.BackgroundColor = _items.Count == 0 ? NativeTheme.Surface : NativeTheme.Outline;
        _error.Hidden = true;
        if (_retry != null) _retry.Hidden = true;
        _header.Update("EQUIPMENT", _items.Count == 1 ? "1 item" : $"{_items.Count} items");
        _header.UpdateFonts(TraitCollection);
        SourceScaledText.Tracked(_emptyTitle, "NO EQUIPMENT", 10, 2, NativeTheme.Secondary, TraitCollection);
        _emptyText.Font = _error.Font = SourceScaledText.Font(16, false, TraitCollection);
        _emptyText.TextColor = _error.TextColor = NativeTheme.TextPrimary;
        _emptyTitle.Hidden = _emptyText.Hidden = _items.Count != 0;
        _rows.Update(_items.Select(item => new EquipmentRow($"equipment.item.{item.Id}", item.Name,
            WeakUiCallback.Create(this, item.Id, static (owner, id) =>
            {
                if (owner.Host.TopViewController == owner)
                    owner.Host.PushHierarchy(new EquipmentFormViewController(owner.Host, id));
            }), Caption: item.Type.ToString().ToUpperInvariant())).ToArray(), TraitCollection);
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
        var navigation = SliceUi.LayoutNavigation(Root, _navigation, _navigationTopBorder);
        _header.Frame = new CGRect(1, 1, width, header);
        _rows.Frame = new CGRect(1, header + 2, width, Root.Bounds.Height - header - navigation - 4);
        var title = SliceUi.Measure(_emptyTitle, width - 64);
        var text = SliceUi.Measure(_emptyText, width - 64);
        var mid = _rows.Frame.Y + _rows.Frame.Height / 2;
        _emptyTitle.Frame = new CGRect(33, mid - (title.Height + text.Height + 12) / 2, width - 64, title.Height);
        _emptyText.Frame = new CGRect(33, _emptyTitle.Frame.Bottom + 12, width - 64, text.Height);
        _error.Frame = new CGRect(25, mid - 48, width - 48, 80);
        if (_retry != null) _retry.Frame = new CGRect(1 + width / 2 - 60, mid + 40, 120, 44);
        _spinner.Center = new CGPoint(1 + width / 2, mid);
    }
}
