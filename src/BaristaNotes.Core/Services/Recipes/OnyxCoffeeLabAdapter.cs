using System.Globalization;
using System.Text.RegularExpressions;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BaristaNotes.Core.Services.Recipes;

/// <summary>
/// Adapter for Onyx Coffee Lab. Onyx publishes per-bean brewing guides on each
/// product page (e.g. Monarch) with espresso and pour-over "recipes" — dose,
/// yield, time, and a grind setting hint.
///
/// This adapter finds the product page for the bean and parses the
/// recipe snippets. It is intentionally tolerant: on any structural change
/// it returns an empty list and the sourcing pipeline falls back to AI.
/// </summary>
public sealed class OnyxCoffeeLabAdapter : HttpRoasterRecipeAdapterBase
{
    public override string Id => "onyx";
    public override string RoasterName => "Onyx Coffee Lab";

    private const string BaseUrl = "https://onyxcoffeelab.com";

    public OnyxCoffeeLabAdapter(HttpClient httpClient)
        : base(httpClient, NullLogger<OnyxCoffeeLabAdapter>.Instance)
    {
    }

    public OnyxCoffeeLabAdapter(HttpClient httpClient, ILogger<OnyxCoffeeLabAdapter> logger)
        : base(httpClient, logger)
    {
    }

    public override bool CanHandle(Bean bean)
    {
        if (string.IsNullOrWhiteSpace(bean.Roaster)) return false;
        var r = bean.Roaster.Trim().ToLowerInvariant();
        // Accept "Onyx", "Onyx Coffee", "Onyx Coffee Lab"
        return r.StartsWith("onyx", StringComparison.Ordinal);
    }

