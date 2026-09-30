using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BaristaNotes.Core.Hosting;
using BaristaNotes.Core.Models.Enums;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Core.Services.Grind;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BaristaNotes.Tests.Unit;

public sealed class SharedAIServiceTests
{
    private const string AdviceJson = """
        {"adjustments":[{"parameter":"grind","direction":"finer","amount":"5 microns"}],"reasoning":"Reduce the flow."}
        """;

    [Fact]
    public async Task MissingConfiguration_ReturnsSourceFailureWithoutReadingData()
    {
        var shots = new Mock<IShotService>(MockBehavior.Strict);
        using var provider = Provider(shots.Object);
        var advice = provider.GetRequiredService<IAIAdviceService>();

        Assert.False(await advice.IsConfiguredAsync());
        var result = await advice.GetAdviceForShotAsync(1);
        Assert.False(result.Success);
        Assert.Equal("AI advice is temporarily unavailable. Please try again later.", result.ErrorMessage);
        Assert.Null(await advice.GetPassiveInsightAsync(1));
        var recommendation = await advice.GetRecommendationsForBeanAsync(1);
        Assert.False(recommendation.Success);
        shots.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TypedLocalAdvice_PreservesPromptOutputAndProviderLabel()
    {
        var shots = new Mock<IShotService>(MockBehavior.Strict);
        var context = Context();
        shots.Setup(service => service.GetShotContextForAIAsync(7)).ReturnsAsync(context);
        var client = new TestChatClient(AdviceJson);
        using var provider = Provider(shots.Object, client);

        var response = await provider.GetRequiredService<IAIAdviceService>().GetAdviceForShotAsync(7);

        Assert.True(response.Success);
        Assert.Equal("via Apple Intelligence", response.Source);
        Assert.Equal("Reduce the flow.", response.Reasoning);
        Assert.Equal("grind", Assert.Single(response.Adjustments).Parameter);
        Assert.Equal(AIPromptBuilder.BuildPrompt(context), response.PromptSent);
        Assert.Equal(1, client.Calls);
        Assert.Contains("Espresso", client.LastMessages![0].Text);
    }

    [Fact]
    public async Task MissingShot_ReturnsSourceFailureWithoutCallingModel()
    {
        var shots = new Mock<IShotService>(MockBehavior.Strict);
        shots.Setup(service => service.GetShotContextForAIAsync(7)).ReturnsAsync((AIAdviceRequestDto?)null);
        var client = new TestChatClient(AdviceJson);
        using var provider = Provider(shots.Object, client);

        var response = await provider.GetRequiredService<IAIAdviceService>().GetAdviceForShotAsync(7);

        Assert.False(response.Success);
        Assert.Equal("Shot not found.", response.ErrorMessage);
        Assert.Equal(0, client.Calls);
    }

    [Fact]
    public async Task LocalFailure_DisablesClientForSharedSingletonLifetime()
    {
        var shots = new Mock<IShotService>(MockBehavior.Strict);
        shots.Setup(service => service.GetShotContextForAIAsync(7)).ReturnsAsync(Context());
        var client = new TestChatClient(AdviceJson) { Failure = new IOException("Local provider failed") };
        using var provider = Provider(shots.Object, client);
        var advice = provider.GetRequiredService<IAIAdviceService>();

        var first = await advice.GetAdviceForShotAsync(7);
        Assert.False(first.Success);
        Assert.Equal("AI service error. Please try again later.", first.ErrorMessage);
        Assert.False(await advice.IsConfiguredAsync());
        client.Failure = null;

        using var scope = provider.CreateScope();
        var same = scope.ServiceProvider.GetRequiredService<IAIAdviceService>();
        Assert.Same(advice, same);
        Assert.False((await same.GetAdviceForShotAsync(7)).Success);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task ContextFailure_PreservesSourceErrorClassification()
    {
        var shots = new Mock<IShotService>(MockBehavior.Strict);
        shots.Setup(service => service.GetShotContextForAIAsync(7)).ThrowsAsync(new HttpRequestException());
        using var provider = Provider(shots.Object, new TestChatClient(AdviceJson));

        var response = await provider.GetRequiredService<IAIAdviceService>().GetAdviceForShotAsync(7);

        Assert.False(response.Success);
        Assert.Equal("Unable to connect. Please check your internet connection.", response.ErrorMessage);
    }

    [Fact]
    public async Task Recommendation_UsesHistoryFrequencyAndNewestTieBreak()
    {
        var shots = new Mock<IShotService>(MockBehavior.Strict);
        var now = DateTime.UtcNow;
        shots.Setup(service => service.GetBeanRecommendationContextAsync(3))
            .ReturnsAsync(new BeanRecommendationContextDto
            {
                BeanId = 3, BeanName = "Test bean", HasHistory = true,
                HistoricalShots =
                [
                    new() { BrewMethod = BrewMethod.Espresso, Timestamp = now.AddDays(-3) },
                    new() { BrewMethod = BrewMethod.Espresso, Timestamp = now.AddDays(-2) },
                    new() { BrewMethod = BrewMethod.FrenchPress, Timestamp = now.AddDays(-1) },
                    new() { BrewMethod = BrewMethod.FrenchPress, Timestamp = now }
                ]
            });
        var client = new TestChatClient("""{"dose":30,"grind":"coarse","output":500,"duration":240}""");
        using var provider = Provider(shots.Object, client);

        var response = await provider.GetRequiredService<IAIAdviceService>().GetRecommendationsForBeanAsync(3);

        Assert.True(response.Success);
        Assert.Equal(30, response.Dose);
        Assert.Equal(RecommendationType.ReturningBean, response.RecommendationType);
        Assert.Contains("French Press", client.LastMessages![0].Text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("coarse", response.GrindSetting);
    }

    [Fact]
    public async Task Recommendation_ContextCancellationStillPropagates()
    {
        var shots = new Mock<IShotService>(MockBehavior.Strict);
        shots.Setup(service => service.GetBeanRecommendationContextAsync(3))
            .ThrowsAsync(new OperationCanceledException());
        using var provider = Provider(shots.Object, new TestChatClient("{}"));

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.GetRequiredService<IAIAdviceService>().GetRecommendationsForBeanAsync(3));
    }

    [Fact]
    public async Task DataScopes_EndBeforeModelCallsAndAreFreshForEachRequest()
    {
        var shots = new Mock<IShotService>(MockBehavior.Strict);
        shots.Setup(service => service.GetShotContextForAIAsync(7)).ReturnsAsync(Context());
        var markers = new List<ScopeMarker>();
        var client = new TestChatClient(AdviceJson)
        {
            BeforeResponse = () => Assert.All(markers, marker => Assert.True(marker.Disposed))
        };
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IChatClient>(client);
        services.AddScoped(_ =>
        {
            var marker = new ScopeMarker();
            markers.Add(marker);
            return marker;
        });
        services.AddScoped<IShotService>(provider =>
        {
            _ = provider.GetRequiredService<ScopeMarker>();
            return shots.Object;
        });
        services.AddBaristaNotesAI();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var advice = provider.GetRequiredService<IAIAdviceService>();

        Assert.True((await advice.GetAdviceForShotAsync(7)).Success);
        Assert.True((await advice.GetAdviceForShotAsync(7)).Success);
        Assert.Equal(2, markers.Count);
        Assert.NotSame(markers[0], markers[1]);
        Assert.All(markers, marker => Assert.True(marker.Disposed));
    }

    [Fact]
    public async Task GrindAI_UsesSharedProviderAndPreservesExplicitCancellation()
    {
        var client = new TestChatClient("""
            {"min_setting":20,"max_setting":30,"suggested_setting":25,"confidence":"high","explanation":"Test scale"}
            """);
        using var provider = Provider(new Mock<IShotService>().Object, client);
        var grind = provider.GetRequiredService<IGrindTranslationAI>();

        var result = await grind.TranslateAsync("Test grinder", BrewMethod.V60, "medium");

        Assert.NotNull(result);
        Assert.Equal(20m, result.MinSetting);
        Assert.Equal(30m, result.MaxSetting);
        Assert.Equal(25m, result.SuggestedSetting);
        Assert.Equal("high", result.Confidence);
        Assert.Contains("Test grinder", client.LastMessages![1].Text);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            grind.TranslateAsync("Test grinder", BrewMethod.V60, "medium", cancelled.Token));
    }

    [Fact]
    public async Task LocalFailure_FallsBackToRealAzureClientAgainstLoopbackOnly()
    {
        var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        var port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();
        using var listener = new HttpListener();
        var endpoint = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(endpoint);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = RespondAsync();
        var shots = new Mock<IShotService>(MockBehavior.Strict);
        shots.Setup(service => service.GetShotContextForAIAsync(7)).ReturnsAsync(Context());
        var local = new TestChatClient(AdviceJson) { Failure = new IOException("Local provider failed") };
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = endpoint,
                ["AzureOpenAI:ApiKey"] = "unused-loopback-test-value"
            }).Build());
        services.AddScoped(_ => shots.Object);
        services.AddSingleton<IChatClient>(local);
        services.AddBaristaNotesAI();
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        try
        {
            var result = await provider.GetRequiredService<IAIAdviceService>()
                .GetAdviceForShotAsync(7, timeout.Token);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal("via Azure OpenAI", result.Source);
            Assert.Equal("Reduce the flow.", result.Reasoning);
            Assert.Equal(1, local.Calls);
            var request = await server.WaitAsync(timeout.Token);
            Assert.Contains("/openai/deployments/gpt-4.1-mini/chat/completions", request.Path);
            Assert.Contains("Test bean", request.Body);
        }
        finally
        {
            timeout.Cancel();
            listener.Stop();
        }

        async Task<(string Path, string Body)> RespondAsync()
        {
            var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(timeout.Token);
            var response = JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = "local-test-response",
                @object = "chat.completion",
                created = 1,
                model = "gpt-4.1-mini",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content = AdviceJson },
                        finish_reason = "stop"
                    }
                },
                usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 }
            });
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = response.Length;
            await context.Response.OutputStream.WriteAsync(response, timeout.Token);
            context.Response.Close();
            return (context.Request.RawUrl ?? "", body);
        }
    }

    private static ServiceProvider Provider(IShotService shots, IChatClient? client = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddScoped(_ => shots);
        if (client is not null)
            services.AddSingleton(client);
        services.AddBaristaNotesAI();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static AIAdviceRequestDto Context() => new()
    {
        ShotId = 7,
        CurrentShot = new() { DoseIn = 18, ActualTime = 28, ActualOutput = 36, BrewMethod = BrewMethod.Espresso },
        BeanInfo = new() { Name = "Test bean" }
    };

    private sealed class ScopeMarker : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    internal sealed class TestChatClient(string responseJson) : IChatClient
    {
        public int Calls { get; private set; }
        public Exception? Failure { get; set; }
        public Action? BeforeResponse { get; init; }
        public IReadOnlyList<ChatMessage>? LastMessages { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastMessages = messages.ToArray();
            cancellationToken.ThrowIfCancellationRequested();
            BeforeResponse?.Invoke();
            if (Failure is not null)
                return Task.FromException<ChatResponse>(Failure);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseJson)));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
