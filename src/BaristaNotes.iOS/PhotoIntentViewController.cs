using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class PhotoIntentViewController : PhotoActionModal
{
    private readonly TaskCompletionSource<PhotoIntentChoice> _choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _chosen;
    public PhotoIntentViewController(SliceNavigationController host) : base(host, "Use This Photo", "photo.intent", dismissBackdrop: false) { }
    public Task<PhotoIntentChoice> Choice => _choice.Task;
    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        var explanation = new UILabel
        {
            Text = "I could not determine the next step. What do you want to create?",
            Lines = 0, Font = SourceScaledText.Font(14, false, TraitCollection), TextColor = NativeTheme.DarkSecondary
        };
        var items = new List<UIView> { new PhotoInsetView(explanation, new UIEdgeInsets(0, 0, 8, 0)) };
        foreach (var (title, body, id, choice) in new[]
        {
            ("Add coffee", "Fill a new bean and bag card from the image", "PhotoIntentCoffeeButton", PhotoIntentChoice.Coffee),
            ("Create profile", "Use the image as a new profile photo", "PhotoIntentProfileButton", PhotoIntentChoice.Profile),
            ("Count people", "Calculate coffee needs for a room or group", "PhotoIntentRoomButton", PhotoIntentChoice.Room),
            ("Take another photo", "Open the camera again", "PhotoIntentRetakeButton", PhotoIntentChoice.Retake)
        })
            items.Add(new PhotoIntentChoiceView(title, body, id, TraitCollection,
                WeakUiCallback.Create(this, choice, static (owner, value) => _ = owner.ChooseAsync(value))));
        SetContent(new PhotoStack(8, new UIEdgeInsets(8, 16, 8, 16), items.ToArray()));
    }
    private async Task ChooseAsync(PhotoIntentChoice choice)
    {
        if (_chosen || !IsOpen) return;
        _chosen = true;
        try { await CloseAsync(); _choice.TrySetResult(choice); }
        catch (Exception error) { _choice.TrySetException(error); }
    }
    protected override void OnClosed()
    {
        if (!_chosen) _choice.TrySetResult(PhotoIntentChoice.Cancel);
    }
}

internal sealed class PhotoIntentChoiceView : UIControl
{
    private readonly UILabel _title, _body;
    public PhotoIntentChoiceView(string title, string body, string id, UITraitCollection traits, Action choose)
    {
        AccessibilityIdentifier = id;
        AccessibilityLabel = title + ". " + body;
        AccessibilityTraits = UIAccessibilityTrait.Button;
        IsAccessibilityElement = true;
        BackgroundColor = NativeTheme.DarkVariant;
        Layer.CornerRadius = 16;
        _title = new UILabel { Text = title, Lines = 0, Font = SourceScaledText.Font(16, true, traits), TextColor = NativeTheme.OnPrimary };
        _body = new UILabel { Text = body, Lines = 0, Font = SourceScaledText.Font(12, false, traits), TextColor = NativeTheme.DarkSecondary };
        AddSubviews(_title, _body);
        TouchUpInside += (_, _) => choose();
    }
    public override CGSize SizeThatFits(CGSize size) => new(size.Width, (nfloat)Math.Max(64,
        20 + SliceUi.Measure(_title, size.Width - 32).Height + SliceUi.Measure(_body, size.Width - 32).Height));
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        var title = SliceUi.Measure(_title, Bounds.Width - 32).Height;
        var body = SliceUi.Measure(_body, Bounds.Width - 32).Height;
        _title.Frame = new CGRect(16, 8, Bounds.Width - 32, title);
        _body.Frame = new CGRect(16, 12 + title, Bounds.Width - 32, body);
    }
}
