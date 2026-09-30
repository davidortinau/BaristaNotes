using Android.Views;
using Android.Text;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class ProfileListAdapter(NativeStyle style, Func<string?, string?> resolveImage,
    Action<int> edit, ILogger logger) : RecyclerView.Adapter
{
    private IReadOnlyList<UserProfileDto> _profiles = [];
    private readonly List<ProfileHolder> _holders = [];
    private Func<string?, string?>? _resolveImage = resolveImage;
    private Action<int>? _edit = edit;

    public void SetItems(IReadOnlyList<UserProfileDto> profiles)
    {
        _profiles = profiles;
        NotifyDataSetChanged();
    }

    public override int ItemCount => _profiles.Count;

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var holder = new ProfileHolder(style, logger);
        _holders.Add(holder);
        return holder;
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var cell = (ProfileHolder)holder;
        var profile = _profiles[position];
        cell.Row.LayoutParameters = new RecyclerView.LayoutParams(-1, -2) { BottomMargin = style.Dp(1) };
        cell.Name.Text = profile.Name;
        cell.Action.ContentDescription = $"Member: {profile.Name}";
        cell.Activate = () => _edit?.Invoke(profile.Id);
        cell.RefreshFocus();
        NativeStyle.Identify(cell.Action, $"Profile_{profile.Id}");
        cell.Avatar.SetAutomationPrefix($"ProfileListAvatar_{profile.Id}");
        _ = cell.Avatar.LoadFileAsync(_resolveImage?.Invoke(profile.AvatarPath));
    }

    public override void OnViewRecycled(Java.Lang.Object holder)
    {
        if (holder is ProfileHolder cell)
        {
            cell.Activate = null;
            _ = cell.Avatar.LoadFileAsync(null);
        }
        base.OnViewRecycled(holder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var holder in _holders)
                holder.Release();
            _holders.Clear();
            _profiles = [];
            _resolveImage = null;
            _edit = null;
        }
        base.Dispose(disposing);
    }

    private sealed class ProfileHolder : RecyclerView.ViewHolder
    {
        private readonly EventHandler _click;
        private readonly EventHandler<View.FocusChangeEventArgs> _focus;
        private readonly NativeStyle _style;
        public FrameLayout Row { get; }
        public TextView Name { get; }
        public Button Action { get; }
        public ProfileAvatarView Avatar { get; }
        public System.Action? Activate { get; set; }

        public ProfileHolder(NativeStyle style, ILogger logger) : base(new FrameLayout(style.Context))
        {
            Row = (FrameLayout)ItemView;
            _style = style;
            Row.SetBackgroundColor(style.Surface);
            Row.SetMinimumHeight(style.Dp(80));
            var content = style.Row();
            content.SetGravity(GravityFlags.CenterVertical);
            content.SetPadding(style.Dp(16), style.Dp(16), style.Dp(16), style.Dp(16));
            content.ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
            Avatar = new ProfileAvatarView(style, logger, 48);
            content.AddView(Avatar, new LinearLayout.LayoutParams(style.Dp(48), style.Dp(48))
            {
                LeftMargin = style.Dp(8), TopMargin = style.Dp(8),
                RightMargin = style.Dp(20), BottomMargin = style.Dp(8)
            });
            var labels = style.Column();
            var caption = style.Label("MEMBER", 10, true, style.Secondary);
            caption.LetterSpacing = 2 * .0624f;
            labels.AddView(caption);
            Name = style.Label("", 20, true);
            Name.SetSingleLine(true);
            Name.Ellipsize = TextUtils.TruncateAt.End;
            labels.AddView(Name);
            content.AddView(labels, new LinearLayout.LayoutParams(0, -2, 1));
            var glyph = style.Label("\ue5cc", 24);
            glyph.Typeface = style.Symbols;
            glyph.SetTextSize(Android.Util.ComplexUnitType.Dip, 24);
            content.AddView(glyph, new LinearLayout.LayoutParams(-2, -2) { LeftMargin = style.Dp(12) });
            Row.AddView(content, new FrameLayout.LayoutParams(-1, -2));
            Action = style.Button("", "ProfileAction");
            Action.SetPadding(0, 0, 0, 0);
            Row.AddView(Action, new FrameLayout.LayoutParams(-1, -1));
            _click = (_, _) => Activate?.Invoke();
            _focus = (_, _) => RefreshFocus();
            Action.Click += _click;
            Action.FocusChange += _focus;
        }

        public void RefreshFocus() => Row.Background = _style.Rounded(_style.Surface, 0,
            Action.HasFocus ? _style.IsDark ? _style.Text : _style.Primary : _style.Surface);

        public void Release()
        {
            Activate = null;
            Action.Click -= _click;
            Action.FocusChange -= _focus;
            Avatar.Dispose();
        }
    }
}
