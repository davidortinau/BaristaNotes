using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BaristaNotes.Tests.Unit;

public sealed class EquipmentWorkflowTests
{
    private readonly Mock<IEquipmentService> _equipment = new(MockBehavior.Strict);
    private readonly Mock<IDataChangeNotifier> _notifier = new(MockBehavior.Strict);

    private EquipmentWorkflow CreateWorkflow() =>
        new(_equipment.Object, _notifier.Object, NullLogger<EquipmentWorkflow>.Instance);

    [Fact]
    public void Draft_LoadsAllEditableFields()
    {
        var draft = new EquipmentDraft();
        Assert.Equal(EquipmentType.Machine, draft.SelectedType);
        Assert.False(draft.IsEditing);
        Assert.Equal("Equipment name is required", draft.ValidationError);

        draft.ApplyLoadedData(new EquipmentDto { Id = 5, Name = "DF64", Type = EquipmentType.Grinder });

        Assert.True(draft.IsEditing);
        Assert.Equal("DF64", draft.Name);
        Assert.Equal(EquipmentType.Grinder, draft.SelectedType);
        Assert.Equal("", draft.Notes);
        Assert.Null(draft.ValidationError);
    }

    [Theory]
    [InlineData(EquipmentType.Machine)]
    [InlineData(EquipmentType.Grinder)]
    [InlineData(EquipmentType.Tamper)]
    [InlineData(EquipmentType.PuckScreen)]
    [InlineData(EquipmentType.Other)]
    public async Task Create_PreservesTypeAndNameAndNotifiesWithCreatedDto(EquipmentType type)
    {
        var draft = new EquipmentDraft { Name = " New equipment ", SelectedType = type, Notes = " \n " };
        var created = new EquipmentDto { Id = 5, Name = draft.Name, Type = type, IsActive = true };
        _equipment.Setup(service => service.CreateEquipmentAsync(It.Is<CreateEquipmentDto>(
            dto => dto.Name == draft.Name && dto.Type == type && dto.Notes == null))).ReturnsAsync(created);
        _notifier.Setup(notifier => notifier.NotifyDataChanged(DataChangeType.EquipmentCreated, created));

        Assert.Same(created, await CreateWorkflow().SaveAsync(draft));
        Assert.Null(draft.EquipmentId);
        _equipment.VerifyAll();
        _notifier.VerifyAll();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("New notes")]
    public async Task Update_ExplicitlySetsOrClearsNotesAndNotifiesWithId(string notes)
    {
        var draft = new EquipmentDraft { EquipmentId = 5, Name = "DF64", SelectedType = EquipmentType.Grinder, Notes = notes };
        var expected = string.IsNullOrWhiteSpace(notes) ? null : notes;
        var saved = new EquipmentDto { Id = 5, Name = draft.Name, Type = draft.SelectedType, Notes = expected };
        _equipment.Setup(service => service.UpdateEquipmentAsync(5, It.Is<UpdateEquipmentDto>(
            dto => dto.Name == draft.Name && dto.Type == draft.SelectedType
                && dto.Notes.IsSpecified && dto.Notes.Value == expected && dto.IsActive == null)))
            .ReturnsAsync(saved);
        _notifier.Setup(notifier => notifier.NotifyDataChanged(DataChangeType.EquipmentUpdated, 5));

        Assert.Same(saved, await CreateWorkflow().SaveAsync(draft));
        _equipment.VerifyAll();
        _notifier.VerifyAll();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(5)]
    public async Task BlankName_IsRejectedBeforeCreateOrUpdate(int? id)
    {
        var draft = new EquipmentDraft { EquipmentId = id, Name = " \t " };

        await Assert.ThrowsAsync<ValidationException>(() => CreateWorkflow().SaveAsync(draft));

        _equipment.VerifyNoOtherCalls();
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Archive_DoesNotUseDelete()
    {
        _equipment.Setup(service => service.ArchiveEquipmentAsync(5)).Returns(Task.CompletedTask);
        _notifier.Setup(notifier => notifier.NotifyDataChanged(DataChangeType.EquipmentUpdated, 5));

        await CreateWorkflow().ArchiveAsync(5);

        _equipment.VerifyAll();
        _equipment.Verify(service => service.DeleteEquipmentAsync(It.IsAny<int>()), Times.Never);
        _notifier.VerifyAll();
    }

    [Fact]
    public async Task FailedSave_DoesNotNotify()
    {
        var failure = new IOException("Write failed");
        _equipment.Setup(service => service.CreateEquipmentAsync(It.IsAny<CreateEquipmentDto>()))
            .ThrowsAsync(failure);

        var thrown = await Assert.ThrowsAsync<IOException>(() =>
            CreateWorkflow().SaveAsync(new EquipmentDraft { Name = "Test" }));

        Assert.Same(failure, thrown);
        _notifier.VerifyNoOtherCalls();
    }
}
