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
    public void BuildPrompt_IncludesGrindMicrons()
    {
        // Arrange
        var context = CreateBasicContext(grindMicrons: 270);

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.Contains("270µm", prompt);
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

    [Fact]
    public void BuildPrompt_IncludesMadeForPersona_WhenContextPresent()
    {
        var context = CreateBasicContext() with
        {
            MadeFor = new UserProfileDto
            {
                Id = 7,
                Name = "Angie",
                Context = "Prefers single-origin pour overs in the morning. Sensitive to bitter notes."
            }
        };

        var prompt = AIPromptBuilder.BuildPrompt(context);

        Assert.Contains("Made For: Angie", prompt);
        Assert.Contains("single-origin pour overs", prompt);
        Assert.Contains("Persona context", prompt);
    }

    [Fact]
    public void BuildPrompt_NotesMadeForPersona_WhenContextEmpty()
    {
        var context = CreateBasicContext() with
        {
            MadeFor = new UserProfileDto
            {
                Id = 7,
                Name = "Angie",
                Context = null
            }
        };

        var prompt = AIPromptBuilder.BuildPrompt(context);

        Assert.Contains("Made For: Angie", prompt);
        Assert.Contains("No persona preferences recorded yet", prompt);
    }

    [Fact]
    public void BuildPrompt_OmitsMadeForSection_WhenNull()
    {
        var context = CreateBasicContext();

        var prompt = AIPromptBuilder.BuildPrompt(context);

        Assert.DoesNotContain("Made For:", prompt);
        Assert.DoesNotContain("Persona context", prompt);
    }

    private AIAdviceRequestDto CreateBasicContext(
        decimal doseIn = 18m,
        decimal? actualOutput = null,
        decimal? actualTime = null,
        int? grindMicrons = null,
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
                GrindMicrons = grindMicrons,
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
                GrindMicrons = 270,
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

    // --- Brew-method-aware prompt tests ------------------------------------
    // These tests guard the rule that AI advice must be grounded in the
    // selected brew method. Espresso-only assumptions (1:2 ratio, 25–35s)
    // must NOT leak into pour over, French press, or cold brew prompts.

    [Theory]
    [InlineData(BaristaNotes.Core.Models.Enums.BrewMethod.Espresso, "Espresso")]
    [InlineData(BaristaNotes.Core.Models.Enums.BrewMethod.PourOver, "Pour Over")]
    [InlineData(BaristaNotes.Core.Models.Enums.BrewMethod.FrenchPress, "French Press")]
    [InlineData(BaristaNotes.Core.Models.Enums.BrewMethod.ColdBrew, "Cold Brew")]
    public void BuildPrompt_NamesBrewMethodInClosingQuestion(
        BaristaNotes.Core.Models.Enums.BrewMethod method,
        string expectedDisplay)
    {
        var context = CreateBasicContext();
        context = context with
        {
            CurrentShot = context.CurrentShot with { BrewMethod = method }
        };

        var prompt = AIPromptBuilder.BuildPrompt(context);

        Assert.Contains(expectedDisplay, prompt);
        Assert.Contains("Brew method:", prompt);
    }

    [Fact]
    public void BuildAdviceSystemPrompt_Espresso_MentionsExtractionRatioAndShotTime()
    {
        var prompt = AIPromptBuilder.BuildAdviceSystemPrompt(
            BaristaNotes.Core.Models.Enums.BrewMethod.Espresso);

        Assert.Contains("espresso", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("1:2", prompt);
        Assert.Contains("25", prompt);
    }

    [Fact]
    public void BuildAdviceSystemPrompt_FrenchPress_DoesNotMentionEspressoRatios()
    {
        var prompt = AIPromptBuilder.BuildAdviceSystemPrompt(
            BaristaNotes.Core.Models.Enums.BrewMethod.FrenchPress);

        Assert.Contains("French Press", prompt);
        Assert.Contains("steep", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("coarse", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1:2 to 1:2.5", prompt);
        Assert.DoesNotContain("25–35s", prompt);
    }

    [Fact]
    public void BuildAdviceSystemPrompt_PourOver_MentionsBloomAndPour()
    {
        var prompt = AIPromptBuilder.BuildAdviceSystemPrompt(
            BaristaNotes.Core.Models.Enums.BrewMethod.PourOver);

        Assert.Contains("pour", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("bloom", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1:2 to 1:2.5", prompt);
    }

    [Fact]
    public void BuildAdviceSystemPrompt_ColdBrew_MentionsSteepHoursAndCoarseGrind()
    {
        var prompt = AIPromptBuilder.BuildAdviceSystemPrompt(
            BaristaNotes.Core.Models.Enums.BrewMethod.ColdBrew);

        Assert.Contains("Cold Brew", prompt);
        Assert.Contains("h", prompt); // hours appear in the range
        Assert.Contains("coarse", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("25–35s", prompt);
    }

    [Fact]
    public void BuildAdviceSystemPrompt_AlwaysPinsBrewMethodAndWarnsAgainstCrossMethodAssumptions()
    {
        foreach (BaristaNotes.Core.Models.Enums.BrewMethod method in
            Enum.GetValues(typeof(BaristaNotes.Core.Models.Enums.BrewMethod)))
        {
            var prompt = AIPromptBuilder.BuildAdviceSystemPrompt(method);
            Assert.Contains(BaristaNotes.Core.Models.Enums.BrewMethodExtensions.DisplayName(method), prompt);
            Assert.Contains("do NOT apply assumptions from other brewing methods", prompt);
        }
    }

    [Fact]
    public void BuildPassiveAdviceSystemPrompt_NamesTheBrewMethod()
    {
        var prompt = AIPromptBuilder.BuildPassiveAdviceSystemPrompt(
            BaristaNotes.Core.Models.Enums.BrewMethod.Aeropress);

        Assert.Contains("Aeropress", prompt);
    }

    [Fact]
    public void BuildRecommendationSystemPrompt_NamesTheBrewMethodAndForbidsEspressoForOthers()
    {
        var prompt = AIPromptBuilder.BuildRecommendationSystemPrompt(
            BaristaNotes.Core.Models.Enums.BrewMethod.V60);

        Assert.Contains("V60", prompt);
        Assert.Contains("do NOT return espresso-style parameters", prompt);
    }

    [Fact]
    public void BuildNewBeanPrompt_FrenchPress_UsesFrenchPressRangesInJsonHints()
    {
        var context = new BeanRecommendationContextDto
        {
            BeanId = 1,
            BeanName = "Test Bean",
            HasHistory = false
        };

        var prompt = AIPromptBuilder.BuildNewBeanPrompt(
            context,
            BaristaNotes.Core.Models.Enums.BrewMethod.FrenchPress);

        Assert.Contains("French Press", prompt);
        // French press dose default range is much larger than espresso's 18–20
        Assert.Contains("French Press range 10", prompt);
        // Espresso-only suggested ranges must NOT leak in
        Assert.DoesNotContain("typically 18-20", prompt);
        Assert.DoesNotContain("typically 36-50", prompt);
        Assert.DoesNotContain("typically 25-35", prompt);
    }

    [Fact]
    public void BuildReturningBeanPrompt_PourOver_TaggesHistoricalShotsWithTheirMethod()
    {
        var context = new BeanRecommendationContextDto
        {
            BeanId = 1,
            BeanName = "Test Bean",
            HasHistory = true,
            HistoricalShots = new List<ShotContextDto>
            {
                new ShotContextDto
                {
                    BrewMethod = BaristaNotes.Core.Models.Enums.BrewMethod.PourOver,
                    DoseIn = 20m,
                    ActualOutput = 320m,
                    ActualTime = 210m,
                    Rating = 4,
                    Timestamp = DateTime.UtcNow.AddDays(-1)
                }
            }
        };

        var prompt = AIPromptBuilder.BuildReturningBeanPrompt(
            context,
            BaristaNotes.Core.Models.Enums.BrewMethod.PourOver);

        Assert.Contains("Pour Over", prompt);
        Assert.Contains("Pour Over range", prompt);
        Assert.Contains("Pour Over, 20g in", prompt);
    }
}
