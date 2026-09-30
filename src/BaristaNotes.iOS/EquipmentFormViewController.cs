using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using CoreGraphics;
using Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class EquipmentFormViewController : SliceViewController
{
    private static readonly (EquipmentType Type, string Caption)[] Types =
    [
        (EquipmentType.Machine, "MACHINE"), (EquipmentType.Grinder, "GRINDER"),
        (EquipmentType.Tamper, "TAMPER"), (EquipmentType.PuckScreen, "PUCK SCREEN"),
        (EquipmentType.Other, "OTHER")
    ];
    private readonly EquipmentDraft _draft;
    private readonly EquipmentHeader _header = new("equipment.form.header", adaptiveTitle: true);
    private readonly UIScrollView _scroll = new() { ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never };
    private readonly UIView _content = new() { BackgroundColor = NativeTheme.Outline };
    private readonly UIView _nameTile = new() { BackgroundColor = NativeTheme.Surface };
    private readonly UIView _typeTile = new() { BackgroundColor = NativeTheme.Surface };
    private readonly UIView _notesTile = new() { BackgroundColor = NativeTheme.Surface };
    private readonly UIView _errorTile = new() { BackgroundColor = NativeTheme.Error, Hidden = true };
    private readonly UIView _spacer = new() { BackgroundColor = NativeTheme.Surface };
    private readonly UILabel _nameCaption = new();
    private readonly UILabel _typeCaption = new();
    private readonly UILabel _notesCaption = new();
    private readonly UILabel _notesPlaceholder = new() { Lines = 0, UserInteractionEnabled = false };
    private readonly UILabel _errorCaption = new();
    private readonly UILabel _errorMessage = new() { Lines = 0 };
    private readonly UITextField _name = new();
    private readonly UITextView _notes = new();
    private readonly List<UIButton> _typeButtons = [];
    private readonly List<EquipmentActionButton> _actions = [];
    private readonly UIActivityIndicatorView _spinner = new(UIActivityIndicatorViewStyle.Medium);
    private NSObject? _keyboardObserver;
    private nfloat _keyboardHeight;
    private bool _saving;
    private bool _loaded;
    private bool _visible;
    private bool _archived;
    private int _pageGeneration;

    public EquipmentFormViewController(SliceNavigationController host, int? id = null, EquipmentType? preset = null) : base(host)
        => _draft = new EquipmentDraft { EquipmentId = id, SelectedType = preset ?? EquipmentType.Machine };

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Root.BackgroundColor = NativeTheme.Outline;
        _scroll.AccessibilityIdentifier = "equipment.form.scroll";
        _scroll.BackgroundColor = NativeTheme.Surface;
        _scroll.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;
        _scroll.AddSubview(_content);
        SliceUi.HeaderLabel(_nameCaption, "NAME");
        _name.Font = NativeTheme.Font(22, true);
        _name.TextColor = NativeTheme.TextPrimary;
        _name.BorderStyle = UITextBorderStyle.None;
        _name.AccessibilityIdentifier = "equipment.name";
        _name.AccessibilityLabel = "Equipment name";
        _name.AttributedPlaceholder = new NSAttributedString("Equipment name", new UIStringAttributes
        {
            Font = _name.Font, ForegroundColor = NativeTheme.Secondary.ColorWithAlpha(0.5f)
        });
        _name.LeftView = new UIView(new CGRect(0, 0, 4, 0));
        _name.LeftViewMode = UITextFieldViewMode.Always;
        _name.ReturnKeyType = UIReturnKeyType.Done;
        _name.ShouldReturn = static field => { field.ResignFirstResponder(); return true; };
        var nameChanged = WeakUiCallback.Create(this, static owner =>
        {
            owner._draft.Name = owner._name.Text ?? "";
            owner.UpdateHeader();
        });
        _name.EditingChanged += (_, _) => nameChanged();
        _nameTile.AddSubviews(_nameCaption, _name);
        foreach (var (type, caption) in Types)
        {
            var button = new EquipmentTypeButton(caption, $"equipment.type.{type}",
                WeakUiCallback.Create(this, type, static (owner, value) =>
                {
                    if (owner.CanEdit) { owner._draft.SelectedType = value; owner.RefreshTypes(); }
                }));
            _typeButtons.Add(button);
            _typeTile.AddSubview(button);
        }
        _typeTile.AddSubview(_typeCaption);
        _notes.AccessibilityIdentifier = "equipment.notes";
        _notes.AccessibilityLabel = "Additional details";
        _notes.BackgroundColor = UIColor.Clear;
        _notes.TextColor = NativeTheme.TextPrimary;
        _notesPlaceholder.Text = "Additional details";
        _notesPlaceholder.TextColor = NativeTheme.Secondary.ColorWithAlpha(0.5f);
        _notesPlaceholder.IsAccessibilityElement = false;
        var notesChanged = WeakUiCallback.Create(this, static owner =>
        {
            owner._draft.Notes = owner._notes.Text ?? "";
            owner._notesPlaceholder.Hidden = owner._draft.Notes.Length > 0;
        });
        _notes.Changed += (_, _) => notesChanged();
        _notesTile.AddSubviews(_notesCaption, _notes, _notesPlaceholder);
        _errorTile.AccessibilityIdentifier = "equipment.error";
        _errorTile.AddSubviews(_errorCaption, _errorMessage);
        _content.AddSubviews(_nameTile, _typeTile, _notesTile, _errorTile, _spacer);
        _actions.Add(new EquipmentActionButton("CANCEL", "equipment.cancel",
            WeakUiCallback.Create(this, static owner =>
            {
                if (owner.Host.TopViewController == owner && owner.Host.PresentedViewController == null)
                    owner.Host.RequestBack();
            })));
        if (_draft.IsEditing)
            _actions.Add(new EquipmentActionButton("DELETE", "equipment.archive",
                WeakUiCallback.Create(this, static owner => owner.ConfirmArchive()), danger: true));
        _actions.Add(new EquipmentActionButton(_draft.IsEditing ? "SAVE" : "ADD", "equipment.save",
            WeakUiCallback.Create(this, static owner => _ = owner.SaveAsync()), inverted: true));
        Root.AddSubviews(_header, _scroll, _spinner);
        Root.AddSubviews(_actions.ToArray());
        if (OperatingSystem.IsIOSVersionAtLeast(17))
            RegisterForTraitChanges<UITraitPreferredContentSizeCategory>(static (environment, _) =>
                ((EquipmentFormViewController)environment).UpdateFonts());
        UpdateFonts();
        UpdateHeader();
        RefreshTypes();
    }

    private bool CanEdit => _loaded && !_saving && Host.TopViewController == this && Host.PresentedViewController == null;
    private bool IsCurrent(int generation) =>
        _visible && generation == _pageGeneration && Host.TopViewController == this;

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        _visible = true;
        _pageGeneration++;
        var weak = new WeakReference<EquipmentFormViewController>(this);
        _keyboardObserver?.Dispose();
        _keyboardObserver = UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
        {
            if (!weak.TryGetTarget(out var owner)) return;
            var local = owner.Root.ConvertRectFromView(args.FrameEnd, null);
            owner._keyboardHeight = (nfloat)Math.Max(0, owner.Root.Bounds.Height - local.Y);
            owner.Root.SetNeedsLayout();
        });
        if (!_loaded) _ = LoadAsync();
        UpdateFonts();
    }

    public override void ViewDidDisappear(bool animated)
    {
        _visible = false;
        _pageGeneration++;
        _spinner.StopAnimating();
        _keyboardObserver?.Dispose();
        _keyboardObserver = null;
        base.ViewDidDisappear(animated);
    }

    private async Task LoadAsync()
    {
        if (!_draft.IsEditing) { _loaded = true; return; }
        var generation = _pageGeneration;
        var id = _draft.EquipmentId!.Value;
        _spinner.StartAnimating();
        _scroll.Hidden = true;
        try
        {
            var item = await Services.RunAsync(provider =>
                provider.GetRequiredService<IEquipmentService>().GetEquipmentByIdAsync(id));
            if (!IsCurrent(generation)) return;
            if (item == null) { SetError("Equipment not found"); return; }
            _draft.ApplyLoadedData(item);
            _name.Text = _draft.Name;
            _notes.Text = _draft.Notes;
            _notesPlaceholder.Hidden = _draft.Notes.Length > 0;
            _loaded = true;
            UpdateHeader();
            RefreshTypes();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Equipment detail read failed for {EquipmentId}", _draft.EquipmentId);
            if (IsCurrent(generation)) SetError($"Failed to load equipment: {error.Message}");
        }
        finally
        {
            if (IsCurrent(generation)) { _spinner.StopAnimating(); _scroll.Hidden = false; }
        }
    }

    private async Task SaveAsync()
    {
        if (!CanEdit || Host.FeedbackHost.IsShowing) return;
        // Source constructs its DTO at the tap, before awaiting I/O. Never let
        // later edits race the background workflow's reads of the live form.
        var saving = new EquipmentDraft
        {
            EquipmentId = _draft.EquipmentId, Name = _draft.Name,
            SelectedType = _draft.SelectedType, Notes = _draft.Notes
        };
        var generation = _pageGeneration;
        if (saving.ValidationError is { } validation) { SetError(validation); return; }
        SetError(null);
        _saving = true;
        _actions[^1].SetText("SAVING…");
        try
        {
            await Services.RunAsync(provider => provider.GetRequiredService<EquipmentWorkflow>().SaveAsync(saving));
            if (!IsCurrent(generation)) return;
            Root.EndEditing(true);
            await Host.FeedbackHost.ShowAndWaitAsync($"'{saving.Name}' {(saving.IsEditing ? "updated" : "created")}");
            if (IsCurrent(generation)) Host.RequestBack();
        }
        catch (Exception error)
        {
            Logger.LogError(error, "Equipment save failed for {EquipmentId}", saving.EquipmentId);
            if (IsCurrent(generation)) SetError($"Failed to save: {error.Message}");
        }
        finally
        {
            _saving = false;
            if (IsCurrent(generation)) _actions[^1].SetText(_draft.IsEditing ? "SAVE" : "ADD");
        }
    }

    private void ConfirmArchive()
    {
        if (!CanEdit || !_draft.IsEditing || Host.FeedbackHost.IsShowing) return;
        Root.EndEditing(true);
        var weak = new WeakReference<EquipmentFormViewController>(this);
        Host.PresentViewController(new EquipmentConfirmationViewController(Host, "Archive Equipment?",
            $"Are you sure you want to archive '{_draft.Name}'? This action cannot be undone.", "Archive",
            async () =>
            {
                if (!weak.TryGetTarget(out var owner)) throw new InvalidOperationException("Equipment form is no longer available.");
                if (!owner._archived)
                {
                    await owner.Services.RunAsync(async provider =>
                    {
                        await provider.GetRequiredService<EquipmentWorkflow>().ArchiveAsync(owner._draft.EquipmentId!.Value);
                        return true;
                    });
                    owner._archived = true;
                }
                await owner.Host.FeedbackHost.ShowAndWaitAsync($"'{owner._draft.Name}' archived");
            }, () =>
            {
                if (weak.TryGetTarget(out var owner) && owner.Host.TopViewController == owner)
                    owner.Host.RequestBack();
            }), false, null);
    }

    private void SetError(string? text)
    {
        _errorMessage.Text = text;
        _errorTile.Hidden = string.IsNullOrEmpty(text);
        Root.SetNeedsLayout();
    }

    private void UpdateHeader()
    {
        _header.Update(_draft.IsEditing ? "EDIT EQUIPMENT" : "NEW EQUIPMENT",
            _draft.IsEditing ? string.IsNullOrEmpty(_draft.Name) ? "Loading…" : _draft.Name : "Add equipment");
        Root.SetNeedsLayout();
    }

    private void RefreshTypes()
    {
        for (var i = 0; i < Types.Length; i++)
        {
            var selected = Types[i].Type == _draft.SelectedType;
            var foreground = selected ? NativeTheme.Surface : NativeTheme.TextPrimary;
            _typeButtons[i].BackgroundColor = selected ? NativeTheme.TextPrimary : EquipmentColors.SurfaceVariant;
            _typeButtons[i].TitleLabel.Font = SourceScaledText.Font(11, true, TraitCollection);
            using var title = new NSAttributedString(Types[i].Caption, new UIStringAttributes
            {
                Font = _typeButtons[i].TitleLabel.Font,
                ForegroundColor = foreground, KerningAdjustment = 1.5f
            });
            _typeButtons[i].SetAttributedTitle(title, UIControlState.Normal);
            _typeButtons[i].AccessibilityTraits = UIAccessibilityTrait.Button | (selected ? UIAccessibilityTrait.Selected : UIAccessibilityTrait.None);
        }
    }

    private void UpdateFonts()
    {
        _header.UpdateFonts(TraitCollection);
        SourceScaledText.Tracked(_nameCaption, "NAME", 10, 2, NativeTheme.Secondary, TraitCollection);
        _name.Font = SourceScaledText.Font(22, true, TraitCollection);
        using var placeholder = new NSAttributedString("Equipment name", new UIStringAttributes
        {
            Font = _name.Font, ForegroundColor = NativeTheme.Secondary.ColorWithAlpha(0.5f)
        });
        _name.AttributedPlaceholder = placeholder;
        SourceScaledText.Tracked(_typeCaption, "TYPE", 10, 2, NativeTheme.Secondary, TraitCollection);
        SourceScaledText.Tracked(_notesCaption, "NOTES", 10, 2, NativeTheme.Secondary, TraitCollection);
        _notes.Font = _notesPlaceholder.Font = SourceScaledText.Font(16, false, TraitCollection);
        SourceScaledText.Tracked(_errorCaption, "ERROR", 10, 2, NativeTheme.Surface.ColorWithAlpha(0.8f), TraitCollection);
        _errorMessage.Font = SourceScaledText.Font(16, true, TraitCollection);
        _errorMessage.TextColor = NativeTheme.Surface;
        foreach (var action in _actions) action.UpdateFonts(TraitCollection);
        RefreshTypes();
        Root.SetNeedsLayout();
    }

