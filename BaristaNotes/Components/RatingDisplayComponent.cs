using MauiReactor;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Styles;
using Fonts;

namespace BaristaNotes.Components;

/// <summary>
/// Component for displaying rating aggregates with star distribution bars.
/// Used in BeanDetailPage to show bean-level ratings.
/// Matches product review UI pattern per requirements.
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
                    .FontSize(14)
                    .TextColor(AppColors.Dark.TextSecondary)
            );
        }

        return VStack(spacing: 16,
            // Average rating display
            RenderAverageRating(),
            
            // Rating distribution bars (5 → 1 stars, descending)
            RenderDistributionBars()
        );
    }

    VisualNode RenderAverageRating()
    {
        return VStack(spacing: 4,
            // Large average number
            HStack(spacing: 8,
                Label(_ratingAggregate!.FormattedAverage)
                    .FontSize(48)
                    .FontAttributes(Microsoft.Maui.Controls.FontAttributes.Bold)
                    .TextColor(AppColors.Dark.Primary),
                
                // Star icon
                Label(MaterialSymbolsFont.Star)
                    .FontFamily(MaterialSymbolsFont.FontFamily)
                    .FontSize(32)
                    .TextColor(AppColors.Dark.Primary)
                    .VCenter()
            ).HCenter(),
            
            // Shot counts
            Label($"{_ratingAggregate.RatedShots} rated shots ({_ratingAggregate.TotalShots} total)")
                .FontSize(14)
                .TextColor(AppColors.Dark.TextSecondary)
                .HCenter()
        );
    }

    VisualNode RenderDistributionBars()
    {
        return VStack(spacing: 8,
            // Display ratings from 5 down to 1 (product review pattern)
            RenderDistributionBar(5),
            RenderDistributionBar(4),
            RenderDistributionBar(3),
            RenderDistributionBar(2),
            RenderDistributionBar(1)
        );
    }

    VisualNode RenderDistributionBar(int rating)
    {
        var count = _ratingAggregate!.GetCountForRating(rating);
        var percentage = _ratingAggregate.GetPercentageForRating(rating);
        
        return HStack(spacing: 8,
            // Rating number with stars
            HStack(spacing: 4,
                Label($"{rating}")
                    .FontSize(14)
                    .TextColor(AppColors.Dark.TextPrimary)
                    .WidthRequest(12),
                
                Label(MaterialSymbolsFont.Star)
                    .FontFamily(MaterialSymbolsFont.FontFamily)
                    .FontSize(14)
                    .TextColor(AppColors.Dark.Primary)
            ).WidthRequest(40),
            
            // Progress bar container
            Grid(
                // Background bar
                BoxView()
                    .Color(AppColors.Dark.Outline)
                    .WidthRequest(200)
                    .HeightRequest(20),
                
                // Filled portion
                BoxView()
                    .BackgroundColor(AppColors.Dark.Primary)
                    .WidthRequest(percentage > 0 ? percentage * 2 : 0) // Max 200px width = 100%
                    .HeightRequest(20)
                    .HStart()
            )
            .WidthRequest(200)
            .HeightRequest(20)
            .HStart(),
            
            // Count label
            Label($"{count}")
                .FontSize(14)
                .TextColor(AppColors.Dark.TextSecondary)
                .WidthRequest(30)
                .HEnd()
        ).VCenter();
    }
}
