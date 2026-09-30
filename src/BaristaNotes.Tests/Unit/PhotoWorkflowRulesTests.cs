using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;

namespace BaristaNotes.Tests.Unit;

public sealed class PhotoWorkflowRulesTests
{
    [Theory]
    [InlineData(PhotoWorkflowIntent.Coffee, PhotoIntentChoice.Coffee)]
    [InlineData(PhotoWorkflowIntent.Profile, PhotoIntentChoice.Profile)]
    [InlineData(PhotoWorkflowIntent.Room, PhotoIntentChoice.Room)]
    public void ObviousSuccessfulAnalysis_SelectsTheSourceRoute(PhotoWorkflowIntent intent, PhotoIntentChoice expected)
    {
        Assert.Equal(expected, PhotoWorkflowRules.AutomaticChoice(
            new PhotoWorkflowAnalysis { Success = true, IsObvious = true, Intent = intent }));
    }

    [Theory]
    [InlineData(false, true, PhotoWorkflowIntent.Coffee)]
    [InlineData(true, false, PhotoWorkflowIntent.Coffee)]
    [InlineData(true, true, PhotoWorkflowIntent.Unknown)]
    public void UncertainOrFailedAnalysis_RequiresAChoice(bool success, bool obvious, PhotoWorkflowIntent intent)
    {
        Assert.Null(PhotoWorkflowRules.AutomaticChoice(
            new PhotoWorkflowAnalysis { Success = success, IsObvious = obvious, Intent = intent }));
    }

    [Fact]
    public void EmptyCoffeeDetails_RequireExtractionButAnyVisibleFieldPreventsIt()
    {
        Assert.True(PhotoWorkflowRules.NeedsCoffeeExtraction(null));
        Assert.True(PhotoWorkflowRules.NeedsCoffeeExtraction(new BeanLabelExtraction { Name = " ", Notes = "\n" }));
        Assert.False(PhotoWorkflowRules.NeedsCoffeeExtraction(new BeanLabelExtraction { Name = "Coffee" }));
        Assert.False(PhotoWorkflowRules.NeedsCoffeeExtraction(new BeanLabelExtraction { Roaster = "Roaster" }));
        Assert.False(PhotoWorkflowRules.NeedsCoffeeExtraction(new BeanLabelExtraction { Origin = "Origin" }));
        Assert.False(PhotoWorkflowRules.NeedsCoffeeExtraction(new BeanLabelExtraction { RoastDate = DateTime.Today }));
        Assert.False(PhotoWorkflowRules.NeedsCoffeeExtraction(new BeanLabelExtraction { Notes = "Washed" }));
    }

    [Fact]
    public void FailedExtraction_UsesBlankEditablePrefillRatherThanFailedFields()
    {
        var failed = new BeanLabelExtraction { Success = false, Name = "Unreliable" };
        Assert.False(PhotoWorkflowRules.NeedsCoffeeExtraction(failed));
        var prefill = PhotoWorkflowRules.CoffeePrefill(failed);
        Assert.True(prefill.Success);
        Assert.Null(prefill.Name);
        var successful = new BeanLabelExtraction { Success = true, Name = "Coffee" };
        Assert.Same(successful, PhotoWorkflowRules.CoffeePrefill(successful));
    }
}
