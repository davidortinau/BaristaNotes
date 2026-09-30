using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BaristaNotes.Tests.Unit;

public sealed class BeanBagWorkflowTests
{
    [Fact]
    public void BeanMapping_PreservesTextAndOnlyTrimsUrl()
    {
        var draft = new BeanDraft
        {
            Name = " Bean ", Roaster = " Roaster ", Origin = " ", Notes = "\n",
            RoasterUrl = " https://example.invalid/coffee "
        };

        var create = draft.ToCreateDto();
        Assert.Equal(" Bean ", create.Name);
        Assert.Equal(" Roaster ", create.Roaster);
        Assert.Null(create.Origin);
        Assert.Null(create.Notes);
        Assert.Equal("https://example.invalid/coffee", create.RoasterUrl);
        var update = draft.ToUpdateDto();
        Assert.True(update.Roaster.IsSpecified);
        Assert.Equal(" Roaster ", update.Roaster.Value);
        Assert.True(update.Origin.IsSpecified);
        Assert.Null(update.Origin.Value);
        Assert.True(update.Notes.IsSpecified);
        Assert.Null(update.Notes.Value);
        Assert.Null(update.IsActive);

        draft.RoasterUrl = " ";
        Assert.Null(draft.ToCreateDto().RoasterUrl);
        Assert.Equal("", draft.ToUpdateDto().RoasterUrl);
    }

    [Fact]
    public void BeanLoad_MapsNullOptionalValues()
    {
        var draft = new BeanDraft();
        Assert.Equal("Bean name is required", draft.ValidationError);
        draft.ApplyLoadedData(new BeanDto { Id = 5, Name = "Bean" });
        Assert.True(draft.IsEditing);
        Assert.Equal("", draft.Roaster);
        Assert.Equal("", draft.Origin);
        Assert.Equal("", draft.Notes);
        Assert.Equal("", draft.RoasterUrl);
        Assert.Null(draft.ValidationError);
    }

    [Fact]
    public async Task BeanUpdate_UsesExplicitClearsThenNotifiesWithId()
    {
        var beans = new Mock<IBeanService>(MockBehavior.Strict);
        var notifier = new Mock<IDataChangeNotifier>(MockBehavior.Strict);
        var saved = new BeanDto { Id = 5, Name = "Bean" };
        beans.Setup(service => service.UpdateBeanAsync(5, It.Is<UpdateBeanDto>(dto =>
            dto.Roaster.IsSpecified && dto.Roaster.Value == null
            && dto.Origin.IsSpecified && dto.Origin.Value == null
            && dto.Notes.IsSpecified && dto.Notes.Value == null
            && dto.RoasterUrl == ""))).ReturnsAsync(saved);
        notifier.Setup(service => service.NotifyDataChanged(DataChangeType.BeanUpdated, 5));
        var workflow = new BeanWorkflow(beans.Object, notifier.Object, NullLogger<BeanWorkflow>.Instance);

        Assert.Same(saved, await workflow.UpdateAsync(new BeanDraft { BeanId = 5, Name = "Bean" }));
        beans.VerifyAll();
        notifier.VerifyAll();
    }

