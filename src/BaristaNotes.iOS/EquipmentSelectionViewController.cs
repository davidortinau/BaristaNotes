using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal enum EquipmentSelectionKind { Machine, Grinder, Accessories }

internal sealed class EquipmentSelectionViewController : SliceViewController
{
    private readonly string _heading;
    private readonly List<EquipmentDto> _items;
    private readonly List<int> _selected;
    private readonly Action<IReadOnlyList<int>> _changed;
    private readonly bool _multiple;
    private readonly EquipmentRows _rows;
    private readonly UILabel _title = new();
    private UIButton? _close;
    private UIButton? _clear;
    private UIButton? _done;

    public EquipmentSelectionViewController(SliceNavigationController host, EquipmentSelectionKind kind,
        List<EquipmentDto> items, IEnumerable<int> selected, Action<IReadOnlyList<int>> changed) : base(host)
    {
        _heading = kind.ToString().ToUpperInvariant();
        _items = items;
        _selected = selected.Distinct().ToList();
        _changed = changed;
        _multiple = kind == EquipmentSelectionKind.Accessories;
        _rows = new EquipmentRows(_multiple ? EquipmentRowStyle.MultiChoice : EquipmentRowStyle.SingleChoice, "equipment.choices");
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        _close = SliceUi.PickerAction("Close", "equipment.choice.close",
            WeakUiCallback.Create(this, static owner => owner.Close()));
        _clear = SliceUi.PickerAction("Clear", "equipment.choice.clear",
            WeakUiCallback.Create(this, static owner =>
            {
                if (owner.Host.TopViewController != owner) return;
                owner._selected.Clear();
                owner._changed([]);
                owner.Close();
            }));
        Root.AddSubviews(_title, _close, _clear, _rows);
        if (_multiple)
        {
            _done = SliceUi.PickerAction("Done", "equipment.choice.done",
                WeakUiCallback.Create(this, static owner => owner.Close()), primary: true);
            Root.AddSubview(_done);
        }
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((EquipmentSelectionViewController)environment).Render(true));
        Render(true);
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        Render(true);
    }

    private void Close()
    {
        if (Host.TopViewController == this) Host.PopViewController(false);
    }

    private void Select(int id)
    {
        if (Host.TopViewController != this) return;
        if (_multiple)
        {
            if (!_selected.Remove(id)) _selected.Add(id);
            _changed(_selected.ToArray());
            Render(false);
        }
        else
        {
            _changed([id]);
            Close();
        }
    }

    private void Render(bool center)
    {
        SourceScaledText.Tracked(_title, _heading, 12, 3, NativeTheme.Secondary, TraitCollection);
        foreach (var button in new[] { _close, _clear, _done })
            if (button != null) button.TitleLabel.Font = SourceScaledText.Font(14, button == _done, TraitCollection);
        _rows.Update(_items.Select(item => new EquipmentRow($"equipment.choice.{item.Id}", item.Name,
            WeakUiCallback.Create(this, item.Id, static (owner, id) => owner.Select(id)),
            Selected: _selected.Contains(item.Id))).ToArray(), TraitCollection, center);
        Root.SetNeedsLayout();
    }

#pragma warning disable CS0672, CA1422
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        if (!OperatingSystem.IsIOSVersionAtLeast(17) && IsViewLoaded) Render(true);
    }
#pragma warning restore CS0672, CA1422

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = Root.SafeAreaLayoutGuide.LayoutFrame;
        var buttons = new[] { _close, _clear, _done }.OfType<UIButton>().ToArray();
        var rowHeight = buttons.Select(button => button.SizeThatFits(CGSize.Empty).Height).DefaultIfEmpty(44).Max();
        var title = SliceUi.Measure(_title, safe.Width - 24);
        _title.Frame = new CGRect(safe.X + (safe.Width - title.Width) / 2, safe.Y + 8 + (rowHeight - title.Height) / 2, title.Width, title.Height);
        if (_close != null) _close.Frame = new CGRect(safe.X + 12, safe.Y + 8, (nfloat)Math.Ceiling(_close.SizeThatFits(CGSize.Empty).Width), rowHeight);
        var x = safe.Right - 12;
        foreach (var button in new[] { _done, _clear })
        {
            if (button == null) continue;
            var width = (nfloat)Math.Ceiling(button.SizeThatFits(CGSize.Empty).Width);
            x -= width;
            button.Frame = new CGRect(x, safe.Y + 8, width, rowHeight);
        }
        _rows.Frame = SliceUi.PickerViewport(Root, safe.Top + rowHeight + 16);
    }
}
