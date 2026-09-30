using Android.Views;
using Android.Widget;
using BaristaNotes.AndroidApp.Views;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp;

public sealed partial class MainActivity
{
    private bool _profilesDirty;

    private async Task RefreshProfilesAsync()
    {
        var cancellation = _lifetime.Token;
        var profiles = await InScopeAsync(services =>
            services.GetRequiredService<IUserProfileService>().GetAllProfilesAsync());
        cancellation.ThrowIfCancellationRequested();
        _newDraft.AvailableUsers = profiles;
        if (_editDraft is not null)
            _editDraft.AvailableUsers = profiles;
        _profilesDirty = false;
        _logger.LogDebug("Refreshed native profile references with {ProfileCount} profiles", profiles.Count);
    }

    private async Task OpenPeopleAsync()
    {
        var revision = _presentationRevision;
        var draft = Draft;
        if (_profilesDirty)
            await RefreshProfilesAsync();
        if (_destroyed || revision != _presentationRevision || !ReferenceEquals(Draft, draft))
            return;
        if (draft.AvailableUsers.Count == 0)
        {
            ShowProfileForm();
            return;
        }

        ClearTransient();
        _page = "picker";
        var column = _style.Column();
        column.SetBackgroundColor(_style.Surface);
        var screen = new NativeScreen(column);
        column.AddView(PickerHeader(screen, "Made by / for", ShowDrink));
        var headings = _style.Row();
        headings.SetGravity(GravityFlags.CenterVertical);
        headings.SetPadding(_style.Dp(12), _style.Dp(4), _style.Dp(12), _style.Dp(8));
        TextView Heading(string text)
        {
            var label = _style.Label(text, 11, true, _style.Secondary);
            label.LetterSpacing = 2 * .0624f;
            label.Gravity = GravityFlags.Center;
            return label;
        }
        headings.AddView(Heading("BY"), new LinearLayout.LayoutParams(0, -2, 1));
        var arrow = _style.Label("\ue941", 20, color: _style.Primary);
        arrow.Typeface = _style.Symbols;
        arrow.Gravity = GravityFlags.Center;
        headings.AddView(arrow);
        headings.AddView(Heading("FOR"), new LinearLayout.LayoutParams(0, -2, 1));
        column.AddView(headings);
        var lists = _style.Row();

        void AddPeopleColumn(string side, int? selected, Action<UserProfileDto> select)
        {
            PeopleAdapter? adapter = null;
            adapter = new PeopleAdapter(_style, side, draft.AvailableUsers, selected, profile =>
                Choice(() =>
                {
                    if (!ReferenceEquals(_transient, screen) || !ReferenceEquals(Draft, draft))
                        return;
                    select(profile);
                    adapter!.SetSelection(profile.Id);
                })(), ResolveProfileAvatarPath, _logger);
            var list = new SelectorRecyclerView(this);
            list.SetAdapter(adapter);
            screen.Own(list);
            screen.Own(adapter);
            NativeStyle.Identify(list, $"People{side}List");
            lists.AddView(list, new LinearLayout.LayoutParams(0, -1, 1));
            var selectedIndex = Math.Max(0, draft.AvailableUsers.FindIndex(profile => profile.Id == selected));
            CenterList(list, selectedIndex);
        }

        AddPeopleColumn("By", draft.SelectedMaker?.Id, profile => draft.SelectedMaker = profile);
        var divider = new View(this);
        divider.SetBackgroundColor(_style.Outline);
        lists.AddView(divider, new LinearLayout.LayoutParams(_style.Dp(1), -1));
        AddPeopleColumn("For", draft.SelectedRecipient?.Id, profile => draft.SelectedRecipient = profile);
        column.AddView(lists, _style.Fill(weight: 1));
        _transient = screen;
        Present(column);
    }
}
