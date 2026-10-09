using Android.Views;
using BaristaNotes.AndroidApp.Services;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private sealed record VoicePageSnapshot(
        string Page, View Root, bool EdgeToEdge, NativeScreen? Transient, DrinkEditor? EditEditor,
        DrinkDraft? EditDraft, int? ShotId, BeanEditor? Bean, BagEditor? Bag, ProfileEditor? Profile,
        EquipmentEditor? Equipment, RangeEditorDraft? Range, BeanDetailState? BeanShotReturn,
        bool SettingsFromHistory, bool BeanCreateToList, Action? SettingsAppearance);
    private sealed record VoiceNavigationFrame(VoiceRoutePlan Destination, VoicePageSnapshot Previous);
    private readonly Stack<VoiceNavigationFrame> _voiceNavigation = [];
    private readonly SemaphoreSlim _voiceNavigationGate = new(1, 1);

    internal async Task NavigateFromVoiceAsync(VoiceRoutePlan plan, Func<bool> ownerActive)
    {
        if (_destroyed || !ownerActive()) return;
        var cancellation = _lifetime.Token;
        await _voiceNavigationGate.WaitAsync(cancellation);
        try { await NavigateVoiceCoreAsync(plan, ownerActive); }
        finally { _voiceNavigationGate.Release(); }
    }

    private async Task NavigateVoiceCoreAsync(VoiceRoutePlan plan, Func<bool> ownerActive)
    {
        if (_destroyed || !ownerActive()) return;
        if (_busy || _filterScreen is not null || _advicePopup is not null || _equipmentConfirmation is not null
            || _rangeConfirmation is not null || _profileEditor?.Saving == true || _profileEditor?.ImageLoading == true
            || _beanEditor?.Saving == true || _bagEditor?.Saving == true || _bagEditor?.ChangingStatus == true
            || (_page == "profile" && _profileEditor?.Name.IsShown == false)
            || (_page == "beanDetail" && _beanEditor?.Scroll.Visibility == ViewStates.Gone)
            || (_page == "bagDetail" && _bagEditor?.Notes.IsShown == false)
            || (_page == "equipmentForm" && _equipmentEditor?.Name.IsShown == false))
            throw new InvalidOperationException("The native navigation owner is busy.");
        if (plan.IgnoredQuery is not null)
            _logger.LogDebug("Voice route query is not applied, preserving the pinned Activity query limitation");
        DrinkDraft? shot = null;
        if (plan.Kind == VoiceRouteKind.Shot && plan.EntityId is int shotId)
        {
            var revision = _presentationRevision;
            shot = new DrinkDraft();
            await LoadDraftAsync(shot, shotId);
            if (_destroyed || !ownerActive() || revision != _presentationRevision) return;
        }
        if (plan.IsRoot)
            ClearVoiceNavigationHistory();
        else
            PushNavigationFrame(plan);
        HideKeyboard();
        try
        {
            switch (plan.Kind)
            {
                case VoiceRouteKind.Drink:
                    _editingShotId = null;
                    ShowDrink();
                    break;
                case VoiceRouteKind.History:
                    await ShowHistoryAsync();
                    break;
                case VoiceRouteKind.Settings:
                    OpenSettings();
                    break;
                case VoiceRouteKind.Profiles:
                    await ShowProfilesAsync();
                    break;
                case VoiceRouteKind.Beans:
                    await ShowBeansAsync();
                    break;
                case VoiceRouteKind.Equipment:
                    await ShowEquipmentAsync();
                    break;
                case VoiceRouteKind.Shot:
                    _beanShotReturn = null;
                    if (shot is null) _editingShotId = null;
                    else
                    {
                        _editEditor = null;
                        _editDraft = shot;
                        _editingShotId = plan.EntityId;
                    }
                    ShowDrink();
                    break;
                case VoiceRouteKind.Profile:
                    await ShowProfileEditorAsync(true, plan.EntityId, null);
                    break;
                case VoiceRouteKind.Bean:
                    if (plan.EntityId is int beanId) await ShowBeanDetailAsync(new BeanDetailState(beanId), true);
                    else ShowBeanForm(returnToBeans: true);
                    break;
                case VoiceRouteKind.Bag:
                    var parent = new BeanDetailState(plan.BeanId ?? 0);
                    parent.Draft.Name = plan.BeanName ?? "";
                    await ShowBagDetailAsync(parent, plan.EntityId);
                    break;
                case VoiceRouteKind.EquipmentDetail:
                    await ShowEquipmentFormAsync(EquipmentReturn.List, plan.EntityId);
                    break;
                case VoiceRouteKind.Ranges:
                    ShowRangeSettings(DrinkValueMetric.DoseIn);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(plan));
            }
        }
        catch
        {
            if (!plan.IsRoot && _voiceNavigation.TryPeek(out var frame) && ReferenceEquals(frame.Destination, plan))
                RestoreVoicePage(_voiceNavigation.Pop().Previous);
            throw;
        }
    }

    private void PushNavigationFrame(VoiceRoutePlan plan)
    {
        var root = _host.GetChildAt(0) ?? throw new InvalidOperationException("The native navigation origin is unavailable.");
        _voiceNavigation.Push(new(plan, new VoicePageSnapshot(_page, root,
            ReferenceEquals(_host.Parent, Window?.DecorView), _transient, _editEditor, _editDraft, _editingShotId,
            _beanEditor, _bagEditor, _profileEditor, _equipmentEditor, _rangeDraft, _beanShotReturn,
            _settingsFromHistory, _beanCreateReturnToList, _refreshSettingsAppearance)));
        // Retain the exact native owner and draft for Back, including nested routes.
        _transient = null;
    }

    private bool TryReturnVoiceNavigation()
    {
        if (_voiceNavigation.Count == 0 || !MatchesVoiceDestination(_voiceNavigation.Peek().Destination)) return false;
        RestoreVoicePage(_voiceNavigation.Pop().Previous);
        return true;
    }

    private bool MatchesVoiceDestination(VoiceRoutePlan plan) => plan.Kind switch
    {
        VoiceRouteKind.Profiles => _page == "profiles",
        VoiceRouteKind.Beans => _page == "beans",
        VoiceRouteKind.Equipment => _page == "equipment",
        VoiceRouteKind.Shot => _page == "drink" && _editingShotId == plan.EntityId,
        VoiceRouteKind.Profile => _page == "profile" && (!plan.EntityId.HasValue || _profileEditor?.Draft.ProfileId == plan.EntityId),
        VoiceRouteKind.Bean => plan.EntityId.HasValue
            ? _page == "beanDetail" && _beanEditor?.State.Draft.BeanId == plan.EntityId : _page == "bean",
        VoiceRouteKind.Bag => _page == "bagDetail" && (!plan.EntityId.HasValue || _bagEditor?.Draft.BagId == plan.EntityId),
        VoiceRouteKind.EquipmentDetail => _page == "equipmentForm"
            && (!plan.EntityId.HasValue || _equipmentEditor?.Draft.EquipmentId == plan.EntityId),
        VoiceRouteKind.Ranges => _page == "ranges",
        _ => false
    };

    private void RestoreVoicePage(VoicePageSnapshot previous)
    {
        HideKeyboard();
        ClearTransient();
        if (_editEditor is { } currentEdit && !ReferenceEquals(currentEdit, previous.EditEditor))
            currentEdit.Screen.Dispose();
        (_page, _transient, _editEditor, _editDraft, _editingShotId) =
            (previous.Page, previous.Transient, previous.EditEditor, previous.EditDraft, previous.ShotId);
        (_beanEditor, _bagEditor, _profileEditor, _equipmentEditor, _rangeDraft) =
            (previous.Bean, previous.Bag, previous.Profile, previous.Equipment, previous.Range);
        _beanShotReturn = previous.BeanShotReturn;
        _settingsFromHistory = previous.SettingsFromHistory;
        _beanCreateReturnToList = previous.BeanCreateToList;
        _refreshSettingsAppearance = previous.SettingsAppearance;
        Present(previous.Root, previous.EdgeToEdge);
        if (_page == "drink")
        {
            if (_editDraft is not null)
            {
                _editDraft.AvailableBags = _newDraft.AvailableBags;
                _editDraft.AvailableEquipment = _newDraft.AvailableEquipment;
                _editDraft.AvailableUsers = _newDraft.AvailableUsers;
            }
            UpdateDrink(_editingShotId.HasValue ? _editEditor! : _newEditor!);
            _voiceReferencesPending = true;
            if (!_refreshingVoiceReferences) _ = RefreshVoiceReferencesAsync();
        }
        else if (_page == "history") RunOperation(ShowHistoryAsync);
        else if (_page == "beanDetail" && _beanEditor is { } bean)
        {
            ObserveBeanTask(() => LoadBeanBagsAsync(bean));
            ObserveBeanTask(() => LoadBeanRecipesAsync(bean));
        }
    }

    private void ClearVoiceNavigationHistory()
    {
        var disposed = new HashSet<NativeScreen>();
        foreach (var frame in _voiceNavigation)
        {
            var screen = frame.Previous.Transient;
            if (screen is not null && !ReferenceEquals(screen, _transient) && disposed.Add(screen)) screen.Dispose();
            var edit = frame.Previous.EditEditor?.Screen;
            if (edit is not null && !ReferenceEquals(edit, _editEditor?.Screen) && disposed.Add(edit)) edit.Dispose();
        }
        _voiceNavigation.Clear();
    }

    private Task ReturnFromBeanDetailAsync()
    {
        if (TryReturnVoiceNavigation()) return Task.CompletedTask;
        return ShowBeansAsync();
    }
}
