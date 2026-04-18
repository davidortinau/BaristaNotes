using MauiReactor;
using MauiReactor.Shapes;
using BaristaNotes.Styles;
using Fonts;

namespace BaristaNotes.Components.FormFields;

/// <summary>
/// A compact chip-style date field. Displays "Today" when the selected date
/// is today, otherwise formats as "MMM d, yyyy" (e.g., "Apr 18, 2026").
/// Tapping the chip opens the native <c>DatePicker</c>.
/// </summary>
/// <example>
/// <code>
/// new TodayChipDateField()
///     .Label("Roast Date")
///     .Date(State.RoastDate)
///     .MaximumDate(DateTime.Today)
///     .OnDateChanged(d =&gt; SetState(s =&gt; s.RoastDate = d))
/// </code>
/// </example>
public class TodayChipDateField : Component
{
    private string? _label;
    private DateTime _date = DateTime.Today;
    private DateTime _maximumDate = DateTime.Today;
    private Action<DateTime>? _onDateChanged;

    public TodayChipDateField Label(string label)
    {
        _label = label;
        return this;
    }

    public TodayChipDateField Date(DateTime date)
    {
        _date = date;
        return this;
    }

    public TodayChipDateField MaximumDate(DateTime maximumDate)
    {
        _maximumDate = maximumDate;
        return this;
    }

    public TodayChipDateField OnDateChanged(Action<DateTime> handler)
    {
        _onDateChanged = handler;
        return this;
    }

    public override VisualNode Render()
    {
        var isLightTheme = Application.Current?.RequestedTheme == AppTheme.Light;
        var backgroundColor = isLightTheme ? AppColors.Light.SurfaceVariant : AppColors.Dark.SurfaceVariant;

        var chipText = _date.Date == DateTime.Today
            ? "Today"
            : _date.ToString("MMM d, yyyy");

        VisualNode chip = Grid(
            rows: "*",
            columns: "*",

            // Visible chip: rounded pill with icon + label
            Border(
                    HStack(spacing: AppSpacing.XS,
                        Label()
                            .Text(MaterialSymbolsFont.Calendar_today)
                            .FontFamily(MaterialSymbolsFont.FontFamily)
                            .FontSize(16)
                            .VCenter(),
                        Label()
                            .Text(chipText)
                            .ThemeKey(ThemeKeys.PrimaryText)
                            .FontSize(14)
                            .VCenter()
                    )
                )
                .Background(backgroundColor)
                .StrokeThickness(0)
                .StrokeShape(RoundRectangle().CornerRadius(18))
                .HeightRequest(36)
                .Padding(AppSpacing.M, 0)
                .HorizontalOptions(LayoutOptions.Start),

            // Invisible DatePicker stacked over chip — receives taps and opens native picker
            DatePicker()
                .Date(_date)
                .MaximumDate(_maximumDate)
                .BackgroundColor(Colors.Transparent)
                .Opacity(0.01)
                .OnDateSelected((s, e) =>
                {
                    var newDate = e.NewDate ?? _date;
                    _onDateChanged?.Invoke(newDate);
                })
        );

        if (string.IsNullOrEmpty(_label))
        {
            return chip;
        }

        return VStack(spacing: AppSpacing.XS,
            Label()
                .Text(_label)
                .ThemeKey(ThemeKeys.Caption)
                .Margin(AppSpacing.M, 0, 0, 0),
            chip
        );
    }
}
