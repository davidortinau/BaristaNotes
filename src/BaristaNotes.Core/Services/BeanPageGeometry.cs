namespace BaristaNotes.Core.Services;

public static class BeanPageGeometry
{
    public const int HeaderPadding = 12;
    public const int LabelGap = 4;
    public const int SeparatorHeight = 1;

    public static double CompactHeaderHeight(double captionHeight, double countHeight) =>
        2 * HeaderPadding + captionHeight + LabelGap + countHeight + SeparatorHeight;

    public static double ExpandedMapHeight(double previousHeroHeight, double previousTop,
        double removedFooterHeight) => Math.Max(1, previousTop + previousHeroHeight - removedFooterHeight);

    public static double TitleTop(double heroHeight, double offset, double safeTop) =>
        Math.Max(heroHeight - offset, safeTop);

    public static double PinnedTitleInset(double titleTop, double safeTop) =>
        Math.Clamp(safeTop - titleTop, 0, safeTop);
}
