using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using BaristaNotes.Core.Services;
using BaristaNotes.Core.Services.DTOs;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Tests.Unit;

[CollectionDefinition(nameof(NominatimRequestGateCollection), DisableParallelization = true)]
public sealed class NominatimRequestGateCollection { }

[Collection(nameof(NominatimRequestGateCollection))]
public sealed class NominatimOriginGeocoderTests : IDisposable
{
    internal const string GujiResponse = """
        [{"name":"Guji","lat":"5.5501401","lon":"39.2030529","category":"boundary",
          "type":"administrative","addresstype":"state_district","place_rank":12,
          "address":{"state_district":"Guji","state":"Oromia","country":"Ethiopia","country_code":"et"}}]
        """;
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "BaristaNotes-OriginTests-" + Guid.NewGuid().ToString("N"));
    private static OriginLookup Lookup => Assert.Single(new BeanOriginMapData([
        new BeanDto { Id = 1, Name = "Private bean name", Origin = "Guji, Ethiopia" }]).Lookups);

    public NominatimOriginGeocoderTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void PublicGujiAdministrativeResultIsARegionNotACityOrCountryPoint()
    {
        var result = NominatimOriginGeocoder.ParseResponse(GujiResponse, Lookup);
        Assert.Equal(OriginPrecision.Region, result.Precision);
        Assert.Equal("Guji", result.Name);
        Assert.Equal(5.5501401, result.Latitude);
        Assert.Equal(39.2030529, result.Longitude);
        Assert.NotEqual(Lookup.Country.Latitude, result.Latitude);
    }

    [Theory]
    [InlineData("country_code\":\"et", "country_code\":\"co")]
    [InlineData("\"place_rank\":12", "\"place_rank\":4")]
    [InlineData("\"lat\":\"5.5501401\"", "\"lat\":\"NaN\"")]
    [InlineData("\"lon\":\"39.2030529\"", "\"lon\":\"181\"")]
    [InlineData("\"addresstype\":\"state_district\"", "\"addresstype\":\"country\"")]
    [InlineData("\"addresstype\":\"state_district\"", "\"addresstype\":\"city\"")]
    [InlineData("\"category\":\"boundary\"", "\"category\":\"amenity\"")]
    [InlineData("\"name\":\"Guji\"", "\"name\":\"Another district\"")]
    public void MismatchedInvalidOrInsufficientPrecisionResultsAreRejected(string before, string after) =>
        Assert.Throws<InvalidDataException>(() => NominatimOriginGeocoder.ParseResponse(
            GujiResponse.Replace(before, after, StringComparison.Ordinal), Lookup));

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("[null]")]
    public void EmptyAndMalformedSearchShapesCannotBecomePreciseLocations(string response) =>
        Assert.Throws<InvalidDataException>(() => NominatimOriginGeocoder.ParseResponse(response, Lookup));

    [Fact]
    public void CityMetadataIsAcceptedSeparatelyFromAdministrativeRegionPrecision()
    {
        var response = GujiResponse.Replace("Guji", "Addis Ababa", StringComparison.Ordinal)
            .Replace("\"place_rank\":12", "\"place_rank\":16", StringComparison.Ordinal)
            .Replace("\"category\":\"boundary\"", "\"category\":\"place\"", StringComparison.Ordinal)
            .Replace("\"addresstype\":\"state_district\"", "\"addresstype\":\"city\"", StringComparison.Ordinal);
        var lookup = Assert.Single(new BeanOriginMapData([
            new BeanDto { Id = 1, Name = "Not transmitted", Origin = "Addis Ababa, Ethiopia" }]).Lookups);
        Assert.Equal(OriginPrecision.City, NominatimOriginGeocoder.ParseResponse(response, lookup).Precision);
    }

    [Fact]
    public async Task RepeatedViewAndNewClientReuseDurableValidatedSuccessWithoutSendingBeanName()
    {
        var calls = 0;
        using var client = Client((request, _) =>
        {
            calls++;
            Assert.Equal(NominatimOriginGeocoder.UserAgent, request.Headers.UserAgent.ToString());
            Assert.Contains("countrycodes=et", request.RequestUri!.Query);
            Assert.Contains("q=guji%2C%20Ethiopia", request.RequestUri.Query);
            Assert.DoesNotContain("Private", request.RequestUri.AbsoluteUri);
            Assert.DoesNotContain("lat=", request.RequestUri.Query);
            Assert.DoesNotContain("lon=", request.RequestUri.Query);
            return Task.FromResult(Response(GujiResponse));
        });
        var first = Geocoder(client);
        var result = await first.ResolveAsync(Lookup, false, CancellationToken.None);
        Assert.Null(result.Error);
        Assert.Equal(result, await first.ResolveAsync(Lookup, false, CancellationToken.None));
        Assert.Equal(result, await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None));
        Assert.Equal(result, await Geocoder(client).ResolveAsync(Lookup, true, CancellationToken.None));
        Assert.Equal(1, calls);
        Assert.Single(Directory.GetFiles(Path.Combine(_directory, "origin-geocode-cache"), "*.json"));
        Assert.Empty(Directory.GetFiles(Path.Combine(_directory, "origin-geocode-cache"), "*.tmp"));
    }

    [Fact]
    public async Task FailedLookupPersistsFallbackUntilExplicitRetryThenCachesRecoveredRegion()
    {
        var calls = 0;
        using var client = Client((_, _) => Task.FromResult(++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Response(GujiResponse)));
        var failed = await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None);
        Assert.Null(failed.Location);
        Assert.Contains("Retry origins", failed.Error);
        Assert.Equal(failed, await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None));
        Assert.Equal(1, calls);
        var recovered = await Geocoder(client).ResolveAsync(Lookup, true, CancellationToken.None);
        Assert.Equal(OriginPrecision.Region, recovered.Location!.Precision);
        Assert.Null(recovered.Error);
        Assert.Equal(recovered, await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RuntimeEndpointChangeUsesNewServiceAndCacheNamespaceWithoutRestart()
    {
        var hosts = new List<string>();
        using var client = Client((request, _) =>
        {
            hosts.Add(request.RequestUri!.Host);
            return Task.FromResult(Response(GujiResponse));
        });
        var geocoder = Geocoder(client);
        Assert.Null((await geocoder.ResolveAsync(Lookup, false, CancellationToken.None)).Error);
        await File.WriteAllTextAsync(Path.Combine(_directory, NominatimOriginGeocoder.SettingsFile),
            """{"endpoint":"https://replacement.example/search"}""");
        Assert.Null((await geocoder.ResolveAsync(Lookup, false, CancellationToken.None)).Error);
        Assert.Equal(new[] { "nominatim.openstreetmap.org", "replacement.example" }, hosts);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_directory, "origin-geocode-cache"), "*.json").Length);
        foreach (var endpoint in new[] { "http://invalid.example/search", "https://invalid.example/search?q=guji",
            "https://invalid.example/search#fragment", "https://user:password@invalid.example/search" })
        {
            await File.WriteAllTextAsync(Path.Combine(_directory, NominatimOriginGeocoder.SettingsFile),
                JsonSerializer.Serialize(new Dictionary<string, string> { ["endpoint"] = endpoint }));
            var invalid = await geocoder.ResolveAsync(Lookup, false, CancellationToken.None);
            Assert.Null(invalid.Location);
            Assert.Contains("settings", invalid.Error);
            Assert.Equal(2, hosts.Count);
        }
        await File.WriteAllTextAsync(Path.Combine(_directory, NominatimOriginGeocoder.SettingsFile),
            """{"endpoint":"https://replacement.example/search"}""");
        Assert.Null((await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None)).Error);
        await File.WriteAllTextAsync(Path.Combine(_directory, NominatimOriginGeocoder.SettingsFile),
            JsonSerializer.Serialize(new Dictionary<string, string> { ["endpoint"] = NominatimOriginGeocoder.DefaultEndpoint }));
        Assert.Null((await geocoder.ResolveAsync(Lookup, false, CancellationToken.None)).Error);
        Assert.Null((await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None)).Error);
        Assert.Equal(2, hosts.Count);
    }

    [Fact]
    public async Task MissingCountryRestrictionReportsFallbackWithoutSendingAnyRequest()
    {
        var calls = 0;
        using var client = Client((_, _) =>
        {
            calls++;
            throw new InvalidOperationException("An unrestricted request must never be sent.");
        });
        var lookup = Lookup with { Country = Lookup.Country with { CountryCode = null } };
        var result = await Geocoder(client).ResolveAsync(lookup, false, CancellationToken.None);
        Assert.Null(result.Location);
        Assert.Contains("country restriction", result.Error);
        Assert.Equal(0, calls);
        Assert.False(Directory.Exists(Path.Combine(_directory, "origin-geocode-cache")));
    }

    [Fact]
    public async Task PersistedSuccessIsRevalidatedAndCorruptCountryNeverTriggersAnAutomaticQuery()
    {
        var calls = 0;
        using var client = Client((_, _) =>
        {
            calls++;
            return Task.FromResult(Response(GujiResponse));
        });
        Assert.Null((await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None)).Error);
        var path = Assert.Single(Directory.GetFiles(Path.Combine(_directory, "origin-geocode-cache"), "*.json"));
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["response"] = GujiResponse.Replace("\"et\"", "\"co\"", StringComparison.Ordinal)
        }));
        var geocoder = Geocoder(client);
        var corrupted = await geocoder.ResolveAsync(Lookup, false, CancellationToken.None);
        Assert.Null(corrupted.Location);
        Assert.Contains("validated", corrupted.Error);
        Assert.Equal(corrupted, await geocoder.ResolveAsync(Lookup, false, CancellationToken.None));
        Assert.Equal(corrupted, await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None));
        Assert.Equal(1, calls);
        Assert.Null((await Geocoder(client).ResolveAsync(Lookup, true, CancellationToken.None)).Error);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RequestsAcrossClientsAreSerialAndStartAtLeastOneSecondApart()
    {
        var starts = new List<long>();
        var active = 0;
        var maximumActive = 0;
        using var client = Client(async (_, token) =>
        {
            starts.Add(Stopwatch.GetTimestamp());
            maximumActive = Math.Max(maximumActive, ++active);
            await Task.Delay(50, token);
            active--;
            return Response(GujiResponse);
        });
        var otherDirectory = Path.Combine(_directory, "second-client");
        await Task.WhenAll(
            Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None),
            new NominatimOriginGeocoder(client, otherDirectory, NullLogger.Instance)
                .ResolveAsync(Lookup, false, CancellationToken.None));
        Assert.Equal(1, maximumActive);
        Assert.Equal(2, starts.Count);
        Assert.True(Stopwatch.GetElapsedTime(starts[0], starts[1]) >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CancellationDoesNotPersistARecoverableFailureOrInventAPosition()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = Client(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Response(GujiResponse);
        });
        using var cancellation = new CancellationTokenSource();
        var pending = Geocoder(client).ResolveAsync(Lookup, false, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.False(Directory.Exists(Path.Combine(_directory, "origin-geocode-cache")));
    }

    [Fact]
    public async Task StalledRequestTimesOutWithinTheTwelveSecondBudgetAndPersistsExplicitFallback()
    {
        using var client = Client(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Response(GujiResponse);
        });
        var started = Stopwatch.GetTimestamp();
        var failed = await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None);
        Assert.InRange(Stopwatch.GetElapsedTime(started).TotalSeconds, 11.5, 16);
        Assert.Null(failed.Location);
        Assert.Contains("timed out", failed.Error);
        Assert.Equal(failed, await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None));
    }

    [Fact]
    public async Task CacheSaveFailureIsExplicitAndRetryRepairsItWithoutRepeatingSuccessfulQuery()
    {
        var calls = 0;
        var blocked = Path.Combine(_directory, "origin-geocode-cache");
        await File.WriteAllTextAsync(blocked, "test-only cache-directory obstruction");
        using var client = Client((_, _) =>
        {
            calls++;
            return Task.FromResult(Response(GujiResponse));
        });
        var geocoder = Geocoder(client);
        var result = await geocoder.ResolveAsync(Lookup, false, CancellationToken.None);
        Assert.NotNull(result.Location);
        Assert.Contains("cache could not be saved", result.Error);
        File.Delete(blocked);
        Assert.Null((await geocoder.ResolveAsync(Lookup, true, CancellationToken.None)).Error);
        Assert.Equal(1, calls);
        Assert.Null((await Geocoder(client).ResolveAsync(Lookup, false, CancellationToken.None)).Error);
        Assert.Equal(1, calls);
    }

    private NominatimOriginGeocoder Geocoder(HttpClient client) => new(client, _directory, NullLogger.Instance);
    internal static HttpClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new Handler(send));
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
    internal static HttpResponseMessage Response(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
