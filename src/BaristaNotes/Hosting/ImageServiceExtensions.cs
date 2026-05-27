namespace BaristaNotes.Hosting;

internal static class ImageServiceExtensions
{
    public static MauiAppBuilder AddImageServices(this MauiAppBuilder builder)
    {
        builder.Services
            .AddSingleton<Microsoft.Maui.Media.IMediaPicker>(Microsoft.Maui.Media.MediaPicker.Default)
            .AddSingleton<IImagePickerService, ImagePickerService>()
            .AddSingleton<IImageProcessingService, ImageProcessingService>()
            .AddSingleton<IVisionService, VisionService>();

        return builder;
    }
}
