using Android.Graphics;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private async Task OpenGrindPickerAsync()
    {
        var draft = Draft;
        var revision = _presentationRevision;
        var input = new DrinkDraft
        {
            GrindMicrons = draft.GrindMicrons,
            BrewMethod = draft.BrewMethod,
            SelectedBagId = draft.SelectedBagId,
            SelectedGrinderId = draft.SelectedGrinderId
        };
        GrindPickerLoadResult? loaded = null;
        var equipmentRevision = _equipmentRevision;
        try
        {
            if (_equipmentDirty)
                await RefreshEquipmentReferencesAsync();
            input.AvailableEquipment = draft.AvailableEquipment.ToList();
            equipmentRevision = _equipmentRevision;
            loaded = await InScopeAsync(services =>
                services.GetRequiredService<GrindPickerWorkflow>().LoadAsync(input));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Preserve the pinned caller's log-and-open behavior. The shared
            // workflow itself does not turn failed reads into a successful load.
            _logger.LogError(exception, "Grind picker load failed; opening the source fallback state");
        }
        if (_destroyed || revision != _presentationRevision || equipmentRevision != _equipmentRevision
            || !ReferenceEquals(Draft, draft)
            || draft.GrindMicrons != input.GrindMicrons || draft.SelectedBagId != input.SelectedBagId
            || draft.BrewMethod != input.BrewMethod || draft.SelectedGrinderId != input.SelectedGrinderId)
        {
            _logger.LogDebug("Discarded a stale native grind load result");
            return;
        }
        ShowGrindPicker(draft, loaded);
    }

    private void ShowGrindPicker(DrinkDraft draft, GrindPickerLoadResult? loaded)
    {
        var range = Ranges.Resolve(DrinkValueMetric.GrindMicrons, draft.BrewMethod);
        var state = new GrindPickerState(range, draft.GrindMicrons, loaded?.Microns);
        ClearTransient();
        _page = "picker";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Surface);
        var screen = new NativeScreen(column);
        column.AddView(PickerHeader(screen, "Grind", () =>
        {
            draft.GrindMicrons = state.DoneValue;
            ShowDrink();
        }));
        var (scopeBackground, scopeAction, description, scopeText) = CreateRangeScopeBar();
        column.AddView(scopeBackground);
        var adapter = new GrindOptionAdapter(_style);
        var list = CreateOwnedEquipmentList(screen, adapter, "GrindValues");
        column.AddView(list, _style.Fill(weight: 1));

        var badge = _style.Row();
        badge.SetGravity(GravityFlags.CenterVertical);
        badge.SetPadding(_style.Dp(20), _style.Dp(18), _style.Dp(20), _style.Dp(18));
        badge.SetBackgroundColor(Color.Argb(12, _style.Text.R, _style.Text.G, _style.Text.B));
        NativeStyle.Identify(badge, "GrindBadge");
        var label = _style.Label("", 16, true);
        label.ImportantForAccessibility = ImportantForAccessibility.No;
        badge.AddView(label, new LinearLayout.LayoutParams(0, -2, 1));
        var needsGrinder = !draft.SelectedGrinderId.HasValue;
        var needsSetup = !needsGrinder && (loaded?.Anchors is null || loaded.Anchors.Count < 2);
        if (needsGrinder || needsSetup)
        {
            var arrow = _style.Label("→", 20, color: _style.Primary);
            arrow.ImportantForAccessibility = ImportantForAccessibility.No;
            badge.AddView(arrow);
            badge.Focusable = true;
            Bind(screen, badge, () =>
            {
                if (needsGrinder)
                    RunOperation(() => OpenEquipmentSelectorAsync(EquipmentPickerKind.Grinder));
                else
                {
                    // The pinned equipmentDetail route is not registered and
                    // its equipment form has no calibration UI. Do not invent it.
                    var grinderId = draft.SelectedGrinderId;
                    ShowDrink();
                    _logger.LogWarning("Grinder {GrinderId} scale setup route is unavailable in the pinned source", grinderId);
                    ShowFeedback("Grinder scale setup is unavailable in this version.", isError: true);
                }
            });
        }
        column.AddView(badge);

        void Refresh()
        {
            description.Text = state.RangeDescription;
            description.SetTextColor(state.IsOutsidePreferredRange ? NativeStyle.Warning : _style.Secondary);
            scopeText.Text = state.RangeToggleText;
            scopeAction.ContentDescription = $"{state.RangeDescription} " +
                (state.ShowsFullRange ? "Show preferred range." : "Show full allowed range.");
            label.Text = DrinkDisplay.GrindBadge(draft.SelectedGrinderId, loaded?.GrinderName,
                loaded?.Anchors, state.StagedValue);
            badge.ContentDescription = label.Text;
            adapter.SetState(state, value => Choice(() =>
            {
                if (!ReferenceEquals(_transient, screen) || !ReferenceEquals(Draft, draft))
                    return;
                state.Select(value);
                Refresh();
            })());
            CenterList(list, state.SelectedIndex);
        }
        Bind(screen, scopeAction, () =>
        {
            state.ToggleRange();
            Refresh();
        });
        Refresh();
        _transient = screen;
        Present(column);
    }
}
