using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private Action Choice(Action action) => () =>
    {
        if (_busy || _destroyed || _feedback.IsVisible)
            return;
        try { action(); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Native selector action failed");
            ShowFeedback(ErrorMessage(exception), isError: true);
        }
    };

    private SelectorRecyclerView CreateList(NativeScreen screen, OptionAdapter adapter, string id)
    {
        var list = new SelectorRecyclerView(this);
        list.SetAdapter(adapter);
        NativeStyle.Identify(list, id);
        // Detach native adapter/layout callbacks before releasing row actions.
        screen.Own(list);
        screen.Own(adapter);
        return list;
    }

    private void CenterList(SelectorRecyclerView list, int index)
    {
        if (index < 0 || _destroyed || list.Handle == IntPtr.Zero)
            return;
        // CenterOn defers measurement to OnLayout, so no queued closure needs
        // to retain the Activity or a picker that has already closed.
        list.CenterOn(index);
    }

    private FrameLayout PickerHeader(NativeScreen screen, string title, Action? done = null)
    {
        var row = new FrameLayout(this);
        row.SetPadding(_style.Dp(12), _style.Dp(8), _style.Dp(12), _style.Dp(8));
        var label = _style.Label(title.ToUpperInvariant(), 12, true, _style.Secondary);
        label.Gravity = GravityFlags.Center;
        label.LetterSpacing = 3 * .0624f;
        NativeStyle.Identify(label, "PickerTitle");
        // Source title spans all three header columns, independent of the
        // unequal Auto widths of Close and Done.
        row.AddView(label, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Center));
        var close = _style.Button("Close", "PickerClose", _style.Secondary);
        ConfigurePickerHeaderAction(close);
        Bind(screen, close, ShowDrink);
        row.AddView(close, new FrameLayout.LayoutParams(-2, -2, GravityFlags.Start | GravityFlags.CenterVertical));
        var finish = _style.Button(done is null ? "" : "Done", "PickerDone");
        ConfigurePickerHeaderAction(finish);
        finish.Typeface = _style.Bold;
        finish.Visibility = done is null ? ViewStates.Invisible : ViewStates.Visible;
        if (done is not null)
            Bind(screen, finish, done);
        row.AddView(finish, new FrameLayout.LayoutParams(-2, -2, GravityFlags.End | GravityFlags.CenterVertical));
        return row;
    }

    private void ConfigurePickerHeaderAction(Button button)
    {
        button.SetMinWidth(_style.Dp(44));
        button.SetMinimumWidth(_style.Dp(44));
        button.SetPadding(_style.Dp(14), _style.Dp(8), _style.Dp(14), _style.Dp(8));
    }

    private void ShowChoices(string title, IReadOnlyList<NativeChoice> choices)
    {
        ClearTransient();
        _page = "picker";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Surface);
        var screen = new NativeScreen(column);
        column.AddView(PickerHeader(screen, title));
        var adapter = new OptionAdapter(_style);
        adapter.SetItems(choices);
        var list = CreateList(screen, adapter, "CategoricalChoices");
        column.AddView(list, _style.Fill(weight: 1));
        _transient = screen;
        Present(column);
        CenterList(list, choices.ToList().FindIndex(item => item.Selected));
    }

    private void OpenMethod() => ShowChoices("Brew Method", BrewMethodExtensions.All.Select(method =>
        new NativeChoice($"Method_{method}", method.DisplayName(), method == Draft.BrewMethod, Choice(() =>
        {
            using var scope = _app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<DrinkWorkflow>().ChangeBrewMethod(
                draft: Draft, method: method, isEditing: _editingShotId.HasValue);
            ShowDrink();
        }))).ToArray());

    private void OpenDrinkType() => ShowChoices("Drink Type", Draft.BrewMethod.DrinkTypesFor().Select(type =>
        new NativeChoice($"DrinkType_{type.Replace(" ", "")}", type, type == Draft.DrinkType,
            Choice(() => { Draft.DrinkType = type; ShowDrink(); }))).ToArray());

    private void OpenRating() => ShowChoices("Rating", Enumerable.Range(0, 5).Select(rating =>
        new NativeChoice($"Rating_{rating}", DrinkDisplay.RatingText(rating), rating == Draft.Rating,
            Choice(() => { Draft.Rating = rating; ShowDrink(); }))).ToArray());

    private async Task OpenBagAsync()
    {
        if (_bagRefreshError is not null)
        {
            ShowBagReadError();
            return;
        }
        if (_bagsDirty)
            await RefreshBagsAsync();
        if (Draft.AvailableBags.Count == 0)
        {
            ShowBeanForm();
            return;
        }

        ShowChoices("Bag", Draft.AvailableBags.Select(bag =>
            new NativeChoice($"Bag_{bag.Id}", $"{bag.BeanName} · Roasted {bag.FormattedRoastDate}",
                bag.Id == Draft.SelectedBagId, Choice(() =>
                {
                    Draft.SelectedBagId = bag.Id;
                    ShowDrink();
                }))).ToArray());
    }

    private void ShowBagReadError()
    {
        ClearTransient();
        _page = "picker";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Surface);
        var screen = new NativeScreen(column);
        column.AddView(PickerHeader(screen, "Bag"));
        var message = _style.Label(_bagRefreshError ?? "The bag list could not be loaded.");
        message.SetPadding(_style.Dp(24), _style.Dp(24), _style.Dp(24), _style.Dp(24));
        NativeStyle.Identify(message, "BagRefreshError");
        column.AddView(message);
        var retry = _style.Button("Retry bag list", "BagRefreshRetry");
        Bind(screen, retry, () => RunOperation(async () =>
        {
            try
            {
                await RefreshBagsAsync();
                await OpenBagAsync();
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _bagRefreshError = ErrorMessage(exception);
                _logger.LogError(exception, "Retrying native bag reference-list read failed");
                message.Text = _bagRefreshError;
            }
        }));
        column.AddView(retry);
        _transient = screen;
        Present(column);
    }

    private void OpenMassPicker(bool isYield)
    {
        var draft = Draft;
        var range = _app.Services.GetRequiredService<IDrinkValueRangeService>().Resolve(
            isYield ? DrinkValueMetric.Yield : DrinkValueMetric.DoseIn, draft.BrewMethod);
        var state = new MassPickerState(range, isYield ? draft.ExpectedOutput : draft.DoseIn);
        ClearTransient();
        _page = "picker";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Surface);
        var screen = new NativeScreen(column);
        column.AddView(PickerHeader(screen, isYield ? "Yield" : "Dose In", () =>
        {
            if (isYield)
                draft.ExpectedOutput = state.DoneValue;
            else
                draft.DoseIn = state.DoneValue;
            ShowDrink();
        }));
        var (scopeBackground, scopeAction, description, scopeLabel) = CreateRangeScopeBar();
        column.AddView(scopeBackground);
        var wholeAdapter = new OptionAdapter(_style, numeric: true);
        var tenthAdapter = new OptionAdapter(_style, numeric: true);
        var wholeList = CreateList(screen, wholeAdapter, "MassWholeList");
        var tenthList = CreateList(screen, tenthAdapter, "MassTenthList");

        void RefreshSelectionAndCenter()
        {
            wholeAdapter.SetSelection($"MassWhole_{state.SelectedWhole}");
            tenthAdapter.SetSelection($"MassTenth_{state.SelectedTenth}");
            // A fractional boundary can change both components even when only
            // one column was tapped or the visible range was toggled.
            CenterList(wholeList, state.WholeValues.ToList().IndexOf(state.SelectedWhole));
            CenterList(tenthList, state.TenthValues.ToList().IndexOf(state.SelectedTenth));
        }
        void RefreshScope()
        {
            description.Text = state.RangeDescription;
            description.SetTextColor(state.IsOutsidePreferredRange ? NativeStyle.Warning : _style.Secondary);
            scopeLabel.Text = state.RangeToggleText;
            scopeAction.ContentDescription = $"{state.RangeDescription} " +
                (state.ShowsFullRange ? "Show preferred range." : "Show full allowed range.");
            wholeAdapter.SetItems(state.WholeValues.Select(value => new NativeChoice(
                $"MassWhole_{value}", value.ToString(), false, Choice(() =>
                {
                    state.SelectWhole(value);
                    RefreshSelectionAndCenter();
                }))).ToArray());
            RefreshSelectionAndCenter();
        }
        tenthAdapter.SetItems(state.TenthValues.Select(value => new NativeChoice(
            $"MassTenth_{value}", value.ToString(), false, Choice(() =>
            {
                state.SelectTenth(value);
                RefreshSelectionAndCenter();
            }))).ToArray());
        Bind(screen, scopeAction, () =>
        {
            state.ToggleRange();
            RefreshScope();
        });
        var lists = _style.Row();
        lists.AddView(wholeList, new LinearLayout.LayoutParams(0, -1, 1));
        var dot = _style.Label(".", 48, color: _style.Secondary);
        dot.Gravity = GravityFlags.CenterVertical;
        lists.AddView(dot, new LinearLayout.LayoutParams(-2, -1));
        lists.AddView(tenthList, new LinearLayout.LayoutParams(0, -1, 1));
        var unit = _style.Label("g", 20, color: _style.Secondary);
        unit.Gravity = GravityFlags.CenterVertical;
        lists.AddView(unit, new LinearLayout.LayoutParams(-2, -1));
        column.AddView(lists, _style.Fill(weight: 1));
        RefreshScope();
        _transient = screen;
        Present(column);
    }
}
