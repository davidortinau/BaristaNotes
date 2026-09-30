using Android.App;
using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class NativeAdvicePopup : IDisposable
{
    private readonly Activity _activity;
    private readonly NativeScreen _screen;
    private readonly FrameLayout _backdrop;
    private readonly LinearLayout _panel;
    private readonly View _closeIcon;
    private readonly Button _closeButton;
    private readonly LinearLayout? _promptToggle;
    private readonly Func<bool> _canInteract;
    private readonly CancellationTokenSource _lifetime;
    private readonly CancellationToken _cancellation;
    private readonly TaskCompletionSource _close = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _ready;
    private bool _disposed;

    public NativeAdvicePopup(Activity activity, NativeStyle style, AIAdviceResponseDto advice,
        Func<bool> canInteract, CancellationToken cancellation)
    {
        _activity = activity;
        _canInteract = canInteract;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _cancellation = _lifetime.Token;
        _backdrop = new FrameLayout(activity) { Clickable = true };
        _backdrop.SetFitsSystemWindows(false);
        _backdrop.SetBackgroundColor(Color.Argb(170, 0, 0, 0));
        NativeStyle.Identify(_backdrop, "AdviceBackdrop");
        _screen = new NativeScreen(_backdrop);
        _screen.Click(_backdrop, RequestClose);
        var window = activity.Window?.DecorView
            ?? throw new InvalidOperationException("The advice inset window is unavailable.");
        var safeHost = new AdviceSafeHost(activity, window);
        safeHost.SetFitsSystemWindows(false);
        _screen.Own(safeHost);
        NativeStyle.Identify(safeHost, "AdviceSafeHost");
        _backdrop.AddView(safeHost, new FrameLayout.LayoutParams(-1, -1));
        _panel = style.Column();
        _panel.SetFitsSystemWindows(false);
        _panel.Clickable = true;
        _panel.Background = style.Rounded(NativeStyle.ModalSurface, 24);
        var designPadding = style.Dp(AdvicePanelBounds.DesignPaddingDp);
        _panel.SetPadding(designPadding, designPadding, designPadding, designPadding);
        NativeStyle.Identify(_panel, "AdvicePanel");
        safeHost.AddView(_panel, new FrameLayout.LayoutParams(-1, -1));

        var header = new FrameLayout(activity);
        var title = style.Label(AdvicePresentation.Title, 18, color: NativeStyle.ModalText);
        title.Typeface = style.Semibold;
        title.Gravity = GravityFlags.Center;
        NativeStyle.Identify(title, "AdviceTitle");
        header.AddView(title, new FrameLayout.LayoutParams(-1, -2));
        var closeIcon = style.Label("\ue14c", 24, color: NativeStyle.ModalText);
        closeIcon.Typeface = style.Symbols;
        closeIcon.Gravity = GravityFlags.Top | GravityFlags.End;
        closeIcon.ContentDescription = "Close advice";
        closeIcon.Focusable = true;
        NativeStyle.Identify(closeIcon, "AdviceHeaderClose");
        header.AddView(closeIcon, new FrameLayout.LayoutParams(style.Dp(44), -2, GravityFlags.End)
        {
            TopMargin = -style.Dp(4)
        });
        _closeIcon = closeIcon;
        _screen.Click(closeIcon, RequestClose);
        _panel.AddView(header);

        var bodySlot = new FrameLayout(activity);
        _panel.AddView(bodySlot, new LinearLayout.LayoutParams(-1, 0, 1) { TopMargin = style.Dp(16) });
        var scroll = new AdviceScrollView(activity, style);
        _screen.Own(scroll);
        NativeStyle.Identify(scroll, "AdviceScroll");
        var body = style.Column();
        body.SetPadding(style.Dp(16), 0, style.Dp(16), 0);
        scroll.AddView(body, new ScrollView.LayoutParams(-1, -2));
        bodySlot.AddView(scroll, new FrameLayout.LayoutParams(-1, -2));
        TextView Label(string text, int size, Color color)
        {
            var label = style.Label(text, size, color: color);
            // These source Controls.Label instances have no custom font family.
            label.Typeface = Typeface.Default;
            return label;
        }
        void Add(View child, int extraTop = 0)
        {
            body.AddView(child, new LinearLayout.LayoutParams(-1, -2)
            {
                TopMargin = style.Dp((body.ChildCount == 0 ? 0 : 12) + extraTop)
            });
        }
        if (advice.Adjustments is { Count: > 0 } adjustments)
        {
            foreach (var adjustment in adjustments)
            {
                var row = style.Row();
                row.SetGravity(GravityFlags.Top);
                row.AddView(Label("•", 14, NativeStyle.ModalSecondary),
                    new LinearLayout.LayoutParams(-2, -2) { RightMargin = style.Dp(8) });
                row.AddView(Label(AdvicePresentation.Adjustment(adjustment), 14, NativeStyle.ModalText),
                    new LinearLayout.LayoutParams(0, -2, 1));
                Add(row);
            }
        }
        else Add(Label(AdvicePresentation.EmptyAdjustments, 14, NativeStyle.ModalSecondary));
        if (!string.IsNullOrWhiteSpace(advice.Reasoning))
        {
            var reason = Label(advice.Reasoning, 13, NativeStyle.ModalSecondary);
            var italic = Typeface.Create(Typeface.Default, TypefaceStyle.Italic)
                ?? throw new InvalidOperationException("The advice reasoning font is unavailable.");
            _screen.Own(italic);
            reason.Typeface = italic;
            NativeStyle.Identify(reason, "AdviceReasoning");
            Add(reason, 4);
        }
        var sourceRow = style.Row();
        sourceRow.SetGravity(GravityFlags.CenterVertical);
        TextView? prompt = null;
        if (!string.IsNullOrWhiteSpace(advice.PromptSent))
        {
            var toggle = style.Row();
            toggle.SetGravity(GravityFlags.CenterVertical);
            toggle.SetPadding(style.Dp(10), style.Dp(6), style.Dp(10), style.Dp(6));
            toggle.Background = style.Rounded(NativeStyle.ModalVariant, 12);
            toggle.Focusable = toggle.Clickable = true;
            var glyph = Label("▸", 12, NativeStyle.ModalText);
            var label = Label("Show prompt", 12, NativeStyle.ModalText);
            label.Typeface = Typeface.DefaultBold;
            toggle.AddView(glyph);
            toggle.AddView(label, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = style.Dp(6) });
            sourceRow.AddView(toggle, new LinearLayout.LayoutParams(-2, -2) { RightMargin = style.Dp(8) });
            NativeStyle.Identify(toggle, "AdvicePromptToggle");
            toggle.ContentDescription = "Show prompt";
            prompt = style.Label(AdvicePresentation.Prompt(advice.PromptSent, advice.HistoricalShotsCount),
                11, color: NativeStyle.ModalSecondary);
            prompt.Visibility = ViewStates.Gone;
            NativeStyle.Identify(prompt, "AdvicePrompt");
            var show = false;
            _screen.Click(toggle, () =>
            {
                if (!_ready || _disposed || !_canInteract()) return;
                show = !show;
                glyph.Text = show ? "▾" : "▸";
                label.Text = show ? "Hide prompt" : "Show prompt";
                toggle.ContentDescription = label.Text;
                // Keep the tap owner, ScrollView and Close controls alive.
                // Replacing the subtree during a tap swallows source toggles.
                prompt.Visibility = show ? ViewStates.Visible : ViewStates.Gone;
                scroll.RequestLayout();
            });
            _promptToggle = toggle;
        }
        var source = Label(advice.Source ?? "", 12, NativeStyle.ModalSecondary);
        source.Gravity = GravityFlags.End | GravityFlags.CenterVertical;
        NativeStyle.Identify(source, "AdviceSource");
        sourceRow.AddView(source, new LinearLayout.LayoutParams(0, -2, 1));
        Add(sourceRow, 8);
        if (prompt is not null) Add(prompt, 4);

        _closeButton = style.Button("Close", "AdviceClose", NativeStyle.ModalText);
        _closeButton.Typeface = Typeface.Default;
        _closeButton.SetTextSize(Android.Util.ComplexUnitType.Sp, 16);
        _closeButton.SetPadding(style.Dp(20), style.Dp(4), style.Dp(20), style.Dp(4));
        _closeButton.Background = style.Rounded(style.Primary, 20);
        _screen.Click(_closeButton, RequestClose);
        _panel.AddView(_closeButton, new LinearLayout.LayoutParams(-1, style.Dp(54)) { TopMargin = style.Dp(16) });
        SetReady(false);
        style.FixTheme(_backdrop);
    }

    public async Task ShowAsync()
    {
        _cancellation.ThrowIfCancellationRequested();
        var window = _activity.Window?.DecorView as ViewGroup
            ?? throw new InvalidOperationException("The advice popup window is unavailable.");
        using var registration = _cancellation.Register(() => _close.TrySetCanceled(_cancellation));
        try
        {
            _panel.TranslationY = window.Height;
            window.AddView(_backdrop, new FrameLayout.LayoutParams(-1, -1));
            await NativeModalMotion.RunAsync(_panel, _backdrop, entering: true, _cancellation);
            _cancellation.ThrowIfCancellationRequested();
            SetReady(true);
            await _close.Task;
            _cancellation.ThrowIfCancellationRequested();
            SetReady(false);
            await NativeModalMotion.RunAsync(_panel, _backdrop, entering: false, _cancellation);
        }
        finally { Dispose(); }
    }

    public void RequestClose()
    {
        if (!_ready || _disposed || !_canInteract()) return;
        SetReady(false);
        _close.TrySetResult();
    }

    private void SetReady(bool ready)
    {
        _ready = ready;
        _closeButton.Enabled = _closeIcon.Enabled = ready;
        if (_promptToggle is not null) _promptToggle.Enabled = ready;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ready = false;
        _lifetime.Cancel();
        _close.TrySetCanceled(_cancellation);
        (_backdrop.Parent as ViewGroup)?.RemoveView(_backdrop);
        _screen.Dispose();
        _lifetime.Dispose();
    }

    private sealed class AdviceScrollView(Activity activity, NativeStyle style) : ScrollView(style.Context)
    {
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            // The pinned DeviceDisplay implementation uses real display metrics,
            // not the Activity's inset-adjusted resource height.
            using var metrics = new Android.Util.DisplayMetrics();
#pragma warning disable CA1422, CS0618
            using var display = activity.WindowManager?.DefaultDisplay
                ?? throw new InvalidOperationException("The advice display is unavailable.");
            display.GetRealMetrics(metrics);
#pragma warning restore CA1422, CS0618
            if (metrics.Density <= 0)
                throw new InvalidOperationException("The advice display density is unavailable.");
            var maximum = style.Dp(AdvicePresentation.MaximumBodyHeight(metrics.HeightPixels / metrics.Density));
            var available = MeasureSpec.GetMode(heightMeasureSpec) == MeasureSpecMode.Unspecified
                ? maximum : Math.Min(maximum, MeasureSpec.GetSize(heightMeasureSpec));
            base.OnMeasure(widthMeasureSpec, MeasureSpec.MakeMeasureSpec(available, MeasureSpecMode.AtMost));
        }
    }
}
