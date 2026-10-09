using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services.Origins;

public sealed class NominatimOriginGeocoder
{
    public const string DefaultEndpoint = "https://nominatim.openstreetmap.org/search";
    public const string EndpointPreference = "origin_geocoder_endpoint";
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(12);
    private static readonly SemaphoreSlim Requests = new(1, 1);
    private static long _lastRequestFinished;
    private static long _cooldownStarted;
    private static TimeSpan _cooldown;
    private readonly HttpClient _http;
    private readonly IPreferencesStore _store;
    private readonly ILogger<NominatimOriginGeocoder> _logger;

    public NominatimOriginGeocoder(HttpClient http, IPreferencesStore store,
        ILogger<NominatimOriginGeocoder> logger)
    {
        _http = http;
        _store = store;
        _logger = logger;
    }

    public string Endpoint
    {
        get => ValidateEndpoint(_store.Get(EndpointPreference, DefaultEndpoint) ?? DefaultEndpoint);
        set => _store.Set(EndpointPreference, ValidateEndpoint(value));
    }

    private static string ValidateEndpoint(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Use an absolute HTTPS search endpoint without credentials, query or fragment.");
        return uri.AbsoluteUri;
    }

    public async Task<OriginLookup> ResolveAsync(OriginQuery query, bool retryUnresolved,
        CancellationToken cancellationToken)
    {
        if (!query.NeedsLookup)
            throw new ArgumentException("Country-only origins do not require geocoding.", nameof(query));
        var endpoint = Endpoint;
        var hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(endpoint + "\n" + query.Country.CountryCode + "\n" + query.Key)));
        var cacheKey = "origin_lookup_v1_" + hash;

        await Requests.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cachedJson = _store.Get(cacheKey, null);
            if (cachedJson is not null)
            {
                OriginLookup cached;
                try
                {
                    cached = JsonSerializer.Deserialize(cachedJson, GeocoderJsonContext.Default.OriginLookup)
                        ?? throw new InvalidDataException("The saved origin lookup is empty.");
                    if (cached.Place is { } place && !IsUsable(place, query))
                        throw new InvalidDataException("The saved origin lookup does not match its country or place.");
                    if (cached.Place is null && string.IsNullOrWhiteSpace(cached.Error))
                        throw new InvalidDataException("The saved unresolved origin has no error information.");
                }
                catch (Exception ex) when (ex is JsonException or InvalidDataException)
                {
                    _logger.LogWarning(ex, "Saved origin lookup is invalid");
                    cached = new(null, "A cached place lookup is invalid. Country locations remain approximate; Retry map to retry.");
                    _store.Set(cacheKey, JsonSerializer.Serialize(cached, GeocoderJsonContext.Default.OriginLookup));
                }
                if (cached.Place is not null || !retryUnresolved)
                    return cached;
            }

            OriginLookup lookup;
            if (query.Country.CountryCode is not { Length: 2 } code)
                lookup = new(null, "This country has no supported country code. Its location remains approximate.");
            else
            {
                var delay = _lastRequestFinished == 0 ? TimeSpan.Zero :
                    TimeSpan.FromSeconds(1) - Stopwatch.GetElapsedTime(_lastRequestFinished);
                var cooldownRemaining = _cooldownStarted == 0 ? TimeSpan.Zero :
                    _cooldown - Stopwatch.GetElapsedTime(_cooldownStarted);
                if (cooldownRemaining > delay)
                    delay = cooldownRemaining;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(RequestTimeout);
                try
                {
                    var url = endpoint + "?format=jsonv2&addressdetails=1&limit=3&accept-language=en" +
                        "&countrycodes=" + Uri.EscapeDataString(code) +
                        "&q=" + Uri.EscapeDataString(query.Text);
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.UserAgent.ParseAdd(
                        "BaristaNotes-MAUI-Maps/1.0 (single-user-origin-comparison-prototype)");
                    using var response = await _http.SendAsync(request,
                        HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode && response.Headers.RetryAfter is { } retryAfter)
                    {
                        var cooldown = retryAfter.Delta ??
                            (retryAfter.Date is { } date ? date - DateTimeOffset.UtcNow : TimeSpan.Zero);
                        if (cooldown > TimeSpan.Zero)
                        {
                            _cooldownStarted = Stopwatch.GetTimestamp();
                            _cooldown = cooldown;
                            _logger.LogWarning("Origin server requested a cooldown of {Cooldown}", cooldown);
                        }
                    }
                    response.EnsureSuccessStatusCode();
                    await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                    using var document = await JsonDocument.ParseAsync(stream,
                        cancellationToken: timeout.Token).ConfigureAwait(false);
                    lookup = Parse(document.RootElement, query);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Origin geocoding timed out");
                    lookup = new(null, "Place lookup timed out. Country locations remain approximate; Retry map to retry.");
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException)
                {
                    _logger.LogWarning(ex, "Origin geocoding request failed");
                    lookup = new(null, "Place lookup failed. Check your connection; Retry map to retry approximate locations.");
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Origin geocoder returned invalid JSON");
                    lookup = new(null, "The geocoder returned an invalid response. Country locations remain approximate; Retry map to retry.");
                }
                finally
                {
                    // This is process-local prototype throttling, not production-wide rate control.
                    _lastRequestFinished = Stopwatch.GetTimestamp();
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            _store.Set(cacheKey, JsonSerializer.Serialize(lookup, GeocoderJsonContext.Default.OriginLookup));
            return lookup;
        }
        finally
        {
            Requests.Release();
        }
    }

    internal static OriginLookup Parse(JsonElement results, OriginQuery query)
    {
        if (results.ValueKind != JsonValueKind.Array)
            return new(null, "The geocoder returned an invalid response. Country locations remain approximate; Retry map to retry.");
        foreach (var result in results.EnumerateArray())
        {
            if (result.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty("place_rank", out var rankElement) ||
                rankElement.ValueKind != JsonValueKind.Number || !rankElement.TryGetInt32(out var rank) ||
                !double.TryParse(Text(result, "lat"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(Text(result, "lon"), NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                continue;
            var category = Text(result, "category");
            var type = Text(result, "type");
            var addressType = Text(result, "addresstype");
            OriginPrecision precision;
            if (rank is >= 8 and <= 16 &&
                ((category == "boundary" && type == "administrative") ||
                 (category == "place" && type == addressType)) &&
                addressType is "state" or "state_district" or "county" or "region" or "district" or "municipality")
                precision = OriginPrecision.Region;
            else if (rank is >= 13 and <= 22 &&
                ((category == "place" && type == addressType) ||
                 (category == "boundary" && type == "administrative")) &&
                addressType is "city" or "town" or "village" or "hamlet" or "suburb" or "neighbourhood")
                precision = addressType == "city" ? OriginPrecision.City : OriginPrecision.Locality;
            else
                continue;
            var name = Text(result, "name");
            if (name.Length == 0)
                name = Text(address, addressType);
            var place = new OriginPlace(name, Text(address, "country_code").ToLowerInvariant(), lon, lat, precision)
            {
                AddressHierarchy = string.Join('\n', address.EnumerateObject()
                    .Where(property => property.Name != "country_code" && property.Value.ValueKind == JsonValueKind.String)
                    .Select(property => property.Value.GetString() ?? "")
                    .Where(value => !string.IsNullOrWhiteSpace(value)))
            };
            if (IsUsable(place, query))
                return new(place, null);
        }
        return new(null, "No matching city or region was found. Country locations remain approximate; Retry map to retry.");
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? "" : "";

    internal static bool IsUsable(OriginPlace place, OriginQuery query) =>
        query.NeedsLookup && !string.IsNullOrWhiteSpace(place.Name) &&
        query.Country.CountryCode is { Length: 2 } code &&
        string.Equals(place.CountryCode, code, StringComparison.OrdinalIgnoreCase) &&
        double.IsFinite(place.Longitude) && double.IsFinite(place.Latitude) &&
        Math.Abs(place.Longitude) <= 180 && Math.Abs(place.Latitude) < 90 &&
        place.Precision is OriginPrecision.Region or OriginPrecision.City or OriginPrecision.Locality &&
        MatchesNames(place, query);

    private static bool MatchesNames(OriginPlace place, OriginQuery query)
    {
        if (place.AddressHierarchy is null)
            return false;
        var details = query.DetailNames.Length > 0 ? query.DetailNames : [query.Detail];
        var name = " " + BeanOriginResolver.Normalize(place.Name) + " ";
        var hierarchy = place.AddressHierarchy.Split('\n', StringSplitOptions.RemoveEmptyEntries).Append(place.Name)
            .Select(value => " " + BeanOriginResolver.Normalize(value) + " ").ToArray();
        return name.Contains(" " + details[0] + " ", StringComparison.Ordinal) &&
            details.All(detail => hierarchy.Any(value => value.Contains(" " + detail + " ", StringComparison.Ordinal)));
    }
}

[JsonSerializable(typeof(OriginLookup))]
internal partial class GeocoderJsonContext : JsonSerializerContext
{
}
