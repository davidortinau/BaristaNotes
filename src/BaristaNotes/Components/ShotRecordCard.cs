using MauiReactor;

namespace BaristaNotes.Components;

partial class ShotRecordCard : Component
{
    [Prop]
    ShotRecordDto? _shot;

    public override VisualNode Render()
    {
        if (_shot == null)
            return null!;

        return Border(
            VStack(spacing: 8,
                // Header: Drink type and rating
                Grid(
                    HStack(spacing: 4,
                        Label(MaterialSymbolsFont.Coffee)
                            .FontFamily(MaterialSymbolsFont.FontFamily)
                            .FontSize(18),
                        Label(_shot.DrinkType)
                            .FontSize(18)
                            .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold),
                        RenderBrewMethodBadge()
                    ),

                    RenderRating()
                ),

                // Bean info
                Label(_shot.Bean?.Name ?? "Unknown Bean")
                    .FontSize(16),

                // Recipe details
                Label($"{_shot.DoseIn:F1}g in → {(_shot.ActualOutput ?? _shot.ExpectedOutput):F1}g out ({(_shot.ActualTime ?? _shot.ExpectedTime):F1}s)")
                    .FontSize(14)
                    .TextColor(Colors.Gray),

                // Equipment (if available)
                // RenderEquipment(),

                // User profiles (if available)
                RenderUserProfilesAndTimestamp()
            )
            .Padding(12)
        )
        .ThemeKey(ThemeKeys.CardBorder);
    }

    VisualNode RenderRating()
    {
        var rating = _shot?.Rating ?? 0;
        var icon = AppIcons.GetRatingIcon(rating);

        return Label(icon)
            .FontFamily(MaterialSymbolsFont.FontFamily)
            .FontSize(24)
            .TextColor(AppColors.Light.Primary)
            .HEnd();
    }

    /// <summary>
    /// Shows the brew-method pill when it's not the default Espresso,
    /// so at-a-glance the user can tell a pour-over/moka/etc. record
    /// apart from espresso shots in the feed.
    /// </summary>
    VisualNode RenderBrewMethodBadge()
    {
        if (_shot == null || _shot.BrewMethod == BrewMethod.Espresso)
            return new MauiReactor.ContentView();

        return Border(
            Label(_shot.BrewMethod.DisplayName())
                .FontSize(10)
                .TextColor(AppColors.Light.Primary)
                .Padding(6, 2)
        )
        .StrokeThickness(1)
        .Stroke(AppColors.Light.Primary)
        .BackgroundColor(Colors.Transparent)
        .StrokeShape(RoundRectangle().CornerRadius(8))
        .VerticalOptions(LayoutOptions.Center)
        .Margin(6, 0, 0, 0);
    }

    MauiReactor.Label RenderUserProfilesAndTimestamp()
    {
        var parts = new List<string>();
        if (_shot?.MadeBy != null)
            parts.Add($"By: {_shot.MadeBy.Name}");
        if (_shot?.MadeFor != null)
            parts.Add($"For: {_shot.MadeFor.Name}");

        if (parts.Count == 0)
            return null;

        return Label()
            .Text($"{FormatTimestamp(_shot.Timestamp)} • {string.Join(" • ", parts)}")
            .FontSize(12)
            .TextColor(Colors.Gray);
    }

    VisualNode? RenderEquipment()
    {
        var equipmentNames = new List<string>();
        if (_shot?.Machine != null)
            equipmentNames.Add(_shot.Machine.Name);
        if (_shot?.Grinder != null)
            equipmentNames.Add(_shot.Grinder.Name);
        equipmentNames.AddRange(_shot?.Accessories?.Select(a => a.Name) ?? Enumerable.Empty<string>());

        if (equipmentNames.Count == 0)
            return null;

        return Label($"Equipment: {string.Join(", ", equipmentNames)}")
            .FontSize(12)
            .TextColor(Colors.Gray);
    }

    string FormatTimestamp(DateTimeOffset timestamp)
    {
        var now = DateTimeOffset.Now;
        var diff = now - timestamp;

        if (diff.TotalMinutes < 1)
            return "Just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return timestamp.ToString("h:mm tt");
        if (diff.TotalDays < 7)
            return timestamp.ToString("ddd h:mm tt");

        return timestamp.ToString("MMM d, h:mm tt");
    }
}
