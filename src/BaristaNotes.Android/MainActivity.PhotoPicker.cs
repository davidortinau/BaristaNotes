using Android.App;
using Android.Content;
using BaristaNotes.AndroidApp.Services;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private AndroidPhotoPicker? _profilePhotoPicker;

    private AndroidPhotoPicker ProfilePhotoPicker =>
        _profilePhotoPicker ??= new AndroidPhotoPicker(this, _app.LoggerFactory.CreateLogger<AndroidPhotoPicker>());

#pragma warning disable CA1422, CS0618
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (_profilePhotoPicker?.HandleResult(requestCode, resultCode, data) == true)
            return;
        if (_voiceCapture?.HandleResult(requestCode, resultCode) == true)
            return;
        if (_photoSession?.HandleResult(requestCode, resultCode) == true)
            return;
        base.OnActivityResult(requestCode, resultCode, data);
    }
#pragma warning restore CA1422, CS0618
}
