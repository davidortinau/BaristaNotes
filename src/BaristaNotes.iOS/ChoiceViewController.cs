using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class ChoiceViewController(SliceNavigationController host, string heading, IReadOnlyList<ChoiceRow> rows)
    : SliceViewController(host)
{
    private readonly ChoiceList _list = new();
    private readonly UILabel _title = SliceUi.Label(heading, 12, true, true);
    private UIButton? _close;
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        SliceUi.TrackedText(_title, heading, 12, 3, NativeTheme.Secondary);
        _title.TextAlignment = UITextAlignment.Left;
        _close = SliceUi.PickerAction("Close", "picker.close", () => Host.PopViewController(false));
        Root.AddSubviews(_list, _title, _close);
    }
    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        _list.SetRows(rows, true);
    }
    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = Root.SafeAreaLayoutGuide.LayoutFrame;
        var top = safe.Top;
        var title = SliceUi.Measure(_title, safe.Width - 24);
        _title.Frame = new CGRect(safe.X + (safe.Width - title.Width) / 2, top + 8 + (44 - title.Height) / 2, title.Width, title.Height);
        if (_close != null) _close.Frame = new CGRect(safe.X + 12, top + 8, _close.SizeThatFits(CGSize.Empty).Width, 44);
        _list.Frame = SliceUi.PickerViewport(Root, top + 60);
    }
}
