using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BaristaNotes.Core.Services.Origins;
using BaristaNotes.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Tests.Unit.Services;

public sealed class NominatimOriginGeocoderTests
{
    // Supplied public response; these coordinates exist only in tests.
    private const string GujiResponse = """
        [{"lat":"5.5501401","lon":"39.2030529","category":"boundary","type":"administrative",
          "place_rank":10,"addresstype":"state_district","name":"Guji",
          "address":{"state_district":"Guji","country_code":"et"}}]
        """;
    private readonly OriginQuery _query = new BeanOriginResolver().Queries("Guji, Ethiopia").Single();

    [Fact]
    public async Task ParentRegion_IsValidatedAndRetainedAcrossCacheAndSnapshot()
    {
        var resolver = new BeanOriginResolver();
        var query = Assert.Single(resolver.Queries("Guji, Oromia, Ethiopia"));
        var response = GujiResponse.Replace("\"country_code\":\"et\"", "\"state\":\"Oromia\",\"country_code\":\"et\"");
        var store = new MockPreferencesStore();
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(response)));
        using var http = new HttpClient(handler);
        var first = await Client(http, store).ResolveAsync(query, false, CancellationToken.None);
        var cached = await Client(http, store).ResolveAsync(query, false, CancellationToken.None);
        Assert.NotNull(first.Place);
        Assert.NotNull(cached.Place);
        Assert.Equal("Guji", cached.Place.Name);
        Assert.Contains("Oromia", cached.Place.AddressHierarchy);
        Assert.Single(handler.Urls);
        Assert.True(NominatimOriginGeocoder.IsUsable(cached.Place, query));
        Assert.False(NominatimOriginGeocoder.IsUsable(cached.Place with { Name = "Oromia" }, query));
        Assert.False(NominatimOriginGeocoder.IsUsable(cached.Place with { AddressHierarchy = "" }, query));
        var bean = new BaristaNotes.Core.Services.DTOs.BeanDto
        {
            Id = 99, Name = "Qualified origin", Origin = query.Text
        };
        var snapshot = resolver.Build([bean], new Dictionary<string, OriginLookup> { [query.Key] = cached });
        Assert.Equal(OriginPrecision.Region, Assert.Single(snapshot.Locations).Precision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetryAfter_DelaysTheNextRequestAcrossClients(bool dateHeader)
    {
        using var handler = new StubHandler((_, _) =>
        {
            if (dateHeader)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                {
                    Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(DateTimeOffset.UtcNow.AddSeconds(3)) }
                });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Headers = { RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2)) }
            });
        });
        using var http = new HttpClient(handler);
        await Client(http, new MockPreferencesStore()).ResolveAsync(_query, false, CancellationToken.None);
        using var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Client(http, new MockPreferencesStore()).ResolveAsync(_query, false, canceled.Token));
        Assert.Single(handler.Starts);
        await Client(http, new MockPreferencesStore()).ResolveAsync(_query, false, CancellationToken.None);
        Assert.Equal(2, handler.Starts.Count);
        Assert.True(Stopwatch.GetElapsedTime(handler.Starts[0], handler.Starts[1]) >= TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void PublicGujiResponse_IsARegionAndNotTheCountryPoint()
    {
        var result = Parse(GujiResponse);
        Assert.Null(result.Error);
        Assert.NotNull(result.Place);
        Assert.Equal(OriginPrecision.Region, result.Place.Precision);
        Assert.Equal(5.5501401, result.Place.Latitude);
        Assert.Equal(39.2030529, result.Place.Longitude);
        Assert.NotEqual(_query.Country.Latitude, result.Place.Latitude);
        Assert.NotEqual(_query.Country.Longitude, result.Place.Longitude);
    }

    [Theory]
    [InlineData("et", "4", "country", "Guji", "5.5", "39.2", "boundary", "administrative")]
    [InlineData("co", "10", "state_district", "Guji", "5.5", "39.2", "boundary", "administrative")]
    [InlineData("et", "10", "state_district", "Other region", "5.5", "39.2", "boundary", "administrative")]
    [InlineData("et", "10", "state_district", "Guji", "NaN", "39.2", "boundary", "administrative")]
    [InlineData("et", "10", "state_district", "Guji", "91", "39.2", "boundary", "administrative")]
    [InlineData("et", "10", "state_district", "Guji", "5.5", "181", "boundary", "administrative")]
    [InlineData("et", "10", "state_district", "Guji", "5.5", "39.2", "amenity", "cafe")]
    [InlineData("et", "30", "city", "Guji", "5.5", "39.2", "place", "city")]
    [InlineData("", "10", "state_district", "Guji", "5.5", "39.2", "boundary", "administrative")]
    public void InvalidMismatchedOrCountryResults_NeverBecomePrecise(string country, string rank,
        string addressType, string name, string latitude, string longitude, string category, string type)
    {
        var result = Parse($$$"""
            [{"lat":"{{{latitude}}}","lon":"{{{longitude}}}","category":"{{{category}}}","type":"{{{type}}}",
              "place_rank":{{{rank}}},"addresstype":"{{{addressType}}}","name":"{{{name}}}",
              "address":{"country_code":"{{{country}}}"}}]
            """);
        Assert.Null(result.Place);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Theory]
    [InlineData("city", "place", "city", OriginPrecision.City)]
    [InlineData("city", "boundary", "administrative", OriginPrecision.City)]
    [InlineData("village", "place", "village", OriginPrecision.Locality)]
    public void ReliableLocalities_AreLabelledWithoutInventingCityPrecision(
        string addressType, string category, string type, OriginPrecision precision)
    {
        var result = Parse($$$"""
            [{"lat":"5.5","lon":"39.2","category":"{{{category}}}","type":"{{{type}}}",
              "place_rank":16,"addresstype":"{{{addressType}}}","name":"Guji",
              "address":{"country_code":"et"}}]
            """);
        Assert.Equal(precision, result.Place?.Precision);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("[null,{}]")]
    public void UnresolvedOrMalformedResponse_HasExplicitApproximateFeedback(string json)
    {
        var result = Parse(json);
        Assert.Null(result.Place);
        Assert.Contains("approximate", result.Error);
    }

    [Fact]
    public async Task SuccessfulLookup_ReusesPersistentCacheAcrossClientsAndExplicitRetry()
    {
        var store = new MockPreferencesStore();
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(GujiResponse)));
        using var http = new HttpClient(handler);
        var first = await Client(http, store).ResolveAsync(_query, false, CancellationToken.None);
        var restarted = await Client(http, store).ResolveAsync(_query, false, CancellationToken.None);
        var retried = await Client(http, store).ResolveAsync(_query, true, CancellationToken.None);
        Assert.Equal(first, restarted);
        Assert.Equal(first, retried);
        Assert.Single(handler.Urls);
        var url = Uri.UnescapeDataString(handler.Urls[0]);
        Assert.Contains("format=jsonv2", url);
        Assert.Contains("addressdetails=1", url);
        Assert.Contains("limit=3", url);
        Assert.Contains("countrycodes=et", url);
        Assert.Contains("accept-language=en", url);
        Assert.Contains("q=Guji, Ethiopia", url);
        Assert.DoesNotContain("&city=", url);
        Assert.DoesNotContain("&country=", url);
        Assert.DoesNotContain("bean", url);
        Assert.Contains("BaristaNotes-MAUI-Maps/1.0", Assert.Single(handler.UserAgents));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnresolvedAndFailedLookups_ArePersistedAndOnlyExplicitRetryQueriesAgain(bool failed)
    {
        var store = new MockPreferencesStore();
        using var handler = new StubHandler((_, _) => Task.FromResult(
            failed ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Response("[]")));
        using var http = new HttpClient(handler);
        var first = await Client(http, store).ResolveAsync(_query, false, CancellationToken.None);
        Assert.Null(first.Place);
        Assert.NotNull(first.Error);
        Assert.Equal(first, await Client(http, store).ResolveAsync(_query, false, CancellationToken.None));
        Assert.Single(handler.Urls);
        await Client(http, store).ResolveAsync(_query, true, CancellationToken.None);
        Assert.Equal(2, handler.Urls.Count);
    }

    [Fact]
    public async Task EndpointChangesAtRuntime_KeepCachesSeparateWithoutLosingOldSuccess()
    {
        var store = new MockPreferencesStore();
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(GujiResponse)));
        using var http = new HttpClient(handler);
        var client = Client(http, store);
        await client.ResolveAsync(_query, false, CancellationToken.None);
        client.Endpoint = "https://geocoder.example/search";
        await client.ResolveAsync(_query, false, CancellationToken.None);
        Assert.Equal("geocoder.example", new Uri(handler.Urls[1]).Host);
        client.Endpoint = NominatimOriginGeocoder.DefaultEndpoint;
        await client.ResolveAsync(_query, false, CancellationToken.None);
        Assert.Equal(2, handler.Urls.Count);
    }

    [Theory]
    [InlineData("http://example.com/search")]
    [InlineData("https://identity@example.com/search")]
    [InlineData("https://example.com/search?q=origin")]
    [InlineData("not an endpoint")]
    public void InvalidEndpoint_IsRejectedWithoutOverwritingExistingSetting(string endpoint)
    {
        using var http = new HttpClient();
        var client = Client(http, new MockPreferencesStore());
        Assert.Throws<ArgumentException>(() => client.Endpoint = endpoint);
        Assert.Equal(NominatimOriginGeocoder.DefaultEndpoint, client.Endpoint);
    }

    [Fact]
    public async Task Requests_AreSerialAndAtLeastOneSecondApartAcrossClients()
    {
        using var handler = new StubHandler(async (_, token) =>
        {
            await Task.Delay(30, token);
            return Response("[]");
        });
        using var http = new HttpClient(handler);
        await Task.WhenAll(
            Client(http, new MockPreferencesStore()).ResolveAsync(_query, false, CancellationToken.None),
            Client(http, new MockPreferencesStore()).ResolveAsync(_query, false, CancellationToken.None));
        Assert.Equal(1, handler.MaximumActive);
        Assert.Equal(2, handler.Starts.Count);
        Assert.True(Stopwatch.GetElapsedTime(handler.Starts[0], handler.Starts[1]) >= TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CountryOnlyAndUnsupportedCountryOrigins_NeverSendRequests()
    {
        using var handler = new StubHandler((_, _) => throw new InvalidOperationException("No request is allowed."));
        using var http = new HttpClient(handler);
        var client = Client(http, new MockPreferencesStore());
        var resolver = new BeanOriginResolver();
        await Assert.ThrowsAsync<ArgumentException>(() => client.ResolveAsync(
            resolver.Queries("Ethiopia").Single(), false, CancellationToken.None));
        var unsupported = await client.ResolveAsync(
            resolver.Queries("A district, Somaliland").Single(), false, CancellationToken.None);
        Assert.Null(unsupported.Place);
        Assert.Contains("approximate", unsupported.Error);
        Assert.Empty(handler.Urls);
    }

    [Fact]
    public async Task Cancellation_DoesNotPersistAFailureOrPreventNextRequest()
    {
        var store = new MockPreferencesStore();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHandler(async (_, token) =>
        {
            if (!cancellation.IsCancellationRequested)
            {
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            return Response(GujiResponse);
        });
        using var http = new HttpClient(handler);
        var request = Client(http, store).ResolveAsync(_query, false, cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        var resolved = await Client(http, store).ResolveAsync(_query, false, CancellationToken.None);
        Assert.NotNull(resolved.Place);
        Assert.Equal(2, handler.Urls.Count);
    }

    [Fact]
    public async Task RealTimeoutToken_BoundsAnUnresponsiveHandler()
    {
        var started = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHandler(async (_, token) =>
        {
            started.SetResult(Stopwatch.GetTimestamp());
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("The timeout did not cancel the request.");
        });
        using var http = new HttpClient(handler);
        var request = Client(http, new MockPreferencesStore()).ResolveAsync(_query, false, CancellationToken.None);
        var start = await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var result = await request.WaitAsync(NominatimOriginGeocoder.RequestTimeout + TimeSpan.FromSeconds(5));
        var elapsed = Stopwatch.GetElapsedTime(start);
        Assert.InRange(elapsed.TotalSeconds, 11.5, 17);
        Assert.Null(result.Place);
        Assert.Contains("timed out", result.Error);
    }

    [Theory]
    [InlineData("invalid json")]
    [InlineData("""{"Place":{"Name":"Guji","CountryCode":"co","Longitude":39.2,"Latitude":5.5,"Precision":1},"Error":null}""")]
    public async Task CorruptOrMismatchedCache_IsApproximateAndExplicitlyRecoverable(string corrupt)
    {
        var store = new MockPreferencesStore();
        using var handler = new StubHandler((_, _) => Task.FromResult(Response(GujiResponse)));
        using var http = new HttpClient(handler);
        var client = Client(http, store);
        await client.ResolveAsync(_query, false, CancellationToken.None);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            client.Endpoint + "\n" + _query.Country.CountryCode + "\n" + _query.Key)));
        store.Set("origin_lookup_v1_" + hash, corrupt);
        var invalid = await client.ResolveAsync(_query, false, CancellationToken.None);
        Assert.Null(invalid.Place);
        Assert.Contains("cached place lookup is invalid", invalid.Error);
        Assert.Single(handler.Urls);
        var recovered = await client.ResolveAsync(_query, true, CancellationToken.None);
        Assert.NotNull(recovered.Place);
        Assert.Equal(2, handler.Urls.Count);
    }

    [Fact]
    public async Task TimeoutOrInvalidJson_HasRecoverableFeedback()
    {
        using var timeoutHandler = new StubHandler((_, _) => throw new OperationCanceledException());
        using var timeoutHttp = new HttpClient(timeoutHandler);
        var timeout = await Client(timeoutHttp, new MockPreferencesStore())
            .ResolveAsync(_query, false, CancellationToken.None);
        Assert.Null(timeout.Place);
        Assert.Contains("timed out", timeout.Error);
        using var invalidHandler = new StubHandler((_, _) => Task.FromResult(Response("not json")));
        using var invalidHttp = new HttpClient(invalidHandler);
        var invalid = await Client(invalidHttp, new MockPreferencesStore())
            .ResolveAsync(_query, false, CancellationToken.None);
        Assert.Null(invalid.Place);
        Assert.Contains("invalid response", invalid.Error);
    }

    private OriginLookup Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return NominatimOriginGeocoder.Parse(document.RootElement, _query);
    }

    private static NominatimOriginGeocoder Client(HttpClient http, MockPreferencesStore store) =>
        new(http, store, NullLogger<NominatimOriginGeocoder>.Instance);

    private static HttpResponseMessage Response(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json) };

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];
        public List<string> UserAgents { get; } = [];
        public List<long> Starts { get; } = [];
        public int MaximumActive { get; private set; }
        private int _active;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            lock (Urls)
            {
                Urls.Add(request.RequestUri!.AbsoluteUri);
                UserAgents.Add(request.Headers.UserAgent.ToString());
                Starts.Add(Stopwatch.GetTimestamp());
                MaximumActive = Math.Max(MaximumActive, ++_active);
            }
            try
            {
                return await respond(request, token);
            }
            finally
            {
                lock (Urls)
                    _active--;
            }
        }
    }
}
