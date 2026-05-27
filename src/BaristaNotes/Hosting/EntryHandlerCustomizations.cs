namespace BaristaNotes.Hosting;

internal static class EntryHandlerCustomizations
{
    /// <summary>
    /// Applies per-platform <see cref="EntryHandler"/> and <see cref="PickerHandler"/>
    /// mappings used by form controls to strip the default platform chrome
    /// (iOS/MacCatalyst borders, Android underlines).
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
    }
}
