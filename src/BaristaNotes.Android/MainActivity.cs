using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OperationCanceledException = System.OperationCanceledException;

namespace BaristaNotes.AndroidApp;

[Activity(
    Name = "com.simplyprofound.baristanotes.nativeapp.MainActivity",
    Theme = "@style/Barista.SplashTheme",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    WindowSoftInputMode = SoftInput.AdjustResize)]
public sealed partial class MainActivity : Activity
{
    private NativeApplication _app = null!;
    private NativeStyle _style = null!;
    private ILogger<MainActivity> _logger = null!;
    private FrameLayout _root = null!;
    private FrameLayout _host = null!;
    private readonly CancellationTokenSource _lifetime = new();
    private NativeFeedbackPresenter _feedback = null!;
    private NativeScreen? _transient;
    private NativeScreen? _historyScreen;
    private DrinkEditor? _newEditor;
    private DrinkEditor? _editEditor;
    private readonly DrinkDraft _newDraft = new();
    private DrinkDraft? _editDraft;
    private int? _editingShotId;
    private ShotFilterCriteria _filters = new();
    private IDataChangeNotifier? _notifier;
    private string _page = "starting";
    private bool _busy;
    private bool _destroyed;
    private bool _bagsDirty;
    private string? _bagRefreshError;
    private long _presentationRevision;

