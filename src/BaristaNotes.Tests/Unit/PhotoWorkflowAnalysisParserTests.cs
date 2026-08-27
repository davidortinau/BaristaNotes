using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Tests.Unit;

public class PhotoWorkflowAnalysisParserTests
{
    [Fact]
    public void ParseResponse_ObviousCoffee_ReturnsExtractedCard()
    {
        const string json = """
            {
              "intent":"coffee",
              "isObvious":true,
              "rationale":"A coffee bag fills the image.",
              "name":"Worka Chelbesa",
              "roaster":"Onyx",
              "origin":"Ethiopia",
              "roastDate":"2026-08-20",
              "notes":"Washed; jasmine and peach"
            }
            """;

        var result = PhotoWorkflowAnalysisParser.ParseResponse(json);

        Assert.True(result.Success);
        Assert.True(result.IsObvious);
        Assert.Equal(PhotoWorkflowIntent.Coffee, result.Intent);
        Assert.NotNull(result.CoffeeDetails);
        Assert.Equal("Worka Chelbesa", result.CoffeeDetails.Name);
        Assert.Equal("Onyx", result.CoffeeDetails.Roaster);
        Assert.Equal("Ethiopia", result.CoffeeDetails.Origin);
        Assert.Equal(new DateTime(2026, 8, 20), result.CoffeeDetails.RoastDate);
        Assert.Equal("Washed; jasmine and peach", result.CoffeeDetails.Notes);
    }

    [Theory]
    [InlineData("profile", PhotoWorkflowIntent.Profile)]
    [InlineData("room", PhotoWorkflowIntent.Room)]
    public void ParseResponse_ObviousNonCoffee_ReturnsIntent(
        string intent,
        PhotoWorkflowIntent expected)
    {
        var json = $$"""{"intent":"{{intent}}","isObvious":true,"rationale":"Clear subject."}""";

        var result = PhotoWorkflowAnalysisParser.ParseResponse(json);

        Assert.True(result.Success);
        Assert.True(result.IsObvious);
        Assert.Equal(expected, result.Intent);
        Assert.Null(result.CoffeeDetails);
    }

    [Fact]
    public void ParseResponse_NotObvious_ForcesUnknown()
    {
        const string json = """
            {"intent":"coffee","isObvious":false,"rationale":"The image has mixed subjects."}
            """;

        var result = PhotoWorkflowAnalysisParser.ParseResponse(json);

        Assert.True(result.Success);
        Assert.False(result.IsObvious);
        Assert.Equal(PhotoWorkflowIntent.Unknown, result.Intent);
        Assert.Null(result.CoffeeDetails);
    }

    [Fact]
    public void ParseResponse_UnsupportedIntent_ForcesUnknown()
    {
        const string json = """
            {"intent":"equipment","isObvious":true,"rationale":"A grinder is visible."}
            """;

        var result = PhotoWorkflowAnalysisParser.ParseResponse(json);

        Assert.True(result.Success);
        Assert.False(result.IsObvious);
        Assert.Equal(PhotoWorkflowIntent.Unknown, result.Intent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{invalid")]
    public void ParseResponse_InvalidResponse_ReturnsFailure(string? response)
    {
        var result = PhotoWorkflowAnalysisParser.ParseResponse(response);

        Assert.False(result.Success);
        Assert.Equal(PhotoWorkflowIntent.Unknown, result.Intent);
        Assert.False(result.IsObvious);
        Assert.NotNull(result.ErrorMessage);
    }
}
