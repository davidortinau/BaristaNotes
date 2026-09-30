using Android.Views;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private AdviceWorkflow? _adviceRequest;
    private DrinkEditor? _adviceOwner;
    private NativeAdvicePopup? _advicePopup;

    private bool IsCurrentAdvice(DrinkEditor owner, int shotId) =>
        !_destroyed && _adviceRequest is not null
        && _page == "drink" && _editingShotId == shotId
        && ReferenceEquals(_editEditor, owner) && ReferenceEquals(_host.GetChildAt(0), owner.Screen.Root);

    private async void RequestAdvice(DrinkEditor owner)
    {
        if (_adviceRequest is not null || _advicePopup is not null || _destroyed
            || _page != "drink" || _editingShotId is not int savedShotId || !ReferenceEquals(_editEditor, owner))
            return;
        var workflowHost = new NativeAdviceWorkflowHost(this, owner, savedShotId);
        using var request = new AdviceWorkflow(
            workflowHost,
            _app.Services.GetRequiredService<IAIAdviceService>(),
            _logger);
        _adviceRequest = request;
        _adviceOwner = owner;
        try
        {
            await request.RunAsync(savedShotId, _lifetime.Token);
        }
        finally
        {
            if (ReferenceEquals(_adviceRequest, request))
            {
                owner.AdviceBar?.Stop();
                _adviceRequest = null;
                _adviceOwner = null;
            }
        }
    }

    private void AdviceError(string title, string detail) =>
        _feedback.Show(AdvicePresentation.Error(title, detail), isError: true);

    private async Task ShowAdviceAsync(DrinkEditor owner, AIAdviceResponseDto response)
    {
        if (_destroyed || _page != "drink" || !ReferenceEquals(_editEditor, owner)) return;
        HideKeyboard();
        using var popup = new NativeAdvicePopup(this, _style, response,
            () => !_destroyed && !_feedback.IsVisible, _lifetime.Token);
        _advicePopup = popup;
        _host.Enabled = false;
        _host.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        try { await popup.ShowAsync(); }
        finally
        {
            if (ReferenceEquals(_advicePopup, popup)) _advicePopup = null;
            if (!_destroyed)
            {
                var blocked = _feedback.IsVisible || _filterScreen is not null || _equipmentConfirmation is not null;
                _host.Enabled = !blocked;
                _host.ImportantForAccessibility = blocked
                    ? ImportantForAccessibility.NoHideDescendants : ImportantForAccessibility.Auto;
            }
        }
    }

    private void CancelAdviceWhenLeaving(View next)
    {
        if (_adviceOwner is { } owner && !ReferenceEquals(next, owner.Screen.Root))
            DisposeAdvice();
    }

    private void DisposeAdvice()
    {
        _adviceRequest?.Dispose();
        _adviceRequest = null;
        _adviceOwner?.AdviceBar?.Stop();
        _adviceOwner = null;
        _advicePopup?.Dispose();
        _advicePopup = null;
    }

    private sealed class NativeAdviceWorkflowHost(
        MainActivity activity,
        DrinkEditor owner,
        int shotId) : IAdviceWorkflowHost
    {
        public bool IsCurrent => activity.IsCurrentAdvice(owner, shotId);

        public void SetLoading(bool loading)
        {
            if (loading)
                owner.AdviceBar?.Start();
            else
                owner.AdviceBar?.Stop();
        }

        public Task PresentAsync(
            AIAdviceResponseDto response,
            CancellationToken cancellation) =>
            activity.ShowAdviceAsync(owner, response);

        public void ShowError(string title, string detail) =>
            activity.AdviceError(title, detail);
    }
}
