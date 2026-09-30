using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public interface IPhotoWorkflowHost
{
    bool IsCurrent { get; }
    bool IsCaptureSupported { get; }
    Task<VoicePhoto?> CaptureAsync(CancellationToken cancellation);
    void SetProcessing(bool processing);
    Task<PhotoIntentChoice> ChooseIntentAsync(CancellationToken cancellation);
    Task OpenCoffeeAsync(BeanLabelExtraction prefill, CancellationToken cancellation);
    Task OpenProfileAsync(byte[] image);
    Task AlertAsync(string title, string message, CancellationToken cancellation);
}

public sealed class PhotoWorkflow(
    IPhotoWorkflowHost host,
    IVisionService vision,
    ILogger logger) : IDisposable
{
    public const string RoomQuestion =
        "Count the people in this image and tell me how many cups of coffee I need to make.";

    private CancellationTokenSource? _current;
    private int _active;
    private bool _disposed;

    public bool IsActive => Volatile.Read(ref _active) != 0;

    public async Task RunAsync(CancellationToken owner)
    {
        if (_disposed || Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            return;

        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(owner);
        _current = lifetime;
        var cancellation = lifetime.Token;
        try
        {
            EnsureCurrent(cancellation);
            if (!host.IsCaptureSupported)
            {
                await host.AlertAsync(
                    "Camera Unavailable",
                    "Camera is not available on this device.",
                    cancellation);
                return;
            }

            while (true)
            {
                EnsureCurrent(cancellation);
                var photo = await host.CaptureAsync(cancellation);
                EnsureCurrent(cancellation);
                if (photo is null)
                    return;

                host.SetProcessing(true);
                byte[] image;
                using (var input = await photo.OpenReadAsync())
                using (var buffer = new MemoryStream())
                {
                    await input.CopyToAsync(buffer, cancellation);
                    image = buffer.ToArray();
                }

                EnsureCurrent(cancellation);
                using var classification = new MemoryStream(image, writable: false);
                var analysis = await vision.ClassifyPhotoAsync(classification, cancellation);
                EnsureCurrent(cancellation);
                if (!analysis.Success)
                {
                    host.SetProcessing(false);
                    await host.AlertAsync(
                        "Photo Analysis Unavailable",
                        analysis.ErrorMessage ?? "Could not analyze the photo.",
                        cancellation);
                    return;
                }

                var choice = PhotoWorkflowRules.AutomaticChoice(analysis);
                if (!choice.HasValue)
                {
                    host.SetProcessing(false);
                    choice = await host.ChooseIntentAsync(cancellation);
                    EnsureCurrent(cancellation);
                }

                if (choice == PhotoIntentChoice.Retake)
                {
                    host.SetProcessing(false);
                    continue;
                }

                switch (choice)
                {
                    case PhotoIntentChoice.Coffee:
                        var details = analysis.CoffeeDetails;
                        if (PhotoWorkflowRules.NeedsCoffeeExtraction(details))
                        {
                            using var extraction = new MemoryStream(image, writable: false);
                            details = await vision.ExtractBeanLabelAsync(extraction, cancellation);
                        }
                        EnsureCurrent(cancellation);
                        if (details?.Success != true || PhotoWorkflowRules.NeedsCoffeeExtraction(details))
                        {
                            host.SetProcessing(false);
                            await host.AlertAsync(
                                "Couldn't Read Label",
                                "Couldn't read the label. You can enter the details manually.",
                                cancellation);
                            EnsureCurrent(cancellation);
                        }
                        await host.OpenCoffeeAsync(
                            PhotoWorkflowRules.CoffeePrefill(details), cancellation);
                        break;

                    case PhotoIntentChoice.Profile:
                        host.SetProcessing(false);
                        await host.OpenProfileAsync(image);
                        break;

                    case PhotoIntentChoice.Room:
                        using (var room = new MemoryStream(image, writable: false))
                        {
                            var result = await vision.AnalyzeImageAsync(
                                room, RoomQuestion, cancellation);
                            EnsureCurrent(cancellation);
                            host.SetProcessing(false);
                            await host.AlertAsync(
                                result.Success ? "Analysis Complete" : "Analysis Failed",
                                result.Success
                                    ? RoomMessage(result)
                                    : result.ErrorMessage ?? "Could not analyze the photo.",
                                cancellation);
                        }
                        break;

                    default:
                        host.SetProcessing(false);
                        break;
                }
                return;
            }
        }
        catch (OperationCanceledException) when (
            cancellation.IsCancellationRequested || !host.IsCurrent)
        {
            logger.LogDebug("Native photo workflow canceled or owner changed");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Capturing or analyzing a photo failed");
            host.SetProcessing(false);
            if (host.IsCurrent && !cancellation.IsCancellationRequested)
            {
                await host.AlertAsync(
                    "Error",
                    $"Failed to capture or analyze photo: {exception.Message}",
                    cancellation);
            }
        }
        finally
        {
            host.SetProcessing(false);
            if (ReferenceEquals(_current, lifetime))
                _current = null;
            Interlocked.Exchange(ref _active, 0);
        }
    }

    public static string RoomMessage(VisionAnalysisResult result) =>
        result.Message ??
        $"I see {result.PeopleCount} {(result.PeopleCount == 1 ? "person" : "people")}. " +
        $"You need {result.CupsNeeded} {(result.CupsNeeded == 1 ? "cup" : "cups")} of coffee, " +
        $"which requires about {result.BeansNeededGrams}g of beans.";

    private void EnsureCurrent(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!host.IsCurrent)
            throw new OperationCanceledException(cancellation);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _current?.Cancel();
    }
}
