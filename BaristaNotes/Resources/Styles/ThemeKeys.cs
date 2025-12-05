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
    
    // Border theme keys
    public const string Card = nameof(Card);
    public const string CardBorder = nameof(CardBorder);
    public const string SelectedCard = nameof(SelectedCard);
    public const string CardVariant = nameof(CardVariant);
    public const string InputBorder = nameof(InputBorder);
    
    // Button theme keys
    public const string SecondaryButton = nameof(SecondaryButton);
    public const string DangerButton = nameof(DangerButton);
}
