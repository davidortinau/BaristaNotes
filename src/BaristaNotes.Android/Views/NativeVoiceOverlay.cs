using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;
using BaristaNotes.Core.Services;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class NativeVoiceOverlay : IOverlayService, IDisposable
{
    private readonly VoiceRoot _root;
    private readonly NativeScreen _screen;
    private readonly FrameLayout _panel;
    private readonly TextView _state, _indicator, _transcript, _response, _readyHint, _speakHint;
    private readonly Button _close, _minimize, _fab;
    private readonly FrameLayout _micHit;
    private readonly ImageView _fabImage;
    private readonly VoiceMicDrawable _micGlyph = new(false), _fabGlyph = new(true);
    private readonly Action _inputChanged;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<View, ImportantForAccessibility> _hiddenSiblings = [];
    private OverlayContent _content = new("Ready", "", false, false, true);
    private readonly float _density;
    private readonly VoiceInteractionState _interaction = new();
    private bool _disposed;
    public bool IsVisible => _interaction.IsVisible;
    public bool IsCollapsed => _interaction.IsCollapsed;
    public bool BlocksInput => _interaction.BlocksInput;
    public event EventHandler<bool>? VisibilityChanged;
    public event EventHandler? CloseRequested;
    public event EventHandler? ExpandRequested;
    public event EventHandler? MicPressStarted;
    public event EventHandler? MicPressEnded;

    public NativeVoiceOverlay(Activity activity, NativeStyle style, Action inputChanged)
    {
        _inputChanged = inputChanged;
        _density = activity.Resources!.DisplayMetrics!.Density;
        _root = new VoiceRoot(activity) { Visibility = ViewStates.Gone };
        _root.SetBackgroundColor(Color.Transparent);
        _root.SetClipChildren(false);
        _root.Elevation = 10000;
        NativeStyle.Identify(_root, "VoiceWindowOverlay");
        _screen = new NativeScreen(_root);
        _screen.OnDispose(() => _root.BeforeMeasure = null);
        _screen.Click(_root, () => { if (BlocksInput) Collapse(); });
        _panel = new FrameLayout(activity) { Clickable = true };
        var background = new GradientDrawable();
        background.SetColor(Color.ParseColor("#1E1E1E"));
        background.SetCornerRadii([Dp(20), Dp(20), Dp(20), Dp(20), 0, 0, 0, 0]);
        _panel.Background = background;
        _screen.Own(background);
        _root.AddView(_panel);
        NativeStyle.Identify(_panel, "VoicePanel");
        var normal = Typeface.Create("Arial", TypefaceStyle.Normal) ?? Typeface.Default!;
        var bold = Typeface.Create("Arial", TypefaceStyle.Bold) ?? Typeface.DefaultBold!;
        var italic = Typeface.Create("Arial", TypefaceStyle.Italic) ?? Typeface.Default!;
        // Arial resolves through Android's shared system-font cache. These are
        // not privately loaded font assets owned by the overlay.
        TextView Text(string text, int size, Color color, Typeface face, string id, GravityFlags gravity)
        {
            var label = new TextView(activity) { Text = text, Typeface = face, Gravity = gravity };
            // The source is an ICanvas renderer with logical units, not a
            // FontAutoScalingEnabled Label. Preserve those DIP font metrics.
            label.SetTextSize(ComplexUnitType.Dip, size);
            label.SetTextColor(color);
            label.SetIncludeFontPadding(false);
            label.SetPadding(0, 0, 0, 0);
            NativeStyle.Identify(label, id);
            _panel.AddView(label);
            return label;
        }
        _state = Text("Ready", 20, Color.White, bold, "VoiceState", GravityFlags.CenterVertical | GravityFlags.Start);
        _state.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        _indicator = Text("● ", 20, Color.ParseColor("#FF9500"), bold, "VoiceActivityIndicator", GravityFlags.CenterVertical);
        _indicator.ImportantForAccessibility = ImportantForAccessibility.No;
        _transcript = Text("", 16, Color.ParseColor("#CCCCCC"), normal, "VoiceTranscript", GravityFlags.Top);
        _response = Text("", 15, Color.ParseColor("#90EE90"), italic, "VoiceResponse", GravityFlags.Top);
        _readyHint = Text("Say something like \"Log shot 18 in, 36 out, 28 seconds\"", 14,
            Color.ParseColor("#888888"), normal, "VoiceReadyHint", GravityFlags.Top | GravityFlags.CenterHorizontal);
        _speakHint = Text("Hold to speak", 13, Color.ParseColor("#888888"), normal,
            "VoiceHoldHint", GravityFlags.Center);
        Button Button(string text, int size, Color color, string id)
        {
            var button = new Button(activity) { Text = text, Typeface = normal, Gravity = GravityFlags.Center };
            button.SetTextSize(ComplexUnitType.Dip, size);
            button.SetTextColor(color);
            button.SetAllCaps(false);
            button.SetPadding(0, 0, 0, 0);
            button.SetMinWidth(0); button.SetMinimumWidth(0);
            button.SetMinHeight(0); button.SetMinimumHeight(0);
            button.SetBackgroundColor(Color.Transparent);
            NativeStyle.Identify(button, id);
            return button;
        }
        _close = Button("✕", 24, Color.White, "VoiceClose");
        _close.ContentDescription = "End voice session";
        _minimize = Button("−", 22, Color.ParseColor("#CCCCCC"), "VoiceCollapse");
        _minimize.ContentDescription = "Minimize voice";
        _panel.AddView(_close); _panel.AddView(_minimize);
        _screen.Click(_close, () => { _ = DelayedCloseAsync(); });
        _screen.Click(_minimize, Collapse);
        _micHit = new FrameLayout(activity) { Clickable = true, Focusable = true };
        _micHit.SetBackgroundColor(Color.Transparent);
        var micImage = new ImageView(activity) { ImportantForAccessibility = ImportantForAccessibility.No };
        micImage.SetImageDrawable(_micGlyph);
        _micHit.AddView(micImage, new FrameLayout.LayoutParams(Dp(96), Dp(96), GravityFlags.Center));
        _panel.AddView(_micHit);
        NativeStyle.Identify(_micHit, "VoiceMic");
        _screen.Click(_micHit, MicTapped);
        _fabImage = new ImageView(activity) { ImportantForAccessibility = ImportantForAccessibility.No };
        _fabImage.SetImageDrawable(_fabGlyph);
        _root.AddView(_fabImage);
        _fab = Button("", 16, Color.White, "VoiceFab");
        _fab.ContentDescription = "Expand voice";
        _root.AddView(_fab);
        _screen.Click(_fab, () => { if (IsVisible && IsCollapsed) ExpandRequested?.Invoke(this, EventArgs.Empty); });
        _screen.Own(_micGlyph); _screen.Own(_fabGlyph);
        _root.BeforeMeasure = Layout;
        style.FixTheme(_root);
        var window = activity.Window?.DecorView as ViewGroup
            ?? throw new InvalidOperationException("Voice window is unavailable.");
        window.AddView(_root, new FrameLayout.LayoutParams(-1, -1));
        Render();
    }

    public void Show()
    {
        if (_disposed) return;
        _interaction.Show();
        _root.Visibility = ViewStates.Visible;
        _root.BringToFront();
        Render();
        VisibilityChanged?.Invoke(this, true);
    }
    public void Hide()
    {
        if (_disposed || !IsVisible) return;
        _interaction.Hide();
        _root.Visibility = ViewStates.Gone;
        Render();
        VisibilityChanged?.Invoke(this, false);
    }
    public void Collapse()
    {
        if (_disposed || !IsVisible || IsCollapsed) return;
        _interaction.Collapse();
        Render();
    }
    public void Expand()
    {
        if (_disposed || !IsVisible || !IsCollapsed) return;
        _interaction.Expand();
        Render();
    }
    public void UpdateContent(OverlayContent content)
    {
        if (_disposed) return;
        _content = content;
        Render();
    }
    public void BringToFront()
    {
        if (IsVisible) _root.BringToFront();
        UpdateInput();
    }
    private void MicTapped()
    {
        var action = _interaction.TapMic(_content.IsProcessing);
        if (action == VoiceMicAction.None) return;
        Render();
        if (action == VoiceMicAction.Start) MicPressStarted?.Invoke(this, EventArgs.Empty);
        else MicPressEnded?.Invoke(this, EventArgs.Empty);
    }
    private async Task DelayedCloseAsync()
    {
        try
        {
            await Task.Delay(100, _lifetime.Token);
            if (IsVisible && !_disposed) CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }
    private void Render()
    {
        _root.Clickable = BlocksInput;
        _panel.Visibility = IsVisible && !IsCollapsed ? ViewStates.Visible : ViewStates.Gone;
        _fab.Visibility = _fabImage.Visibility = IsVisible && IsCollapsed ? ViewStates.Visible : ViewStates.Gone;
        _state.Text = _content.StateText;
        _transcript.Text = _content.Transcript;
        _response.Text = _content.AIResponse ?? "";
        _transcript.Visibility = string.IsNullOrEmpty(_content.Transcript) ? ViewStates.Gone : ViewStates.Visible;
        _response.Visibility = string.IsNullOrEmpty(_content.AIResponse) ? ViewStates.Gone : ViewStates.Visible;
        _indicator.Visibility = _content.IsListening || _content.IsProcessing ? ViewStates.Visible : ViewStates.Gone;
        _readyHint.Visibility = _content.IsReady && string.IsNullOrEmpty(_content.Transcript)
            && string.IsNullOrEmpty(_content.AIResponse) ? ViewStates.Visible : ViewStates.Gone;
        _speakHint.Visibility = _content.IsReady && !_content.IsListening && !_content.IsProcessing ? ViewStates.Visible : ViewStates.Gone;
        _micHit.Enabled = !_content.IsProcessing;
        _micHit.ContentDescription = _content.IsProcessing ? "Microphone unavailable while processing"
            : _interaction.MicActive ? "Stop listening" : "Start listening";
        _micGlyph.Update(_interaction.MicActive, _content.IsListening, _content.IsProcessing);
        _fabGlyph.Update(false, _content.IsListening, _content.IsProcessing);
        Layout();
        UpdateInput();
    }
    private void UpdateInput()
    {
        if (BlocksInput && _root.Parent is ViewGroup window)
        {
            foreach (var stale in _hiddenSiblings.Keys.Where(view => view.Handle == IntPtr.Zero || !ReferenceEquals(view.Parent, window)).ToArray())
                _hiddenSiblings.Remove(stale);
            for (var index = 0; index < window.ChildCount; index++)
            {
                if (window.GetChildAt(index) is not { } sibling || ReferenceEquals(sibling, _root)) continue;
                _hiddenSiblings.TryAdd(sibling, sibling.ImportantForAccessibility);
                sibling.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
            }
        }
        else
        {
            foreach (var (view, appearance) in _hiddenSiblings)
                if (view.Handle != IntPtr.Zero) view.ImportantForAccessibility = appearance;
            _hiddenSiblings.Clear();
        }
        _inputChanged();
    }
    private int Dp(double value) => (int)Math.Round(value * _density);
    private void Layout() => Layout(_root.Width, _root.Height);
    private void Layout(int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        var g = VoiceGeometry.Calculate(width / _density, height / _density, !string.IsNullOrEmpty(_content.Transcript));
        Place(_panel, g.Panel); Place(_close, g.Close); Place(_minimize, g.Minimize);
        Place(_indicator, g.Indicator); Place(_state, g.State); Place(_transcript, g.Transcript);
        Place(_response, g.Response); Place(_readyHint, g.ReadyHint); Place(_micHit, g.MicHit);
        Place(_speakHint, g.SpeakHint); Place(_fab, g.Fab);
        Place(_fabImage, new(g.Fab.X - 5, g.Fab.Y - 5, 66, 66));
    }
    private void Place(View view, VoiceRect rect) => view.LayoutParameters =
        new FrameLayout.LayoutParams(Math.Max(0, Dp(rect.Width)), Math.Max(0, Dp(rect.Height)))
        { LeftMargin = Dp(rect.X), TopMargin = Dp(rect.Y) };
    public void Dispose()
    {
        if (_disposed) return;
        Hide();
        _disposed = true;
        _lifetime.Cancel();
        (_root.Parent as ViewGroup)?.RemoveView(_root);
        _screen.Dispose();
        _lifetime.Dispose();
        VisibilityChanged = null; CloseRequested = null; ExpandRequested = null;
        MicPressStarted = null; MicPressEnded = null;
    }
    private sealed class VoiceRoot(Activity activity) : FrameLayout(activity)
    {
        private int _layoutWidth, _layoutHeight;
        public Action<int, int>? BeforeMeasure { get; set; }
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            var width = MeasureSpec.GetSize(widthMeasureSpec);
            var height = MeasureSpec.GetSize(heightMeasureSpec);
            if (width > 0 && height > 0 && (width != _layoutWidth || height != _layoutHeight))
            {
                _layoutWidth = width;
                _layoutHeight = height;
                BeforeMeasure?.Invoke(width, height);
            }
            base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        }
    }
}
