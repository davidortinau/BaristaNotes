using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class PhotoWorkflowSession : IPhotoWorkflowHost
{
    private readonly WeakReference<DrinkViewController> _owner;
    private readonly SliceNavigationController _host;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ILogger _logger;
    private readonly PhotoWorkflow _workflow;
    private PhotoActionModal? _modal;
    private UIAlertController? _alert;
    private bool _retired;
    private bool _completed;

    public PhotoWorkflowSession(DrinkViewController owner, SliceNavigationController host)
    {
        _owner = new(owner);
        _host = host;
        _logger = host.Logging.CreateLogger<PhotoWorkflowSession>();
        _workflow = new PhotoWorkflow(this, host.Services.Singleton<IVisionService>(), _logger);
    }

    public bool IsCurrent =>
        !_retired && !_completed && _host.IsSceneAttached
        && _owner.TryGetTarget(out var owner) && owner.OwnsPhotoSession(this)
        && ReferenceEquals(_host.TopViewController, owner);

    public bool IsCaptureSupported =>
        IsCurrent && _host.Services.Singleton<INativePhotoCapture>().CaptureSupported;

    public async Task RunAsync()
    {
        try
        {
            await _workflow.RunAsync(_cancellation.Token);
        }
        finally
        {
            SetProcessing(false);
            _modal?.Retire();
            _modal?.Dispose();
            _modal = null;
            _completed = true;
            _workflow.Dispose();
            _cancellation.Dispose();
            if (_owner.TryGetTarget(out var owner))
                owner.PhotoSessionFinished(this);
        }
    }

    public Task<VoicePhoto?> CaptureAsync(CancellationToken cancellation) =>
        _host.Services.Singleton<INativePhotoCapture>()
            .CaptureAsync(_host, NativePhotoRequest.General, cancellation);

    public async Task<PhotoIntentChoice> ChooseIntentAsync(CancellationToken cancellation)
    {
        var intent = new PhotoIntentViewController(_host);
        _modal = intent;
        try
        {
            await ShowAsync(intent, cancellation);
            return await intent.Choice.WaitAsync(cancellation);
        }
        finally
        {
            intent.Dispose();
            if (ReferenceEquals(_modal, intent))
                _modal = null;
        }
    }

    public async Task OpenCoffeeAsync(
        BeanLabelExtraction prefill,
        CancellationToken cancellation)
    {
        var weak = new WeakReference<PhotoWorkflowSession>(this);
        var popup = new AddCoffeeViewController(
            _host,
            () => weak.TryGetTarget(out var current) && current.IsCurrent,
            bag =>
            {
                if (weak.TryGetTarget(out var current)
                    && current.IsCurrent
                    && current._owner.TryGetTarget(out var owner))
                {
                    owner.SelectPhotoBag(current, bag);
                }
            },
            (message, kind) =>
            {
                if (weak.TryGetTarget(out var current) && current.IsCurrent)
                    current._host.FeedbackHost.Show(message, kind);
            });
        _modal = popup;
        try
        {
            await popup.InitializeAsync(prefill, cancellation);
            if (!IsCurrent)
                return;
            SetProcessing(false);
            await ShowAsync(popup, cancellation);
            await popup.Finished.WaitAsync(cancellation);
        }
        finally
        {
            popup.Dispose();
            if (ReferenceEquals(_modal, popup))
                _modal = null;
        }
    }

    public Task OpenProfileAsync(byte[] image)
    {
        if (IsCurrent)
            _host.PushViewController(
                new ProfileCreateViewController(_host, stagedAvatarBytes: image), false);
        return Task.CompletedTask;
    }

    public async Task AlertAsync(
        string title,
        string message,
        CancellationToken cancellation)
    {
        if (!IsCurrent)
            return;
        var done = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var alert = UIAlertController.Create(
            title, message, UIAlertControllerStyle.Alert);
        _alert = alert;
        alert.AddAction(UIAlertAction.Create(
            "OK", UIAlertActionStyle.Default, _ =>
            {
                // Manual intent can present another modal immediately after this alert.
                alert.DismissViewController(true, () => done.TrySetResult());
            }));
        _host.PresentViewController(alert, true, null);
        try
        {
            await done.Task.WaitAsync(cancellation);
        }
        finally
        {
            if (cancellation.IsCancellationRequested)
                alert.DismissViewController(false, null);
            if (ReferenceEquals(_alert, alert))
                _alert = null;
        }
    }

    public void SetProcessing(bool processing)
    {
        if (_owner.TryGetTarget(out var owner))
            owner.SetPhotoProcessing(this, processing && IsCurrent);
    }

    private async Task ShowAsync(
        PhotoActionModal modal,
        CancellationToken cancellation)
    {
        if (!IsCurrent)
            return;
        _host.PresentViewController(modal, false, null);
        await modal.Opened.WaitAsync(cancellation);
    }

    public void Retire()
    {
        if (_retired || _completed)
            return;
        _retired = true;
        _workflow.Dispose();
        try
        {
            _cancellation.Cancel();
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Photo cancellation callback failed");
        }
        _modal?.Retire();
        _alert?.DismissViewController(false, null);
        SetProcessing(false);
    }
}
