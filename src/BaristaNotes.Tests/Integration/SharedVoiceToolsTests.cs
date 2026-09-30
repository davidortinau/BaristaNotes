using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BaristaNotes.Core.Data;
using BaristaNotes.Core.Hosting;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using BaristaNotes.Services;
using BaristaNotes.Services.AI;
using BaristaNotes.Tests.Mocks;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace BaristaNotes.Tests.Integration;

public sealed class SharedVoiceToolsTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"barista-voice-{Guid.NewGuid():N}");
    private readonly TestPlatform _platform = new();
    private ServiceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);
        _provider = CreateProvider(Path.Combine(_directory, "voice.db"), new ConfigurationBuilder().Build());
        await _provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _provider.DisposeAsync();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void GeneratedToolNamesAndSchemas_AreAvailableWithoutMaui()
    {
        string[] expected =
        [
            "log_shot", "add_bean", "add_bag", "rate_last_shot", "add_equipment", "add_profile",
            "get_shot_count", "add_tasting_notes", "filter_shots", "get_last_shot", "find_shots",
            "get_bean_count", "find_beans", "get_bag_count", "find_bags", "get_equipment_count",
            "find_equipment", "get_profile_count", "find_profiles", "analyze_room_for_coffee",
            "get_available_pages", "navigate_to", "navigate_to_shot_detail", "navigate_to_profile_detail",
            "navigate_to_bean_detail", "navigate_to_equipment_detail", "navigate_to_bag_detail",
            "get_profile_context", "append_profile_context", "set_profile_context",
            "summarize_preferences_from_history", "identify_person_in_camera",
            "identify_person_in_photo_file", "list_available_beans", "open_roaster_url"
        ];
        var functions = VoiceTools.Default.Tools.OfType<AIFunction>().ToArray();
        Assert.Equal(expected, functions.Select(function => function.Name));
        Assert.All(functions, function => Assert.Equal(JsonValueKind.Object, function.JsonSchema.ValueKind));
    }

    [Fact]
    public async Task GeneratedTools_CreateAndUpdateRealDataAcrossScopes()
    {
        using (var scope = _provider.CreateScope())
        {
            var services = scope.ServiceProvider;
            Assert.Contains("Added profile", await Invoke(services, "add_profile", ("name", "Voice guest")));
            Assert.Contains("Added bean", await Invoke(services, "add_bean", ("name", "Voice coffee")));
            Assert.Contains("Added bag", await Invoke(services, "add_bag",
                ("beanName", "Voice coffee"), ("roastDate", "3 days ago")));
            Assert.Contains("Added grinder", await Invoke(services, "add_equipment",
                ("name", "Voice grinder"), ("type", "grinder")));
            Assert.Contains("Shot logged", await Invoke(services, "log_shot",
                ("doseGrams", 18.5), ("outputGrams", 37.0), ("timeSeconds", 29),
                ("tastingNotes", "Balanced")));
            Assert.Contains("Rated your last shot 0/4", await Invoke(services, "rate_last_shot", ("rating", 0)));
        }

        using var verification = _provider.CreateScope();
        var servicesAfter = verification.ServiceProvider;
        var profile = Assert.Single(await servicesAfter.GetRequiredService<IUserProfileService>().GetAllProfilesAsync());
        Assert.Equal("Voice guest", profile.Name);
        var bean = Assert.Single(await servicesAfter.GetRequiredService<IBeanService>().GetAllActiveBeansAsync());
        Assert.Equal("Voice coffee", bean.Name);
        var bag = Assert.Single(await servicesAfter.GetRequiredService<IBagService>().GetBagsForBeanAsync(bean.Id));
        Assert.Equal(DateTime.Today.AddDays(-3), bag.RoastDate);
        Assert.Single(await servicesAfter.GetRequiredService<IEquipmentService>().GetAllActiveEquipmentAsync());
        var shot = await servicesAfter.GetRequiredService<IShotService>().GetMostRecentShotAsync();
        Assert.NotNull(shot);
        Assert.Equal(18.5m, shot.DoseIn);
        Assert.Equal(37m, shot.ActualOutput);
        Assert.Equal(29m, shot.ActualTime);
        Assert.Equal(0, shot.Rating);
        Assert.Equal("Balanced", shot.TastingNotes);
    }

    [Fact]
    public async Task NavigationTools_EmitTypedPlatformRequests()
    {
        using var scope = _provider.CreateScope();
        var services = scope.ServiceProvider;
        await Invoke(services, "navigate_to", ("pageName", "activity"));
        await Invoke(services, "navigate_to_shot_detail", ("shotId", 7));
        await Invoke(services, "navigate_to_profile_detail", ("profileId", 8));
        await Invoke(services, "navigate_to_bean_detail", ("beanId", 9));
        await Invoke(services, "navigate_to_equipment_detail", ("equipmentId", 10));

        Assert.Equal(new[]
        {
            new VoiceNavigationRequest("//history"),
            new VoiceNavigationRequest("shot-logging", 7),
            new VoiceNavigationRequest("profile-form", 8),
            new VoiceNavigationRequest("bean-detail", 9),
            new VoiceNavigationRequest("equipment-detail", 10)
        }, _platform.Navigation);
        Assert.Contains("Unknown page", await Invoke(services, "navigate_to", ("pageName", "nonexistent destination")));
        Assert.Equal(5, _platform.Navigation.Count);
    }

    [Fact]
    public async Task ProfileContextTools_PreserveAppendCapAndClear()
    {
        using var scope = _provider.CreateScope();
        var services = scope.ServiceProvider;
        await Invoke(services, "add_profile", ("name", "Context guest"));
        var profile = Assert.Single(await services.GetRequiredService<IUserProfileService>().GetAllProfilesAsync());
        await Invoke(services, "append_profile_context", ("profileId", profile.Id), ("note", "  Likes filter coffee. "));
        await Invoke(services, "append_profile_context", ("profileId", profile.Id), ("note", " Avoids bitterness. "));
        var current = await services.GetRequiredService<IUserProfileService>().GetProfileByIdAsync(profile.Id);
        Assert.Equal("Likes filter coffee.\nAvoids bitterness.", current?.Context);
        Assert.Contains("Cannot append", await Invoke(services, "append_profile_context",
            ("profileId", profile.Id), ("note", new string('x', 2000))));
        await Invoke(services, "set_profile_context", ("profileId", profile.Id), ("context", ""));
        Assert.Null((await services.GetRequiredService<IUserProfileService>().GetProfileByIdAsync(profile.Id))?.Context);
    }

    [Fact]
    public async Task VoiceWithoutCloudConfiguration_PreservesUnavailableResponse()
    {
        using var scope = _provider.CreateScope();
        var voice = scope.ServiceProvider.GetRequiredService<IVoiceCommandService>();
        Assert.Same(voice, scope.ServiceProvider.GetRequiredService<VoiceCommandService>());
        var result = await voice.ProcessCommandAsync(new VoiceCommandRequestDto("log eighteen grams", 1));
        Assert.False(result.Success);
        Assert.Equal("Voice commands are temporarily unavailable.", result.Message);
    }

    [Fact]
    public async Task RoomTool_PausesBeforeCaptureAndDisposesCallerOwnedStream()
    {
        var vision = new Mock<IVisionService>(MockBehavior.Strict);
        vision.Setup(service => service.IsAvailableAsync()).ReturnsAsync(true);
        Stream? analyzed = null;
        vision.Setup(service => service.AnalyzeImageAsync(It.IsAny<Stream>(), "Count cups", It.IsAny<CancellationToken>()))
            .Callback<Stream, string, CancellationToken>((stream, _, _) => analyzed = stream)
            .ReturnsAsync(VisionAnalysisResult.Ok(3, "Three cups."));
        await using var provider = CreateProvider(Path.Combine(_directory, "room.db"),
            new ConfigurationBuilder().Build(), vision.Object);
        using var scope = provider.CreateScope();
        var paused = false;
        scope.ServiceProvider.GetRequiredService<IVoiceCommandService>().PauseSpeechRequested +=
            (_, _) => paused = true;
        _platform.Capture = options =>
        {
            Assert.True(paused);
            Assert.Equal(new VoiceCaptureOptions("Take a photo of the room", 1024, 1024, 70), options);
            return Task.FromResult<VoicePhoto?>(new VoicePhoto(
                "synthetic.jpg", () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]))));
        };

        var response = await Invoke(scope.ServiceProvider, "analyze_room_for_coffee", ("userQuestion", "Count cups"));

        Assert.Equal("Three cups.", response);
        Assert.NotNull(analyzed);
        Assert.False(analyzed.CanRead);
        vision.VerifyAll();
    }

    [Fact]
    public async Task BagAndBrowserTools_PassPlatformRequestsWithoutDoingUiOrNetworkWork()
    {
        using var scope = _provider.CreateScope();
        var services = scope.ServiceProvider;
        await Invoke(services, "add_bean", ("name", "Navigation bean"));
        await Invoke(services, "add_bag", ("beanName", "Navigation bean"));
        var bean = Assert.Single(await services.GetRequiredService<IBeanService>().GetAllActiveBeansAsync());
        var bag = Assert.Single(await services.GetRequiredService<IBagService>().GetBagsForBeanAsync(bean.Id));
        await Invoke(services, "navigate_to_bag_detail", ("bagId", bag.Id));
        Assert.Equal(new VoiceNavigationRequest("bag-detail", bag.Id, bean.Id, bean.Name),
            Assert.Single(_platform.Navigation));

        await Invoke(services, "open_roaster_url", ("beanId", bean.Id),
            ("suggestedUrl", "https://example.invalid/coffee"));
        Assert.Equal("https://example.invalid/coffee", Assert.Single(_platform.Opened).ToString());
        Assert.Equal("https://example.invalid/coffee",
            (await services.GetRequiredService<IBeanService>().GetBeanByIdAsync(bean.Id))?.RoasterUrl);
    }

    [Fact]
    public async Task VoicePipeline_UsesGeneratedToolThroughLoopbackAndPreservesHistory()
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        var endpoint = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(endpoint);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var requests = new List<string>();
        var server = RespondAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["AzureOpenAI:Endpoint"] = endpoint,
                ["AzureOpenAI:ApiKey"] = "unused-loopback-test-value"
            }).Build();
        await using var provider = CreateProvider(Path.Combine(_directory, "pipeline.db"), configuration);
        await provider.GetRequiredService<DatabaseInitializationService>().InitializeAsync();
        try
        {
            using var scope = provider.CreateScope();
            var voice = scope.ServiceProvider.GetRequiredService<IVoiceCommandService>();
            var response = await voice.ProcessCommandAsync(
                new VoiceCommandRequestDto("add a profile named Pipeline guest", 1), timeout.Token);
            Assert.True(response.Success, response.Message);
            Assert.Equal("Added the profile.", response.Message);
            var next = await voice.ProcessCommandAsync(
                new VoiceCommandRequestDto("use thirty four grams", 1), timeout.Token);
            Assert.True(next.Success);
            voice.ClearConversationHistory();
            Assert.True((await voice.ProcessCommandAsync(
                new VoiceCommandRequestDto("hello", 1), timeout.Token)).Success);
            await server.WaitAsync(timeout.Token);

            using var verification = provider.CreateScope();
            var profile = Assert.Single(await verification.ServiceProvider
                .GetRequiredService<IUserProfileService>().GetAllProfilesAsync());
            Assert.Equal("Pipeline guest", profile.Name);
            using var continued = JsonDocument.Parse(requests[2]);
            var messages = continued.RootElement.GetProperty("messages");
            Assert.True(messages.GetArrayLength() > 2);
            Assert.Contains("34", messages[messages.GetArrayLength() - 1].GetProperty("content").ToString());
            using var cleared = JsonDocument.Parse(requests[3]);
            Assert.Equal(2, cleared.RootElement.GetProperty("messages").GetArrayLength());
        }
        finally
        {
            timeout.Cancel();
            listener.Stop();
        }

        async Task RespondAsync()
        {
            for (var index = 0; index < 4; index++)
            {
                var context = await listener.GetContextAsync().WaitAsync(timeout.Token);
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                requests.Add(await reader.ReadToEndAsync(timeout.Token));
                object message = index == 0
                    ? new
                    {
                        role = "assistant",
                        tool_calls = new[]
                        {
                            new
                            {
                                id = "synthetic-tool-call",
                                type = "function",
                                function = new { name = "add_profile", arguments = "{\"name\":\"Pipeline guest\"}" }
                            }
                        }
                    }
                    : new { role = "assistant", content = "Added the profile." };
                var bytes = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    id = $"loopback-{index}",
                    @object = "chat.completion",
                    created = 1,
                    model = "gpt-4.1-mini",
                    choices = new[] { new { index = 0, message, finish_reason = index == 0 ? "tool_calls" : "stop" } },
                    usage = new { prompt_tokens = 1, completion_tokens = 1, total_tokens = 2 }
                });
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, timeout.Token);
                context.Response.Close();
            }
        }
    }

    private ServiceProvider CreateProvider(string database, IConfiguration configuration, IVisionService? vision = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPreferencesStore, MockPreferencesStore>();
        services.AddSingleton(Mock.Of<IImageProcessingService>());
        services.AddSingleton(configuration);
        services.AddSingleton<IVoicePlatformActions>(_platform);
        if (vision is not null)
            services.AddSingleton(vision);
        services.AddBaristaNotesCore(database).AddBaristaNotesVoice();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    private static async Task<string> Invoke(IServiceProvider services, string name,
        params (string Name, object? Value)[] values)
    {
        var function = VoiceTools.Default.Tools.OfType<AIFunction>().Single(tool => tool.Name == name);
        var arguments = new AIFunctionArguments(values.ToDictionary(item => item.Name, item => item.Value))
        {
            Services = services
        };
        return Assert.IsType<string>(await function.InvokeAsync(arguments));
    }

    private sealed class TestPlatform : IVoicePlatformActions
    {
        public List<VoiceNavigationRequest> Navigation { get; } = [];
        public List<Uri> Opened { get; } = [];
        public Func<VoiceCaptureOptions, Task<VoicePhoto?>>? Capture { get; set; }
        public bool IsCaptureSupported => Capture is not null;
        public void Navigate(VoiceNavigationRequest request) => Navigation.Add(request);
        public Task<VoicePhoto?> CapturePhotoAsync(VoiceCaptureOptions options) =>
            Capture?.Invoke(options) ?? Task.FromResult<VoicePhoto?>(null);
        public Task OpenBrowserAsync(Uri uri)
        {
            Opened.Add(uri);
            return Task.CompletedTask;
        }
    }
}
