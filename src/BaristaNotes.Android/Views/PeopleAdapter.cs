using Android.Text;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging;
using AccessibilityNodeInfo = Android.Views.Accessibility.AccessibilityNodeInfo;

namespace BaristaNotes.AndroidApp.Views;

internal sealed class PeopleAdapter : RecyclerView.Adapter
{
    private readonly NativeStyle _style;
    private readonly string _side;
    private readonly List<PersonHolder> _holders = [];
    private readonly IReadOnlyList<UserProfileDto> _profiles;
    private Action<UserProfileDto>? _select;
    private int? _selectedId;
    private Func<string?, string?>? _resolveImage;
    private readonly ILogger _logger;

    public PeopleAdapter(NativeStyle style, string side, IReadOnlyList<UserProfileDto> profiles,
        int? selectedId, Action<UserProfileDto> select, Func<string?, string?> resolveImage, ILogger logger)
    {
        _style = style;
        _side = side;
        _profiles = profiles;
        _selectedId = selectedId;
        _select = select;
        _resolveImage = resolveImage;
        _logger = logger;
        HasStableIds = true;
    }

    public override int ItemCount => _profiles.Count;
    public override long GetItemId(int position) => _profiles[position].Id;

    public void SetSelection(int id)
    {
        var previous = _selectedId;
        if (previous == id)
            return;
        _selectedId = id;
        for (var index = 0; index < _profiles.Count; index++)
            if (_profiles[index].Id == previous || _profiles[index].Id == id)
                NotifyItemChanged(index);
    }

    public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
    {
        var holder = new PersonHolder(_style, _logger);
        _holders.Add(holder);
        return holder;
    }

    public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
    {
        var cell = (PersonHolder)holder;
        var profile = _profiles[position];
        var selected = profile.Id == _selectedId;
        var size = selected ? 72 : 52;
        cell.Root.Action = _select is { } select ? () => select(profile) : null;
        cell.Root.Selected = selected;
        cell.Root.ContentDescription = $"Made {_side.ToLowerInvariant()}: {profile.Name}";
        cell.Avatar.LayoutParameters = new LinearLayout.LayoutParams(_style.Dp(size), _style.Dp(size))
        {
            Gravity = GravityFlags.CenterHorizontal
        };
        cell.Avatar.ConfigurePeople(size, selected);
        cell.Avatar.SetAutomationPrefix($"People{_side}Avatar_{profile.Id}");
        _ = cell.Avatar.LoadFileAsync(_resolveImage?.Invoke(profile.AvatarPath));
        cell.Name.Text = profile.Name;
        cell.Name.Typeface = selected ? _style.Bold : _style.Regular;
        cell.Name.SetTextColor(selected ? _style.Primary : _style.Text);
        cell.Name.SetTextSize(Android.Util.ComplexUnitType.Sp, selected ? 18 : 14);
        NativeStyle.Identify(cell.Root, $"People{_side}_{profile.Id}");
    }

    public override void OnViewRecycled(Java.Lang.Object holder)
    {
        if (holder is PersonHolder cell)
        {
            cell.Root.Action = null;
            _ = cell.Avatar.LoadFileAsync(null);
        }
        base.OnViewRecycled(holder);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _select = null;
            _resolveImage = null;
            foreach (var holder in _holders)
            {
                holder.Root.Action = null;
                holder.Avatar.Dispose();
            }
            _holders.Clear();
        }
        base.Dispose(disposing);
    }

    private sealed class PersonHolder : RecyclerView.ViewHolder
    {
        public PersonRow Root { get; }
        public ProfileAvatarView Avatar { get; }
        public TextView Name { get; }

        public PersonHolder(NativeStyle style, ILogger logger) : base(new PersonRow(style.Context))
        {
            Root = (PersonRow)ItemView;
            Root.Orientation = Orientation.Vertical;
            Root.SetPadding(style.Dp(8), style.Dp(14), style.Dp(8), style.Dp(14));
            Root.LayoutParameters = new RecyclerView.LayoutParams(-1, -2);
            Avatar = new ProfileAvatarView(style, logger, 52);
            Avatar.ImportantForAccessibility = ImportantForAccessibility.No;
            Root.AddView(Avatar);
            Name = style.Label("", 14);
            Name.SetSingleLine(true);
            Name.Ellipsize = TextUtils.TruncateAt.End;
            Name.Gravity = GravityFlags.Center;
            Name.ImportantForAccessibility = ImportantForAccessibility.No;
            Root.AddView(Name, new LinearLayout.LayoutParams(-1, -2) { TopMargin = style.Dp(6) });
        }
    }

    private sealed class PersonRow(Android.Content.Context context) : LinearLayout(context)
    {
        public Action? Action { get; set; }

        protected override void OnAttachedToWindow()
        {
            base.OnAttachedToWindow();
            Clickable = Focusable = true;
            FocusableInTouchMode = true;
            ImportantForAccessibility = ImportantForAccessibility.Yes;
        }

        public override bool PerformClick()
        {
            if (!Enabled || Action is null)
                return false;
            base.PerformClick();
            Action();
            return true;
        }

        public override void OnInitializeAccessibilityNodeInfo(AccessibilityNodeInfo? info)
        {
            base.OnInitializeAccessibilityNodeInfo(info);
            if (info is null)
                return;
            info.ClassName = "android.widget.Button";
            info.Clickable = true;
            info.Selected = Selected;
        }
    }
}
