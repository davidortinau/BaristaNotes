using System.Globalization;
using System.Text;
using System.Text.Json;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

public sealed record OriginCountry(string Name, double Longitude, double Latitude, IReadOnlyList<string> Aliases,
    string? CountryCode = null);
public sealed record BeanOriginCountry(OriginCountry Country, IReadOnlyList<BeanDto> Beans);
public enum OriginPrecision { ApproximateCountry, City, Locality, Region }
public sealed record OriginLookup(string Id, OriginCountry Country, string Detail);
public sealed record OriginLocation(string Name, double Longitude, double Latitude, OriginPrecision Precision);
public sealed record BeanOriginPlace(string Id, OriginCountry Country, OriginLocation Location,
    IReadOnlyList<BeanDto> Beans)
{
    public string Label => Location.Precision switch
    {
        OriginPrecision.City or OriginPrecision.Locality => $"{Location.Name}, {Country.Name}",
        OriginPrecision.Region => $"{Location.Name} region, {Country.Name}",
        _ => $"{Country.Name} (approximate country)"
    };
}

public sealed class BeanOriginMapData
{
    private static readonly Lazy<IReadOnlyList<OriginCountry>> Catalogue = new(ReadCountries);
    private static readonly Lazy<(string Text, OriginCountry Country)[]> Aliases = new(() =>
        Catalogue.Value.SelectMany(country => country.Aliases.Append(country.Name)
            .Select(alias => (Text: Normalize(alias), Country: country)))
            .DistinctBy(alias => (alias.Text, alias.Country.Name))
            .OrderByDescending(alias => alias.Text.Length).ToArray());

    public IReadOnlyList<BeanOriginCountry> Countries { get; }
    public IReadOnlyList<BeanDto> MappedBeans { get; }
    public IReadOnlyList<BeanDto> UnmappedBeans { get; }
    public IReadOnlyList<BeanOriginPlace> Places { get; }
    public IReadOnlyList<OriginLookup> Lookups { get; }
    public bool HasAmbiguousBlend { get; }
    public static IReadOnlyList<OriginCountry> CountryCatalogue => Catalogue.Value;

    public BeanOriginMapData(IEnumerable<BeanDto> savedNonDeletedBeans,
        IReadOnlyDictionary<string, OriginLocation>? resolvedLocations = null)
    {
        var mapped = new List<BeanDto>();
        var unmapped = new List<BeanDto>();
        var groups = new Dictionary<string, (OriginCountry Country, List<BeanDto> Beans)>(StringComparer.Ordinal);
        var places = new Dictionary<string, BeanOriginPlace>(StringComparer.Ordinal);
        var lookups = new Dictionary<string, OriginLookup>(StringComparer.Ordinal);
        foreach (var bean in savedNonDeletedBeans.DistinctBy(bean => bean.Id))
        {
            var countries = Resolve(bean.Origin);
            if (countries.Count == 0) { unmapped.Add(bean); continue; }
            mapped.Add(bean);
            foreach (var country in countries)
            {
                if (!groups.TryGetValue(country.Name, out var group))
                    groups.Add(country.Name, group = (country, []));
                group.Beans.Add(bean);
                var details = DetailsForCountry(bean.Origin!, country, countries.Count, out var ambiguous);
                HasAmbiguousBlend |= ambiguous;
                foreach (var detail in details)
                {
                    var id = detail.Length == 0 ? country.Name : $"{country.Name}|{Normalize(detail)}";
                    if (detail.Length > 0) lookups.TryAdd(id, new OriginLookup(id, country, detail));
                    var location = resolvedLocations != null && resolvedLocations.TryGetValue(id, out var resolved)
                        ? resolved : new OriginLocation(country.Name, country.Longitude, country.Latitude,
                            OriginPrecision.ApproximateCountry);
                    if (!places.TryGetValue(id, out var place))
                        places.Add(id, new BeanOriginPlace(id, country, location, [bean]));
                    else
                        places[id] = place with { Beans = place.Beans.Append(bean).ToArray() };
                }
            }
        }
        Countries = groups.Values.OrderBy(group => group.Country.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new BeanOriginCountry(group.Country, Sort(group.Beans))).ToArray();
        MappedBeans = Sort(mapped);
        UnmappedBeans = Sort(unmapped);
        Places = places.Values.OrderBy(place => place.Id, StringComparer.Ordinal).ToArray();
        Lookups = lookups.Values.OrderBy(lookup => lookup.Id, StringComparer.Ordinal).ToArray();
    }

    public IReadOnlyList<BeanDto> BeansForPlaces(IEnumerable<string> ids)
    {
        var selected = ids.ToHashSet(StringComparer.Ordinal);
        return Sort(Places.Where(place => selected.Contains(place.Id))
            .SelectMany(place => place.Beans).DistinctBy(bean => bean.Id));
    }

    private static string[] DetailsForCountry(string origin, OriginCountry country, int countryCount, out bool ambiguous)
    {
        ambiguous = false;
        var segments = OriginSegments(origin).ToArray();
        if (segments.Length > 1 && segments.All(segment => Resolve(segment).Count == 1))
        {
            return segments.Where(segment => Resolve(segment)[0].Name == country.Name)
                .Select(RemoveCountryNames).Distinct(StringComparer.Ordinal).ToArray();
        }
        var detail = RemoveCountryNames(origin);
        if (countryCount == 1) return [detail];
        ambiguous = detail.Length > 0;
        return [""];
    }

