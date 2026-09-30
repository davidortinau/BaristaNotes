using BaristaNotes.Core.Data.Repositories;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Grind;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BaristaNotes.Tests.Unit;

public sealed class GrindPickerWorkflowTests
{
    private readonly Mock<IBagRepository> _bags = new(MockBehavior.Strict);
    private readonly Mock<IShotRecordRepository> _shots = new(MockBehavior.Strict);
    private readonly Mock<IGrinderProfileRepository> _profiles = new(MockBehavior.Strict);
    private readonly Mock<IDrinkValueRangeService> _ranges = new(MockBehavior.Strict);

    public GrindPickerWorkflowTests()
    {
        _ranges.Setup(service => service.Resolve(DrinkValueMetric.GrindMicrons, BrewMethod.Espresso))
            .Returns(new EffectiveDrinkValueRange(
                new(280, 380), new(40, 1500), 280, 5, "um", ValueRangeSource.Custom));
    }

    private GrindPickerWorkflow CreateWorkflow() =>
        new(_bags.Object, _shots.Object, _profiles.Object, _ranges.Object,
            NullLogger<GrindPickerWorkflow>.Instance);

    [Fact]
    public async Task ExistingMicrons_SkipHistoryWithoutMutatingTheDraft()
    {
        var draft = new DrinkDraft { GrindMicrons = 800, SelectedBagId = 1 };

        var result = await CreateWorkflow().LoadAsync(draft);

        Assert.Equal(800, result.Microns);
        Assert.Null(result.Anchors);
        Assert.False(result.IsUncalibrated);
        Assert.Equal(800, draft.GrindMicrons);
        _bags.VerifyNoOtherCalls();
        _shots.VerifyNoOtherCalls();
        _profiles.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BeanMethodHistory_TakesPrecedenceOverCustomDefault()
    {
        _bags.Setup(repo => repo.GetByIdAsync(5)).ReturnsAsync(new Bag { Id = 5, BeanId = 3 });
        _shots.Setup(repo => repo.GetMostRecentMicronsByBeanAsync(3, BrewMethod.Espresso))
            .ReturnsAsync(600);
        var draft = new DrinkDraft { SelectedBagId = 5 };

        var result = await CreateWorkflow().LoadAsync(draft);

        Assert.Equal(600, result.Microns);
        Assert.Null(draft.GrindMicrons);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingBagOrHistory_UsesEffectiveCustomDefault(bool bagExists)
    {
        _bags.Setup(repo => repo.GetByIdAsync(5))
            .ReturnsAsync(bagExists ? new Bag { Id = 5, BeanId = 3 } : null);
        if (bagExists)
        {
            _shots.Setup(repo => repo.GetMostRecentMicronsByBeanAsync(3, BrewMethod.Espresso))
                .ReturnsAsync((int?)null);
        }

        var result = await CreateWorkflow().LoadAsync(new DrinkDraft { SelectedBagId = 5 });

        Assert.Equal(280, result.Microns);
    }

    [Fact]
    public async Task NoBag_UsesDefaultWithoutQueryingHistory()
    {
        var result = await CreateWorkflow().LoadAsync(new DrinkDraft());

        Assert.Equal(280, result.Microns);
        _bags.VerifyNoOtherCalls();
        _shots.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConfiguredAnchors_AreLoadedAfterExistingSeedRefresh()
    {
        var anchors = new[] { new GrindAnchor(100, 1, "user"), new GrindAnchor(800, 9, "user") };
        var profile = new GrinderProfile
        {
            EquipmentId = 7,
            AnchorsJson = DeterministicGrindInterpolator.SerializeAnchors(anchors)
        };
        var sequence = new MockSequence();
        _profiles.InSequence(sequence).Setup(repo => repo.EnsureCurrentSeedsAsync(7)).ReturnsAsync(profile);
        _profiles.InSequence(sequence).Setup(repo => repo.GetByEquipmentIdAsync(7)).ReturnsAsync(profile);
        var draft = GrinderDraft("My grinder");

        var result = await CreateWorkflow().LoadAsync(draft);

        Assert.Equal(anchors, result.Anchors);
        Assert.Equal("My grinder", result.GrinderName);
        Assert.False(result.IsUncalibrated);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ invalid")]
    [InlineData("[]")]
    [InlineData("[{\"micron\":100,\"setting\":1,\"source\":\"user\"}]")]
    public async Task MissingOrIncompleteAnchors_UseKnownNameSeeds(string? json)
    {
        _profiles.Setup(repo => repo.EnsureCurrentSeedsAsync(7)).ReturnsAsync((GrinderProfile?)null);
        _profiles.Setup(repo => repo.GetByEquipmentIdAsync(7))
            .ReturnsAsync(new GrinderProfile { EquipmentId = 7, AnchorsJson = json });

        var result = await CreateWorkflow().LoadAsync(GrinderDraft("Turin DF64"));

        Assert.Same(DeterministicGrindInterpolator.DF64SeedAnchors, result.Anchors);
        Assert.False(result.IsUncalibrated);
    }

    [Fact]
    public async Task UnknownSelectedGrinder_RequiresCalibration()
    {
        _profiles.Setup(repo => repo.EnsureCurrentSeedsAsync(7)).ReturnsAsync((GrinderProfile?)null);
        _profiles.Setup(repo => repo.GetByEquipmentIdAsync(7)).ReturnsAsync((GrinderProfile?)null);

        var result = await CreateWorkflow().LoadAsync(GrinderDraft("Unlisted grinder"));

        Assert.True(result.IsUncalibrated);
        Assert.Null(result.Anchors);
    }

    [Fact]
    public async Task ReadFailure_IsNotDisguisedAsDefaultSuccess()
    {
        var failure = new IOException("Bag storage unavailable");
        _bags.Setup(repo => repo.GetByIdAsync(5)).ThrowsAsync(failure);

        var actual = await Assert.ThrowsAsync<IOException>(
            () => CreateWorkflow().LoadAsync(new DrinkDraft { SelectedBagId = 5 }));

        Assert.Same(failure, actual);
    }

    private static DrinkDraft GrinderDraft(string name) =>
        new()
        {
            SelectedGrinderId = 7,
            AvailableEquipment =
            [
                new EquipmentDto { Id = 7, Name = name, Type = EquipmentType.Grinder }
            ]
        };
}
