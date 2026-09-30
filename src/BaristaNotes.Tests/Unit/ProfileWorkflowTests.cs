using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Exceptions;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BaristaNotes.Tests.Unit;

public sealed class ProfileWorkflowTests
{
    private readonly Mock<IUserProfileService> _profiles = new(MockBehavior.Strict);
    private readonly Mock<IDataChangeNotifier> _notifier = new(MockBehavior.Strict);
    private ProfileWorkflow CreateWorkflow() =>
        new(_profiles.Object, _notifier.Object, NullLogger<ProfileWorkflow>.Instance);

    [Fact]
    public void Load_PreservesStagedPhotoAndMapsNullContext()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var draft = new ProfileDraft { StagedAvatarBytes = bytes };
        draft.ApplyLoadedData(new UserProfileDto { Id = 5, Name = "Guest" });

        Assert.Equal(5, draft.ProfileId);
        Assert.True(draft.IsEditing);
        Assert.Equal("", draft.Context);
        Assert.Same(bytes, draft.StagedAvatarBytes);
        Assert.Null(draft.ValidationError);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n ")]
    public async Task BlankName_DoesNotWriteOrNotify(string name)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            CreateWorkflow().SaveDetailsAsync(new ProfileDraft { Name = name }));

        _profiles.VerifyNoOtherCalls();
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Create_SetsDraftIdBeforeCreatedNotificationAndNormalizesBlankContextOnly()
    {
        var draft = new ProfileDraft { Name = " Guest ", Context = " \n " };
        var saved = new UserProfileDto { Id = 5, Name = draft.Name };
        _profiles.Setup(service => service.CreateProfileAsync(It.Is<CreateUserProfileDto>(
            dto => dto.Name == " Guest " && dto.Context == null && dto.AvatarPath == null)))
            .ReturnsAsync(saved);
        _notifier.Setup(notifier => notifier.NotifyDataChanged(DataChangeType.ProfileCreated, saved))
            .Callback(() => Assert.Equal(5, draft.ProfileId));

        Assert.Same(saved, await CreateWorkflow().SaveDetailsAsync(draft));
        _profiles.VerifyAll();
        _notifier.VerifyAll();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Likes pour over")]
    public async Task Update_PreservesExactContextAndNotifiesWithId(string context)
    {
        var draft = new ProfileDraft { ProfileId = 5, Name = "Guest", Context = context };
        var saved = new UserProfileDto { Id = 5, Name = draft.Name, Context = context };
        _profiles.Setup(service => service.UpdateProfileAsync(5, It.Is<UpdateUserProfileDto>(
            dto => dto.Name == "Guest" && dto.Context == context && dto.AvatarPath == null)))
            .ReturnsAsync(saved);
        _notifier.Setup(notifier => notifier.NotifyDataChanged(DataChangeType.ProfileUpdated, 5));

        Assert.Same(saved, await CreateWorkflow().SaveDetailsAsync(draft));
        _profiles.VerifyAll();
        _notifier.VerifyAll();
    }

    [Fact]
    public async Task FailedPhoto_PreservesSavedIdAndStagedBytesForUpdateRetry()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var draft = new ProfileDraft { Name = "Guest", StagedAvatarBytes = bytes };
        var saved = new UserProfileDto { Id = 5, Name = draft.Name };
        _profiles.Setup(service => service.CreateProfileAsync(It.IsAny<CreateUserProfileDto>())).ReturnsAsync(saved);
        _profiles.Setup(service => service.UpdateProfileAsync(5, It.IsAny<UpdateUserProfileDto>())).ReturnsAsync(saved);
        _profiles.Setup(service => service.UpdateProfileImageAsync(5, It.IsAny<Stream>()))
            .ReturnsAsync(ProfileImageUpdateResult.FailureResult("Failed to save image"));
        _notifier.Setup(notifier => notifier.NotifyDataChanged(DataChangeType.ProfileCreated, saved));
        _notifier.Setup(notifier => notifier.NotifyDataChanged(DataChangeType.ProfileUpdated, 5));
        var workflow = CreateWorkflow();

        await workflow.SaveDetailsAsync(draft);
        var photo = await workflow.SaveStagedAvatarAsync(draft);
        Assert.NotNull(photo);
        Assert.False(photo.Success);
        Assert.Equal("Failed to save image", photo.ErrorMessage);
        Assert.Same(bytes, draft.StagedAvatarBytes);
        Assert.Equal(5, draft.ProfileId);
        await workflow.SaveDetailsAsync(draft);

        _profiles.Verify(service => service.CreateProfileAsync(It.IsAny<CreateUserProfileDto>()), Times.Once);
        _profiles.Verify(service => service.UpdateProfileAsync(5, It.IsAny<UpdateUserProfileDto>()), Times.Once);
        _notifier.Verify(notifier => notifier.NotifyDataChanged(DataChangeType.ProfileUpdated, 5), Times.Once);
    }

    [Fact]
    public async Task SuccessfulPhoto_ClearsStagedBytesBeforeNotificationAndDisposesStream()
    {
        var draft = new ProfileDraft { ProfileId = 5, Name = "Guest", StagedAvatarBytes = [1, 2, 3] };
        Stream? passedStream = null;
        _profiles.Setup(service => service.UpdateProfileImageAsync(5, It.IsAny<Stream>()))
            .Callback<int, Stream>((_, stream) =>
            {
                passedStream = stream;
                Assert.False(stream.CanWrite);
                Assert.Equal(0, stream.Position);
                Assert.Equal(3, stream.Length);
            })
            .ReturnsAsync(ProfileImageUpdateResult.SuccessResult("profile_avatar_5.jpg"));
        _notifier.Setup(notifier => notifier.NotifyDataChanged(DataChangeType.ProfileUpdated, 5))
            .Callback(() => Assert.Null(draft.StagedAvatarBytes));

        var result = await CreateWorkflow().SaveStagedAvatarAsync(draft);

        Assert.True(result?.Success);
        Assert.NotNull(passedStream);
        Assert.False(passedStream.CanRead);
        _notifier.VerifyAll();
    }

    [Fact]
    public async Task NoStagedPhoto_DoesNotCallImageService()
    {
        Assert.Null(await CreateWorkflow().SaveStagedAvatarAsync(new ProfileDraft()));
        Assert.Null(await CreateWorkflow().SaveStagedAvatarAsync(new ProfileDraft { StagedAvatarBytes = [] }));
        _profiles.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StagedPhotoWithoutSavedId_IsAnExplicitError()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateWorkflow().SaveStagedAvatarAsync(new ProfileDraft { StagedAvatarBytes = [1] }));
        _profiles.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SaveFailure_LeavesDraftUnsavedAndDoesNotNotify()
    {
        var failure = new IOException("Write failed");
        _profiles.Setup(service => service.CreateProfileAsync(It.IsAny<CreateUserProfileDto>())).ThrowsAsync(failure);
        var draft = new ProfileDraft { Name = "Guest" };

        var actual = await Assert.ThrowsAsync<IOException>(() => CreateWorkflow().SaveDetailsAsync(draft));

        Assert.Same(failure, actual);
        Assert.Null(draft.ProfileId);
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Delete_UsesServiceAndExistingUpdateNotification()
    {
        _profiles.Setup(service => service.DeleteProfileAsync(5)).Returns(Task.CompletedTask);
        _notifier.Setup(notifier => notifier.NotifyDataChanged(DataChangeType.ProfileUpdated, 5));

        await CreateWorkflow().DeleteAsync(5);

        _profiles.VerifyAll();
        _notifier.VerifyAll();
    }
}
