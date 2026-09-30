using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging;
using UIKit;

namespace BaristaNotes.Native.iOS;

internal sealed class RangeEditorViewController : RangePageViewController
{
    private readonly RangeEditorDraft _draft;
    private readonly IDrinkValueRangeService _ranges;
    private readonly UILabel _error = SliceUi.Label("", 14, true);
    private RangeFieldTile? _minimum;
    private RangeFieldTile? _maximum;
    private RangeTextTile? _errorTile;
    private UIButton? _save;
    private bool _saving;
    private bool _promptOpen;
    private bool _allowNavigation;
    private bool _previousBackGesture;
    private UIScreenEdgePanGestureRecognizer? _backGesture;

    public RangeEditorViewController(SliceNavigationController host, DrinkValueMetric metric, BrewMethod method)
        : base(host, $"CUSTOM {DrinkValueRangeFormatting.MetricTitle(metric).ToUpperInvariant()}",
            method.DisplayName(), "range.editor.scroll")
    {
        _ranges = Services.Singleton<IDrinkValueRangeService>();
        _draft = new RangeEditorDraft(metric, method, _ranges.GetSettings());
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        var unit = new UILabel();
        SliceUi.TrackedText(unit, $"ENTER VALUES IN {_draft.EditorUnit.Label.ToUpperInvariant()}", 12, 2, NativeTheme.Secondary);
        AddTile(new RangeTextTile(8, 14, 0, unit,
            SliceUi.Label($"Recommended: {DrinkValueRangeFormatting.FormatRange(_draft.Metric, _draft.Definition.AutoRange)}", 14),
            SliceUi.Label($"Allowed: {DrinkValueRangeFormatting.FormatRange(_draft.Metric, _draft.Definition.HardRange)}", 14, secondary: true)));
        _minimum = new RangeFieldTile("MINIMUM", _draft.EditorUnit.Label, "Minimum value", "RangeMinimum", value =>
        {
            _draft.MinimumText = value;
            ShowError(_draft.ValidationError);
        });
        _maximum = new RangeFieldTile("MAXIMUM", _draft.EditorUnit.Label, "Maximum value", "RangeMaximum", value =>
        {
            _draft.MaximumText = value;
            ShowError(_draft.ValidationError);
        });
        _minimum.Entry.Text = _draft.MinimumText;
        _maximum.Entry.Text = _draft.MaximumText;
        AddTile(_minimum);
        AddTile(_maximum);
        _error.TextColor = NativeTheme.Surface;
        _errorTile = new RangeTextTile(0, 12, 56, _error)
        {
            BackgroundColor = NativeTheme.Error, AccessibilityIdentifier = "range.editor.error", Hidden = true
        };
        AddTile(_errorTile);
        var recommended = new RangeLinkTile("UseRecommendedRange", UseRecommended);
        var range = DrinkValueRangeFormatting.FormatRange(_draft.Metric, _draft.Definition.AutoRange);
        recommended.Update("USE RECOMMENDED RANGE", range, "", true, false,
            $"Use recommended range. {range}.", "\uf053");
        AddTile(recommended);
        AddTile(new RangeTextTile(0, 8, 16) { BackgroundColor = NativeTheme.Outline });
        AddAction(SliceUi.FormAction("CANCEL", "RangeEditorCancel", RequestClose));
        _save = SliceUi.FormAction("SAVE", "RangeEditorSave", Save, true);
        AddAction(_save);
        _backGesture = new UIScreenEdgePanGestureRecognizer(() =>
        {
            if (_backGesture?.State == UIGestureRecognizerState.Ended &&
                _backGesture.TranslationInView(Root).X > 40) RequestClose();
        }) { Edges = UIRectEdge.Left };
        Root.AddGestureRecognizer(_backGesture);
    }

    public override void ViewWillAppear(bool animated)
    {
        base.ViewWillAppear(animated);
        if (Host.InteractivePopGestureRecognizer is { } gesture)
        {
            _previousBackGesture = gesture.Enabled;
            // A completed edge swipe must ask before UIKit pops this dirty draft.
            gesture.Enabled = false;
        }
    }

    public override void ViewDidDisappear(bool animated)
    {
        if (Host.InteractivePopGestureRecognizer is { } gesture) gesture.Enabled = _previousBackGesture;
        base.ViewDidDisappear(animated);
    }

    internal bool CanNavigate(Action continueNavigation)
    {
        if (_saving || _promptOpen) return false;
        if (_allowNavigation || !_draft.IsDirty) return true;
        Confirm("Discard changes?", "Your range changes have not been saved.", "Discard", "Keep Editing", () =>
        {
            _allowNavigation = true;
            continueNavigation();
        });
        return false;
    }

    private void RequestClose() => Host.RequestBack();

    private void Save()
    {
        if (_saving || _promptOpen) return;
        if (!_draft.TryGetRange(out var range, out var error)) { ShowError(error); return; }
        _saving = true;
        if (_save != null) _save.Enabled = false;
        try
        {
            _ranges.SaveOverride(_draft.Metric, _draft.Method, range.Minimum, range.Maximum);
            _allowNavigation = true;
            _saving = false;
            Host.RequestBack();
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to save {Metric} range for {Method}", _draft.Metric, _draft.Method);
            ShowError(exception.Message);
        }
        finally { _saving = false; if (_save != null) _save.Enabled = true; }
    }

    private void UseRecommended()
    {
        if (_saving || _promptOpen) return;
        if (!_draft.HasOverride)
        {
            _draft.UseRecommended();
            if (_minimum != null) _minimum.Entry.Text = _draft.MinimumText;
            if (_maximum != null) _maximum.Entry.Text = _draft.MaximumText;
            ShowError(null);
            return;
        }
        var metric = DrinkValueRangeFormatting.MetricTitle(_draft.Metric).ToLowerInvariant();
        Confirm("Use recommended range?", $"Remove the custom {metric} range for {_draft.Method.DisplayName()}?",
            "Use Recommended", "Cancel", () =>
            {
                try
                {
                    _ranges.RemoveOverride(_draft.Metric, _draft.Method);
                    _allowNavigation = true;
                    Host.RequestBack();
                }
                catch (Exception exception)
                {
                    Logger.LogError(exception, "Failed to remove {Metric} range for {Method}", _draft.Metric, _draft.Method);
                    ShowError(exception.Message);
                }
            });
    }

    private void Confirm(string title, string message, string accept, string cancel, Action confirmed)
    {
        if (_promptOpen) return;
        _promptOpen = true;
        var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create(cancel, UIAlertActionStyle.Cancel, _ => _promptOpen = false));
        alert.AddAction(UIAlertAction.Create(accept, UIAlertActionStyle.Default, _ =>
        {
            _promptOpen = false;
            confirmed();
        }));
        PresentViewController(alert, true, null);
    }

    private void ShowError(string? message)
    {
        _error.Text = message;
        if (_errorTile != null) _errorTile.Hidden = message == null;
        Root.SetNeedsLayout();
    }
}
