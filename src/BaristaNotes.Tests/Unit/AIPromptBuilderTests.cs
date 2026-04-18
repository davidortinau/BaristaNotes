using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BaristaNotes.Tests.Unit;

// Note: These tests verify prompt building logic that will be implemented in AIAdviceService.
// Since the service is in the MAUI project (not testable from pure .NET test project),
// we test the prompt building separately using a testable helper class.

/// <summary>
/// Tests for AI prompt building functionality.
/// </summary>
public class AIPromptBuilderTests
{
    [Fact]
    public void BuildPrompt_IncludesCurrentShotDose()
    {
        // Arrange
        var context = CreateBasicContext(doseIn: 18.5m);

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("18.5g in", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesCurrentShotYield()
    {
        // Arrange
        var context = CreateBasicContext(actualOutput: 38m);

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("38g out", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesCurrentShotTime()
    {
        // Arrange
        var context = CreateBasicContext(actualTime: 28m);

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("28s", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesGrindSetting()
    {
        // Arrange
        var context = CreateBasicContext(grindSetting: "15.5");

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Grind: 15.5", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesRating()
    {
        // Arrange
        var context = CreateBasicContext(rating: 3);

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Rating: 3/4", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesTastingNotes_WhenPresent()
    {
        // Arrange
        var context = CreateBasicContext(tastingNotes: "sour, thin, underextracted");

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Tasting notes: sour, thin, underextracted", prompt);
    }

    [Fact]
    public void BuildPrompt_OmitsTastingNotes_WhenEmpty()
    {
        // Arrange
        var context = CreateBasicContext(tastingNotes: null);

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.DoesNotContain("Tasting notes:", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesBeanName()
    {
        // Arrange
        var context = CreateBasicContext();
        context = context with { BeanInfo = context.BeanInfo with { Name = "Ethiopian Yirgacheffe" } };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Name: Ethiopian Yirgacheffe", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesRoaster()
    {
        // Arrange
        var context = CreateBasicContext();
        context = context with { BeanInfo = context.BeanInfo with { Roaster = "Counter Culture" } };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Roaster: Counter Culture", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesOrigin()
    {
        // Arrange
        var context = CreateBasicContext();
        context = context with { BeanInfo = context.BeanInfo with { Origin = "Ethiopia" } };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Origin: Ethiopia", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesDaysSinceRoast()
    {
        // Arrange
        var context = CreateBasicContext();
        context = context with { BeanInfo = context.BeanInfo with { DaysFromRoast = 14 } };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Days since roast: 14", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesBeanFlavorNotes()
    {
        // Arrange
        var context = CreateBasicContext();
        context = context with { BeanInfo = context.BeanInfo with { Notes = "Blueberry, citrus, floral" } };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Flavor notes: Blueberry, citrus, floral", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesMachineName_WhenEquipmentPresent()
    {
        // Arrange
        var context = CreateBasicContext();
        context = context with
        {
            Equipment = new EquipmentContextDto
            {
                MachineName = "Breville Barista Express"
            }
        };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Machine: Breville Barista Express", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesGrinderName_WhenEquipmentPresent()
    {
        // Arrange
        var context = CreateBasicContext();
        context = context with
        {
            Equipment = new EquipmentContextDto
            {
                GrinderName = "Niche Zero"
            }
        };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Grinder: Niche Zero", prompt);
    }

    [Fact]
    public void BuildPrompt_OmitsEquipmentSection_WhenNull()
    {
        // Arrange
        var context = CreateBasicContext();
        context = context with { Equipment = null };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.DoesNotContain("## Equipment", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesBestRatedShots_WhenAvailable()
    {
        // Arrange
        var context = CreateBasicContext();
        context = context with
        {
            HistoricalShots = new List<ShotContextDto>
            {
                new ShotContextDto
                {
                    DoseIn = 18m,
                    ActualOutput = 36m,
                    ActualTime = 28m,
                    Rating = 4,
                    Timestamp = DateTime.UtcNow.AddDays(-1)
                },
                new ShotContextDto
                {
                    DoseIn = 18m,
                    ActualOutput = 40m,
                    ActualTime = 32m,
                    Rating = 2,
                    Timestamp = DateTime.UtcNow.AddDays(-2)
                }
            }
        };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Best rated shots:", prompt);
        Assert.Contains("rated 4/4", prompt);
    }

    [Fact]
    public void BuildPrompt_IncludesRecentShots()
    {
        // Arrange
        var context = CreateBasicContext();
        context = context with
        {
            HistoricalShots = new List<ShotContextDto>
            {
                new ShotContextDto
                {
                    DoseIn = 18m,
                    ActualOutput = 36m,
                    ActualTime = 28m,
                    Rating = 3,
                    Timestamp = DateTime.UtcNow.AddDays(-1)
                }
            }
        };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("Most recent shots:", prompt);
    }

    [Fact]
    public void BuildPrompt_ContainsAllRequiredSections()
    {
        // Arrange
        var context = CreateFullContext();

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("## Current Shot", prompt);
        Assert.Contains("## Bean Information", prompt);
        Assert.Contains("## Equipment", prompt);
        Assert.Contains("## Previous Shots", prompt);
        Assert.Contains("what adjustments would you suggest", prompt);
    }

    [Fact]
    public void BuildPrompt_HandlesMinimalContext()
    {
        // Arrange - only required fields
        var context = new AIAdviceRequestDto
        {
            ShotId = 1,
            CurrentShot = new ShotContextDto { DoseIn = 18m },
            BeanInfo = new BeanContextDto { Name = "Test Bean" }
        };

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.NotEmpty(prompt);
        Assert.Contains("18g in", prompt);
        Assert.Contains("Test Bean", prompt);
    }

    private AIAdviceRequestDto CreateBasicContext(
        decimal doseIn = 18m,
        decimal? actualOutput = null,
        decimal? actualTime = null,
        string grindSetting = "",
        int? rating = null,
        string? tastingNotes = null)
    {
        return new AIAdviceRequestDto
        {
            ShotId = 1,
            CurrentShot = new ShotContextDto
            {
                DoseIn = doseIn,
                ActualOutput = actualOutput,
                ActualTime = actualTime,
                GrindSetting = grindSetting,
                Rating = rating,
                TastingNotes = tastingNotes,
                Timestamp = DateTime.UtcNow
            },
            BeanInfo = new BeanContextDto
            {
                Name = "Test Bean",
                RoastDate = DateTime.UtcNow.AddDays(-7),
                DaysFromRoast = 7
            }
        };
    }

    private AIAdviceRequestDto CreateFullContext()
    {
        return new AIAdviceRequestDto
        {
            ShotId = 1,
            CurrentShot = new ShotContextDto
            {
                DoseIn = 18.5m,
                ActualOutput = 38m,
                ActualTime = 28m,
                GrindSetting = "15",
                Rating = 2,
                TastingNotes = "sour, thin",
                Timestamp = DateTime.UtcNow
            },
            BeanInfo = new BeanContextDto
            {
                Name = "Ethiopian Natural",
                Roaster = "Local Roaster",
                Origin = "Ethiopia",
                RoastDate = DateTime.UtcNow.AddDays(-10),
                DaysFromRoast = 10,
                Notes = "Blueberry, wine-like"
            },
            Equipment = new EquipmentContextDto
            {
                MachineName = "Gaggia Classic",
                GrinderName = "Eureka Mignon"
            },
            HistoricalShots = new List<ShotContextDto>
            {
                new ShotContextDto
                {
                    DoseIn = 18m,
                    ActualOutput = 36m,
                    ActualTime = 30m,
                    Rating = 4,
                    Timestamp = DateTime.UtcNow.AddDays(-1)
                },
                new ShotContextDto
                {
                    DoseIn = 18m,
                    ActualOutput = 34m,
                    ActualTime = 26m,
                    Rating = 3,
                    Timestamp = DateTime.UtcNow.AddDays(-2)
                }
            }
        };
    }
}
