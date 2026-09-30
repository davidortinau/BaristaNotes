using Android.App;
using Android.Views;
using Android.Widget;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class NativePhotoIntentPopup : IDisposable
{
    private readonly NativePhotoModal _modal;
    private readonly ILogger _logger;
    private readonly TaskCompletionSource<PhotoIntentChoice> _choice = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _completing;
    public NativePhotoModal Modal => _modal;

    public NativePhotoIntentPopup(Activity activity, NativeStyle style, Func<bool> ownerActive,
        Action inputChanged, ILogger logger, CancellationToken cancellation)
    {
        _logger = logger;
        _modal = new(activity, style, "Use This Photo", "PhotoIntent", false, ownerActive, inputChanged, cancellation);
        _modal.CancelRequested = () => { _ = CompleteAsync(PhotoIntentChoice.Cancel); };
        var body = style.Column();
        body.SetPadding(style.Dp(16), style.Dp(8), style.Dp(16), style.Dp(8));
        var screen = new NativeScreen(body);
        var explanation = style.Label("I could not determine the next step. What do you want to create?", 14,
            color: NativeStyle.ModalSecondary);
        body.AddView(explanation, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = style.Dp(16) });
        foreach (var (title, description, id, choice) in new[]
        {
            ("Add coffee", "Fill a new bean and bag card from the image", "PhotoIntentCoffeeButton", PhotoIntentChoice.Coffee),
            ("Create profile", "Use the image as a new profile photo", "PhotoIntentProfileButton", PhotoIntentChoice.Profile),
            ("Count people", "Calculate coffee needs for a room or group", "PhotoIntentRoomButton", PhotoIntentChoice.Room),
            ("Take another photo", "Open the camera again", "PhotoIntentRetakeButton", PhotoIntentChoice.Retake)
        })
        {
            var card = style.Column();
            card.Background = style.Rounded(NativeStyle.ModalVariant, 16);
            card.SetMinimumHeight(style.Dp(64));
            card.SetPadding(style.Dp(16), style.Dp(8), style.Dp(16), style.Dp(8));
            card.Focusable = true;
            var heading = style.Label(title, 16, color: NativeStyle.ModalText);
            heading.Typeface = style.Semibold;
            heading.ImportantForAccessibility = ImportantForAccessibility.No;
            card.AddView(heading);
            var detail = style.Label(description, 12, color: NativeStyle.ModalSecondary);
            detail.ImportantForAccessibility = ImportantForAccessibility.No;
            card.AddView(detail,
                new LinearLayout.LayoutParams(-1, -2) { TopMargin = style.Dp(4) });
            NativeStyle.Identify(card, id);
            card.ContentDescription = title + ". " + description;
            screen.Click(card, () => { if (_modal.CanInteract) _ = CompleteAsync(choice); });
            body.AddView(card, new LinearLayout.LayoutParams(-1, -2)
            {
                TopMargin = body.ChildCount > 1 ? style.Dp(8) : 0
            });
        }
        _modal.SetContent(screen);
    }
    public async Task<PhotoIntentChoice> ShowAsync(CancellationToken cancellation)
    {
        await _modal.ShowAsync();
        try { return await _choice.Task.WaitAsync(cancellation); }
        finally { Dispose(); }
    }
    private async Task CompleteAsync(PhotoIntentChoice choice)
    {
        if (_completing) return;
        _completing = true;
        try
        {
            await _modal.CloseAsync();
            _choice.TrySetResult(choice);
        }
        catch (OperationCanceledException) { _choice.TrySetCanceled(); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Photo intent popup exit failed");
            _choice.TrySetException(exception);
        }
    }
    public void Dispose()
    {
        _choice.TrySetResult(PhotoIntentChoice.Cancel);
        _modal.Dispose();
    }
}
