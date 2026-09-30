using Android.App;
using Android.Content;
using Android.Views;
using BaristaNotes.AndroidApp.Services;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private NativeVoiceOverlay? _voiceOverlay;
    private VoiceSessionWorkflow? _voiceSession;
    private VoiceUiDispatcher? _voiceDispatcher;
    private AndroidVoiceCapture? _voiceCapture;
    private AlertDialog? _voicePermissionDialog;
    private int _nextVoiceCaptureCode = 0x6000;
    private long _nextVoiceSessionId;
    private bool _voiceReferencesPending;
    private bool _refreshingVoiceReferences;
    private bool VoiceBlocksInput => _voiceOverlay?.BlocksInput == true;

    private async void ToggleVoiceFromPage()
    {
        if (_destroyed) return;
        try
        {
            if (_page != "drink")
            {
                ClearVoiceNavigationHistory();
                _editingShotId = null;
                ShowDrink();
            }
            if (_voiceSession is { IsOpen: true } existing)
            {
                await existing.CloseAsync();
                return;
            }
            _voiceDispatcher ??= new VoiceUiDispatcher();
            _voiceOverlay ??= new NativeVoiceOverlay(this, _style, UpdateVoiceInputGate);
            var scope = _app.Services.CreateScope();
            VoiceSessionWorkflow? session = null;
            try
            {
                var platform = scope.ServiceProvider.GetRequiredService<NativeVoicePlatformActions>();
                platform.Attach(this, _voiceDispatcher, _voiceOverlay,
                    () => !_destroyed && ReferenceEquals(_voiceSession, session) && session?.IsOpen == true);
                var speech = scope.ServiceProvider.GetRequiredService<ISpeechRecognitionService>();
                var engine = scope.ServiceProvider.GetRequiredService<IVoiceCommandService>();
                session = new VoiceSessionWorkflow(
                    ++_nextVoiceSessionId,
                    speech,
                    engine,
                    _voiceOverlay,
                    () =>
                    {
                        scope.Dispose();
                        return ValueTask.CompletedTask;
                    },
                    _voiceDispatcher.DispatchState,
                    _ => ShowVoicePermissionDeniedAsync(
                        () => !_destroyed
                            && ReferenceEquals(_voiceSession, session)
                            && session?.IsOpen == true),
                    platform.EndOwner,
                    _logger);
                _voiceSession = session;
                session.Open();
            }
            catch
            {
                if (session is not null)
                {
                    session.Dispose();
                    if (ReferenceEquals(_voiceSession, session)) _voiceSession = null;
                }
                else scope.Dispose();
                throw;
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Opening or closing native voice failed");
            if (!_destroyed) ShowFeedback(ErrorMessage(exception), isError: true);
        }
    }

    private void UpdateVoiceInputGate()
    {
        if (_destroyed || _host is null || _feedback is null) return;
        var blocked = VoiceBlocksInput || _feedback.IsVisible || _filterScreen is not null
            || _equipmentConfirmation is not null || _advicePopup is not null || PhotoBlocksInput;
        _host.Enabled = !blocked;
        _host.ImportantForAccessibility = blocked ? ImportantForAccessibility.NoHideDescendants : ImportantForAccessibility.Auto;
        _photoSession?.SetCovered(_feedback.IsVisible || VoiceBlocksInput);
    }

    private Task ShowVoicePermissionDeniedAsync(Func<bool> ownerActive)
    {
        if (!ownerActive()) return Task.CompletedTask;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var builder = new AlertDialog.Builder(this);
        builder.SetTitle("Permission Required");
        builder.SetMessage("Speech recognition requires microphone and speech permissions. Please enable them in Settings.");
        builder.SetPositiveButton("Open Settings", (_, _) =>
            {
                if (!ownerActive()) return;
                try
                {
                    using var intent = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings,
                        Android.Net.Uri.FromParts("package", PackageName, null));
                    StartActivity(intent);
                }
                catch (Exception exception) { _logger.LogError(exception, "Opening app permission settings failed"); }
            });
        builder.SetNegativeButton("Cancel", (_, _) => { });
        var dialog = builder.Create() ?? throw new InvalidOperationException("The permission dialog could not be created.");
        _voicePermissionDialog = dialog;
        EventHandler? dismissed = null;
        dismissed = (_, _) =>
        {
            dialog.DismissEvent -= dismissed;
            if (ReferenceEquals(_voicePermissionDialog, dialog)) _voicePermissionDialog = null;
            completed.TrySetResult();
            dialog.Dispose();
        };
        dialog.DismissEvent += dismissed;
        dialog.Show();
        return completed.Task;
    }

    internal int NextVoiceCaptureRequestCode()
    {
        VoiceUiDispatcher.RequireMainThread();
        if (_nextVoiceCaptureCode > 0x7fff)
            throw new InvalidOperationException("Camera request identifiers exhausted for this Activity.");
        return _nextVoiceCaptureCode++;
    }
    internal void AttachVoiceCapture(AndroidVoiceCapture capture) => _voiceCapture = capture;
    internal void DetachVoiceCapture(AndroidVoiceCapture capture)
    {
        if (ReferenceEquals(_voiceCapture, capture)) _voiceCapture = null;
    }

    private void QueueVoiceReferenceRefresh(DataChangeType type)
    {
        if (_voiceSession is null || (!_voiceSession.IsOpen && _voiceSession.Completion.IsCompleted)
            || type is not (DataChangeType.BeanCreated or DataChangeType.BeanUpdated
            or DataChangeType.BagCreated or DataChangeType.BagUpdated or DataChangeType.EquipmentCreated
            or DataChangeType.EquipmentUpdated or DataChangeType.ProfileCreated or DataChangeType.ProfileUpdated)) return;
        _voiceDispatcher?.Post(() =>
        {
            if (_destroyed) return;
            _voiceReferencesPending = true;
            if (!_refreshingVoiceReferences) _ = RefreshVoiceReferencesAsync();
        });
    }

    private async Task RefreshVoiceReferencesAsync()
    {
        _refreshingVoiceReferences = true;
        try
        {
            do
            {
                _voiceReferencesPending = false;
                var revision = _presentationRevision;
                if (_bagsDirty) await RefreshBagsAsync();
                if (_equipmentDirty) await RefreshEquipmentReferencesAsync();
                if (_profilesDirty) await RefreshProfilesAsync();
                if (!_destroyed && revision == _presentationRevision && _page == "drink")
                    UpdateDrink(_editingShotId.HasValue ? _editEditor! : _newEditor!);
            } while (_voiceReferencesPending && !_destroyed);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Refreshing voice-created picker references failed");
        }
        finally { _refreshingVoiceReferences = false; }
    }

    private void DisposeVoice()
    {
        _voicePermissionDialog?.Dismiss();
        _voiceSession?.Dispose();
        _voiceSession = null;
        _voiceCapture?.Dispose();
        _voiceOverlay?.Dispose();
        _voiceOverlay = null;
        ClearVoiceNavigationHistory();
    }
}
