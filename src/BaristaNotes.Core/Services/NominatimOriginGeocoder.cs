using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BaristaNotes.Core.Services;

public sealed record OriginLookupResult(OriginLocation? Location, string? Error);

public interface IOriginGeocoder
{
    Task<OriginLookupResult> ResolveAsync(OriginLookup lookup, bool retryFailure, CancellationToken cancellationToken);
}

public sealed class NominatimOriginGeocoder(HttpClient client, string dataDirectory, ILogger logger) : IOriginGeocoder
{
    public const string DefaultEndpoint = "https://nominatim.openstreetmap.org/search";
    public const string UserAgent = "BaristaNotes-Native-OriginPrototype/0.1";
    public const string SettingsFile = "origin-geocoding.json";
    private const int MaximumResponseBytes = 128 * 1024;
    private static readonly SemaphoreSlim RequestGate = new(1, 1);
    private static long _lastRequest;
    private static TimeSpan _minimumDelay = TimeSpan.FromSeconds(1);
    private readonly Dictionary<string, OriginLookupResult> _memory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _successfulResponses = new(StringComparer.Ordinal);

    public async Task<OriginLookupResult> ResolveAsync(OriginLookup lookup, bool retryFailure, CancellationToken cancellationToken)
    {
        await RequestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var (endpoint, userAgent) = await ReadSettingsAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (lookup.Country.CountryCode is not { Length: 2 } countryCode)
                throw new InvalidDataException("This catalogue country has no supported ISO country restriction.");
            var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"v1|{endpoint.AbsoluteUri}|{countryCode}|{BeanOriginMapData.Normalize(lookup.Detail)}")));
            var cachePath = Path.Combine(dataDirectory, "origin-geocode-cache", key + ".json");
            if (_memory.TryGetValue(key, out var remembered) && (!retryFailure || remembered.Error == null))
            {
                logger.LogDebug("Native origin lookup reused from memory; failed={Failed}", remembered.Error != null);
                return remembered;
            }
            string? response = null;
            if (File.Exists(cachePath))
            {
                try
                {
                    using var cached = JsonDocument.Parse(await File.ReadAllTextAsync(cachePath, cancellationToken).ConfigureAwait(false));
                    if (cached.RootElement.TryGetProperty("response", out var cachedResponse))
                    {
                        response = cachedResponse.GetString() ?? throw new InvalidDataException("The origin cache response is empty.");
                        var location = ParseResponse(response, lookup);
                        cancellationToken.ThrowIfCancellationRequested();
                        _successfulResponses[key] = response;
                        logger.LogDebug("Native origin lookup reused from validated persistent cache");
                        return _memory[key] = new OriginLookupResult(location, null);
                    }
                    var failure = cached.RootElement.GetProperty("failure").GetString()
                        ?? throw new InvalidDataException("The origin cache failure is empty.");
                    if (string.IsNullOrWhiteSpace(failure))
                        throw new InvalidDataException("The origin cache failure is empty.");
                    if (!retryFailure)
                    {
                        logger.LogDebug("Native origin lookup retained cached failure; explicit retry required");
                        return _memory[key] = new OriginLookupResult(null, failure);
                    }
                }
                catch (Exception error) when (IsRecoverable(error))
                {
                    logger.LogWarning(error, "Native origin cache read or validation failed");
                    if (!retryFailure)
                        return _memory[key] = new OriginLookupResult(null,
                            "A cached origin could not be validated. Approximate country locations remain; use Retry origins.");
                    response = null;
                }
            }

            OriginLookupResult result;
            try
            {
                if (_successfulResponses.TryGetValue(key, out var successfulResponse))
                {
                    response = successfulResponse;
                    result = new OriginLookupResult(ParseResponse(response, lookup), null);
                }
                else
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(12));
                    while (_lastRequest != 0)
                    {
                        var elapsed = Stopwatch.GetElapsedTime(_lastRequest);
                        if (elapsed >= _minimumDelay) break;
                        await Task.Delay(_minimumDelay - elapsed + TimeSpan.FromMilliseconds(1), timeout.Token).ConfigureAwait(false);
                    }
                    using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(endpoint.AbsoluteUri +
                        "?format=jsonv2&addressdetails=1&limit=5&accept-language=en&countrycodes=" + countryCode +
                        "&q=" + Uri.EscapeDataString(lookup.Detail + ", " + lookup.Country.Name)));
                    request.Headers.UserAgent.ParseAdd(userAgent);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    _lastRequest = Stopwatch.GetTimestamp();
                    using var httpResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token)
                        .ConfigureAwait(false);
                    var retryAfter = httpResponse.Headers.RetryAfter;
                    var requestedDelay = retryAfter?.Delta ?? (retryAfter?.Date is { } date
                        ? date - DateTimeOffset.UtcNow : TimeSpan.FromSeconds(1));
                    _minimumDelay = requestedDelay > TimeSpan.FromSeconds(1) ? requestedDelay : TimeSpan.FromSeconds(1);
                    if (retryAfter != null && requestedDelay > TimeSpan.Zero) _lastRequest = Stopwatch.GetTimestamp();
                    httpResponse.EnsureSuccessStatusCode();
                    await using var stream = await httpResponse.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
                    using var body = new MemoryStream();
                    var buffer = new byte[4096];
                    int read;
                    while ((read = await stream.ReadAsync(buffer, timeout.Token).ConfigureAwait(false)) != 0)
                    {
                        if (body.Length + read > MaximumResponseBytes)
                            throw new InvalidDataException("The geocoder response exceeded the prototype size limit.");
                        await body.WriteAsync(buffer.AsMemory(0, read), timeout.Token).ConfigureAwait(false);
                    }
                    response = Encoding.UTF8.GetString(body.ToArray());
                    result = new OriginLookupResult(ParseResponse(response, lookup), null);
                    _successfulResponses[key] = response;
                    logger.LogInformation("Native detailed origin resolved with validated country; precision={Precision}", result.Location!.Precision);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Native origin lookup timed out");
                response = null;
                result = new OriginLookupResult(null,
                    "Origin lookup timed out. Approximate country locations remain; use Retry origins.");
            }
            catch (Exception error) when (IsRecoverable(error))
            {
                logger.LogWarning(error, "Native origin lookup failed; retaining approximate country coordinates");
                response = null;
                result = new OriginLookupResult(null,
                    "Some detailed origins could not be resolved or validated. Approximate country locations remain; use Retry origins.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                using var body = new MemoryStream();
                using (var writer = new Utf8JsonWriter(body))
                {
                    writer.WriteStartObject();
                    if (response != null) writer.WriteString("response", response);
                    else writer.WriteString("failure", result.Error);
                    writer.WriteEndObject();
                }
                var temporaryPath = cachePath + ".tmp";
                try
                {
                    await File.WriteAllBytesAsync(temporaryPath, body.ToArray(), cancellationToken).ConfigureAwait(false);
                    File.Move(temporaryPath, cachePath, overwrite: true);
                }
                finally
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
            }
            catch (Exception error) when (IsRecoverable(error))
            {
                logger.LogError(error, "Native origin lookup cache could not be persisted");
                result = result with { Error = string.Join(" ", new[] { result.Error,
                    "Origin cache could not be saved. Reopening may require a lookup; use Retry origins." }.OfType<string>()) };
            }
            return _memory[key] = result;
        }
        catch (Exception error) when (IsRecoverable(error))
        {
            logger.LogError(error, "Native origin geocoding configuration or country restriction failed");
            return new OriginLookupResult(null,
                "Origin lookup settings or country restriction are unavailable. Approximate country locations remain; correct settings and use Retry origins.");
        }
        finally
        {
            RequestGate.Release();
        }
    }

    private async Task<(Uri Endpoint, string UserAgent)> ReadSettingsAsync(CancellationToken token)
    {
        var path = Path.Combine(dataDirectory, SettingsFile);
        var endpoint = DefaultEndpoint;
        var userAgent = UserAgent;
        if (File.Exists(path))
        {
            using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(path, token).ConfigureAwait(false));
            endpoint = settings.RootElement.GetProperty("endpoint").GetString() ?? "";
            if (settings.RootElement.TryGetProperty("userAgent", out var agent)) userAgent = agent.GetString() ?? "";
        }
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
            throw new InvalidDataException("Origin geocoding requires an absolute HTTPS search endpoint without a query, fragment or credentials.");
        if (!userAgent.Contains("BaristaNotes", StringComparison.Ordinal) || userAgent.Contains('\r') || userAgent.Contains('\n'))
            throw new InvalidDataException("The origin geocoder requires an identifying BaristaNotes User-Agent.");
        using var validation = new HttpRequestMessage();
        validation.Headers.UserAgent.ParseAdd(userAgent);
        return (uri, userAgent);
    }

    internal static OriginLocation ParseResponse(string json, OriginLookup lookup)
    {
        if (lookup.Country.CountryCode is not { Length: 2 })
            throw new InvalidDataException("An ISO country restriction is required to validate an origin.");
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("The geocoder response is not a search-result array.");
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.Object ||
                !address.TryGetProperty("country_code", out var code) || code.ValueKind != JsonValueKind.String ||
                !string.Equals(code.GetString(), lookup.Country.CountryCode, StringComparison.OrdinalIgnoreCase) ||
                !item.TryGetProperty("lat", out var lat) || !item.TryGetProperty("lon", out var lon) ||
                !Coordinate(lat, -85, 85, out var latitude) || !Coordinate(lon, -180, 180, out var longitude) ||
                !item.TryGetProperty("place_rank", out var rank) || rank.ValueKind != JsonValueKind.Number ||
                !rank.TryGetInt32(out var placeRank) ||
                placeRank <= 4 || placeRank > 22) continue;
            var category = String(item, "category");
            var type = String(item, "addresstype");
            if (category is not ("boundary" or "place")) continue;
            var precision = type switch
            {
                "city" => OriginPrecision.City,
                "town" or "village" or "hamlet" => OriginPrecision.Locality,
                "state" or "state_district" or "county" or "region" or "district" or "municipality"
                    or "administrative" or "province" => OriginPrecision.Region,
                _ => OriginPrecision.ApproximateCountry
            };
            if (precision == OriginPrecision.ApproximateCountry) continue;
            if ((precision is OriginPrecision.City or OriginPrecision.Locality) && placeRank < 13) continue;
            var name = String(item, "name");
            if (name.Length == 0) name = String(address, type);
            if (name.Length == 0) continue;
            var wanted = BeanOriginMapData.Normalize(lookup.Detail).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word is not ("region" or "district" or "province" or "city" or "zone")).ToArray();
            var normalizedName = " " + BeanOriginMapData.Normalize(name) + " ";
            // A hierarchy match alone must not accept a different named place in the requested region.
            if (wanted.Length == 0 || !normalizedName.Contains(" " + wanted[0] + " ", StringComparison.Ordinal)) continue;
            var hierarchy = " " + BeanOriginMapData.Normalize(name + " " + string.Join(" ",
                address.EnumerateObject().Where(property => property.Name is not ("country" or "country_code") &&
                    property.Value.ValueKind == JsonValueKind.String).Select(property => property.Value.GetString()))) + " ";
            if (!wanted.All(word => hierarchy.Contains(" " + word + " ", StringComparison.Ordinal))) continue;
            return new OriginLocation(name, longitude, latitude, precision);
        }
        throw new InvalidDataException("No result matched the requested country, named place and city/region precision.");
    }

    private static string String(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    private static bool Coordinate(JsonElement value, double minimum, double maximum, out double coordinate)
    {
        coordinate = double.NaN;
        var parsed = value.ValueKind == JsonValueKind.String
            ? double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate)
            : value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out coordinate);
        return parsed && double.IsFinite(coordinate) && coordinate >= minimum && coordinate <= maximum;
    }

    private static bool IsRecoverable(Exception error) => error is HttpRequestException or IOException or InvalidDataException or
        UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException;
}
