using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class ProfilesViewController : SliceViewController
{
    private readonly EquipmentHeader _header = new("profiles.header");
    private readonly UICollectionView _list = new(CGRect.Empty, new UICollectionViewFlowLayout
    {
        MinimumLineSpacing = 0, MinimumInteritemSpacing = 0
    });
    private readonly UIActivityIndicatorView _loading = new(UIActivityIndicatorViewStyle.Medium);
    private readonly UIView _state = new();
    private readonly UILabel _stateTitle = new();
    private readonly UILabel _stateMessage = SliceUi.Label("", 16);
    private readonly List<UIButton> _navigation = [];
    private readonly UIView _navigationTopBorder = SliceUi.NavigationTopBorder();
    private readonly ProfileSource _source;
    private readonly ProfileLayout _layout;
    private UIButton? _retry;
    private long _loadVersion;
    private bool _visible;

    public ProfilesViewController(SliceNavigationController host) : base(host)
    {
        var weak = new WeakReference<ProfilesViewController>(this);
        _source = new ProfileSource(profile =>
        {
            if (weak.TryGetTarget(out var owner) && owner.Host.TopViewController == owner &&
                !owner.Host.FeedbackHost.IsShowing && owner.Host.PresentedViewController == null)
                owner.Host.PushHierarchy(new ProfileCreateViewController(owner.Host, profile.Id));
        }, Services.Singleton<IImageProcessingService>(), Logger);
        _layout = new ProfileLayout(_source);
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = NativeTheme.Outline;
        _header.Update("PROFILES", "0 profiles");
        _list.AccessibilityIdentifier = "profiles.list";
        _list.BackgroundColor = NativeTheme.Outline;
        _list.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
        _list.DataSource = _source;
        _list.Delegate = _layout;
        _list.RegisterClassForCell(typeof(ProfileCell), "profile");
        _loading.AccessibilityIdentifier = "profiles.loading";
        _loading.AccessibilityLabel = "Loading profiles";
        _loading.Color = NativeTheme.Primary;
        _state.AccessibilityIdentifier = "profiles.state";
        _state.BackgroundColor = NativeTheme.Surface;
        _stateTitle.TextAlignment = _stateMessage.TextAlignment = UITextAlignment.Center;
        _retry = SliceUi.Button("Retry", "profiles.retry",
            WeakUiCallback.Create(this, static owner => _ = owner.RefreshAsync()));
        _retry.BackgroundColor = NativeTheme.Primary;
        _retry.SetTitleColor(NativeTheme.Surface, UIControlState.Normal);
        _state.AddSubviews(_stateTitle, _stateMessage, _retry);
        _navigation.Add(SliceUi.Icon("\uefef", "New Drink", "nav.drink", Host.NewDrink));
        _navigation.Add(SliceUi.Icon("\uf009", "Activity", "nav.activity", Host.Activity));
        _navigation.Add(SliceUi.Icon("\ue8b8", "Settings", "nav.settings", Host.RequestBack));
        var add = SliceUi.Icon("\ue145", "Add profile", "profiles.add", WeakUiCallback.Create(this, static owner =>
        {
            if (owner.Host.TopViewController == owner && !owner.Host.FeedbackHost.IsShowing && owner.Host.PresentedViewController == null)
                owner.Host.PushHierarchy(new ProfileCreateViewController(owner.Host));
        }));
        add.TitleLabel.Font = NativeTheme.Icons(24);
        add.BackgroundColor = NativeTheme.TextPrimary;
        add.SetTitleColor(NativeTheme.Surface, UIControlState.Normal);
        _navigation.Add(add);
        Root.AddSubviews(_header, _list, _state, _loading);
        Root.AddSubviews(_navigation.ToArray());
        Root.AddSubview(_navigationTopBorder);
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((ProfilesViewController)environment).UpdateFonts());
        UpdateFonts();
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        _visible = true;
        _ = RefreshAsync();
    }

    public override void ViewDidDisappear(bool animated)
    {
        _visible = false;
        _loadVersion++;
        _loading.StopAnimating();
        base.ViewDidDisappear(animated);
    }

    private async Task RefreshAsync()
    {
        var version = ++_loadVersion;
        _state.Hidden = _list.Hidden = true;
        _loading.StartAnimating();
        try
        {
            var profiles = await Services.RunAsync(provider =>
                provider.GetRequiredService<IUserProfileService>().GetAllProfilesAsync());
            if (!_visible || version != _loadVersion) return;
            _source.Profiles = profiles;
            _header.Update("PROFILES", profiles.Count == 1 ? "1 profile" : $"{profiles.Count} profiles");
            _list.ReloadData();
            _list.Hidden = profiles.Count == 0;
            if (profiles.Count == 0)
                ShowState("NO PROFILES", "Add household members or coffee personas", false);
            Root.SetNeedsLayout();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Native profile list failed to load");
            if (!_visible || version != _loadVersion) return;
            ShowState("ERROR", exception.Message, true);
        }
        finally
        {
            if (_visible && version == _loadVersion) _loading.StopAnimating();
        }
    }

    private void ShowState(string title, string message, bool retry)
    {
        SourceScaledText.Tracked(_stateTitle, title, 10, 2, NativeTheme.Secondary, TraitCollection);
        _stateMessage.Text = message;
        if (_retry != null) _retry.Hidden = !retry;
        _state.Hidden = false;
        Root.SetNeedsLayout();
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var width = Root.Bounds.Width;
        var topInset = (nfloat)Math.Max(0, Root.SafeAreaInsets.Top - 1);
        _header.TopInset = topInset;
        var headerHeight = _header.SizeThatFits(new CGSize(width - 2, nfloat.MaxValue)).Height;
        var bottom = SliceUi.LayoutNavigation(Root, _navigation, _navigationTopBorder);
        _header.TopInset = topInset;
        _header.Frame = new CGRect(1, 1, width - 2, headerHeight);
        var body = new CGRect(1, headerHeight + 2, width - 2,
            (nfloat)Math.Max(0, Root.Bounds.Height - headerHeight - bottom - 4));
        _list.Frame = _state.Frame = body;
        _loading.Frame = new CGRect(body.GetMidX() - 20, body.GetMidY() - 20, 40, 40);
        var inset = _retry is { Hidden: false } ? 24 : 32;
        var title = SliceUi.Measure(_stateTitle, body.Width - 2 * inset);
        var message = SliceUi.Measure(_stateMessage, body.Width - 2 * inset);
        var retryHeight = _retry is { Hidden: false } ? 56 : 0;
        var stateTop = (body.Height - title.Height - message.Height - 12 - retryHeight) / 2;
        _stateTitle.Frame = new CGRect(inset, stateTop, body.Width - 2 * inset, title.Height);
        _stateMessage.Frame = new CGRect(inset, stateTop + title.Height + 12, body.Width - 2 * inset, message.Height);
        if (_retry != null)
            _retry.Frame = new CGRect((body.Width - 100) / 2, _stateMessage.Frame.Bottom + 12, 100, 44);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _visible = false;
            _loadVersion++;
        }
        base.Dispose(disposing);
    }

    private void UpdateFonts()
    {
        _source.FontTraits = TraitCollection;
        _header.UpdateFonts(TraitCollection);
        SourceScaledText.Tracked(_stateTitle, _stateTitle.Text ?? "", 10, 2, NativeTheme.Secondary, TraitCollection);
        _stateMessage.Font = SourceScaledText.Font(16, false, TraitCollection);
        if (_retry != null) _retry.TitleLabel.Font = SourceScaledText.Font(14, false, TraitCollection);
        _list.ReloadData();
        Root.SetNeedsLayout();
    }

