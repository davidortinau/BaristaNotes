using BaristaNotes.Components;

namespace BaristaNotes.Tests.Unit;

public sealed class BeanPageScrollGeometryTests
{
    [Theory]
    [InlineData(false, 0, 372, 372)]
    [InlineData(false, 24, 348, 372)]
    [InlineData(false, 59, 313, 372)]
    [InlineData(true, 0, 260, 260)]
    [InlineData(true, 24, 236, 260)]
    public void HeroEnlargesUpwardToPhysicalTopAndPreservesLowerEdge(
        bool landscape, double topInset, double previousHeight, double expected)
    {
        var height = BeanPageScrollGeometry.HeroHeight(landscape);
        Assert.Equal(expected, height);
        Assert.Equal(previousHeight + topInset, height);
        Assert.Equal(topInset, height - previousHeight);
        Assert.True(height > (landscape ? 140 : 252));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(24, 0)]
    [InlineData(59, 0)]
    [InlineData(59, 100)]
    [InlineData(59, 312)]
    [InlineData(59, 313)]
    [InlineData(59, 314)]
    [InlineData(59, 372)]
    [InlineData(59, 600)]
    [InlineData(59, -20)]
    public void CompactTitlePinsBelowStatusEdgeWithoutExpandedSafePadding(double topInset, double offset)
    {
        const double hero = 372;
        var overlayTop = BeanPageScrollGeometry.TitleOffset(hero, offset);
        var safePadding = BeanPageScrollGeometry.TitleSafePadding(hero, offset, topInset);
        var labelTop = overlayTop + safePadding;

        Assert.Equal(Math.Max(topInset, hero - offset), labelTop);
        Assert.InRange(safePadding, 0, topInset);
        Assert.Equal(0, BeanPageScrollGeometry.TitleSafePadding(hero, 0, topInset));
        Assert.Equal(topInset, BeanPageScrollGeometry.TitleSafePadding(hero, hero, topInset));
    }

