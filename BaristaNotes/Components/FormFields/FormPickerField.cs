using MauiReactor;
using BaristaNotes.Styles;

namespace BaristaNotes.Components.FormFields;

/// <summary>
/// A reusable form picker field component with a label and styled input container.
/// Uses fluent builder pattern for configuration.
/// </summary>
public class FormPickerField : Component
{
    private string _label = string.Empty;
    private string _title = string.Empty;
    private List<object> _itemsSource = new();
    private int _selectedIndex = -1;
    private Action<int>? _onSelectedIndexChanged;

    public FormPickerField Label(string label)
    {
        _label = label;
        return this;
    }

    public FormPickerField Title(string title)
    {
        _title = title;
        return this;
    }

    public FormPickerField ItemsSource(IEnumerable<string> itemsSource)
    {
        _itemsSource = itemsSource.Cast<object>().ToList();
        return this;
    }

    public FormPickerField ItemsSource(List<object> itemsSource)
    {
        _itemsSource = itemsSource;
        return this;
    }

    public FormPickerField SelectedIndex(int selectedIndex)
    {
        _selectedIndex = selectedIndex;
        return this;
    }

    public FormPickerField OnSelectedIndexChanged(Action<int> handler)
    {
        _onSelectedIndexChanged = handler;
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
            BoxView()
                .BackgroundColor(backgroundColor)
                .HeightRequest(50)
                .CornerRadius(25)
                .GridRow(1),

            // Row 1: Picker on top of BoxView
            Picker()
                .Title(_title)
                .ItemsSource(_itemsSource)
                .SelectedIndex(_selectedIndex)
                .BackgroundColor(Colors.Transparent)
                .GridRow(1)
                .Margin(AppSpacing.M, 0)
                .VCenter()
                .OnSelectedIndexChanged(idx => _onSelectedIndexChanged?.Invoke(idx))
        );
    }
}
