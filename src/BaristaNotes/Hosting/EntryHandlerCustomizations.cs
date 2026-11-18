namespace BaristaNotes.Hosting;

internal static class EntryHandlerCustomizations
{
    /// <summary>
    /// Applies per-platform <see cref="EntryHandler"/> and <see cref="PickerHandler"/>
    /// mappings used by form controls to strip the default platform chrome
    /// (iOS/MacCatalyst borders, Android underlines), and the Windows
    /// <see cref="ImageHandler"/> font-icon size cap.
    /// </summary>
    public static void Apply()
    {
#if IOS || MACCATALYST
        EntryHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
        {
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
            // Tiny left padding so text isn't flush.
            handler.PlatformView.LeftView = new UIKit.UIView(new CoreGraphics.CGRect(0, 0, 4, 0));
            handler.PlatformView.LeftViewMode = UIKit.UITextFieldViewMode.Always;
        });

        PickerHandler.Mapper.AppendToMapping("NoBorder", (handler, view) =>
        {
            handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
            handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
            handler.PlatformView.LeftView = new UIKit.UIView(new CoreGraphics.CGRect(0, 0, 4, 0));
            handler.PlatformView.LeftViewMode = UIKit.UITextFieldViewMode.Always;
        });
#endif

#if ANDROID
        EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
        {
            handler.PlatformView.Background = null;
            handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Transparent);
            handler.PlatformView.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);

            handler.PlatformView.SetPadding(0, handler.PlatformView.PaddingTop, 0, handler.PlatformView.PaddingBottom);
        });

        PickerHandler.Mapper.AppendToMapping("NoUnderline", (handler, view) =>
        {
            var pv = handler.PlatformView;
            pv.Background = null;
            pv.SetBackgroundColor(Android.Graphics.Color.Transparent);
            pv.BackgroundTintList =
                Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
            pv.SetPadding(0, 0, 0, 0);
        });
#endif

#if WINDOWS
        // WinUI stretches an Image up to fill its space, so an unsized font icon
        // renders far larger than its glyph Size. iOS and Android draw it at its
        // Size, so cap it to match.
        ImageHandler.Mapper.AppendToMapping("FontImageSize", (handler, view) =>
        {
            if (view is Microsoft.Maui.Controls.Image image
                && image.Source is FontImageSource font
                && image.WidthRequest < 0
                && image.HeightRequest < 0)
            {
                image.MaximumWidthRequest = font.Size;
                image.MaximumHeightRequest = font.Size;
            }
        });
#endif
    }
}
