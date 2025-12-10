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
}
