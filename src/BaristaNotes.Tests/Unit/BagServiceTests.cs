using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using BaristaNotes.Core.Data.Repositories;
using Moq;
using Xunit;

namespace BaristaNotes.Tests.Unit;

/// <summary>
/// Unit tests for BagService (T030).
/// Tests bag CRUD operations, validation, and completion management.
/// </summary>
public class BagServiceTests
{
    private readonly Mock<IBagRepository> _mockBagRepo;
    private readonly Mock<IBeanRepository> _mockBeanRepo;
    private readonly BagService _service;

    public BagServiceTests()
    {
        _mockBagRepo = new Mock<IBagRepository>();
        _mockBeanRepo = new Mock<IBeanRepository>();
        _service = new BagService(_mockBagRepo.Object, _mockBeanRepo.Object);
    }

    #region CreateBagAsync Tests

    [Fact]
    public async Task CreateBagAsync_ValidBag_Success()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Test Bean" };
        var bag = new Bag
        {
            BeanId = 1,
            RoastDate = DateTime.Now.AddDays(-1),
            Notes = "Test notes"
        };

        _mockBeanRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(bean);
        _mockBagRepo.Setup(x => x.CreateAsync(It.IsAny<Bag>())).ReturnsAsync(bag);

        // Act
        var result = await _service.CreateBagAsync(bag);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        _mockBagRepo.Verify(x => x.CreateAsync(It.IsAny<Bag>()), Times.Once);
    }

    [Fact]
    public async Task CreateBagAsync_NonExistentBean_ThrowsValidationException()
    {
        // Arrange
        var bag = new Bag { BeanId = 999, RoastDate = DateTime.Now };
        _mockBeanRepo.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Bean?)null);

        // Act
        var result = await _service.CreateBagAsync(bag);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Bean", result.ErrorMessage);
        _mockBagRepo.Verify(x => x.CreateAsync(It.IsAny<Bag>()), Times.Never);
    }

    [Fact]
    public async Task CreateBagAsync_FutureRoastDate_ThrowsValidationException()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Test Bean" };
        var bag = new Bag
        {
            BeanId = 1,
            RoastDate = DateTime.Now.AddDays(1) // Future date
        };

        _mockBeanRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(bean);

        // Act
        var result = await _service.CreateBagAsync(bag);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("future", result.ErrorMessage.ToLower());
        _mockBagRepo.Verify(x => x.CreateAsync(It.IsAny<Bag>()), Times.Never);
    }

    [Fact]
    public async Task CreateBagAsync_NotesTooLong_ThrowsValidationException()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Test Bean" };
        var bag = new Bag
        {
            BeanId = 1,
            RoastDate = DateTime.Now,
            Notes = new string('x', 501) // >500 characters
        };

        _mockBeanRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(bean);

        // Act
        var result = await _service.CreateBagAsync(bag);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("500", result.ErrorMessage);
        _mockBagRepo.Verify(x => x.CreateAsync(It.IsAny<Bag>()), Times.Never);
    }

    #endregion

    #region GetActiveBagsForShotLoggingAsync Tests

    [Fact]
    public async Task GetActiveBagsForShotLoggingAsync_ReturnsOnlyIncompleteBags()
    {
        // Arrange
        var activeBags = new List<BagSummaryDto>
        {
            new() { Id = 1, BeanId = 1, BeanName = "Bean 1", IsComplete = false, RoastDate = DateTime.Now.AddDays(-1) },
            new() { Id = 2, BeanId = 2, BeanName = "Bean 2", IsComplete = false, RoastDate = DateTime.Now.AddDays(-2) }
        };

        _mockBagRepo.Setup(x => x.GetActiveBagsForShotLoggingAsync())
            .ReturnsAsync(activeBags);

        // Act
        var result = await _service.GetActiveBagsForShotLoggingAsync();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, bag => Assert.False(bag.IsComplete));
    }

    [Fact]
    public async Task GetActiveBagsForShotLoggingAsync_OrderedByRoastDateDesc()
    {
        // Arrange - Repository returns sorted data
        var activeBags = new List<BagSummaryDto>
        {
            new() { Id = 2, BeanId = 2, BeanName = "Bean 2", RoastDate = DateTime.Now.AddDays(-1) }, // Newest
            new() { Id = 3, BeanId = 3, BeanName = "Bean 3", RoastDate = DateTime.Now.AddDays(-3) },
            new() { Id = 1, BeanId = 1, BeanName = "Bean 1", RoastDate = DateTime.Now.AddDays(-5) }  // Oldest
        };

        _mockBagRepo.Setup(x => x.GetActiveBagsForShotLoggingAsync())
            .ReturnsAsync(activeBags);

        // Act
        var result = await _service.GetActiveBagsForShotLoggingAsync();

        // Assert
        Assert.Equal(3, result.Count);
        // Verify descending order (newest first)
        for (int i = 0; i < result.Count - 1; i++)
        {
            Assert.True(result[i].RoastDate >= result[i + 1].RoastDate, 
                $"Bag at index {i} (RoastDate: {result[i].RoastDate}) should be >= bag at index {i+1} (RoastDate: {result[i+1].RoastDate})");
        }
    }

    [Fact]
    public async Task GetActiveBagsForShotLoggingAsync_EmptyList_WhenNoBags()
    {
        // Arrange
        _mockBagRepo.Setup(x => x.GetActiveBagsForShotLoggingAsync())
            .ReturnsAsync(new List<BagSummaryDto>());

        // Act
        var result = await _service.GetActiveBagsForShotLoggingAsync();

        // Assert
        Assert.Empty(result);
    }

    #endregion

    #region MarkBagCompleteAsync Tests

    [Fact]
    public async Task MarkBagCompleteAsync_ValidBag_SetsIsCompleteTrue()
    {
        // Arrange
        var bag = new Bag
        {
            Id = 1,
            BeanId = 1,
            RoastDate = DateTime.Now,
            IsComplete = false
        };

        _mockBagRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(bag);
        _mockBagRepo.Setup(x => x.UpdateAsync(It.IsAny<Bag>())).ReturnsAsync(bag);

        // Act
        await _service.MarkBagCompleteAsync(1);

        // Assert
        _mockBagRepo.Verify(x => x.UpdateAsync(It.Is<Bag>(b => b.IsComplete == true)), Times.Once);
    }

    [Fact]
    public async Task MarkBagCompleteAsync_NonExistentBag_ThrowsException()
    {
        // Arrange
        _mockBagRepo.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Bag?)null);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(
            async () => await _service.MarkBagCompleteAsync(999));
        
        _mockBagRepo.Verify(x => x.UpdateAsync(It.IsAny<Bag>()), Times.Never);
    }

    #endregion

    #region ReactivateBagAsync Tests

    [Fact]
    public async Task ReactivateBagAsync_CompletedBag_SetsIsCompleteFalse()
    {
        // Arrange
        var bag = new Bag
        {
            Id = 1,
            BeanId = 1,
            RoastDate = DateTime.Now,
            IsComplete = true
        };

        _mockBagRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(bag);
        _mockBagRepo.Setup(x => x.UpdateAsync(It.IsAny<Bag>())).ReturnsAsync(bag);

        // Act
        await _service.ReactivateBagAsync(1);

        // Assert
        _mockBagRepo.Verify(x => x.UpdateAsync(It.Is<Bag>(b => b.IsComplete == false)), Times.Once);
    }

    [Fact]
    public async Task ReactivateBagAsync_NonExistentBag_ThrowsException()
    {
        // Arrange
        _mockBagRepo.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Bag?)null);

        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(
            async () => await _service.ReactivateBagAsync(999));
        
        _mockBagRepo.Verify(x => x.UpdateAsync(It.IsAny<Bag>()), Times.Never);
    }

    #endregion

    #region CreateNewBagForBeanAsync Tests

    [Fact]
    public async Task CreateNewBagForBeanAsync_returns_bag_summary_for_valid_input()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Ethiopian Yirgacheffe", IsActive = true, IsDeleted = false };
        var roastDate = DateTime.Today.AddDays(-2);

        _mockBeanRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(bean);
        _mockBagRepo
            .Setup(x => x.CreateAsync(It.IsAny<Bag>()))
            .ReturnsAsync((Bag b) => { b.Id = 42; return b; });

        // Act
        var result = await _service.CreateNewBagForBeanAsync(1, roastDate, "  From Trader Joe's  ");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(42, result.Data!.Id);
        Assert.Equal(1, result.Data.BeanId);
        Assert.Equal("Ethiopian Yirgacheffe", result.Data.BeanName);
        Assert.Equal(roastDate, result.Data.RoastDate);
        Assert.Equal("  From Trader Joe's  ", result.Data.Notes);
        Assert.False(result.Data.IsComplete);
        Assert.Equal(0, result.Data.ShotCount);
        Assert.Null(result.Data.AverageRating);
        _mockBagRepo.Verify(x => x.CreateAsync(It.IsAny<Bag>()), Times.Once);
    }

    [Fact]
    public async Task CreateNewBagForBeanAsync_fails_when_bean_not_found()
    {
        // Arrange
        _mockBeanRepo.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((Bean?)null);

        // Act
        var result = await _service.CreateNewBagForBeanAsync(999, DateTime.Today);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Bean not found", result.ErrorMessage);
        _mockBagRepo.Verify(x => x.CreateAsync(It.IsAny<Bag>()), Times.Never);
    }

    [Fact]
    public async Task CreateNewBagForBeanAsync_fails_when_bean_is_deleted()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Deleted Bean", IsActive = true, IsDeleted = true };
        _mockBeanRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(bean);

        // Act
        var result = await _service.CreateNewBagForBeanAsync(1, DateTime.Today);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Bean not found", result.ErrorMessage);
        _mockBagRepo.Verify(x => x.CreateAsync(It.IsAny<Bag>()), Times.Never);
    }

    [Fact]
    public async Task CreateNewBagForBeanAsync_fails_when_roastDate_in_future()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Test Bean", IsActive = true, IsDeleted = false };
        _mockBeanRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(bean);

        // Act
        var result = await _service.CreateNewBagForBeanAsync(1, DateTime.Today.AddDays(1));

        // Assert
        Assert.False(result.Success);
        Assert.Contains("future", result.ErrorMessage.ToLower());
        _mockBagRepo.Verify(x => x.CreateAsync(It.IsAny<Bag>()), Times.Never);
    }

    [Fact]
    public async Task CreateNewBagForBeanAsync_defaults_isComplete_false_isActive_true()
    {
        // Arrange
        var bean = new Bean { Id = 1, Name = "Test Bean", IsActive = true, IsDeleted = false };
        Bag? captured = null;

        _mockBeanRepo.Setup(x => x.GetByIdAsync(1)).ReturnsAsync(bean);
        _mockBagRepo
            .Setup(x => x.CreateAsync(It.IsAny<Bag>()))
            .Callback<Bag>(b => captured = b)
            .ReturnsAsync((Bag b) => { b.Id = 7; return b; });

        // Act
        var result = await _service.CreateNewBagForBeanAsync(1, DateTime.Today.AddDays(-1));

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(captured);
        Assert.False(captured!.IsComplete);
        Assert.True(captured.IsActive);
        Assert.False(captured.IsDeleted);
        Assert.False(result.Data!.IsComplete);
    }

    #endregion
}
