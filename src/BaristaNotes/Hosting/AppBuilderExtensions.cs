namespace BaristaNotes.Hosting;

internal static class AppBuilderExtensions
{
    public static MauiAppBuilder ConfigureBaristaApp(this MauiAppBuilder builder)
    {
        builder
            .UseMauiReactorApp<BaristaApp>(app =>
            {
                app.UseTheme<ApplicationTheme>();
                app.SetWindowsSpecificAssetsDirectory("Assets");
                app.Resources.MergedDictionaries.Add(new UXDivers.Popups.Maui.Controls.DarkTheme());
                app.Resources.MergedDictionaries.Add(new UXDivers.Popups.Maui.Controls.PopupStyles());
                app.Resources.MergedDictionaries.Add(BuildCustomResources());
            })
            .UseUXDiversPopups()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Manrope-Regular.ttf", "Manrope");
                fonts.AddFont("Manrope-SemiBold.ttf", "ManropeSemibold");
                fonts.AddFont("MaterialSymbols.ttf", MaterialSymbolsFont.FontFamily);
                fonts.AddFont("coffee-icons.ttf", "coffee-icons");
            })
            .ConfigureMauiHandlers(_ => EntryHandlerCustomizations.Apply());

        return builder;
    }

    private static ResourceDictionary BuildCustomResources() => new()
    {
        // Font Families
        { "IconsFontFamily", MaterialSymbolsFont.FontFamily },
        { "AppFontFamily", "Manrope" },
        { "AppSemiBoldFamily", "ManropeSemibold" },

        // UXDivers Popups icon overrides
        { "UXDPopupsCloseIconButton", MaterialSymbolsFont.Close },
        { "UXDPopupsCheckCircleIconButton", MaterialSymbolsFont.Check_circle },

        // UXDivers Popups theme colors
        { "BackgroundColor", AppColors.Dark.Surface },
        { "BackgroundSecondaryColor", AppColors.Dark.Surface },
        { "BackgroundTertiaryColor", Colors.Red },
        { "PrimaryColor", AppColors.Dark.Primary },
        { "PrimaryVariantColor", AppColors.Dark.SurfaceElevated },
        { "TextColor", AppColors.Dark.TextPrimary },
        { "TextTertiaryColor", AppColors.Dark.TextSecondary },
        { "PopupBackgroundColor", AppColors.Dark.SurfaceElevated },
        { "PopupBorderColor", AppColors.Dark.Outline },
    };
}
