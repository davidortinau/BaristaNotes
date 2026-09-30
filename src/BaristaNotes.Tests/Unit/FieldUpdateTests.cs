using System.Text.Json;
using System.Text.Json.Serialization;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Tests.Unit;

public class FieldUpdateTests
{
    [Fact]
    public void Default_IsUnspecified()
    {
        var update = default(FieldUpdate<string?>);

        Assert.False(update.IsSpecified);
        Assert.Null(update.Value);
    }

    [Fact]
    public void ExplicitNull_IsSpecified()
    {
        FieldUpdate<int?> number = (int?)null;
        FieldUpdate<string?> text = (string?)null;

        Assert.True(number.IsSpecified);
        Assert.Null(number.Value);
        Assert.True(text.IsSpecified);
        Assert.Null(text.Value);
    }

    [Fact]
    public void RecordCopy_RetainsUnspecifiedFields()
    {
        var original = new UpdateShotDto { DrinkType = "Espresso" };
        var changed = original with { Rating = 0 };

        Assert.False(original.Rating.IsSpecified);
        Assert.True(changed.Rating.IsSpecified);
        Assert.Equal(0, changed.Rating.Value);
        Assert.False(changed.TastingNotes.IsSpecified);
    }

    [Theory]
    [InlineData(JsonIgnoreCondition.Never)]
    [InlineData(JsonIgnoreCondition.WhenWritingNull)]
    [InlineData(JsonIgnoreCondition.WhenWritingDefault)]
    public void JsonRoundTrip_PreservesOmittedSetAndClear(JsonIgnoreCondition ignoreCondition)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = ignoreCondition
        };
        var original = new UpdateShotDto
        {
            DrinkType = "Espresso",
            Rating = 0,
            MachineId = FieldUpdate<int?>.Set(null),
            TastingNotes = FieldUpdate<string?>.Set(null)
        };

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<UpdateShotDto>(json, options);

        Assert.NotNull(restored);
        Assert.True(restored.Rating.IsSpecified);
        Assert.Equal(0, restored.Rating.Value);
        Assert.True(restored.MachineId.IsSpecified);
        Assert.Null(restored.MachineId.Value);
        Assert.True(restored.TastingNotes.IsSpecified);
        Assert.Null(restored.TastingNotes.Value);
        Assert.False(restored.GrinderId.IsSpecified);
    }
}
