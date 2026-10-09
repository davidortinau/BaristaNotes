using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed partial class DrinkViewController : SliceViewController
{
    private readonly int? _editingId;
    private readonly DrinkDraft _draft = new();
    private readonly List<DrinkTile> _tiles = [];
    private readonly List<UIButton> _navigation = [];
    private readonly UIView _navigationTopBorder = SliceUi.NavigationTopBorder();
    private DrinkTile? _save;
    private readonly UILabel _loading = SliceUi.Label("Loading…", 18);
    private readonly PeopleTileContent _people = new();
    private UIButton? _retry;
    private bool _loaded;
    private bool _busy;
    private bool _openingPicker;
    private int _recipeVersion;
    private Func<Task>? _retryRead;

    public DrinkViewController(SliceNavigationController host, int? editingId = null) : base(host) => _editingId = editingId;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = NativeTheme.Outline;
        AddTile("METHOD", "drink.method", static owner => owner.PickMethod());
        AddTile("BAG", "drink.bag", static owner => _ = owner.GuardAsync(owner.OpenBagAsync),
            static owner => _ = owner.OpenRecipeForSelectedBagAsync());
        AddTile("DRINK TYPE", "drink.type", static owner => owner.PickDrinkType());
        AddTile("RATING", "drink.rating", static owner => owner.PickRating());
        AddTile("DOSE IN", "ShotTile_DoseIn", static owner => owner.OpenMass(true));
        AddTile("YIELD", "ShotTile_Yield", static owner => owner.OpenMass(false));
        AddTile("TIME", "ShotTile_Time", static owner => owner.OpenTime());
        AddTile("GRIND", "ShotTile_Grind", static owner => _ = owner.GuardAsync(owner.OpenGrindAsync));
        AddTile("WATER TEMP", "drink.temperature", static owner => owner.OpenWaterTemperature());
        AddTile("MADE BY / FOR", "drink.people", static owner => _ = owner.GuardAsync(owner.OpenPeopleAsync));
        AddTile("MACHINE", "drink.machine", static owner => _ = owner.GuardAsync(() => owner.OpenEquipmentAsync(EquipmentSelectionKind.Machine)));
        AddTile("GRINDER", "drink.grinder", static owner => _ = owner.GuardAsync(() => owner.OpenEquipmentAsync(EquipmentSelectionKind.Grinder)));
        _tiles[9].SetCustomContent(_people);
        _save = new DrinkTile(_editingId.HasValue ? "UPDATE" : "SAVE", "drink.save",
            WeakUiCallback.Create(this, static owner => _ = owner.GuardAsync(owner.SaveAsync)), true);
        Root.AddSubview(_save);
        _navigation.Add(SliceUi.Icon("\uf009", "Activity", "nav.activity",
            WeakUiCallback.Create(this, static owner =>
            {
                if (owner._loaded) owner.Host.Activity();
            })));
        _navigation.Add(SliceUi.Icon("\ue8b8", "Settings", "nav.settings",
            WeakUiCallback.Create(this, static owner =>
            {
                if (owner._loaded) owner.Host.Settings();
            })));
        _navigation.Add(SliceUi.Icon("\ue029", "Voice", "nav.voice",
            WeakUiCallback.Create(this, static owner =>
            {
                if (owner._loaded) _ = owner.Host.ToggleVoiceAsync();
            })));
        _navigation.Add(SliceUi.Icon(_editingId.HasValue ? "\uf136" : "\ue412",
            _editingId.HasValue ? "AI advice" : "Camera", _editingId.HasValue ? "nav.advice" : "nav.camera",
            WeakUiCallback.Create(this, static owner =>
            {
                if (!owner._loaded) return;
                if (owner._editingId.HasValue) _ = owner.RequestAdviceAsync();
                else owner.OpenPhoto();
            })));
        Root.AddSubviews(_navigation.ToArray());
        Root.AddSubview(_navigationTopBorder);
        _loading.AccessibilityIdentifier = "drink.loading";
        _loading.TextAlignment = UITextAlignment.Center;
        _loading.BackgroundColor = NativeTheme.Surface;
        _loading.Hidden = true;
        Root.AddSubview(_loading);
        _retry = SliceUi.Button("Retry", "drink.retry",
            WeakUiCallback.Create(this, static owner => _ = owner.GuardAsync(owner._retryRead ?? owner.LoadAsync)));
        _retry.Hidden = true;
        Root.AddSubview(_retry);
        UpdateTiles();
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        _adviceVisible = true;
        if (!_loaded) _voiceReadiness = GuardAsync(LoadAsync);
        else _voiceReadiness = GuardAsync(RefreshReferencesAsync);
    }

    private void AddTile(string label, string id, Action<DrinkViewController> action,
        Action<DrinkViewController>? longPress = null)
    {
        Action Guarded(Action<DrinkViewController> callback) =>
            WeakUiCallback.Create(this, callback, static (owner, activate) =>
        {
            if (!owner._busy && !owner._openingPicker && owner._loaded &&
                owner.Host.IsSceneAttached && owner.Host.TopViewController == owner &&
                !owner.Host.FeedbackHost.IsShowing && owner.Host.PresentedViewController == null)
                activate(owner);
        });
        var tile = new DrinkTile(label, id, Guarded(action),
            longPress: longPress == null ? null : Guarded(longPress));
        _tiles.Add(tile);
        Root.AddSubview(tile);
    }

    private async Task LoadAsync()
    {
        _loading.Hidden = true;
        if (_retry != null) _retry.Hidden = true;
        try
        {
            await Services.InitializeAsync();
            var loaded = await Services.RunAsync(provider => provider.GetRequiredService<DrinkWorkflow>().LoadAsync(_editingId));
            Services.OnUi(provider => provider.GetRequiredService<DrinkWorkflow>().ApplyLoadedData(_draft, loaded));
            _retryRead = null;
            _loaded = true;
            UpdateTiles();
            Host.Performance.DrinkReady(Host);
#if DEBUG
            var testForm = NativeReadFaults.TakeLaunchForm();
            if (testForm == "bean") Host.PushViewController(new BeanDetailViewController(Host), false);
            else if (testForm == "profile") Host.PushViewController(new ProfileCreateViewController(Host), false);
#endif
        }
        catch
        {
            _loaded = false;
            _retryRead = LoadAsync;
            _loading.Text = "Could not load drink.";
            _loading.Hidden = false;
            if (_retry != null) _retry.Hidden = false;
            throw;
        }
    }

    public async Task RefreshBagsAsync()
    {
        try
        {
#if DEBUG
            NativeReadFaults.BeforeBagRead(Logger);
#endif
            _draft.AvailableBags = await Services.RunAsync(provider =>
                provider.GetRequiredService<IBagService>().GetActiveBagsForShotLoggingAsync());
            _retryRead = null;
            _loading.Hidden = true;
            if (_retry != null) _retry.Hidden = true;
            UpdateTiles();
        }
        catch
        {
            ShowReadRetry("Could not refresh bags.", RefreshBagsAsync);
            throw;
        }
    }

    private void ShowReadRetry(string message, Func<Task> retry)
    {
        _retryRead = retry;
        _loading.Text = message;
        _loading.Hidden = false;
        if (_retry != null) _retry.Hidden = false;
    }

    private async Task RefreshReferencesAsync()
    {
        _draft.TempUnit = Services.Singleton<IPreferencesService>().GetTemperatureUnit();
        UpdateTiles();
        await RefreshBagsAsync();
        await RefreshPeopleAsync();
        await RefreshEquipmentAsync();
        Host.Performance.DrinkReady(Host);
    }

    private async Task RefreshEquipmentAsync()
    {
        _draft.AvailableEquipment = (await Services.RunAsync(provider =>
            provider.GetRequiredService<IEquipmentService>().GetAllActiveEquipmentAsync())).ToList();
        UpdateTiles();
    }

    // The pinned grid has an Accessories selector implementation but no rendered
    // entry tile. Keep the controller path without inventing a new product entry.
    internal Task OpenAccessoriesAsync() => OpenEquipmentAsync(EquipmentSelectionKind.Accessories);

    private async Task OpenEquipmentAsync(EquipmentSelectionKind kind)
    {
        if (_openingPicker) return;
        _openingPicker = true;
        try
        {
            await RefreshEquipmentAsync();
            if (Host.TopViewController != this) return;
            var items = _draft.AvailableEquipment.Where(item => kind switch
            {
                EquipmentSelectionKind.Machine => item.Type == EquipmentType.Machine,
                EquipmentSelectionKind.Grinder => item.Type == EquipmentType.Grinder,
                _ => item.Type != EquipmentType.Machine && item.Type != EquipmentType.Grinder
            }).ToList();
            if (items.Count == 0)
            {
                Host.PushViewController(new EquipmentFormViewController(Host, preset: kind switch
                {
                    EquipmentSelectionKind.Machine => EquipmentType.Machine,
                    EquipmentSelectionKind.Grinder => EquipmentType.Grinder,
                    _ => EquipmentType.Tamper
                }), false);
                return;
            }
            var selected = kind == EquipmentSelectionKind.Accessories ? _draft.SelectedAccessoryIds :
                kind == EquipmentSelectionKind.Machine
                    ? _draft.SelectedMachineId is int machine ? [machine] : new List<int>()
                    : _draft.SelectedGrinderId is int grinder ? [grinder] : new List<int>();
            var weak = new WeakReference<DrinkViewController>(this);
            Host.PushViewController(new EquipmentSelectionViewController(Host, kind, items, selected, ids =>
            {
                if (!weak.TryGetTarget(out var owner)) return;
                if (kind == EquipmentSelectionKind.Machine) owner._draft.SelectedMachineId = ids.Count > 0 ? ids[0] : null;
                else if (kind == EquipmentSelectionKind.Grinder) owner._draft.SelectedGrinderId = ids.Count > 0 ? ids[0] : null;
                else owner._draft.SelectedAccessoryIds = ids.ToList();
                owner.UpdateTiles();
            }), false);
        }
        finally { _openingPicker = false; }
    }

    private async Task OpenGrindAsync()
    {
        if (_openingPicker) return;
        _openingPicker = true;
        try
        {
            var definition = Services.Singleton<IDrinkValueRangeService>().Resolve(DrinkValueMetric.GrindMicrons, _draft.BrewMethod);
            GrindPickerLoadResult loaded;
            try
            {
                loaded = await Services.RunAsync(provider => provider.GetRequiredService<GrindPickerWorkflow>().LoadAsync(_draft));
            }
            catch (Exception error)
            {
                // Preserve the source log-and-open fallback, not a new silent success.
                Logger.LogError(error, "Failed to load grind picker; opening source fallback");
                loaded = new GrindPickerLoadResult(_draft.GrindMicrons ?? (int)definition.Default, null, null, false);
            }
            if (Host.TopViewController != this) return;
            var weak = new WeakReference<DrinkViewController>(this);
            Host.PushViewController(new GrindViewController(Host, definition, _draft.GrindMicrons, loaded,
                _draft.SelectedGrinderId, value =>
                {
                    if (!weak.TryGetTarget(out var owner)) return;
                    owner._draft.GrindMicrons = value;
                    owner.UpdateTiles();
                    owner.Host.PopViewController(false);
                }, () =>
                {
                    if (weak.TryGetTarget(out var owner))
                        _ = owner.GuardAsync(() => owner.OpenEquipmentAsync(EquipmentSelectionKind.Grinder));
                }), false);
        }
        finally { _openingPicker = false; }
    }

    private async Task RefreshPeopleAsync()
    {
        try
        {
            _draft.AvailableUsers = await Services.RunAsync(provider =>
                provider.GetRequiredService<IUserProfileService>().GetAllProfilesAsync());
            _retryRead = null;
            _loading.Hidden = true;
            if (_retry != null) _retry.Hidden = true;
            UpdateTiles();
        }
        catch
        {
            ShowReadRetry("Could not refresh profiles.", RefreshPeopleAsync);
            throw;
        }
    }

    private async Task OpenPeopleAsync()
    {
        await RefreshPeopleAsync();
        if (_draft.AvailableUsers.Count == 0)
            Host.PushViewController(new ProfileCreateViewController(Host), true);
        else
            Host.PushViewController(new PeopleViewController(Host, _draft, UpdateTiles), false);
    }

    private async Task OpenBagAsync()
    {
        await RefreshBagsAsync();
        if (_draft.AvailableBags.Count == 0)
        {
            Host.PushViewController(new BeanDetailViewController(Host), true);
            return;
        }
        var rows = _draft.AvailableBags.Select(bag => new ChoiceRow(
            $"bag.{bag.Id}", $"{bag.BeanName} · Roasted {bag.FormattedRoastDate}",
            _draft.SelectedBagId == bag.Id, () =>
            {
                _draft.SelectedBagId = bag.Id;
                UpdateTiles();
                Host.PopViewController(false);
            })).ToList();
        Host.PushViewController(new ChoiceViewController(Host, "BAG", rows), false);
    }

    private async Task OpenRecipeForSelectedBagAsync()
    {
        var bag = _draft.AvailableBags.FirstOrDefault(item => item.Id == _draft.SelectedBagId);
        if (bag == null)
        {
            Host.FeedbackHost.Show("Select a bag first to view its recipe.", NativeFeedbackKind.Information);
            return;
        }
        var method = _draft.BrewMethod;
        var version = ++_recipeVersion;
        bool IsCurrent() => version == _recipeVersion && Host.IsSceneAttached && Host.TopViewController == this;
        _openingPicker = true;
        try
        {
            var recipe = await Services.RunAsync(provider =>
                provider.GetRequiredService<IRecipeService>().GetRecipeForBeanAndMethodAsync(bag.BeanId, method));
            if (!IsCurrent()) return;
            if (recipe == null)
            {
                Host.FeedbackHost.Show($"No {method.DisplayName()} recipe for {bag.BeanName} yet.", NativeFeedbackKind.Information);
                return;
            }
            Host.PushViewController(new BeanDetailViewController(Host, bag.BeanId), false);
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Failed to open recipe for {BagId}", bag.Id);
            if (IsCurrent()) ShowFeedback("Couldn't open the recipe.", isError: true);
        }
        finally { _openingPicker = false; }
    }

    private void PickMethod()
    {
        var rows = BrewMethodExtensions.All.Select(method => new ChoiceRow(
            $"method.{method}", method.DisplayName(), _draft.BrewMethod == method, () =>
            {
                Services.OnUi(provider => provider.GetRequiredService<DrinkWorkflow>().ChangeBrewMethod(_draft, method, _editingId.HasValue));
                UpdateTiles();
                Host.PopViewController(false);
            })).ToList();
        Host.PushViewController(new ChoiceViewController(Host, "BREW METHOD", rows), false);
    }

    private void PickDrinkType() => Host.PushViewController(new ChoiceViewController(Host, "DRINK TYPE",
        _draft.BrewMethod.DrinkTypesFor().Select(drink => new ChoiceRow($"type.{drink}", drink, _draft.DrinkType == drink,
            () => { _draft.DrinkType = drink; UpdateTiles(); Host.PopViewController(false); })).ToList()), false);

    private void PickRating() => Host.PushViewController(new ChoiceViewController(Host, "RATING",
        Enumerable.Range(0, 5).Select(rating => new ChoiceRow($"rating.{rating}",
            DrinkDisplay.RatingText(rating), _draft.Rating == rating,
            () => { _draft.Rating = rating; UpdateTiles(); Host.PopViewController(false); })).ToList()), false);

    private void OpenMass(bool dose)
    {
        var definition = Services.Singleton<IDrinkValueRangeService>().Resolve(
            dose ? DrinkValueMetric.DoseIn : DrinkValueMetric.Yield, _draft.BrewMethod);
        Host.PushViewController(new MassViewController(Host, dose ? "DOSE IN" : "YIELD",
            new MassPickerState(definition, dose ? _draft.DoseIn : _draft.ExpectedOutput), value =>
            {
                if (dose) _draft.DoseIn = value;
                else _draft.ExpectedOutput = value;
                UpdateTiles();
                Host.PopViewController(false);
            }), false);
    }

    private void OpenTime()
    {
        var range = Services.Singleton<IDrinkValueRangeService>().Resolve(DrinkValueMetric.Time, _draft.BrewMethod);
        Host.PushViewController(new NumericViewController(Host, "Time", range,
            _draft.ActualTime ?? _draft.ExpectedTime,
            value => DrinkDisplay.TimeValue(value) + (DrinkDisplay.TimeUnit(value) ?? ""),
            value =>
            {
                _draft.ActualTime = _draft.ExpectedTime = value;
                UpdateTiles();
                Host.PopViewController(false);
            }), false);
    }

    private void OpenWaterTemperature()
    {
        var fahrenheit = _draft.TempUnit == TemperatureUnit.Fahrenheit;
        var range = fahrenheit ? new DrinkValueRange(150, 212) : new DrinkValueRange(65, 100);
        var current = _draft.WaterTempC.HasValue
            ? fahrenheit ? (decimal)DrinkDisplay.CelsiusToFahrenheit(_draft.WaterTempC.Value) : _draft.WaterTempC.Value
            : fahrenheit ? 200 : 93;
        var definition = new EffectiveDrinkValueRange(range, range, fahrenheit ? 200 : 93,
            fahrenheit ? 1m : 0.5m, fahrenheit ? "F" : "C", ValueRangeSource.Auto);
        Host.PushViewController(new NumericViewController(Host, "Water Temp", definition, current,
            value => value.ToString(fahrenheit ? "0" : "0.#") + (fahrenheit ? "\u00b0F" : "\u00b0C"),
            value =>
            {
                _draft.WaterTempC = fahrenheit ? DrinkDisplay.FahrenheitToCelsius((double)value) : value;
                UpdateTiles();
                Host.PopViewController(false);
            }, useStagedValueAsOriginal: true), false);
    }

    private async Task SaveAsync()
    {
        if (_busy || !_loaded || Host.FeedbackHost.IsShowing) return;
        _busy = true;
        if (_save != null) _save.Enabled = false;
        try
        {
            var saved = await Services.RunAsync(provider => provider.GetRequiredService<DrinkWorkflow>().SaveAsync(_draft, _editingId));
            Logger.LogInformation("Native drink save completed for {ShotId}, editing {Editing}", saved.Id, _editingId.HasValue);
            if (!_editingId.HasValue) await LoadAsync();
            ShowFeedback(_editingId.HasValue ? "Drink updated" : $"{_draft.DrinkType} logged");
        }
        finally { _busy = false; if (_save != null) _save.Enabled = true; }
    }

    private void UpdateTiles()
    {
        if (_tiles.Count == 0) return;
        if (_draft.SelectedMaker is { } maker)
            _draft.SelectedMaker = _draft.AvailableUsers.FirstOrDefault(user => user.Id == maker.Id);
        if (_draft.SelectedRecipient is { } recipient)
            _draft.SelectedRecipient = _draft.AvailableUsers.FirstOrDefault(user => user.Id == recipient.Id);
        _tiles[0].SetValue(_draft.BrewMethod.DisplayName());
        _tiles[1].SetValue(_draft.AvailableBags.FirstOrDefault(bag => bag.Id == _draft.SelectedBagId)?.BeanName ?? _draft.BeanName ?? "—");
        _tiles[2].SetValue(_draft.DrinkType);
        _tiles[3].SetValue(DrinkDisplay.RatingText(_draft.Rating));
        _tiles[4].SetValue(_draft.DoseIn.ToString("0.#"), "g");
        _tiles[5].SetValue(_draft.ExpectedOutput.ToString("0.#"), "g");
        _tiles[6].SetValue(DrinkDisplay.TimeValue(_draft.ActualTime ?? _draft.ExpectedTime),
            DrinkDisplay.TimeUnit(_draft.ActualTime ?? _draft.ExpectedTime));
        _tiles[7].SetValue(_draft.GrindMicrons?.ToString() ?? "—", _draft.GrindMicrons.HasValue ? "µm" : null);
        _tiles[8].SetValue(DrinkDisplay.WaterTemperatureValue(_draft.WaterTempC, _draft.TempUnit),
            DrinkDisplay.WaterTemperatureUnit(_draft.WaterTempC, _draft.TempUnit));
        _tiles[9].SetValue($"{_draft.SelectedMaker?.Name ?? "—"} → {_draft.SelectedRecipient?.Name ?? "—"}");
        _people.Update(_draft.SelectedMaker, _draft.SelectedRecipient,
            Services.Singleton<IImageProcessingService>(), Logger);
        _tiles[10].SetValue(_draft.AvailableEquipment.FirstOrDefault(x => x.Id == _draft.SelectedMachineId)?.Name ?? "—");
        _tiles[11].SetValue(_draft.AvailableEquipment.FirstOrDefault(x => x.Id == _draft.SelectedGrinderId)?.Name ?? "—");
        _save?.SetValue(_editingId.HasValue ? "Update" : "Log Drink");
        Root.SetNeedsLayout();
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var width = Root.Bounds.Width;
        var height = Root.Bounds.Height;
        var column = (width - 5) / 4;
        var tileWidth = column * 2 + 1;
        var topInset = (nfloat)Math.Max(0, Root.SafeAreaLayoutGuide.LayoutFrame.Top - 1);
        var first = (nfloat)Math.Max(_tiles[0].MeasureHeight(tileWidth, topInset), _tiles[1].MeasureHeight(tileWidth, topInset));
        var bottom = SliceUi.LayoutNavigation(Root, _navigation, _navigationTopBorder);
        var row = (height - first - bottom - 9) / 6;
        for (var i = 0; i < _tiles.Count; i++)
        {
            var index = i / 2;
            _tiles[i].TopInset = index == 0 ? topInset : 0;
            _tiles[i].Frame = new CGRect(1 + i % 2 * (2 * column + 2), index == 0 ? 1 : first + 2 + (index - 1) * (row + 1),
                tileWidth, index == 0 ? first : row);
        }
        if (_save != null) _save.Frame = new CGRect(1, first + 2 + 5 * (row + 1), width - 2, row);
        _loading.Frame = Root.Bounds;
        if (_retry != null) _retry.Frame = new CGRect(24, Root.SafeAreaInsets.Top + 88, width - 48, 48);
        LayoutAdviceBar(height - bottom - 4);
    }
}
