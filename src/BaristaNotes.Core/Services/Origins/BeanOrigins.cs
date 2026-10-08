using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services.Origins;

public sealed record OriginCountry
{
    public string Name { get; init; } = "";
    public string[] Aliases { get; init; } = [];
    public string? CountryCode { get; init; }
    public double Longitude { get; init; }
    public double Latitude { get; init; }
}

public sealed record CountryBeans(OriginCountry Country, IReadOnlyList<BeanDto> Beans);

public sealed record BeanOriginSnapshot(
    IReadOnlyList<CountryBeans> Countries,
    IReadOnlyList<BeanDto> MappedBeans,
    IReadOnlyList<BeanDto> UnmappedBeans)
{
    public IReadOnlyList<OriginLocation> Locations { get; init; } = [];
}

public sealed class BeanOriginResolver
{
    private readonly (string Name, OriginCountry Country)[] _aliases;
    public IReadOnlyList<OriginCountry> Countries { get; }

    public BeanOriginResolver()
    {
        using var stream = typeof(BeanOriginResolver).Assembly
            .GetManifestResourceStream("BaristaNotes.Core.origin-countries.json")
            ?? throw new InvalidOperationException("The country origin catalog is missing.");
        Countries = JsonSerializer.Deserialize(stream, OriginJsonContext.Default.OriginCountryArray)
            ?? throw new InvalidDataException("The country origin catalog is empty.");
        using var codesStream = typeof(BeanOriginResolver).Assembly
            .GetManifestResourceStream("BaristaNotes.Core.origin-country-codes.json")
            ?? throw new InvalidOperationException("The country code catalog is missing.");
        var codes = JsonSerializer.Deserialize(codesStream, OriginJsonContext.Default.DictionaryStringString)
            ?? throw new InvalidDataException("The country code catalog is empty.");
        Countries = Countries.Select(country =>
            country with { CountryCode = codes.GetValueOrDefault(country.Name) }).ToArray();
        if (codes.Any(pair => !Countries.Any(country => country.Name == pair.Key) ||
                pair.Value.Length != 2 || pair.Value.Any(character => character is < 'a' or > 'z')))
            throw new InvalidDataException("The country code catalog is invalid.");
        if (Countries.Count != 177 || Countries.Any(country =>
                string.IsNullOrWhiteSpace(country.Name) ||
                !double.IsFinite(country.Longitude) || !double.IsFinite(country.Latitude) ||
                Math.Abs(country.Longitude) > 180 || Math.Abs(country.Latitude) > 90))
            throw new InvalidDataException("The country origin catalog is invalid.");

        _aliases = Countries.SelectMany(country => country.Aliases.Append(country.Name)
                .Select(Normalize).Distinct()
                .Select(name => (Name: name, Country: country)))
            .OrderByDescending(alias => alias.Name.Length).ToArray();
    }

    public IReadOnlyList<OriginCountry> Resolve(string? origin)
    {
        var text = " " + Normalize(origin) + " ";
        var matches = new List<(int Start, int End, OriginCountry Country)>();
        foreach (var alias in _aliases)
        {
            var needle = " " + alias.Name + " ";
            var start = 0;
            while ((start = text.IndexOf(needle, start, StringComparison.Ordinal)) >= 0)
            {
                var end = start + needle.Length - 1;
                // Prefer full country names over overlapping names (e.g. Guinea in Papua New Guinea).
                if (!matches.Any(match => start < match.End && end > match.Start))
                    matches.Add((start, end, alias.Country));
                start++;
            }
        }
        return matches.Select(match => match.Country).DistinctBy(country => country.Name)
            .OrderBy(country => country.Name, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<OriginQuery> Queries(string? origin)
    {
        var countries = Resolve(origin);
        if (countries.Count == 0)
            return [];

        // Only unambiguous blend components can become detailed places.
        var components = (origin ?? "").Split(['/', ';', '|', '+', '&'],
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (countries.Count == 1)
        {
            var whole = Query(origin!, countries[0]);
            if (!whole.NeedsLookup || components.Length < 2 || components.Any(component => Resolve(component).Count != 1))
                return [whole];
            return components.Select(component => Query(component, countries[0]))
                .DistinctBy(query => query.Key).ToArray();
        }
        return countries.SelectMany(country =>
        {
            var matching = components.Where(component =>
                Resolve(component) is { Count: 1 } resolved && resolved[0].Name == country.Name).ToArray();
            return matching.Length == 0
                ? new[] { Query(country.Name, country) }
                : matching.Select(component => Query(component, country)).ToArray();
        }).DistinctBy(query => query.Key).ToArray();
    }

    private static OriginQuery Query(string origin, OriginCountry country)
    {
        var detail = " " + Normalize(origin) + " ";
        foreach (var alias in country.Aliases.Append(country.Name)
                     .Select(Normalize).OrderByDescending(alias => alias.Length))
        {
            var needle = " " + alias + " ";
            while (detail.Contains(needle, StringComparison.Ordinal))
                detail = detail.Replace(needle, " ", StringComparison.Ordinal);
        }
        detail = Normalize(detail);
        var key = detail.Length == 0 ? "country:" + country.Name : "place:" + country.Name + ":" + Normalize(origin);
        return new OriginQuery(key, origin.Trim(), detail, country);
    }

    public BeanOriginSnapshot Build(IEnumerable<BeanDto> savedBeans,
        IReadOnlyDictionary<string, OriginLookup>? lookups = null)
    {
        var countries = new Dictionary<string, (OriginCountry Country, List<BeanDto> Beans)>();
        var locations = new Dictionary<string, OriginLocation>();
        var mapped = new List<BeanDto>();
        var unmapped = new List<BeanDto>();
        foreach (var bean in savedBeans.DistinctBy(bean => bean.Id)
                     .OrderBy(bean => bean.Name, StringComparer.OrdinalIgnoreCase))
        {
            var origins = Resolve(bean.Origin);
            if (origins.Count == 0)
                unmapped.Add(bean);
            else
                mapped.Add(bean);
            foreach (var country in origins)
            {
                if (!countries.TryGetValue(country.Name, out var group))
                    countries[country.Name] = group = (country, []);
                group.Beans.Add(bean);
            }
            foreach (var query in Queries(bean.Origin))
            {
                var place = lookups?.GetValueOrDefault(query.Key)?.Place;
                if (place is not null && !NominatimOriginGeocoder.IsUsable(place, query))
                    throw new InvalidDataException("A resolved origin does not match its country or place.");
                if (!locations.TryGetValue(query.Key, out var location))
                    location = new OriginLocation(query.Key, query.Country,
                        place?.Name ?? query.Country.Name,
                        place?.Longitude ?? query.Country.Longitude,
                        place?.Latitude ?? query.Country.Latitude,
                        place?.Precision ?? OriginPrecision.ApproximateCountry, []);
                locations[query.Key] = location with { Beans = location.Beans.Append(bean).ToArray() };
            }
        }
        return new BeanOriginSnapshot(
            countries.Values.OrderBy(group => group.Country.Name, StringComparer.Ordinal)
                .Select(group => new CountryBeans(group.Country, group.Beans)).ToArray(),
            mapped, unmapped)
        {
            Locations = locations.Values.OrderBy(location => location.Key, StringComparer.Ordinal).ToArray()
        };
    }

    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var result = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(character))
                result.Append(char.ToLowerInvariant(character));
            else if (result.Length > 0 && result[^1] != ' ')
                result.Append(' ');
        }
        return result.ToString().Trim();
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OriginCountry[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class OriginJsonContext : JsonSerializerContext
{
}
