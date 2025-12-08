using MauiReactor;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Styles;
using Fonts;

namespace BaristaNotes.Components;

/// <summary>
/// Component for displaying rating aggregates with sentiment icon distribution bars.
/// Uses 0-4 rating scale (5 levels: Terrible, Bad, Average, Good, Excellent).
/// Matches the ShotLoggingPage rating input pattern per requirements.
/// </summary>
partial class RatingDisplayComponent : Component
{
    [Prop]
    RatingAggregateDto? _ratingAggregate;

    public override VisualNode Render()
    {
        if (_ratingAggregate == null || !_ratingAggregate.HasRatings)
        {
            return VStack(spacing: 8,
                Label("No ratings yet")
                    .ThemeKey(ThemeKeys.SecondaryText)
            );
        }

        return VStack(spacing: 16,
            // Average rating display
            RenderAverageRating(),

            // Rating distribution bars (4 → 0, descending)
            RenderDistributionBars()
        );
    }

    VisualNode RenderAverageRating()
    {
        return VStack(spacing: 4,
            // Large average number
            HStack(spacing: 8,
                Label(_ratingAggregate!.FormattedAverage)
                    .ThemeKey(ThemeKeys.RatingAverage)
            ).HCenter(),

            // Shot count
            Label($"{_ratingAggregate.TotalShots} shots")
                .ThemeKey(ThemeKeys.SecondaryText)
                .HCenter()
        );
    }

    VisualNode RenderDistributionBars()
    {
        return VStack(spacing: 8,
            // Display ratings from 4 (Excellent) down to 0 (Terrible)
            RenderDistributionBar(4),
            RenderDistributionBar(3),
            RenderDistributionBar(2),
            RenderDistributionBar(1),
            RenderDistributionBar(0)
        );
    }

    VisualNode RenderDistributionBar(int rating)
    {
        var count = _ratingAggregate!.GetCountForRating(rating);
        var percentage = _ratingAggregate.GetPercentageForRating(rating);

        return HStack(spacing: 8,
            // Rating icon (left side)
            Label(AppIcons.GetRatingIcon(rating))
                .FontFamily(MaterialSymbolsFont.FontFamily)
                .FontSize(20)
                .ThemeKey(ThemeKeys.SecondaryText)
                .WidthRequest(30)
                .HCenter(),

            // Progress bar container
            Grid(
                // Background bar
                BoxView()
                    .ThemeKey(ThemeKeys.RatingBarBackground)
                    .WidthRequest(200)
                    .HeightRequest(20),

                // Filled portion
                BoxView()
                    .ThemeKey(ThemeKeys.RatingBarFilled)
                    .WidthRequest(percentage > 0 ? percentage * 2 : 0) // Max 200px width = 100%
                    .HeightRequest(20)
                    .HStart()
            )
            .WidthRequest(200)
            .HeightRequest(20)
            .HStart(),

            // Count label (right side)
            Label($"{count}")
                .ThemeKey(ThemeKeys.SecondaryText)
                .WidthRequest(30)
                .HEnd()
        ).VCenter();
    }
}
