using System.ClientModel;
using System.ClientModel.Primitives;
using BaristaNotes.Api.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Api.Tests;

public sealed class ReplayChatClientTests : IDisposable
{
    readonly string _directory = Path.Combine(Path.GetTempPath(), "replay-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    sealed class FakeModel : IChatClient
    {
        public Func<ChatResponse>? Answer { get; set; }
        public int Calls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Answer!());
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    (ReplayChatClient Client, FakeModel Model, ModelReplay Replay) Create(ReplayMode mode = ReplayMode.Auto)
    {
        var model = new FakeModel();
        var replay = new ModelReplay(_directory, mode);
        return (new ReplayChatClient(model, replay, NullLogger<ReplayChatClient>.Instance), model, replay);
    }

    static ChatMessage[] Ask(string text) => [new(ChatRole.User, text)];

    [Fact]
    public async Task Replays_the_recorded_answer_when_the_model_is_unreachable()
    {
        var (client, model, _) = Create();
        model.Answer = () => new ChatResponse(new ChatMessage(ChatRole.Assistant, "{\"picks\":[]}"));
        await client.GetResponseAsync(Ask("drink 1 on 2026-10-01"));

        model.Answer = () => throw new HttpRequestException("No route to host");
        var scope = new RunScope(Guid.NewGuid(), null!);
        using (RunContext.Enter(scope))
        {
            var replayed = await client.GetResponseAsync(Ask("drink 1 on 2026-10-27"));
            Assert.Equal("{\"picks\":[]}", replayed.Text);
        }

        Assert.Equal("The model could not be reached.", scope.Flags.ReplayReason);
    }

    [Fact]
    public async Task Only_mode_never_calls_the_model()
    {
        var (client, model, replay) = Create();
        model.Answer = () => new ChatResponse(new ChatMessage(ChatRole.Assistant, "recorded"));
        await client.GetResponseAsync(Ask("same request"));

        replay.Mode = ReplayMode.Only;
        var answer = await client.GetResponseAsync(Ask("same request"));

        Assert.Equal("recorded", answer.Text);
        Assert.Equal(1, model.Calls);
    }

    [Fact]
    public async Task Fails_clearly_when_there_is_no_recording()
    {
        var (client, model, _) = Create();
        model.Answer = () => throw new HttpRequestException("offline");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetResponseAsync(Ask("never recorded")));
        Assert.Contains("no recorded answer", error.Message);
    }

    [Fact]
    public async Task Does_not_hide_errors_from_a_reachable_model()
    {
        var (client, model, _) = Create();
        model.Answer = () => new ChatResponse(new ChatMessage(ChatRole.Assistant, "recorded"));
        await client.GetResponseAsync(Ask("request"));

        model.Answer = () => throw new ClientResultException("Unauthorized", new FakeResponse(401));
        await Assert.ThrowsAsync<ClientResultException>(() => client.GetResponseAsync(Ask("request")));
    }

    sealed class FakeResponse(int status) : PipelineResponse
    {
        public override int Status => status;
        public override string ReasonPhrase => "";
        public override Stream? ContentStream { get; set; }
        public override BinaryData Content => BinaryData.FromString("");
        protected override PipelineResponseHeaders HeadersCore => throw new NotSupportedException();
        public override BinaryData BufferContent(CancellationToken cancellationToken = default) => Content;
        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Content);
        public override void Dispose() { }
    }
}
