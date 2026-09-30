using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Tests.Unit;

public sealed class NativeCoffeeCompletionTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task CompletionFailure_ReportsToOriginalOwnerBeforeRelease(bool existingBean, bool exitFails)
    {
        var owner = new CompletionOwner();
        var failure = new IOException(exitFails ? "Exit failed" : "Selection failed");
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = CompleteAsync(owner, existingBean, () => exit.Task, bag =>
        {
            owner.Created(bag);
            throw failure;
        });
        Assert.Empty(owner.FeedbackRequests);
        Assert.Equal(0, owner.Releases);
        Assert.Equal(0, owner.Callbacks);

        if (exitFails)
            exit.SetException(failure);
        else
            exit.SetResult();
        await work;

        Assert.Equal(existingBean ? "Couldn't create bag" : failure.Message,
            Assert.Single(owner.FeedbackRequests));
        Assert.Equal(exitFails ? 0 : 1, owner.Callbacks);
        Assert.Equal(1, owner.Releases);
        Assert.Equal(exitFails
            ? new[] { "haptic", "feedback", "release" }
            : new[] { "haptic", "created", "feedback", "release" }, owner.Events);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task OwnerLostDuringExit_SuppressesCallbackAndError(bool existingBean, bool exitFails)
    {
        var owner = new CompletionOwner();
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var work = CompleteAsync(owner, existingBean, () => exit.Task, owner.Created);

        owner.IsOriginalPage = false;
        if (exitFails)
            exit.SetException(new IOException("Exit failed"));
        else
            exit.SetResult();
        await work;

        Assert.Equal(0, owner.Callbacks);
        Assert.Empty(owner.FeedbackRequests);
        Assert.Equal(new[] { "haptic", "release" }, owner.Events);
        Assert.Equal(1, owner.Releases);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task CompletionCancellation_ReleasesWithoutError(bool existingBean, bool cancelExit)
    {
        var owner = new CompletionOwner();

        await CompleteAsync(owner, existingBean,
            () => cancelExit ? Task.FromCanceled(new CancellationToken(true)) : Task.CompletedTask,
            bag =>
            {
                owner.Created(bag);
                throw new OperationCanceledException();
            });

        Assert.Equal(cancelExit ? 0 : 1, owner.Callbacks);
        Assert.Empty(owner.FeedbackRequests);
        Assert.Equal(1, owner.Releases);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OwnerLostInFailingCallback_DoesNotNotifyReplacement(bool existingBean)
    {
        var owner = new CompletionOwner();

        await CompleteAsync(owner, existingBean, () => Task.CompletedTask, bag =>
        {
            owner.Created(bag);
            owner.IsOriginalPage = false;
            throw new IOException("Selection failed");
        });

        Assert.Equal(1, owner.Callbacks);
        Assert.Empty(owner.FeedbackRequests);
        Assert.Equal(1, owner.Releases);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Success_UsesCapturedCallbackAndReleasesOnce(bool existingBean)
    {
        var owner = new CompletionOwner();
        var exit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<BagSummaryDto> callback = owner.Created;
        var work = CompleteAsync(owner, existingBean, () => exit.Task, callback);
        var replacementCalls = 0;
        callback = _ => replacementCalls++;

        Assert.Equal(0, owner.Callbacks);
        Assert.Equal(0, owner.Releases);
        exit.SetResult();
        await work;

        Assert.Same(owner.SavedBag, owner.ReceivedBag);
        Assert.Equal(1, owner.Callbacks);
        Assert.Equal(0, replacementCalls);
        Assert.Empty(owner.FeedbackRequests);
        Assert.Equal(new[] { "haptic", "created", "release" }, owner.Events);
        Assert.Equal(1, owner.Releases);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingCallback_StillReleasesOnce(bool existingBean)
    {
        var owner = new CompletionOwner();

        await CompleteAsync(owner, existingBean, () => Task.CompletedTask, null);

        Assert.Equal(0, owner.Callbacks);
        Assert.Empty(owner.FeedbackRequests);
        Assert.Equal(1, owner.Releases);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FeedbackFailure_PropagatesAfterOneRelease(bool existingBean)
    {
        var owner = new CompletionOwner();
        var failure = new IOException("Feedback failed");
        var requests = 0;

        var actual = await Assert.ThrowsAsync<IOException>(() => CompleteAsync(
            owner, existingBean, () => Task.FromException(new IOException("Exit failed")),
            owner.Created, (_, _) =>
            {
                Assert.True(owner.IsCurrent());
                requests++;
                throw failure;
            }));

        Assert.Same(failure, actual);
        Assert.Equal(1, requests);
        Assert.Equal(0, owner.Callbacks);
        Assert.Equal(1, owner.Releases);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OldCleanupOrderControl_SuppressesEvenUnguardedFeedback(bool exitFails)
    {
        var owner = new CompletionOwner();

        await Assert.ThrowsAsync<IOException>(() => CoffeeCompletion.RunAsync(
            () => owner.Events.Add("haptic"),
            () => exitFails ? Task.FromException(new IOException("Exit failed")) : Task.CompletedTask,
            owner.IsCurrent, () =>
            {
                owner.Created(owner.SavedBag);
                throw new IOException("Selection failed");
            }, owner.Release));
        owner.Feedback("Couldn't create bag", true);

        Assert.Empty(owner.FeedbackRequests);
        Assert.Equal(1, owner.Releases);
    }

    private static Task CompleteAsync(CompletionOwner owner, bool existingBean,
        Func<Task> exit, Action<BagSummaryDto>? created, Action<string, bool>? feedback = null) =>
        new CoffeeCompletion().CompleteSavedBagAsync(
            owner.SavedBag, existingBean ? CoffeeCreationPath.ExistingBean : CoffeeCreationPath.TypeForm,
            () => owner.Events.Add("haptic"), exit, owner.IsCurrent, created,
            feedback ?? owner.Feedback, owner.Release, NullLogger.Instance);

    // Feedback requests model the owner guard, not a rendered Android toast.
    private sealed class CompletionOwner
    {
        public BagSummaryDto SavedBag { get; } = new() { Id = 42, BeanName = "Completion test coffee" };
        public BagSummaryDto? ReceivedBag { get; private set; }
        public bool IsOriginalPage { get; set; } = true;
        public int Callbacks { get; private set; }
        public int Releases { get; private set; }
        public List<string> Events { get; } = [];
        public List<string> FeedbackRequests { get; } = [];

        public bool IsCurrent() => IsOriginalPage && Releases == 0;

        public void Created(BagSummaryDto bag)
        {
            Assert.True(IsCurrent());
            ReceivedBag = bag;
            Callbacks++;
            Events.Add("created");
        }

        public void Feedback(string message, bool error)
        {
            Assert.True(error);
            if (!IsCurrent())
                return;
            FeedbackRequests.Add(message);
            Events.Add("feedback");
        }

        public void Release()
        {
            Releases++;
            Events.Add("release");
        }
    }
}
