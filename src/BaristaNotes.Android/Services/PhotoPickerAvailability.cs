namespace BaristaNotes.AndroidApp.Services;

internal enum PhotoPickerRoute { System, SystemFallback, Documents }

internal static class PhotoPickerAvailability
{
    public const string SystemAction = "android.provider.action.PICK_IMAGES";
    public const string SystemFallbackAction = "androidx.activity.result.contract.action.PICK_IMAGES";

    // Matches the pinned AndroidX PickVisualMedia capability order, not just
    // the OS release on which ACTION_PICK_IMAGES entered the public SDK.
    public static PhotoPickerRoute Select(int apiLevel, int rExtension, bool hasSystemFallback) =>
        apiLevel >= 33 || (apiLevel >= 30 && rExtension >= 2)
            ? PhotoPickerRoute.System
            : hasSystemFallback ? PhotoPickerRoute.SystemFallback : PhotoPickerRoute.Documents;
}
