using MauiReactor;
using BaristaNotes.Styles;

namespace BaristaNotes.Components.FormFields;

/// <summary>
/// A reusable form entry field component with a label and styled input container.
/// Uses fluent builder pattern for configuration.
/// </summary>
public class FormEntryField : Component
{
    private string _label = string.Empty;
    private string _text = string.Empty;
    private string _placeholder = string.Empty;
    private Action<string>? _onTextChanged;
    private Microsoft.Maui.Keyboard? _keyboard;

    public FormEntryField Label(string label)
    {
        _label = label;
        return this;
    }

    public FormEntryField Text(string text)
    {
        _text = text;
        return this;
    }

    public FormEntryField Placeholder(string placeholder)
    {
        _placeholder = placeholder;
        return this;
    }

    public FormEntryField OnTextChanged(Action<string> handler)
    {
        _onTextChanged = handler;
        return this;
    }

    public FormEntryField Keyboard(Microsoft.Maui.Keyboard keyboard)
    {
        _keyboard = keyboard;
        return this;
    }

    public override VisualNode Render()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var backgroundColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;

        return Grid(
            rows: "Auto, 50",
            columns: "*",

            // Row 0: Label
            Label()
                .ThemeKey(ThemeKeys.Caption)
                .GridRow(0)
                .Text(_label)
                .Margin(AppSpacing.M, 0, 0, AppSpacing.XS),

            // Row 1: BoxView background with rounded ends
            BoxView()
                .BackgroundColor(backgroundColor)
                .HeightRequest(50)
                .CornerRadius(25)
                .GridRow(1),

            // Row 1: Entry on top of BoxView
            Entry()
                .Text(_text)
                .Placeholder(_placeholder)
                .Keyboard(_keyboard ?? Microsoft.Maui.Keyboard.Default)
                .BackgroundColor(Colors.Transparent)
                .GridRow(1)
                .Margin(AppSpacing.M, 0)
                .VCenter()
                .OnTextChanged(text => _onTextChanged?.Invoke(text))
        );
    }
}
