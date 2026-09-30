using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private FrameLayout EquipmentSelectorHeader(NativeScreen screen, string title, Action clear, Action? done)
    {
        var header = new FrameLayout(this);
        header.SetPadding(_style.Dp(12), _style.Dp(8), _style.Dp(12), _style.Dp(8));
        var label = _style.Label(title.ToUpperInvariant(), 12, true, _style.Secondary);
        label.LetterSpacing = 3 * .0624f;
        label.Gravity = GravityFlags.Center;
        NativeStyle.Identify(label, "PickerTitle");
        header.AddView(label, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Center));
        var close = _style.Button("Close", "PickerClose", _style.Secondary);
        ConfigurePickerHeaderAction(close);
        Bind(screen, close, ShowDrink);
        header.AddView(close, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Start | GravityFlags.CenterVertical));
        var right = _style.Row();
        var clearButton = _style.Button("Clear", "EquipmentSelectorClear", _style.Secondary);
        ConfigurePickerHeaderAction(clearButton);
        Bind(screen, clearButton, clear);
        right.AddView(clearButton);
        if (done is not null)
        {
            var finish = _style.Button("Done", "PickerDone", _style.Primary);
            ConfigurePickerHeaderAction(finish);
            finish.Typeface = _style.Bold;
            Bind(screen, finish, done);
            right.AddView(finish);
        }
        header.AddView(right, new FrameLayout.LayoutParams(-2, -2, GravityFlags.End | GravityFlags.CenterVertical));
        return header;
    }

    private async Task OpenEquipmentSelectorAsync(EquipmentPickerKind kind)
    {
        var draft = Draft;
        var revision = _presentationRevision;
        List<EquipmentDto> equipment;
        try
        {
            equipment = await RefreshEquipmentReferencesAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _equipmentDirty = true;
            _logger.LogError(exception, "Failed to read equipment for {Picker}", kind);
            if (!_destroyed && revision == _presentationRevision && ReferenceEquals(Draft, draft))
                ShowEquipmentSelectorReadError(kind, ErrorMessage(exception));
            return;
        }
        if (_destroyed || revision != _presentationRevision || !ReferenceEquals(Draft, draft))
            return;
        var items = equipment.Where(item => kind switch
        {
            EquipmentPickerKind.Machine => item.Type == EquipmentType.Machine,
            EquipmentPickerKind.Grinder => item.Type == EquipmentType.Grinder,
            _ => item.Type != EquipmentType.Machine && item.Type != EquipmentType.Grinder
        }).ToList();
        if (items.Count == 0)
        {
            await ShowEquipmentFormAsync(EquipmentReturn.Drink, preset: kind switch
            {
                EquipmentPickerKind.Machine => EquipmentType.Machine,
                EquipmentPickerKind.Grinder => EquipmentType.Grinder,
                _ => EquipmentType.Tamper
            });
            return;
        }
        ShowEquipmentSelector(kind, draft, items);
    }

    private void ShowEquipmentSelector(EquipmentPickerKind kind, DrinkDraft draft, List<EquipmentDto> items)
    {
        ClearTransient();
        _page = "picker";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Surface);
        var screen = new NativeScreen(column);
        var multi = kind == EquipmentPickerKind.Accessories;
        column.AddView(EquipmentSelectorHeader(screen, kind.ToString(), () =>
        {
            if (multi)
                draft.SelectedAccessoryIds = [];
            else if (kind == EquipmentPickerKind.Machine)
                draft.SelectedMachineId = null;
            else
                draft.SelectedGrinderId = null;
            ShowDrink();
        }, multi ? ShowDrink : null));
        if (multi)
        {
            var adapter = new AccessoryOptionAdapter(_style);
            var list = CreateOwnedEquipmentList(screen, adapter, "AccessoryChoices");
            void Refresh()
            {
                adapter.SetItems(items, draft.SelectedAccessoryIds, id => Choice(() =>
                {
                    if (!ReferenceEquals(_transient, screen) || !ReferenceEquals(Draft, draft))
                        return;
                    if (!draft.SelectedAccessoryIds.Remove(id))
                        draft.SelectedAccessoryIds.Add(id);
                    Refresh();
                })());
                CenterList(list, items.FindIndex(item => draft.SelectedAccessoryIds.Contains(item.Id)));
            }
            column.AddView(list, _style.Fill(weight: 1));
            Refresh();
        }
        else
        {
            var selectedId = kind == EquipmentPickerKind.Machine ? draft.SelectedMachineId : draft.SelectedGrinderId;
            var adapter = new OptionAdapter(_style);
            adapter.SetItems(items.Select(item => new NativeChoice($"EquipmentChoice_{item.Id}",
                item.Name, item.Id == selectedId, Choice(() =>
                {
                    if (!ReferenceEquals(_transient, screen) || !ReferenceEquals(Draft, draft))
                        return;
                    if (kind == EquipmentPickerKind.Machine)
                        draft.SelectedMachineId = item.Id;
                    else
                        draft.SelectedGrinderId = item.Id;
                    ShowDrink();
                }))).ToArray());
            var list = CreateOwnedEquipmentList(screen, adapter, "EquipmentChoices");
            column.AddView(list, _style.Fill(weight: 1));
            CenterList(list, items.FindIndex(item => item.Id == selectedId));
        }
        _transient = screen;
        Present(column);
    }

    private void ShowEquipmentSelectorReadError(EquipmentPickerKind kind, string error)
    {
        ClearTransient();
        _page = "picker";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Surface);
        var screen = new NativeScreen(column);
        column.AddView(PickerHeader(screen, kind.ToString()));
        var body = _style.Column();
        body.SetGravity(GravityFlags.Center);
        body.SetPadding(_style.Dp(24), _style.Dp(24), _style.Dp(24), _style.Dp(24));
        var message = _style.Label(error, 16);
        message.Gravity = GravityFlags.Center;
        NativeStyle.Identify(message, "EquipmentSelectorReadError");
        body.AddView(message);
        var retry = _style.Button("Retry equipment list", "EquipmentSelectorRetry");
        Bind(screen, retry, () => RunOperation(() => OpenEquipmentSelectorAsync(kind)));
        body.AddView(retry);
        column.AddView(body, _style.Fill(weight: 1));
        _transient = screen;
        Present(column);
    }
}