#pragma warning disable CS0672, CA1422
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        if (!OperatingSystem.IsIOSVersionAtLeast(17) && IsViewLoaded) UpdateFonts();
    }
#pragma warning restore CS0672, CA1422

    private sealed class ProfileSource(Action<UserProfileDto> activate, IImageProcessingService images, ILogger logger) : UICollectionViewDataSource
    {
        public List<UserProfileDto> Profiles { get; set; } = [];
        public UITraitCollection FontTraits { get; set; } = new();
        public override nint GetItemsCount(UICollectionView collectionView, nint section) => Profiles.Count;
        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (ProfileCell)collectionView.DequeueReusableCell("profile", indexPath);
            var profile = Profiles[(int)indexPath.Item];
            string? path = null;
            if (!string.IsNullOrEmpty(profile.AvatarPath) && images.ImageExists(profile.AvatarPath))
                path = images.GetImagePath(profile.AvatarPath);
            cell.Bind(profile, path, () => activate(profile), FontTraits, logger);
            return cell;
        }
        public void Activate(int index)
        {
            if (index >= 0 && index < Profiles.Count) activate(Profiles[index]);
        }
    }

    private static class ProfileRowMetrics
    {
        public const int AvatarSize = 48;
        public const int AvatarMargin = 8;
        public const int TilePadding = 16;
        public const int TileStroke = 1;
        public const int ColumnSpacing = 12;
        public const int MinimumTileHeight = 80;
        public const int BottomMargin = 1;
        public const int AvatarExtent = AvatarSize + 2 * AvatarMargin;
        public const int ContentInset = TilePadding + TileStroke;
        public const int TextLeft = ContentInset + AvatarExtent + ColumnSpacing;

        public static nfloat TextWidth(nfloat width, nfloat trailingWidth) =>
            (nfloat)Math.Max(1, width - 2 * ContentInset - AvatarExtent - 2 * ColumnSpacing - trailingWidth);

        public static nfloat TileHeight(nfloat textHeight, nfloat trailingHeight) =>
            (nfloat)Math.Max(MinimumTileHeight,
                Math.Max(AvatarExtent, Math.Max(textHeight, trailingHeight)) + 2 * ContentInset);
    }

    private sealed class ProfileLayout(ProfileSource source) : UICollectionViewDelegateFlowLayout
    {
        private readonly UILabel _caption = new() { Text = "MEMBER", Font = NativeTheme.Font(10, true), Lines = 1 };
        private readonly UILabel _name = new() { Font = NativeTheme.Font(20, true), Lines = 1 };
        private readonly UILabel _chevron = new() { Text = "\ue5cc", Font = NativeTheme.Icons(24), Lines = 1 };
        public override CGSize GetSizeForItem(UICollectionView collectionView, UICollectionViewLayout layout, NSIndexPath indexPath)
        {
            _name.Text = source.Profiles[(int)indexPath.Item].Name;
            _name.Font = SourceScaledText.Font(20, true, source.FontTraits);
            _caption.Font = SourceScaledText.Font(10, true, source.FontTraits);
            var trailing = SliceUi.Measure(_chevron, collectionView.Bounds.Width);
            var textWidth = ProfileRowMetrics.TextWidth(collectionView.Bounds.Width, trailing.Width);
            var textHeight = SliceUi.Measure(_caption, textWidth).Height + SliceUi.Measure(_name, textWidth).Height;
            return new CGSize(collectionView.Bounds.Width,
                ProfileRowMetrics.TileHeight(textHeight, trailing.Height) + ProfileRowMetrics.BottomMargin);
        }
        public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
        {
            collectionView.DeselectItem(indexPath, false);
            source.Activate((int)indexPath.Item);
        }
    }

    [Register("NativeProfileManagementCell")]
    private sealed class ProfileCell : UICollectionViewCell
    {
        private readonly UIView _surface = new() { BackgroundColor = NativeTheme.Surface };
        private readonly UILabel _caption = new();
        private readonly UILabel _name = SliceUi.Label("", 20, true);
        private readonly ProfileAvatarView _avatar = new(ProfileRowMetrics.AvatarSize);
        private readonly UILabel _chevron = SliceUi.Label("\ue5cc", 24);
        private readonly UIView _divider = new() { BackgroundColor = NativeTheme.Outline };
        private Action? _activate;

        public ProfileCell(ObjCRuntime.NativeHandle handle) : base(handle)
        {
            BackgroundColor = NativeTheme.Surface;
            IsAccessibilityElement = true;
            AccessibilityTraits = UIAccessibilityTrait.Button;
            SliceUi.HeaderLabel(_caption, "MEMBER");
            _caption.Lines = _name.Lines = 1;
            _caption.LineBreakMode = _name.LineBreakMode = UILineBreakMode.TailTruncation;
            _surface.Layer.BorderWidth = ProfileRowMetrics.TileStroke;
            _surface.Layer.BorderColor = NativeTheme.Surface.CGColor;
            _chevron.Font = NativeTheme.Icons(24);
            _surface.AddSubviews(_avatar, _caption, _name, _chevron);
            ContentView.AddSubviews(_surface, _divider);
        }

        public void Bind(UserProfileDto profile, string? path, Action activate, UITraitCollection traits, ILogger logger)
        {
            AccessibilityIdentifier = $"profiles.member.{profile.Id}";
            AccessibilityLabel = $"Member: {profile.Name}";
            _name.Text = profile.Name;
            _activate = activate;
            SourceScaledText.Tracked(_caption, "MEMBER", 10, 2, NativeTheme.Secondary, traits);
            _name.Font = SourceScaledText.Font(20, true, traits);
            try { _avatar.SetPath(path, logger); }
            catch (Exception error)
            {
                logger.LogError(error, "Could not load profile row image for {ProfileId}", profile.Id);
                _avatar.SetPath(null);
            }
            _avatar.UpdateFonts(traits);
            SetNeedsLayout();
        }

        public override bool AccessibilityActivate()
        {
            if (_activate == null) return false;
            _activate();
            return true;
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            var width = ContentView.Bounds.Width;
            var height = ContentView.Bounds.Height - ProfileRowMetrics.BottomMargin;
            _surface.Frame = new CGRect(0, 0, width, height);
            _surface.Layer.BorderColor = NativeTheme.Surface.GetResolvedColor(TraitCollection).CGColor;
            var chevron = SliceUi.Measure(_chevron, width);
            var textWidth = ProfileRowMetrics.TextWidth(width, chevron.Width);
            var caption = SliceUi.Measure(_caption, textWidth);
            var name = SliceUi.Measure(_name, textWidth);
            var top = (height - caption.Height - name.Height) / 2;
            _avatar.Frame = new CGRect(ProfileRowMetrics.ContentInset,
                (height - ProfileRowMetrics.AvatarExtent) / 2, ProfileRowMetrics.AvatarExtent, ProfileRowMetrics.AvatarExtent);
            _caption.Frame = new CGRect(ProfileRowMetrics.TextLeft, top, textWidth, caption.Height);
            _name.Frame = new CGRect(ProfileRowMetrics.TextLeft, top + caption.Height, textWidth, name.Height);
            _chevron.Frame = new CGRect(width - ProfileRowMetrics.ContentInset - chevron.Width,
                (height - chevron.Height) / 2, chevron.Width, chevron.Height);
            _divider.Frame = new CGRect(0, height, width, ProfileRowMetrics.BottomMargin);
        }
    }
}
