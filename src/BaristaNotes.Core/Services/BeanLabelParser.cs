using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

/// <summary>
/// Parses raw AI model responses into <see cref="BeanLabelExtraction"/> results.
/// Tolerates markdown-fenced JSON and trailing prose.
/// </summary>
public static class BeanLabelParser
{
    /// <summary>
    /// Parses a raw model response into a <see cref="BeanLabelExtraction"/>.
    /// Never throws: returns Success=false on any parse failure.
    /// </summary>
    public static BeanLabelExtraction ParseResponse(string? rawResponse)
    {
        var result = new BeanLabelExtraction { RawResponse = rawResponse };

        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            result.Success = false;
            result.ErrorMessage = "Empty response from model.";
            return result;
        }

        var json = ExtractJson(rawResponse);
        if (json is null)
        {
            result.Success = false;
            result.ErrorMessage = "No JSON object found in response.";
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            result.Name = GetStringOrNull(root, "name");
            result.Roaster = GetStringOrNull(root, "roaster");
            result.Origin = GetStringOrNull(root, "origin");
            result.RoastDate = GetDateOrNull(root, "roastDate");
            result.Success = true;
            return result;
        }
        catch (JsonException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Invalid JSON: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Extracts a JSON object from the given text. Strips markdown fences and
    /// extracts the first {...} block when prose surrounds the JSON.
    /// </summary>
    internal static string? ExtractJson(string text)
    {
        var trimmed = text.Trim();

        // Strip markdown code fences (```json ... ``` or ``` ... ```)
        var fenceMatch = Regex.Match(
            trimmed,
            @"```(?:json)?\s*(?<body>[\s\S]*?)\s*```",
            RegexOptions.IgnoreCase);
        if (fenceMatch.Success)
        {
            trimmed = fenceMatch.Groups["body"].Value.Trim();
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        // Extract first balanced {...} block (tolerant of prose before/after).
        var start = trimmed.IndexOf('{');
        if (start < 0)
        {
            return null;
        }

        var depth = 0;
        var inString = false;
        var escape = false;
        for (var i = start; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (escape)
            {
                escape = false;
                continue;
            }
            if (c == '\\')
            {
                escape = true;
                continue;
            }
            if (c == '"')
            {
                inString = !inString;
                continue;
            }
            if (inString)
            {
                continue;
            }
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return trimmed.Substring(start, i - start + 1);
                }
            }
        }

        return null;
    }

    private static string? GetStringOrNull(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }
        if (prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }
        if (prop.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        var s = prop.GetString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    private static DateTime? GetDateOrNull(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }
        if (prop.ValueKind == JsonValueKind.Null || prop.ValueKind == JsonValueKind.Undefined)
        {
            return null;
        }
        if (prop.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        var s = prop.GetString();
        if (string.IsNullOrWhiteSpace(s))
        {
            return null;
        }

        // Strict YYYY-MM-DD preferred; be tolerant of a few common variants.
        string[] formats =
        {
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "MM/dd/yyyy",
            "M/d/yyyy",
        };
        if (DateTime.TryParseExact(s.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return dt.Date;
        }
        if (DateTime.TryParse(s.Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
        {
            return dt.Date;
        }
        return null;
    }
}
