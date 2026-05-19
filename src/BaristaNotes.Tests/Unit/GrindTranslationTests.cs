using BaristaNotes.Core.Services.Grind;
using Xunit;

namespace BaristaNotes.Tests.Unit;

public class GrindHintParserTests
{
    [Theory]
    [InlineData("725µm", 725)]
    [InlineData("725 µm", 725)]
    [InlineData("725um", 725)]
    [InlineData("725 microns", 725)]
    [InlineData("750 micron", 750)]
    [InlineData("Grind: 400µm", 400)]
    public void Parse_Microns_ReturnsMicronKind(string raw, int expected)
    {
        var p = GrindHintParser.Parse(raw);
        Assert.Equal(GrindHintKind.Microns, p.Kind);
        Assert.Equal(expected, p.Microns);
        Assert.Equal($"um:{expected}", p.Normalized);
        Assert.NotNull(p.MicronRange);
    }

    [Theory]
    [InlineData("fine", GrindDescriptor.Fine)]
    [InlineData("Fine", GrindDescriptor.Fine)]
    [InlineData("medium", GrindDescriptor.Medium)]
    [InlineData("medium-fine", GrindDescriptor.MediumFine)]
    [InlineData("medium fine", GrindDescriptor.MediumFine)]
    [InlineData("medium-coarse", GrindDescriptor.MediumCoarse)]
    [InlineData("coarse", GrindDescriptor.Coarse)]
    [InlineData("extra-fine", GrindDescriptor.ExtraFine)]
    [InlineData("very coarse", GrindDescriptor.ExtraCoarse)]
    public void Parse_Descriptor_ReturnsDescriptiveKind(string raw, GrindDescriptor expected)
    {
        var p = GrindHintParser.Parse(raw);
        Assert.Equal(GrindHintKind.Descriptive, p.Kind);
        Assert.Equal(expected, p.Descriptor);
        Assert.NotNull(p.Microns);
        Assert.NotNull(p.MicronRange);
    }

    [Fact]
    public void Parse_MediumFine_BeforeMedium_DoesNotMatchMediumOnly()
    {
        // Regression: "medium-fine" must not be classified as plain "medium".
        var p = GrindHintParser.Parse("medium-fine");
        Assert.Equal(GrindDescriptor.MediumFine, p.Descriptor);
    }

    [Fact]
    public void Parse_NumericWithScale_Recognized()
    {
        var p = GrindHintParser.Parse("EK43: 7.5");
        Assert.Equal(GrindHintKind.Numeric, p.Kind);
        Assert.Equal("ek43", p.NumericScale);
        Assert.Equal(7.5m, p.NumericValue);
    }

    [Fact]
    public void Parse_EmptyOrWhitespace_ReturnsUnknown()
    {
        Assert.Equal(GrindHintKind.Unknown, GrindHintParser.Parse(null).Kind);
        Assert.Equal(GrindHintKind.Unknown, GrindHintParser.Parse("").Kind);
        Assert.Equal(GrindHintKind.Unknown, GrindHintParser.Parse("   ").Kind);
    }

    [Fact]
    public void Parse_OutOfRangeMicron_FallsThrough()
    {
        // 30µm is not realistic and should not be accepted as microns.
        var p = GrindHintParser.Parse("30µm");
        Assert.NotEqual(GrindHintKind.Microns, p.Kind);
    }
}

public class DeterministicGrindInterpolatorTests
{
    [Fact]
    public void Interpolate_Df64Seed_700Micron_MapsNearPourOver()
    {
        // 700µm is anchored at dial 60 on the DF64V chart curve (V60/pour-over upper).
        var result = DeterministicGrindInterpolator.Interpolate(
            DeterministicGrindInterpolator.DF64SeedAnchors,
            targetMicron: 700m);
        Assert.NotNull(result);
        Assert.InRange(result!.Suggested, 55m, 65m);
        Assert.True(result.Min <= result.Suggested && result.Suggested <= result.Max);
    }

    [Fact]
    public void Interpolate_BelowMinAnchor_ClampsToMin()
    {
        var result = DeterministicGrindInterpolator.Interpolate(
            DeterministicGrindInterpolator.DF64SeedAnchors,
            targetMicron: 30m);
        Assert.NotNull(result);
        // Smallest seed anchor on the 0–90 dial is 0 (Turkish floor, 50µm).
        Assert.Equal(0m, result!.Suggested);
    }

    [Fact]
    public void Interpolate_AboveMaxAnchor_ClampsToMax()
    {
        var result = DeterministicGrindInterpolator.Interpolate(
            DeterministicGrindInterpolator.DF64SeedAnchors,
            targetMicron: 5000m);
        Assert.NotNull(result);
        // Largest seed anchor on the 0–90 dial is 90 (cold drip/brew ceiling, 1300µm).
        Assert.Equal(90m, result!.Suggested);
    }

    [Fact]
    public void Interpolate_FewerThanTwoAnchors_ReturnsNull()
    {
        var result = DeterministicGrindInterpolator.Interpolate(
            new[] { new GrindAnchor(500m, 3m, "x") },
            targetMicron: 600m);
        Assert.Null(result);
    }

    [Fact]
    public void Interpolate_RespectsMinMaxSettingClamps()
    {
        var result = DeterministicGrindInterpolator.Interpolate(
            DeterministicGrindInterpolator.DF64SeedAnchors,
            targetMicron: 725m,
            minSetting: 6.0m,
            maxSetting: 9.0m);
        Assert.NotNull(result);
        Assert.True(result!.Suggested >= 6.0m);
    }

    [Fact]
    public void SerializeAndParse_RoundTrips()
    {
        var anchors = new[]
        {
            new GrindAnchor(200m, 1.2m, "community"),
            new GrindAnchor(725m, 5.5m, "ai", DateTime.UtcNow),
        };
        var json = DeterministicGrindInterpolator.SerializeAnchors(anchors);
        var parsed = DeterministicGrindInterpolator.ParseAnchors(json);
        Assert.Equal(2, parsed.Count);
        Assert.Equal(725m, parsed[1].Micron);
        Assert.Equal(5.5m, parsed[1].Setting);
        Assert.Equal("ai", parsed[1].Source);
    }

    [Fact]
    public void ParseAnchors_MalformedJson_ReturnsEmpty()
    {
        Assert.Empty(DeterministicGrindInterpolator.ParseAnchors("not-json"));
        Assert.Empty(DeterministicGrindInterpolator.ParseAnchors(null));
        Assert.Empty(DeterministicGrindInterpolator.ParseAnchors(""));
    }
}
