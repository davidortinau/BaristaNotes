using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Workflows;

public interface IAdviceWorkflowHost
{
    bool IsCurrent { get; }
    void SetLoading(bool loading);
    Task PresentAsync(
        AIAdviceResponseDto response,
        CancellationToken cancellation);
    void ShowError(string title, string detail);
}

public sealed class AdviceWorkflow(
    IAdviceWorkflowHost host,
    IAIAdviceService service,
    ILogger logger,
    TimeSpan? requestTimeout = null) : IDisposable
{
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly TimeSpan _requestTimeout = requestTimeout ?? RequestTimeout;
    private CancellationTokenSource? _current;
    private int _active;
    private bool _disposed;

    public bool IsActive => Volatile.Read(ref _active) != 0;

    public async Task RunAsync(int shotId, CancellationToken owner)
    {
        if (_disposed || Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            return;

        using var request = CancellationTokenSource.CreateLinkedTokenSource(owner);
        _current = request;
        try
        {
            if (!host.IsCurrent)
                return;

            bool configured;
            try
            {
                configured = await service.IsConfiguredAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "AI advice configuration check failed for saved shot {ShotId}",
                    shotId);
                if (host.IsCurrent)
                {
                    host.ShowError(
                        AdvicePresentation.Unavailable,
                        AdvicePresentation.UnavailableDetail);
                }
                return;
            }

            if (!host.IsCurrent || owner.IsCancellationRequested)
                return;
            if (!configured)
            {
                host.ShowError(
                    AdvicePresentation.Unavailable,
                    AdvicePresentation.UnavailableDetail);
                return;
            }

            request.CancelAfter(_requestTimeout);
            host.SetLoading(true);
            logger.LogInformation(
                "Requesting advice for saved shot {ShotId}",
                shotId);
            var response = await service.GetAdviceForShotAsync(
                shotId, request.Token);

            if (!host.IsCurrent || owner.IsCancellationRequested)
                return;
            host.SetLoading(false);
            if (response.Success)
            {
                await host.PresentAsync(response, owner);
            }
            else
            {
                host.ShowError(
                    AdvicePresentation.Unsuccessful,
                    response.ErrorMessage ?? AdvicePresentation.UnsuccessfulDetail);
            }
        }
        catch (OperationCanceledException)
        {
            if (host.IsCurrent && !owner.IsCancellationRequested)
            {
                host.ShowError(
                    AdvicePresentation.TimedOut,
                    AdvicePresentation.TimedOutDetail);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Advice request failed for saved shot {ShotId}",
                shotId);
            if (host.IsCurrent && !owner.IsCancellationRequested)
            {
                host.ShowError(
                    AdvicePresentation.Failed,
                    exception.Message);
            }
        }
        finally
        {
            host.SetLoading(false);
            if (ReferenceEquals(_current, request))
                _current = null;
            Interlocked.Exchange(ref _active, 0);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _current?.Cancel();
    }
}
