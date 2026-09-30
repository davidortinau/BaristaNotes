using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Tests.Unit;

public sealed class SharedVisionServiceTests
{
    [Fact]
    public async Task MissingConfiguration_DoesNotConsumeCallerStream()
    {
        var service = CreateService(new ConfigurationBuilder().Build());
        using var image = new MemoryStream([1, 2, 3]);

        Assert.False(await service.IsAvailableAsync());
        Assert.False((await service.AnalyzeImageAsync(image, "Count visible people")).Success);
        Assert.False((await service.ExtractBeanLabelAsync(image)).Success);
        Assert.False((await service.ClassifyPhotoAsync(image)).Success);
        Assert.Equal(0, image.Position);
        Assert.True(image.CanRead);
    }

    [Theory]
    [InlineData("I see 3 people.", 3)]
    [InlineData("There is one person.", 1)]
    [InlineData("The room is empty.", 0)]
    [InlineData("The image is unclear.", 0)]
    public async Task RoomAnalysis_UsesVisionModelAndSourceCountRules(string response, int people)
    {
        await WithResponseAsync(response, async service =>
        {
            using var image = new MemoryStream([1, 2, 3]);
            var result = await service.AnalyzeImageAsync(image, "How many cups?");

            Assert.True(result.Success);
            Assert.Equal(people, result.PeopleCount);
            Assert.Equal(people, result.CupsNeeded);
            Assert.Equal(people * 18, result.BeansNeededGrams);
            Assert.Equal(response, result.Message);
            Assert.True(image.CanRead);
        }, "gpt-4o", "How many cups?");
    }

    [Fact]
    public async Task BeanExtraction_UsesLighterModelAndSharedParser()
    {
        const string response = """
            {"name":"Test Coffee","roaster":"Test Roaster","origin":"Ethiopia","roastDate":"2026-09-20","notes":"Washed"}
            """;
        await WithResponseAsync(response, async service =>
        {
            using var image = new MemoryStream([1, 2, 3]);
            var result = await service.ExtractBeanLabelAsync(image);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal("Test Coffee", result.Name);
            Assert.Equal("Test Roaster", result.Roaster);
            Assert.Equal("Ethiopia", result.Origin);
            Assert.True(image.CanRead);
        }, "gpt-4o-mini", "Extract the fields.");
    }

    [Fact]
    public async Task Classification_UsesVisionModelAndPreservesUncertainIntent()
    {
        const string response = """
            {"intent":"unknown","isObvious":false,"rationale":"Mixed subjects","name":null,"roaster":null,"origin":null,"roastDate":null,"notes":null}
            """;
        await WithResponseAsync(response, async service =>
        {
            using var image = new MemoryStream([1, 2, 3]);
            var result = await service.ClassifyPhotoAsync(image);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(PhotoWorkflowIntent.Unknown, result.Intent);
            Assert.False(result.IsObvious);
            Assert.Equal("Mixed subjects", result.Rationale);
            Assert.True(image.CanRead);
        }, "gpt-4o", "Choose the obvious next workflow");
    }

    [Fact]
    public async Task InvalidBeanResponse_ReturnsFailureRatherThanInventedFields()
    {
        await WithResponseAsync("not json", async service =>
        {
            using var image = new MemoryStream([1]);
            var result = await service.ExtractBeanLabelAsync(image);
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }, "gpt-4o-mini", "Extract the fields.");
    }

    [Fact]
    public async Task CallerCancellation_PreservesClassificationVersusAnalysisDifference()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = "http://127.0.0.1:1/",
                ["AzureOpenAI:ApiKey"] = "unused-test-value"
            }).Build();
        var service = CreateService(configuration);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var image = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ClassifyPhotoAsync(image, cancelled.Token));
        var analysis = await service.AnalyzeImageAsync(image, "Count", cancelled.Token);
        Assert.False(analysis.Success);
        Assert.Equal("Analysis timed out. Please try again.", analysis.ErrorMessage);
        var extraction = await service.ExtractBeanLabelAsync(image, cancelled.Token);
        Assert.False(extraction.Success);
        Assert.Equal("Extraction timed out. Please try again.", extraction.ErrorMessage);
    }

    private static VisionService CreateService(IConfiguration configuration) =>
        new(configuration, NullLogger<VisionService>.Instance);

    private static async Task WithResponseAsync(
        string message, Func<VisionService, Task> test, string model, string expectedPrompt)
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        using var listener = new HttpListener();
        var endpoint = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(endpoint);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var server = RespondAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = endpoint,
                ["AzureOpenAI:ApiKey"] = "unused-loopback-test-value"
            }).Build();
        try
        {
            await test(CreateService(configuration)).WaitAsync(timeout.Token);
            var request = await server.WaitAsync(timeout.Token);
            Assert.Contains($"/openai/deployments/{model}/chat/completions", request.Path);
            Assert.Contains(expectedPrompt, request.Body);
            Assert.Contains("data:image/jpeg;base64,", request.Body);
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
                id = "synthetic-loopback-response",
                @object = "chat.completion",
                created = 1,
                model,
                choices = new[]
                {
                    new { index = 0, message = new { role = "assistant", content = message }, finish_reason = "stop" }
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
}
