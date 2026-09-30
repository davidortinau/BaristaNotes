using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class NativeVoicePlatformActions(ILogger<NativeVoicePlatformActions> logger, NativeCameraCapture camera) : IVoicePlatformActions
{
    private WeakReference<SliceNavigationController>? _host;
    public void Attach(SliceNavigationController host) => _host = new(host);
    internal void Detach(SliceNavigationController host)
    {
        if (_host?.TryGetTarget(out var attached) == true && ReferenceEquals(attached, host)) _host = null;
    }
    public bool IsCaptureSupported => NativeCameraCapture.IsSupported;

    public void Navigate(VoiceNavigationRequest request)
    {
        // Deliberately queued, unlike inline-on-main speech state updates.
        // The shared tool acknowledges dispatch, not a completed transition.
        UIApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (_host == null || !_host.TryGetTarget(out var host)) throw new InvalidOperationException("No native navigation window.");
                var plan = VoiceRoutePlan.From(request);
                if (plan.IgnoredQuery is not null)
                {
                    logger.LogWarning(
                        "Voice route query is not applied; preserving the source filtered-history limitation: {Route}",
                        request.Route);
                }
                if (plan.EntityId is int id)
                {
                    switch (plan.Kind)
                    {
                        case VoiceRouteKind.Shot: host.Edit(id); break;
                        case VoiceRouteKind.Profile: host.PushVoicePage(() => new ProfileCreateViewController(host, id)); break;
                        case VoiceRouteKind.Bean: host.PushVoicePage(() => new BeanDetailViewController(host, id)); break;
                        case VoiceRouteKind.EquipmentDetail: host.PushVoicePage(() => new EquipmentFormViewController(host, id)); break;
                        case VoiceRouteKind.Bag:
                            host.PushVoicePage(() => new BagDetailViewController(
                                host, plan.BeanId ?? 0, plan.BeanName ?? "", id));
                            break;
                        default: throw new ArgumentException("Unsupported voice detail route.", nameof(request));
                    }
                }
                else
                {
                    switch (plan.Kind)
                    {
                        case VoiceRouteKind.Drink:
                        case VoiceRouteKind.Shot: host.NewDrink(); break;
                        case VoiceRouteKind.History: host.Activity(); break;
                        case VoiceRouteKind.Settings: host.Settings(); break;
                        case VoiceRouteKind.Beans: host.Beans(); break;
                        case VoiceRouteKind.Equipment: host.Equipment(); break;
                        case VoiceRouteKind.Profiles: host.Profiles(); break;
                        case VoiceRouteKind.Bean: host.PushVoicePage(() => new BeanDetailViewController(host)); break;
                        case VoiceRouteKind.Profile: host.PushVoicePage(() => new ProfileCreateViewController(host)); break;
                        case VoiceRouteKind.EquipmentDetail: host.PushVoicePage(() => new EquipmentFormViewController(host)); break;
                        case VoiceRouteKind.Bag:
                            host.PushVoicePage(() => new BagDetailViewController(
                                host, plan.BeanId ?? 0, plan.BeanName ?? ""));
                            break;
                        case VoiceRouteKind.Ranges: host.Ranges(DrinkValueMetric.DoseIn); break;
                        default: throw new ArgumentException("Unsupported voice route.", nameof(request));
                    }
                }
                logger.LogInformation("Voice navigation dispatched to {Route} with entity {EntityId}", request.Route, request.EntityId);
            }
            catch (Exception error) { logger.LogError(error, "Queued voice navigation failed for {Route}", request.Route); }
        });
    }
    public Task<VoicePhoto?> CapturePhotoAsync(VoiceCaptureOptions options) => NativeUiThread.InvokeAsync(() =>
    {
        if (_host == null || !_host.TryGetTarget(out var host)) throw new InvalidOperationException("No native camera presenter.");
        return camera.CaptureAsync(host, options);
    });
    public Task OpenBrowserAsync(Uri uri) => NativeUiThread.InvokeAsync(async () =>
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("https" or "http"))
            throw new ArgumentException("Only absolute web URLs are supported.", nameof(uri));
        var opened = await UIApplication.SharedApplication.OpenUrlAsync(new NSUrl(uri.AbsoluteUri), new UIApplicationOpenUrlOptions());
        if (!opened) throw new InvalidOperationException("The platform could not open the web address.");
        return true;
    });
}
