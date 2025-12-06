using MauiReactor;
using BaristaNotes.Styles;

namespace BaristaNotes.Components.FormFields;

/// <summary>
/// A reusable form editor field component with a label and styled multi-line input container.
/// Uses fluent builder pattern for configuration.
/// </summary>
public class FormEditorField : Component
{
    private string _label = string.Empty;
    private string _text = string.Empty;
    private string _placeholder = string.Empty;
    private Action<string>? _onTextChanged;
    private double _heightRequest = 100;

    public FormEditorField Label(string label)
    {
        _label = label;
        return this;
    }

    public FormEditorField Text(string text)
    {
        _text = text;
        return this;
    }

    public FormEditorField Placeholder(string placeholder)
    {
        _placeholder = placeholder;
        return this;
    }

    public FormEditorField OnTextChanged(Action<string> handler)
    {
        _onTextChanged = handler;
        return this;
    }

    public FormEditorField HeightRequest(double height)
    {
        _heightRequest = height;
        return this;
    }

    public override VisualNode Render()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var backgroundColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;

        return Grid(
            rows: $"Auto, {_heightRequest}",
            columns: "*",

            // Row 0: Label
            Label()
                .ThemeKey(ThemeKeys.Caption)
                .GridRow(0)
                .Text(_label)
                .Margin(AppSpacing.M, 0, 0, AppSpacing.XS),

            // Row 1: BoxView background with rounded corners
            BoxView()
                .BackgroundColor(backgroundColor)
                .HeightRequest(_heightRequest)
                .CornerRadius(16)
                .GridRow(1),

            // Row 1: Editor on top of BoxView
            Editor()
                .Text(_text)
                .Placeholder(_placeholder)
                .BackgroundColor(Colors.Transparent)
                .GridRow(1)
                .Margin(AppSpacing.M, AppSpacing.S)
                .OnTextChanged(text => _onTextChanged?.Invoke(text))
        );
    }
}