    [Theory]
    [InlineData(65, 0, 0, 65)]
    [InlineData(65, 0, 372, 65)]
    [InlineData(65, 59, -20, 65)]
    [InlineData(65, 59, 0, 65)]
    [InlineData(65, 59, 313, 65)]
    [InlineData(65, 59, 314, 66)]
    [InlineData(65, 59, 340, 92)]
    [InlineData(65, 59, 371, 123)]
    [InlineData(65, 59, 372, 124)]
    [InlineData(65, 24, 360, 77)]
    [InlineData(65, 24, 372, 89)]
    [InlineData(250, 59, 372, 309)]
    public void ScrollSpacerReservesOverlayCoverSoFirstRowStartsBelowSeparator(
        double titleHeight, double topInset, double offset, double expectedSpaceHeight)
    {
        const double hero = 372;
        var spaceHeight = BeanPageScrollGeometry.TitleSpaceHeight(hero, titleHeight, offset, topInset);
        var separatorBottom = BeanPageScrollGeometry.TitleOffset(hero, offset)
            + BeanPageScrollGeometry.TitleSafePadding(hero, offset, topInset) + titleHeight;
        var firstRowTop = hero + spaceHeight - offset;

        Assert.Equal(expectedSpaceHeight, spaceHeight);
        Assert.Equal(separatorBottom, firstRowTop);
        Assert.Equal(titleHeight, BeanPageScrollGeometry.TitleSpaceHeight(hero, titleHeight, 0, topInset));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 59)]
    [InlineData(160, 59)]
    [InlineData(2 * 81, 59)]
    [InlineData(2 * 81 + 52, 24)]
    [InlineData(2 * 81 + 52, 59)]
    [InlineData(5 * 81, 59)]
    public void ShortListExtentStaysStableThroughSafeAreaPinAndReverseWithoutDoubleCountingPadding(
        double bodyHeight, double topInset)
    {
        const double viewport = 650;
        const double hero = 372;
        const double title = 65;
        var measuredSpace = title;
        foreach (var offset in new[] { 0d, 100, 313, 314, 340, 371, 372, 371, 340, 314, 313, 100, 0 })
        {
            var measuredHeader = hero + measuredSpace + bodyHeight;
            var belowTitle = measuredHeader - hero - measuredSpace;
            var requestedSpace = BeanPageScrollGeometry.TitleSpaceHeight(hero, title, offset, topInset);
            var tail = BeanPageScrollGeometry.TailHeight(viewport, requestedSpace, belowTitle);
            var contentHeight = hero + requestedSpace + bodyHeight + tail;
            var separatorBottom = BeanPageScrollGeometry.TitleOffset(hero, offset)
                + BeanPageScrollGeometry.TitleSafePadding(hero, offset, topInset) + title;

            Assert.Equal(bodyHeight, belowTitle);
            Assert.Equal(hero, contentHeight - viewport);
            Assert.Equal(separatorBottom, hero + requestedSpace - offset);
            measuredSpace = requestedSpace;
        }
        Assert.Equal(title, measuredSpace);
    }

    [Theory]
    [InlineData(0, 372)]
    [InlineData(100, 272)]
    [InlineData(371, 1)]
    [InlineData(372, 0)]
    [InlineData(600, 0)]
    [InlineData(-20, 392)]
    public void TitleOverlayRetainsExistingFollowAndPinMotion(double offset, double expected)
    {
        Assert.Equal(expected, BeanPageScrollGeometry.TitleOffset(372, offset));
    }

    [Fact]
    public void ReverseScrollReleasesSafePaddingAndRestoresCompactHeaderAtMapEdge()
    {
        const double hero = 372;
        const double topInset = 59;
        foreach (var offset in new[] { 0d, 100, 313, 372, 600, 372, 313, 100, 0 })
        {
            var overlayTop = BeanPageScrollGeometry.TitleOffset(hero, offset);
            var safePadding = BeanPageScrollGeometry.TitleSafePadding(hero, offset, topInset);
            Assert.Equal(Math.Max(topInset, hero - offset), overlayTop + safePadding);
            if (offset <= hero - topInset)
                Assert.Equal(0, safePadding);
            if (offset >= hero)
                Assert.Equal(topInset, safePadding);
        }
        Assert.Equal(hero, BeanPageScrollGeometry.TitleOffset(hero, 0));
        Assert.Equal(0, BeanPageScrollGeometry.ParallaxOffset(hero, 0));
    }

    [Fact]
    public void IntermediateParallaxMovesMapMoreSlowlyButItsClipCompletelyLeaves()
    {
        const double hero = 372;
        const double offset = 100;
        var translation = BeanPageScrollGeometry.ParallaxOffset(hero, offset);
        Assert.Equal(16, translation);
        Assert.InRange(-offset + translation, -offset + 1, -1);
        Assert.Equal(hero - offset, BeanPageScrollGeometry.TitleOffset(hero, offset));
        foreach (var fullyScrolledOffset in new[] { hero, hero + 100 })
            Assert.True(hero - fullyScrolledOffset <= 0);
        Assert.Equal(0, BeanPageScrollGeometry.TitleOffset(hero, hero));
        Assert.Equal(0, BeanPageScrollGeometry.ParallaxOffset(hero, 0));
        Assert.Equal(hero, BeanPageScrollGeometry.TitleOffset(hero, 0));
        Assert.Equal(0, BeanPageScrollGeometry.ParallaxOffset(hero, -20));
        Assert.Equal(hero * 0.16, BeanPageScrollGeometry.ParallaxOffset(hero, 600));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 59)]
    [InlineData(160, 59)]
    [InlineData(2 * 81 + 52, 24)]
    [InlineData(2 * 81 + 52, 59)]
    [InlineData(5 * 81, 59)]
    [InlineData(20 * 81, 0)]
    [InlineData(20 * 81, 59)]
    public void EmptyShortFilteredAndLongBodiesCanScrollPastEntireHero(double bodyHeight, double topInset)
    {
        const double viewport = 650;
        const double hero = 372;
        const double title = 65;
        var titleSpace = BeanPageScrollGeometry.TitleSpaceHeight(hero, title, hero, topInset);
        var tail = BeanPageScrollGeometry.TailHeight(viewport, titleSpace, bodyHeight);
        var maximumOffset = hero + titleSpace + bodyHeight + tail - viewport;
        Assert.True(maximumOffset >= hero);
        Assert.Equal(0, BeanPageScrollGeometry.TitleOffset(hero, maximumOffset));
        Assert.Equal(Math.Max(1, viewport - titleSpace - bodyHeight), tail);
    }

    [Fact]
    public void LargerTitleIsAccountedForWithoutFixedFontSizeAssumptions()
    {
        Assert.Equal(240, BeanPageScrollGeometry.TailHeight(650, 250, 160));
    }

    [Fact]
    public void IosTwoRowBootstrapReservesMeasuredTitleBeforeBodyGateAndCreatesFullCollapseExtent()
    {
        const double viewport = 650;
        const double hero = 372;
        const double title = 77;
        const double rows = 2 * 81;
        const double topInset = 59;
        var initialSpace = BeanPageScrollGeometry.MinimumTitleHeight;
        const double initialFooter = 1;
        var initialHeader = hero + initialSpace;
        var initialFooterTop = initialHeader + rows;

        var space = BeanPageScrollGeometry.TitleSpaceHeight(hero, title, 0, topInset);
        Assert.Equal(77, space);
        Assert.False(BeanPageScrollGeometry.TryGetNativeBodyHeight(
            initialFooterTop + initialFooter, initialHeader, initialFooterTop, initialFooter,
            hero + space, initialFooter, hero, space, out _));

        var header = hero + space;
        var footerTop = header + rows;
        Assert.True(BeanPageScrollGeometry.TryGetNativeBodyHeight(
            footerTop + initialFooter, header, footerTop, initialFooter,
            header, initialFooter, hero, space, out var body));
        Assert.Equal(rows, body);
        Assert.Equal(hero + title, header);
        Assert.True(footerTop + initialFooter - viewport < hero);

        var tail = BeanPageScrollGeometry.TailHeight(viewport, space, body);
        Assert.Equal(411, tail);
        Assert.Equal(hero, footerTop + tail - viewport);

        foreach (var offset in new[] { 0d, 100, 313, 340, 371, 372, 371, 340, 313, 100, 0 })
        {
            space = BeanPageScrollGeometry.TitleSpaceHeight(hero, title, offset, topInset);
            tail = BeanPageScrollGeometry.TailHeight(viewport, space, body);
            var separatorBottom = BeanPageScrollGeometry.TitleOffset(hero, offset)
                + BeanPageScrollGeometry.TitleSafePadding(hero, offset, topInset) + title;

            Assert.Equal(hero, hero + space + rows + tail - viewport);
            Assert.Equal(separatorBottom, hero + space - offset);
        }
        Assert.Equal(136, BeanPageScrollGeometry.TitleSpaceHeight(hero, title, hero, topInset));
        Assert.Equal(0, BeanPageScrollGeometry.TitleOffset(hero, hero));
        Assert.Equal(title, space);
    }

    [Theory]
    [InlineData(1022, 437, 599, 423, 496, 364, 124)]
    [InlineData(1022, 496, 658, 364, 437, 423, 65)]
    [InlineData(1081, 496, 658, 364, 496, 364, 124)]
    [InlineData(1022, 496, 658, 364, 0, 364, 124)]
    [InlineData(1022, 496, 658, 364, 496, 423, 124)]
    [InlineData(1022, 437, 599, 423, 437, 423, 124)]
    public void IosMixedHeaderFooterOrContentSizeSnapshotsCannotReplaceSettledBodyHeight(
        double content, double nativeHeader, double footerTop, double nativeFooter,
        double measuredHeader, double requestedFooter, double measuredSpace)
    {
        Assert.False(BeanPageScrollGeometry.TryGetNativeBodyHeight(
            content, nativeHeader, footerTop, nativeFooter, measuredHeader, requestedFooter,
            372, measuredSpace, out _));
    }

    [Theory]
    [InlineData(0.5, true)]
    [InlineData(0.5001, false)]
    public void IosNativeAndMauiMeasurementAgreementHasHalfPointTolerance(double difference, bool expected)
    {
        Assert.Equal(expected, BeanPageScrollGeometry.TryGetNativeBodyHeight(
            1022, 496, 658, 364, 496 + difference, 364, 372, 124, out var body));
        if (expected)
            Assert.Equal(162, body);
    }

    [Theory]
    [InlineData(0, 0, 65, 59)]
    [InlineData(0, 160, 65, 59)]
    [InlineData(162, 0, 65, 59)]
    [InlineData(162, 52, 65, 59)]
    [InlineData(405, 0, 65, 24)]
    [InlineData(1620, 52, 65, 59)]
    [InlineData(162, 0, 250, 59)]
    public void IosSettledNativeGeometryKeepsBodyAndPinReservationStableThroughReverse(
        double rows, double belowTitle, double title, double topInset)
    {
        const double viewport = 650;
        const double hero = 372;
        var expectedBody = rows + belowTitle;
        foreach (var offset in new[] { 0d, 100, 313, 314, 340, 371, 372, 371, 340, 314, 313, 100, 0 })
        {
            var space = BeanPageScrollGeometry.TitleSpaceHeight(hero, title, offset, topInset);
            var header = hero + space + belowTitle;
            var footerTop = header + rows;
            var footer = BeanPageScrollGeometry.TailHeight(viewport, space, expectedBody);
            var content = footerTop + footer;

            Assert.True(BeanPageScrollGeometry.TryGetNativeBodyHeight(
                content, header, footerTop, footer, header, footer, hero, space, out var body));
            Assert.Equal(expectedBody, body);
            Assert.Equal(footer, BeanPageScrollGeometry.TailHeight(viewport, space, body));
            Assert.Equal(Math.Max(hero, hero + space + expectedBody + 1 - viewport), content - viewport);
            Assert.Equal(
                BeanPageScrollGeometry.TitleOffset(hero, offset)
                    + BeanPageScrollGeometry.TitleSafePadding(hero, offset, topInset) + title,
                hero + space - offset);
        }
    }
}
