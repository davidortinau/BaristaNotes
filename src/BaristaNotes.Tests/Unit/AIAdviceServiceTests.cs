using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services;
using Microsoft.Extensions.Configuration;
using Moq;

namespace BaristaNotes.Tests.Unit;

/// <summary>
/// Tests for AIPromptBuilder and AIAdviceResponseDto functionality.
/// Note: Full AIAdviceService tests require MAUI project reference.
/// These tests focus on the testable Core layer components.
/// </summary>
public class AIAdviceServiceTests
{
    #region AIPromptBuilder Tests

    [Fact]
    public void AIPromptBuilder_BuildPrompt_CreatesValidPrompt()
    {
        // Arrange
        var context = CreateBasicContext();

        // Act
        var prompt = AIPromptBuilder.BuildPrompt(context);

        // Assert
        Assert.NotEmpty(prompt);
        Assert.Contains("Current Shot", prompt);
        Assert.Contains("Bean Information", prompt);
    }

    [Fact]
    public void AIPromptBuilder_BuildPassivePrompt_CreatesShortPrompt()
    {
        // Arrange
        var context = CreateContextWithHistory();

        // Act
        var prompt = AIPromptBuilder.BuildPassivePrompt(context);

        // Assert
        Assert.NotEmpty(prompt);
        Assert.Contains("Quick tip?", prompt);
    }

    [Fact]
    public void AIPromptBuilder_BuildPassivePrompt_IncludesBestShotComparison()
    {
        // Arrange
        var context = CreateContextWithHistory();

        // Act
        var prompt = AIPromptBuilder.BuildPassivePrompt(context);

        // Assert
        Assert.Contains("Best shot was:", prompt);
    }

    #endregion

    #region AIAdviceResponseDto Tests

    [Fact]
    public void AIAdviceResponseDto_HasCorrectErrorFormat_WhenFailed()
    {
        // Arrange
        var response = new AIAdviceResponseDto
        {
            Success = false,
            ErrorMessage = "Test error"
        };

        // Assert
        Assert.False(response.Success);
        Assert.Null(response.Advice);
        Assert.Equal("Test error", response.ErrorMessage);
    }

    [Fact]
    public void AIAdviceResponseDto_HasCorrectFormat_WhenSuccessful()
    {
        // Arrange
        var response = new AIAdviceResponseDto
        {
            Success = true,
            Advice = "Try grinding finer"
        };

        // Assert
        Assert.True(response.Success);
        Assert.Equal("Try grinding finer", response.Advice);
        Assert.Null(response.ErrorMessage);
    }

    [Fact]
    public void AIAdviceResponseDto_SetsGeneratedAt()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var response = new AIAdviceResponseDto { Success = true };

        var after = DateTime.UtcNow;