    [Fact]
    public async Task BeanInvalidUpdate_DoesNotWrite()
    {
        var beans = new Mock<IBeanService>(MockBehavior.Strict);
        var notifier = new Mock<IDataChangeNotifier>(MockBehavior.Strict);
        var workflow = new BeanWorkflow(beans.Object, notifier.Object, NullLogger<BeanWorkflow>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.UpdateAsync(new BeanDraft { Name = "Bean" }));
        await Assert.ThrowsAsync<ValidationException>(() => workflow.UpdateAsync(new BeanDraft { BeanId = 5 }));
        beans.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public void BagValidation_UsesDateOnlyThenNotesLimit()
    {
        var today = new DateTime(2026, 9, 27, 8, 0, 0);
        var draft = new BagDraft { RoastDate = today.AddHours(4), Notes = new string('x', 500) };
        Assert.Null(draft.GetValidationError(today));
        draft.Notes += "x";
        Assert.Equal("Notes cannot exceed 500 characters", draft.GetValidationError(today));
        draft.RoastDate = today.AddDays(1);
        Assert.Equal("Roast date cannot be in the future", draft.GetValidationError(today));
    }

    [Fact]
    public void BagLoad_PreservesStatusAndFallbackName()
    {
        var draft = new BagDraft();
        var date = DateTime.Today.AddDays(-2).AddHours(8);
        draft.ApplyLoadedData(new Bag { Id = 5, BeanId = 3, IsComplete = true, RoastDate = date }, "Fallback");

        Assert.Equal("Fallback", draft.BeanName);
        Assert.Equal("", draft.Notes);
        Assert.True(draft.IsEditing);
        var entity = draft.ToEntity();
        Assert.Equal(5, entity.Id);
        Assert.Equal(3, entity.BeanId);
        Assert.Equal(date, entity.RoastDate);
        Assert.True(entity.IsComplete);
        Assert.Null(entity.Notes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BagSave_UsesCorrectBranchAndEntityNotification(bool editing)
    {
        var bags = new Mock<IBagService>(MockBehavior.Strict);
        var notifier = new Mock<IDataChangeNotifier>(MockBehavior.Strict);
        var saved = new Bag { Id = 5, BeanId = 3, RoastDate = DateTime.Today };
        var result = OperationResult<Bag>.Ok(saved);
        if (editing)
            bags.Setup(service => service.UpdateBagAsync(It.Is<Bag>(bag => bag.Id == 5 && bag.Notes == null)))
                .ReturnsAsync(result);
        else
            bags.Setup(service => service.CreateBagAsync(It.Is<Bag>(bag => bag.Id == 0 && bag.Notes == null)))
                .ReturnsAsync(result);
        notifier.Setup(service => service.NotifyDataChanged(
            editing ? DataChangeType.BagUpdated : DataChangeType.BagCreated, saved));
        var workflow = new BagWorkflow(bags.Object, notifier.Object, NullLogger<BagWorkflow>.Instance);
        var draft = new BagDraft { BagId = editing ? 5 : null, BeanId = 3, RoastDate = DateTime.Today, Notes = " " };

        Assert.Same(result, await workflow.SaveAsync(draft));
        Assert.Equal(editing ? 5 : (int?)null, draft.BagId);
        bags.VerifyAll();
        notifier.VerifyAll();
    }

    [Fact]
    public async Task BagFailure_RetainsServiceResultAndDoesNotNotify()
    {
        var bags = new Mock<IBagService>(MockBehavior.Strict);
        var notifier = new Mock<IDataChangeNotifier>(MockBehavior.Strict);
        var result = OperationResult<Bag>.Fail("Bean not found");
        bags.Setup(service => service.CreateBagAsync(It.IsAny<Bag>())).ReturnsAsync(result);
        var workflow = new BagWorkflow(bags.Object, notifier.Object, NullLogger<BagWorkflow>.Instance);

        Assert.Same(result, await workflow.SaveAsync(new BagDraft { BeanId = 99, RoastDate = DateTime.Today }));
        notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BagFutureFormDate_DoesNotCallService()
    {
        var bags = new Mock<IBagService>(MockBehavior.Strict);
        var notifier = new Mock<IDataChangeNotifier>(MockBehavior.Strict);
        var workflow = new BagWorkflow(bags.Object, notifier.Object, NullLogger<BagWorkflow>.Instance);
        var result = await workflow.SaveAsync(new BagDraft { RoastDate = DateTime.Today.AddDays(1) });

        Assert.False(result.Success);
        Assert.Equal("Roast date cannot be in the future", result.ErrorMessage);
        bags.VerifyNoOtherCalls();
        notifier.VerifyNoOtherCalls();
    }
}
