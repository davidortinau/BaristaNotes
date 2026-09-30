using Android.App;
using Android.Content;
using Android.Content.PM;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Logging;
using AndroidUri = Android.Net.Uri;
using Looper = Android.OS.Looper;

namespace BaristaNotes.AndroidApp.Services;

internal sealed class AndroidPhotoPicker(Activity activity, ILogger logger)
{
    public const int RequestCode = 0x5048;
    private readonly WeakReference<Activity> _activity = new(activity);
    private readonly ContentResolver _resolver = activity.ApplicationContext?.ContentResolver
        ?? throw new InvalidOperationException("The app photo content resolver is unavailable.");
    private Pending? _pending;

    public Task<Stream?> PickAsync(IImageProcessingService processing, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (Looper.MyLooper() != Looper.MainLooper || SynchronizationContext.Current is not { } context)
            throw new InvalidOperationException("The photo picker must start on the Android UI thread.");
        if (_pending is not null)
            throw new InvalidOperationException("A photo picker request is already active.");
        if (!_activity.TryGetTarget(out var current))
            throw new InvalidOperationException("The photo picker Activity is unavailable.");
        var pending = new Pending(processing, cancellation);
        _pending = pending;
        pending.Registration = cancellation.Register(() => context.Post(_ => Cancel(pending), null));
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var apiLevel = (int)Android.OS.Build.VERSION.SdkInt;
#pragma warning disable CA1416 // SdkExtensions exists on R/API30; pinned AndroidX has the same late-annotation suppression.
            var extension = OperatingSystem.IsAndroidVersionAtLeast(30)
                ? Android.OS.Ext.SdkExtensions.GetExtensionVersion(30)
                : 0;
#pragma warning restore CA1416
            using var fallbackIntent = new Intent(PhotoPickerAvailability.SystemFallbackAction);
#pragma warning disable CA1422, CS0618
            using var fallback = PhotoPickerAvailability.Select(apiLevel, extension, false) == PhotoPickerRoute.System
                ? null
                : current.PackageManager?.ResolveActivity(fallbackIntent,
                    PackageInfoFlags.MatchDefaultOnly | PackageInfoFlags.MatchSystemOnly);
#pragma warning restore CA1422, CS0618
            var route = PhotoPickerAvailability.Select(apiLevel, extension, fallback?.ActivityInfo is not null);
            if (route != PhotoPickerRoute.Documents)
            {
                try
                {
                    using var intent = new Intent(route == PhotoPickerRoute.System
                        ? PhotoPickerAvailability.SystemAction : PhotoPickerAvailability.SystemFallbackAction);
                    if (route == PhotoPickerRoute.SystemFallback)
                    {
                        var target = fallback?.ActivityInfo
                            ?? throw new InvalidOperationException("The resolved system photo picker is unavailable.");
                        if (string.IsNullOrEmpty(target.PackageName) || string.IsNullOrEmpty(target.Name))
                            throw new InvalidOperationException("The system photo picker has no valid component.");
                        intent.SetClassName(target.PackageName, target.Name);
                    }
                    intent.SetType("image/*");
                    Start(current, intent);
                    logger.LogDebug("Opened single-image photo picker via {PickerRoute}", route);
                    return pending.Completion.Task;
                }
                catch (ActivityNotFoundException)
                {
                    logger.LogWarning("System photo picker unavailable; using source-compatible document selection");
                }
            }
            using var content = new Intent(Intent.ActionGetContent);
            content.SetType("image/*");
            content.AddCategory(Intent.CategoryOpenable!);
            content.PutExtra(Intent.ExtraAllowMultiple, false);
            content.AddFlags(ActivityFlags.GrantReadUriPermission);
            using var chooser = Intent.CreateChooser(content, (string?)null)
                ?? throw new InvalidOperationException("The photo chooser could not be created.");
            Start(current, chooser);
            logger.LogDebug("Opened single-image GET_CONTENT fallback");
        }
        catch (Java.Lang.SecurityException exception)
        {
            Fail(pending, new UnauthorizedAccessException("Photo library permission denied", exception));
        }
        catch (Exception exception)
        {
            Fail(pending, exception);
        }
        return pending.Completion.Task;
    }

    private static void Start(Activity owner, Intent intent)
    {
        // This head intentionally derives from platform Activity, not a new
        // ComponentActivity/UI dependency. Its activity-result API remains supported.
#pragma warning disable CA1422, CS0618
        owner.StartActivityForResult(intent, RequestCode);
#pragma warning restore CA1422, CS0618
    }

    public bool HandleResult(int requestCode, Result result, Intent? data)
    {
        if (requestCode != RequestCode)
            return false;
        var pending = _pending;
        if (pending is null)
        {
            logger.LogDebug("Ignored a photo result without a live request");
            return true;
        }
        if (result != Result.Ok)
        {
            logger.LogDebug("Photo selection canceled by the user");
            Finish(pending, null);
            return true;
        }
        var selected = data?.Data;
        if (selected is null && data?.ClipData is { ItemCount: > 0 } clip)
            selected = clip.GetItemAt(0)?.Uri;
        if (selected is null || string.IsNullOrEmpty(selected.ToString()))
        {
            logger.LogDebug("Photo picker returned no selected image");
            Finish(pending, null);
            return true;
        }
        var uri = AndroidUri.Parse(selected.ToString());
        if (uri is null)
        {
            Fail(pending, new IOException("The selected photo URI could not be read."));
            return true;
        }
        _pending = null;
        pending.Reading = true;
        _ = ReadSelectionAsync(pending, uri);
        return true;
    }

    private async Task ReadSelectionAsync(Pending pending, AndroidUri uri)
    {
        using (uri)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(pending.Cancellation))
        {
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            MemoryStream? original = null;
            Stream? output = null;
            try
            {
                original = await Task.Run(async () =>
                {
                    using var input = _resolver.OpenInputStream(uri)
                        ?? throw new IOException("The selected photo did not provide a readable stream.");
                    var copy = new MemoryStream();
                    try
                    {
                        await input.CopyToAsync(copy, timeout.Token);
                        copy.Position = 0;
                        return copy;
                    }
                    catch
                    {
                        copy.Dispose();
                        throw;
                    }
                }, timeout.Token);
                // Match the source picker's requested preprocessing. The shared
                // profile service still owns its own400/85 pass and size-only gate.
                output = await pending.Processing.DownsampleAsync(original, 400, 85);
                if (output is null)
                {
                    original.Position = 0;
                    output = original;
                    original = null;
                    logger.LogWarning("Picker preprocessing returned null; preserving original bytes");
                }
                else if (ReferenceEquals(output, original))
                {
                    original = null;
                }
                timeout.Token.ThrowIfCancellationRequested();
                if (!pending.Completion.TrySetResult(output))
                    output.Dispose();
                output = null;
                logger.LogDebug("Selected photo stream prepared; no persistent URI grant requested");
            }
            catch (OperationCanceledException) when (pending.Cancellation.IsCancellationRequested)
            {
                pending.Completion.TrySetCanceled(pending.Cancellation);
                logger.LogDebug("Photo stream read canceled with its Activity lifetime");
            }
            catch (OperationCanceledException exception)
            {
                logger.LogWarning(exception, "Selected photo content read timed out");
                pending.Completion.TrySetException(new TimeoutException("The selected photo could not be read in time.", exception));
            }
            catch (Java.Lang.SecurityException exception)
            {
                logger.LogWarning(exception, "Selected photo access was denied");
                pending.Completion.TrySetException(new UnauthorizedAccessException("Photo library permission denied", exception));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Reading the selected photo failed");
                pending.Completion.TrySetException(exception);
            }
            finally
            {
                output?.Dispose();
                original?.Dispose();
                pending.Registration.Dispose();
            }
        }
    }

    private void Cancel(Pending pending)
    {
        if (pending.Reading)
            return;
        if (ReferenceEquals(_pending, pending))
            _pending = null;
        pending.Completion.TrySetCanceled(pending.Cancellation);
        pending.Registration.Dispose();
        logger.LogDebug("Canceled pending photo selection with Activity lifetime");
    }

    private void Finish(Pending pending, Stream? stream)
    {
        if (ReferenceEquals(_pending, pending))
            _pending = null;
        pending.Registration.Dispose();
        if (!pending.Completion.TrySetResult(stream))
            stream?.Dispose();
    }

    private void Fail(Pending pending, Exception exception)
    {
        logger.LogError(exception, "Starting photo selection failed");
        if (ReferenceEquals(_pending, pending))
            _pending = null;
        pending.Registration.Dispose();
        pending.Completion.TrySetException(exception);
    }

    private sealed class Pending(IImageProcessingService processing, CancellationToken cancellation)
    {
        public IImageProcessingService Processing { get; } = processing;
        public CancellationToken Cancellation { get; } = cancellation;
        public TaskCompletionSource<Stream?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationTokenRegistration Registration { get; set; }
        public bool Reading { get; set; }
    }
}
