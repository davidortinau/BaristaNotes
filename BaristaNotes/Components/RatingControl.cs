using MauiReactor;
using BaristaNotes.Theme;

namespace BaristaNotes.Components;

public class RatingControl : Component<RatingControlState>
{
    private int _rating;
    private Action<int>? _onRatingChanged;
    private bool _isReadOnly;

    public RatingControl Rating(int rating)
    {
        _rating = rating;
        return this;
    }

    public RatingControl OnRatingChanged(Action<int> handler)
    {
        _onRatingChanged = handler;
        return this;
    }

    public RatingControl IsReadOnly(bool isReadOnly = true)
    {
        _isReadOnly = isReadOnly;
        return this;
    }

    public override VisualNode Render()
    {
        return HStack(spacing: AppSpacing.S,
            Enumerable.Range(1, 5).Select(index =>
                RenderStar(index)
            ).ToArray()
        )
        .HeightRequest(40)
        .HCenter();
    }

    private VisualNode RenderStar(int index)
    {
        var isFilled = index <= _rating;
        
        if (_isReadOnly)
        {
            // Read-only star (no interaction)
            return Label(isFilled ? "★" : "☆")
                .FontSize(32)
                .TextColor(isFilled ? AppColors.Caramel : AppColors.SecondaryText)
                .HCenter()
                .VCenter();
        }

        // Interactive star
        return new MauiReactor.Button()
            .Text(isFilled ? "★" : "☆")
            .FontSize(32)
            .TextColor(isFilled ? AppColors.Caramel : AppColors.SecondaryText)
            .BackgroundColor(Colors.Transparent)
            .WidthRequest(40)
            .HeightRequest(40)
            .Padding(0)
            .OnClicked(() =>
            {
                _onRatingChanged?.Invoke(index);
            });
    }
}

public class RatingControlState
{
    public int Rating { get; set; }
}
