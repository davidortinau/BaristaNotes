using Android.Views;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private async Task ConfirmProfileDeleteAsync(ProfileEditor editor)
    {
        if (!IsCurrentProfile(editor) || editor.Draft.ProfileId is not int id || id <= 0
            || editor.Saving || editor.ImageLoading)
            return;
        HideKeyboard();
        var name = editor.Draft.Name;
        var committed = false;
        using var confirmation = new NativeArchiveConfirmation(this, _style, name,
            async cancellation =>
            {
                await _profileWriteGate.WaitAsync(cancellation);
                try
                {
                    await InScopeAsync(async services =>
                    {
                        await services.GetRequiredService<ProfileWorkflow>().DeleteAsync(id);
                        return true;
                    });
                    committed = true;
                }
                finally
                {
                    _profileWriteGate.Release();
                }
                cancellation.ThrowIfCancellationRequested();
                if (IsCurrentProfile(editor))
                    await _feedback.ShowSuccessAsync($"Profile '{name}' deleted");
                cancellation.ThrowIfCancellationRequested();
            }, () => !_destroyed && !_feedback.IsVisible, _logger, _lifetime.Token,
            new SimpleActionContent("Delete Profile?",
                $"Are you sure you want to delete '{name}'? This action cannot be undone.",
                "Delete", "ProfileDelete"));
        // Reuse the existing modal gate/back/teardown owner as well as its view.
        _equipmentConfirmation = confirmation;
        _host.Enabled = false;
        _host.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
        try
        {
            var confirmed = await confirmation.ShowAsync();
            if (confirmed && IsCurrentProfile(editor))
                await ReturnFromProfileEditorAsync(editor);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Profile deletion confirmation failed after commit={Committed}", committed);
            if (committed && IsCurrentProfile(editor))
                await ReturnFromProfileEditorAsync(editor);
            else
                SetProfileError(editor, $"Failed to delete: {ErrorMessage(exception)}");
        }
        finally
        {
            if (ReferenceEquals(_equipmentConfirmation, confirmation))
                _equipmentConfirmation = null;
            if (!_destroyed)
            {
                var blocked = _feedback.IsVisible || _filterScreen is not null;
                _host.Enabled = !blocked;
                _host.ImportantForAccessibility = blocked
                    ? ImportantForAccessibility.NoHideDescendants : ImportantForAccessibility.Auto;
            }
        }
    }
}
