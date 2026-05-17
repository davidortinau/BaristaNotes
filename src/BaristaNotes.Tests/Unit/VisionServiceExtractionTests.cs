using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Tests.Unit;

/// <summary>
/// Tests for <see cref="BeanLabelParser"/>, which VisionService.ExtractBeanLabelAsync
/// delegates to for turning raw model output into a <see cref="BeanLabelExtraction"/>.
/// </summary>
public class VisionServiceExtractionTests
{
    [Fact]
    public void ParseResponse_CleanJson_AllFieldsPopulated()
    {
        const string json = """
            {"name":"Yirgacheffe","roaster":"Blue Bottle","origin":"Ethiopia","roastDate":"2024-05-14"}
            """;

        var result = BeanLabelParser.ParseResponse(json);

        Assert.True(result.Success);
        Assert.Equal("Yirgacheffe", result.Name);
        Assert.Equal("Blue Bottle", result.Roaster);
        Assert.Equal("Ethiopia", result.Origin);
        Assert.NotNull(result.RoastDate);
        Assert.Equal(new DateTime(2024, 5, 14), result.RoastDate!.Value.Date);
        Assert.Equal(json, result.RawResponse);
    }

    [Fact]
    public void ParseResponse_PartialJson_OnlyNamePopulated()
    {
        const string json = """
            {"name":"Monarch","roaster":null,"origin":null,"roastDate":null}
            """;

        var result = BeanLabelParser.ParseResponse(json);

        Assert.True(result.Success);
        Assert.Equal("Monarch", result.Name);
        Assert.Null(result.Roaster);
        Assert.Null(result.Origin);
        Assert.Null(result.RoastDate);
    }

    [Fact]
    public void ParseResponse_MissingFields_ReturnsNulls()
    {
        const string json = """{"name":"Monarch"}""";

        var result = BeanLabelParser.ParseResponse(json);

        Assert.True(result.Success);
        Assert.Equal("Monarch", result.Name);
        Assert.Null(result.Roaster);
        Assert.Null(result.Origin);
        Assert.Null(result.RoastDate);
    }

    [Fact]
    public void ParseResponse_MarkdownFencedJson_StripsFences()
    {
        const string response = """
            ```json
            {"name":"Black Cat Espresso","roaster":"Intelligentsia","origin":"Blend","roastDate":"2024-01-02"}
            ```
            """;

        var result = BeanLabelParser.ParseResponse(response);

        Assert.True(result.Success);
        Assert.Equal("Black Cat Espresso", result.Name);
        Assert.Equal("Intelligentsia", result.Roaster);
        Assert.Equal("Blend", result.Origin);
        Assert.Equal(new DateTime(2024, 1, 2), result.RoastDate!.Value.Date);
    }

    [Fact]
    public void ParseResponse_PlainFencesWithoutLang_StripsFences()
    {
        const string response = """
            ```
            {"name":"Test","roaster":null,"origin":null,"roastDate":null}
            ```
            """;

        var result = BeanLabelParser.ParseResponse(response);

        Assert.True(result.Success);
        Assert.Equal("Test", result.Name);
    }

    [Fact]
    public void ParseResponse_ProseAroundJson_ExtractsJsonBlock()
    {
        const string response = """
            Sure, here is the extracted info:
            {"name":"Hologram","roaster":"Counter Culture","origin":"Colombia Huila","roastDate":null}
            Let me know if you need anything else.
            """;

        var result = BeanLabelParser.ParseResponse(response);

        Assert.True(result.Success);
        Assert.Equal("Hologram", result.Name);
        Assert.Equal("Counter Culture", result.Roaster);
        Assert.Equal("Colombia Huila", result.Origin);
        Assert.Null(result.RoastDate);
    }

    [Fact]
    public void ParseResponse_InvalidJson_ReturnsFailure()
    {
        const string response = "{ this is not valid json";

        var result = BeanLabelParser.ParseResponse(response);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ParseResponse_NoJsonAtAll_ReturnsFailure()
    {
        const string response = "I'm sorry, I can't read the label.";

        var result = BeanLabelParser.ParseResponse(response);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ParseResponse_NullInput_ReturnsFailure()
    {
        var result = BeanLabelParser.ParseResponse(null);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ParseResponse_EmptyInput_ReturnsFailure()
    {
        var result = BeanLabelParser.ParseResponse("   ");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void ParseResponse_BlankStringFields_TreatedAsNull()
    {
        const string json = """{"name":"  ","roaster":"","origin":"Kenya","roastDate":""}""";

        var result = BeanLabelParser.ParseResponse(json);

        Assert.True(result.Success);
        Assert.Null(result.Name);
        Assert.Null(result.Roaster);
        Assert.Equal("Kenya", result.Origin);
        Assert.Null(result.RoastDate);
    }

    [Fact]
    public void ParseResponse_UnparseableDate_ReturnsNullDateButStillSuccess()
    {
        const string json = """{"name":"X","roaster":null,"origin":null,"roastDate":"not-a-date"}""";

        var result = BeanLabelParser.ParseResponse(json);

        Assert.True(result.Success);
        Assert.Null(result.RoastDate);
    }

    [Fact]
    public void ParseResponse_AlternateDateFormat_Parsed()
    {
        const string json = """{"name":"X","roaster":null,"origin":null,"roastDate":"2024/06/01"}""";

        var result = BeanLabelParser.ParseResponse(json);

        Assert.True(result.Success);
        Assert.NotNull(result.RoastDate);
        Assert.Equal(new DateTime(2024, 6, 1), result.RoastDate!.Value.Date);
    }

    [Fact]
    public void ParseResponse_PreservesRawResponse()
    {
        const string response = "prose {\"name\":\"X\"} more";

        var result = BeanLabelParser.ParseResponse(response);

        Assert.Equal(response, result.RawResponse);
    }
}