#pragma warning disable CS0672, CA1422
    public override void TraitCollectionDidChange(UITraitCollection? previousTraitCollection)
    {
        base.TraitCollectionDidChange(previousTraitCollection);
        if (!OperatingSystem.IsIOSVersionAtLeast(17) && IsViewLoaded) UpdateFonts();
    }
#pragma warning restore CS0672, CA1422

    public override void ViewDidLayoutSubviews()
    {
        base.ViewDidLayoutSubviews();
        var width = Root.Bounds.Width;
        var height = Root.Bounds.Height - _keyboardHeight;
        _header.TopInset = Root.SafeAreaInsets.Top;
        var headerHeight = _header.SizeThatFits(new CGSize(width, nfloat.MaxValue)).Height;
        var actionHeight = _actions.Select(action => action.SizeThatFits(CGSize.Empty).Height).DefaultIfEmpty(72).Max();
        _header.Frame = new CGRect(0, 0, width, headerHeight);
        _scroll.Frame = new CGRect(0, headerHeight + 1, width, (nfloat)Math.Max(1, height - headerHeight - actionHeight - 2));
        _spinner.Center = new CGPoint(width / 2, (headerHeight + height - actionHeight) / 2);
        var nameCaption = SliceUi.Measure(_nameCaption, width - 32).Height;
        var entryHeight = (nfloat)Math.Max(44, _name.SizeThatFits(new CGSize(width - 32, nfloat.MaxValue)).Height);
        var nameHeight = (nfloat)Math.Max(100, 32 + nameCaption + entryHeight);
        _nameTile.Frame = new CGRect(0, 0, width, nameHeight);
        var nameTop = (nameHeight - nameCaption - entryHeight) / 2;
        _nameCaption.Frame = new CGRect(16, nameTop, width - 32, nameCaption);
        _name.Frame = new CGRect(16, nameTop + nameCaption, width - 32, entryHeight);
        var typeCaption = SliceUi.Measure(_typeCaption, width - 32).Height;
        var chipWidth = (width - 48) / 3;
        var firstChipRow = _typeButtons.Take(3).Max(button => button.SizeThatFits(new CGSize(chipWidth, nfloat.MaxValue)).Height);
        var secondChipRow = _typeButtons.Skip(3).Max(button => button.SizeThatFits(new CGSize(chipWidth, nfloat.MaxValue)).Height);
        var typeHeight = 30 + typeCaption + 10 + firstChipRow + secondChipRow + 8;
        var y = nameHeight + 1;
        _typeTile.Frame = new CGRect(0, y, width, typeHeight);
        _typeCaption.Frame = new CGRect(16, 14, width - 32, typeCaption);
        for (var i = 0; i < _typeButtons.Count; i++)
            _typeButtons[i].Frame = new CGRect(16 + i % 3 * (chipWidth + 8),
                24 + typeCaption + (i < 3 ? 0 : firstChipRow + 8), chipWidth, i < 3 ? firstChipRow : secondChipRow);
        y += typeHeight + 1;
        var notesCaption = SliceUi.Measure(_notesCaption, width - 32).Height;
        var notesHeight = (nfloat)Math.Max(160, notesCaption + 148);
        _notesTile.Frame = new CGRect(0, y, width, notesHeight);
        _notesCaption.Frame = new CGRect(16, 14, width - 32, notesCaption);
        _notes.Frame = new CGRect(16, 14 + notesCaption, width - 32, 120);
        var placeholderLeft = 16 + _notes.TextContainerInset.Left + _notes.TextContainer.LineFragmentPadding;
        _notesPlaceholder.Frame = new CGRect(placeholderLeft, 14 + notesCaption + _notes.TextContainerInset.Top,
            width - placeholderLeft - 16, SliceUi.Measure(_notesPlaceholder, width - placeholderLeft - 16).Height);
        y += notesHeight + 1;
        if (!_errorTile.Hidden)
        {
            var caption = SliceUi.Measure(_errorCaption, width - 32).Height;
            var message = SliceUi.Measure(_errorMessage, width - 32).Height;
            var errorHeight = (nfloat)Math.Max(60, 24 + caption + message);
            _errorTile.Frame = new CGRect(0, y, width, errorHeight);
            _errorCaption.Frame = new CGRect(16, 12, width - 32, caption);
            _errorMessage.Frame = new CGRect(16, 12 + caption, width - 32, message);
            y += errorHeight;
        }
        y += 1;
        _spacer.Frame = new CGRect(0, y, width, (nfloat)Math.Max(24, _scroll.Bounds.Height - y));
        _content.Frame = new CGRect(0, 0, width, _spacer.Frame.Bottom);
        _scroll.ContentSize = _content.Bounds.Size;
        var actionWidth = (width - _actions.Count + 1) / _actions.Count;
        for (var i = 0; i < _actions.Count; i++)
            _actions[i].Frame = new CGRect(i * (actionWidth + 1), height - actionHeight, actionWidth, actionHeight);
    }
}

internal static class EquipmentColors
{
    public static UIColor SurfaceVariant { get; } = UIColor.FromDynamicProvider(traits =>
        traits.UserInterfaceStyle == UIUserInterfaceStyle.Dark
            ? UIColor.FromRGB(0x7D, 0x5A, 0x45) : UIColor.FromRGB(0xEC, 0xDA, 0xC4));
    public static UIColor DarkElevated { get; } = UIColor.FromRGB(0xB3, 0xA2, 0x91);
}
