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
    ///
    /// Strategy (in order):
    /// 1. <b>Onyx brew-guide blocks</b> (primary): each Onyx product page
    ///    embeds espresso + filter brew guides keyed by Wistia video ids of
    ///    the form <c>wistia_id_brew_guide_{filter|espresso}_en</c>, wrapped
    ///    in a <c>&lt;div class="guide-body" data-guide="..."&gt;</c>
    ///    element. Inside we parse <c>Coffee: Ng</c>, <c>Water/Yield: Mg</c>,
    ///    <c>@ NNN°F</c>, a micron grind hint, and the nearest
    ///    <c>data-duration="..."</c> seconds.
    /// 2. <b>Heading fallback</b>: if no brew-guide blocks are present, fall
    ///    back to the older heading-based scraper. This keeps the adapter
    ///    useful on unusual pages, e.g. blog posts, and keeps existing tests
    ///    with simple <c>&lt;h2&gt;Espresso&lt;/h2&gt;</c> markup working.
    /// </summary>
    internal static IReadOnlyList<ScrapedRecipe> ParseRecipes(string html, string sourceUrl)
    {
        var onyxRecipes = ParseOnyxBrewGuides(html, sourceUrl);
        if (onyxRecipes.Count > 0) return onyxRecipes;

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

    // --- Onyx brew-guide block parser --------------------------------------
    // NOTE: RegexTimeout is the source-of-truth match timeout; it is declared
    // below in the heading-parser section but must be referenced by the
    // regexes in this block. Because static field initializers run top-to-
    // bottom, we inline the timeout literal here to avoid reading a zero
    // value from an uninitialized field (which would throw
    // ArgumentOutOfRangeException at type load).

    private static readonly TimeSpan OnyxRegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex WistiaGuideMarker = new(
        @"wistia_id_brew_guide_(?<method>filter|espresso)_en",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        OnyxRegexTimeout);

    private static readonly Regex CoffeeLine = new(
        @"Coffee\s*:\s*(?<dose>\d{1,3}(?:\.\d+)?)\s*g",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        OnyxRegexTimeout);

    private static readonly Regex WaterOrYieldLine = new(
        @"(?:Water|Yield)\s*:\s*(?<amt>\d{1,4}(?:\.\d+)?)\s*g(?:[^<\r\n]{0,40}@\s*(?<temp>\d{2,3}(?:\.\d+)?)\s*°?\s*(?<unit>[CF]))?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        OnyxRegexTimeout);

    private static readonly Regex MicronGrind = new(
        @"(?<um>\d{2,4})\s*(?:µm|um|microns?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        OnyxRegexTimeout);

    private static readonly Regex StrongGrindBlock = new(
        @"<strong>\s*Grind\s*</strong>\s*(?:<br[^>]*>|\s|:)*\s*(?<hint>[^<\r\n]{1,60})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        OnyxRegexTimeout);

    private static readonly Regex DataDuration = new(
        @"data-duration\s*=\s*""(?<secs>\d{1,4})""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        OnyxRegexTimeout);

    internal static IReadOnlyList<ScrapedRecipe> ParseOnyxBrewGuides(string html, string sourceUrl)
    {
        var recipes = new List<ScrapedRecipe>();
        try
        {
            foreach (Match marker in WistiaGuideMarker.Matches(html))
            {
                var methodToken = marker.Groups["method"].Value.ToLowerInvariant();
                var method = methodToken == "espresso" ? BrewMethod.Espresso : BrewMethod.PourOver;

                var block = ExtractGuideBody(html, marker.Index) ?? SurroundingWindow(html, marker.Index, 4000);
                if (block == null) continue;

                var recipe = ParseOnyxBlock(block, method, sourceUrl);
                if (recipe != null && !recipes.Any(r => r.BrewMethod == recipe.BrewMethod))
                    recipes.Add(recipe);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return Array.Empty<ScrapedRecipe>();
        }
        return recipes;
    }

    /// <summary>
    /// Finds the enclosing <c>&lt;div class="guide-body" ...&gt;...&lt;/div&gt;</c>
    /// that contains the Wistia marker at <paramref name="markerIndex"/>. Uses a
    /// simple bracket-counting scan rather than regex because HTML nesting is
    /// not regex-friendly.
    /// </summary>
    internal static string? ExtractGuideBody(string html, int markerIndex)
    {
        const string openTagToken = "<div class=\"guide-body";
        var start = html.LastIndexOf(openTagToken, markerIndex, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;

        // Walk forward counting <div>/</div> to find the matching closer.
        var depth = 0;
        var i = start;
        while (i < html.Length)
        {
            var nextOpen = html.IndexOf("<div", i, StringComparison.OrdinalIgnoreCase);
            var nextClose = html.IndexOf("</div>", i, StringComparison.OrdinalIgnoreCase);
            if (nextClose < 0) return null;

            if (nextOpen >= 0 && nextOpen < nextClose)
            {
                depth++;
                i = nextOpen + 4;
            }
            else
            {
                depth--;
                i = nextClose + 6;
                if (depth <= 0) return html.Substring(start, i - start);
            }
        }
        return null;
    }

    private static string SurroundingWindow(string html, int index, int radius)
    {
        var start = Math.Max(0, index - radius);
        var end = Math.Min(html.Length, index + radius);
        return html.Substring(start, end - start);
    }

    private static ScrapedRecipe? ParseOnyxBlock(string block, BrewMethod method, string sourceUrl)
    {
        decimal? dose = null;
        decimal? yield = null;
        decimal? tempC = null;
        decimal? totalTime = null;
        string? grindHint = null;

        var coffee = CoffeeLine.Match(block);
        if (coffee.Success)
            dose = ParseDecimal(coffee.Groups["dose"].Value);

        // Prefer Yield for espresso; otherwise Water.
        Match? chosen = null;
        foreach (Match m in WaterOrYieldLine.Matches(block))
        {
            // First match is usually authoritative; for espresso the HTML has
            // only "Yield: Ng", for filter "Water: Ng @ NNN°F".
            chosen = m;
            break;
        }
        if (chosen != null)
        {
            yield = ParseDecimal(chosen.Groups["amt"].Value);
            if (chosen.Groups["temp"].Success)
            {
                var t = ParseDecimal(chosen.Groups["temp"].Value);
                if (t.HasValue)
                {
                    tempC = string.Equals(chosen.Groups["unit"].Value, "F", StringComparison.OrdinalIgnoreCase)
                        ? Math.Round((t.Value - 32m) * 5m / 9m, 1)
                        : t.Value;
                }
            }
        }

        var grindStrong = StrongGrindBlock.Match(block);
        if (grindStrong.Success)
        {
            var hint = grindStrong.Groups["hint"].Value.Trim();
            if (hint.Length > 0) grindHint = hint;
        }
        if (grindHint == null)
        {
            var um = MicronGrind.Match(block);
            if (um.Success) grindHint = um.Groups["um"].Value + "µm";
        }

        var dur = DataDuration.Match(block);
        if (dur.Success)
            totalTime = ParseDecimal(dur.Groups["secs"].Value);

        if (!dose.HasValue && !yield.HasValue && !totalTime.HasValue && grindHint == null)
            return null;

        return new ScrapedRecipe
        {
            BrewMethod = method,
            Title = $"{method.DisplayName()} recipe (Onyx)",
            SourceUrl = sourceUrl,
            DoseIn = dose,
            OutputAmount = yield,
            GrindHint = grindHint,
            BrewTempC = tempC,
            TotalTimeSeconds = totalTime,
            Notes = null
        };
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
