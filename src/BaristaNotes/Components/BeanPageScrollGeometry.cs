namespace BaristaNotes.Components;

internal static class BeanPageScrollGeometry
{
    internal const double PortraitHeroHeight = 372;
    internal const double LandscapeHeroHeight = 260;
    internal const double MinimumTitleHeight = 1;

    internal static double HeroHeight(bool landscape) =>
        landscape ? LandscapeHeroHeight : PortraitHeroHeight;

    internal static double TitleOffset(double heroHeight, double offset) =>
        Math.Max(0, heroHeight - offset);

    internal static double TitleSafePadding(double heroHeight, double offset, double topInset) =>
        Math.Max(0, topInset - TitleOffset(heroHeight, offset));

    internal static double TitleSpaceHeight(double heroHeight, double titleHeight, double offset, double topInset) =>
        titleHeight + TitleSafePadding(heroHeight, offset, topInset);

    internal static double ParallaxOffset(double heroHeight, double offset) =>
        Math.Clamp(offset, 0, heroHeight) * 0.16;

    internal static double TailHeight(double viewportHeight, double titleHeight, double bodyHeight) =>
        Math.Max(1, viewportHeight - titleHeight - bodyHeight);

    internal static bool TryGetNativeBodyHeight(
        double contentHeight, double nativeHeaderHeight, double nativeFooterTop, double nativeFooterHeight,
        double measuredHeaderHeight, double requestedFooterHeight,
        double heroHeight, double measuredTitleSpaceHeight, out double bodyHeight)
    {
        bodyHeight = 0;
        if (!double.IsFinite(contentHeight) || !double.IsFinite(nativeHeaderHeight)
            || !double.IsFinite(nativeFooterTop) || !double.IsFinite(nativeFooterHeight)
            || !double.IsFinite(measuredHeaderHeight)
            || !double.IsFinite(requestedFooterHeight) || !double.IsFinite(heroHeight)
            || !double.IsFinite(measuredTitleSpaceHeight)
            || nativeHeaderHeight <= 0 || nativeFooterHeight < 0
            || heroHeight <= 0 || measuredTitleSpaceHeight <= 0
            || Math.Abs(nativeHeaderHeight - measuredHeaderHeight) > 0.5
            || Math.Abs(nativeFooterHeight - requestedFooterHeight) > 0.5
            || Math.Abs(contentHeight - nativeFooterTop - nativeFooterHeight) > 0.5
            || nativeHeaderHeight + 0.5 < heroHeight + measuredTitleSpaceHeight
            || nativeFooterTop + 0.5 < nativeHeaderHeight)
            return false;

        bodyHeight = Math.Max(0, contentHeight - nativeFooterHeight - heroHeight - measuredTitleSpaceHeight);
        return true;
    }
}
