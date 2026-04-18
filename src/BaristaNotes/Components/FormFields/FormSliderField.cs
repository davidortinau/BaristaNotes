using MauiReactor.Shapes;
using BaristaNotes.Styles;

namespace BaristaNotes.Components.FormFields;

/// <summary>
/// A reusable form slider field component with a label and styled input container.
/// Uses fluent builder pattern for configuration.
/// </summary>
public class FormSliderField : Component
{
    private string _label = string.Empty;
    private double _minimum = 0;
    private double _maximum = 100;
    private double _value = 0;
    private Action<double>? _onValueChanged;

    public FormSliderField Label(string label)
    {
        _label = label;
        return this;
    }

    public FormSliderField Minimum(double minimum)
    {
        _minimum = minimum;
        return this;
    }

    public FormSliderField Maximum(double maximum)
    {
        _maximum = maximum;
        return this;
    }

    public FormSliderField Value(double value)
    {
        _value = value;
        return this;
    }

    public FormSliderField OnValueChanged(Action<double> handler)
    {
        _onValueChanged = handler;
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
                .Text(_label)
                .ThemeKey(ThemeKeys.Caption)
                .GridRow(0)
                .Margin(AppSpacing.M, 0, 0, AppSpacing.XS),

            // Row 1: BoxView background with rounded ends
            Border()
                .Background(backgroundColor)
                .HeightRequest(50)
                .StrokeThickness(0)
                .StrokeShape(RoundRectangle().CornerRadius(25))
                .GridRow(1),

            // Row 1: Slider on top of BoxView
            Slider()
                .Minimum(_minimum)
                .Maximum(_maximum)
                .Value(_value)
                .GridRow(1)
                .Margin(AppSpacing.M, 0)
                .VCenter()
                .OnValueChanged(val => _onValueChanged?.Invoke(val))
        );
    }
}
