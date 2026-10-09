using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class PeopleViewController : SliceViewController
{
    private readonly PeopleColumn _by;
    private readonly PeopleColumn _for;
    private readonly UILabel _title = new();
    private readonly UILabel _byTitle = new();
    private readonly UILabel _forTitle = new();
    private readonly UILabel _arrow = SliceUi.Label("\ue941", 20);
    private readonly UIView _divider = new() { BackgroundColor = NativeTheme.Outline };
    private UIButton? _close;
    private UIButton? _done;

    public PeopleViewController(SliceNavigationController host, DrinkDraft draft, Action changed) : base(host)
    {
        var images = host.Services.Singleton<IImageProcessingService>();
        _by = new PeopleColumn("by", draft.AvailableUsers, draft.SelectedMaker?.Id, images, Logger, user =>
        {
            draft.SelectedMaker = user;
            changed();
        });
        _for = new PeopleColumn("for", draft.AvailableUsers, draft.SelectedRecipient?.Id, images, Logger, user =>
        {
            draft.SelectedRecipient = user;
            changed();
        });
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        SliceUi.TrackedText(_title, "MADE BY / FOR", 12, 3, NativeTheme.Secondary);
        SliceUi.TrackedText(_byTitle, "BY", 11, 2, NativeTheme.Secondary);
        SliceUi.TrackedText(_forTitle, "FOR", 11, 2, NativeTheme.Secondary);
        _title.TextAlignment = _byTitle.TextAlignment = _forTitle.TextAlignment = UITextAlignment.Center;
        _arrow.Font = NativeTheme.Icons(20);
        _arrow.TextColor = NativeTheme.Primary;
        _close = SliceUi.PickerAction("Close", "picker.close", () => Host.PopViewController(false));
        _done = SliceUi.PickerAction("Done", "picker.done", () => Host.PopViewController(false), true);
        Root.AddSubviews(_title, _byTitle, _forTitle, _arrow, _divider, _by, _for, _close, _done);
    }

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var safe = Root.SafeAreaLayoutGuide.LayoutFrame;
        var titleHeight = SliceUi.Measure(_title, safe.Width - 24).Height;
        _title.Frame = new CGRect(safe.X + 12, safe.Top + 8 + (44 - titleHeight) / 2, safe.Width - 24, titleHeight);
        if (_close != null) _close.Frame = new CGRect(safe.X + 12, safe.Top + 8, _close.SizeThatFits(CGSize.Empty).Width, 44);
        if (_done != null) _done.Frame = new CGRect(safe.Right - 12 - _done.SizeThatFits(CGSize.Empty).Width, safe.Top + 8,
            _done.SizeThatFits(CGSize.Empty).Width, 44);
        var arrow = SliceUi.Measure(_arrow, safe.Width);
        var labelsHeight = (nfloat)Math.Max(arrow.Height, SliceUi.Measure(_byTitle, safe.Width / 2).Height);
        var subTop = safe.Top + 64;
        var subColumn = (safe.Width - 24 - arrow.Width) / 2;
        _byTitle.Frame = new CGRect(safe.X + 12, subTop, subColumn, labelsHeight);
        _arrow.Frame = new CGRect(safe.X + (safe.Width - arrow.Width) / 2, subTop, arrow.Width, labelsHeight);
        _forTitle.Frame = new CGRect(_arrow.Frame.Right, subTop, subColumn, labelsHeight);
        var viewport = SliceUi.PickerViewport(Root, subTop + labelsHeight + 8);
        var column = (viewport.Width - 1) / 2;
        _by.Frame = new CGRect(viewport.X, viewport.Y, column, viewport.Height);
        _divider.Frame = new CGRect(_by.Frame.Right, viewport.Y, 1, viewport.Height);
        _for.Frame = new CGRect(_divider.Frame.Right, viewport.Y, column, viewport.Height);
    }

    private sealed class PeopleColumn : UICollectionView
    {
        private readonly PersonSource _source;
        private readonly Layout _layout;
        private bool _center = true;
        private CGSize _previousSize;

        public PeopleColumn(string side, List<UserProfileDto> users, int? selected,
            IImageProcessingService images, ILogger logger, Action<UserProfileDto> select)
            : base(CGRect.Empty, new UICollectionViewFlowLayout { MinimumLineSpacing = 0, MinimumInteritemSpacing = 0 })
        {
            AccessibilityIdentifier = "people." + side;
            BackgroundColor = NativeTheme.Surface;
            ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never;
            _source = new PersonSource(side, users, selected, images, logger, user =>
            {
                select(user);
                _source!.SelectedId = user.Id;
                ReloadData();
            });
            _layout = new Layout(_source);
            DataSource = _source;
            Delegate = _layout;
            RegisterClassForCell(typeof(PersonCell), "person");
        }

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            if (Bounds.Size != _previousSize)
            {
                _previousSize = Bounds.Size;
                CollectionViewLayout.InvalidateLayout();
                _center = true;
            }
            var padding = Bounds.Height / 2;
            if (ContentInset.Top != padding) ContentInset = new UIEdgeInsets(padding, 0, padding, 0);
            if (!_center || Bounds.Width <= 0 || Bounds.Height <= 0 || _source.Users.Count == 0) return;
            var index = Math.Max(0, _source.Users.FindIndex(user => user.Id == _source.SelectedId));
            var attributes = GetLayoutAttributesForItem(NSIndexPath.FromItemSection(index, 0));
            if (attributes == null) return;
            _center = false;
            SetContentOffset(new CGPoint(0, attributes.Frame.GetMidY() - Bounds.Height / 2), false);
        }

        private sealed class PersonSource(string side, List<UserProfileDto> users, int? selected,
            IImageProcessingService images, ILogger logger, Action<UserProfileDto> select)
            : UICollectionViewDataSource
        {
            public List<UserProfileDto> Users { get; } = users;
            public int? SelectedId { get; set; } = selected;
            public override nint GetItemsCount(UICollectionView collectionView, nint section) => Users.Count;
            public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath path)
            {
                var cell = (PersonCell)collectionView.DequeueReusableCell("person", path);
                var user = Users[(int)path.Item];
                cell.Bind(side, user, user.Id == SelectedId, images, logger, () => select(user));
                return cell;
            }
        }

        private sealed class Layout(PersonSource source) : UICollectionViewDelegateFlowLayout
        {
            private readonly UILabel _measure = new() { Lines = 1 };
            public override CGSize GetSizeForItem(UICollectionView view, UICollectionViewLayout layout, NSIndexPath path)
            {
                var user = source.Users[(int)path.Item];
                var selected = source.SelectedId == user.Id;
                _measure.Text = user.Name;
                _measure.Font = NativeTheme.Font(selected ? 18 : 14, selected);
                return new CGSize(view.Bounds.Width, (selected ? 72 : 52) + 34 + SliceUi.Measure(_measure, view.Bounds.Width - 16).Height);
            }
        }
    }

    [Register("NativePersonCell")]
    private sealed class PersonCell : UICollectionViewCell
    {
        private readonly UIButton _button = new(UIButtonType.Custom);
        private readonly UILabel _name = new() { Lines = 1, LineBreakMode = UILineBreakMode.TailTruncation, TextAlignment = UITextAlignment.Center };
        private readonly ProfileAvatarView _avatar = new(52);
        private Action? _select;
        private nfloat _avatarSize;

        public PersonCell(ObjCRuntime.NativeHandle handle) : base(handle)
        {
            _button.TouchUpInside += Select;
            _name.UserInteractionEnabled = _avatar.UserInteractionEnabled = false;
            _name.IsAccessibilityElement = _avatar.IsAccessibilityElement = false;
            _button.AddSubviews(_name, _avatar);
            ContentView.AddSubview(_button);
            BackgroundColor = NativeTheme.Surface;
        }

        public void Bind(string side, UserProfileDto profile, bool selected,
            IImageProcessingService images, ILogger logger, Action select)
        {
            _select = select;
            _button.AccessibilityIdentifier = $"people.{side}.{profile.Id}";
            _button.AccessibilityLabel = profile.Name;
            _button.AccessibilityTraits = UIAccessibilityTrait.Button | (selected ? UIAccessibilityTrait.Selected : UIAccessibilityTrait.None);
            _name.Text = profile.Name;
            _name.Font = NativeTheme.Font(selected ? 18 : 14, selected);
            _name.TextColor = selected ? NativeTheme.Primary : NativeTheme.TextPrimary;
            _avatarSize = selected ? 72 : 52;
            _avatar.ConfigurePeople(_avatarSize, selected);
            _avatar.SetProfile(profile, images, logger);
            SetNeedsLayout();
        }

        private void Select(object? sender, EventArgs args) => _select?.Invoke();

        public override void LayoutSubviews()
        {
            base.LayoutSubviews();
            _button.Frame = ContentView.Bounds;
            _avatar.Frame = new CGRect((Bounds.Width - _avatarSize) / 2, 14, _avatarSize, _avatarSize);
            _name.Frame = new CGRect(8, _avatar.Frame.Bottom + 6, Bounds.Width - 16,
                SliceUi.Measure(_name, Bounds.Width - 16).Height);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _button.TouchUpInside -= Select;
                _select = null;
                _avatar.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
