using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private bool _settingsFromHistory;

    private void OpenSettings()
    {
        _settingsFromHistory = _page == "history";
        ShowSettings();
    }

    private async Task ShowProfilesAsync()
    {
        ClearTransient();
        _page = "profiles";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Outline);
        column.SetPadding(_style.Dp(1), _style.Dp(1), _style.Dp(1), _style.Dp(1));
        var screen = new NativeScreen(column);
        var header = (ViewGroup)BuildHeader("PROFILES", "0 profiles");
        var count = (TextView)header.GetChildAt(1)!;
        NativeStyle.Identify(count, "ProfileCount");
        column.AddView(header, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = _style.Dp(1) });
        var body = new FrameLayout(this);
        body.SetBackgroundColor(_style.Surface);
        var adapter = new ProfileListAdapter(_style, ResolveProfileAvatarPath,
            id => Choice(() => ShowProfileForm(returnToProfiles: true, profileId: id))(), _logger);
        var list = new SelectorRecyclerView(this);
        list.SetAdapter(adapter);
        screen.Own(list);
        screen.Own(adapter);
        list.SetBackgroundColor(_style.Outline);
        list.Visibility = ViewStates.Gone;
        NativeStyle.Identify(list, "ProfileList");
        body.AddView(list, new FrameLayout.LayoutParams(-1, -1));
        var status = _style.Column();
        status.SetGravity(GravityFlags.Center);
        status.SetPadding(_style.Dp(32), _style.Dp(24), _style.Dp(32), _style.Dp(24));
        var progress = new ProgressBar(this);
        status.AddView(progress);
        var title = _style.Label("", 10, true, _style.Secondary);
        title.LetterSpacing = 2 * .0624f;
        title.Gravity = GravityFlags.Center;
        title.Visibility = ViewStates.Gone;
        status.AddView(title);
        var message = _style.Label("");
        message.Gravity = GravityFlags.Center;
        message.Visibility = ViewStates.Gone;
        NativeStyle.Identify(message, "ProfileListMessage");
        status.AddView(message, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(12) });
        var retry = _style.Button("Retry", "ProfileListRetry", _style.Surface);
        retry.Background = _style.Rounded(_style.Primary, 8);
        retry.Visibility = ViewStates.Gone;
        Bind(screen, retry, () => RunOperation(ShowProfilesAsync));
        status.AddView(retry, new LinearLayout.LayoutParams(-2, -2) { TopMargin = _style.Dp(12) });
        body.AddView(status, new FrameLayout.LayoutParams(-1, -1));
        column.AddView(body, _style.Fill(weight: 1));
        var navigation = BuildNavigation(screen,
            ("\uefef", "New Drink", "NavDrink", () => { _editingShotId = null; ShowDrink(); }),
            ("\uf009", "Activity", "NavActivity", () => RunOperation(ShowHistoryAsync)),
            ("\ue8b8", "Settings", "ProfilesSettings", ShowSettings),
            ("\ue145", "Add profile", "ProfilesAdd", () => ShowProfileForm(returnToProfiles: true)));
        var add = (Button)((LinearLayout)navigation).GetChildAt(3)!;
        add.SetBackgroundColor(_style.Text);
        add.SetTextColor(_style.Surface);
        column.AddView(navigation, new LinearLayout.LayoutParams(-1, -2) { TopMargin = _style.Dp(1) });
        _transient = screen;
        Present(column, edgeToEdge: true);
        try
        {
            await RefreshProfilesAsync();
            if (_destroyed || !ReferenceEquals(_transient, screen))
                return;
            var profiles = _newDraft.AvailableUsers;
            count.Text = profiles.Count == 1 ? "1 profile" : $"{profiles.Count} profiles";
            adapter.SetItems(profiles);
            progress.Visibility = ViewStates.Gone;
            if (profiles.Count == 0)
            {
                status.SetPadding(_style.Dp(32), _style.Dp(32), _style.Dp(32), _style.Dp(32));
                title.Text = "NO PROFILES";
                message.Text = "Add household members or coffee personas";
                title.Visibility = message.Visibility = ViewStates.Visible;
            }
            else
            {
                status.Visibility = ViewStates.Gone;
                list.Visibility = ViewStates.Visible;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to load native profile list");
            if (_destroyed || !ReferenceEquals(_transient, screen))
                return;
            progress.Visibility = ViewStates.Gone;
            status.SetPadding(_style.Dp(24), _style.Dp(24), _style.Dp(24), _style.Dp(24));
            title.Text = "ERROR";
            message.Text = ErrorMessage(exception);
            title.Visibility = message.Visibility = ViewStates.Visible;
            retry.Visibility = ViewStates.Visible;
        }
    }

    private Task ReturnFromProfileFormAsync() => _profileEditor is { } editor
        ? ReturnFromProfileEditorAsync(editor)
        : Task.CompletedTask;
}
