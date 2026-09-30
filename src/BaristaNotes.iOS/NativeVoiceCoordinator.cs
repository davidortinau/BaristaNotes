using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class NativeVoiceCoordinator : IAsyncDisposable
{
    private readonly SliceNavigationController _host;
    private readonly NativeVoiceOverlay _overlay;
    private readonly ILogger _logger;
    private readonly EventHandler<DataChangedEventArgs> _changed;
    private readonly HashSet<VoiceSessionWorkflow> _retiring = [];
    private VoiceSessionWorkflow? _current;
    private Task? _closing;
    private long _nextId;
    private string _lastResponse = "";
    private bool _disposed;

    public NativeVoiceCoordinator(
        SliceNavigationController host,
        NativeVoiceOverlay overlay)
    {
        _host = host;
        _overlay = overlay;
        _logger = host.Logging.CreateLogger<NativeVoiceCoordinator>();
        var weak = new WeakReference<NativeVoiceCoordinator>(this);
        _changed = (_, args) => NativeUiThread.Send(() =>
        {
            if (weak.TryGetTarget(out var owner) && !owner._disposed)
                owner._host.RefreshVoiceReferences(args.ChangeType);
        });
        host.Services.Singleton<IDataChangeNotifier>().DataChanged += _changed;
    }

    public async Task ToggleAsync()
    {
        if (_disposed)
            return;
        if (_closing is { IsCompleted: false })
        {
            await _closing;
            return;
        }
        if (_current is not null)
        {
            await CloseAsync();
            return;
        }

        var scope = _host.Services.CreateVoiceScope();
        VoiceSessionWorkflow? session = null;
        try
        {
            var commands =
                scope.ServiceProvider.GetRequiredService<IVoiceCommandService>();
            var speech =
                scope.ServiceProvider.GetRequiredService<ISpeechRecognitionService>();
            session = new VoiceSessionWorkflow(
                ++_nextId,
                speech,
                commands,
                _overlay,
                scope.DisposeAsync,
                NativeUiThread.Send,
                token => ShowPermissionDeniedAsync(session, token),
                () => EndOwner(session),
                _logger,
                _lastResponse,
                response =>
                {
                    if (session is not null && IsCurrent(session))
                        _lastResponse = response;
                });
            _current = session;
            session.Open();
            _logger.LogInformation(
                "Voice session {SessionId} opened with new scoped engine",
                session.Id);
        }
        catch (Exception error)
        {
            if (session is null)
                await scope.DisposeAsync();
            else
                session.Dispose();
            _logger.LogError(error, "Could not open voice session");
            _host.FeedbackHost.Show(
                "Voice commands are temporarily unavailable.",
                NativeFeedbackKind.Error);
        }
    }

    public Task CloseAsync()
    {
        if (_closing is { IsCompleted: false })
            return _closing;
        _closing = CloseCurrentAsync();
        return _closing;
    }

    private async Task CloseCurrentAsync()
    {
        var session = _current;
        if (session is null)
            return;
        await session.CloseAsync();
    }

    private void EndOwner(VoiceSessionWorkflow? session)
    {
        if (session is null)
            return;
        if (ReferenceEquals(_current, session))
            _current = null;
        if (_retiring.Add(session))
            _ = RetireAsync(session);
    }

    private async Task RetireAsync(VoiceSessionWorkflow session)
    {
        try
        {
            await session.Completion;
        }
        catch (Exception error)
        {
            _logger.LogError(
                error,
                "Voice session {SessionId} retirement failed",
                session.Id);
        }
        finally
        {
            _retiring.Remove(session);
        }
    }

    private bool IsCurrent(VoiceSessionWorkflow session) =>
        !_disposed && ReferenceEquals(_current, session) && !session.IsRetired;

    private async Task ShowPermissionDeniedAsync(
        VoiceSessionWorkflow? session,
        CancellationToken token)
    {
        if (session is null || !IsCurrent(session)
            || _host.PresentedViewController is not null)
        {
            return;
        }
        var completed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var alert = UIAlertController.Create(
            "Permission Required",
            "Speech recognition requires microphone and speech permissions. Please enable them in Settings.",
            UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create(
            "Cancel",
            UIAlertActionStyle.Cancel,
            _ => completed.TrySetResult()));
        alert.AddAction(UIAlertAction.Create(
            "Open Settings",
            UIAlertActionStyle.Default,
            _ =>
            {
                completed.TrySetResult();
                UIApplication.SharedApplication.OpenUrl(
                    new Foundation.NSUrl(UIApplication.OpenSettingsUrlString),
                    new UIApplicationOpenUrlOptions(),
                    null);
            }));
        _host.PresentViewController(alert, true, null);
        try
        {
            await completed.Task.WaitAsync(token);
        }
        finally
        {
            if (token.IsCancellationRequested
                && ReferenceEquals(_host.PresentedViewController, alert))
            {
                _host.DismissViewController(false, null);
            }
        }
    }

    internal VoiceSessionStatus Status => new(
        _current?.Id,
        _overlay.IsVisible,
        _overlay.IsCollapsed,
        _current?.IsRecording ?? false,
        _current?.IsStarting ?? false,
        _current?.IsProcessing ?? false,
        _current?.Transcript ?? "",
        _current?.LastResponse ?? "",
        _retiring.Count);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        _host.Services.Singleton<IDataChangeNotifier>().DataChanged -= _changed;
        var current = _current;
        if (current is not null)
            await current.CloseAsync();
        foreach (var session in _retiring.ToArray())
            await session.Completion;
    }
}

internal sealed record VoiceSessionStatus(
    long? SessionId,
    bool Visible,
    bool Collapsed,
    bool Recording,
    bool Starting,
    bool Processing,
    string Transcript,
    string LastResponse,
    int RetiringScopes);
