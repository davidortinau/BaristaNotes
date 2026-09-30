using BaristaNotes.Core.Models;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BaristaNotes.Tests.Unit;

public sealed class AddCoffeeWorkflowTests
{
    private readonly Mock<IBeanService> _beans = new(MockBehavior.Strict);
    private readonly Mock<IBagService> _bags = new(MockBehavior.Strict);
    private AddCoffeeWorkflow CreateWorkflow() =>
        new(_beans.Object, _bags.Object, NullLogger<AddCoffeeWorkflow>.Instance);

    [Fact]
    public void TypeDraft_TrimsFieldsAndPlacesNotesOnlyOnBag()
    {
        var draft = new AddCoffeeDraft
        {
            Name = " Coffee ", Roaster = " Roaster ", Origin = " Origin ", Notes = " Bag notes "
        };
        var input = draft.ToCreateDto();
        Assert.Equal("Coffee", input.Name);
        Assert.Equal("Roaster", input.Roaster);
        Assert.Equal("Origin", input.Origin);
        Assert.Null(input.Notes);
        Assert.Equal("Bag notes", draft.BagNotes);
    }

    [Fact]
    public async Task Validation_PrecedesAnyWrite()
    {
        var workflow = CreateWorkflow();
        var blank = await workflow.CreateAsync(new AddCoffeeDraft());
        Assert.Equal("Bean name is required", blank.Bean.ErrorMessage);
        var future = await workflow.CreateAsync(new AddCoffeeDraft { Name = "Coffee", RoastDate = DateTime.Today.AddDays(1) });
        Assert.Equal("Roast date cannot be in the future", future.Bean.ErrorMessage);
        _beans.VerifyNoOtherCalls();
        _bags.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BeanFailure_DoesNotCreateBag()
    {
        var failure = OperationResult<BeanDto>.Fail("Bean failed");
        _beans.Setup(service => service.CreateBeanAsync(It.IsAny<CreateBeanDto>())).ReturnsAsync(failure);
        var result = await CreateWorkflow().CreateAsync(new AddCoffeeDraft { Name = "Coffee" });
        Assert.Same(failure, result.Bean);
        Assert.Null(result.InitialBag);
        _bags.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BagFailure_RemainsSeparateFromCommittedBean()
    {
        var bean = OperationResult<BeanDto>.Ok(new BeanDto { Id = 5, Name = "Coffee" });
        var bag = OperationResult<BagSummaryDto>.Fail("Bag failed");
        _beans.Setup(service => service.CreateBeanAsync(It.IsAny<CreateBeanDto>())).ReturnsAsync(bean);
        _bags.Setup(service => service.CreateNewBagForBeanAsync(5, DateTime.Today, null)).ReturnsAsync(bag);
        var result = await CreateWorkflow().CreateAsync(new AddCoffeeDraft { Name = "Coffee" });
        Assert.Same(bean, result.Bean);
        Assert.Same(bag, result.InitialBag);
    }

    [Fact]
    public async Task BagInput_IsCapturedBeforeBeanWriteCompletes()
    {
        var completion = new TaskCompletionSource<OperationResult<BeanDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _beans.Setup(service => service.CreateBeanAsync(It.Is<CreateBeanDto>(input => input.Name == "Coffee")))
            .Returns(completion.Task);
        var date = DateTime.Today.AddDays(-3);
        var bag = OperationResult<BagSummaryDto>.Ok(new BagSummaryDto { Id = 7, BeanId = 5, BeanName = "Coffee" });
        _bags.Setup(service => service.CreateNewBagForBeanAsync(5, date, "Original notes")).ReturnsAsync(bag);
        var draft = new AddCoffeeDraft { Name = "Coffee", RoastDate = date, Notes = " Original notes " };

        var save = CreateWorkflow().CreateAsync(draft);
        draft.RoastDate = DateTime.Today;
        draft.Notes = "Changed later";
        completion.SetResult(OperationResult<BeanDto>.Ok(new BeanDto { Id = 5, Name = "Coffee" }));

        Assert.Same(bag, (await save).InitialBag);
        _bags.VerifyAll();
    }

    [Fact]
    public async Task Browse_AddsTodayBagWithoutCreatingAnotherBean()
    {
        var result = OperationResult<BagSummaryDto>.Ok(new BagSummaryDto { Id = 7, BeanId = 5, BeanName = "Coffee" });
        _bags.Setup(service => service.CreateNewBagForBeanAsync(5, DateTime.Today, null)).ReturnsAsync(result);
        Assert.Same(result, await CreateWorkflow().AddBagForExistingBeanAsync(5));
        _beans.VerifyNoOtherCalls();
    }
}