        // Assert
        Assert.True(response.GeneratedAt >= before);
        Assert.True(response.GeneratedAt <= after);
    }

    #endregion

    #region DTO Tests

    [Fact]
    public void ShotContextDto_CanBeCreated_WithDefaultValues()
    {
        // Act
        var dto = new ShotContextDto { DoseIn = 18m };

        // Assert
        Assert.Equal(18m, dto.DoseIn);
        Assert.Null(dto.ActualOutput);
        Assert.Null(dto.ActualTime);
        Assert.Null(dto.Rating);
        Assert.Null(dto.TastingNotes);
    }

    [Fact]
    public void BeanContextDto_CanBeCreated_WithAllFields()
    {
        // Act
        var dto = new BeanContextDto
        {
            Name = "Test Bean",
            Roaster = "Test Roaster",
            Origin = "Ethiopia",
            RoastDate = DateTime.UtcNow.AddDays(-7),
            DaysFromRoast = 7,
            Notes = "Fruity, floral"
        };

        // Assert
        Assert.Equal("Test Bean", dto.Name);
        Assert.Equal("Test Roaster", dto.Roaster);
        Assert.Equal("Ethiopia", dto.Origin);
        Assert.Equal(7, dto.DaysFromRoast);
        Assert.Equal("Fruity, floral", dto.Notes);
    }

    [Fact]
    public void EquipmentContextDto_CanBeCreated()
    {
        // Act
        var dto = new EquipmentContextDto
        {
            MachineName = "Gaggia Classic",
            GrinderName = "Niche Zero"
        };

        // Assert
        Assert.Equal("Gaggia Classic", dto.MachineName);
        Assert.Equal("Niche Zero", dto.GrinderName);
    }

    [Fact]
    public void AIAdviceRequestDto_CanBeCreated_WithMinimalData()
    {
        // Act
        var dto = new AIAdviceRequestDto
        {
            ShotId = 1,
            CurrentShot = new ShotContextDto { DoseIn = 18m },
            BeanInfo = new BeanContextDto { Name = "Test Bean" }
        };

        // Assert
        Assert.Equal(1, dto.ShotId);
        Assert.NotNull(dto.CurrentShot);
        Assert.NotNull(dto.BeanInfo);
        Assert.Empty(dto.HistoricalShots);
        Assert.Null(dto.Equipment);
    }

    #endregion

    #region Helper Methods

    private AIAdviceRequestDto CreateBasicContext()
    {
        return new AIAdviceRequestDto
        {
            ShotId = 1,
            CurrentShot = new ShotContextDto
            {
                DoseIn = 18m,
                ActualOutput = 36m,
                ActualTime = 28m,
                GrindSetting = "15",
                Rating = 2,
                Timestamp = DateTime.UtcNow
            },
            BeanInfo = new BeanContextDto
            {
                Name = "Test Bean",
                DaysFromRoast = 7
            }
        };
    }

    private AIAdviceRequestDto CreateContextWithHistory()
    {
        return new AIAdviceRequestDto
        {
            ShotId = 1,
            CurrentShot = new ShotContextDto
            {
                DoseIn = 18m,
                ActualOutput = 36m,
                ActualTime = 28m,
                Timestamp = DateTime.UtcNow
            },
            BeanInfo = new BeanContextDto
            {
                Name = "Test Bean",
                DaysFromRoast = 7
            },
            HistoricalShots = new List<ShotContextDto>
            {
                new ShotContextDto
                {
                    DoseIn = 18m,
                    ActualOutput = 38m,
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

    #endregion

    #region AIPromptBuilder Bean Recommendation Tests (T013-T014, T024-T025)

    [Fact]
    public void AIPromptBuilder_BuildNewBeanPrompt_CreatesValidPrompt()
    {
        // Arrange - T013
        var context = new BeanRecommendationContextDto
        {
            BeanId = 1,
            BeanName = "Ethiopia Yirgacheffe",
            Roaster = "Local Roaster",
            Origin = "Ethiopia",
            Notes = "Fruity, floral, bright acidity",
            RoastDate = DateTime.Now.AddDays(-10),
            DaysFromRoast = 10,
            HasHistory = false
        };

        // Act
        var prompt = AIPromptBuilder.BuildNewBeanPrompt(context);

        // Assert
        Assert.NotEmpty(prompt);
        Assert.Contains("Bean Information", prompt);
        Assert.Contains("Ethiopia Yirgacheffe", prompt);
        Assert.Contains("Local Roaster", prompt);
        Assert.Contains("Ethiopia", prompt);
        Assert.Contains("Fruity, floral", prompt);
        Assert.Contains("no previous shots", prompt.ToLower());
        Assert.Contains("JSON", prompt);
        Assert.Contains("dose", prompt);
        Assert.Contains("grind", prompt);
        Assert.Contains("output", prompt);
        Assert.Contains("duration", prompt);
    }

    [Fact]
    public void AIPromptBuilder_BuildReturningBeanPrompt_IncludesHistoricalShots()
    {
        // Arrange - T024
        var context = new BeanRecommendationContextDto
        {
            BeanId = 1,
            BeanName = "Colombia Supremo",
            Roaster = "Test Roaster",
            Origin = "Colombia",
            DaysFromRoast = 14,
            HasHistory = true,
            HistoricalShots = new List<ShotContextDto>
            {
                new ShotContextDto { DoseIn = 18m, ActualOutput = 36m, ActualTime = 28m, GrindSetting = "15", Rating = 4, Timestamp = DateTime.UtcNow.AddDays(-1) },
                new ShotContextDto { DoseIn = 18m, ActualOutput = 38m, ActualTime = 30m, GrindSetting = "14", Rating = 3, Timestamp = DateTime.UtcNow.AddDays(-2) }
            }
        };

        // Act
        var prompt = AIPromptBuilder.BuildReturningBeanPrompt(context);

        // Assert
        Assert.NotEmpty(prompt);
        Assert.Contains("Bean Information", prompt);
        Assert.Contains("Colombia Supremo", prompt);
        Assert.Contains("Previous Shots", prompt);
        Assert.Contains("18g in", prompt);
        Assert.Contains("36g out", prompt);
        Assert.Contains("rated 4/4", prompt);
        Assert.Contains("JSON", prompt);
    }

    [Fact]
    public void AIPromptBuilder_BuildNewBeanPrompt_IncludesEquipmentWhenProvided()
    {
        // Arrange
        var context = new BeanRecommendationContextDto
        {
            BeanId = 1,
            BeanName = "Test Bean",
            HasHistory = false,
            Equipment = new EquipmentContextDto
            {
                MachineName = "Gaggia Classic Pro",
                GrinderName = "Niche Zero"
            }
        };

        // Act
        var prompt = AIPromptBuilder.BuildNewBeanPrompt(context);

        // Assert
        Assert.Contains("Equipment", prompt);
        Assert.Contains("Gaggia Classic Pro", prompt);
        Assert.Contains("Niche Zero", prompt);
    }

    #endregion

    #region AIRecommendationDto Tests (T014, T025)

    [Fact]
    public void AIRecommendationDto_NewBeanType_HasCorrectFormat()
    {
        // Arrange - T014: Verify new bean recommendation has all four parameters
        var dto = new AIRecommendationDto
        {
            Success = true,
            Dose = 18m,
            GrindSetting = "15",
            Output = 36m,
            Duration = 28m,
            RecommendationType = RecommendationType.NewBean,
            Source = "via OpenAI"
        };

        // Assert
        Assert.True(dto.Success);
        Assert.Equal(18m, dto.Dose);
        Assert.Equal("15", dto.GrindSetting);
        Assert.Equal(36m, dto.Output);
        Assert.Equal(28m, dto.Duration);
        Assert.Equal(RecommendationType.NewBean, dto.RecommendationType);
    }

    [Fact]
    public void AIRecommendationDto_ReturningBeanType_HasCorrectFormat()
    {
        // Arrange - T025: Verify returning bean recommendation has all four parameters
        var dto = new AIRecommendationDto
        {
            Success = true,
            Dose = 19m,
            GrindSetting = "14",
            Output = 38m,
            Duration = 30m,
            RecommendationType = RecommendationType.ReturningBean,
            Source = "via Apple Intelligence"
        };

        // Assert
        Assert.True(dto.Success);
        Assert.Equal(19m, dto.Dose);
        Assert.Equal("14", dto.GrindSetting);
        Assert.Equal(38m, dto.Output);
        Assert.Equal(30m, dto.Duration);
        Assert.Equal(RecommendationType.ReturningBean, dto.RecommendationType);
    }

    [Fact]
    public void AIRecommendationDto_FailedRequest_HasErrorMessage()
    {
        // Arrange
        var dto = new AIRecommendationDto
        {
            Success = false,
            ErrorMessage = "AI service unavailable"
        };

        // Assert
        Assert.False(dto.Success);
        Assert.Equal("AI service unavailable", dto.ErrorMessage);
        Assert.Equal(0m, dto.Dose); // Default values
    }

    [Fact]
    public void AIRecommendationDto_CanIncludeConfidenceIndicator()
    {
        // Arrange
        var dto = new AIRecommendationDto
        {
            Success = true,
            Dose = 18m,
            GrindSetting = "medium",
            Output = 36m,
            Duration = 30m,
            RecommendationType = RecommendationType.ReturningBean,
            Confidence = "High confidence based on 8 previous shots"
        };

        // Assert
        Assert.Equal("High confidence based on 8 previous shots", dto.Confidence);
    }

    #endregion

    #region RecommendationType Enum Tests

    [Fact]
    public void RecommendationType_NewBean_HasCorrectValue()
    {
        // Assert
        Assert.Equal(0, (int)RecommendationType.NewBean);
    }

    [Fact]
    public void RecommendationType_ReturningBean_HasCorrectValue()
    {
        // Assert
        Assert.Equal(1, (int)RecommendationType.ReturningBean);
    }

    #endregion

    #region BeanRecommendationContextDto Tests

    [Fact]
    public void BeanRecommendationContextDto_CanBeCreated_WithAllFields()
    {
        // Arrange & Act
        var dto = new BeanRecommendationContextDto
        {
            BeanId = 1,
            BeanName = "Test Bean",
            Roaster = "Test Roaster",
            Origin = "Ethiopia",
            Notes = "Fruity notes",
            RoastDate = DateTime.UtcNow.AddDays(-7),
            DaysFromRoast = 7,
            HasHistory = true,
            HistoricalShots = new List<ShotContextDto>
            {
                new ShotContextDto { DoseIn = 18m, Rating = 4 }
            },
            Equipment = new EquipmentContextDto { MachineName = "Test Machine" }
        };

        // Assert
        Assert.Equal(1, dto.BeanId);
        Assert.Equal("Test Bean", dto.BeanName);
        Assert.Equal("Test Roaster", dto.Roaster);
        Assert.Equal("Ethiopia", dto.Origin);
        Assert.True(dto.HasHistory);
        Assert.Single(dto.HistoricalShots!);
        Assert.NotNull(dto.Equipment);
    }

    [Fact]
    public void BeanRecommendationContextDto_CanBeCreated_WithMinimalData()
    {
        // Arrange & Act
        var dto = new BeanRecommendationContextDto
        {
            BeanId = 1,
            BeanName = "Minimal Bean",
            HasHistory = false
        };

        // Assert
        Assert.Equal(1, dto.BeanId);
        Assert.Equal("Minimal Bean", dto.BeanName);
        Assert.False(dto.HasHistory);
        Assert.Null(dto.HistoricalShots);
        Assert.Null(dto.Roaster);
        Assert.Null(dto.Equipment);
    }

    #endregion
}
