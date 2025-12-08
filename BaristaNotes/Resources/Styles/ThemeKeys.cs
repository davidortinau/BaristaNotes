namespace BaristaNotes.Styles;

/// <summary>
/// Centralized theme key constants for MauiReactor theme system.
/// Use these with .ThemeKey() method on components.
/// </summary>
public static class ThemeKeys
{
    // Label theme keys
    public const string Headline = nameof(Headline);
    public const string SubHeadline = nameof(SubHeadline);
    public const string PrimaryText = nameof(PrimaryText);
    public const string SecondaryText = nameof(SecondaryText);
    public const string TextSecondary = nameof(TextSecondary); // Alias for compatibility
    public const string MutedText = nameof(MutedText);
    public const string Caption = nameof(Caption);
    public const string CardTitle = nameof(CardTitle);
    public const string CardSubtitle = nameof(CardSubtitle);
    public const string FormTitle = nameof(FormTitle);
    public const string FormLabel = nameof(FormLabel);
    
    // Border theme keys
    public const string Card = nameof(Card);
    public const string CardBorder = nameof(CardBorder);
    public const string SelectedCard = nameof(SelectedCard);
    public const string CardVariant = nameof(CardVariant);
    public const string InputBorder = nameof(InputBorder);
    public const string BottomSheet = nameof(BottomSheet);
    
    // Button theme keys
    public const string PrimaryButton = nameof(PrimaryButton);
    public const string SecondaryButton = nameof(SecondaryButton);
    public const string DangerButton = nameof(DangerButton);
    
    // Entry/Editor theme keys
    public const string Entry = nameof(Entry);
    
    // Rating display theme keys
    public const string RatingAverage = nameof(RatingAverage);
    public const string RatingIcon = nameof(RatingIcon);
    public const string RatingBarBackground = nameof(RatingBarBackground);
    public const string RatingBarFilled = nameof(RatingBarFilled);
}
