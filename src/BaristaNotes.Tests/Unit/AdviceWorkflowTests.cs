using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Workflows;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BaristaNotes.Tests.Unit;

public sealed class AdviceWorkflowTests
{
    [Fact]
    public async Task UnavailableService_ShowsSharedUnavailableMessage()
    {
        var host = new TestHost();
        var service = Service(configured: false);

        using var workflow = Create(host, service.Object);
        await workflow.RunAsync(7, CancellationToken.None);

        Assert.Equal(
            (AdvicePresentation.Unavailable, AdvicePresentation.UnavailableDetail),
            Assert.Single(host.Errors));
        Assert.Equal([false], host.LoadingStates);
    }

    [Fact]
    public async Task SuccessfulResponse_PresentsWhileOwnerIsCurrent()
    {
        var response = new AIAdviceResponseDto { Success = true, Reasoning = "Adjust finer." };
        var host = new TestHost();
        var service = Service(response: response);

        using var workflow = Create(host, service.Object);
        await workflow.RunAsync(8, CancellationToken.None);

        Assert.Same(response, host.Presented);
        Assert.Equal([true, false, false], host.LoadingStates);
        Assert.Empty(host.Errors);
    }

    [Fact]
    public async Task UnsuccessfulResponse_PreservesServiceDetail()
    {
        var host = new TestHost();
        var service = Service(response: new AIAdviceResponseDto
        {
            Success = false,
            ErrorMessage = "No shot context."
        });

        using var workflow = Create(host, service.Object);
        await workflow.RunAsync(9, CancellationToken.None);

        Assert.Equal(
            (AdvicePresentation.Unsuccessful, "No shot context."),
            Assert.Single(host.Errors));
    }

    [Fact]
    public async Task Timeout_ShowsTimedOutMessage()
    {
        var host = new TestHost();
        var service = Service(request: async cancellation =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellation);
            return new AIAdviceResponseDto();
        });

        using var workflow = Create(
            host,
            service.Object,
            TimeSpan.FromMilliseconds(20));
        await workflow.RunAsync(10, CancellationToken.None);

        Assert.Equal(
            (AdvicePresentation.TimedOut, AdvicePresentation.TimedOutDetail),
            Assert.Single(host.Errors));
        Assert.False(workflow.IsActive);
    }

    [Fact]
    public async Task OwnerCancellation_SuppressesErrorAndPresentation()
    {
        using var owner = new CancellationTokenSource();
        var host = new TestHost();
        var service = Service(request: cancellation =>
        {
            owner.Cancel();
            return Task.FromCanceled<AIAdviceResponseDto>(cancellation);
        });

        using var workflow = Create(host, service.Object);
        await workflow.RunAsync(11, owner.Token);

        Assert.Empty(host.Errors);
        Assert.Null(host.Presented);
    }

    [Fact]
    public async Task RequestFailure_ShowsFailedMessageAndStopsLoading()
    {
        var host = new TestHost();
        var service = Service(request: _ =>
            Task.FromException<AIAdviceResponseDto>(
                new InvalidOperationException("provider failed")));

        using var workflow = Create(host, service.Object);
        await workflow.RunAsync(12, CancellationToken.None);

        Assert.Equal(
            (AdvicePresentation.Failed, "provider failed"),
            Assert.Single(host.Errors));
        Assert.False(host.LoadingStates.Last());
    }

    private static AdviceWorkflow Create(
        TestHost host,
        IAIAdviceService service,
        TimeSpan? timeout = null) =>
        new(host, service, NullLogger.Instance, timeout);

    private static Mock<IAIAdviceService> Service(
        bool configured = true,
        AIAdviceResponseDto? response = null,
        Func<CancellationToken, Task<AIAdviceResponseDto>>? request = null)
    {
        var service = new Mock<IAIAdviceService>(MockBehavior.Strict);
        service.Setup(x => x.IsConfiguredAsync()).ReturnsAsync(configured);
        if (configured)
        {
            service.Setup(x => x.GetAdviceForShotAsync(
                    It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns<int, CancellationToken>((_, cancellation) =>
                    request?.Invoke(cancellation)
                    ?? Task.FromResult(response ?? new AIAdviceResponseDto { Success = true }));
        }
        return service;
    }

    private sealed class TestHost : IAdviceWorkflowHost
    {
        public bool Current { get; set; } = true;
        public List<bool> LoadingStates { get; } = [];
        public List<(string Title, string Detail)> Errors { get; } = [];
        public AIAdviceResponseDto? Presented { get; private set; }
        public bool IsCurrent => Current;

        public void SetLoading(bool loading) => LoadingStates.Add(loading);

        public Task PresentAsync(
            AIAdviceResponseDto response,
            CancellationToken cancellation)
        {
            Presented = response;
            return Task.CompletedTask;
        }

        public void ShowError(string title, string detail) =>
            Errors.Add((title, detail));
    }
}
