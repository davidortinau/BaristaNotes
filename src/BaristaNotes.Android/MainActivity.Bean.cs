using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private void ShowBeanForm(bool returnToBeans = false)
    {
        ClearTransient();
        _beanCreateReturnToList = returnToBeans;
        _page = "bean";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Outline);
        var screen = new NativeScreen(column);
        column.AddView(BuildHeader("NEW BEAN", "Add bean"), new LinearLayout.LayoutParams(-1, -2)
        {
            BottomMargin = _style.Dp(1)
        });
        var scroll = new ScrollView(this) { FillViewport = true };
        var fields = _style.Column();
        fields.SetBackgroundColor(_style.Surface);
        scroll.AddView(fields);
        column.AddView(scroll, _style.Fill(weight: 1));

        EditText Field(string title, string hint, string id, bool large = false, bool multiline = false)
        {
            var tile = _style.Column();
            tile.SetPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(14));
            tile.SetMinimumHeight(_style.Dp(multiline ? 150 : large ? 100 : 90));
            var caption = _style.Label(title, 10, true, _style.Secondary);
            caption.LetterSpacing = 2 * .0624f;
            tile.AddView(caption);
            var entry = new EditText(this)
            {
                Hint = hint,
                Typeface = multiline ? _style.Regular : _style.Bold,
                InputType = multiline ? InputTypes.ClassText | InputTypes.TextFlagMultiLine : InputTypes.ClassText,
                Gravity = GravityFlags.Top | GravityFlags.Start
            };
            entry.SetTextSize(Android.Util.ComplexUnitType.Sp, large ? 22 : multiline ? 16 : 18);
            entry.SetTextColor(_style.Text);
            entry.SetHintTextColor(Android.Graphics.Color.Argb(128,
                _style.Secondary.R, _style.Secondary.G, _style.Secondary.B));
            entry.SetBackgroundColor(Android.Graphics.Color.Transparent);
            entry.SetPadding(0, _style.Dp(8), 0, 0);
            entry.SetSingleLine(!multiline);
            entry.ContentDescription = title;
            NativeStyle.Identify(entry, id);
            tile.AddView(entry, new LinearLayout.LayoutParams(-1, multiline ? _style.Dp(100) : -2));
            fields.AddView(tile);
            var divider = new View(this);
            divider.SetBackgroundColor(_style.Outline);
            fields.AddView(divider, new LinearLayout.LayoutParams(-1, _style.Dp(1)));
            return entry;
        }

        var name = Field("NAME", "Bean name (required)", "BeanName", large: true);
        var roaster = Field("ROASTER", "Roaster name", "BeanRoaster");
        var origin = Field("ORIGIN", "Country or region", "BeanOrigin");
        var url = Field("ROASTER URL", "https://… (where to reorder these beans)", "BeanUrl");
        url.InputType = InputTypes.ClassText | InputTypes.TextVariationUri;
        var notes = Field("NOTES", "Tasting notes, processing method…", "BeanNotes", multiline: true);
        var (errorTile, error) = BeanErrorTile();
        NativeStyle.Identify(error, "BeanError");
        fields.AddView(errorTile);
#if NATIVE_UI_FIXTURE
        var failRead = _style.Button("TEST: fail next bag refresh", "FixtureFailBagRefresh");
        Bind(screen, failRead, () =>
        {
            _failNextBagRefresh = true;
            failRead.Text = "TEST: next refresh will fail";
        });
        fields.AddView(failRead);
