using System.Globalization;
using System.Text.Json;
using BaristaNotes.Core.Services.DTOs;

namespace BaristaNotes.Core.Services;

public static class PhotoWorkflowAnalysisParser
{
    public static PhotoWorkflowAnalysis ParseResponse(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return PhotoWorkflowAnalysis.Error("Empty response from model.", rawResponse);
        }

        var json = BeanLabelParser.ExtractJson(rawResponse);
        if (json is null)
        {
            return PhotoWorkflowAnalysis.Error("No JSON object found in response.", rawResponse);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var isObvious = root.TryGetProperty("isObvious", out var obviousElement)
                && obviousElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                && obviousElement.GetBoolean();
            var intent = ParseIntent(GetString(root, "intent"));
            isObvious = isObvious && intent != PhotoWorkflowIntent.Unknown;

            if (!isObvious)
            {
                intent = PhotoWorkflowIntent.Unknown;
            }

            BeanLabelExtraction? coffeeDetails = null;
            if (intent == PhotoWorkflowIntent.Coffee)
            {
                coffeeDetails = new BeanLabelExtraction
                {
                    Success = true,
                    Name = GetString(root, "name"),
                    Roaster = GetString(root, "roaster"),
                    Origin = GetString(root, "origin"),
                    RoastDate = GetDate(root, "roastDate"),
                    Notes = GetString(root, "notes"),
                    RawResponse = rawResponse
                };
            }

            return new PhotoWorkflowAnalysis
            {
                Success = true,
                Intent = intent,
                IsObvious = isObvious,
                Rationale = GetString(root, "rationale"),
                CoffeeDetails = coffeeDetails,
                RawResponse = rawResponse
            };
        }
        catch (JsonException ex)
        {
            return PhotoWorkflowAnalysis.Error($"Invalid JSON: {ex.Message}", rawResponse);
        }
    }

    private static PhotoWorkflowIntent ParseIntent(string? intent)
        => intent?.Trim().ToLowerInvariant() switch
        {
            "coffee" => PhotoWorkflowIntent.Coffee,
            "profile" => PhotoWorkflowIntent.Profile,
            "room" => PhotoWorkflowIntent.Room,
            _ => PhotoWorkflowIntent.Unknown
        };

    private static string? GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTime? GetDate(JsonElement root, string propertyName)
    {
        var value = GetString(root, propertyName);
        if (value is null)
        {
            return null;
        }

        return DateTime.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var date)
            ? date.Date
            : null;
    }
}
