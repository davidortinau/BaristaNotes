using BaristaNotes.Core.Services;

namespace BaristaNotes.Tests.Unit;

public sealed class BeanPageGeometryTests
{
    [Theory]
    [InlineData(0, 360, 80)]
    [InlineData(24, 336, 80)]
    [InlineData(59, 321, 80)]
    public void ExtendingMapUpwardPreservesItsFormerVisibleLowerEdge(double top, double hero, double footer)
    {
        var height = BeanPageGeometry.ExpandedMapHeight(hero, top, footer);

        Assert.Equal(top + hero - footer, height);
        Assert.Equal(top, height - (hero - footer));
    }

    [Theory]
    [InlineData(12, 38, 79)]
    [InlineData(18, 76, 123)]
    public void CompactHeaderUsesNaturalLabelHeightsAndOnlySmallGaps(double caption, double count, double expected)
    {
        var height = BeanPageGeometry.CompactHeaderHeight(caption, count);

        Assert.Equal(expected, height);
        Assert.Equal(29, height - caption - count);
        Assert.Equal(1, BeanPageGeometry.SeparatorHeight);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(59)]
    public void HeaderPinsAtSafeEdgeAndReleasesInReverseWithoutChangingHeroHeight(double safeTop)
    {
        const double hero = 300;
        var pinOffset = hero - safeTop;

        Assert.Equal(hero, BeanPageGeometry.TitleTop(hero, 0, safeTop));
        Assert.Equal(safeTop + 1, BeanPageGeometry.TitleTop(hero, pinOffset - 1, safeTop));
        Assert.Equal(safeTop, BeanPageGeometry.TitleTop(hero, pinOffset, safeTop));
        Assert.Equal(safeTop, BeanPageGeometry.TitleTop(hero, hero + 500, safeTop));
        Assert.Equal(safeTop + 1, BeanPageGeometry.TitleTop(hero, pinOffset - 1, safeTop));
        Assert.Equal(hero, BeanPageGeometry.TitleTop(hero, 0, safeTop));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(59)]
    [InlineData(88)]
    public void FullyCollapsedFirstBeanStartsBelowPinnedSeparator(double safeTop)
    {
        const double hero = 814;
        const double titleHeight = 231;
        var titleTop = hero - hero;
        var reservedTitleHeight = titleHeight + BeanPageGeometry.PinnedTitleInset(titleTop, safeTop);
        var separatorBottom = BeanPageGeometry.TitleTop(hero, hero, safeTop) + titleHeight;

        Assert.Equal(separatorBottom, titleTop + reservedTitleHeight);
        Assert.Equal(titleHeight + safeTop, reservedTitleHeight);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(24)]
    [InlineData(59)]
    [InlineData(88)]
    public void SafeInsetReservationIsContinuousAndLeavesNoExpandedHeaderGap(double safeTop)
    {
        const double titleHeight = 231;
        Assert.Equal(0, BeanPageGeometry.PinnedTitleInset(814, safeTop));
        Assert.Equal(0, BeanPageGeometry.PinnedTitleInset(safeTop + 1, safeTop));
        Assert.Equal(0, BeanPageGeometry.PinnedTitleInset(safeTop, safeTop));

        foreach (var titleTop in new[] { safeTop, safeTop / 2, 0 })
        {
            var inset = BeanPageGeometry.PinnedTitleInset(titleTop, safeTop);
            Assert.Equal(safeTop + titleHeight, titleTop + titleHeight + inset);
        }

        Assert.Equal(safeTop, BeanPageGeometry.PinnedTitleInset(-100, safeTop));
        Assert.Equal(0, BeanPageGeometry.PinnedTitleInset(safeTop + 1, safeTop));
        Assert.Equal(0, BeanPageGeometry.PinnedTitleInset(814, safeTop));
    }
}
