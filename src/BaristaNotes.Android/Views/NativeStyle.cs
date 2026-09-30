using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace BaristaNotes.AndroidApp.Views;

internal sealed partial class NativeStyle : IDisposable
{
    public Context Context { get; }
    public Typeface Regular { get; }
    public Typeface Bold { get; }
    public Typeface Semibold { get; }
    public Typeface Symbols { get; }
    public bool IsDark { get; private set; }
    private NativePalette Palette => IsDark ? NativePalette.Dark : NativePalette.Light;
    public Color Surface => new(Palette.Surface);
    public Color SurfaceVariant => new(Palette.Variant);
    public Color Text => new(Palette.Text);
    public Color Secondary => new(Palette.Secondary);
    public Color Outline => new(Palette.Outline);
    public Color Primary => Color.ParseColor("#86543F");
    public Color Error => Color.ParseColor("#EF5350");
    public static Color Warning => Color.ParseColor("#FFA726");
    public static Color ModalSurface => new(NativePalette.Dark.Surface);
    public static Color ModalElevated => Color.ParseColor("#B3A291");
    public static Color ModalVariant => new(NativePalette.Dark.Variant);
    public static Color ModalText => new(NativePalette.Dark.Text);
    public static Color ModalSecondary => new(NativePalette.Dark.Secondary);
    public static Color ModalOutline => new(NativePalette.Dark.Outline);

    public NativeStyle(Context context, bool? isDark = null)
    {
        Context = context;
        IsDark = isDark ?? ((context.Resources!.Configuration!.UiMode & UiMode.NightMask) == UiMode.NightYes);
        Regular = LoadFont("Manrope-Regular.ttf");
        Semibold = LoadFont("Manrope-SemiBold.ttf");
        Symbols = LoadFont("MaterialSymbols.ttf");
        Bold = OperatingSystem.IsAndroidVersionAtLeast(28)
            ? Typeface.Create(Regular, 700, false)
                ?? throw new InvalidOperationException("The source-weight Manrope font is unavailable.")
            : Typeface.Create(Regular, TypefaceStyle.Bold)
                ?? throw new InvalidOperationException("The source-weight Manrope font is unavailable.");
    }

    private Typeface LoadFont(string name) =>
        Typeface.CreateFromAsset(Context.Assets, $"Fonts/{name}")
        ?? throw new InvalidOperationException($"Bundled font {name} is unavailable.");

    public int Dp(double value) =>
        (int)Math.Round(value * Context.Resources!.DisplayMetrics!.Density);

    public TextView Label(string text, float size = 16, bool bold = false, Color? color = null)
    {
        var label = new TextView(Context) { Text = text, Typeface = bold ? Bold : Regular };
        label.SetTextSize(ComplexUnitType.Sp, size);
        label.SetTextColor(color ?? Text);
        label.SetIncludeFontPadding(true);
        return Track(label);
    }

    public Button Button(string text, string id, Color? color = null)
    {
        var button = new Button(Context) { Text = text, Typeface = Regular };
        button.SetAllCaps(false);
        button.SetTextSize(ComplexUnitType.Sp, 14);
        button.SetTextColor(color ?? Primary);
        button.SetBackgroundColor(Color.Transparent);
        button.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
        button.SetMinHeight(Dp(44));
        button.SetMinimumHeight(Dp(44));
        Identify(button, id);
        return Track(button);
    }

    public LinearLayout Column() => Track(new LinearLayout(Context) { Orientation = Android.Widget.Orientation.Vertical });
    public LinearLayout Row() => Track(new LinearLayout(Context) { Orientation = Android.Widget.Orientation.Horizontal });
    public LinearLayout.LayoutParams Fill(float weight = 0) => weight > 0
        ? new(ViewGroup.LayoutParams.MatchParent, 0, weight)
        : new(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
    public LinearLayout.LayoutParams Share() => new(0, ViewGroup.LayoutParams.MatchParent, 1);

    public GradientDrawable Rounded(Color fill, float radius, Color? stroke = null, float strokeWidth = 1) =>
        new PaletteDrawable(fill, Dp(radius), stroke, Dp(strokeWidth));

    public static void Identify(View view, string id)
    {
#if DEBUG
        // This is the official native Views tag API, not a separate automation layer.
        using var value = new Java.Lang.String(id);
        view.SetTag(Resource.Id.ailoha_automation_id, value);
#endif
    }

    public void Dispose()
    {
        _appearanceViews.Dispose();
        _fixedThemeViews.Clear();
        Regular.Dispose();
        Bold.Dispose();
        Semibold.Dispose();
        Symbols.Dispose();
    }
}

internal sealed class NativeScreen(View root) : IDisposable
{
    private readonly List<Action> _cleanup = [];
    public View Root { get; } = root;

    public void Click(View view, Action action)
    {
        EventHandler handler = (_, _) => action();
        view.Click += handler;
        _cleanup.Add(() => view.Click -= handler);
    }

    public void Own(IDisposable resource) => _cleanup.Add(resource.Dispose);
    public void OnDispose(Action release) => _cleanup.Add(release);

    public void Dispose()
    {
        foreach (var release in _cleanup)
            release();
        _cleanup.Clear();
        Root.Dispose();
    }
}