    private static IEnumerable<string> OriginSegments(string origin) =>
        origin.Split(['/', ';', '|', '+'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(SplitConjunctions);

    private static IEnumerable<string> SplitConjunctions(string segment)
    {
        var text = " " + string.Join(" , ", segment.Split(',').Select(Normalize)) + " ";
        var countries = MatchCountries(text).ToArray();
        var start = 0;
        var index = text.IndexOf(" and ", StringComparison.Ordinal);
        while (index >= 0)
        {
            if (!countries.Any(country => index + 1 >= country.Start && index + 4 <= country.Start + country.Length))
            {
                var part = text[start..index].Trim();
                if (part.Length > 0) yield return part;
                start = index + 5;
            }
            index = text.IndexOf(" and ", index + 4, StringComparison.Ordinal);
        }
        var remaining = text[start..].Trim();
        if (remaining.Length > 0) yield return remaining;
    }

    private static bool IsCountryName(string normalized) =>
        Aliases.Value.Any(alias => alias.Text == normalized);

    private static IEnumerable<(string Text, bool IsPlace)> CountryComponents(string text)
    {
        var components = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize).ToArray();
        for (var index = 0; index < components.Length; index++)
        {
            // Only the place paired with an explicit country is protected; other country matches remain.
            var isPlace = !IsCountryName(components[index]) && index + 1 < components.Length &&
                IsCountryName(components[index + 1]);
            yield return (components[index], isPlace);
        }
    }

    private static string RemoveCountryNames(string text)
    {
        var remaining = new List<string>();
        foreach (var component in CountryComponents(text))
        {
            var normalized = " " + component.Text + " ";
            if (!component.IsPlace)
            {
                foreach (var alias in Aliases.Value)
                {
                    var needle = " " + alias.Text + " ";
                    while (normalized.Contains(needle, StringComparison.Ordinal))
                        normalized = normalized.Replace(needle, " ", StringComparison.Ordinal);
                }
            }
            remaining.Add(normalized);
        }
        return string.Join(" ", string.Join(" ", remaining).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word is not ("and" or "blend")));
    }

    public IReadOnlyList<BeanDto> BeansForCountries(IEnumerable<string> names)
    {
        var selected = names.ToHashSet(StringComparer.Ordinal);
        return Sort(Countries.Where(group => selected.Contains(group.Country.Name))
            .SelectMany(group => group.Beans).DistinctBy(bean => bean.Id));
    }

    public static IReadOnlyList<OriginCountry> Resolve(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return [];
        return OriginSegments(origin).SelectMany(segment =>
                MatchCountries(" " + string.Join(" ", CountryComponents(segment)
                    .Where(component => !component.IsPlace).Select(component => component.Text)) + " "))
            .Select(match => match.Country)
            .DistinctBy(country => country.Name)
            .OrderBy(country => country.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<(OriginCountry Country, int Start, int Length)> MatchCountries(string text)
    {
        var occupied = new bool[text.Length];
        // Longest non-overlapping names prevent Guinea/Congo/Sudan matching inside another country's name.
        foreach (var alias in Aliases.Value)
        {
            var needle = " " + alias.Text + " ";
            var index = text.IndexOf(needle, StringComparison.Ordinal);
            while (index >= 0)
            {
                var start = index + 1;
                var end = start + alias.Text.Length;
                if (!occupied.AsSpan(start, alias.Text.Length).Contains(true))
                {
                    occupied.AsSpan(start, alias.Text.Length).Fill(true);
                    yield return (alias.Country, start, alias.Text.Length);
                }
                index = text.IndexOf(needle, end, StringComparison.Ordinal);
            }
        }
    }

    private static BeanDto[] Sort(IEnumerable<BeanDto> beans) =>
        beans.OrderBy(bean => bean.Name, StringComparer.OrdinalIgnoreCase).ThenBy(bean => bean.Id).ToArray();

    internal static string Normalize(string text)
    {
        var normalized = new StringBuilder();
        foreach (var character in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) normalized.Append(char.ToLowerInvariant(character));
            else if (normalized.Length > 0 && normalized[^1] != ' ') normalized.Append(' ');
        }
        return normalized.ToString().Trim();
    }

    private static IReadOnlyList<OriginCountry> ReadCountries()
    {
        using var stream = typeof(BeanOriginMapData).Assembly.GetManifestResourceStream("BaristaNotes.OriginCountries.json")
            ?? throw new InvalidOperationException("The origin country catalogue is missing.");
        using var document = JsonDocument.Parse(stream);
        var codes = document.RootElement.GetProperty("country_codes");
        return document.RootElement.GetProperty("countries").EnumerateArray().Select(country =>
            new OriginCountry(country.GetProperty("name").GetString()
                ?? throw new InvalidOperationException("An origin country name is missing."),
                country.GetProperty("longitude").GetDouble(), country.GetProperty("latitude").GetDouble(),
                country.GetProperty("aliases").EnumerateArray().Select(alias => alias.GetString()
                    ?? throw new InvalidOperationException("An origin country alias is missing.")).ToArray(),
                codes.TryGetProperty(country.GetProperty("name").GetString()!, out var code)
                    ? code.GetString() : null)).ToArray();
    }
}