#endif
        var actions = _style.Row();
        var cancel = _style.Button("CANCEL", "BeanCancel", _style.Text);
        ConfigureBeanAction(cancel);
        cancel.SetBackgroundColor(_style.Surface);
        Bind(screen, cancel, CancelBeanCreate);
        actions.AddView(cancel, new LinearLayout.LayoutParams(0, -1, 1) { RightMargin = _style.Dp(1) });
        var add = _style.Button("ADD", "BeanAdd", _style.Surface);
        ConfigureBeanAction(add);
        add.SetBackgroundColor(_style.Text);
        Bind(screen, add, () => RunOperation(async () =>
        {
            add.Text = "SAVING…";
            error.Text = "";
            errorTile.Visibility = ViewStates.Gone;
            try
            {
                var draft = new BeanDraft
                {
                    Name = name.Text ?? "",
                    Roaster = roaster.Text ?? "",
                    Origin = origin.Text ?? "",
                    Notes = notes.Text ?? "",
                    RoasterUrl = url.Text ?? ""
                };
                if (draft.ValidationError is { } validation)
                {
                    error.Text = validation;
                    errorTile.Visibility = ViewStates.Visible;
                    return;
                }
                var input = draft.ToCreateDto();
                var result = await InScopeAsync(services =>
                    services.GetRequiredService<BeanCreationWorkflow>().CreateWithInitialBagAsync(input));
                if (_destroyed || !ReferenceEquals(_transient, screen))
                    return;
                if (!result.Bean.Success || result.Bean.Data is null)
                {
                    error.Text = result.Bean.ErrorMessage ?? "Bean creation did not return the new bean.";
                    errorTile.Visibility = ViewStates.Visible;
                    return;
                }
                HideKeyboard();
                if (returnToBeans)
                {
                    // Source management reports bean success independently of
                    // an unsuccessful initial-bag result. Do not relabel it as
                    // successful bag creation.
                    if (result.InitialBag?.Success != true || result.InitialBag.Data is null)
                        _logger.LogWarning("Bean {BeanId} created with incomplete initial-bag outcome", result.Bean.Data.Id);
                    await _feedback.ShowSuccessAsync($"Bean '{result.Bean.Data.Name}' created");
                    if (!_destroyed && ReferenceEquals(_transient, screen))
                    {
                        if (!TryReturnVoiceNavigation()) await ShowBeansAsync();
                    }
                    return;
                }
                // Creation is complete. Leave the form before any read retry so
                // a refresh failure can never re-enable ADD for this creation.
                _bagsDirty = true;
                ShowDrink();
                if (result.InitialBag?.Success != true || result.InitialBag.Data is null)
                {
                    _logger.LogError("Bean {BeanId} was created but initial bag {BagId} creation failed",
                        result.Bean.Data.Id, result.InitialBag?.Data?.Id);
                    ShowFeedback($"Bean created; initial bag unavailable. {result.InitialBag?.ErrorMessage ?? "No initial bag returned."}",
                        isError: true);
                    return;
                }
                try
                {
                    await RefreshBagsAsync();
                    UpdateDrink(_editingShotId.HasValue ? _editEditor! : _newEditor!);
                    ShowFeedback($"Bean '{result.Bean.Data.Name}' created");
                }
                catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _bagRefreshError = ErrorMessage(exception);
                    _logger.LogError(exception, "Bean {BeanId} and bag {BagId} created; reference-list refresh failed",
                        result.Bean.Data.Id, result.InitialBag.Data.Id);
                    ShowFeedback("Bean created. Bag list could not refresh. Tap BAG to retry.", isError: true);
                }
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Bean creation did not complete normally");
                if (!_destroyed && ReferenceEquals(_transient, screen))
                {
                    error.Text = $"Failed to save: {ErrorMessage(exception)}";
                    errorTile.Visibility = ViewStates.Visible;
                }
            }
            finally
            {
                if (!_destroyed && ReferenceEquals(_transient, screen))
                    add.Text = "ADD";
            }
        }));
        actions.AddView(add, new LinearLayout.LayoutParams(0, -1, 1));
        column.AddView(actions, new LinearLayout.LayoutParams(-1, -2));
        _transient = screen;
        Present(column, edgeToEdge: true);
    }

    private View BuildHeader(string caption, string title, bool safeArea = true, bool compact = false)
    {
        LinearLayout header = safeArea
            ? new EdgeAwareColumn(this, () => Window?.DecorView) { Orientation = Orientation.Vertical }
            : _style.Column();
        header.SetMinimumHeight(compact ? 0 : _style.Dp(120));
        header.SetBackgroundColor(_style.Surface);
        if (header is EdgeAwareColumn edge)
            edge.SetContentPadding(_style.Dp(16), _style.Dp(14), _style.Dp(16), _style.Dp(14));
        else
            header.SetPadding(_style.Dp(16), _style.Dp(compact ? BeanPageGeometry.HeaderPadding : 14),
                _style.Dp(16), _style.Dp(compact ? BeanPageGeometry.HeaderPadding : 14));
        var label = _style.Label(caption, 10, true, _style.Secondary);
        label.LetterSpacing = 2 * .0624f;
        header.AddView(label);
        var value = _style.Label(title, 28, true);
        value.Gravity = GravityFlags.Bottom;
        header.AddView(value, compact
            ? new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(BeanPageGeometry.LabelGap) }
            : _style.Fill(weight: 1));
        if (!compact) return header;
        var outlined = new FrameLayout(this);
        outlined.SetBackgroundColor(_style.Outline);
        outlined.SetPadding(0, 0, 0, _style.Dp(BeanPageGeometry.SeparatorHeight));
        outlined.AddView(header, new FrameLayout.LayoutParams(-1, -2));
        return outlined;
    }

    private void ConfigureBeanAction(Button action)
    {
        action.Typeface = _style.Semibold;
        action.SetTextSize(Android.Util.ComplexUnitType.Sp, 18);
        action.LetterSpacing = .0624f;
        action.SetPadding(_style.Dp(8), _style.Dp(18), _style.Dp(8), _style.Dp(30));
        action.SetMinHeight(_style.Dp(72));
        action.SetMinimumHeight(_style.Dp(72));
    }

    private void HideKeyboard()
    {
        if (CurrentFocus?.WindowToken is { } token && GetSystemService(InputMethodService) is InputMethodManager keyboard)
            keyboard.HideSoftInputFromWindow(token, HideSoftInputFlags.None);
        CurrentFocus?.ClearFocus();
    }
}