    private DrinkDraft Draft => _editingShotId.HasValue ? _editDraft! : _newDraft;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        _app = Application as NativeApplication
            ?? throw new InvalidOperationException("Native application host is unavailable.");
        _app.ThemeService.ApplyCurrentMode();
        SetTheme(_app.ThemeService.IsDark ? Resource.Style.Barista_DarkTheme : Resource.Style.Barista_MainTheme);
        base.OnCreate(savedInstanceState);
        _logger = _app.LoggerFactory.CreateLogger<MainActivity>();
        _style = new NativeStyle(this, _app.ThemeService.IsDark);
        _root = new FrameLayout(this);
        _host = new FrameLayout(this);
        _root.SetBackgroundColor(_style.Surface);
        _root.AddView(_host, new FrameLayout.LayoutParams(-1, -1));
        SetContentView(_root);
        ConfigurePerformance(Intent);
        _feedback = new NativeFeedbackPresenter(this, _style,
            _app.LoggerFactory.CreateLogger<NativeFeedbackPresenter>(), blocked =>
            {
                _host.Enabled = !blocked && _filterScreen is null && _equipmentConfirmation is null && _advicePopup is null && !VoiceBlocksInput && !PhotoBlocksInput;
                _host.ImportantForAccessibility = blocked || _filterScreen is not null || _equipmentConfirmation is not null || _advicePopup is not null || VoiceBlocksInput || PhotoBlocksInput
                    ? ImportantForAccessibility.NoHideDescendants : ImportantForAccessibility.Auto;
                _photoSession?.SetCovered(blocked || VoiceBlocksInput);
            });
        _notifier = _app.Services.GetRequiredService<IDataChangeNotifier>();
        _notifier.DataChanged += OnDataChanged;
        _app.ThemeService.Changed += OnNativeThemeChanged;
        ApplyNativeWindowTheme();
        RunOperation(InitializeAsync);
    }

    private async Task InitializeAsync()
    {
        ShowStatus("Preparing data", loading: true);
        await _app.Services.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        await LoadDraftAsync(_newDraft, editingShotId: null);
        _lifetime.Token.ThrowIfCancellationRequested();
        _editingShotId = null;
        ShowDrink();
        NotifyPerformanceAppReady();
    }

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, Task<T>> operation)
    {
        var cancellation = _lifetime.Token;
        var result = await Task.Run(async () =>
        {
            using var scope = _app.Services.CreateScope();
            return await operation(scope.ServiceProvider);
        }, cancellation);
        cancellation.ThrowIfCancellationRequested();
        return result;
    }

    private async Task LoadDraftAsync(DrinkDraft draft, int? editingShotId)
    {
        var loaded = await InScopeAsync(services =>
            services.GetRequiredService<DrinkWorkflow>().LoadAsync(editingShotId: editingShotId));
        using var scope = _app.Services.CreateScope();
        // This continuation runs on Android's main SynchronizationContext.
        scope.ServiceProvider.GetRequiredService<DrinkWorkflow>().ApplyLoadedData(draft, loaded);
        _bagsDirty = false;
        _profilesDirty = false;
        _equipmentDirty = false;
    }

    private async Task RefreshBagsAsync()
    {
#if NATIVE_UI_FIXTURE
        if (_failNextBagRefresh)
        {
            _failNextBagRefresh = false;
            throw new IOException("Controlled UI fixture: bag refresh failed after creation.");
        }
#endif
        var bags = await InScopeAsync(services =>
            services.GetRequiredService<IBagService>().GetActiveBagsForShotLoggingAsync());
        _logger.LogDebug("Refreshed native bag references with {BagCount} selectable bags", bags.Count);
        _newDraft.AvailableBags = bags;
        if (_editDraft is not null)
            _editDraft.AvailableBags = bags;
        _bagsDirty = false;
        _bagRefreshError = null;
    }

    private void OnDataChanged(object? sender, DataChangedEventArgs args)
    {
        if (args.ChangeType is DataChangeType.EquipmentCreated or DataChangeType.EquipmentUpdated)
        {
            RunOnUiThread(() =>
            {
                if (!_destroyed)
                {
                    _equipmentDirty = true;
                    _equipmentRevision++;
                }
            });
        }
        if (args.ChangeType is DataChangeType.ProfileCreated or DataChangeType.ProfileUpdated)
        {
            RunOnUiThread(() =>
            {
                if (!_destroyed)
                    _profilesDirty = true;
            });
        }
        if (args.ChangeType is DataChangeType.BeanCreated or DataChangeType.BeanUpdated
            or DataChangeType.BagCreated or DataChangeType.BagUpdated)
        {
            RunOnUiThread(() =>
            {
                if (!_destroyed)
                    _bagsDirty = true;
            });
        }
        QueueVoiceReferenceRefresh(args.ChangeType);
    }

    private async void RunOperation(Func<Task> operation)
    {
        if (_busy || _destroyed)
            return;
        _busy = true;
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The operation may finish its source-defined data write, but its
            // destroyed Activity must never receive a late UI update.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Native slice operation failed on {Page}", _page);
            if (!_destroyed)
            {
                var message = ErrorMessage(exception);
                if (_page == "starting")
                    ShowStatus(message, loading: false);
                else
                    ShowFeedback(message, isError: true);
            }
        }
        finally
        {
            _busy = false;
        }
    }

    private static string ErrorMessage(Exception exception) =>
        exception is BaristaNotes.Core.Services.Exceptions.ValidationException validation
            ? validation.Errors.SelectMany(pair => pair.Value).FirstOrDefault() ?? exception.Message
            : exception.Message;

    private void Bind(NativeScreen screen, View view, Action action) => screen.Click(view, () =>
    {
        if (_busy || _destroyed || _feedback.IsVisible || _equipmentConfirmation is not null || _advicePopup is not null || VoiceBlocksInput || PhotoBlocksInput
            || (_filterScreen is not null && (!ReferenceEquals(screen, _filterScreen) || !_filterReady)))
            return;
        try { action(); }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Native view action failed on {Page}", _page);
            ShowFeedback(ErrorMessage(exception), isError: true);
        }
    });

    private void Present(View view, bool edgeToEdge = false)
    {
        CancelAdviceWhenLeaving(view);
        CancelPhotoWhenLeaving(view);
        _presentationRevision++;
        var windowHost = Window?.DecorView as ViewGroup
            ?? throw new InvalidOperationException("The Android window host is unavailable.");
        var desiredParent = edgeToEdge ? windowHost : _root;
        if (!ReferenceEquals(_host.Parent, desiredParent))
        {
            (_host.Parent as ViewGroup)?.RemoveView(_host);
            // Keep page content below the native system-bar backgrounds; transient
            // popup families are separately attached above those backgrounds.
            if (edgeToEdge)
                windowHost.AddView(_host, Math.Min(1, windowHost.ChildCount), new FrameLayout.LayoutParams(-1, -1));
            else
                _root.AddView(_host, 0, new FrameLayout.LayoutParams(-1, -1));
        }
        _host.RemoveAllViews();
        _host.AddView(view, new FrameLayout.LayoutParams(-1, -1));
        ApplyNativeWindowTheme();
        _voiceOverlay?.BringToFront();
        UpdateVoiceInputGate();
    }

    private void ClearTransient()
    {
        if (_transient is null)
            return;
        _host.RemoveView(_transient.Root);
        _transient.Dispose();
        _transient = null;
    }

    private void ShowStatus(string message, bool loading)
    {
        ClearTransient();
        var column = _style.Column();
        column.SetGravity(GravityFlags.Center);
        column.SetPadding(_style.Dp(24), _style.Dp(24), _style.Dp(24), _style.Dp(24));
        var screen = new NativeScreen(column);
        if (loading)
            column.AddView(new ProgressBar(this));
        var label = _style.Label(message);
        label.Gravity = GravityFlags.Center;
        NativeStyle.Identify(label, "InitializationStatus");
        column.AddView(label);
        if (!loading)
        {
            var retry = _style.Button("Retry", "InitializationRetry");
            Bind(screen, retry, () => RunOperation(InitializeAsync));
            column.AddView(retry);
        }
        _transient = screen;
        Present(column);
    }

    private void ShowFeedback(string message, bool isError = false) => _feedback.Show(message, isError);

    public override void OnBackPressed()
    {
        if (_feedback.IsVisible)
        {
            _feedback.Dismiss();
            return;
        }
        if (_advicePopup is not null)
        {
            _advicePopup.RequestClose();
            return;
        }
        if (PhotoBlocksInput)
        {
            _photoSession?.RequestBack();
            return;
        }
        if (_equipmentConfirmation is not null)
        {
            _equipmentConfirmation.RequestCancel();
            return;
        }
        if (TryReturnVoiceNavigation()) return;
        if (_page == "equipmentForm" && _equipmentEditor is { } equipmentEditor)
        {
            CancelEquipmentForm(equipmentEditor);
            return;
        }
        if (_page == "bagDetail" && _bagEditor is { } bagEditor)
        {
            ObserveBeanTask(() => ReturnFromBagAsync(bagEditor));
            return;
        }
        if (_page == "beanDetail")
        {
            HideKeyboard();
            ObserveBeanTask(ShowBeansAsync);
            return;
        }
        if (_busy)
            return;
        if (_filterScreen is not null)
            CloseFilter();
        else if (_page == "rangeEditor")
            RunOperation(LeaveRangeEditorAsync);
        else if (_page == "ranges")
            ShowSettings();
        else if (_page == "profile")
            RunOperation(ReturnFromProfileFormAsync);
        else if (_page == "profiles")
            ShowSettings();
        else if (_page == "equipment")
            ShowSettings();
        else if (_page == "beans")
            ShowSettings();
        else if (_page == "bean")
            CancelBeanCreate();
        else if (_page == "settings")
        {
            if (_settingsFromHistory)
                RunOperation(ShowHistoryAsync);
            else
                ShowDrink();
        }
        else if (_page == "picker")
            ShowDrink();
        else if (_editingShotId.HasValue && _beanShotReturn is not null)
            ObserveBeanTask(ReturnToBeanFromShotAsync);
        else if (_editingShotId.HasValue)
            RunOperation(ShowHistoryAsync);
        else if (_page == "history")
            ShowDrink();
        else if (OperatingSystem.IsAndroidVersionAtLeast(33))
            MoveTaskToBack(true);
        else
            base.OnBackPressed();
    }

    protected override void OnDestroy()
    {
        _destroyed = true;
        DisposePerformance();
        if (_photoSession is { } photo) ReleasePhotoSession(photo);
        DisposeVoice();
        _app.ThemeService.Changed -= OnNativeThemeChanged;
        DisposeAdvice();
        _rangeConfirmation?.Dismiss();
        _equipmentConfirmation?.Dispose();
        _lifetime.Cancel();
        _feedback.Dispose();
        if (_notifier is not null)
            _notifier.DataChanged -= OnDataChanged;
        DisposeFilter();
        _host.RemoveAllViews();
        (_host.Parent as ViewGroup)?.RemoveView(_host);
        _transient?.Dispose();
        _beanListReturnState?.Dispose();
        _beanListReturnState = null;
        _beanMapReturnState = null;
        _newEditor?.Screen.Dispose();
        _editEditor?.Screen.Dispose();
        _historyScreen?.Dispose();
        _lifetime.Dispose();
        _style.Dispose();
        base.OnDestroy();
        _appearanceContext?.Dispose();
        _appearanceContext = null;
    }

    partial void ConfigurePerformance(Intent? intent);
    partial void NotifyPerformanceAppReady();
    partial void DisposePerformance();
}