    protected override async Task<IReadOnlyList<ScrapedRecipe>> FetchCoreAsync(
        Bean bean, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bean.Name))
            return Array.Empty<ScrapedRecipe>();

        var url = BuildProductUrl(bean.Name);
        var html = await TryGetHtmlAsync(url, ct);
        if (html == null)
            return Array.Empty<ScrapedRecipe>();

        return ParseRecipes(html, url);
    }

    internal static string BuildProductUrl(string beanName)
    {
        // Onyx product slugs are lowercased with hyphens and no punctuation.
        var slug = Slugify(beanName);
        return $"{BaseUrl}/products/{slug}";
    }

    private static string Slugify(string input)
    {
        var lower = input.Trim().ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lower.Length);
        var lastDash = false;
        foreach (var ch in lower)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }
        return sb.ToString().Trim('-');
    }

    /// <summary>
    /// Exposed internal for testing. Parses the HTML for recipe blocks.
    /// Strategy: find sections whose heading or class matches "espresso" /
    /// "pour over" / "drip" etc, then pull dose/yield/time/grind.
    /// </summary>
    internal static IReadOnlyList<ScrapedRecipe> ParseRecipes(string html, string sourceUrl)
    {
        var result = new List<ScrapedRecipe>();

        foreach (var (method, headingPattern) in MethodHeadingPatterns)
        {
            var block = ExtractSectionBody(html, headingPattern);
            if (block == null) continue;

            var recipe = ParseSection(block, method, sourceUrl);
            if (recipe != null) result.Add(recipe);
        }

        return result;
    }

    private static readonly (BrewMethod Method, string HeadingPattern)[] MethodHeadingPatterns =
    {
        (BrewMethod.Espresso,    @"espresso"),
        (BrewMethod.PourOver,    @"pour\s*over|v60|chemex"),
        (BrewMethod.Drip,        @"\bdrip\b|\bbatch\b"),
        (BrewMethod.Aeropress,   @"aeropress"),
        (BrewMethod.FrenchPress, @"french\s*press"),
        (BrewMethod.Moka,        @"moka"),
    };

    // All scraping regexes get a safety timeout + bounded quantifiers to
    // avoid catastrophic backtracking on hostile/malformed HTML from a
    // compromised or unexpectedly laid out roaster site.
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex DoseYieldTime = new(
        @"(?<dose>\d{1,3}(?:\.\d+)?)\s*(?:g|grams?)\s*[:\-→to/]+\s*(?<yield>\d{1,4}(?:\.\d+)?)\s*(?:g|grams?|ml)\s*(?:[^0-9]{0,40}(?<time>\d{1,3}(?:\.\d+)?)\s*(?:s|sec|seconds?))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex DoseWaterNoYield = new(
        @"(?<dose>\d{1,3}(?:\.\d+)?)\s*(?:g|grams?)\s*[^0-9]{1,10}(?<water>\d{2,4})\s*(?:g|grams?|ml)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex TempC = new(
        @"(?<temp>\d{2,3}(?:\.\d+)?)\s*°?\s*C\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex TempF = new(
        @"(?<temp>\d{2,3}(?:\.\d+)?)\s*°?\s*F\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex TimeSeconds = new(
        @"(?<mm>\d{1,2})\s*[:]\s*(?<ss>\d{2})|(?<sec>\d{1,3})\s*(?:s|sec|seconds?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private static readonly Regex GrindHint = new(
        @"grind[^<:]{0,20}[:\-]\s*(?<hint>[^<\r\n.]{3,40})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        RegexTimeout);

    private static ScrapedRecipe? ParseSection(string block, BrewMethod method, string sourceUrl)
    {
        var text = StripHtml(block);

        decimal? dose = null;
        decimal? yield = null;
        decimal? time = null;
        decimal? tempC = null;
        string? grind = null;

        // Regex timeouts throw RegexMatchTimeoutException; treat those as
        // "couldn't parse" and return no recipe rather than letting a
        // pathological roaster-site HTML payload take down the sourcing task.
        try
        {        var dyt = DoseYieldTime.Match(text);
        if (dyt.Success)
        {
            dose = ParseDecimal(dyt.Groups["dose"].Value);
            yield = ParseDecimal(dyt.Groups["yield"].Value);
            if (dyt.Groups["time"].Success)
                time = ParseDecimal(dyt.Groups["time"].Value);
        }
        else
        {
            var dw = DoseWaterNoYield.Match(text);
            if (dw.Success)
            {
                dose = ParseDecimal(dw.Groups["dose"].Value);
                yield = ParseDecimal(dw.Groups["water"].Value);
            }
        }

        if (!time.HasValue)
        {
            var ts = TimeSeconds.Match(text);
            if (ts.Success)
            {
                if (ts.Groups["mm"].Success)
                {
                    var mm = int.Parse(ts.Groups["mm"].Value, CultureInfo.InvariantCulture);
                    var ss = int.Parse(ts.Groups["ss"].Value, CultureInfo.InvariantCulture);
                    time = mm * 60 + ss;
                }
                else if (ts.Groups["sec"].Success)
                {
                    time = ParseDecimal(ts.Groups["sec"].Value);
                }
            }
        }

        var tempCMatch = TempC.Match(text);
        if (tempCMatch.Success)
        {
            tempC = ParseDecimal(tempCMatch.Groups["temp"].Value);
        }
        else
        {
            var tempFMatch = TempF.Match(text);
            if (tempFMatch.Success)
            {
                var f = ParseDecimal(tempFMatch.Groups["temp"].Value);
                if (f.HasValue)
                    tempC = Math.Round((f.Value - 32m) * 5m / 9m, 1);
            }
        }

        var grindMatch = GrindHint.Match(text);
        if (grindMatch.Success)
            grind = grindMatch.Groups["hint"].Value.Trim();

        }
        catch (RegexMatchTimeoutException)
        {
            // Hostile/pathological HTML — bail out and let the sourcing
            // pipeline fall through to the AI fallback or empty result.
            return null;
        }

        // If we couldn't find any useful data, skip this section.
        if (!dose.HasValue && !yield.HasValue && !time.HasValue && grind == null)
            return null;

        return new ScrapedRecipe
        {
            BrewMethod = method,
            Title = $"{method.DisplayName()} recipe (Onyx)",
            SourceUrl = sourceUrl,
            DoseIn = dose,
            OutputAmount = yield,
            GrindHint = grind,
            BrewTempC = tempC,
            TotalTimeSeconds = time,
            Notes = null
        };
    }

    /// <summary>
    /// Pulls the HTML content between a heading matching <paramref name="headingPattern"/>
    /// and the next heading of equal-or-higher level (or end of document).
    /// Returns null if no matching heading is present.
    /// </summary>
    internal static string? ExtractSectionBody(string html, string headingPattern)
    {
        var headingRegex = new Regex(
            $@"<h([1-4])[^>]*>\s*(?:<[^>]+>\s*)*([^<]*?(?:{headingPattern})[^<]*?)(?:\s*<[^>]+>)*\s*</h\1\s*>",
            RegexOptions.IgnoreCase,
            RegexTimeout);
        var match = headingRegex.Match(html);
        if (!match.Success) return null;

        var startIdx = match.Index + match.Length;
        var level = match.Groups[1].Value;
        var endRegex = new Regex($@"<h[1-{level}][^>]*>", RegexOptions.IgnoreCase, RegexTimeout);
        var endMatch = endRegex.Match(html, startIdx);
        var endIdx = endMatch.Success ? endMatch.Index : html.Length;

        return html.Substring(startIdx, endIdx - startIdx);
    }

    private static readonly Regex TagStripper = new("<[^>]+>", RegexOptions.Compiled);

    private static string StripHtml(string html)
    {
        var stripped = TagStripper.Replace(html, " ");
        return System.Net.WebUtility.HtmlDecode(stripped);
    }

    private static decimal? ParseDecimal(string s)
    {
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
            return v;
        return null;
    }
}
