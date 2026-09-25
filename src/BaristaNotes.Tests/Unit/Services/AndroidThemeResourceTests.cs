using System.Xml.Linq;
using BaristaNotes.Styles;

namespace BaristaNotes.Tests.Unit.Services;

public class AndroidThemeResourceTests
{
    [Theory]
    [InlineData("values", false)]
    [InlineData("values-night", true)]
    public void NativeStartupPalette_MatchesManagedSurfaceAndIconContrast(string qualifier, bool dark)
    {
        var resources = Load(qualifier, "colors.xml").Root!;
        var surface = resources.Elements("color").Single(e => (string?)e.Attribute("name") == "barista_background");
        var icons = resources.Elements("bool").Single(e => (string?)e.Attribute("name") == "barista_light_status_bar");

        Assert.Equal((dark ? AppColors.Dark.Surface : AppColors.Light.Surface).ToArgbHex(),
            Color.FromArgb(surface.Value).ToArgbHex());
        Assert.Equal(!dark, bool.Parse(icons.Value));
    }

    [Theory]
    [InlineData("Barista.MainTheme", "Maui.MainTheme.NoActionBar", "android:windowBackground")]
    [InlineData("Barista.SplashTheme", "Maui.SplashTheme", "android:windowSplashScreenBackground")]
    public void NativeThemes_UseMatchingWindowAndStatusBar(string name, string parent, string backgroundAttribute)
    {
        var style = Load("values", "styles.xml").Root!.Elements("style")
            .Single(e => (string?)e.Attribute("name") == name);
        var items = style.Elements("item").ToDictionary(e => (string)e.Attribute("name")!, e => e.Value);

        Assert.Equal(parent, (string?)style.Attribute("parent"));
        Assert.Equal("@color/barista_background", items[backgroundAttribute]);
        Assert.Equal("@color/barista_background", items["android:statusBarColor"]);
        Assert.Equal("@bool/barista_light_status_bar", items["android:windowLightStatusBar"]);
    }

    [Fact]
    public void PreAndroid12Splash_UsesSameBackgroundAndGeneratedLogo()
    {
        XNamespace android = "http://schemas.android.com/apk/res/android";
        var layers = Load("drawable", "barista_splash.xml").Root!.Elements("item").ToArray();

        Assert.Equal("@color/barista_background", (string?)layers[0].Attribute(android + "drawable"));
        Assert.Equal("@drawable/maui_splash_image", (string?)layers[1].Attribute(android + "drawable"));
    }

    private static XDocument Load(string directory, string name)
        => XDocument.Load(Path.Combine(AppContext.BaseDirectory, "AndroidResources", directory, name));
}
